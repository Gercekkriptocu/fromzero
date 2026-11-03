using System.IO;
using System.Text.Json;
namespace FromZero;
public record PreparationCheckpoint(string Step,DateTimeOffset Checked,List<PreparationChange> Changes,List<string> Warnings);
public record PreparationPowerBackup(string Original,string Applied,DateTimeOffset Created);
public interface IPreparationPower{string Current();Task<bool> AvailableAsync(string id);Task SetAsync(string id);bool PluggedIn();}
public sealed class PreparationPower:IPreparationPower
{
 public string Current()=>GameService.ActivePlan();
 public async Task<bool> AvailableAsync(string id){var result=await Commands.RunAsync("powercfg.exe",["/list"]);return result.Success&&result.Output.Contains(id,StringComparison.OrdinalIgnoreCase);}
 public Task SetAsync(string id)=>GameService.SetPowerPlanAsync(id);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]private struct PowerState{public byte Ac,Battery,Percent,Reserved;public uint Seconds,Full;}
 [System.Runtime.InteropServices.DllImport("kernel32.dll")]private static extern bool GetSystemPowerStatus(out PowerState status);
 public bool PluggedIn()=>GetSystemPowerStatus(out var status)&&status.Ac==1;
}
public static class PreparationRecovery
{
 public static string SnapshotPath(PreparationJob job,string name)=>PackageSafety.ResolveInside(PreparationStore.Folder(job.Id),name+".json");
 public static T? Read<T>(PreparationJob job,string name){var path=SnapshotPath(job,name);if(!File.Exists(path))return default;if(new FileInfo(path).Length>16_000_000)throw new InvalidDataException("Hazırlama raporu sınırı aşıldı.");return JsonSerializer.Deserialize<T>(File.ReadAllText(path),LocalStore.Json);}
 public static async Task WriteAsync<T>(PreparationJob job,string name,T value){var path=SnapshotPath(job,name);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(value,LocalStore.Json));using(var file=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))file.Flush(true);File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
 public static async Task<PreparationCheckpoint> ObserveAsync(PreparationJob job,PreparationStep step,PreparationSnapshot before,PreparationSnapshot after)
 {
  var checkpoint=new PreparationCheckpoint(step.Title,DateTimeOffset.Now,PreparationComparison.Compare(before,after,step.Title),before.Warnings.Concat(after.Warnings).Distinct().ToList());
  await WriteAsync(job,"checkpoint-"+step.Id+"-"+step.Attempts,checkpoint);await WriteAsync(job,"latest-checkpoint",checkpoint);await WriteAsync(job,"snapshot-after",after);return checkpoint;
 }
 public static List<PreparationCheckpoint> Checkpoints(PreparationJob job)=>Directory.EnumerateFiles(PreparationStore.Folder(job.Id),"checkpoint-*.json").Where(f=>new FileInfo(f).Length<16_000_000).Select(f=>JsonSerializer.Deserialize<PreparationCheckpoint>(File.ReadAllText(f),LocalStore.Json)).OfType<PreparationCheckpoint>().OrderByDescending(c=>c.Checked).ToList();
 public static string PowerId(string preference)=>preference switch{"balanced"=>"381b4222-f694-41f0-9685-ff5bb260df2e","performance"=>PerformanceProfile.HighPower,"quiet"=>"a1841308-3541-4fab-bc81-f71556f20b4a",_=>throw new InvalidDataException("Güç tercihi bilinmiyor.")};
 public static async Task<string> ApplyPowerAsync(PreparationJob job,IPreparationPower? backend=null)
 {
  backend??=new PreparationPower();var target=PowerId(job.Options.PowerPreference);var existing=Read<PreparationPowerBackup>(job,"power-backup");if(existing!=null){if(backend.Current().Equals(existing.Applied,StringComparison.OrdinalIgnoreCase))return "Bu hazırlamanın güç ayarı zaten etkin.";throw new InvalidOperationException("Önceki güç yedeği mevcut. Kurtarma bölümünden geri al ya da yeni bir hazırlama planı oluştur.");}
  if(job.Options.PowerPreference=="performance"&&!backend.PluggedIn())throw new InvalidOperationException("Performans güç planı için adaptörü tak. Pilde otomatik yüksek performans uygulanmadı.");
  if(!await backend.AvailableAsync(target))throw new InvalidOperationException("Seçilen güç planı bu bilgisayarda mevcut değil. OEM / Modern Standby desteği farklı olabilir.");var current=backend.Current();if(!Guid.TryParse(current,out _))throw new InvalidDataException("Mevcut güç planı okunamadı.");if(current.Equals(target,StringComparison.OrdinalIgnoreCase))return "Seçilen güç planı zaten etkin.";
  await WriteAsync(job,"power-backup",new PreparationPowerBackup(current,target,DateTimeOffset.Now));await backend.SetAsync(target);if(!backend.Current().Equals(target,StringComparison.OrdinalIgnoreCase))throw new IOException("Güç planının uygulandığı doğrulanamadı. Geri alma yedeği korundu.");return "Güç planı uygulandı ve yeniden okunarak doğrulandı. Önceki plan yedeklendi.";
 }
 public static async Task<string> RestorePowerAsync(PreparationJob job,IPreparationPower? backend=null)
 {
  backend??=new PreparationPower();var backup=Read<PreparationPowerBackup>(job,"power-backup")??throw new InvalidOperationException("Güç yedeği yok.");if(!Guid.TryParse(backup.Original,out _)||!Guid.TryParse(backup.Applied,out _))throw new InvalidDataException("Güç yedeği geçersiz.");
  if(!backend.Current().Equals(backup.Applied,StringComparison.OrdinalIgnoreCase))return "Güç planı daha sonra değiştirilmiş. Mevcut tercih korundu; yedek duruyor.";
  await backend.SetAsync(backup.Original);if(!backend.Current().Equals(backup.Original,StringComparison.OrdinalIgnoreCase))throw new IOException("Önceki güç planı doğrulanamadı. Yedek duruyor.");File.Delete(SnapshotPath(job,"power-backup"));return "Önceki güç planı geri yüklendi ve doğrulandı.";
 }
 public static async Task<PreparationOutcome> ApplyPreferencesAsync(PreparationJob job,CancellationToken token)
 {
  List<string> details=[];bool attention=false;if(job.Options.PowerPreference!="none")try{details.Add(await ApplyPowerAsync(job));}catch(Exception ex)when(ex is IOException or InvalidOperationException){attention=true;details.Add(ex.Message);}
  if(job.Options.GraphicsPreference!="none"){
   var service=new GameGraphicsService();var targets=service.Discover().Where(t=>job.Options.Games.Contains(t.Game)).ToList();if(targets.Count==0){attention=true;details.Add("Seçilen oyunların desteklenen görüntü dosyası bulunamadı. Önce CS2 / VALORANT ayarlarını oyunda kaydet.");}
   foreach(var target in targets){token.ThrowIfCancellationRequested();try{if(service.Active(target)!=null){details.Add(target.Label+": etkin yedek bulundu; tekrar üzerine yazılmadı.");continue;}var plan=service.Preview(target,job.Options.GraphicsPreference=="fps"?"competitive":"balanced");if(plan.Changes.Count==0){details.Add(target.Label+": ayarlar zaten uygun.");continue;}await service.ApplyAsync(plan);details.Add(target.Label+": "+plan.Changes.Count+" görüntü ayarı uygulandı; dosya yedeği alındı.");}catch(Exception ex)when(ex is IOException or InvalidOperationException){attention=true;details.Add(target.Label+": "+ex.Message);}}
  }
  return new(attention?"attention":"done",string.Join("\n",details));
 }
}

