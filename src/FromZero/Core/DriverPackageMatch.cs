using System.Runtime.InteropServices;

namespace FromZero;

public record DriverPackageMatch(bool Installable,string Reason,string Version,int HardwareIdOrder);

public static class DriverPackageMatching
{
 public static DriverPackageMatch Assess(OfflineDriverEntry package,DeviceInfo device,DriverInfo? installed,string? architecture=null)
 {
  var versionText=package.DriverVersion.Split(',').Last().Trim();
  var order=Array.FindIndex(device.HardwareIds,id=>package.HardwareIds.Contains(id,StringComparer.OrdinalIgnoreCase));
  DriverPackageMatch Reject(string reason)=>new(false,reason,versionText,order);
  if(!device.Present)return Reject("Device is no longer connected");
  if(order<0)return Reject("No exact hardware ID match");
  if(package.Class.Equals("Firmware",StringComparison.OrdinalIgnoreCase))return Reject("Firmware requires the manufacturer's tool");
  if(device.Class.Length>0&&device.Class is not ("Unknown" or "Other")&&
     !device.Class.Equals(package.Class,StringComparison.OrdinalIgnoreCase))return Reject("Device class differs from the package");
  if(!package.Architectures.Contains(architecture??RuntimeInformation.OSArchitecture.ToString(),StringComparer.OrdinalIgnoreCase))
   return Reject("Package architecture could not be confirmed");
  if(package.Warning.Length>0||package.Publisher.Length==0||package.CatalogHash.Length!=64)
   return Reject("Signed driver catalogue could not be verified");
  if(!Version.TryParse(versionText,out var target))return Reject("Package version could not be read");
  if(installed!=null&&Version.TryParse(installed.Version,out var current)&&target<=current)
   return Reject("The installed driver is the same version or newer");
  return new(true,"Exact hardware ID and signed package verified. Windows checks OS compatibility and driver rank.",versionText,order);
 }
}
