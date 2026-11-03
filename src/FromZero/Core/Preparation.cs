using System.IO;
using System.Text.Json;
namespace FromZero;

public sealed record PreparationOptions
{
 public string Profile{get;init;}="basic";
 public List<string> Apps{get;init;}=[];
 public bool WindowsUpdates{get;init;}=true;
 public bool Drivers{get;init;}=true;
 public bool GamingLibraries{get;init;}
 public bool CoreLibraries{get;init;}=true;
 public List<string> RequiredLibraries{get;init;}=[];
 public bool GpuApps{get;init;}=true;
 public bool OemDrivers{get;init;}=true;
 public bool ResumeAtLogon{get;init;}=true;
 public string OfflineFolder{get;init;}="";
 public List<string> Games{get;init;}=[];
 public string PowerPreference{get;init;}="none";
 public string GraphicsPreference{get;init;}="none";
}
public sealed record PreparationStep
{
 public string Id{get;init;}="";public string Title{get;init;}="";public string Kind{get;init;}="";
 public string State{get;set;}="pending";public string Detail{get;set;}="";public int Attempts{get;set;}
 public DateTimeOffset? Started{get;set;}public DateTimeOffset? Finished{get;set;}
}
public sealed record PreparationJob
{
 public int Schema{get;init;}=2;public string Id{get;init;}=Guid.NewGuid().ToString("N");
 public string UserSid{get;init;}="";public string Machine{get;init;}=Environment.MachineName;public string Executable{get;init;}="";
 public DateTimeOffset Created{get;init;}=DateTimeOffset.Now;public DateTimeOffset? Consented{get;set;}
 public PreparationOptions Options{get;init;}=new();public List<PreparationStep> Steps{get;init;}=[];
 public string State{get;set;}="planned";public string Message{get;set;}="Plan hazır.";
 public DateTimeOffset Updated{get;set;}=DateTimeOffset.Now;public DateTimeOffset BootTime{get;set;}
 public bool RebootRequired{get;set;}public int Pass{get;set;}
}
public static class PreparationPolicy
{
 public static readonly string[] AppIds=PreparationExperience.Apps.Select(a=>a.Id).ToArray();
 public static readonly string[] PackageIds=new[]{"firefox","prepare-chrome","prepare-vlc","steam","prepare-vscode","vc-x64","vc-x86","vc2005-x64","vc2005-x86","vc2008-x64","vc2008-x86","vc2010-x64","vc2010-x86","vc2012-x64","vc2012-x86","vc2013-x64","vc2013-x86","directx","directx-offline","nvidia","amd","intel","dell-command","hp-image-assistant","dotnet-desktop-10-0-win-x64"}.Concat(AppIds.Where(id=>id is not("firefox" or "chrome" or "vlc" or "steam" or "vscode")).Select(id=>"app-"+id)).ToArray();
 public static bool Done(PreparationStep step)=>step.State is "done" or "skipped";
 public static bool SameBoot(DateTimeOffset first,DateTimeOffset second)=>Math.Abs((first-second).TotalSeconds)<120;
 public static bool Pending(PreparationJob job)=>job.State is "running" or "paused" or "reboot" or "interrupted" or "attention";
 public static PreparationJob Create(PreparationOptions options,string sid,string executable)
 {
  ValidateOptions(options);
  List<PreparationStep> steps=[];
  void Add(string id,string title,string kind)=>steps.Add(new(){Id=id,Title=title,Kind=kind});
  Add("preflight","Bilgisayarı ve bağlantıyı kontrol et","preflight");Add("baseline","Kurulum öncesi durumu kaydet","baseline");
  if(options.Drivers){Add("backup","Mevcut sürücüleri yedekle","backup");if(options.OfflineFolder.Length>0)Add("offline","USB'deki eşleşen sürücüleri kur","offline");Add("drivers","Ağ, yonga seti, ekran kartı ve diğer sürücüler","drivers");if(options.OemDrivers)Add("oem","Desteklenen üreticide modele özel sürücüler","oem");}
  if(options.WindowsUpdates)Add("windows","Windows güvenlik ve kararlılık güncellemeleri","windows");
  if(options.CoreLibraries){Add("package-vc-x64","Visual C++ v14 x64","package");Add("package-vc-x86","Visual C++ v14 x86","package");}
  if(options.GamingLibraries){Add("package-directx-offline","DirectX ek oyun kütüphaneleri (tam paket)","package");foreach(var year in new[]{"2005","2008","2010","2012","2013"})foreach(var arch in new[]{"x64","x86"})Add("package-vc"+year+"-"+arch,"Visual C++ "+year+" "+arch,"package");}
  foreach(var id in options.RequiredLibraries.Distinct().Where(id=>!steps.Any(s=>s.Id=="package-"+id)))Add("package-"+id,id=="directx-offline"?"DirectX ek oyun kütüphaneleri (tam paket)":id.StartsWith("vc-")?"Visual C++ v14 "+id[3..]:"Visual C++ "+id[2..].Replace("-"," "),"package");
  if(options.GpuApps)Add("gpu-apps","Algılanan ekran kartının üretici uygulaması","gpu-apps");
  foreach(var id in options.Apps.Distinct())Add("app-"+id,PreparationExperience.Name(id),"app");
  if(options.PowerPreference!="none"||options.GraphicsPreference!="none")Add("preferences","Seçilen kullanım ayarlarını uygula","preferences");
  if(options.Games.Count>0)Add("games","Seçilen oyunları kullanıma hazırlama kontrolü","games");
  Add("verify","Kurulumları ve kalan eksikleri doğrula","verify");
  return new(){UserSid=sid,Executable=executable,Options=options,Steps=steps};
 }
 public static void ValidateOptions(PreparationOptions? options)
 {
  if(options==null||options.Apps==null||options.OfflineFolder==null||options.Games==null||options.RequiredLibraries==null)throw new InvalidDataException("Hazırlama seçimleri eksik.");
  if(options.RequiredLibraries.Count>13||options.RequiredLibraries.Distinct().Count()!=options.RequiredLibraries.Count||options.RequiredLibraries.Any(id=>!PackageIds.Contains(id)||!(id.StartsWith("vc",StringComparison.Ordinal)||id=="directx-offline")))throw new InvalidDataException("Unsupported runtime selection.");
  if(options.GraphicsPreference!="none"&&!options.Games.Any(g=>g is "cs2" or "valorant"))throw new InvalidDataException("Görüntü tercihi için önce CS2 veya VALORANT seç.");
  if(options.Games.Count>PreparationExperience.Games.Count||options.Games.Distinct().Count()!=options.Games.Count||options.Games.Any(id=>!PreparationExperience.Games.Any(g=>g.Id==id))||options.PowerPreference is not("none" or "balanced" or "performance" or "quiet")||options.GraphicsPreference is not("none" or "balanced" or "fps"))throw new InvalidDataException("Oyun veya kullanım tercihi geçersiz.");
  if((!PreparationExperience.Profiles.Any(p=>p.Id==options.Profile)&&options.Profile!="custom")||options.Apps.Count>AppIds.Length||options.Apps.Distinct().Count()!=options.Apps.Count||options.Apps.Any(id=>!AppIds.Contains(id)))throw new InvalidDataException("Hazırlama profili veya uygulama seçimi geçersiz.");
  if(options.OfflineFolder.Length>0&&(!Path.IsPathFullyQualified(options.OfflineFolder)||!Directory.Exists(options.OfflineFolder)))throw new InvalidDataException("Yerel sürücü klasörü bulunamadı.");
 }
 public static void Validate(PreparationJob job,string sid)
 {
  if(job==null||job.Steps==null||job.Steps.Any(s=>s==null))throw new InvalidDataException("Hazırlama adımları eksik.");
  ValidateOptions(job.Options);
  if(job.Schema!=2||!Guid.TryParseExact(job.Id,"N",out _)||job.UserSid!=sid||job.Machine!=Environment.MachineName)throw new InvalidDataException("Hazırlama kaydı bu kullanıcıya veya bilgisayara ait değil.");
  var expected=Create(job.Options,sid,job.Executable).Steps;
  if(job.Steps.Count!=expected.Count||!job.Steps.Zip(expected).All(p=>p.First.Id==p.Second.Id&&p.First.Kind==p.Second.Kind&&p.First.Title==p.Second.Title)||job.Pass is <0 or >4||job.State is not("planned" or "running" or "paused" or "reboot" or "attention" or "complete" or "interrupted"))throw new InvalidDataException("Hazırlama adımları değiştirilmiş veya geçersiz.");
  if(job.Steps.Any(s=>s.Attempts is <0 or >12||s.State is not("pending" or "running" or "done" or "skipped" or "failed" or "attention" or "reboot")))throw new InvalidDataException("Hazırlama durum kaydı geçersiz.");
 }
 public static PreparationJob Upgrade(PreparationJob old)
 {
  if(old.Schema!=1)return old;ValidateOptions(old.Options);
  var upgraded=Create(old.Options,old.UserSid,old.Executable) with{Id=old.Id,Machine=old.Machine,Created=old.Created,State="planned",Message="Önceki plan yeni özelliklere göre yenilendi. Yeni adımları inceleyip yeniden onayla."};
  foreach(var step in upgraded.Steps.Where(s=>s.Kind is "app" or "package" or "backup")){var previous=old.Steps?.FirstOrDefault(s=>s.Id==step.Id&&Done(s));if(previous!=null){step.State=previous.State;step.Detail=previous.Detail;}}
  return upgraded;
 }
}
public interface IPreparationBackend
{
 Task<PreparationOutcome> ExecuteAsync(PreparationJob job,PreparationStep step,IProgress<string> progress,CancellationToken token);
}
public record PreparationOutcome(string State,string Detail,bool Reboot=false,bool Hold=false);
public interface IPreparationResumeVerifier{Task<string?> MissingCompletedAsync(PreparationJob job,PreparationStep step,CancellationToken token);}
public sealed class PreparationEngine(IPreparationBackend backend,Action<PreparationJob> save,Func<bool> stop)
{
 public async Task RunAsync(PreparationJob job,DateTimeOffset boot,IProgress<string> progress,CancellationToken token=default)
 {
  if(job.Consented==null)throw new InvalidOperationException("Kurulum planı henüz kabul edilmedi.");
  PreparationAudit.Read(job);
  if(job.State is "paused" or "interrupted" or "attention" or "reboot"&&backend is IPreparationResumeVerifier verifier)foreach(var completed in job.Steps.Where(s=>s.State=="done"&&s.Kind is "app" or "package")){
   var missing=await verifier.MissingCompletedAsync(job,completed,token);if(missing!=null){completed.State="attention";completed.Detail=missing;job.State="attention";job.Message="A previously completed installation changed. Review the plan before resuming. "+missing;PreparationAudit.Append(job,completed,"resume-review");save(job);return;}
  }
  if(job.State=="reboot"&&PreparationPolicy.SameBoot(job.BootTime,boot)){job.Message="Önce bilgisayarı yeniden başlat. Tamamlanan işlemler korunur.";save(job);return;}
  var restarted=!PreparationPolicy.SameBoot(job.BootTime,boot);job.BootTime=boot;
  if(job.State!="complete"&&job.Steps.Any(s=>!PreparationPolicy.Done(s)))foreach(var verify in job.Steps.Where(s=>s.Kind=="verify"))verify.State="pending";
  if(restarted){job.RebootRequired=false;foreach(var step in job.Steps.Where(s=>s.State=="reboot")){step.State="pending";}if(job.Pass>0){foreach(var step in job.Steps.Where(s=>s.Kind is "drivers" or "windows" or "verify"))step.State="pending";}}
  job.State="running";save(job);
  foreach(var step in job.Steps){
   if(PreparationPolicy.Done(step))continue;
   if(step.State=="running")PreparationAudit.Append(job,step,"interrupted-attempt");
   if(step.Kind is "drivers" or "oem" or "offline"&&job.Steps.Any(s=>s.Kind=="backup"&&s.State!="done")){step.State="attention";step.Detail="Sürücü yedeği alınamadığı için kurulum yapılmadı.";save(job);continue;}
   if(stop()||token.IsCancellationRequested){job.State="paused";job.Message="İşlem güvenli bir adım sınırında durduruldu.";save(job);return;}
   if(step.Attempts>=4){step.State="attention";step.Detail="Dört denemede tamamlanamadı. Ayrıntıları kontrol et.";save(job);continue;}
   step.State="running";step.Attempts++;step.Started=DateTimeOffset.Now;step.Finished=null;job.Message=step.Title;save(job);PreparationAudit.Append(job,step,"started");progress.Report(step.Title);
   try{
    var result=await backend.ExecuteAsync(job,step,progress,token);
    if(result.State is not("done" or "skipped" or "attention" or "failed"))throw new InvalidDataException("Kurulum sonucu geçersiz.");
    step.State=result.State;step.Detail=result.Detail;job.RebootRequired|=result.Reboot;
    if(result.Reboot){if(job.Pass>=4){step.State="attention";job.State="attention";job.Message="Dört yeniden başlatma turunda tamamlanamadı. Kurulum raporunu incele.";}else{step.State="reboot";job.State="reboot";job.Pass++;job.Message="Devam etmek için yeniden başlatma gerekiyor.";}step.Finished=DateTimeOffset.Now;PreparationAudit.Append(job,step,"reboot");save(job);return;}
    if(result.Hold){step.Finished=DateTimeOffset.Now;job.State="attention";job.Message=step.Detail;PreparationAudit.Append(job,step,"hold");save(job);return;}
   }catch(OperationCanceledException){step.State="pending";job.State="paused";job.Message="Durduruldu; kaldığın yerden devam edebilirsin.";PreparationAudit.Append(job,step,"canceled");save(job);return;}
   catch(Exception ex){step.State="failed";step.Detail=ex.Message;}
   step.Finished=DateTimeOffset.Now;PreparationAudit.Append(job,step,"finished");save(job);
   if(step.Kind=="preflight"&&step.State!="done"){job.State="attention";job.Message=step.Detail;save(job);return;}
   if(step.Kind=="backup"&&step.State!="done"){foreach(var next in job.Steps.Where(s=>s.Kind is "drivers" or "oem" or "offline"&&!PreparationPolicy.Done(s))){next.State="attention";next.Detail="Sürücü yedeği alınamadığı için kurulum yapılmadı.";}}
  }
  job.State=job.Steps.Any(s=>s.State is "failed" or "attention")?"attention":"complete";
  job.Message=job.State=="complete"?"Seçilen hazırlama işlemleri tamamlandı ve kontrol edildi.":"Hazırlama tamamlandı; kontrol edilmesi gereken işler var.";save(job);
 }
}
public static class PreparationStore
{
 public static string Folder(string id){if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Hazırlama kimliği geçersiz.");var path=LocalStore.FilePath(Path.Combine("Preparation",id));Directory.CreateDirectory(path);PackageSafety.RejectReparsePoints(LocalStore.Root,path);return path;}
 public static string PathFor(string id)=>Path.Combine(Folder(id),"state.json");
 public static void Save(PreparationJob job)=>SaveState(job,true);
 public static void SaveState(PreparationJob job,bool remember)
 {
  job.Updated=DateTimeOffset.Now;
  Replace(PathFor(job.Id),JsonSerializer.Serialize(job,LocalStore.Json));
  if(remember)Replace(LocalStore.FilePath("last-preparation.txt"),job.Id);
  static void Replace(string path,string content)
  {
   var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try
   {
    File.WriteAllText(temporary,content);
    using(var file=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))file.Flush(true);
    File.Move(temporary,path,true);
   }
   finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
 public static PreparationJob? Load(string id){var path=PathFor(id);if(!File.Exists(path))return null;if(new FileInfo(path).Length>4_000_000)throw new InvalidDataException("Hazırlama kaydı çok büyük.");var job=JsonSerializer.Deserialize<PreparationJob>(File.ReadAllText(path),LocalStore.Json);return job!=null?PreparationPolicy.Upgrade(job):null;}
 public static List<PreparationJob> Recent(string sid,int count=12)
 {
  var root=LocalStore.FilePath("Preparation");if(!Directory.Exists(root))return [];PackageSafety.RejectReparsePoints(LocalStore.Root,root);List<PreparationJob> jobs=[];
  foreach(var folder in Directory.EnumerateDirectories(root).Where(p=>Guid.TryParseExact(Path.GetFileName(p),"N",out _)).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(Math.Clamp(count,1,30))){try{var job=Load(Path.GetFileName(folder));if(job!=null){PreparationPolicy.Validate(job,sid);jobs.Add(job);}}catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException){}}
  return jobs.OrderByDescending(j=>j.Created).ToList();
 }
 public static PreparationJob? Last(){try{var path=LocalStore.FilePath("last-preparation.txt");var job=File.Exists(path)?Load(File.ReadAllText(path).Trim()):null;if(job!=null)PreparationPolicy.Validate(job,job.UserSid);return job;}catch(Exception ex)when(ex is IOException or JsonException){return null;}}
}


