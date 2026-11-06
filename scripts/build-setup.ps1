param(
 [string]$Compiler,
 [string]$SigningCertificateThumbprint,
 [string]$TimestampUrl,
 [switch]$UpdateAssets
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Compiler) { $Compiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (!$Compiler) {
 $Compiler = @(
  (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
  (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
 ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (!$Compiler) { throw 'Install Inno Setup 6.7 or later, or pass -Compiler with the path to ISCC.exe.' }
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$dist = Join-Path $root 'dist'
$stage = [IO.Path]::GetFullPath((Join-Path $dist ('setup-stage-' + [Guid]::NewGuid().ToString('N'))))
$payload = Join-Path $stage 'payload'
if (![IO.Path]::GetDirectoryName($stage).Equals($dist, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected installer staging directory.' }
try {
 $publish = @{Runtime='win-x64'; OutputRoot=$payload; PayloadOnly=$true; SigningCertificateThumbprint=$SigningCertificateThumbprint; TimestampUrl=$TimestampUrl}
 & (Join-Path $PSScriptRoot 'publish.ps1') @publish
 $version = (Get-Item -LiteralPath (Join-Path $payload 'FromZero.exe')).VersionInfo.ProductVersion.Split('+')[0]
 if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Unexpected FromZero package version.' }
 if ($UpdateAssets) { & (Join-Path $PSScriptRoot 'write-update-assets.ps1') -Payload $payload -Output $dist }
 & $Compiler "/DPayloadDir=$payload" "/DAppVersion=$version" "/DSetupOutput=$stage" (Join-Path $root 'installer\FromZero.iss')
 if ($LASTEXITCODE -ne 0) { throw "Inno Setup exited with code $LASTEXITCODE." }
 $name = 'FromZero-Setup-win-x64.exe'
 $built = Join-Path $stage $name
 if ($SigningCertificateThumbprint) {
  $cert = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $SigningCertificateThumbprint)
  $options = @{LiteralPath=$built; Certificate=$cert; HashAlgorithm='SHA256'}
  if ($TimestampUrl) { $options.TimestampServer = $TimestampUrl }
  $result = Set-AuthenticodeSignature @options
  if ($result.Status -ne 'Valid') { throw "Installer signature verification failed: $($result.Status)" }
 }
 $setup = Join-Path $dist $name
 Move-Item -LiteralPath $built -Destination $setup -Force
 $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
 Set-Content -LiteralPath ($setup + '.sha256') -Value "$hash  $name" -Encoding ascii
 Write-Output ("Setup {0}: {1}" -f $version,$setup)
} finally {
 if (Test-Path -LiteralPath $stage -PathType Container) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
