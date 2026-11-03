// Adapted from ReClaim paths.rs, MIT. See ThirdParty/Reclaim/LICENSE.txt.
using System.IO;
namespace FromZero;

public sealed class ReclaimGuard
{
 private readonly List<string> noWipe=[];
 private readonly List<string> protectedTrees=[];
 private static readonly string[] ProtectedNames=[".git","system volume information","$recycle.bin","$windows.~bt","$windows.~ws","windows.old"];
 public static string Key(string path)=>Path.GetFullPath(path).TrimEnd('\\').ToLowerInvariant();
 public static bool Within(string child,string parent)=>child.Equals(parent,StringComparison.OrdinalIgnoreCase)||child.StartsWith(parent.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);
 public ReclaimGuard()
 {
  void Root(string? path){if(!string.IsNullOrWhiteSpace(path))noWipe.Add(Key(path));}
  void Tree(string? path){if(!string.IsNullOrWhiteSpace(path))protectedTrees.Add(Key(path));}
  foreach(var v in new[]{"USERPROFILE","APPDATA","LOCALAPPDATA","PROGRAMDATA","PROGRAMFILES","PROGRAMFILES(X86)","ProgramW6432","PUBLIC","WINDIR"})Root(Environment.GetEnvironmentVariable(v));
  foreach(var v in new[]{"OneDrive","OneDriveConsumer","OneDriveCommercial"})Tree(Environment.GetEnvironmentVariable(v));
  var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  foreach(var name in new[]{"AppData","AppData\\LocalLow","Desktop","Documents","Downloads","Pictures","Music","Videos","Favorites","Contacts","Links","Saved Games","Searches","OneDrive",".ssh",".gnupg"})Root(Path.Combine(profile,name));
  foreach(var folder in new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.MyDocuments,Environment.SpecialFolder.MyPictures,Environment.SpecialFolder.MyMusic,Environment.SpecialFolder.MyVideos})Root(Environment.GetFolderPath(folder));
  var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
  foreach(var name in new[]{"System32","SysWOW64","WinSxS","Installer","servicing","Boot","Fonts","assembly","SystemApps","SystemResources","Microsoft.NET"})Tree(Path.Combine(windows,name));
  var drive=Path.GetPathRoot(windows)!;Root(Path.Combine(drive,"Users"));foreach(var name in new[]{"Recovery","Boot","EFI"})Tree(Path.Combine(drive,name));
  var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);foreach(var name in new[]{"Packages","Programs","Microsoft"})Root(Path.Combine(local,name));
  Root(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft"));
  Tree(LocalStore.Root);Tree(AppContext.BaseDirectory);
 }
 public void Check(string path,bool contents=false)
 {
  if(!Path.IsPathFullyQualified(path)||path.StartsWith("\\\\",StringComparison.Ordinal)||path.Split('\\','/').Any(p=>p is "." or ".."))throw new InvalidDataException("Mutlak yerel yol gerekli.");
  var key=Key(path);
  if(key.Length<=3||key.Split('\\').Any(p=>ProtectedNames.Contains(p))||noWipe.Any(p=>Within(p,key))||protectedTrees.Any(p=>Within(key,p)||Within(p,key)))throw new InvalidDataException("Korunan temizlik hedefi: "+path);
  if(!contents&&(key==Key(Path.GetTempPath())||key==Key(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp"))))throw new InvalidDataException("Geçici klasörün kendisi silinemez.");
  PackageSafety.RejectReparsePoints(path,path);
 }
}
