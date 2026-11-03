param(
 [Parameter(Mandatory)][string]$Root,
 [Parameter(Mandatory)][string]$Runtime,
 [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
)
$ErrorActionPreference = 'Stop'
$rootFull = [IO.Path]::GetFullPath($Root)
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $rootFull 'FromZero.exe')).ProductVersion.Split('+')[0]
$deps = Get-Content -LiteralPath (Join-Path $rootFull 'FromZero.deps.json') -Raw | ConvertFrom-Json -AsHashtable
$components = @($deps.libraries.GetEnumerator() | Sort-Object Key | ForEach-Object {
 $parts = $_.Key -split '/', 2
 if ($parts.Length -eq 2 -and $parts[0] -ne 'FromZero') {
  $component = [ordered]@{type='library'; name=$parts[0]; version=$parts[1]; 'bom-ref'=$_.Key}
  if ($_.Value.type -eq 'package') { $component.purl = 'pkg:nuget/' + $parts[0] + '@' + $parts[1] }
  $component
 }
})
$sbom = [ordered]@{bomFormat='CycloneDX'; specVersion='1.6'; version=1; metadata=@{component=@{type='application'; name='FromZero'; version=$version}}; components=$components}
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $rootFull 'FromZero.sbom.cdx.json'), ($sbom | ConvertTo-Json -Depth 8), $encoding)
$manifestPath = Join-Path $rootFull 'release-manifest.json'
$signaturePath = Join-Path $rootFull 'release-manifest.sig'
if (Test-Path -LiteralPath $signaturePath) { Remove-Item -LiteralPath $signaturePath -Force }
$files = @(Get-ChildItem -LiteralPath $rootFull -File -Recurse | Sort-Object FullName | ForEach-Object {
 if ($_.FullName -ne $manifestPath) {
  if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Release contains a linked file.' }
  [ordered]@{Path=[IO.Path]::GetRelativePath($rootFull,$_.FullName); Size=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
 }
})
$manifest = [ordered]@{Schema=1; Version=$version; Runtime=$Runtime; Files=$files}
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 6), $encoding)
if ($Certificate) {
 $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
 if (!$rsa) { throw 'The selected certificate has no available RSA signing key.' }
 try {
  $signature = $rsa.SignData([IO.File]::ReadAllBytes($manifestPath),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)
  [IO.File]::WriteAllBytes($signaturePath,$signature)
 } finally { $rsa.Dispose() }
}
