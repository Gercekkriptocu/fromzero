using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace FromZero;
public partial class MainWindow
{
 private PreparationOptions preparationOptions=PreparationExperience.Profile("basic");
 private PreparationJob? preparationJob=PreparationStore.Last();
 private TextBlock? preparationStatus;private StackPanel? preparationRows;
 private async Task StartPreparationAsync()
 {
  PreparationPolicy.ValidateOptions(preparationOptions);var readiness=PreparationBackend.InspectPreflight(preparationOptions);
  if(!readiness.Ready){RenderPreparationPreflight();throw new InvalidOperationException("Hazırlama başlamadı.\n\n"+readiness.Message);}
  if(preparationJob==null||preparationJob.State=="complete"||!Path.GetFullPath(preparationJob.Executable).Equals(Path.GetFullPath(Environment.ProcessPath!),StringComparison.OrdinalIgnoreCase))await PlanPreparationAsync();var job=preparationJob!;PreparationPolicy.Validate(job,PreparationBackend.Sid);
  if(job.State=="reboot"&&PreparationPolicy.SameBoot(job.BootTime,PreparationBackend.Boot))throw new InvalidOperationException("Devam etmeden önce Windows'u yeniden başlat.");
  if(job.Consented==null){var summary=await PreparationPlanDescriptionAsync(job);if(!Confirm(summary+"\n\nSeçili üreticilerin / Microsoft'un kurulum ve lisans koşullarını kabul ederek bu işlemleri başlatıyorsun. Yönetici izni istenir; ekran / ağ kısa süre kesilebilir. Çalışmanı kaydet. Kurucular gerektiğinde kendi ekranlarını açar. Başlatılsın mı?","Bilgisayarı hazırlama planı"))throw new OperationCanceledException("Hazırlama başlatılmadı; plan onayı verilmedi.");job.Consented=DateTimeOffset.Now;PreparationStore.Save(job);}
  preparationJob=job;if(preparationOptionsPanel!=null)preparationOptionsPanel.IsEnabled=false;CancelOperation.Visibility=Visibility.Visible;
  try{await PreparationWorker.LaunchAsync(job,new Progress<PreparationJob>(state=>{preparationJob=state;OperationTitle.Text=state.Message;RenderPreparationJob();}));}
  finally{CancelOperation.Visibility=Visibility.Collapsed;preparationJob=PreparationStore.Load(job.Id);Navigate("prepare");}
  var result=preparationJob;if(result!=null){operationCompletion=result.State switch{"attention"=>"KONTROL GEREKLİ / "+result.Message,"paused"=>"DURAKLATILDI / "+result.Message,"reboot"=>"YENİDEN BAŞLATMA GEREKLİ",_=>null};if(result.State=="attention"&&result.Steps.Any(s=>s.Kind=="preflight"&&s.State is "attention" or "failed"))throw new InvalidOperationException("Hazırlama durdu.\n\n"+result.Message);}
 }
 private async Task ExportPreparationAsync()
 {
  var parent=PickFolder("Format öncesi USB veya dış disk klasörünü seç");if(parent==null)return;var path=await new CapsuleService(inventory,driverService).CreateAsync(parent,new Progress<string>(m=>OperationTitle.Text=m));
  var ids=preparationOptions.Apps.Select(PreparationPackages.RecipeId).Concat(PreparationPolicy.Create(preparationOptions,PreparationBackend.Sid,Environment.ProcessPath!).Steps.Where(s=>s.Kind=="package").Select(s=>s.Id[8..])).Distinct().ToList();var packages=Path.Combine(path,"Packages");Directory.CreateDirectory(packages);List<OfflinePreparationPackage> records=[];List<string> warnings=[];
  foreach(var id in ids)try{var recipe=await PreparationPackages.ResolveAsync(id);var downloaded=await PreparationPackages.AcquireAsync(id,"",new Progress<string>(m=>OperationTitle.Text=m),default);await AppDownloads.VerifyCachedAsync(recipe,downloaded);var target=Path.Combine(packages,id+recipe.Extension);File.Copy(downloaded.Path,target,false);var hash=await PackageSafety.HashFileAsync(path,target);records.Add(new(id,hash.Path,hash.Sha256,hash.Size,id=="prepare-vlc"?recipe.Name[4..]:PreparationPackages.KnownTarget(id)?.ToString()??"",recipe));}catch(Exception ex){warnings.Add(id+": "+ex.Message);}
  await File.WriteAllTextAsync(Path.Combine(path,"preparation-packages.json"),JsonSerializer.Serialize(records,LocalStore.Json));
  await File.WriteAllTextAsync(Path.Combine(path,"preparation-profile.json"),JsonSerializer.Serialize(preparationOptions with{OfflineFolder=""},LocalStore.Json));
  await File.WriteAllTextAsync(Path.Combine(path,"preparation-export-report.json"),JsonSerializer.Serialize(new{Complete=warnings.Count==0,Requested=ids,Available=records.Select(r=>r.Id),Warnings=warnings},LocalStore.Json));
  await File.WriteAllTextAsync(Path.Combine(path,"BASLA.txt"),"FROMZERO / FORMAT SONRASI HAZIRLAMA\r\nApp\\FromZero.exe dosyasını aç. Bilgisayarı hazırla ekranında USB kök klasörü ve kaydettiğin seçimler yüklenir. Planı incele, sonra Bilgisayarımı hazırla ile başlat.\r\nBu paket Windows kurulum ortamı veya kişisel dosya yedeği değildir. Wi-Fi parolası içermez.\r\nİnternet yoksa yalnız mevcut donanımla eşleşen, imzalı yerel sürücüler ve pakette bulunan kurucular kullanılabilir. Windows güncellemeleri için internet gerekir.\r\nEksik indirilen paketler preparation-export-report.json dosyasında belirtilir. SHA-256 bozulma kontrolüdür, USB'nin kaynağının güvenilir olduğunu tek başına kanıtlamaz.\r\n");
  var manifestPath=Path.Combine(path,"capsule.json");var manifest=JsonSerializer.Deserialize<CapsuleManifest>(await File.ReadAllTextAsync(manifestPath),LocalStore.Json)!;List<CapsuleFile> files=[];foreach(var file in PackageSafety.EnumerateSafeFiles(path).Where(f=>f!=manifestPath))files.Add(await PackageSafety.HashFileAsync(path,file));await File.WriteAllTextAsync(manifestPath,JsonSerializer.Serialize(manifest with{Files=files},LocalStore.Json));
  Detail("USB hazırlama paketi",path+"\n\nSürücü yedeği ve "+records.Count+" doğrulanmış kurucu hazır. Format sonrasında App\\FromZero.exe → Bilgisayarımı hazırla → Sistem ve kurulum seçenekleri → USB sürücü klasörü seç. Paket kök klasörünü seç. Kişisel dosya yedeği değildir.\n"+string.Join("\n",warnings));
 }
}




