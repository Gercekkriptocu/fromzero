using FromZero;

internal static class DriverCatalogChecks
{
 public static void Run(Action<bool,string> check)
 {
  var device=new DeviceInfo("Test network adapter","instance","Net",0,["PCI\\VEN_10EC&DEV_8168&SUBSYS_12345678","PCI\\VEN_10EC&DEV_8168"]);
  var package=new OfflineDriverEntry("adapter.inf","adapter.cat",new string('A',64),new string('B',64),"Net",
   "10/01/2026,11.2.3.4",["X64"],[device.HardwareIds[0]],[device.Name],"Microsoft Windows Hardware Compatibility Publisher","");
  var installed=new DriverInfo(device.Name,"10.0.1.0","Realtek","oem1.inf",device.Id,"",true);
  check(DriverPackageMatching.Assess(package,device,installed,"X64").Installable&&
   !DriverPackageMatching.Assess(package,device,installed,"Arm64").Installable,
   "Driver package review requires the target device's exact hardware ID and host architecture");
  check(!DriverPackageMatching.Assess(package with{Warning="Invalid signature"},device,installed,"X64").Installable&&
   !DriverPackageMatching.Assess(package with{Class="Firmware"},device,installed,"X64").Installable&&
   !DriverPackageMatching.Assess(package with{HardwareIds=["PCI\\VEN_10EC&DEV_9999"]},device,installed,"X64").Installable,
   "Driver package review rejects unsigned, firmware and unrelated device packages");
  check(!DriverPackageMatching.Assess(package,device,installed with{Version="11.2.3.4"},"X64").Installable&&
   !DriverPackageMatching.Assess(package,device with{Present=false},installed,"X64").Installable,
   "Driver review prevents downgrades and queues for disconnected devices");
 }
}
