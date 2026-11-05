namespace FromZero;

internal static class OfficeDiscovery
{
 internal const string Script="""
 function Get-OfficeInstallations {
  $result=New-Object 'System.Collections.Generic.List[object]';
  $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase);
  $executables=@('WINWORD.EXE','EXCEL.EXE','POWERPNT.EXE','OUTLOOK.EXE','MSACCESS.EXE','ONENOTE.EXE','VISIO.EXE','WINPROJ.EXE','MSPUB.EXE');
  function Add-OfficeFolder([string]$base,[string]$version,[string]$platform,[string]$products,[string]$source) {
   if(-not $base -or -not [IO.Path]::IsPathRooted($base) -or $base.StartsWith('\\')){return};
   $folders=@($base);
   foreach($major in @('Office14','Office15','Office16')){
    $folders+=Join-Path $base $major;
    $folders+=Join-Path $base ('root\'+$major);
   }
   foreach($folder in $folders){
    $folder=[IO.Path]::GetFullPath($folder).TrimEnd('\');
    if($seen.Contains($folder) -or -not(Test-Path -LiteralPath $folder -PathType Container)){continue};
    $apps=@($executables | Where-Object {Test-Path -LiteralPath (Join-Path $folder $_) -PathType Leaf});
    $ospp=Join-Path $folder 'OSPP.VBS';$subscription=Join-Path $folder 'vnextdiag.ps1';
    if(-not(Test-Path -LiteralPath $ospp -PathType Leaf)){$ospp=''};
    if(-not(Test-Path -LiteralPath $subscription -PathType Leaf)){$subscription=''};
    if($apps.Count -eq 0 -and -not $ospp -and -not $subscription){continue};
    $folderVersion=$version;
    if(-not $folderVersion -and $apps.Count -gt 0){
     $folderVersion=[string](Get-Item -LiteralPath (Join-Path $folder $apps[0])).VersionInfo.FileVersion;
    }
    [void]$seen.Add($folder);
    $result.Add([pscustomobject]@{Path=$folder;Version=$folderVersion;Architecture=$platform;
     Products=@($products -split ',' | ForEach-Object {$_.Trim()} | Where-Object {$_});
     Source=$source;OsppScript=$ospp;SubscriptionScript=$subscription;Applications=@($apps)});
   }
  }
  foreach($keyPath in @(
   'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration',
   'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Office\ClickToRun\Configuration',
   'HKLM:\SOFTWARE\Microsoft\Office\15.0\ClickToRun\Configuration',
   'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Office\15.0\ClickToRun\Configuration')){
   if(Test-Path -LiteralPath $keyPath){
    $config=Get-ItemProperty -LiteralPath $keyPath;
    $version=[string]$config.VersionToReport;
    if(-not $version){$version=[string]$config.ClientVersionToReport};
    Add-OfficeFolder ([string]$config.InstallationPath) $version ([string]$config.Platform) ([string]$config.ProductReleaseIds) 'Click-to-Run';
   }
  }
  foreach($prefix in @('HKLM:\SOFTWARE\Microsoft\Office','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Office')){
   foreach($major in @('14.0','15.0','16.0')){
    $keyPath=$prefix+'\'+$major+'\Common\InstallRoot';
    if(Test-Path -LiteralPath $keyPath){
     $location=[string](Get-ItemProperty -LiteralPath $keyPath).Path;
     Add-OfficeFolder $location $major '' '' 'Registered Office';
    }
   }
  }
  foreach($base in @($env:ProgramW6432,$env:ProgramFiles,${env:ProgramFiles(x86)}) | Where-Object {$_} | Select-Object -Unique){
   Add-OfficeFolder (Join-Path $base 'Microsoft Office') '' '' '' 'Local files';
  }
  foreach($exe in $executables){
   foreach($prefix in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\')){
    $keyPath=$prefix+$exe;
    if(Test-Path -LiteralPath $keyPath){
     $file=[string](Get-Item -LiteralPath $keyPath).GetValue('');
     if($file){Add-OfficeFolder ([IO.Path]::GetDirectoryName($file.Trim('"'))) '' '' '' 'App Paths'};
    }
   }
  }
  if(Get-Command Get-AppxPackage -ErrorAction SilentlyContinue){
   foreach($package in @(Get-AppxPackage -Name 'Microsoft.Office.Desktop' -ErrorAction SilentlyContinue)){
    Add-OfficeFolder (Join-Path $package.InstallLocation 'Office16') ([string]$package.Version) ([string]$package.Architecture) '' 'Microsoft Store';
    Add-OfficeFolder $package.InstallLocation ([string]$package.Version) ([string]$package.Architecture) '' 'Microsoft Store';
   }
  }
  return $result.ToArray();
 }
 """;
}
