using System.IO;
using System.Text.RegularExpressions;
namespace FromZero;
public record AppSignals(bool Running,int StartupEntries,bool Shared);
public static class AppLifecycle
{
 public static bool Shared(InstalledApp app)=>app.SystemComponent||app.ProtectedPackage||app.NoRemove||Regex.IsMatch(app.Name,@"^(Microsoft\s+)?(Visual C\+\+|Windows Desktop Runtime|\.NET (Runtime|Host|SDK)|ASP\.NET Core|Windows App Runtime)(?!\w)",RegexOptions.IgnoreCase);
 public static string CommandExecutable(string command)
 {
  var match=Regex.Match(Environment.ExpandEnvironmentVariables(command).Trim(),"^(?:\"(?<exe>[^\"]+\\.exe)\"|(?<exe>[^\"]+?\\.exe))(?:\\s|$)",RegexOptions.IgnoreCase);
  var path=match.Groups["exe"].Value;
  return Regex.IsMatch(path,@"^[A-Za-z]:[\\/]")?path:"";
 }
 private static bool OwnsPath(InstalledApp app,string path)
 {
  if(path.Length==0)return false;
  if(!string.IsNullOrWhiteSpace(app.Executable)&&path.Equals(app.Executable,StringComparison.OrdinalIgnoreCase))return true;
  var location=app.InstallLocation;
  if(string.IsNullOrWhiteSpace(location)||!Regex.IsMatch(location,@"^[A-Za-z]:[\\/]"))return false;
  try
  {
   var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
   var parent=Path.GetDirectoryName(root);
   if(parent==null||parent==Path.GetPathRoot(parent)||root.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows),StringComparison.OrdinalIgnoreCase))return false;
   return Path.GetFullPath(path).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
  }
  catch(ArgumentException){return false;}
 }
 public static AppSignals Inspect(InstalledApp app,IEnumerable<string> runningPaths,IEnumerable<StartupEntry> startup)=>new(runningPaths.Any(p=>OwnsPath(app,p)),startup.Count(s=>!s.Disabled&&OwnsPath(app,CommandExecutable(s.Command))),Shared(app));
 public static bool StillRegistered(InstalledApp expected,IEnumerable<InstalledApp> inventory)=>inventory.Any(a=>expected.PackageFullName.Length>0?a.PackageFullName.Equals(expected.PackageFullName,StringComparison.OrdinalIgnoreCase):a.Source==expected.Source&&a.RegistryKey==expected.RegistryKey&&a.Name.Equals(expected.Name,StringComparison.OrdinalIgnoreCase));
 public static List<string> RemainingFiles(InstalledApp app,IEnumerable<InstalledApp> remaining)
 {
  var location=app.InstallLocation;
  if(Shared(app)||app.PackageFullName.Length>0||string.IsNullOrWhiteSpace(location)||!Regex.IsMatch(location,@"^[A-Za-z]:[\\/]"))return [];
  var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
  var managedRoots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")};
  if(!managedRoots.Any(p=>p.Length>0&&root.StartsWith(Path.TrimEndingDirectorySeparator(p)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))||!Directory.Exists(root))return [];
  if(remaining.Any(a=>!string.IsNullOrWhiteSpace(a.InstallLocation)&&(root.Equals(Path.TrimEndingDirectorySeparator(a.InstallLocation),StringComparison.OrdinalIgnoreCase)||a.InstallLocation.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))))return [];
  PackageSafety.RejectReparsePoints(root,root);
  return Directory.EnumerateFileSystemEntries(root).Take(200).Where(p=>(File.GetAttributes(p)&FileAttributes.ReparsePoint)==0).Select(Path.GetFileName).OfType<string>().Order().ToList();
 }
}
