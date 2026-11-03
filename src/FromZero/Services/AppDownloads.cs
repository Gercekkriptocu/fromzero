using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record DownloadRecipe(string Name,string Url,string Publisher,string[] Hosts,string ResolvePattern="",string Id="",bool DriverTool=false,string ExpectedSha512="",string ExpectedSha256="",string Extension=".exe",bool PublisherHashOnly=false,bool CatalogSigned=false);
public record VerifiedDownload(string Name,string Path,string Publisher,string Hash,long Bytes,string Source,DateTimeOffset Checked);
public static class AppDownloads
{
 private static readonly object CatalogGate=new();
 private static readonly List<DownloadRecipe> catalog = [
  new("Firefox","https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=tr","Mozilla Corporation",["mozilla.org","mozilla.net"],Id:"firefox"),
  new("Visual Studio Code","https://update.code.visualstudio.com/latest/win32-x64-user/stable","Microsoft Corporation",["visualstudio.com","microsoft.com","vscode-cdn.net"],Id:"vscode"),
  new("Steam","https://cdn.fastly.steamstatic.com/client/installer/SteamSetup.exe","Valve Corp.",["steamstatic.com","steampowered.com"],Id:"steam"),
  new("NVIDIA App","https://www.nvidia.com/en-us/software/nvidia-app/","NVIDIA Corporation",["nvidia.com"],@"https://us\.download\.nvidia\.com/nvapp/client/[\d.]+/NVIDIA_app_v[\d.]+\.exe","nvidia",true),
  new("AMD Auto-Detect / Adrenalin kurulumu","https://www.amd.com/en/support/download/drivers.html","Advanced Micro Devices",["amd.com"],@"https://drivers\.amd\.com/drivers/installer/[^\s""'<>]+minimalsetup[^\s""'<>]+\.exe","amd",true),
  new("Intel Driver & Support Assistant","https://dsadata.intel.com/installer","Intel",["intel.com"],Id:"intel",DriverTool:true),
  new("DirectX ek oyun kütüphaneleri","https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe","Microsoft Corporation",["microsoft.com"],Id:"directx"),
  new("Visual C++ v14 x64","https://aka.ms/vc14/vc_redist.x64.exe","Microsoft Corporation",["aka.ms","microsoft.com","visualstudio.com"],Id:"vc-x64"),
  new("Visual C++ v14 x86","https://aka.ms/vc14/vc_redist.x86.exe","Microsoft Corporation",["aka.ms","microsoft.com","visualstudio.com"],Id:"vc-x86"),
  new("Visual C++ 2013 x64","https://download.microsoft.com/download/2/e/6/2e61cfa4-993b-4dd4-91da-3737cd5cd6e3/vcredist_x64.exe","Microsoft Corporation",["microsoft.com"],Id:"vc2013-x64"),
  new("Visual C++ 2013 x86","https://download.microsoft.com/download/2/e/6/2e61cfa4-993b-4dd4-91da-3737cd5cd6e3/vcredist_x86.exe","Microsoft Corporation",["microsoft.com"],Id:"vc2013-x86")
 ];
 static AppDownloads(){
  catalog.Add(new("Chrome x64 (tüm kullanıcılar)","https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi","Google",["google.com"],Id:"prepare-chrome",Extension:".msi"));
  catalog.Add(new("Visual Studio Code (tüm kullanıcılar)","https://update.code.visualstudio.com/latest/win32-x64/stable","Microsoft Corporation",["visualstudio.com","microsoft.com","vscode-cdn.net"],Id:"prepare-vscode"));
  catalog.Add(new("Logi Options+","https://www.logitech.com/en-us/software/logi-options-plus.html","Logitech",["logitech.com","download01.logi.com"],@"https://download01\.logi\.com/web/ftp/pub/techsupport/optionsplus/logioptionsplus_installer\.exe",Id:"logi-options",DriverTool:true));
  catalog.Add(new("Logitech G HUB","https://www.logitechg.com/en-us/innovation/g-hub.html","Logitech",["logitechg.com","download01.logi.com"],@"https://download01\.logi\.com/web/ftp/pub/techsupport/gaming/lghub_installer\.exe",Id:"logi-ghub",DriverTool:true));
  catalog.Add(new("Corsair iCUE","https://www.corsair.com/us/en/s/downloads","CORSAIR",["corsair.com"],@"https://www3\.corsair\.com/software/CUE_V5/public/modules/windows/installer/Install%20iCUE\.exe",Id:"corsair-icue",DriverTool:true));
  catalog.Add(new("SteelSeries GG","https://steelseries.com/gg/downloads/gg/latest/windows","GN Hearing A/S",["steelseries.com","steelseriescdn.com"],Id:"steelseries-gg",DriverTool:true));
  catalog.Add(new("Samsung Magician","https://semiconductor.samsung.com/consumer-storage/support/tools/","Samsung",["samsung.com"],@"https://download\.semiconductor\.samsung\.com/resources/software-resources/Samsung_Magician_Installer_Official_[\d.]+\.exe",Id:"samsung-magician",DriverTool:true));
  catalog.Add(new("Dell Command Update 5.7.1","https://dl.dell.com/FOLDER14847280M/2/Dell-Command-Update-for-Win32_61R17_WIN64_5.7.1_A00.EXE","Dell Technologies Inc.",["dl.dell.com"],Id:"dell-command",DriverTool:true,ExpectedSha256:"1f7ae4e4a20e7005a894cf9429765e99cf197cc021303f7e105abac463114b0c"));
  catalog.Add(new("HP Image Assistant 5.3.7","https://hpia.hpcloud.hp.com/downloads/hpia/hp-hpia-5.3.7.exe","HP Inc.",["hpia.hpcloud.hp.com"],Id:"hp-image-assistant",DriverTool:true));
  catalog.Add(new("Lenovo System Update 5.08.04.85","https://download.lenovo.com/pccbbs/thinkvantage_en/system_update_5.08.04.85.exe","Lenovo",["download.lenovo.com"],Id:"lenovo-system-update",DriverTool:true,ExpectedSha256:"4f044c4a1cece3fe4be22408da7e1d8e8338eeaae4491ce18fdaa5b655dc9585"));
  catalog.Add(new("DirectX çevrimdışı ek oyun kütüphaneleri","https://download.microsoft.com/download/8/4/a/84a35bf1-dafe-4ae8-82af-ad2ae20b6b14/directx_Jun2010_redist.exe","Microsoft Corporation",["download.microsoft.com"],Id:"directx-offline"));
  catalog.Add(new("Microsoft XNA Framework 4.0","https://download.microsoft.com/download/a/c/2/ac2c903b-e6e8-42c2-9fd7-bebac362a930/xnafx40_redist.msi","Microsoft Corporation",["download.microsoft.com"],Id:"xna",Extension:".msi"));
  foreach(var (year,path) in new[]{("2005","8/b/4/8b42259f-5d70-43f4-ac2e-4b208fd8d66a"),("2008","5/d/8/5d8c65cb-c849-4025-8e95-c3966cafd8ae"),("2010","1/6/5/165255e7-1014-4d0a-b094-b6a430a6bffc"),("2012","1/6/b/16b06f60-3b20-4ff2-b699-5e9b7962f9ae/VSU_4")})
   foreach(var arch in new[]{"x64","x86"})catalog.Add(new("Visual C++ "+year+" "+arch,"https://download.microsoft.com/download/"+path+"/vcredist_"+arch+".exe","Microsoft Corporation",["download.microsoft.com"],Id:"vc"+year+"-"+arch));
 }
 internal static void RegisterPreparationVlc(string version)
 {
  if(!Regex.IsMatch(version,@"^\d+\.\d+\.\d+(?:\.\d+)?$"))throw new InvalidDataException("VLC sürüm kaydı geçersiz.");
  var recipe=new DownloadRecipe("VLC "+version,"https://mirror.verinomi.com/videolan/vlc/"+version+"/win64/vlc-"+version+"-win64.exe","VideoLAN",["videolan.org","mirror.verinomi.com"],Id:"prepare-vlc");
  lock(CatalogGate){catalog.RemoveAll(r=>r.Id==recipe.Id);catalog.Add(recipe);}
 }
 public static IReadOnlyList<DownloadRecipe> Recipes{get{lock(CatalogGate)return catalog.ToArray();}}
 internal static void RegisterApp(DownloadRecipe recipe)
 {
  if(!recipe.Id.StartsWith("app-",StringComparison.Ordinal)||!ApplicationSources.Known(recipe.Id[4..])||recipe.Extension is not (".exe" or ".msi")||recipe.ExpectedSha256.Length>0&&!Regex.IsMatch(recipe.ExpectedSha256,"^[a-fA-F0-9]{64}$")||!Allowed(new Uri(recipe.Url),recipe.Hosts)||recipe.PublisherHashOnly&&!HashOnlyAllowed(recipe))throw new InvalidDataException("Uygulama indirme kaynağı doğrulanamadı.");
  if(recipe.CatalogSigned&&!CommunityPackageCatalog.Allowed(recipe))throw new InvalidDataException("Topluluk paketinin kaynak/hash/imza koşulları doğrulanamadı.");
  lock(CatalogGate){catalog.RemoveAll(r=>r.Id==recipe.Id);catalog.Add(recipe);}
 }
 internal static bool HashOnlyAllowed(DownloadRecipe recipe){var prefix=recipe.Id switch{"app-7zip"=>"/ip7z/7zip/releases/download/","app-sharex"=>"/ShareX/ShareX/releases/download/","app-mpchc"=>"/clsid2/mpc-hc/releases/download/",_=>""};return prefix.Length>0&&recipe.PublisherHashOnly&&Regex.IsMatch(recipe.ExpectedSha256,"^[a-fA-F0-9]{64}$")&&new Uri(recipe.Url).Host=="github.com"&&new Uri(recipe.Url).AbsolutePath.StartsWith(prefix,StringComparison.Ordinal);}
 internal static void RegisterRuntime(DownloadRecipe recipe)
 {
  if(!Regex.IsMatch(recipe.Id,@"^dotnet-(?:[a-z]+-\d+-\d+|sdk-\d+-\d+-[1-9]00)-win-(x64|x86|arm64)$")||recipe.Publisher!="Microsoft Corporation"||!Regex.IsMatch(recipe.ExpectedSha512,"^[a-fA-F0-9]{128}$")||!Allowed(new Uri(recipe.Url),["builds.dotnet.microsoft.com","download.visualstudio.microsoft.com","dotnetcli.azureedge.net"]))throw new InvalidDataException("Çalışma ortamı kaynağı doğrulanamadı.");
  lock(CatalogGate){catalog.RemoveAll(r=>r.Id==recipe.Id);catalog.Add(recipe);}
 }
 public static bool Allowed(Uri uri,IEnumerable<string> hosts)=>uri.Scheme=="https"&&string.IsNullOrEmpty(uri.UserInfo)&&uri.IsDefaultPort&&hosts.Any(h=>uri.Host.Equals(h,StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith("."+h,StringComparison.OrdinalIgnoreCase));
 public static Uri ExtractDownload(string html,DownloadRecipe recipe)
 {
  if(recipe.ResolvePattern.Length==0)return new Uri(recipe.Url);
  var matches=Regex.Matches(WebUtility.HtmlDecode(html),recipe.ResolvePattern,RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2));
  var url=matches.Select(m=>m.Value).Distinct().FirstOrDefault()??throw new InvalidDataException("Üreticinin güncel kurulum bağlantısı sayfada bulunamadı. Resmi sayfayı kullan veya daha sonra yeniden dene.");
  var uri=new Uri(url);if(!Allowed(uri,recipe.Hosts))throw new InvalidDataException("Üretici dışı indirme adresi reddedildi.");return uri;
 }
 private static HttpClient Client()
 {
  var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromMinutes(15)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FromZero/0.5");
  return client;
 }
 private static async Task<HttpResponseMessage> GetAsync(HttpClient http,Uri uri,DownloadRecipe recipe,CancellationToken cancellation)
 {
  for(var redirects=0;redirects<10;redirects++){
   if(!Allowed(uri,recipe.Hosts))throw new InvalidDataException("İndirme resmi HTTPS alan adı dışına yönlendirildi.");
   using var request=new HttpRequestMessage(HttpMethod.Get,uri);request.Headers.Referrer=new Uri(recipe.Url);
   var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation);
   if((int)response.StatusCode is >=300 and <400){
    var target=response.Headers.Location;response.Dispose();if(target==null)throw new IOException("Yönlendirme adresi eksik.");uri=target.IsAbsoluteUri?target:new Uri(uri,target);continue;
   }
   try{response.EnsureSuccessStatusCode();return response;}catch{response.Dispose();throw;}
  }
  throw new IOException("Çok fazla indirme yönlendirmesi.");
 }
 public static async Task<Uri> ResolveAsync(DownloadRecipe recipe,CancellationToken cancellation=default)
 {
  if(!Recipes.Contains(recipe))throw new InvalidDataException("Desteklenmeyen indirme tarifi.");
  if(recipe.ResolvePattern.Length==0)return new Uri(recipe.Url);
  using var http=Client();using var response=await GetAsync(http,new Uri(recipe.Url),recipe,cancellation);
  if(response.Content.Headers.ContentLength>8_000_000)throw new InvalidDataException("Üretici sayfası çok büyük.");
  var html=await response.Content.ReadAsStringAsync(cancellation);if(html.Length>8_000_000)throw new InvalidDataException("Üretici sayfası çok büyük.");
  return ExtractDownload(html,recipe);
 }
 public static VerifiedDownload? Cached(DownloadRecipe recipe)
 {
  var path=LocalStore.FilePath("download-"+recipe.Id+".json");if(!File.Exists(path))return null;
  try{var item=JsonSerializer.Deserialize<VerifiedDownload>(File.ReadAllText(path),LocalStore.Json);return item!=null&&File.Exists(item.Path)&&new FileInfo(item.Path).Length==item.Bytes?item:null;}catch(Exception ex)when(ex is JsonException or IOException){return null;}
 }
 public static async Task<(string Path,string Publisher,string Hash)> DownloadAsync(DownloadRecipe recipe,IProgress<string> progress,CancellationToken cancellation=default)
 {
  if(!Recipes.Contains(recipe))throw new InvalidDataException("Desteklenmeyen indirme tarifi.");
  if(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture!=System.Runtime.InteropServices.Architecture.X64)throw new PlatformNotSupportedException("Bu indirme tarifleri Windows x64 içindir. Mimarin için üreticinin resmi sayfasını kullan.");
  progress.Report(recipe.Name+" için resmi indirme bağlantısı bulunuyor…");
  var source=await ResolveAsync(recipe,cancellation);
  var folder=LocalStore.FilePath("Downloads");Directory.CreateDirectory(folder);PackageSafety.RejectReparsePoints(folder,folder);
  var path=System.IO.Path.Combine(folder,recipe.Id+"-"+Guid.NewGuid().ToString("N")+recipe.Extension);var partial=path+".partial"+recipe.Extension;
  using var http=Client();
  try{
   using var response=await GetAsync(http,source,recipe,cancellation);
   const long limit=1024L*1024*1024;if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Kurulum paketi boyut sınırını aşıyor.");
   var contentType=response.Content.Headers.ContentType?.MediaType??"";
   if(contentType.Contains("html",StringComparison.OrdinalIgnoreCase)||contentType.StartsWith("text/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Sunucu kurulum paketi yerine metin/sayfa döndürdü.");
   await using(var input=await response.Content.ReadAsStreamAsync(cancellation))await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)){
    var buffer=new byte[81920];long total=0;int read;var last=DateTime.UtcNow;
    while((read=await input.ReadAsync(buffer,cancellation))>0){
     if(total==0&&(recipe.Extension==".msi"?(read<8||!buffer.AsSpan(0,8).SequenceEqual(new byte[]{0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1})):(read<2||buffer[0]!='M'||buffer[1]!='Z')))throw new InvalidDataException("İndirilen dosya beklenen Windows kurulum biçiminde değil.");
     total+=read;if(total>limit)throw new InvalidDataException("Kurulum paketi çok büyük.");await output.WriteAsync(buffer.AsMemory(0,read),cancellation);
     if((DateTime.UtcNow-last).TotalMilliseconds>350){progress.Report(recipe.Name+" indiriliyor… "+(total/1048576d).ToString("0.0")+" MB");last=DateTime.UtcNow;}
    }
    if(total<1024)throw new IOException("İndirilen dosya boş veya eksik.");
    if(response.Content.Headers.ContentLength is long expected&&total!=expected)throw new IOException("İndirme tamamlanmadı.");
   }
   progress.Report(HashOnlyAllowed(recipe)?"Üreticinin SHA-256 kaydı doğrulanıyor…":"Üreticinin dijital imzası doğrulanıyor…");
   await CheckExpectedHashAsync(recipe,partial);
   var publisher=HashOnlyAllowed(recipe)?recipe.Name+" / üreticinin SHA-256 kaydı doğrulandı (dijital imza yok)":await DriverService.SignatureAsync(partial);
   if(!HashOnlyAllowed(recipe)&&!recipe.CatalogSigned&&!publisher.Contains(recipe.Publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("İmza yayıncısı beklenen üreticiyle eşleşmedi: "+publisher);
   if(recipe.CatalogSigned&&string.IsNullOrWhiteSpace(publisher))throw new InvalidDataException("Katalog paketinin güvenilir Windows imzası yok.");
   File.Move(partial,path,false);var hash=await PackageSafety.HashFileAsync(folder,path);
   var record=new VerifiedDownload(recipe.Name,path,publisher,hash.Sha256,hash.Size,source.ToString(),DateTimeOffset.Now);
   await File.WriteAllTextAsync(LocalStore.FilePath("download-"+recipe.Id+".json"),JsonSerializer.Serialize(record,LocalStore.Json),cancellation);
   Journal.Write("Resmi kurulum paketi doğrulandı",recipe.Name+"\nYayıncı: "+publisher+"\nSHA-256: "+hash.Sha256);
   return(path,publisher,hash.Sha256);
  }finally{if(File.Exists(partial))File.Delete(partial);}
 }
 public static async Task VerifyCachedAsync(DownloadRecipe recipe,VerifiedDownload item)
 {
  var folder=LocalStore.FilePath("Downloads");var path=PackageSafety.ResolveInside(folder,Path.GetRelativePath(folder,item.Path));
  var hash=await PackageSafety.HashFileAsync(folder,path);if(hash.Sha256!=item.Hash||hash.Size!=item.Bytes)throw new InvalidDataException("İndirilen paket son doğrulamadan beri değişmiş. Yeniden indir.");
  if(!HashOnlyAllowed(recipe)){var publisher=await DriverService.SignatureAsync(path);if(string.IsNullOrWhiteSpace(publisher)||!recipe.CatalogSigned&&!publisher.Contains(recipe.Publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Paket yayıncısı doğrulanamadı.");}
  await CheckExpectedHashAsync(recipe,path);
 }
 private static async Task CheckExpectedHashAsync(DownloadRecipe recipe,string path)
 {
  if(recipe.ExpectedSha512.Length>0){await using var stream=File.OpenRead(path);var hash=Convert.ToHexString(await System.Security.Cryptography.SHA512.HashDataAsync(stream));if(!hash.Equals(recipe.ExpectedSha512,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Paket SHA-512 değeri üreticinin sürüm kaydıyla eşleşmedi.");}
  if(recipe.ExpectedSha256.Length>0){await using var stream=File.OpenRead(path);var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));if(!hash.Equals(recipe.ExpectedSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Paket SHA-256 değeri üreticinin sürüm kaydıyla eşleşmedi.");}
 }
}

