using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;

public record FaceitCrosshair(string Nickname,string Slug,string PlayerName,string Code,string Observed,string Checked,string Source,string Match,Guid? PlayerId=null,string Team="",string TeamChecked="");
public static class FaceitCrosshairs
{
 private static readonly Lazy<List<FaceitCrosshair>> bundled=new(()=>{
  using var stream=typeof(FaceitCrosshairs).Assembly.GetManifestResourceStream("FromZero.faceit-crosshairs.json")??throw new IOException("FACEIT crosshair kayıtları bulunamadı.");
  return JsonSerializer.Deserialize<List<FaceitCrosshair>>(stream)??[];
 });
 public static FaceitCrosshair? MatchPlayer(IEnumerable<FaceitCrosshair> entries,FaceitRank rank)=>
  entries.FirstOrDefault(e=>rank.PlayerId is Guid id&&e.PlayerId is Guid known?id==known:e.Nickname.Equals(rank.Name,StringComparison.OrdinalIgnoreCase));
 public static (string Team,string Checked) TeamFor(FaceitCrosshair? entry)
 {
  var team=entry==null?null:bundled.Value.FirstOrDefault(e=>e.Nickname.Equals(entry.Nickname,StringComparison.OrdinalIgnoreCase));
  return (team?.Team??"",team?.TeamChecked??"");
 }
 internal static void Validate(FaceitCrosshair item)
 {
  var definition=bundled.Value.SingleOrDefault(d=>d.Nickname.Equals(item.Nickname,StringComparison.OrdinalIgnoreCase));
  if(definition==null||item.Slug!=definition.Slug||item.PlayerName!=definition.PlayerName||(item.PlayerId.HasValue&&item.PlayerId!=definition.PlayerId)||item.Source!="https://crosshair.club/players/"+definition.Slug||!DateOnly.TryParseExact(item.Observed,"yyyy-MM-dd",out _)||!DateOnly.TryParseExact(item.Checked,"yyyy-MM-dd",out _)||item.Match.Length>300)throw new InvalidDataException("FACEIT crosshair kaynağı veya hesap eşlemesi geçersiz.");
  CrosshairCode.Decode(item.Code);
 }
 public static List<FaceitCrosshair> Read()
 {
  var result=new List<FaceitCrosshair>();foreach(var definition in bundled.Value){Validate(definition);var item=definition;
   try{var path=LocalStore.FilePath("faceit-crosshair-"+definition.Slug+".json");if(File.Exists(path)){var saved=JsonSerializer.Deserialize<FaceitCrosshair>(File.ReadAllText(path));if(saved!=null&&saved.Nickname==definition.Nickname){Validate(saved);if(string.CompareOrdinal(saved.Observed,definition.Observed)>0||(saved.Observed==definition.Observed&&string.CompareOrdinal(saved.Checked,definition.Checked)>=0))item=saved with{PlayerId=definition.PlayerId};}}}catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException){}
   result.Add(item);
  }return result;
 }
 internal static FaceitCrosshair Parse(FaceitCrosshair definition,string html)
 {
  Validate(definition);if(html.Length>4_000_000)throw new InvalidDataException("Crosshair sayfası çok büyük.");
  string Capture(string pattern)=>Regex.Match(html,pattern,RegexOptions.Singleline,TimeSpan.FromSeconds(2)).Groups[1].Value;
  var name=Capture("\"alternateName\":\\s*\"([^\"]+)\"");
  if(!name.Equals(definition.PlayerName,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Kaynak oyuncusu değişmiş; önceki hesap kaydı korundu.");
  var code=Capture("<code class=\"pl-x-code\">([^<]+)</code>");
  var observed=Capture("\"dateModified\":\\s*\"([^\"]+)\"");
  var match=Capture("<p class=\"pl-x-seen\">(.*?)</p>");
  match=Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(match,"<[^>]+>"," ")),"\\s+"," ").Trim();
  var item=definition with{Code=code,Observed=observed,Checked=DateTimeOffset.Now.ToString("yyyy-MM-dd"),Match=match};Validate(item);return item;
 }
 public static async Task<FaceitCrosshair> RefreshAsync(string nickname)
 {
  var definition=bundled.Value.SingleOrDefault(d=>d.Nickname.Equals(nickname,StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException("Bu FACEIT hesabı için doğrulanmış kaynak eşlemesi yok.");
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));var token=deadline.Token;
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All});client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.9.1");
  using var response=await client.GetAsync(definition.Source,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>4_000_000)throw new IOException("Crosshair kaynağı çok büyük.");
  await using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[81920];int read;
  while((read=await input.ReadAsync(buffer,token))>0){if(output.Length+read>4_000_000)throw new IOException("Kaynak boyut sınırı aşıldı.");output.Write(buffer,0,read);}
  var item=Parse(definition,System.Text.Encoding.UTF8.GetString(output.ToArray()));SourceHealth.Note(definition.Source,"Verified","Player identity and crosshair code");var path=LocalStore.FilePath("faceit-crosshair-"+definition.Slug+".json");
  await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(item),token);File.Move(path+".tmp",path,true);return item;
 }
}
