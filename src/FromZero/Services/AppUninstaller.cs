using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace FromZero;
public record UninstallPlan(string FileName,string Arguments,string PackageFullName="");
public record AppRemovalResult(SoftwareInventory Inventory,bool? Removed,string Message);
public static class AppUninstaller
{
 private static readonly string[] Shells=["cmd.exe","powershell.exe","pwsh.exe","rundll32.exe","regsvr32.exe","mshta.exe","wscript.exe","cscript.exe"];
 public static UninstallPlan Plan(InstalledApp app,Func<string,bool>? exists=null)
 {
  exists??=File.Exists;
  if(AppLifecycle.Shared(app))throw new InvalidDataException("This system or shared component is protected. Use Windows Settings if removal is supported.");
  if(app.PackageFullName.Length>0){if(!Regex.IsMatch(app.PackageFullName,@"^[A-Za-z0-9][A-Za-z0-9._-]{3,250}$"))throw new InvalidDataException("Invalid MSIX package identity.");return new("","",app.PackageFullName);}
  if(app.WindowsInstaller&&Guid.TryParse(app.RegistryKey,out var product))return new(Path.Combine(Environment.SystemDirectory,"msiexec.exe"),"/x "+product.ToString("B"));
  var command=Environment.ExpandEnvironmentVariables(app.UninstallCommand.Trim());
  var match=Regex.Match(command,"^(?:\"(?<file>[^\"]+\\.exe)\"|(?<file>[^\"]+?\\.exe))(?:\\s+(?<args>.*))?$",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
  if(!match.Success)throw new InvalidDataException("No supported vendor uninstaller was found. Open Windows Installed Apps.");
  var file=match.Groups["file"].Value;var args=match.Groups["args"].Value;
  if(Path.GetFileName(file).Equals("msiexec.exe",StringComparison.OrdinalIgnoreCase)){
   var code=Regex.Match(args,@"(?i)(?:/I|/X)\s*(\{[0-9a-f-]{36}\})");
   if(!code.Success||!Guid.TryParse(code.Groups[1].Value,out product))throw new InvalidDataException("Invalid Windows Installer product identity.");
   return new(Path.Combine(Environment.SystemDirectory,"msiexec.exe"),"/x "+product.ToString("B"));
  }
  if(!Regex.IsMatch(file,@"^[A-Za-z]:[\\/]")||Shells.Contains(Path.GetFileName(file),StringComparer.OrdinalIgnoreCase)||!exists(file)||args.Contains('\n')||args.Contains('\r'))throw new InvalidDataException("The uninstall entry cannot be launched safely. Use Windows Installed Apps.");
  return new(Path.GetFullPath(file),args);
 }
 public static bool CanRemove(InstalledApp app){try{Plan(app);return true;}catch(InvalidDataException){return false;}}
 public static async Task<AppRemovalResult> RemoveAsync(InstalledApp expected)
 {
  // Re-read the registry/package inventory: a stale UI row cannot substitute a different command.
  var fresh=(await InstalledSoftware.ReadAsync()).Apps.SingleOrDefault(a=>expected.PackageFullName.Length>0?a.PackageFullName==expected.PackageFullName:a.Source==expected.Source&&a.RegistryKey==expected.RegistryKey&&a.Name==expected.Name&&a.UninstallCommand==expected.UninstallCommand&&a.Version==expected.Version)??throw new IOException("The application changed or was already removed. Refresh the list.");
  var plan=Plan(fresh);
  if(plan.PackageFullName.Length>0){
   var name=PackageSafety.QuotePs(plan.PackageFullName);
   var result=await Commands.PsAsync("$p=Get-AppxPackage | Where-Object {$_.PackageFullName -eq "+name+"};if(-not $p -or $p.NonRemovable -or $p.IsFramework -or $p.IsResourcePackage){throw 'Package cannot be removed.'};Remove-AppxPackage -Package $p.PackageFullName",300);
   if(!result.Success)throw new IOException(result.Output);
  }else{
   using var process=Process.Start(new ProcessStartInfo(plan.FileName){Arguments=plan.Arguments,UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(plan.FileName)!})??throw new IOException("The vendor uninstaller did not start.");
   await process.WaitForExitAsync();
   if(process.ExitCode is not 0 and not 3010 and not 1641)throw new IOException("Uninstaller exit code: "+process.ExitCode+". It may have been canceled. Refresh to check the installed state.");
  }
  var inventory=await InstalledSoftware.ReadAsync();
  bool? removed=fresh.PackageFullName.Length>0&&inventory.Warning.Length>0?null:!AppLifecycle.StillRegistered(fresh,inventory.Apps);
  var message=removed switch{true=>"Removal verified in the refreshed inventory.",false=>"The uninstall flow finished, but the application is still registered. It may need a restart or another check.",_=>"The uninstall flow finished, but Store inventory could not be verified."};
  Journal.Write("Application uninstaller",fresh.Name+" / "+message,removed==true);
  return new(inventory,removed,message);
 }
}
