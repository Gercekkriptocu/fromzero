using System.Net.Http;
namespace FromZero;
public record OemSource(string Name,string Url,string Category,string Terms,string Note="");
public record OemTool(string Id,string Name,string Url,string Category,string RecipeId,string Terms,string Note);
public static class OemSources
{
 public static IReadOnlyList<OemSource> All{get;}=Read("manufacturers").Select(p=>p.Length is 4 or 5?new OemSource(p[1],ValidateUrl(p[2]),p[0],p[3],p.Length==5?p[4]:""):throw new System.IO.InvalidDataException("Üretici kaydı geçersiz.")).ToArray();
 public static IReadOnlyList<OemTool> Tools{get;}=Read("manufacturer-tools").Select(p=>p.Length==7?new OemTool(p[0],p[1],ValidateUrl(p[2]),p[3],p[4],p[5],p[6]):throw new System.IO.InvalidDataException("Üretici aracı kaydı geçersiz.")).ToArray();
 private static IEnumerable<string[]> Read(string name)
 {
  using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FromZero."+name+".txt")??throw new System.IO.InvalidDataException("Üretici kataloğu bulunamadı.");
  using var reader=new System.IO.StreamReader(stream);string? line;while((line=reader.ReadLine())!=null)if(!string.IsNullOrWhiteSpace(line)&&!line.StartsWith('#'))yield return line.Split('|');
 }
 public static string ValidateUrl(string url)
 {
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length>0||uri.HostNameType!=UriHostNameType.Dns)throw new System.IO.InvalidDataException("Resmi destek adresi HTTPS olmalı.");return url;
 }
 public static bool Matches(string terms,string hardware)=>terms.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Any(term=>term.Length<=3?System.Text.RegularExpressions.Regex.IsMatch(hardware,@"(?<![\p{L}\p{N}])"+System.Text.RegularExpressions.Regex.Escape(term)+@"(?![\p{L}\p{N}])",System.Text.RegularExpressions.RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)):hardware.Contains(term,StringComparison.OrdinalIgnoreCase));
 public static string HardwareText(IEnumerable<DeviceInfo> devices,string system)=>system+"\n"+string.Join("\n",devices.Where(d=>d.Present).Select(d=>d.Name+" "+string.Join(" ",d.HardwareIds)));
 public static async Task<string> CheckPageAsync(string url)
 {
  if(!All.Any(s=>s.Url==url)&&!Tools.Any(s=>s.Url==url))throw new System.IO.InvalidDataException("This support page is outside the official catalogue.");
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(12)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.16");
  try
  {
   using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead);
   var code=(int)response.StatusCode;
   var state=response.IsSuccessStatusCode?"Reachable":code is >=300 and <400?"Redirected":"Unavailable";
   SourceHealth.Note(url,state,"HTTP "+code);
   return state+" · HTTP "+code;
  }
  catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException)
  {
   SourceHealth.Note(url,"Unavailable",ex.GetType().Name);return "Unavailable · "+ex.GetType().Name;
  }
 }
}

