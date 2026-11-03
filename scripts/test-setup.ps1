param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$expectedHash = ((Get-Content -LiteralPath ($installerPath + '.sha256') -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Setup checksum mismatch.' }

foreach ($language in @('en','tr')) {
 $target = Join-Path ([IO.Path]::GetTempPath()) ('FromZero-setup-check-' + [Guid]::NewGuid().ToString('N'))
 $argsList = @('/CURRENTUSER','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',"/LANG=$language",('/DIR="' + $target + '"'))
 try {
  $install = Start-Process -FilePath $installerPath -ArgumentList $argsList -WindowStyle Hidden -Wait -PassThru
  if ($install.ExitCode -ne 0) { throw "Setup failed in $language ($($install.ExitCode))." }
  $selected = (Get-Content -LiteralPath (Join-Path $target 'install-language.txt') -Raw).Trim()
  if ($selected -ne $language) { throw "Incorrect installer language marker: $selected." }
  $payload = Join-Path $target 'App'
  $manifest = Get-Content -LiteralPath (Join-Path $payload 'release-manifest.json') -Raw | ConvertFrom-Json
  if ($manifest.Runtime -ne 'win-x64' -or $manifest.Files.Count -lt 4) { throw 'Invalid installed payload.' }
  foreach ($file in $manifest.Files) {
   $path = [IO.Path]::GetFullPath((Join-Path $payload $file.Path))
   if (!$path.StartsWith($payload + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid manifest path.' }
   if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) { throw "Installed payload hash mismatch: $($file.Path)." }
  }
  Write-Output "Verified installed files and $language language marker ($($manifest.Files.Count) files)."
 } finally {
  $uninstaller = Join-Path $target 'unins000.exe'
  if (Test-Path -LiteralPath $uninstaller) {
   $uninstall = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
   if ($uninstall.ExitCode -ne 0) { throw "Uninstall failed ($($uninstall.ExitCode))." }
   if (Test-Path -LiteralPath (Join-Path $target 'App\FromZero.exe')) { throw 'Uninstall left the application executable behind.' }
  }
 }
}
