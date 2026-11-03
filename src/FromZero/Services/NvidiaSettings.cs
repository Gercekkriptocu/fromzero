using System.IO;
using System.Runtime.InteropServices;
namespace FromZero;
public record NvidiaValue(uint Id,uint Value,bool UserOverride,uint Predefined,string Name);
public sealed class NvidiaSettings : IDisposable
{
 public const uint PowerId=0x1057EB71,QualityId=0x00CE2691,RefreshId=0x0064B541,VsyncId=0x00A879CF;
 public const uint VsyncOff=0x08416747;
 public static bool Known(uint id)=>id is PowerId or QualityId or RefreshId or VsyncId;
 public static string Name(uint id)=>id switch{PowerId=>"Güç yönetimi",QualityId=>"Doku filtreleme kalitesi",RefreshId=>"Tercih edilen yenileme hızı",VsyncId=>"Dikey senkronizasyon",_=>throw new InvalidDataException("Desteklenmeyen NVIDIA ayarı.")};
 public static Dictionary<uint,uint> Desired(PerformanceOptions options){var result=new Dictionary<uint,uint>();if(options.NvidiaPower)result.Add(PowerId,1);if(options.NvidiaQuality)result.Add(QualityId,20);if(options.NvidiaRefresh)result.Add(RefreshId,1);if(options.NvidiaVsyncOff)result.Add(VsyncId,VsyncOff);return result;}
 public static string Describe(NvidiaValue value)=>value.Id switch{
  PowerId=>value.Value==1?"Maksimum performansı tercih et":value.Value==5?"Sürücü varsayılanı / normal":"Güç tercihi "+value.Value,
  QualityId=>value.Value switch{20=>"Yüksek performans",10=>"Performans",0=>"Kalite",0xfffffff6=>"Yüksek kalite",_=>"Değer "+value.Value},
  RefreshId=>value.Value==1?"Kullanılabilen en yüksek":value.Value==0?"Uygulama kontrolünde":"Değer "+value.Value,
  VsyncId=>value.Value switch{VsyncOff=>"Kapalı",0x47814940=>"Açık",0x60925292=>"Uygulama kontrolünde",0x18888888=>"Hızlı senkronizasyon",_=>"Değer "+value.Value},_=>"Bilinmiyor"};
 // NVIDIA NVDRS_SETTING_V1, pack(4), UnicodeString=2048 WCHAR, union max=4100 bytes.
 public const int SettingSize=12320,IdOffset=4100,TypeOffset=4104,LocationOffset=4108,PredefinedFlagOffset=4112,PredefinedValueOffset=4120,CurrentValueOffset=8220;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Query(uint id);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Initialize();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Create(out IntPtr session);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int SessionAction(IntPtr session);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int BaseProfile(IntPtr session,out IntPtr profile);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int GetValue(IntPtr session,IntPtr profile,uint id,IntPtr setting);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int SetValue(IntPtr session,IntPtr profile,IntPtr setting);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int DeleteValue(IntPtr session,IntPtr profile,uint id);
 private readonly IntPtr library;private IntPtr session,profile;private readonly Query query;
 private bool disposed;
 private T Api<T>(uint id)where T:Delegate{var p=query(id);if(p==IntPtr.Zero)throw new NotSupportedException("NVIDIA sürücüsü bu ayar işlevini sunmuyor.");return Marshal.GetDelegateForFunctionPointer<T>(p);}
 private static void Check(int status){if(status!=0)throw new InvalidOperationException("NVIDIA ayar işlemi başarısız (NVAPI "+status+"). Sürücü desteği veya yönetici yetkisi gerekebilir.");}
 public NvidiaSettings()
 {
  var path=Path.Combine(Environment.SystemDirectory,"nvapi64.dll");
  if(!File.Exists(path))throw new NotSupportedException("NVIDIA NVAPI sürücü bileşeni bulunamadı.");
  Authenticode.Verify(path);library=NativeLibrary.Load(path);
  try{query=Marshal.GetDelegateForFunctionPointer<Query>(NativeLibrary.GetExport(library,"nvapi_QueryInterface"));Check(Api<Initialize>(0x0150e828)());Check(Api<Create>(0x0694d52e)(out session));Check(Api<SessionAction>(0x375dbd6b)(session));Check(Api<BaseProfile>(0xda8466a0)(session,out profile));}
  catch{Dispose();throw;}
 }
 private static IntPtr NewSetting(uint id){var ptr=Marshal.AllocHGlobal(SettingSize);Marshal.Copy(new byte[SettingSize],0,ptr,SettingSize);Marshal.WriteInt32(ptr,SettingSize|(1<<16));Marshal.WriteInt32(ptr,IdOffset,unchecked((int)id));return ptr;}
 public NvidiaValue Read(uint id)
 {
  if(!Known(id))throw new InvalidDataException("Desteklenmeyen NVIDIA ayarı.");
  var ptr=NewSetting(id);try{Check(Api<GetValue>(0x73bf8338)(session,profile,id,ptr));if(Marshal.ReadInt32(ptr,TypeOffset)!=0)throw new NotSupportedException("NVIDIA ayarı DWORD türünde değil.");return new(id,unchecked((uint)Marshal.ReadInt32(ptr,CurrentValueOffset)),Marshal.ReadInt32(ptr,PredefinedFlagOffset)==0,unchecked((uint)Marshal.ReadInt32(ptr,PredefinedValueOffset)),Name(id));}finally{Marshal.FreeHGlobal(ptr);}
 }
 public void Set(uint id,uint value)
 {
  if(!Valid(id,value))throw new InvalidDataException("Desteklenmeyen NVIDIA ayarı/değeri.");
  var ptr=NewSetting(id);try{Marshal.WriteInt32(ptr,CurrentValueOffset,unchecked((int)value));Check(Api<SetValue>(0x577dd202)(session,profile,ptr));}finally{Marshal.FreeHGlobal(ptr);}
 }
 public void Restore(NvidiaValue original)
 {
  if(!Known(original.Id))throw new InvalidDataException("Desteklenmeyen NVIDIA geri alma ayarı.");
  if(original.UserOverride)Set(original.Id,original.Value);
  else Check(Api<DeleteValue>(0xe4a26362)(session,profile,original.Id));
 }
 public void Save()=>Check(Api<SessionAction>(0xfcbc7e14)(session));
 public void Dispose(){if(disposed)return;disposed=true;if(session!=IntPtr.Zero){try{Api<SessionAction>(0xdad9cff8)(session);}finally{session=IntPtr.Zero;}}if(library!=IntPtr.Zero)NativeLibrary.Free(library);}
 public static bool Valid(uint id,uint value)=>id switch{PowerId=>value<=5,QualityId=>value is 0 or 10 or 20 or 0xfffffff6,RefreshId=>value<=1||(value&~0xff0u)==0,VsyncId=>value is VsyncOff or 0x47814940 or 0x60925292 or 0x18888888 or 0x32610244 or 0x71271021 or 0x13245256,_=>false};
 public static Task<List<NvidiaValue>> InspectAsync()=>Task.Run(()=>{using var nvidia=new NvidiaSettings();return new List<NvidiaValue>{nvidia.Read(PowerId),nvidia.Read(QualityId),nvidia.Read(RefreshId),nvidia.Read(VsyncId)};});
}
