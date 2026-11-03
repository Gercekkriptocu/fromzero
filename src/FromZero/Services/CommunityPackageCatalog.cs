using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record CommunityPackage(string Id,string Slug,string Name,string Version,string Url,string Hash,string[] Hosts,string Homepage,string Source,string Category,string[] Aliases,string Checked);
public static class CommunityPackageCatalog
{
 private static readonly Lazy<IReadOnlyList<CommunityPackage>> definitions=new(()=>{
  using var stream=typeof(CommunityPackageCatalog).Assembly.GetManifestResourceStream("FromZero.application-packages.json")??throw new IOException("Geniş paket kataloğu bulunamadı.");var result=JsonSerializer.Deserialize<List<CommunityPackage>>(stream)??[];
  foreach(var d in result)Validate(d);if(result.Select(d=>d.Id).Distinct().Count()!=result.Count)throw new InvalidDataException("Katalog kimliği yineleniyor.");return result.AsReadOnly();});
 public static IReadOnlyList<CommunityPackage> Definitions=>definitions.Value;
 private static void Validate(CommunityPackage d){if(!Regex.IsMatch(d.Slug,"^[a-zA-Z0-9][a-zA-Z0-9.-]{1,70}$")||d.Id!="community-"+d.Slug||d.Source!="https://raw.githubusercontent.com/ScoopInstaller/Extras/master/bucket/"+d.Slug+".json"||!Regex.IsMatch(d.Hash,"^[a-fA-F0-9]{64}$")||d.Name.Length is <2 or >150||d.Aliases.Length==0||d.Aliases.Any(a=>a.Length<2)||!AppDownloads.Allowed(new Uri(d.Url),d.Hosts)||Path.GetExtension(new Uri(d.Url).AbsolutePath).ToLowerInvariant() is not (".exe" or ".msi"))throw new InvalidDataException("Geniş katalog kaydı geçersiz: "+d.Id);}
 internal static CommunityPackage Parse(CommunityPackage definition,string json)
 {
  using var doc=JsonDocument.Parse(json);var r=doc.RootElement;var version=r.GetProperty("version").GetString()??"";if(ApplicationCatalog.Number(version)==null||version.Contains("beta",StringComparison.OrdinalIgnoreCase)||version.Contains("preview",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Kararlı sayısal sürüm bulunamadı.");
  var artifact=r.TryGetProperty("architecture",out var architectures)?architectures.GetProperty("64bit"):r;
  string Scalar(JsonElement value){if(value.ValueKind==JsonValueKind.String)return value.GetString()!;if(value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()==1)return value[0].GetString()!;throw new InvalidDataException("Çok parçalı paket desteklenmiyor.");}
  var uri=new Uri(Scalar(artifact.GetProperty("url")));var url=uri.GetLeftPart(UriPartial.Query);var hash=Scalar(artifact.GetProperty("hash"));
  if(!AppDownloads.Allowed(uri,definition.Hosts))throw new InvalidDataException("İndirme alan adı değişmiş; yeni paket elle incelenmeli.");
  if(new Uri(definition.Url).Host=="github.com"&&!uri.AbsolutePath.StartsWith("/"+string.Join('/',new Uri(definition.Url).AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries).Take(2))+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Üretici deposu değişmiş.");
  var next=definition with{Version=version,Url=url,Hash=hash,Checked=DateTimeOffset.Now.ToString("yyyy-MM-dd")};Validate(next);return next;
 }
 private static DownloadRecipe Recipe(CommunityPackage d)=>new(d.Name+" "+d.Version,d.Url,"",d.Hosts,Id:"app-"+d.Id,ExpectedSha256:d.Hash,Extension:Path.GetExtension(new Uri(d.Url).AbsolutePath).ToLowerInvariant(),CatalogSigned:true);
 internal static bool Allowed(DownloadRecipe r)=>r.CatalogSigned&&!r.PublisherHashOnly&&r.Publisher==""&&Regex.IsMatch(r.ExpectedSha256,"^[a-fA-F0-9]{64}$")&&Definitions.Any(d=>"app-"+d.Id==r.Id&&r.Hosts.SequenceEqual(d.Hosts)&&AppDownloads.Allowed(new Uri(r.Url),d.Hosts)&&(new Uri(d.Url).Host!="github.com"||new Uri(r.Url).AbsolutePath.StartsWith("/"+string.Join('/',new Uri(d.Url).AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries).Take(2))+"/",StringComparison.OrdinalIgnoreCase)));
 private static ApplicationRelease Release(CommunityPackage d,IReadOnlyList<InstalledApp> apps,bool fresh,DateTimeOffset? checkedAt)
 {
  var recipe=Recipe(d);AppDownloads.RegisterApp(recipe);var installed=apps.Where(a=>ApplicationCatalog.Matches(a,d.Aliases)).Select(a=>a.Version).Distinct().ToArray();
  return new(recipe.Id,d.Name,d.Version,installed,fresh?ApplicationCatalog.State(installed,d.Version):"Kayıt kontrolü gerekli",recipe,d.Source,d.Category,checkedAt);
 }
 public static async Task<ApplicationScan> ScanAsync(IReadOnlyList<InstalledApp> apps,IProgress<string>? progress,CancellationToken token)
 {
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(30)};client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.9");using var limit=new SemaphoreSlim(6);int done=0;
  var results=await Task.WhenAll(Definitions.Select(async definition=>{
   await limit.WaitAsync(token);try{try{var feed=await ReleaseFeedCache.GetAsync(client,definition.Source,token);var current=Parse(definition,feed.Body);SourceHealth.Note(definition.Source,"Verified","Parsed package manifest");progress?.Report($"Geniş katalog • {Interlocked.Increment(ref done)}/{Definitions.Count} • {current.Name}");return (Release:Release(current,apps,true,feed.Checked),Warning:"");}
    catch(Exception ex)when(ex is HttpRequestException or IOException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException or TaskCanceledException){token.ThrowIfCancellationRequested();Interlocked.Increment(ref done);SourceHealth.Note(definition.Source,"Unavailable",ex.GetType().Name);return (Release:Release(definition,apps,false,null),Warning:definition.Name+": "+ex.Message);}
   }finally{limit.Release();}}));
  return new(DateTimeOffset.Now,results.Select(r=>r.Release).ToList(),results.Where(r=>r.Warning.Length>0).Select(r=>r.Warning).ToList());
 }
}


