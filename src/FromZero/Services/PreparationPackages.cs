using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
namespace FromZero;

public sealed record PreparationPackage(string Id,string[] Arguments,string[] Patterns,bool Interactive=false,string ProcessName="");
public static class PreparationPackages
{
 private static readonly Dictionary<string,ApplicationRelease> SelectedReleases=new(StringComparer.Ordinal);
 private static bool Extended(string id)=>id.StartsWith("app-",StringComparison.Ordinal)&&PreparationExperience.Apps.Any(a=>"app-"+a.Id==id);
 public static string DisplayName(string id)=>PreparationExperience.Apps.FirstOrDefault(a=>RecipeId(a.Id)==id)?.Name??AppDownloads.Recipes.FirstOrDefault(r=>r.Id==id)?.Name??id;
 public static Version? KnownTarget(string id)=>SelectedReleases.TryGetValue(id,out var release)?ApplicationCatalog.Number(release.Version):null;
 public static PreparationPackage Definition(string id)=>id switch{
  "firefox"=>new(id,["/S"],["Mozilla Firefox"],ProcessName:"firefox"),
  "prepare-chrome"=>new(id,["/qn","/norestart"],["Google Chrome"],ProcessName:"chrome"),
  "prepare-vlc"=>new(id,["/S"],["VLC media player"],ProcessName:"vlc"),
  "steam"=>new(id,["/S"],["Steam"],ProcessName:"steam"),
  "prepare-vscode"=>new(id,["/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-"],["Microsoft Visual Studio Code"],ProcessName:"Code"),
  "directx"=>new(id,["/Q"],[]),
  "directx-offline"=>new(id,[],[]),
  "vc-x64" or "vc-x86"=>new(id,["/install","/quiet","/norestart"],[]),
  "vc2005-x64" or "vc2005-x86"=>new(id,[],[]),
  "vc2008-x64" or "vc2008-x86"=>new(id,["/q","/norestart"],[]),
  "vc2010-x64" or "vc2010-x86" or "vc2012-x64" or "vc2012-x86" or "vc2013-x64" or "vc2013-x86"=>new(id,["/install","/quiet","/norestart"],[]),
  "nvidia"=>new(id,[],["NVIDIA App"],true),
  "amd"=>new(id,[],["AMD Software","AMD Radeon Software"],true),
  "intel"=>new(id,[],["Intel Driver & Support Assistant","Intel® Driver & Support Assistant","Intel(R) Driver & Support Assistant"],true),
  "dell-command"=>new(id,["/s"],["Dell Command | Update","Dell Command Update"]),
  "hp-image-assistant"=>new(id,[],[]),
  "dotnet-desktop-10-0-win-x64"=>new(id,["/install","/quiet","/norestart"],["Microsoft Windows Desktop Runtime - 10."]),
  _=>Extended(id)?ExtendedDefinition(id):throw new InvalidDataException("Otomatik kurulum tarifi bulunmuyor.")
 };
 private static PreparationPackage ExtendedDefinition(string id){var app=PreparationExperience.Apps.Single(a=>"app-"+a.Id==id);return new(id,app.Arguments,app.Patterns,false,app.Process);}
 public static string RecipeId(string app)=>app switch{"chrome"=>"prepare-chrome","vlc"=>"prepare-vlc","vscode"=>"prepare-vscode","firefox" or "steam"=>app,_=>PreparationExperience.Apps.Any(a=>a.Id==app)?"app-"+app:throw new InvalidDataException("Desteklenmeyen uygulama.")};
 public static bool Present(string id,IEnumerable<InstalledApp> apps)
 {
  if(id=="dotnet-desktop-10-0-win-x64")return InstalledVersion(id,apps)!=null;var definition=Definition(id);return apps.Any(a=>Matches(id,a));
 }
 public static bool Matches(string id,InstalledApp app)=>ApplicationCatalog.Matches(app,Definition(id).Patterns)&&(id!="app-powershell"||!app.Name.Contains("Preview",StringComparison.OrdinalIgnoreCase));
 public static Version? InstalledVersion(string id,IEnumerable<InstalledApp> apps)
 {
  if(id=="dotnet-desktop-10-0-win-x64")return RuntimeCatalog.InstalledVersions().GetValueOrDefault("x64/Microsoft.WindowsDesktop.App",[]).Select(ApplicationCatalog.Number).OfType<Version>().Where(v=>v.Major==10).OrderDescending().FirstOrDefault();
  var versions=apps.Where(a=>Matches(id,a)).Select(a=>ApplicationCatalog.Number(a.Version)).OfType<Version>();
  if(id=="app-node-lts"&&KnownTarget(id) is {} target)versions=versions.Where(v=>v.Major==target.Major);return versions.OrderDescending().FirstOrDefault();
 }
 public static async Task<Version?> TargetVersionAsync(string id,CancellationToken token)
 {
  using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(25)};
  var url=id switch{"firefox"=>"https://product-details.mozilla.org/1.0/firefox_versions.json","prepare-chrome"=>"https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?pageSize=1","prepare-vscode"=>"https://update.code.visualstudio.com/api/update/win32-x64/stable/latest",_=>""};
  if(url.Length==0)return null;using var response=await http.GetAsync(url,token);response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));var value=id switch{"firefox"=>json.RootElement.GetProperty("LATEST_FIREFOX_VERSION").GetString(),"prepare-chrome"=>json.RootElement.GetProperty("versions")[0].GetProperty("version").GetString(),_=>json.RootElement.GetProperty("productVersion").GetString()};return ApplicationCatalog.Number(value??"")??throw new InvalidDataException("Üreticinin hedef sürümü okunamadı.");
 }
 public static async Task<DownloadRecipe> ResolveAsync(string id,CancellationToken token=default)
 {
  if(!PreparationPolicy.PackageIds.Contains(id))throw new InvalidDataException("Hazırlama paketi reddedildi.");
  if(Extended(id)){
   var result=await ApplicationCatalog.ScanAsync(InstalledSoftware.ReadDesktopApps(),token:token,selected:new HashSet<string>{id[4..]});var release=result.Applications.SingleOrDefault(a=>a.Id==id)??throw new IOException("Üretici sürüm kaydı bulunamadı: "+string.Join(" / ",result.Warnings));
   if(release.SourceChecked==null)throw new IOException("Güncel üretici kaydı doğrulanamadı. Bu paket otomatik kurulmadı: "+release.Name+" / "+string.Join(" / ",result.Warnings));
   if(release.Recipe.PublisherHashOnly||release.Recipe.CatalogSigned)throw new InvalidDataException("Otomatik kurulum için sabit üretici imzası gerekir.");SelectedReleases[id]=release;return release.Recipe;
  }
  if(id=="prepare-vlc"){
   using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};
   using var response=await client.GetAsync("https://update.videolan.org/vlc/status-win-x64",token);response.EnsureSuccessStatusCode();
   var body=await response.Content.ReadAsStringAsync(token);if(body.Length>100_000)throw new InvalidDataException("VLC sürüm kaydı çok büyük.");AppDownloads.RegisterPreparationVlc(body.Split('\n')[0].Trim());
  }
  if(id=="dotnet-desktop-10-0-win-x64"){
   using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(40)};
   using var response=await client.GetAsync("https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json",token);response.EnsureSuccessStatusCode();var body=await response.Content.ReadAsStringAsync(token);if(body.Length>24_000_000)throw new InvalidDataException(".NET sürüm kaydı çok büyük.");using var json=JsonDocument.Parse(body);RuntimeCatalog.ParseChannel(json.RootElement,"10.0",true,RuntimeCatalog.InstalledVersions());
  }
  return AppDownloads.Recipes.Single(r=>r.Id==id);
 }
 public static async Task<VerifiedDownload> AcquireAsync(string id,string offline,IProgress<string> progress,CancellationToken token,bool refresh=false)
 {
  DownloadRecipe recipe;
  if(offline.Length>0&&!refresh&&Extended(id)&&ReadOffline(offline).FirstOrDefault(p=>p.Id==id) is {} offlineEntry)recipe=RegisterOffline(offlineEntry);
  else try{recipe=await ResolveAsync(id,token);}catch(Exception ex)when(offline.Length>0&&ex is HttpRequestException or TaskCanceledException or IOException){
   token.ThrowIfCancellationRequested();if(Extended(id)){if(!refresh||!ReadOffline(offline).Any(p=>p.Id==id))throw;return await AcquireAsync(id,offline,progress,token,false);}
   if(id!="prepare-vlc"||offline.Length==0)throw;var manifest=ReadOffline(offline);var entry=manifest.FirstOrDefault(p=>p.Id==id)??throw new IOException("VLC çevrimdışı paketi yok.");AppDownloads.RegisterPreparationVlc(entry.Version);recipe=AppDownloads.Recipes.Single(r=>r.Id==id);
  }
  if((!refresh||recipe.ExpectedSha256.Length>0||recipe.ExpectedSha512.Length>0)&&AppDownloads.Cached(recipe) is {} cache){await AppDownloads.VerifyCachedAsync(recipe,cache);return cache;}
  if(offline.Length>0&&!refresh){var entry=ReadOffline(offline).FirstOrDefault(p=>p.Id==id);if(entry!=null){
   var file=PackageSafety.ResolveInside(offline,entry.File);var hash=await PackageSafety.HashFileAsync(offline,file);if(!hash.Sha256.Equals(entry.Sha256,StringComparison.OrdinalIgnoreCase)||hash.Size!=entry.Bytes)throw new InvalidDataException("USB kurucu bütünlüğü doğrulanamadı.");
   var publisher=await DriverService.SignatureAsync(file);if(!publisher.Contains(recipe.Publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("USB kurucu yayıncısı eşleşmedi.");
   var folder=LocalStore.FilePath("Downloads");Directory.CreateDirectory(folder);var target=Path.Combine(folder,id+"-"+Guid.NewGuid().ToString("N")+recipe.Extension);File.Copy(file,target);
   var record=new VerifiedDownload(recipe.Name,target,publisher,hash.Sha256,hash.Size,recipe.Url,DateTimeOffset.Now);await AppDownloads.VerifyCachedAsync(recipe,record);await File.WriteAllTextAsync(LocalStore.FilePath("download-"+id+".json"),JsonSerializer.Serialize(record,LocalStore.Json),token);return record;
  }}
  try{await AppDownloads.DownloadAsync(recipe,progress,token);return AppDownloads.Cached(recipe)??throw new IOException("Doğrulanmış kurucu kaydı bulunamadı.");}
  catch(Exception ex)when(refresh&&offline.Length>0&&ex is HttpRequestException or TaskCanceledException){return await AcquireAsync(id,offline,progress,token,false);}
 }
 internal static DownloadRecipe RegisterOffline(OfflinePreparationPackage entry)
 {
  var recipe=entry.Recipe??throw new InvalidDataException("USB paketinin üretici kaydı eksik. Güncel FromZero ile USB kitini yeniden oluştur.");if(recipe.Url==null||recipe.Publisher==null||recipe.Name==null||recipe.ResolvePattern==null||recipe.Extension==null||entry.Version==null)throw new InvalidDataException("USB üretici alanları eksik.");var id=entry.Id[4..];var source=ApplicationSources.Github.FirstOrDefault(s=>s.Id==id);string publisher;string[] hosts;string extension;
  if(source!=null){publisher=source.Publisher;hosts=["github.com","githubusercontent.com"];extension=source.AssetPattern.Contains("msi",StringComparison.Ordinal)?".msi":".exe";var uri=new Uri(recipe.Url);if(uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/"+source.Repository+"/releases/download/",StringComparison.Ordinal)||!System.Text.RegularExpressions.Regex.IsMatch(Uri.UnescapeDataString(uri.Segments.Last()),source.AssetPattern,System.Text.RegularExpressions.RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)))throw new InvalidDataException("USB paketi resmi üretici sürümüne ait değil.");}
  else if(id=="node-lts"){publisher="OpenJS Foundation";hosts=["nodejs.org"];extension=".msi";if(!System.Text.RegularExpressions.Regex.IsMatch(new Uri(recipe.Url).AbsolutePath,@"^/dist/v\d+\.\d+\.\d+/node-v\d+\.\d+\.\d+-x64\.msi$"))throw new InvalidDataException("Node.js USB kaydı geçersiz.");}
  else if(id=="thunderbird"){publisher="Mozilla Corporation";hosts=["mozilla.org","mozilla.net","thunderbird.net"];extension=".exe";if(!recipe.Url.StartsWith("https://download.mozilla.org/?product=thunderbird-latest-ssl&os=win64&lang=",StringComparison.Ordinal))throw new InvalidDataException("Thunderbird USB kaydı geçersiz.");}
  else if(id=="edge"){publisher="Microsoft Corporation";hosts=["microsoft.com"];extension=".msi";if(!new Uri(recipe.Url).AbsolutePath.EndsWith(".msi",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Edge USB kaydı geçersiz.");}
  else throw new InvalidDataException("Çevrimdışı üretici tarifi bilinmiyor.");
  if(!Extended(entry.Id)||recipe.Id!=entry.Id||recipe.Publisher!=publisher||recipe.Extension!=extension||recipe.PublisherHashOnly||recipe.CatalogSigned||recipe.DriverTool||recipe.ResolvePattern.Length>0||!AppDownloads.Allowed(new Uri(recipe.Url),hosts)||ApplicationCatalog.Number(entry.Version)==null)throw new InvalidDataException("USB üretici kaydı doğrulanamadı.");
  // The USB cannot supply hosts, commands, publisher rules or a target version for online updates.
  var pinned=recipe with{Hosts=hosts,ExpectedSha256=entry.Sha256,ExpectedSha512=""};AppDownloads.RegisterApp(pinned);SelectedReleases[entry.Id]=new(entry.Id,recipe.Name,entry.Version,[],"Çevrimdışı paket",pinned,"USB / üretici imzası","",null);return pinned;
 }
 public static string StageRoot(string jobId)
 {
  if(!Guid.TryParseExact(jobId,"N",out _))throw new InvalidDataException("Kurulum kimliği geçersiz.");
  var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"FromZero","Preparation",jobId);
  Directory.CreateDirectory(root);PackageSafety.RejectReparsePoints(Path.GetPathRoot(root)!,root);
  var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
  foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})security.AddAccessRule(new(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
  security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));new DirectoryInfo(root).SetAccessControl(security);return root;
 }
 public static async Task<string> ExtractHpiaAsync(string jobId,VerifiedDownload download)
 {
  var recipe=AppDownloads.Recipes.Single(r=>r.Id=="hp-image-assistant");await AppDownloads.VerifyCachedAsync(recipe,download);var root=StageRoot(jobId);var file=Path.Combine(root,"hpia-extract-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(download.Path,file,false);
  using var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(locked));if(hash!=download.Hash)throw new InvalidDataException("HP paketi aktarımda değişti.");
  var folder=Path.Combine(root,"HPIA-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var extract=await Commands.InstallProcessAsync(file,["/s","/e","/f",folder]);var executable=Path.Combine(folder,"HPImageAssistant.exe");
  if(extract.ExitCode is not(0 or 1168)||!File.Exists(executable))throw new IOException("HP aracı çıkartılamadı: "+extract.ExitCode);
  var publisher=await DriverService.SignatureAsync(executable);if(!publisher.Contains("HP Inc.",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("HPImageAssistant yayıncısı doğrulanamadı.");return executable;
 }
 public static Task<(int Code,string Path)> InstallAsync(string jobId,string id,VerifiedDownload download,IProgress<string> progress)
  =>InstallAsync(jobId,AppDownloads.Recipes.Single(r=>r.Id==id),Definition(id),download,progress);

 internal static async Task<(int Code,string Path)> InstallAsync(string jobId,DownloadRecipe recipe,PreparationPackage definition,VerifiedDownload download,IProgress<string> progress)
 {
  var id=recipe.Id;
  await AppDownloads.VerifyCachedAsync(recipe,download);
  var stage=StageRoot(jobId);var file=Path.Combine(stage,id+"-"+Guid.NewGuid().ToString("N")+recipe.Extension);File.Copy(download.Path,file,false);
  using var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
  var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(locked));if(hash!=download.Hash)throw new InvalidDataException("Kurulum dosyası aktarılırken değişmiş.");
  var publisher=await DriverService.SignatureAsync(file);if(!publisher.Contains(recipe.Publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Kurulum yayıncısı doğrulanamadı.");
  if(id is "directx-offline" or "vc2005-x64" or "vc2005-x86"){
   var directory=Path.Combine(stage,"Extract-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);progress.Report(recipe.Name+" tam paketi çıkartılıyor…");
   var unpack=await Commands.InstallProcessAsync(file,["/Q","/C","/T:"+directory]);if(unpack.ExitCode!=0)throw new IOException("Microsoft paketi çıkartılamadı: "+unpack.ExitCode);
   if(id!="directx-offline"){
    var msi=Path.Combine(directory,"vcredist.msi");if(!File.Exists(msi))throw new InvalidDataException("İmzalı Microsoft arşivindeki vcredist.msi bulunamadı.");
    progress.Report(recipe.Name+" kuruluyor…");var installed=await Commands.InstallProcessAsync(Path.Combine(Environment.SystemDirectory,"msiexec.exe"),["/i",msi,"/qn","/norestart"]);return(installed.ExitCode,msi);
   }
   var setup=Path.Combine(directory,"DXSETUP.exe");if(!File.Exists(setup)||!(await DriverService.SignatureAsync(setup)).Contains("Microsoft Corporation",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("DirectX kurulum dosyası doğrulanamadı.");
   progress.Report("DirectX oyun kütüphaneleri kuruluyor…");var result=await Commands.InstallProcessAsync(setup,["/silent"]);return(result.ExitCode,setup);
  }
  progress.Report(definition.Interactive?recipe.Name+" kurucusunda üreticinin istediği seçimleri tamamla…":recipe.Name+" kuruluyor…");
  var info=new ProcessStartInfo(recipe.Extension==".msi"?Path.Combine(Environment.SystemDirectory,"msiexec.exe"):file){UseShellExecute=false,CreateNoWindow=!definition.Interactive,WorkingDirectory=stage};
  if(recipe.Extension==".msi"){info.ArgumentList.Add("/i");info.ArgumentList.Add(file);}
  foreach(var argument in definition.Arguments)info.ArgumentList.Add(argument);
  using var process=Process.Start(info)??throw new IOException("Kurucu başlatılamadı.");
  // Never kill a running driver/MSI installer. Cancellation is handled between steps.
  await process.WaitForExitAsync();return(process.ExitCode,file);
 }
 public static IReadOnlyList<OfflinePreparationPackage> ReadOffline(string root)
 {
  var path=PackageSafety.ResolveInside(root,"preparation-packages.json");if(!File.Exists(path))return [];
  if(new FileInfo(path).Length>1_000_000)throw new InvalidDataException("USB kurulum listesi geçersiz.");var list=JsonSerializer.Deserialize<List<OfflinePreparationPackage>>(File.ReadAllText(path),LocalStore.Json)??[];
  if(list.Count>PreparationPolicy.PackageIds.Length||list.Any(p=>p==null||p.Sha256==null||p.File==null||p.Version==null)||list.Select(p=>p.Id).Distinct().Count()!=list.Count||list.Any(p=>!PreparationPolicy.PackageIds.Contains(p.Id)||!System.Text.RegularExpressions.Regex.IsMatch(p.Sha256,"^[A-Fa-f0-9]{64}$")||p.Bytes<1024||p.Bytes>1024L*1024*1024))throw new InvalidDataException("USB paket listesi doğrulanamadı.");
  foreach(var item in list)PackageSafety.ResolveInside(root,item.File);return list;
 }
}
public record OfflinePreparationPackage(string Id,string File,string Sha256,long Bytes,string Version,DownloadRecipe? Recipe=null);



