using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
namespace FromZero;
public static class PreparationWorker
{
 public static bool Running(string id){try{using var mutex=Mutex.OpenExisting("Local\\FromZero-Preparation-"+PreparationBackend.Sid+"-"+id);return true;}catch(WaitHandleCannotBeOpenedException){return false;}}
 public static async Task RunAsync(string id,bool checkOnly=false)
 {
  using var mutex=new Mutex(true,"Local\\FromZero-Preparation-"+PreparationBackend.Sid+"-"+id,out var first);if(!first)return;
  using var serial=new Mutex(true,"Local\\FromZero-Preparation-Active-"+PreparationBackend.Sid,out var only);if(!only)throw new InvalidOperationException("Başka bir hazırlama işlemi çalışıyor.");
  var job=PreparationStore.Load(id)??throw new InvalidDataException("Hazırlama planı bulunamadı.");PreparationPolicy.Validate(job,PreparationBackend.Sid);
  if(!Path.GetFullPath(Environment.ProcessPath??"").Equals(Path.GetFullPath(job.Executable),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Uygulamanın konumu değişmiş. Yeni plan oluştur.");
  if(checkOnly){var readiness=PreparationBackend.InspectPreflight(job.Options);await PreparationRecovery.WriteAsync(job,"startup-check",new{Checked=DateTimeOffset.Now,PlanValid=true,UserMatches=true,ExecutableMatches=true,Elevated=IsElevated(),CheckOnly=true,ReadyToInstall=readiness.Ready,Preflight=readiness,InstallStepsStarted=job.Steps.Count(s=>s.Attempts>0)});return;}
  if(job.Consented==null)throw new InvalidOperationException("Hazırlama planı henüz kabul edilmedi. Önce uygulamada planı inceleyip onayla.");
  if(job.State=="complete")return;if(!PreparationBackend.IsAdmin)throw new UnauthorizedAccessException("Kurulum motoru yönetici izni olmadan çalışmaz.");
  var previousError=ErrorPath(id);if(File.Exists(previousError))File.Delete(previousError);
  var stopFile=Path.Combine(PreparationStore.Folder(id),"stop");if(File.Exists(stopFile))File.Delete(stopFile);SaveResume(job);
  try{
   foreach(var step in job.Steps.Where(s=>s.State=="running")){PreparationAudit.Append(job,step,"interrupted-attempt");job.State="interrupted";step.State="pending";step.Detail="Önceki işlem kesildi; kurulum kaydı kontrol edilerek devam edilecek.";}
   var engine=new PreparationEngine(new PreparationBackend(),PreparationStore.Save,()=>File.Exists(stopFile));
   await engine.RunAsync(job,PreparationBackend.Boot,new ImmediateProgress(message=>{job.Message=message;PreparationStore.Save(job);}));
   Journal.Write("Hazırlama sonucu",job.State+" / "+job.Message,job.State=="complete");
  }catch(Exception ex){job.State="attention";job.Message=ex.Message;PreparationStore.Save(job);Journal.Write("Hazırlama motoru",ex.ToString(),false);}
  finally{if(job.State=="complete"||!job.Options.ResumeAtLogon)RemoveResume();}
 }
 public static async Task LaunchAsync(PreparationJob job,IProgress<PreparationJob> progress)
 {
  PreparationPolicy.Validate(job,PreparationBackend.Sid);if(job.Consented==null)throw new InvalidOperationException("Önce kurulum planını kabul et.");
  if(!Path.GetFullPath(job.Executable).Equals(Path.GetFullPath(Environment.ProcessPath!),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Kayıtlı uygulama yolu değişmiş; yeni plan oluştur.");
  if(Running(job.Id)){while(Running(job.Id)){await Task.Delay(800);var active=PreparationStore.Load(job.Id);if(active!=null)progress.Report(active);}var ended=PreparationStore.Load(job.Id);if(ended!=null)progress.Report(ended);return;}PreparationStore.Save(job);
  var info=StartInfo(job.Executable,job.Id,IsElevated());
  using var process=Process.Start(info)??throw new IOException("Hazırlama başlatılamadı.");
  while(!process.HasExited){await Task.Delay(800);try{var current=PreparationStore.Load(job.Id);if(current!=null){var active=current.Steps.FirstOrDefault(s=>s.State=="running");var live=Path.Combine(PreparationStore.Folder(job.Id),"windows-progress.txt");if(active?.Kind is "windows" or "drivers"&&File.Exists(live)&&new FileInfo(live).LastWriteTimeUtc>=active.Started?.UtcDateTime)current.Message=File.ReadAllText(live).Trim();progress.Report(current);}}catch(IOException){}}
  var final=PreparationStore.Load(job.Id);if(final!=null)progress.Report(final);
  if(final==null||final.State is "planned" or "running")throw new IOException(FailureMessage(job.Id,process.ExitCode));
 }
 private static bool IsElevated()=>PreparationBackend.IsAdmin;
 public static ProcessStartInfo StartInfo(string executable,string id,bool elevated)
 {
  if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Hazırlama kimliği geçersiz.");
  var info=new ProcessStartInfo(executable){UseShellExecute=!elevated,WindowStyle=ProcessWindowStyle.Hidden,CreateNoWindow=true};if(!elevated)info.Verb="runas";info.ArgumentList.Add("--preparation-worker");info.ArgumentList.Add(id);return info;
 }
 private static string ErrorPath(string id)=>Path.Combine(PreparationStore.Folder(id),"worker-error.json");
 public static string FailureMessage(string id,int exitCode)
 {
  try{var path=ErrorPath(id);if(File.Exists(path)&&new FileInfo(path).Length<100_000){var error=JsonSerializer.Deserialize<PreparationWorkerError>(File.ReadAllText(path),LocalStore.Json);if(!string.IsNullOrWhiteSpace(error?.Message))return "Hazırlama başlatılamadı: "+error.Message+"\nRapor: "+path;}}catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException){}
  return "Hazırlama çalışanı sonuç kaydetmeden kapandı (kod 0x"+unchecked((uint)exitCode).ToString("X8")+").\nUygulamayı güncelle veya USB ve kurtarma bölümünden rapor klasörünü incele. Bu hata tek başına yönetici yetkisi sorunu anlamına gelmez.";
 }
 public static void RecordStartupFailure(string id,Exception error)
 {
  try{Journal.Write("Hazırlama başlatılamadı",error.ToString(),false);}catch(Exception){}
  try{
   var job=PreparationStore.Load(id);if(job==null||job.Machine!=Environment.MachineName||job.UserSid!=PreparationBackend.Sid)return;
   File.WriteAllText(ErrorPath(id),JsonSerializer.Serialize(new PreparationWorkerError(DateTimeOffset.Now,error.GetType().FullName??error.GetType().Name,error.Message,error.ToString()),LocalStore.Json));
   job.State="attention";job.Message="Hazırlama başlatılamadı: "+error.Message;PreparationStore.SaveState(job,false);
  }catch(Exception){}
 }
 public static void Stop(string id)=>File.WriteAllText(Path.Combine(PreparationStore.Folder(id),"stop"),"stop after current step");
 private static void SaveResume(PreparationJob job)
 {
  if(!job.Options.ResumeAtLogon){RemoveResume();return;}if(job.Executable.Contains('"'))throw new InvalidDataException("Uygulama yolu geçersiz.");
  using var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");run.SetValue("FromZeroPreparation","\""+job.Executable+"\" --resume-preparation "+job.Id);
 }
 public static void RemoveResume(){using var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);run?.DeleteValue("FromZeroPreparation",false);}
 private sealed class ImmediateProgress(Action<string> report):IProgress<string>{public void Report(string value)=>report(value);}
}
public record PreparationWorkerError(DateTimeOffset Checked,string Type,string Message,string Detail);
