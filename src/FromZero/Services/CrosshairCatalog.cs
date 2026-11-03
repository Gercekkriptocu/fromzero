using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record CrosshairSnapshot(string Id,string Name,string Source,string Checked,string Updated,Dictionary<string,string> Fields,string Team="",string TeamChecked="");
public record CrosshairEntry(CrosshairSnapshot Snapshot,CrosshairSettings Settings,string Code);
public static class CrosshairCatalog
{
 public const string FormatSource="https://www.counter-strike.net/newsentry/674006995886408819";
 private static List<CrosshairSnapshot> Bundled(){using var resource=typeof(CrosshairCatalog).Assembly.GetManifestResourceStream("FromZero.crosshairs.json")??throw new IOException("Crosshair kataloğu bulunamadı.");return JsonSerializer.Deserialize<List<CrosshairSnapshot>>(resource)??[];}
 private static readonly Dictionary<string,CrosshairSnapshot> Profiles=Bundled().ToDictionary(s=>s.Id,StringComparer.OrdinalIgnoreCase);
 public static CrosshairSettings Settings(IReadOnlyDictionary<string,string> fields)
 {
  string Text(string key)=>fields.TryGetValue(key,out var text)?text:throw new InvalidDataException("Crosshair alanı eksik: "+key);
  int Number(string key)=>int.TryParse(Text(key),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)?value:throw new InvalidDataException("Crosshair sayı alanı tanınmadı.");
  byte Color(string key){var value=Number(key);return value is >=0 and <=255?(byte)value:throw new InvalidDataException("Crosshair rengi geçersiz.");}
  double Fraction(string key)=>double.TryParse(Text(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:throw new InvalidDataException("Crosshair oranı tanınmadı.");
  bool Flag(string key)=>Text(key) switch{"Yes"=>true,"No"=>false,_=>throw new InvalidDataException("Crosshair tercihi tanınmadı.")};
  int style=Text("cl_crosshairstyle") switch{"Dynamic Cross"=>0,"Dynamic Circle"=>1,"Dynamic Cross (Legacy)" or "Dynamic Cross (Legacy/Shot Feedback)"=>2,"Static Circle"=>3,"Static Cross"=>4,"Static Cross (Shot Feedback)"=>5,"Dot Only"=>6,"Dynamic Quad"=>7,"Static Square"=>8,"Static Quad"=>9,_=>throw new InvalidDataException("Crosshair stili tanınmadı.")};
  int outline=Text("cl_crosshair_drawoutline") switch{"No"=>0,"Yes" or "Full"=>1,"Half"=>2,_=>throw new InvalidDataException("Crosshair kenarlığı tanınmadı.")};
  var s=new CrosshairSettings(style,Flag("cl_crosshair_recoil"),Flag("cl_crosshairdot"),Flag("cl_crosshair_t"),outline,Color("cl_crosshaircolor_r"),Color("cl_crosshaircolor_g"),Color("cl_crosshaircolor_b"),Color("cl_crosshaircolor_a"),Color("cl_crosshairoutline_r"),Color("cl_crosshairoutline_g"),Color("cl_crosshairoutline_b"),Color("cl_crosshairoutline_a"),Number("cl_crosshair_gap"),Number("cl_crosshair_length"),Number("cl_crosshair_thickness"),Number("cl_crosshair_dynamic_spread_limit"),Number("cl_crosshair_dynamic_splitdist"),Fraction("cl_crosshair_dynamic_splitalpha_innermod"),Fraction("cl_crosshair_dynamic_splitalpha_outermod"),Fraction("cl_crosshair_dynamic_maxdist_splitratio"),Number("cl_crosshair_screen_height"),Fraction("cl_ironsight_dot_scale"),Flag("cl_ironsight_usecrosshaircolor"));CrosshairCode.Validate(s);return s;
 }
 public static CrosshairEntry Entry(CrosshairSnapshot snapshot)
 {
  if(!Profiles.TryGetValue(snapshot.Id,out var profile)||snapshot.Source!="https://prosettings.net/players/"+snapshot.Id+"/"||!DateOnly.TryParse(snapshot.Checked,out _)||
   snapshot.Team is { Length: > 100 }||snapshot.Team?.Any(char.IsControl)==true)throw new InvalidDataException("Crosshair katalog kaynağı doğrulanamadı.");
  var settings=Settings(snapshot.Fields);
  var team=string.IsNullOrWhiteSpace(snapshot.Team)?profile.Team:snapshot.Team;
  var teamChecked=DateOnly.TryParseExact(snapshot.TeamChecked,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)?
   snapshot.TeamChecked:team==profile.Team?profile.TeamChecked:"";
  return new(snapshot with{Name=profile.Name,Team=team,TeamChecked=teamChecked},settings,CrosshairCode.Encode(settings));
 }
 public static List<CrosshairEntry> Read()
 {
  var entries=new List<CrosshairEntry>();
  foreach(var original in Profiles.Values){var snapshot=original;var cache=LocalStore.FilePath("crosshair-"+original.Id+".json");try{if(File.Exists(cache)){var saved=JsonSerializer.Deserialize<CrosshairSnapshot>(File.ReadAllText(cache));if(saved?.Id==original.Id){Entry(saved);snapshot=saved;}}}catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException){}entries.Add(Entry(snapshot));}return entries;
 }
 internal static CrosshairSnapshot Parse(string id,string html)
 {
  if(!Profiles.TryGetValue(id,out var profile)||html.Length>4_000_000)throw new InvalidDataException("Crosshair kaynağı reddedildi.");
  var table=Regex.Match(html,"id=\"cs2_crosshair\".*?<table class=\"settings\">(?<table>.*?)</table>",RegexOptions.Singleline,TimeSpan.FromSeconds(2));if(!table.Success)throw new InvalidDataException("Kaynak crosshair tablosu değişmiş; önceki kayıt korundu.");
  var fields=new Dictionary<string,string>();foreach(Match match in Regex.Matches(table.Groups["table"].Value,"data-field=\"(?<key>[^\"]+)\".*?<td>(?<value>.*?)</td>",RegexOptions.Singleline,TimeSpan.FromSeconds(2))){var key=match.Groups["key"].Value;if(!fields.TryAdd(key,WebUtility.HtmlDecode(Regex.Replace(match.Groups["value"].Value,"<[^>]+>","")).Trim()))throw new InvalidDataException("Kaynak alanı yinelenmiş.");}
  var team=Regex.Match(html,"<th>Team</th>\\s*<td>\\s*<a href=\"https://prosettings\\.net/teams/[a-z0-9-]+/\">([^<]+)</a>",RegexOptions.None,TimeSpan.FromSeconds(2));
  var updated=Regex.Match(html,"article:modified_time\" content=\"([^\"]+)").Groups[1].Value;
  var checkedOn=DateTimeOffset.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  var snapshot=new CrosshairSnapshot(id,profile.Name,profile.Source,checkedOn,updated,fields,
   team.Success?WebUtility.HtmlDecode(team.Groups[1].Value).Trim():profile.Team,team.Success?checkedOn:profile.TeamChecked);
  Entry(snapshot);return snapshot;
 }
 public static async Task<CrosshairEntry> RefreshAsync(string id)
 {
  if(!Profiles.ContainsKey(id))throw new InvalidDataException("Katalog dışı oyuncu.");using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(25)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.9");using var response=await client.GetAsync("https://prosettings.net/players/"+id+"/",HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>4_000_000)throw new IOException("Kaynak çok büyük.");await using var input=await response.Content.ReadAsStreamAsync();using var output=new MemoryStream();var buffer=new byte[81920];int read;while((read=await input.ReadAsync(buffer))>0){if(output.Length+read>4_000_000)throw new IOException("Kaynak boyut sınırı aşıldı.");output.Write(buffer,0,read);}
  var snapshot=Parse(id,System.Text.Encoding.UTF8.GetString(output.ToArray()));SourceHealth.Note(snapshot.Source,"Verified","Catalogue fields and code");var path=LocalStore.FilePath("crosshair-"+id+".json");await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(snapshot,LocalStore.Json));File.Move(path+".tmp",path,true);return Entry(snapshot);
 }
}

