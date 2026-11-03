using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace FromZero;
public record PerformanceOptions(bool Power,bool Animations,bool NvidiaPower,bool NvidiaQuality,bool NvidiaRefresh=false,bool NvidiaVsyncOff=false);
public record PerformanceBackup(DateTimeOffset Created,string OriginalPower,string AppliedPower,bool OriginalAnimations,bool ChangedAnimations,List<NvidiaValue> Nvidia,PerformanceOptions Options);
public interface IPerformanceBackend
{
 string Power();Task<bool> HasHighPowerAsync();Task SetPowerAsync(string id);
 bool Animations();void SetAnimations(bool enabled);
 List<NvidiaValue> ReadNvidia(PerformanceOptions options);void ApplyNvidia(PerformanceOptions options);void RestoreNvidia(List<NvidiaValue> original,PerformanceOptions options);
}
public sealed class WindowsPerformanceBackend:IPerformanceBackend
{
 [DllImport("user32.dll",SetLastError=true)]private static extern bool SystemParametersInfo(uint action,uint parameter,ref int value,uint flags);
 [DllImport("user32.dll",SetLastError=true)]private static extern bool SystemParametersInfo(uint action,uint parameter,IntPtr value,uint flags);
 public string Power()=>GameService.ActivePlan();public Task<bool> HasHighPowerAsync()=>GameService.HasHighPerformanceAsync();public Task SetPowerAsync(string id)=>GameService.SetPowerPlanAsync(id);
 public bool Animations(){int value=0;if(!SystemParametersInfo(0x1042,0,ref value,0))throw new IOException("Animasyon tercihi okunamadı.");return value!=0;}
 public void SetAnimations(bool enabled){if(!SystemParametersInfo(0x1043,0,new IntPtr(enabled?1:0),3))throw new IOException("Windows animasyon tercihi uygulanamadı.");}
 public List<NvidiaValue> ReadNvidia(PerformanceOptions options){using var nvidia=new NvidiaSettings();return NvidiaSettings.Desired(options).Keys.Select(nvidia.Read).ToList();}
 public void ApplyNvidia(PerformanceOptions options){using var nvidia=new NvidiaSettings();foreach(var setting in NvidiaSettings.Desired(options))nvidia.Set(setting.Key,setting.Value);nvidia.Save();}
 public void RestoreNvidia(List<NvidiaValue> original,PerformanceOptions options){using var nvidia=new NvidiaSettings();var desired=NvidiaSettings.Desired(options);foreach(var setting in original){var current=nvidia.Read(setting.Id);if(desired.TryGetValue(setting.Id,out var applied)&&current.Value==applied)nvidia.Restore(setting);}nvidia.Save();}
}
public sealed class PerformanceProfile
{
 public const string HighPower="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
 private readonly IPerformanceBackend backend;
 private readonly string path;
 public PerformanceBackup? Backup{get;private set;}
 public PerformanceProfile(IPerformanceBackend? backend=null,string? path=null)
 {
  this.backend=backend??new WindowsPerformanceBackend();this.path=path??LocalStore.FilePath("performance-profile.json");
  if(File.Exists(this.path)){Backup=JsonSerializer.Deserialize<PerformanceBackup>(File.ReadAllText(this.path),LocalStore.Json)??throw new InvalidDataException("Performans yedeği okunamadı.");Validate(Backup);}
 }
 private static void Validate(PerformanceBackup b)
 {
  if(!Guid.TryParse(b.OriginalPower,out _)||!Guid.TryParse(b.AppliedPower,out _))throw new InvalidDataException("Güç planı kaydı geçersiz.");
  var desired=NvidiaSettings.Desired(b.Options);
  if(b.Nvidia.Any(s=>!NvidiaSettings.Known(s.Id)||!desired.ContainsKey(s.Id)||!NvidiaSettings.Valid(s.Id,s.Value))||b.Nvidia.Select(s=>s.Id).Distinct().Count()!=b.Nvidia.Count)throw new InvalidDataException("Desteklenmeyen GPU ayarı yedekte bulundu.");
 }
 private void Save(){var tmp=path+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(Backup,LocalStore.Json));File.Move(tmp,path,true);}
 public async Task<string> ApplyAsync(PerformanceOptions options)
 {
  if(Backup!=null)throw new InvalidOperationException("Önce etkin performans profilini geri al.");
  var original=backend.Power();var applied=options.Power&&await backend.HasHighPowerAsync()?HighPower:original;
  var animations=backend.Animations();var desired=NvidiaSettings.Desired(options);var nvidia=desired.Count>0?backend.ReadNvidia(options):[];
  if(nvidia.Count!=desired.Count||nvidia.Select(s=>s.Id).Distinct().Count()!=desired.Count||nvidia.Any(s=>!desired.ContainsKey(s.Id)||!NvidiaSettings.Valid(s.Id,s.Value)))throw new InvalidDataException("Seçilen NVIDIA ayarları güvenle yedeklenemedi; değişiklik yapılmadı.");
  Backup=new(DateTimeOffset.Now,original,applied,animations,options.Animations,nvidia,options);Save();
  try{
   if(applied!=original)await backend.SetPowerAsync(applied);
   if(options.Animations)backend.SetAnimations(false);
   if(nvidia.Count>0)backend.ApplyNvidia(options);
   if(nvidia.Count>0){var read=backend.ReadNvidia(options);if(read.Count!=desired.Count||desired.Any(s=>read.SingleOrDefault(r=>r.Id==s.Key)?.Value!=s.Value))throw new IOException("NVIDIA sürücüsü seçilen değerleri doğrulamadı; profil geri alınıyor.");}
   var summary=(applied!=original?"• Windows Yüksek Performans güç planı etkin.\n":options.Power?(original==HighPower?"• Windows Yüksek Performans güç planı zaten etkin.\n":"• Cihaz Yüksek Performans planı sunmuyor; mevcut plan korundu.\n"):"")+(options.Animations?"• Windows istemci alanı animasyonları kapatıldı.\n":"")+string.Join("\n",desired.Select(s=>"• NVIDIA "+NvidiaSettings.Name(s.Key)+": "+NvidiaSettings.Describe(new(s.Key,s.Value,true,0,""))))+"\n\nGenel 3D profili kaydedildi; oyuna özel sürücü profilleri öncelikli olabilir. Ekranın gerçek Hz'si ayrı ayarlanır. Profil kalıcıdır; Geri al düğmesiyle önceki ayarlara dönebilirsin. Yeni oyun başlatmalarında etkili olur. FPS kazanımı ölçülmedi.";
   Journal.Write("Performans profili uygulandı",summary);return summary;
  }catch{await RestoreAsync();throw;}
 }
 public async Task<string> RestoreAsync()
 {
  var state=Backup;if(state==null)return "Etkin profil yok.";
  var errors=new List<string>();
  try{if(state.Nvidia.Count>0)backend.RestoreNvidia(state.Nvidia,state.Options);}catch(Exception ex){errors.Add("NVIDIA: "+ex.Message);}
  try{if(state.ChangedAnimations&&!backend.Animations())backend.SetAnimations(state.OriginalAnimations);}catch(Exception ex){errors.Add("Windows: "+ex.Message);}
  try{if(backend.Power()==state.AppliedPower&&state.AppliedPower!=state.OriginalPower)await backend.SetPowerAsync(state.OriginalPower);}catch(Exception ex){errors.Add("Güç planı: "+ex.Message);}
  if(errors.Count>0){Journal.Write("Performans geri alma eksik",string.Join("\n",errors),false);throw new InvalidOperationException("Bazı ayarlar geri alınamadı; yedek korundu.\n"+string.Join("\n",errors));}
  File.Delete(path);Backup=null;Journal.Write("Performans profili geri alındı","Oturum dışında değiştirilen ayarlar korunur.");return "Profilin değiştirdiği ayarlar geri alındı. Sonradan değiştirdiğin değerlere dokunulmadı.";
 }
}
