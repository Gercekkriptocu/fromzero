using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record FaceitRank(int Rank,string Name,int Elo,string Region,string Updated,string Source,string Profile,Guid? PlayerId=null);
public record FaceitSnapshot(string Checked,string Scope,List<FaceitRank> Entries);
public static class FaceitCatalog
{
 public const string PublicGlobalUrl="https://cssurf.site/leaderboard";
 public static readonly string[] Regions=["EU","NA","SA","SEA","OCE"];
 public static string Url(string region)=>Regions.Contains(region)?"https://faceittracker.net/leaderboards/elo?region="+region:throw new InvalidDataException("FACEIT bölgesi tanınmadı.");
 internal static void Validate(FaceitSnapshot snapshot){if(snapshot.Entries.Count!=10||snapshot.Entries.Select(e=>e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=10||!snapshot.Entries.Select(e=>e.Rank).SequenceEqual(Enumerable.Range(1,10))||snapshot.Entries.Zip(snapshot.Entries.Skip(1),(a,b)=>a.Elo>=b.Elo).Any(v=>!v)||snapshot.Entries.Any(e=>e.Elo is <1000 or >10000||e.Name.Length is <2 or >40||!Regions.Contains(e.Region)||(e.Source!=Url(e.Region)&&e.Source!=PublicGlobalUrl)||e.Profile!="https://www.faceit.com/en/players/"+Uri.EscapeDataString(e.Name)))throw new InvalidDataException("FACEIT sıralama kaydı doğrulanamadı.");}
 internal static FaceitSnapshot ParseGlobal(string html)
 {
  if(html.Length>5_000_000)throw new InvalidDataException("Ranking page too large.");
  var regions=new Dictionary<string,(string Region,Guid? Id)>(StringComparer.OrdinalIgnoreCase);bool global=false;
  foreach(Match block in Regex.Matches(html,"self\\.__next_f\\.push\\(\\[1,(\"(?:\\\\.|[^\"\\\\])*\")\\]\\)",RegexOptions.Singleline,TimeSpan.FromSeconds(2))){
   var content=JsonSerializer.Deserialize<string>(block.Groups[1].Value)??"";
   if(!content.Contains("\"initialRegion\":\"Global\""))continue;
   var start=content.IndexOf("\"initialPlayers\":",StringComparison.Ordinal);if(start<0)continue;
   var reader=new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(content[(start+17)..]));using var data=JsonDocument.ParseValue(ref reader);
   foreach(var player in data.RootElement.EnumerateArray()){var name=player.GetProperty("nickname").GetString()!;var region=player.GetProperty("region").GetString()!;region=region=="OCEANIA"?"OCE":region;if(Regions.Contains(region))regions[name]=(region,player.TryGetProperty("playerId",out var id)&&id.TryGetGuid(out var uuid)?uuid:null);}global=true;
  }
  if(!global)throw new InvalidDataException("Public source no longer identifies a global leaderboard. Previous ranking kept.");
  var entries=new List<FaceitRank>();
  foreach(Match row in Regex.Matches(html,"<a\\b[^>]*>(?<body>.*?)</a>",RegexOptions.Singleline,TimeSpan.FromSeconds(2))){
   var body=row.Groups["body"].Value;if(!body.Contains("text-accent-soft"))continue;
   var name=WebUtility.HtmlDecode(Regex.Match(body,"<span[^>]*class=\"[^\"]*text-text-primary[^\"]*\"[^>]*>([^<]+)</span>",RegexOptions.None,TimeSpan.FromSeconds(1)).Groups[1].Value);
   var text=Regex.Match(body,"<span[^>]*class=\"[^\"]*text-accent-soft[^\"]*\"[^>]*>([0-9, ]+)</span>",RegexOptions.None,TimeSpan.FromSeconds(1)).Groups[1].Value;
   if(!regions.TryGetValue(name,out var region)||!int.TryParse(text.Replace(",","").Replace(" ",""),out var elo))continue;
   entries.Add(new(0,name,elo,region.Region,"",PublicGlobalUrl,"https://www.faceit.com/en/players/"+Uri.EscapeDataString(name),region.Id));
  }
  var top=entries.GroupBy(e=>e.Name,StringComparer.OrdinalIgnoreCase).Select(g=>g.MaxBy(e=>e.Elo)!).OrderByDescending(e=>e.Elo).ThenBy(e=>e.Name,StringComparer.Ordinal).Take(10).Select((e,i)=>e with{Rank=i+1}).ToList();
  var snapshot=new FaceitSnapshot(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"),"Global ELO · public HTML leaderboard · no API key",top);Validate(snapshot);return snapshot;
 }
 private static async Task<string> HtmlAsync(HttpClient client,string url){using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));var token=deadline.Token;using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>5_000_000)throw new IOException("Ranking source too large.");await using var input=await response.Content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[81920];int read;while((read=await input.ReadAsync(buffer,token))>0){if(memory.Length+read>5_000_000)throw new IOException("Ranking source limit exceeded.");memory.Write(buffer,0,read);}return System.Text.Encoding.UTF8.GetString(memory.ToArray());}
 public static FaceitSnapshot Read()
 {
  using var stream=typeof(FaceitCatalog).Assembly.GetManifestResourceStream("FromZero.faceit-top10.json")??throw new IOException("FACEIT kataloğu yok.");
  var bundled=JsonSerializer.Deserialize<FaceitSnapshot>(stream)??throw new InvalidDataException("FACEIT kataloğu yok.");
  Validate(bundled);
  try{
   var path=LocalStore.FilePath("faceit-top10.json");
   if(File.Exists(path)&&JsonSerializer.Deserialize<FaceitSnapshot>(File.ReadAllText(path)) is { } cached){
    Validate(cached);
    if(DateTimeOffset.TryParse(cached.Checked,System.Globalization.CultureInfo.InvariantCulture,out var savedAt)&&
     DateTimeOffset.TryParse(bundled.Checked,System.Globalization.CultureInfo.InvariantCulture,out var bundledAt)&&savedAt>=bundledAt)return cached;
   }
  }catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException){}
  return bundled;
 }
 internal static List<FaceitRank> ParseRegion(string region,string html)
 {
  if(html.Length>5_000_000)throw new InvalidDataException("Sıralama sayfası çok büyük.");var result=new List<FaceitRank>();
  foreach(Match row in Regex.Matches(html,"<tr\\s+data-history=\"(?<history>[^\"]*)\"[^>]*>(?<row>.*?)</tr>",RegexOptions.Singleline,TimeSpan.FromSeconds(2))){
   var content=row.Groups["row"].Value;var name=WebUtility.HtmlDecode(Regex.Match(content,"href=\"/players/[^>]*>([^<]+)").Groups[1].Value);var elo=Regex.Match(content,"data-th=\"Elo\"[^>]*>\\s*(\\d+)").Groups[1].Value;if(!int.TryParse(elo,out var value)||name.Length==0)continue;
   var updated="";try{using var history=JsonDocument.Parse(WebUtility.HtmlDecode(row.Groups["history"].Value));if(history.RootElement.GetArrayLength()>0)updated=history.RootElement[0].GetProperty("timestamp").GetString()??"";}catch(Exception ex)when(ex is JsonException or InvalidOperationException or KeyNotFoundException){}
   result.Add(new(result.Count+1,name,value,region,updated,Url(region),"https://www.faceit.com/en/players/"+Uri.EscapeDataString(name)));if(result.Count==10)break;
  }
  if(result.Count!=10)throw new InvalidDataException(region+" sıralama şeması değişmiş; önceki global kayıt korundu.");return result;
 }
 public static async Task<FaceitSnapshot> RefreshAsync()
 {
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(20)};client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.15 (public leaderboard reader)");FaceitSnapshot snapshot;
  try{snapshot=ParseGlobal(await HtmlAsync(client,PublicGlobalUrl));SourceHealth.Note(PublicGlobalUrl,"Verified","Global ranking and 10 unique players");}
  catch(Exception primary)when(primary is HttpRequestException or IOException or InvalidDataException or JsonException or OperationCanceledException or InvalidOperationException or KeyNotFoundException or RegexMatchTimeoutException){
   SourceHealth.Note(PublicGlobalUrl,"Unavailable",primary.GetType().Name);
   try{var pages=await Task.WhenAll(Regions.Select(async region=>ParseRegion(region,await HtmlAsync(client,Url(region)))));var entries=pages.SelectMany(p=>p).GroupBy(e=>e.Name,StringComparer.OrdinalIgnoreCase).Select(g=>g.MaxBy(e=>e.Elo)!).OrderByDescending(e=>e.Elo).Take(10).Select((e,i)=>e with{Rank=i+1}).ToList();snapshot=new(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"),"EU, NA, SA, SEA, OCE · public regional HTML",entries);Validate(snapshot);}
   catch(Exception fallback){throw new IOException("Public rankings could not be refreshed. No API key is required. The last verified list is still available. "+primary.Message+" / "+fallback.Message);}
  }
  var path=LocalStore.FilePath("faceit-top10.json");await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(snapshot));File.Move(path+".tmp",path,true);return snapshot;
 }
}
