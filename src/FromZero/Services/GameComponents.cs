using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace FromZero;
public record GameComponent(string Id,string Name,string Architecture,bool Present,string Evidence,string Source,string? Recipe,string Group);
public record ComponentInventory(DateTimeOffset Checked,string Direct3D,List<GameComponent> Components);
public static class GameComponents
{
 public const string DxPage="https://www.microsoft.com/en-us/download/details.aspx?id=35";
 public const string VcPage="https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist";
 [DllImport("d3d11.dll",CallingConvention=CallingConvention.StdCall)]private static extern int D3D11CreateDevice(IntPtr adapter,int type,IntPtr software,uint flags,uint[] levels,uint count,uint sdk,out IntPtr device,out uint feature,out IntPtr context);
 public static string Direct3DLevel()
 {
  IntPtr device=IntPtr.Zero,context=IntPtr.Zero;
  try{var levels=new uint[]{0xc100,0xc000,0xb100,0xb000,0xa100,0xa000,0x9300};var hr=D3D11CreateDevice(IntPtr.Zero,1,IntPtr.Zero,0,levels,(uint)levels.Length,7,out device,out var feature,out context);return hr>=0?$"Donanım özellik seviyesi {(feature>>12)}.{(feature>>8)&15}":"Donanım özellik seviyesi okunamadı (0x"+unchecked((uint)hr).ToString("X8")+")";}
  catch(DllNotFoundException){return "Direct3D 11 sistem bileşeni bulunamadı.";}
  finally{if(context!=IntPtr.Zero)Marshal.Release(context);if(device!=IntPtr.Zero)Marshal.Release(device);}
 }
 public static ComponentInventory Scan(IEnumerable<InstalledApp> installed)
 {
  var apps=installed.ToList();var result=new List<GameComponent>();
  foreach(var arch in new[]{"x64","x86"}){
   var system=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),arch=="x64"?"System32":"SysWOW64");
   void FileCheck(string id,string name,string file,string source,string? recipe,string group){var path=Path.Combine(system,file);var present=File.Exists(path);string version="";if(present){try{version=FileVersionInfo.GetVersionInfo(path).FileVersion??"";}catch(IOException){}}result.Add(new(id+"-"+arch,name,arch,present,present?file+" • "+version:file+" bulunamadı",source,recipe,group));}
   FileCheck("d3d9","Windows Direct3D 9","d3d9.dll","ms-settings:windowsupdate",null,"Windows grafik");
   FileCheck("d3d11","Windows Direct3D 11","d3d11.dll","ms-settings:windowsupdate",null,"Windows grafik");
   FileCheck("d3d12","Windows Direct3D 12","d3d12.dll","ms-settings:windowsupdate",null,"Windows grafik");
   foreach(var dll in new[]{"d3dx9_43.dll","d3dx10_43.dll","d3dx11_43.dll","XInput1_3.dll","XAudio2_7.dll","D3DCompiler_43.dll","xactengine3_7.dll"})FileCheck(dll.ToLowerInvariant(),Path.GetFileNameWithoutExtension(dll),dll,DxPage,"directx","Eski oyun kütüphaneleri");
   foreach(var year in new[]{"2005","2008","2010","2012","2013","2015–2026"}){
    var matches=apps.Where(a=>a.Name.Contains("Visual C++",StringComparison.OrdinalIgnoreCase)&&a.Name.Contains(arch,StringComparison.OrdinalIgnoreCase)&&(year=="2015–2026"?new[]{"2015","2017","2019","2022","2026","v14"}.Any(x=>a.Name.Contains(x,StringComparison.OrdinalIgnoreCase)):a.Name.Contains(year))).ToList();
    result.Add(new("vc"+year+"-"+arch,"Visual C++ "+year,arch,matches.Count>0,matches.Count>0?string.Join(" • ",matches.Select(m=>m.Version)):"Kurulum kaydı bulunamadı",VcPage,year=="2015–2026"?"vc-"+arch:"vc"+year+"-"+arch,"Visual C++"));
   }
   FileCheck("openal","OpenAL","OpenAL32.dll","https://openal.org/downloads/",null,"Diğer oyun bileşenleri");
   FileCheck("vulkan","Vulkan Loader","vulkan-1.dll","https://vulkan.lunarg.com/sdk/home",null,"Diğer oyun bileşenleri");
  }
  using(var framework=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")){
   var release=framework?.GetValue("Release") is int value?value:0;result.Add(new("net481",".NET Framework 4.8.1","Windows",release>=533320,"Release: "+release,"https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481",null,"Diğer oyun bileşenleri"));
  }
  using(var legacy=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5"))result.Add(new("net35",".NET Framework 3.5","Windows",legacy?.GetValue("Install") is int v&&v==1,"Windows isteğe bağlı özelliği","ms-settings:optionalfeatures",null,"Diğer oyun bileşenleri"));
  var physx=apps.Where(a=>a.Name.Contains("PhysX",StringComparison.OrdinalIgnoreCase)).ToList();result.Add(new("physx","NVIDIA PhysX","x86 / x64",physx.Count>0,string.Join(" • ",physx.Select(a=>a.Version)),"https://www.nvidia.com/en-us/drivers/physx/physx-9-23-1019-driver/",null,"Diğer oyun bileşenleri"));
  var xna=apps.Where(a=>a.Name.Contains("XNA Framework",StringComparison.OrdinalIgnoreCase)).ToList();result.Add(new("xna","Microsoft XNA Framework","x86",xna.Count>0,string.Join(" • ",xna.Select(a=>a.Version)),"https://www.microsoft.com/en-us/download/details.aspx?id=20914",null,"Diğer oyun bileşenleri"));
  return new(DateTimeOffset.Now,Direct3DLevel(),result);
 }
}
