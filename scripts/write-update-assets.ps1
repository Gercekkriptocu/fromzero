param(
 [Parameter(Mandatory)][string]$Payload,
 [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $Payload 'release-manifest.json'
$signaturePath = Join-Path $Payload 'release-manifest.sig'
if (!(Test-Path -LiteralPath $signaturePath -PathType Leaf)) { throw 'Online updates require a signed release manifest.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Runtime -ne 'win-x64') { throw 'Unexpected online update architecture.' }
$base = Join-Path $Output 'FromZero-update-win-x64'
$stream = [IO.File]::Create($base + '.pack')
try {
 foreach ($file in $manifest.Files) {
  $path = Join-Path $Payload $file.Path
  if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $file.Sha256) { throw "Payload changed: $($file.Path)" }
  $inputFile = [IO.File]::OpenRead($path)
  try { $inputFile.CopyTo($stream) } finally { $inputFile.Dispose() }
 }
} finally { $stream.Dispose() }
Copy-Item -LiteralPath $manifestPath -Destination ($base + '.json') -Force
Copy-Item -LiteralPath $signaturePath -Destination ($base + '.sig') -Force
