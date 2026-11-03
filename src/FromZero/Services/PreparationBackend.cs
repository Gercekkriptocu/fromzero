using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FromZero;

public sealed class PreparationBackend:IPreparationBackend,IPreparationResumeVerifier
{
 private List<InstalledApp>? resumeApps;private ComponentInventory? resumeComponents;
 public Task<string?> MissingCompletedAsync(PreparationJob job,PreparationStep step,CancellationToken token)
 {
  token.ThrowIfCancellationRequested();resumeApps??=InstalledSoftware.ReadDesktopApps();
  if(step.Kind=="app"){var id=PreparationPackages.RecipeId(step.Id[4..]);return Task.FromResult<string?>(PreparationPackages.Present(id,resumeApps)?null:step.Title+": installation record no longer present.");}
  if(step.Kind=="package"){resumeComponents??=GameComponents.Scan(resumeApps);var id=step.Id[8..];return Task.FromResult<string?>(RuntimePresent(id,resumeComponents)?null:step.Title+": required components no longer detected.");}
  return Task.FromResult<string?>(null);
 }
 private readonly InventoryService inventory=new();
 public async Task<PreparationOutcome> ExecuteAsync(PreparationJob job,PreparationStep step,IProgress<string> progress,CancellationToken token)
 {
  if(step.Kind is not("offline" or "drivers" or "oem" or "windows" or "app" or "gpu-apps" or "preferences"))return await ExecuteCoreAsync(job,step,progress,token);
  progress.Report(step.Title+" / önceki durum kaydediliyor…");var before=await PreparationDiagnostics.CaptureAsync(token);await PreparationRecovery.WriteAsync(job,"before-"+step.Id+"-"+step.Attempts,before);
  PreparationOutcome result;try{result=await ExecuteCoreAsync(job,step,progress,token);}catch{try{var failedAfter=await PreparationDiagnostics.CaptureAsync(CancellationToken.None);await PreparationRecovery.ObserveAsync(job,step,before,failedAfter);}catch(Exception ex){Journal.Write("Hazırlama sonrası karşılaştırma",ex.Message,false);}throw;}
  try{progress.Report(step.Title+" / sonraki durum karşılaştırılıyor…");var after=await PreparationDiagnostics.CaptureAsync(token);var checkpoint=await PreparationRecovery.ObserveAsync(job,step,before,after);var regressions=checkpoint.Changes.Where(c=>c.Regression).ToList();if(regressions.Count>0)return result with{State="attention",Hold=true,Detail=result.Detail+"\nBu adımdan sonra "+regressions.Count+" olası bozulma görüldü. Yeniden kontrol et veya Kurtarma'yı incele. Zaman ilişkisi tek başına neden kanıtı değildir."};}
  catch(Exception ex)when(ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException){return result with{State="attention",Detail=result.Detail+"\nSon durum karşılaştırılamadı: "+ex.Message};}return result;
 }
 public static bool IsAdmin=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
 public static string Sid=>WindowsIdentity.GetCurrent().User?.Value??throw new InvalidOperationException("Kullanıcı kimliği okunamadı.");
 public static DateTimeOffset Boot=>DateTimeOffset.UtcNow-TimeSpan.FromMilliseconds(Environment.TickCount64);
 public static bool HasNetwork()=>NetworkInterface.GetAllNetworkInterfaces().Any(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType is not(NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)&&n.GetIPProperties().GatewayAddresses.Count>0);
 public static PreparationPreflightReport InspectPreflight(PreparationOptions options)=>PreparationPreflight.Evaluate(Path.GetPathRoot(Environment.SystemDirectory)!,new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!).AvailableFreeSpace,RuntimeInformation.OSArchitecture==Architecture.X64&&OperatingSystem.IsWindowsVersionAtLeast(10,0,19041),HasNetwork(),!string.IsNullOrWhiteSpace(options.OfflineFolder)&&Directory.Exists(options.OfflineFolder));
 private async Task<PreparationOutcome> ExecuteCoreAsync(PreparationJob job,PreparationStep step,IProgress<string> progress,CancellationToken token)
 {
  if(!IsAdmin)throw new UnauthorizedAccessException("Hazırlama kurulumu yönetici izni gerektirir.");
  if(step.Kind is "drivers" or "windows" or "oem"&&!HasNetwork())
  {
   var stop=Path.Combine(PreparationStore.Folder(job.Id),"stop");
   for(int check=0;check<6&&!HasNetwork()&&!File.Exists(stop);check++)
   {
    progress.Report("Waiting for network after offline setup…");await Task.Delay(2500,token);
   }
   if(File.Exists(stop))throw new OperationCanceledException();
   if(!HasNetwork())return new("attention","Offline steps are preserved. Connect to a network and choose Resume preparation.",Hold:true);
  }
  switch(step.Kind){
   case "preflight":
    var preflight=InspectPreflight(job.Options);return new(preflight.Ready?"done":"attention",preflight.Message);
   case "backup":
    var destination=Path.Combine(PreparationStore.Folder(job.Id),"DriverBackup");Directory.CreateDirectory(destination);PackageSafety.RejectReparsePoints(LocalStore.Root,destination);
    var backup=await Commands.PsAsync("& $env:SystemRoot\\System32\\pnputil.exe /export-driver '*' "+PackageSafety.QuotePs(destination)+";if($LASTEXITCODE -notin @(0,259,3010)){throw ('Sürücü yedeği kodu '+$LASTEXITCODE)}",900);
    if(!backup.Success)throw new IOException(backup.Output);return new("done",Directory.EnumerateFiles(destination,"*.inf",SearchOption.AllDirectories).Any()?"Üçüncü taraf sürücüleri yedeklendi: "+destination:"Yedeklenecek üçüncü taraf sürücüsü bulunmadı. Windows'un kutu içi sürücüleri bu dışa aktarıma dahil değildir.");
   case "offline":return await OfflineDriversAsync(job,progress,token);
   case "drivers":return await PreparationWindows.ApplyAsync(true,progress,token,Path.Combine(PreparationStore.Folder(job.Id),"stop"));
   case "windows":return await PreparationWindows.ApplyAsync(false,progress,token,Path.Combine(PreparationStore.Folder(job.Id),"stop"));
   case "package":return await PackageAsync(job,step.Id[8..],progress,token);
   case "app":return await PackageAsync(job,PreparationPackages.RecipeId(step.Id[4..]),progress,token);
   case "gpu-apps":return await GpuAsync(job,progress,token);
   case "oem":return await OemAsync(job,progress,token);
   case "verify":return await VerifyAsync(job,progress,token);
   case "baseline":var baseline=await PreparationDiagnostics.CaptureAsync(token);await PreparationRecovery.WriteAsync(job,"snapshot-before",baseline);return new("done","Aygıt, sürücü, uygulama, ses, ekran ve bağlantı durumu kaydedildi. "+string.Join(" / ",baseline.Warnings));
   case "preferences":return await PreparationRecovery.ApplyPreferencesAsync(job,token);
   case "games":var gameSnapshot=await PreparationDiagnostics.CaptureAsync(token);var gameChecks=await PreparationGameReadiness.InspectAsync(job.Options.Games,gameSnapshot,token);await PreparationRecovery.WriteAsync(job,"games-readiness",new ReadinessReport(DateTimeOffset.Now,gameChecks));return new(gameChecks.Any(c=>c.State=="attention")?"attention":"done",gameChecks.Count+" oyun kontrolü; "+gameChecks.Count(c=>c.State=="manual")+" kullanıcı doğrulaması gerekli. Bu adım oyunları indirmez veya oturum açmaz.");
   default:throw new InvalidDataException("Hazırlama adımı tanınmıyor.");
  }
 }
 public static bool RuntimePresent(string id,ComponentInventory components)=>id is "directx" or "directx-offline"?!components.Components.Any(c=>c.Recipe=="directx"&&!c.Present):components.Components.Any(c=>c.Recipe==id&&c.Present);
 private async Task<PreparationOutcome> PackageAsync(PreparationJob job,string id,IProgress<string> progress,CancellationToken token)
 {
  var definition=PreparationPackages.Definition(id);var installed=InstalledSoftware.ReadDesktopApps();
  var runtime=id is "directx" or "directx-offline"||id.StartsWith("vc",StringComparison.Ordinal);var component=runtime?GameComponents.Scan(installed):null;
  Version? target=null;string targetWarning="";
  if(!runtime&&id is "firefox" or "prepare-chrome" or "prepare-vscode")try{target=await PreparationPackages.TargetVersionAsync(id,token);}catch(Exception ex)when(ex is System.Net.Http.HttpRequestException or TaskCanceledException){targetWarning=" Güncel üretici sürümü okunamadı: "+ex.Message;}
  if(id.StartsWith("app-",StringComparison.Ordinal)&&HasNetwork())try{await PreparationPackages.ResolveAsync(id,token);target=PreparationPackages.KnownTarget(id);}catch(Exception ex)when(job.Options.OfflineFolder.Length>0&&ex is System.Net.Http.HttpRequestException or IOException or TaskCanceledException){token.ThrowIfCancellationRequested();targetWarning=" Çevrimiçi sürüm alınamadı; imzalı USB paketi deneniyor.";}
  if(target!=null&&PreparationPackages.InstalledVersion(id,installed) is {} current&&current>=target)return new("done","Kurulu sürüm üreticinin kararlı sürümüyle karşılaştırıldı: "+current+" ≥ "+target);
  if(id=="steam"&&PreparationPackages.Present(id,installed))return new("done","Steam kurulu; istemcinin kendi güncelleyicisi kullanılır.");
  // Existing apps are updated using the latest signed producer installer; no blind process termination.
  if(!runtime&&definition.ProcessName.Length>0&&Process.GetProcessesByName(definition.ProcessName).Length>0)return new("attention",definition.ProcessName+" açık. Çalışmanı kaydedip uygulamayı kapattıktan sonra devam et.");
  if(runtime&&id is not("vc-x64" or "vc-x86" or "directx-offline")&&RuntimePresent(id,component!))return new("done","Gerekli bileşenin kurulu olduğu doğrulandı.");
  var package=await PreparationPackages.AcquireAsync(id,job.Options.OfflineFolder,progress,token,HasNetwork());token.ThrowIfCancellationRequested();
  if(id=="dotnet-desktop-10-0-win-x64"){target=ApplicationCatalog.Number(AppDownloads.Recipes.Single(r=>r.Id==id).Name);var versions=RuntimeCatalog.InstalledVersions().GetValueOrDefault("x64/Microsoft.WindowsDesktop.App",[]);if(versions.Any(v=>ApplicationCatalog.Number(v)>=target))return new("done","Dell aracının .NET Desktop 10 ön koşulu doğrulandı.");}
  if(id.StartsWith("app-",StringComparison.Ordinal))target=PreparationPackages.KnownTarget(id);
  if(id=="prepare-vlc")target=ApplicationCatalog.Number(AppDownloads.Recipes.Single(r=>r.Id==id).Name);
  if(id is "vc-x64" or "vc-x86"){
   target=ApplicationCatalog.Number(FileVersionInfo.GetVersionInfo(package.Path).ProductVersion??"");
   if(target==null)targetWarning=" Visual C++ kurucusunun hedef sürümü okunamadı; sürüm güncelliği ayrıca kontrol edilmeli.";
   var arch=id.EndsWith("x64")?"x64":"x86";var existing=installed.Where(a=>a.Name.Contains("Visual C++",StringComparison.OrdinalIgnoreCase)&&a.Name.Contains(arch,StringComparison.OrdinalIgnoreCase)).Select(a=>ApplicationCatalog.Number(a.Version)).OfType<Version>().Where(v=>v.Major==14).OrderDescending().FirstOrDefault();
   if(target!=null&&existing>=target)return new("done","Visual C++ v14 sürümü doğrulandı: "+existing+" ≥ "+target);
  }
  if(!runtime&&target!=null&&PreparationPackages.InstalledVersion(id,installed) is {} latest&&latest>=target)return new("done","Kurulu sürüm hedef paketi karşılıyor: "+latest);
  var result=await PreparationPackages.InstallAsync(job.Id,id,package,progress);
  if(result.Code is not(0 or 3010))return new("attention","Kurucu kodu: "+result.Code+". Kurulum tamamlandı olarak işaretlenmedi.");
  if(result.Code==3010)return new("done","Kurucu yeniden başlatma istiyor; sonrasında doğrulanacak.",true);
  // Bootstrap installers can exit before their children finish writing installation records.
  for(var attempt=0;attempt<12;attempt++){
   installed=InstalledSoftware.ReadDesktopApps();var present=runtime?RuntimePresent(id,GameComponents.Scan(installed)):PreparationPackages.Present(id,installed);
   var versionMatches=runtime||target==null||PreparationPackages.InstalledVersion(id,installed)>=target;
   if(id is "vc-x64" or "vc-x86"&&target!=null){var arch=id.EndsWith("x64")?"x64":"x86";versionMatches=installed.Where(a=>a.Name.Contains("Visual C++",StringComparison.OrdinalIgnoreCase)&&a.Name.Contains(arch,StringComparison.OrdinalIgnoreCase)).Any(a=>ApplicationCatalog.Number(a.Version)>=target);}
   if(present&&versionMatches){
    return new(targetWarning.Length>0?"attention":"done",(runtime?"Bileşen kurulumu yeniden taranarak doğrulandı.":"Kurulum ve hedef sürüm kaydı doğrulandı: "+string.Join(" / ",installed.Where(a=>ApplicationCatalog.Matches(a,definition.Patterns)).Select(a=>a.Name+" "+a.Version)))+targetWarning);
   }await Task.Delay(1500);
  }
  return new("attention","Kurucu kapandı ancak beklenen kurulum kaydı / bileşen henüz doğrulanamadı. Üretici günlüğünü ve yeniden başlatma durumunu kontrol et.");
 }
 private async Task<PreparationOutcome> GpuAsync(PreparationJob job,IProgress<string> progress,CancellationToken token)
 {
  var devices=await inventory.DevicesAsync();var vendors=devices.Where(d=>d.Present).Select(GpuSoftware.Vendor).Where(v=>v.Length>0).Distinct().ToList();
  if(vendors.Count==0)return new("attention","PCI kimliğiyle desteklenen GPU tespit edilemedi. Genel görüntü aygıtında Windows sürücü adımı ve son kontrol kullanılır.");
  List<string> notes=[];var needsAttention=false;
  foreach(var vendor in vendors){var id=vendor switch{"NVIDIA"=>"nvidia","AMD"=>"amd",_=>"intel"};
   if(PreparationPackages.Present(id,InstalledSoftware.ReadDesktopApps())){notes.Add(vendor+" uygulaması zaten kurulu.");continue;}
   progress.Report(vendor+" üretici uygulaması eksik. Resmi kurucu açılacak; üreticinin seçimlerini tamamla.");
   var result=await PackageAsync(job,id,progress,token);notes.Add(vendor+": "+result.Detail);if(result.Reboot)return result;needsAttention|=result.State is "attention" or "failed";
  }return new(needsAttention?"attention":"done",string.Join("\n",notes));
 }
 private async Task<PreparationOutcome> OemAsync(PreparationJob job,IProgress<string> progress,CancellationToken token)
 {
  var passport=await inventory.PassportAsync();var maker=passport.GetProperty("Manufacturer").GetString()??"";
  if(maker.Equals("HP",StringComparison.OrdinalIgnoreCase)||maker.Contains("Hewlett",StringComparison.OrdinalIgnoreCase)){
   var model=passport.GetProperty("Model").GetString()??"";
   if(!new[]{"EliteBook","ProBook","ZBook","EliteDesk","ProDesk","EliteOne","ProOne","HP Z","HP Pro","HP Elite"}.Any(p=>model.Contains(p,StringComparison.OrdinalIgnoreCase)))return new("skipped","HP tüketici modeli / HPIA iş modeli eşleşmesi yok. Bu bilgisayar için Windows Update ve yerel sürücü kaynakları kullanıldı. Model özel paketler HP destek sayfasından incelenebilir.");
   var download=await PreparationPackages.AcquireAsync("hp-image-assistant",job.Options.OfflineFolder,progress,token);token.ThrowIfCancellationRequested();progress.Report("HP modeline uygun sürücüler inceleniyor…");var hpCli=await PreparationPackages.ExtractHpiaAsync(job.Id,download);var report=Path.Combine(PreparationStore.Folder(job.Id),"HPReport");Directory.CreateDirectory(report);var cache=Path.Combine(Path.GetDirectoryName(hpCli)!,"Downloads");Directory.CreateDirectory(cache);
   var hp=await Commands.InstallProcessAsync(hpCli,["/Operation:Analyze","/Category:Drivers","/Selection:All","/Action:Install","/Silent","/ReportFolder:"+report,"/SoftpaqDownloadFolder:"+cache]);
   return new(hp.ExitCode is 0 or 256 or 257 or 3010?"done":"attention","HP Image Assistant kodu: "+hp.ExitCode+". Model / OS destek dışıysa (4096 / 4104) Windows sürücü kaynağı kullanılır. Ayrıntı: "+report,hp.ExitCode==3010);
  }
  if(!maker.Contains("Dell",StringComparison.OrdinalIgnoreCase))return new("skipped","Bu üreticide otomatik sürücü kaynağı Windows Update ve seçilen USB paketidir. Model özel üretici araçları Güncellemeler > Üretici araçları bölümündedir.");
  var paths=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Dell","CommandUpdate","dcu-cli.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Dell","CommandUpdate","dcu-cli.exe")};
  var cli=paths.FirstOrDefault(File.Exists);if(cli==null){var dependency=await PackageAsync(job,"dotnet-desktop-10-0-win-x64",progress,token);if(dependency.Reboot||dependency.State!="done")return dependency;var install=await PackageAsync(job,"dell-command",progress,token);if(install.Reboot||install.State!="done")return install;cli=paths.FirstOrDefault(File.Exists);}
  if(cli==null)return new("attention","Dell Command Update CLI bulunamadı; bu model / ön koşullar üreticinin destek kapsamı dışında olabilir.");
  var publisher=await DriverService.SignatureAsync(cli);if(!publisher.Contains("Dell",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Dell aracı yayıncısı doğrulanamadı.");
  progress.Report("Dell modeline uygun sürücüler uygulanıyor…");
  var output=await Commands.InstallProcessAsync(cli,["/applyUpdates","-silent","-reboot=disable","-updateType=driver","-outputLog="+Path.Combine(PreparationStore.Folder(job.Id),"dell-update.log")]);
  return new(output.ExitCode is 0 or 1 or 500 or 3010?"done":"attention","Dell Command Update sonucu: "+output.ExitCode+". Ayrıntı: dell-update.log",output.ExitCode is 1 or 3010);
 }
 private async Task<PreparationOutcome> OfflineDriversAsync(PreparationJob job,IProgress<string> progress,CancellationToken token)
 {
  var root=job.Options.OfflineFolder;if(root.Length==0)return new("skipped","USB sürücü klasörü seçilmedi.");
  var deviceList=(await inventory.DevicesAsync()).Where(d=>d.Present).ToList();var infs=PackageSafety.EnumerateSafeFiles(root).Where(p=>Path.GetExtension(p).Equals(".inf",StringComparison.OrdinalIgnoreCase)).Take(5001).ToList();if(infs.Count>5000)throw new InvalidDataException("Sürücü klasöründe çok fazla INF var.");
  var errors=await OfflineDriverCatalog.VerifyCapsuleAsync(root);
  var index=OfflineDriverCatalog.Read(root);if(index!=null&&errors.Count==0)errors.AddRange(await OfflineDriverCatalog.VerifyAsync(root,index,deviceList));
  if(errors.Count>0)return new("attention","USB driver verification failed:\n"+string.Join("\n",errors.Take(20)),Hold:true);
  var service=new DriverService();List<string> notes=[];var reboot=false;
  foreach(var inf in infs.Where(p=>new FileInfo(p).Length<8_000_000).OrderByDescending(p=>Regex.IsMatch(PackageSafety.InfText(p),@"(?im)^\s*Class\s*=\s*Net\b"))){
   token.ThrowIfCancellationRequested();if(new FileInfo(inf).Length>8_000_000)continue;
   if(Regex.IsMatch(PackageSafety.InfText(inf),@"(?im)^\s*Class\s*=\s*Firmware\b"))continue;
   var ids=PackageSafety.ReadHardwareIds(inf);var matches=deviceList.Where(d=>d.Class!="Firmware"&&d.HardwareIds.Any(ids.Contains)).ToList();if(matches.Count==0)continue;
   await service.CheckInfAsync(inf,matches);var source=Path.GetDirectoryName(inf)!;var stage=Path.Combine(PreparationPackages.StageRoot(job.Id),"inf-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
   long bytes=0;foreach(var file in PackageSafety.EnumerateSafeFiles(source)){bytes+=new FileInfo(file).Length;if(bytes>4L*1024*1024*1024)throw new IOException("Yerel sürücü klasörü boyut sınırını aşıyor.");var target=PackageSafety.ResolveInside(stage,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target,false);}
   var stagedInf=Path.Combine(stage,Path.GetFileName(inf));await service.CheckInfAsync(stagedInf,matches);progress.Report("USB sürücüsü: "+string.Join(", ",matches.Select(d=>d.Name)));
   var install=await Commands.InstallProcessAsync(Path.Combine(Environment.SystemDirectory,"pnputil.exe"),["/add-driver",stagedInf,"/install"]);
   if(install.ExitCode is not(0 or 259 or 3010))notes.Add(Path.GetFileName(inf)+": hata "+install.ExitCode);else notes.Add(Path.GetFileName(inf)+": Windows paket sonucu "+install.ExitCode);reboot|=install.ExitCode==3010;
   if(reboot)break;
  }
  return new(notes.Any(n=>n.Contains(": hata"))?"attention":"done",notes.Count==0?"Mevcut donanımla tam kimlik eşleşmesi bulunan yerel sürücü yok.":string.Join("\n",notes)+"\nEtkin sürücü son kontrolde ayrıca incelenir.",reboot);
 }
 private async Task<PreparationOutcome> VerifyAsync(PreparationJob job,IProgress<string> progress,CancellationToken token)
 {
  progress.Report("Bağlantı, donanım, ses, oyunlar ve uygulamalar yeniden kontrol ediliyor…");var snapshot=await PreparationDiagnostics.CaptureAsync(token);var report=await PreparationDiagnostics.InspectAsync(job.Options,snapshot,token);await PreparationDiagnostics.SaveReportAsync(job,snapshot,report);
  var baseline=PreparationRecovery.Read<PreparationSnapshot>(job,"snapshot-before");if(baseline!=null)await PreparationRecovery.WriteAsync(job,"before-after",PreparationComparison.Compare(baseline,snapshot,"Son kontrol"));
  return new(report.Attention>0?"attention":"done",report.Passed+" kontrol doğrulandı; "+report.Attention+" sorun / eksik ve "+report.Manual+" kullanıcı testi. Ayrıntılar Hazırla / Durumu kontrol et ekranında.");
 }
}


