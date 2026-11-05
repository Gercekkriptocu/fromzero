param([string]$Runtime = 'win-x64', [string]$SigningCertificateThumbprint, [string]$TimestampUrl, [switch]$PayloadOnly, [string]$OutputRoot)
$ErrorActionPreference = 'Stop'
if ($Runtime -notin @('win-x64', 'win-arm64')) { throw 'Desteklenmeyen çalışma mimarisi.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputRoot) { $OutputRoot = Join-Path $projectRoot "dist\FromZero-$Runtime" }
$outputRoot = [IO.Path]::GetFullPath($OutputRoot)
$publisherCertificate = $null
$publisherPublicKey = $null
$buildProperties = @('-p:ContinuousIntegrationBuild=true','-p:FromZeroReleasePublicKey=')
try {
 if ($SigningCertificateThumbprint) {
  if ($SigningCertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'Invalid certificate thumbprint.' }
  $publisherCertificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $SigningCertificateThumbprint)
  $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($publisherCertificate)
  if (!$rsa -or $rsa.KeySize -lt 2048) { throw 'An RSA publisher signing key of at least 2048 bits is required.' }
  try {
   $publisherPublicKey = Join-Path ([IO.Path]::GetTempPath()) ('FromZero-public-' + [Guid]::NewGuid().ToString('N') + '.pem')
   [IO.File]::WriteAllText($publisherPublicKey,$rsa.ExportSubjectPublicKeyInfoPem())
   $buildProperties[-1] = '-p:FromZeroReleasePublicKey=' + $publisherPublicKey
  } finally { $rsa.Dispose() }
 }
 dotnet publish (Join-Path $projectRoot 'src\FromZero\FromZero.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o $outputRoot @buildProperties
 if ($LASTEXITCODE -ne 0) { throw 'Paket oluşturulamadı.' }
} finally {
 if ($publisherPublicKey -and (Test-Path -LiteralPath $publisherPublicKey)) { Remove-Item -LiteralPath $publisherPublicKey -Force }
}
if ($publisherCertificate) {
 $sign = @{LiteralPath=(Join-Path $outputRoot 'FromZero.exe'); Certificate=$publisherCertificate; HashAlgorithm='SHA256'}
 if ($TimestampUrl) { $sign.TimestampServer=$TimestampUrl }
 $signed = Set-AuthenticodeSignature @sign
 if ($signed.Status -ne 'Valid') { throw ('Authenticode verification failed: ' + $signed.Status) }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\KULLANIM.md') -Destination (Join-Path $outputRoot 'KULLANIM.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\GELISIM-0.15.md') -Destination (Join-Path $outputRoot 'GELISIM-0.15.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\SURUM-0.16.md') -Destination (Join-Path $outputRoot 'SURUM-0.16.md') -Force
$releaseVersion = (Get-Item -LiteralPath (Join-Path $outputRoot 'FromZero.exe')).VersionInfo.ProductVersion.Split('.')
$releaseNotesName = 'SURUM-' + $releaseVersion[0] + '.' + $releaseVersion[1] + '.md'
$releaseNotesPath = Join-Path $projectRoot ('docs\' + $releaseNotesName)
if (Test-Path -LiteralPath $releaseNotesPath) { Copy-Item -LiteralPath $releaseNotesPath -Destination (Join-Path $outputRoot $releaseNotesName) -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\AUTOMATIK-KURULUM.md') -Destination (Join-Path $outputRoot 'OTOMATIK-KURULUM.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\HAZIRLIK-0.13.md') -Destination (Join-Path $outputRoot 'HAZIRLIK-0.13.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\Assets\Fonts\OFL.txt') -Destination (Join-Path $outputRoot 'PixelifySans-OFL.txt') -Force
$qrLicense = Join-Path $projectRoot 'src\FromZero\Assets\QRCoder-LICENSE.txt'
Copy-Item -LiteralPath $qrLicense -Destination (Join-Path $outputRoot 'QRCoder-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\Assets\CrosshairCodec-LICENSE.txt') -Destination (Join-Path $outputRoot 'CrosshairCodec-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\Assets\CommunityCatalog-LICENSE.txt') -Destination (Join-Path $outputRoot 'CommunityCatalog-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\Assets\Ascent-SOURCE.txt') -Destination (Join-Path $outputRoot 'Ascent-SOURCE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\Assets\Dust2-SOURCE.txt') -Destination (Join-Path $outputRoot 'Dust2-SOURCE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\Assets\NVIDIA-NVAPI-NOTICE.txt') -Destination (Join-Path $outputRoot 'NVIDIA-NVAPI-NOTICE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\FromZero\ThirdParty\Reclaim\LICENSE.txt') -Destination (Join-Path $outputRoot 'THIRD-PARTY-NOTICES.txt') -Force
foreach ($legacyName in @('ReClaim-LICENSE.txt','ReClaim-KAYNAK.md','DOGRULAMA-indirmeler.json','DOGRULAMA-uretici-sayfalari.json')) {
 $legacyPath = Join-Path $outputRoot $legacyName
 if (Test-Path -LiteralPath $legacyPath) { Remove-Item -LiteralPath $legacyPath -Force }
}
& (Join-Path $PSScriptRoot 'write-release-metadata.ps1') -Root $outputRoot -Runtime $Runtime -Certificate $publisherCertificate
if ($PayloadOnly) { Write-Output "Hazır: $outputRoot"; return }
$archive = Join-Path $projectRoot "dist\FromZero-$Runtime.zip"
$archiveStage = Join-Path $projectRoot ("dist\FromZero-$Runtime-" + [Guid]::NewGuid().ToString('N') + '.partial.zip')
try {
 Compress-Archive -LiteralPath $outputRoot -DestinationPath $archiveStage
 Move-Item -LiteralPath $archiveStage -Destination $archive -Force
} finally {
 if (Test-Path -LiteralPath $archiveStage) { Remove-Item -LiteralPath $archiveStage -Force }
}
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | ForEach-Object { $_.Hash + '  ' + [IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath ($archive + '.sha256')
Write-Output "Hazır: $outputRoot"
Write-Output "Arşiv: $archive"

