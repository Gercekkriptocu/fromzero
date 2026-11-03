using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record ApplicationRelease(string Id,string Name,string Version,string[] Installed,string State,DownloadRecipe Recipe,string Source,string Category="Genel",DateTimeOffset? SourceChecked=null)
{
 public bool NeedsUpdate=>State=="Güncelleme var";
}
public record ApplicationScan(DateTimeOffset Checked,List<ApplicationRelease> Applications,List<string> Warnings);
public static class ApplicationCatalog
{
 private static readonly string[] FeedHosts=["product-details.mozilla.org","update.code.visualstudio.com","versionhistory.googleapis.com","edgeupdates.microsoft.com","www.7-zip.org","update.videolan.org","api.github.com","github.com","nodejs.org","update.vivaldi.com"];
 private static async Task<string> TextAsync(HttpClient client,string url,CancellationToken token)
 {
  if(!AppDownloads.Allowed(new Uri(url),FeedHosts))throw new InvalidDataException("Uygulama sürüm kaynağı reddedildi.");
  return (await ReleaseFeedCache.GetAsync(client,url,token)).Body;
 }
 internal static Version? Number(string text){var match=Regex.Match(text,@"\d+(?:\.\d+){1,3}");return match.Success&&System.Version.TryParse(match.Value,out var parsed)?new Version(parsed.Major,parsed.Minor,Math.Max(0,parsed.Build),Math.Max(0,parsed.Revision)):null;}
 internal static bool Matches(InstalledApp app,string[] patterns)=>patterns.Any(p=>app.Name.Equals(p,StringComparison.OrdinalIgnoreCase)||app.Name.StartsWith(p+" ",StringComparison.OrdinalIgnoreCase)||app.Name.StartsWith(p+" (",StringComparison.OrdinalIgnoreCase)||app.Name.StartsWith(p+"_",StringComparison.OrdinalIgnoreCase)||app.Name.StartsWith(p+"-",StringComparison.OrdinalIgnoreCase))&&!new[]{"Beta","Nightly","Canary","Developer Edition","ESR","WebView2","Updater","Maintenance Service","Language Pack"}.Any(p=>app.Name.Contains(p,StringComparison.OrdinalIgnoreCase))&&!app.Name.EndsWith(" Update",StringComparison.OrdinalIgnoreCase);
 internal static string State(IEnumerable<string> installed,string latest)
 {
  var versions=installed.ToList();if(versions.Count==0)return "Kurulu değil";var target=Number(latest);var known=versions.Select(Number).OfType<Version>().ToList();return target==null||known.Count==0?"Sürüm karşılaştırılamadı":known.Max()>=target?"Güncel veya daha yeni":"Güncelleme var";
 }
 public static async Task<ApplicationScan> ScanAsync(IEnumerable<InstalledApp> installed,IProgress<string>? progress=null,CancellationToken token=default,IReadOnlySet<string>? selected=null)
 {
  if(selected!=null&&selected.Any(id=>!ApplicationSources.Known(id)||id.StartsWith("community-",StringComparison.Ordinal)))throw new InvalidDataException("Seçilen üretici kaynağı bilinmiyor.");
  string SourceName(string id)=>ApplicationSources.Github.FirstOrDefault(g=>g.Id==id)?.Name??id switch{"firefox"=>"Firefox","vscode"=>"Visual Studio Code","chrome"=>"Chrome","edge"=>"Edge","vlc"=>"VLC","thunderbird"=>"Thunderbird","node-lts"=>"Node.js LTS","node-current"=>"Node.js Current","vivaldi"=>"Vivaldi",_=>""};
  var selectedNames=selected?.Select(SourceName).ToHashSet(StringComparer.Ordinal);
  var apps=installed.ToList();var releases=new List<ApplicationRelease>();var warnings=new List<string>();
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(35)};client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.8");
  void Add(string id,string name,string version,string url,string publisher,string[] hosts,string source,string[] patterns,string sha="",string extension=".exe",bool sameMajor=false){
   if(Number(version)==null)throw new InvalidDataException("Üretici sürüm numarası okunamadı.");var recipe=new DownloadRecipe(name+" "+version,url,publisher,hosts,Id:"app-"+id,ExpectedSha256:sha,Extension:extension,PublisherHashOnly:id is "7zip" or "sharex" or "mpchc");AppDownloads.RegisterApp(recipe);SourceHealth.Note(source,"Verified","Parsed stable release");
   var found=apps.Where(a=>Matches(a,patterns)).Select(a=>a.Version).Distinct().ToArray();var track=sameMajor?found.Where(v=>Number(v)?.Major==Number(version)?.Major).ToArray():found;var state=sameMajor&&found.Length>0&&track.Length==0?"Başka sürüm hattı kurulu":State(track,version);releases.Add(new(recipe.Id,name,version,found,state,recipe,source,ApplicationSources.Category(id),ReleaseFeedCache.Read(source)?.Checked??DateTimeOffset.Now));
  }
  async Task Query(string name,Func<Task> query){if(selectedNames!=null&&!selectedNames.Contains(name))return;token.ThrowIfCancellationRequested();progress?.Report(name+" / üretici sürüm kaydı…");try{await query();}catch(Exception ex)when(ex is HttpRequestException or IOException or JsonException or InvalidDataException or InvalidOperationException or TaskCanceledException or System.Xml.XmlException or FormatException){if(token.IsCancellationRequested)throw new OperationCanceledException(token);warnings.Add(name+": "+ex.Message);}}
  await Query("Firefox",async()=>{const string url="https://product-details.mozilla.org/1.0/firefox_versions.json";using var doc=JsonDocument.Parse(await TextAsync(client,url,token));Add("firefox","Firefox",doc.RootElement.GetProperty("LATEST_FIREFOX_VERSION").GetString()!,"https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=tr","Mozilla Corporation",["mozilla.org","mozilla.net"],url,["Mozilla Firefox","Mozilla.Firefox"]);});
  await Query("Visual Studio Code",async()=>{const string url="https://update.code.visualstudio.com/api/update/win32-x64-user/stable/latest";using var doc=JsonDocument.Parse(await TextAsync(client,url,token));var r=doc.RootElement;Add("vscode","Visual Studio Code",r.GetProperty("productVersion").GetString()!,r.GetProperty("url").GetString()!,"Microsoft Corporation",["microsoft.com","visualstudio.com","vscode-cdn.net"],url,["Microsoft Visual Studio Code","Microsoft.VisualStudioCode"],r.GetProperty("sha256hash").GetString()!);});
  await Query("Chrome",async()=>{const string url="https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions/all/releases?filter=endtime=none&order_by=version%20desc";using var doc=JsonDocument.Parse(await TextAsync(client,url,token));var stable=doc.RootElement.GetProperty("releases").EnumerateArray().Where(r=>r.TryGetProperty("fraction",out var fraction)&&fraction.GetDouble()>=0.5).OrderByDescending(r=>Number(r.GetProperty("version").GetString()!)).First();Add("chrome","Google Chrome",stable.GetProperty("version").GetString()!,"https://dl.google.com/chrome/install/ChromeStandaloneSetup64.exe","Google LLC",["google.com"],url,["Google Chrome"]);});
  await Query("Edge",async()=>{const string url="https://edgeupdates.microsoft.com/api/products?view=enterprise";using var doc=JsonDocument.Parse(await TextAsync(client,url,token));var release=doc.RootElement.EnumerateArray().Single(r=>r.GetProperty("Product").GetString()=="Stable").GetProperty("Releases").EnumerateArray().Where(r=>r.GetProperty("Platform").GetString()=="Windows"&&r.GetProperty("Architecture").GetString()=="x64").OrderByDescending(r=>Number(r.GetProperty("ProductVersion").GetString()!)).First();var file=release.GetProperty("Artifacts").EnumerateArray().Single(a=>a.GetProperty("ArtifactName").GetString()=="msi");if(file.GetProperty("HashAlgorithm").GetString()!="SHA256")throw new InvalidDataException("Edge hash algoritması desteklenmiyor.");Add("edge","Microsoft Edge",release.GetProperty("ProductVersion").GetString()!,file.GetProperty("Location").GetString()!,"Microsoft Corporation",["microsoft.com"],url,["Microsoft Edge"],file.GetProperty("Hash").GetString()!,".msi");});
  await Query("VLC",async()=>{const string source="https://update.videolan.org/vlc/status-win-x64";var text=await TextAsync(client,source,token);var version=text.Split('\n')[0].Trim();Add("vlc","VLC",version,"https://mirror.verinomi.com/videolan/vlc/"+version+"/win64/vlc-"+version+"-win64.exe","VideoLAN",["videolan.org","mirror.verinomi.com"],source,["VLC media player"]);});
  await Query("Thunderbird",async()=>{const string source="https://product-details.mozilla.org/1.0/thunderbird_versions.json";using var doc=JsonDocument.Parse(await TextAsync(client,source,token));Add("thunderbird","Thunderbird",doc.RootElement.GetProperty("LATEST_THUNDERBIRD_VERSION").GetString()!,"https://download.mozilla.org/?product=thunderbird-latest-ssl&os=win64&lang=tr","Mozilla Corporation",["mozilla.org","mozilla.net","thunderbird.net"],source,["Mozilla Thunderbird","Thunderbird"]);});
  foreach(var lts in new[]{true,false})await Query(lts?"Node.js LTS":"Node.js Current",async()=>{const string source="https://nodejs.org/dist/index.json";using var doc=JsonDocument.Parse(await TextAsync(client,source,token));var r=doc.RootElement.EnumerateArray().First(v=>(!lts||v.GetProperty("lts").ValueKind==JsonValueKind.String)&&v.GetProperty("files").EnumerateArray().Any(f=>f.GetString()=="win-x64-msi"));var version=r.GetProperty("version").GetString()!;var filename="node-"+version+"-x64.msi";var sums=await TextAsync(client,"https://nodejs.org/dist/"+version+"/SHASUMS256.txt",token);var match=Regex.Match(sums,@"(?m)^([a-fA-F0-9]{64})\s+"+Regex.Escape(filename)+@"\r?$");if(!match.Success)throw new InvalidDataException("Node.js SHA-256 kaydı bulunamadı.");Add(lts?"node-lts":"node-current",lts?"Node.js LTS":"Node.js Current",version,"https://nodejs.org/dist/"+version+"/"+filename,"OpenJS Foundation",["nodejs.org"],source,["Node.js"],match.Groups[1].Value,".msi",true);});
  await Query("Vivaldi",async()=>{const string source="https://update.vivaldi.com/update/1.0/public/appcast.x64.xml";var xml=await TextAsync(client,source,token);using var reader=System.Xml.XmlReader.Create(new StringReader(xml),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null});var doc=System.Xml.Linq.XDocument.Load(reader);var file=doc.Descendants("enclosure").First(e=>e.Attribute("url")?.Value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)==true&&e.Attribute("url")!.Value.Contains(".x64.",StringComparison.OrdinalIgnoreCase));var url=file.Attribute("url")!.Value;var version=Regex.Match(url,@"\d+\.\d+\.\d+\.\d+").Value;Add("vivaldi","Vivaldi",version,url,"Vivaldi Technologies",["vivaldi.com"],source,["Vivaldi"]);});
  foreach(var definition in ApplicationSources.Github)await Query(definition.Name,async()=>{
   var (id,name,repo,pattern,publisher,installedName,category)=definition;
   var source="https://api.github.com/repos/"+repo+"/releases/latest";string metadata;
   try{metadata=await TextAsync(client,source,token);}catch(Exception ex)when(ex is HttpRequestException or IOException){var page=await GithubPageAsync(client,definition,token);Add(id,name,page.Version,page.Url,publisher,["github.com","githubusercontent.com"],"https://github.com/"+repo+"/releases/latest",id=="git"?["Git version","Git"]:[installedName],extension:page.Extension);return;}
   using var doc=JsonDocument.Parse(metadata);var r=doc.RootElement;if(r.GetProperty("prerelease").GetBoolean()||r.GetProperty("draft").GetBoolean())throw new InvalidDataException("Kararlı sürüm bulunamadı.");
   var asset=r.GetProperty("assets").EnumerateArray().First(a=>Regex.IsMatch(a.GetProperty("name").GetString()!,pattern,RegexOptions.IgnoreCase));var sha=asset.TryGetProperty("digest",out var digest)&&digest.GetString()?.StartsWith("sha256:")==true?digest.GetString()![7..]:"";
   Add(id,name,(id=="git"?r.GetProperty("tag_name").GetString()!.Replace(".windows.","."):r.GetProperty("tag_name").GetString()!),asset.GetProperty("browser_download_url").GetString()!,publisher,["github.com","githubusercontent.com"],source,id=="git"?["Git version","Git"]:[installedName],sha,asset.GetProperty("name").GetString()!.EndsWith(".msi",StringComparison.OrdinalIgnoreCase)?".msi":".exe");
  });
  using(var stream=typeof(ApplicationCatalog).Assembly.GetManifestResourceStream("FromZero.producer-snapshots.json")){
   if(stream!=null)foreach(var saved in JsonSerializer.Deserialize<List<ApplicationRelease>>(stream)??[]){
    if(selected!=null&&!selected.Contains(saved.Id[4..]))continue;
    if(releases.Any(r=>r.Id==saved.Id))continue;
    if(!saved.Id.StartsWith("app-",StringComparison.Ordinal)||saved.Id.StartsWith("app-community-",StringComparison.Ordinal)||!ApplicationSources.Known(saved.Id[4..])||saved.Id!=saved.Recipe.Id||!AppDownloads.Allowed(new Uri(saved.Source),FeedHosts))throw new InvalidDataException("Gömülü üretici kaydı doğrulanamadı.");
    AppDownloads.RegisterApp(saved.Recipe);var alias=ApplicationSources.Github.FirstOrDefault(d=>"app-"+d.Id==saved.Id)?.InstalledName??saved.Name;
    var found=apps.Where(a=>Matches(a,[alias,saved.Name])).Select(a=>a.Version).Distinct().ToArray();
    releases.Add(saved with{Installed=found,State="Kayıt kontrolü gerekli",SourceChecked=null});
   }
  }
  if(selected==null){var community=await CommunityPackageCatalog.ScanAsync(apps,progress,token);releases.AddRange(community.Applications);warnings.AddRange(community.Warnings);}
  return new(DateTimeOffset.Now,releases,warnings);
 }
 internal static (string Version,string Url,string Extension) GithubHtmlPackage(GithubAppSource definition,string tag,string html)
 {
  if(tag.Length>150||!Regex.IsMatch(tag,@"^[\w.\-+]+$"))throw new InvalidDataException("Üreticinin kararlı sürüm etiketi geçersiz.");
  foreach(Match match in Regex.Matches(html,"href=\"([^\"]+)\"",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1))){var path=WebUtility.HtmlDecode(match.Groups[1].Value);var prefix="/"+definition.Repository+"/releases/download/"+Uri.EscapeDataString(tag)+"/";if(!path.StartsWith(prefix,StringComparison.Ordinal))continue;var url=new Uri(new Uri("https://github.com"),path);var asset=Uri.UnescapeDataString(url.Segments.Last());if(!Regex.IsMatch(asset,definition.AssetPattern,RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)))continue;return(tag.Replace(".windows.","."),url.AbsoluteUri,asset.EndsWith(".msi",StringComparison.OrdinalIgnoreCase)?".msi":".exe");}
  throw new InvalidDataException("Üreticinin kararlı sürüm sayfasında uygun x64 kurucu bulunamadı.");
 }
 private static async Task<(string Version,string Url,string Extension)> GithubPageAsync(HttpClient client,GithubAppSource definition,CancellationToken token)
 {
  var latest=new Uri("https://github.com/"+definition.Repository+"/releases/latest");using var response=await client.GetAsync(latest,HttpCompletionOption.ResponseHeadersRead,token);var location=response.Headers.Location??throw new IOException("Üreticinin kararlı sürüm yönlendirmesi okunamadı.");var target=location.IsAbsoluteUri?location:new Uri(latest,location);var prefix="/"+definition.Repository+"/releases/tag/";
  if(target.Scheme!="https"||target.Host!="github.com"||!target.AbsolutePath.StartsWith(prefix,StringComparison.Ordinal)||target.Query.Length>0||target.UserInfo.Length>0)throw new InvalidDataException("Üretici sürüm yönlendirmesi reddedildi.");
  var tag=Uri.UnescapeDataString(target.AbsolutePath[prefix.Length..]);var html=await TextAsync(client,"https://github.com/"+definition.Repository+"/releases/expanded_assets/"+Uri.EscapeDataString(tag),token);return GithubHtmlPackage(definition,tag,html);
 }
}


