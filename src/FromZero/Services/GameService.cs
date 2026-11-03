using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FromZero;
public record GameProfile(string Name,string Executable,string Arguments,string ProcessName,string SteamId);
public record RegistrySnapshot(string Key,string Name,int Original,int Applied);
public record GameSession(DateTimeOffset Started,string OriginalPower,string AppliedPower,List<RegistrySnapshot> Registry,string Game,string ProcessName,bool SeenRunning);
public interface IGamePlatform
{
 string GetPlan();
 Task<bool> HasPerformancePlanAsync();
 Task SetPlanAsync(string id);
 int? ReadDword(string key,string name);
 void WriteDword(string key,string name,int value);
}
public interface IGameStateStore
{
 GameSession? Load();
 void Save(GameSession state);
 void Clear();
}
public sealed class FileGameStateStore : IGameStateStore
{
 private static string PathName=>LocalStore.FilePath("game-session.json");
 public GameSession? Load()=>File.Exists(PathName)?JsonSerializer.Deserialize<GameSession>(File.ReadAllText(PathName),LocalStore.Json):null;
 public void Save(GameSession state){var tmp=PathName+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(state,LocalStore.Json));File.Move(tmp,PathName,true);}
 public void Clear()=>File.Delete(PathName);
}
public sealed class WindowsGamePlatform : IGamePlatform
{
 public string GetPlan()=>GameService.ActivePlan();
 public Task<bool> HasPerformancePlanAsync()=>GameService.HasHighPerformanceAsync();
 public Task SetPlanAsync(string id)=>GameService.SetPowerPlanAsync(id);
 public int? ReadDword(string key,string name){using var k=Registry.CurrentUser.OpenSubKey(key);return k?.GetValue(name) is int value && k.GetValueKind(name)==RegistryValueKind.DWord?value:null;}
 public void WriteDword(string key,string name,int value){using var k=Registry.CurrentUser.OpenSubKey(key,true)??throw new IOException("Ayar anahtarı açılamadı.");k.SetValue(name,value,RegistryValueKind.DWord);}
}
public sealed class GameService
{
 private const string HighPerformance="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
 private readonly SemaphoreSlim gate=new(1,1);
 public GameSession? Session {get;private set;}
 private readonly IGamePlatform platform;
 private readonly IGameStateStore store;
 [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root,out IntPtr guid);
 [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
 public GameService(IGamePlatform? platform=null,IGameStateStore? store=null){this.platform=platform??new WindowsGamePlatform();this.store=store??new FileGameStateStore();try{Session=this.store.Load();ValidateSession(Session);}catch(Exception ex) when(ex is JsonException or InvalidDataException){Session=null;Journal.Write("Oyun oturumu kaydı okunamadı",ex.Message+" Güç planını Windows Ayarları'ndan kontrol edin.",false);}}
 private static void ValidateSession(GameSession? session){
  if(session==null)return;
  if(!Guid.TryParse(session.OriginalPower,out _)||!Guid.TryParse(session.AppliedPower,out _))throw new InvalidDataException("Oturumdaki güç planı kimliği geçersiz.");
  foreach(var r in session.Registry){var allowed=(r.Key==@"Software\Microsoft\GameBar"&&r.Name=="AutoGameModeEnabled"&&r.Applied==1)||(r.Key==@"System\GameConfigStore"&&r.Name=="GameDVR_Enabled"&&r.Applied==0)||(r.Key==@"Software\Microsoft\Windows\CurrentVersion\GameDVR"&&r.Name=="AppCaptureEnabled"&&r.Applied==0);if(!allowed)throw new InvalidDataException("Oturum kaydında desteklenmeyen kayıt ayarı.");}
 }
 public static string ActivePlan()
 {
  var result=PowerGetActiveScheme(IntPtr.Zero,out var ptr);if(result!=0)throw new InvalidOperationException("Güç planı okunamadı: "+result);
  try{return Marshal.PtrToStructure<Guid>(ptr).ToString();}finally{LocalFree(ptr);}
 }
 public static async Task<bool> HasHighPerformanceAsync(){var r=await Commands.RunAsync("powercfg.exe",["/list"]);return r.Success&&r.Output.Contains(HighPerformance,StringComparison.OrdinalIgnoreCase);}
 internal static async Task SetPowerPlanAsync(string id)
 {
  if(!Guid.TryParse(id,out var guid))throw new InvalidDataException("Güç planı kimliği geçersiz.");
  var result=await Commands.RunAsync("powercfg.exe",["/setactive",guid.ToString()]);
  if(!result.Success){result=await Commands.ElevatedAsync("& $env:SystemRoot\\System32\\powercfg.exe /setactive '"+guid+"'; if ($LASTEXITCODE -ne 0) { throw 'Güç planı değiştirilemedi.' }");}
  if(!result.Success || !ActivePlan().Equals(guid.ToString(),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Güç planı etkinleştirilemedi.");
 }
 private void Save(){if(Session!=null)store.Save(Session);}
 public async Task BeginAsync(string game,string processName,bool highPower,bool gameMode,bool noRecording)
 {
  await gate.WaitAsync();
  try{
   if(Session!=null)throw new InvalidOperationException("Önce mevcut oyun oturumunu geri al.");
   var snapshots=new List<RegistrySnapshot>();
   void Plan(string keyName,string valueName,int applied){
    var original=platform.ReadDword(keyName,valueName);
    if(original==null)throw new InvalidOperationException(valueName+" mevcut bir DWORD ayarı değil. Windows Ayarları üzerinden yapılandır; tahmini kayıt oluşturulmadı.");
    snapshots.Add(new(keyName,valueName,original.Value,applied));
   }
   if(gameMode)Plan(@"Software\Microsoft\GameBar","AutoGameModeEnabled",1);
   if(noRecording){Plan(@"System\GameConfigStore","GameDVR_Enabled",0);Plan(@"Software\Microsoft\Windows\CurrentVersion\GameDVR","AppCaptureEnabled",0);}
   if(highPower&&!await platform.HasPerformancePlanAsync())throw new InvalidOperationException("Bu bilgisayar Yüksek Performans güç planını sunmuyor. Üreticinin güç yönetimi kullanılabilir; bu seçeneği kapatıp devam et.");
   var original=platform.GetPlan();Session=new(DateTimeOffset.Now,original,highPower?HighPerformance:original,snapshots,game,processName,false);Save();
   try{
    if(highPower)await platform.SetPlanAsync(HighPerformance);
    foreach(var item in snapshots)platform.WriteDword(item.Key,item.Name,item.Applied);
    Journal.Write("Oyun modu hazırlandı",game+"; yüksek performans: "+highPower+"; kayıt ayarı: "+noRecording+"; oyun modu tercihi: "+gameMode);
   }catch{await RestoreUnlockedAsync();throw;}
  }finally{gate.Release();}
 }
 public async Task<string> RestoreAsync(){await gate.WaitAsync();try{return await RestoreUnlockedAsync();}finally{gate.Release();}}
 private async Task<string> RestoreUnlockedAsync()
 {
  if(Session==null)return "Etkin oyun oturumu yok.";
  var state=Session;var remaining=new List<RegistrySnapshot>();var notes=new List<string>();
  foreach(var item in state.Registry){
   try{
    if(platform.ReadDword(item.Key,item.Name) is int current && current==item.Applied){platform.WriteDword(item.Key,item.Name,item.Original);notes.Add(item.Name+" geri alındı.");}
    else notes.Add(item.Name+" oturum dışında değişmiş veya silinmiş; üzerine yazılmadı.");
   }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){remaining.Add(item);notes.Add(item.Name+": "+ex.Message);}
  }
  var planOk=false;
  try{
   var current=platform.GetPlan();
   if(current==state.AppliedPower && current!=state.OriginalPower)await platform.SetPlanAsync(state.OriginalPower);
   else if(current!=state.OriginalPower)notes.Add("Güç planı oturum dışında değişmiş; seçimin korundu.");
   planOk=true;
  }catch(Exception ex){notes.Add("Güç planı geri alınamadı: "+ex.Message);}
  if(remaining.Count==0&&planOk){store.Clear();Session=null;notes.Add("Oturum kapandı.");}
  else {Session=state with { Registry=remaining };Save();}
  var result=string.Join("\n",notes);Journal.Write("Oyun modu geri alma",result,Session==null);return result;
 }
 public async Task<string?> PollAsync()
 {
  var session=Session;if(session==null||string.IsNullOrWhiteSpace(session.ProcessName))return null;
  var running=Process.GetProcessesByName(session.ProcessName);bool found=running.Length>0;foreach(var p in running)p.Dispose();
  if(found&&!session.SeenRunning){await gate.WaitAsync();try{if(Session==session){Session=session with{SeenRunning=true};Save();}}finally{gate.Release();}return null;}
  if(!found&&session.SeenRunning)return await RestoreAsync();
  if(!found&&!session.SeenRunning&&DateTimeOffset.Now-session.Started>TimeSpan.FromMinutes(5))return "Oyun 5 dakika içinde görülmedi. "+await RestoreAsync();
  return null;
 }
 public static List<GameProfile> ReadProfiles(){var path=LocalStore.FilePath("games.json");if(!File.Exists(path))return [];return JsonSerializer.Deserialize<List<GameProfile>>(File.ReadAllText(path),LocalStore.Json)??[];}
 public static void SaveProfile(GameProfile profile){var list=ReadProfiles();list.RemoveAll(g=>g.Name.Equals(profile.Name,StringComparison.OrdinalIgnoreCase));list.Add(profile);File.WriteAllText(LocalStore.FilePath("games.json"),JsonSerializer.Serialize(list,LocalStore.Json));}
 public static List<GameProfile> DetectSteam()
 {
  using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");var root=key?.GetValue("SteamPath")?.ToString();if(root==null||!Directory.Exists(root))return [];
  var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase){root};var libraries=Path.Combine(root,"steamapps","libraryfolders.vdf");
  if(File.Exists(libraries))foreach(Match m in Regex.Matches(File.ReadAllText(libraries),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
  var result=new List<GameProfile>();
  foreach(var library in roots){var apps=Path.Combine(library,"steamapps");if(!Directory.Exists(apps))continue;
   foreach(var file in Directory.EnumerateFiles(apps,"appmanifest_*.acf")){
    var text=File.ReadAllText(file);string Value(string name)=>Regex.Match(text,"\""+name+"\"\\s+\"([^\"]+)\"").Groups[1].Value;
    var id=Value("appid");var name=Value("name");if(!uint.TryParse(id,out _)||name.Length==0)continue;
    var process=id switch{"730"=>"cs2","570"=>"dota2","578080"=>"TslGame","1172470"=>"r5apex",_=>""};result.Add(new(name,Path.Combine(root,"steam.exe"),"",process,id));
   }
  }
  return result.DistinctBy(p=>p.SteamId).ToList();
 }
 public static Process? Launch(GameProfile profile)
 {
  if(!File.Exists(profile.Executable)||!Path.GetExtension(profile.Executable).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Oyun/Steam çalıştırılabilir dosyası bulunamadı.");
  var info=new ProcessStartInfo(profile.Executable){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(profile.Executable)};
  if(profile.SteamId.Length>0){if(!uint.TryParse(profile.SteamId,out _))throw new InvalidDataException("Steam oyun kimliği geçersiz.");info.ArgumentList.Add("-applaunch");info.ArgumentList.Add(profile.SteamId);}
  foreach(var arg in SplitArguments(profile.Arguments))info.ArgumentList.Add(arg);
  var process=Process.Start(info);Journal.Write("Oyun başlatıldı",profile.Name);return process;
 }
 public static List<string> SplitArguments(string arguments)
 {
  if(arguments.IndexOfAny(['\r','\n','\0'])>=0)throw new InvalidDataException("Başlatma seçenekleri tek satır olmalı.");
  var result=new List<string>();var sb=new System.Text.StringBuilder();bool quoted=false,active=false;
  foreach(var c in arguments){if(c=='"'){quoted=!quoted;active=true;}else if(char.IsWhiteSpace(c)&&!quoted){if(active){result.Add(sb.ToString());sb.Clear();active=false;}}else{sb.Append(c);active=true;}}
  if(quoted)throw new InvalidDataException("Başlatma seçeneklerinde kapanmamış tırnak var.");if(active)result.Add(sb.ToString());return result;
 }
}
