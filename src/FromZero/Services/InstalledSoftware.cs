using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FromZero;

public record InstalledApp(string Name,string Version,string Publisher,string InstallLocation,string Executable,string Source,string RegistryKey="",string DisplayIcon="",string UninstallCommand="",bool WindowsInstaller=false,bool SystemComponent=false,bool NoRemove=false,long EstimatedBytes=0,string InstallDate="",string PackageFullName="",bool ProtectedPackage=false,string DisplayName="");
public record SoftwareInventory(DateTimeOffset Checked,List<InstalledApp> Apps,string Warning);
public record GpuTool(string Id,string Name,string Vendor,string SupportUrl,string[] AppPatterns,string[] RelativeExecutables,string Publisher);
public record GpuToolState(GpuTool Tool,List<DeviceInfo> Devices,List<DriverInfo> Drivers,List<InstalledApp> Apps,string Executable,string FileVersion)
{
 public bool HardwarePresent=>Devices.Count>0;
 public bool ExecutablePresent=>Executable.Length>0&&File.Exists(Executable);
}

public static class InstalledSoftware
{
 public static string DisplayLabel(InstalledApp app)=>string.IsNullOrWhiteSpace(app.DisplayName)?app.Name:app.DisplayName;
 public static List<InstalledApp> ReadDesktopApps()
 {
  var result=new List<InstalledApp>();
  foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
  foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
   try {
    using var baseKey=RegistryKey.OpenBaseKey(hive,view);
    using var uninstall=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
    if(uninstall==null)continue;
    foreach(var sub in uninstall.GetSubKeyNames()) {
     using var key=uninstall.OpenSubKey(sub);if(key==null)continue;
     var name=key.GetValue("DisplayName") as string;if(string.IsNullOrWhiteSpace(name))continue;
     var location=Environment.ExpandEnvironmentVariables(key.GetValue("InstallLocation") as string??"").Trim('"');
     var icon=key.GetValue("DisplayIcon") as string??"";
     var exe=ParseDisplayIcon(icon);
     result.Add(new(name,key.GetValue("DisplayVersion") as string??"",key.GetValue("Publisher") as string??"",location,exe,$"{hive}/{view}",sub,icon,key.GetValue("UninstallString") as string??"",key.GetValue("WindowsInstaller") is int msi&&msi==1,key.GetValue("SystemComponent") is int sys&&sys==1,key.GetValue("NoRemove") is int nr&&nr==1,key.GetValue("EstimatedSize") is int kb?Math.Max(0,(long)kb)*1024:0,key.GetValue("InstallDate") as string??""));
    }
   }catch(Exception ex)when(ex is System.Security.SecurityException or UnauthorizedAccessException or IOException){ }
  }
  return result.DistinctBy(a=>(a.Name.ToUpperInvariant(),a.Version,a.InstallLocation.ToUpperInvariant())).OrderBy(a=>a.Name).ToList();
 }
 public static string ParseDisplayIcon(string value)
 {
  value=Environment.ExpandEnvironmentVariables(value.Trim());
  var match=Regex.Match(value,"^(?:\"(?<path>[^\"]+\\.exe)\"|(?<path>.+?\\.exe))(?:,\\s*-?\\d+)?$",RegexOptions.IgnoreCase);
  return match.Success&&File.Exists(match.Groups["path"].Value)?Path.GetFullPath(match.Groups["path"].Value):"";
 }
 public static async Task<SoftwareInventory> ReadAsync()
 {
  var desktop=await Task.Run(ReadDesktopApps);var warning="";
  try {
   var store=await Commands.JsonAsync<List<InstalledApp>>("""
$labels=@(Get-StartApps)
ConvertTo-Json -Depth 4 -InputObject @(Get-AppxPackage | ForEach-Object {
 $a=$_; $logo=''; $friendly=[string]($labels | Where-Object {$_.AppID -like ($a.PackageFamilyName+'!*')} | Select-Object -First 1 -ExpandProperty Name)
 try {$manifest=Get-AppxPackageManifest -Package $a.PackageFullName; $relative=[string](@($manifest.Package.Applications.Application)[0].VisualElements.Square44x44Logo);if(-not $relative){$relative=[string]$manifest.Package.Properties.Logo};if($relative -and -not $relative.StartsWith('ms-resource:')){$logo=Join-Path $a.InstallLocation $relative}}catch{}
 [pscustomobject]@{Name=[string]$a.Name;DisplayName=$friendly;Version=[string]$a.Version;Publisher=[string]$a.Publisher;InstallLocation=[string]$a.InstallLocation;Executable='';DisplayIcon=$logo;Source='Microsoft Store / MSIX';PackageFullName=[string]$a.PackageFullName;ProtectedPackage=([bool]$a.NonRemovable -or [bool]$a.IsFramework -or [bool]$a.IsResourcePackage)}
})
""",90);
   desktop.AddRange(store.Select(a=>a with{DisplayName=a.DisplayName??""}));
  }catch(Exception ex)when(ex is InvalidOperationException or TimeoutException or JsonException){warning="Store/MSIX envanteri alınamadı; masaüstü uygulamaları listelendi. "+ex.Message;}
  return new(DateTimeOffset.Now,desktop.DistinctBy(a=>(a.Name.ToUpperInvariant(),a.Version,a.Source)).OrderBy(a=>a.Name).ToList(),warning);
 }
 public static bool Matches(string name,IEnumerable<string> patterns)=>patterns.Any(p=>name.Contains(p,StringComparison.OrdinalIgnoreCase));
 public static List<InstalledApp> FindCatalogApps(IEnumerable<InstalledApp> apps,string catalogName)
 {
  string[] patterns=catalogName switch{
   "Firefox"=>["Mozilla Firefox","Mozilla.Firefox"],"Visual Studio Code"=>["Microsoft Visual Studio Code","Microsoft.VisualStudioCode"],"Steam"=>["Steam"],"VLC"=>["VLC media player","VideoLAN.VLC"],"7-Zip"=>["7-Zip"],"Discord"=>["Discord"],_=>[catalogName]
  };
  return apps.Where(a=>catalogName=="Steam"?a.Name.Equals("Steam",StringComparison.OrdinalIgnoreCase):Matches(a.Name,patterns)).ToList();
 }
}

public static class GpuSoftware
{
 public static IReadOnlyList<GpuTool> Tools {get;}=[
  new("nvidia","NVIDIA App","NVIDIA","https://www.nvidia.com/en-us/software/nvidia-app/",["NVIDIA App"],[ @"NVIDIA Corporation\NVIDIA app\CEF\NVIDIA app.exe",@"NVIDIA Corporation\NVIDIA app\NVIDIA app.exe"],"NVIDIA Corporation"),
  new("amd","AMD Software: Adrenalin Edition","AMD","https://www.amd.com/en/support/download/drivers.html",["AMD Software","AMD Radeon Software"],[@"AMD\CNext\CNext\RadeonSoftware.exe"],"Advanced Micro Devices"),
  new("intel","Intel Driver & Support Assistant","Intel","https://www.intel.com/content/www/us/en/support/detect.html",["Intel Driver & Support Assistant","Intel® Driver & Support Assistant","Intel(R) Driver & Support Assistant"],[@"Intel\Driver and Support Assistant\DSATray.exe"],"Intel")
 ];
 public static string Vendor(DeviceInfo device)
 {
  if(!device.Class.Equals("Display",StringComparison.OrdinalIgnoreCase))return "";
  var ids=device.HardwareIds.Concat([device.Id]);
  if(ids.Any(id=>id.StartsWith(@"PCI\VEN_10DE",StringComparison.OrdinalIgnoreCase)))return "NVIDIA";
  if(ids.Any(id=>id.StartsWith(@"PCI\VEN_1002",StringComparison.OrdinalIgnoreCase)))return "AMD";
  if(ids.Any(id=>id.StartsWith(@"PCI\VEN_8086",StringComparison.OrdinalIgnoreCase)))return "Intel";
  return "";
 }
 public static List<GpuToolState> Inspect(IEnumerable<DeviceInfo> devices,IEnumerable<DriverInfo> drivers,IEnumerable<InstalledApp> apps)
 {
  var allDevices=devices.Where(d=>d.Present).ToList();var allDrivers=drivers.ToList();var allApps=apps.ToList();var results=new List<GpuToolState>();
  foreach(var tool in Tools) {
   var hardware=allDevices.Where(d=>Vendor(d)==tool.Vendor).ToList();
   var matchingDrivers=allDrivers.Where(d=>hardware.Any(h=>h.Id.Equals(d.DeviceId,StringComparison.OrdinalIgnoreCase))).ToList();
   var installed=allApps.Where(a=>InstalledSoftware.Matches(a.Name,tool.AppPatterns)).ToList();
   var candidates=new List<string>();
   foreach(var folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) {
    if(folder.Length>0)candidates.AddRange(tool.RelativeExecutables.Select(e=>Path.Combine(folder,e)));
   }
   foreach(var app in installed){
    if(app.Executable.Length>0 && tool.RelativeExecutables.Any(e=>Path.GetFileName(e).Equals(Path.GetFileName(app.Executable),StringComparison.OrdinalIgnoreCase)))candidates.Add(app.Executable);
    if(Directory.Exists(app.InstallLocation))foreach(var relative in tool.RelativeExecutables){
     candidates.Add(Path.Combine(app.InstallLocation,Path.GetFileName(relative)));
     if(tool.Id=="nvidia")candidates.Add(Path.Combine(app.InstallLocation,"CEF","NVIDIA app.exe"));
    }
   }
   var exe=candidates.FirstOrDefault(File.Exists)??"";
   string version="";if(exe.Length>0){try{var info=FileVersionInfo.GetVersionInfo(exe);version=(info.FileVersion??info.ProductVersion??"").Split(';')[0];}catch(IOException){}}
   results.Add(new(tool,hardware,matchingDrivers,installed,exe,version));
  }
  return results;
 }
 public static bool InfPresent(DriverInfo driver)=>driver.Inf.Length>0&&Path.GetFileName(driver.Inf)==driver.Inf&&File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"INF",driver.Inf));
}
