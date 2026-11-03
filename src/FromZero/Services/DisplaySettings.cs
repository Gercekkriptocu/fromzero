using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace FromZero;

public record DisplayMode(string Device,uint Width,uint Height,uint Bits,uint Hz,uint Flags,uint Orientation,int X,int Y)
{
 public string Label=>$"{Width} × {Height} • {Hz} Hz";
 public bool SameSurface(DisplayMode other)=>Device==other.Device&&Width==other.Width&&Height==other.Height&&Bits==other.Bits&&Flags==other.Flags&&Orientation==other.Orientation&&X==other.X&&Y==other.Y;
}
public record DisplayScreen(string Name,DisplayMode Current,IReadOnlyList<DisplayMode> Modes);
public record DisplayBackup(DisplayMode Original,DisplayMode Applied,bool Confirmed);
public interface IDisplayBackend
{
 IReadOnlyList<DisplayScreen> Read();void Test(DisplayMode mode);void Apply(DisplayMode mode,bool persist);
}
public sealed class WindowsDisplayBackend:IDisplayBackend
{
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Device
 {
  public uint Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint Flags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;
 }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Mode
 {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;
  public ushort Spec,Driver,Size,Extra;public uint Fields;public int X,Y;public uint Orientation,FixedOutput;
  public short Color,Duplex,YResolution,TTOption,Collate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string FormName;
  public ushort LogPixels;public uint Bits,Width,Height,Flags,Hz,IcmMethod,IcmIntent,Media,Dither,Reserved1,Reserved2,PanningWidth,PanningHeight;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplayDevices(string? device,uint index,ref Device output,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplaySettingsEx(string device,int index,ref Mode mode,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int ChangeDisplaySettingsEx(string device,ref Mode mode,IntPtr window,uint flags,IntPtr parameter);
 private static Mode Empty()=>new(){Size=(ushort)Marshal.SizeOf<Mode>(),DeviceName="",FormName=""};
 private static DisplayMode Convert(string device,Mode m,int x,int y)=>new(device,m.Width,m.Height,m.Bits,m.Hz,m.Flags,m.Orientation,x,y);
 public IReadOnlyList<DisplayScreen> Read()
 {
  if(Marshal.SizeOf<Mode>()!=220||Marshal.SizeOf<Device>()!=840)throw new NotSupportedException("Ekran API yapısı uyumsuz.");
  var screens=new List<DisplayScreen>();
  for(uint i=0;i<64;i++){
   var device=new Device{Size=(uint)Marshal.SizeOf<Device>(),Name="",Description="",Id="",Key=""};
   if(!EnumDisplayDevices(null,i,ref device,0))break;
   if((device.Flags&1)==0||(device.Flags&8)!=0)continue;
   var current=Empty();if(!EnumDisplaySettingsEx(device.Name,-1,ref current,0))continue;
   var active=Convert(device.Name,current,current.X,current.Y);var modes=new List<DisplayMode>();
   for(int n=0;n<4096;n++){var mode=Empty();if(!EnumDisplaySettingsEx(device.Name,n,ref mode,0))break;var candidate=Convert(device.Name,mode,current.X,current.Y);if(active.SameSurface(candidate)&&candidate.Hz>=24&&candidate.Hz<=1000&&candidate.Bits==32)modes.Add(candidate);}
   screens.Add(new(device.Description+" / "+device.Name,active,modes.Distinct().OrderBy(m=>m.Hz).ToList()));
  }
  return screens;
 }
 private Mode Native(DisplayMode target)
 {
  // Start with the current complete DEVMODE; change only the frequency field.
  var mode=Empty();if(!EnumDisplaySettingsEx(target.Device,-1,ref mode,0))throw new IOException("Ekran artık bağlı değil.");
  var current=Convert(target.Device,mode,mode.X,mode.Y);
  if(!current.SameSurface(target))throw new IOException("Ekran çözünürlüğü veya yerleşimi değişti; yeniden tara.");
  if(!Read().Any(s=>s.Current.Device==target.Device&&s.Modes.Contains(target)))throw new InvalidDataException("Sürücünün sunduğu bir yenileme hızı seç.");
  mode.Fields=0x00400000;mode.Hz=target.Hz;return mode;
 }
 public void Test(DisplayMode target){var mode=Native(target);Check(ChangeDisplaySettingsEx(target.Device,ref mode,IntPtr.Zero,2,IntPtr.Zero));}
 public void Apply(DisplayMode target,bool persist){var mode=Native(target);Check(ChangeDisplaySettingsEx(target.Device,ref mode,IntPtr.Zero,persist?1u:0u,IntPtr.Zero));}
 private static void Check(int code){if(code!=0)throw new IOException("Ekran hızı uygulanamadı (Windows "+code+"). Yeniden başlatma isteyen veya desteklenmeyen mod kullanılmadı.");}
}
public sealed class DisplaySettings:IDisposable
{
 private readonly IDisplayBackend backend;private readonly string path;private readonly object gate=new();private Timer? timer;private string? timerError;
 public DisplayBackup? Backup{get;private set;}
 public DisplaySettings(IDisplayBackend? backend=null,string? path=null){this.backend=backend??new WindowsDisplayBackend();this.path=path??LocalStore.FilePath("display-profile.json");if(File.Exists(this.path)){Backup=JsonSerializer.Deserialize<DisplayBackup>(File.ReadAllText(this.path),LocalStore.Json)??throw new InvalidDataException("Ekran yedeği okunamadı.");Validate(Backup);}}
 private static void Validate(DisplayBackup backup){if(!backup.Original.SameSurface(backup.Applied)||backup.Original.Width==0||backup.Original.Height==0||backup.Original.Bits!=32||!backup.Original.Device.StartsWith(@"\\.\DISPLAY",StringComparison.Ordinal)||backup.Original.Hz is <24 or >1000||backup.Applied.Hz is <24 or >1000)throw new InvalidDataException("Ekran yedeği geçersiz.");}
 private void Save(){var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(Backup,LocalStore.Json));File.Move(temp,path,true);}
 public IReadOnlyList<DisplayScreen> Read()=>backend.Read();
 public void Begin(DisplayMode target,TimeSpan? timeout=null)
 {
  lock(gate){
   if(Backup!=null)throw new InvalidOperationException("Önce etkin ekran profilini geri al.");timerError=null;
   var screen=backend.Read().SingleOrDefault(s=>s.Current.Device==target.Device)??throw new IOException("Ekran bulunamadı.");
   if(!screen.Current.SameSurface(target)||!screen.Modes.Contains(target))throw new InvalidDataException("Yalnız mevcut çözünürlükte, sürücünün sunduğu Hz seçilebilir.");
   if(screen.Current==target)throw new InvalidOperationException("Bu yenileme hızı zaten etkin.");
   if(timeout is{} delay&&(delay<=TimeSpan.Zero||delay>TimeSpan.FromSeconds(30)))throw new InvalidDataException("Geçersiz ekran deneme süresi.");
   backend.Test(target);Backup=new(screen.Current,target,false);Validate(Backup);Save();
   // Arm before changing the display so a blocked UI cannot prevent recovery.
   timer=new Timer(_=>{lock(gate){try{RestoreCore();}catch(Exception ex){timerError=ex.Message;Journal.Write("Ekran zaman aşımı geri alma",ex.Message,false);}}},null,timeout??TimeSpan.FromSeconds(15),Timeout.InfiniteTimeSpan);
   try{backend.Apply(target,false);RequireCurrent(target);}catch{RestoreCore();throw;}
  }
 }
 private void RequireCurrent(DisplayMode expected){if(backend.Read().SingleOrDefault(s=>s.Current.Device==expected.Device)?.Current!=expected)throw new IOException("Sürücü istenen ekran hızını doğrulamadı.");}
 public void Keep()
 {
  lock(gate){
   if(Backup==null||Backup.Confirmed)throw new InvalidOperationException(timerError??"Onay süresi doldu; değişiklik geri alındı.");
   var state=Backup;timer?.Dispose();timer=null;
   try{RequireCurrent(state.Applied);backend.Apply(state.Applied,true);RequireCurrent(state.Applied);Backup=state with{Confirmed=true};Save();}
   catch{Backup=state;RestoreCore();throw;}
  }
 }
 private string RestoreCore()
 {
  timer?.Dispose();timer=null;var state=Backup;if(state==null){if(timerError!=null)throw new IOException(timerError);return "Etkin ekran değişikliği yok.";}
  var current=backend.Read().SingleOrDefault(s=>s.Current.Device==state.Applied.Device)?.Current;
  if(current==null)throw new IOException("Ekran bağlı değil; yedek korundu.");
  if(current==state.Applied){backend.Test(state.Original);backend.Apply(state.Original,true);RequireCurrent(state.Original);}
  File.Delete(path);Backup=null;return current==state.Applied?"Önceki ekran yenileme hızına dönüldü.":"Ekran ayarı sonradan değişmiş; yeni seçimin korundu.";
 }
 public string Restore(){lock(gate)return RestoreCore();}
 public void Dispose(){lock(gate){timer?.Dispose();timer=null;if(Backup is{Confirmed:false})RestoreCore();}}
}
