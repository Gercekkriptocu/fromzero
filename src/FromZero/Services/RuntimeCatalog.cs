using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
namespace FromZero;

public record RuntimePackage(string Id,string Name,string Channel,string Version,string Architecture,bool Supported,string[] Installed,string State,DownloadRecipe Recipe)
{
 public bool NeedsUpdate=>Installed.Length>0&&State=="Güncelleme var";
}
public record RuntimeScan(DateTimeOffset Checked,List<RuntimePackage> Packages,List<string> Warnings,int Channels);
public static class RuntimeCatalog
{
 public const string IndexUrl="https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json";
 private static readonly string[] MetadataHosts=["builds.dotnet.microsoft.com","dotnetcli.blob.core.windows.net"];
 private static readonly string[] InstallerHosts=["builds.dotnet.microsoft.com","download.visualstudio.microsoft.com","dotnetcli.azureedge.net"];
 public static string[] Architectures=>RuntimeInformation.OSArchitecture==Architecture.Arm64?["arm64","x64","x86"]:RuntimeInformation.OSArchitecture==Architecture.X64?["x64","x86"]:["x86"];
 private static async Task<JsonDocument> GetJsonAsync(HttpClient client,string url,CancellationToken cancellation)
 {
  var uri=new Uri(url);if(!AppDownloads.Allowed(uri,MetadataHosts))throw new InvalidDataException("Resmi .NET sürüm kaynağı dışındaki adres reddedildi.");
  using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,cancellation);response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>24_000_000)throw new IOException("Sürüm kaydı boyut sınırını aştı.");
  await using var input=await response.Content.ReadAsStreamAsync(cancellation);using var output=new MemoryStream();var buffer=new byte[81920];int read;
  while((read=await input.ReadAsync(buffer,cancellation))>0){if(output.Length+read>24_000_000)throw new IOException("Sürüm kaydı çok büyük.");output.Write(buffer,0,read);}
  return JsonDocument.Parse(output.ToArray());
 }
 public static Dictionary<string,string[]> InstalledVersions()
 {
  var result=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
  foreach(var arch in Architectures){
   var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
    using var baseKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view);using var key=baseKey.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\"+arch);
    if(key?.GetValue("InstallLocation") is string root)roots.Add(root);
   }
   var program=Environment.GetEnvironmentVariable(arch=="x86"?"ProgramFiles(x86)":"ProgramW6432");if(!string.IsNullOrEmpty(program))roots.Add(Path.Combine(program,"dotnet",arch=="x64"&&RuntimeInformation.OSArchitecture==Architecture.Arm64?"x64":""));
   // The per-user default root belongs to the current process architecture.
   if(arch==RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant())roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".dotnet"));
   foreach(var framework in new[]{"Microsoft.NETCore.App","Microsoft.WindowsDesktop.App","Microsoft.AspNetCore.App","sdk"}){
    var versions=new HashSet<string>();foreach(var root in roots){try{var path=framework=="sdk"?Path.Combine(root,"sdk"):Path.Combine(root,"shared",framework);if(Directory.Exists(path))foreach(var dir in Directory.EnumerateDirectories(path)){var v=Path.GetFileName(dir);if(Version.TryParse(v,out _))versions.Add(v);}}catch(IOException){}catch(UnauthorizedAccessException){}}
    result[arch+"/"+framework]=versions.OrderByDescending(v=>Version.Parse(v)).ToArray();
   }
  }
  return result;
 }
 internal static List<RuntimePackage> ParseChannel(JsonElement document,string channel,bool supported,IReadOnlyDictionary<string,string[]> installed)
 {
  if(!Version.TryParse(channel,out var channelVersion)||channelVersion.Major<3)throw new InvalidDataException("Desteklenmeyen .NET sürüm hattı.");
  var result=new List<RuntimePackage>();
  var releases=document.GetProperty("releases").EnumerateArray().Where(r=>Version.TryParse(r.GetProperty("release-version").GetString(),out _)).OrderByDescending(r=>Version.Parse(r.GetProperty("release-version").GetString()!)).ToList();
  foreach(var (section,label,framework,kind) in new[]{("runtime",".NET Runtime","Microsoft.NETCore.App","runtime"),("windowsdesktop","Windows Desktop Runtime","Microsoft.WindowsDesktop.App","desktop"),("aspnetcore-runtime","ASP.NET Core Runtime","Microsoft.AspNetCore.App","aspnet"),("sdk",".NET SDK","sdk","sdk")}){
   var release=releases.FirstOrDefault(r=>r.TryGetProperty(section,out _));if(release.ValueKind==JsonValueKind.Undefined)continue;
   var products=section=="sdk"?releases.SelectMany(r=>r.TryGetProperty("sdks",out var sdks)?sdks.EnumerateArray().ToArray():r.TryGetProperty("sdk",out var sdk)?new[]{sdk}:Array.Empty<JsonElement>()).Where(p=>Version.TryParse(p.GetProperty("version").GetString(),out _)).GroupBy(p=>Version.Parse(p.GetProperty("version").GetString()!).Build/100).Select(g=>g.OrderByDescending(p=>Version.Parse(p.GetProperty("version").GetString()!)).First()).OrderByDescending(p=>Version.Parse(p.GetProperty("version").GetString()!)).ToList():[release.GetProperty(section)];
   foreach(var product in products){var version=product.GetProperty("version").GetString()??"";if(!Version.TryParse(version,out var latest)||latest.Major!=channelVersion.Major||latest.Minor!=channelVersion.Minor)continue;
   foreach(var arch in Architectures){
    var file=product.GetProperty("files").EnumerateArray().FirstOrDefault(f=>f.GetProperty("rid").GetString()=="win-"+arch&&f.GetProperty("name").GetString()!.EndsWith(".exe",StringComparison.OrdinalIgnoreCase));
    if(file.ValueKind==JsonValueKind.Undefined)continue;var url=file.GetProperty("url").GetString()!;var hash=file.GetProperty("hash").GetString()!;
    if(!AppDownloads.Allowed(new Uri(url),InstallerHosts))throw new InvalidDataException(".NET kurucusu resmi üretici adresinde değil.");
    var band=section=="sdk"?latest.Build/100*100:0;var extra=section=="sdk"&&product.GetProperty("version").GetString()!=products[0].GetProperty("version").GetString()?"-"+band:"";
    var id="dotnet-"+kind+"-"+channel.Replace('.','-')+extra+"-win-"+arch;var recipe=new DownloadRecipe(label+" "+version+" "+arch,url,"Microsoft Corporation",InstallerHosts,Id:id,ExpectedSha512:hash);
    AppDownloads.RegisterRuntime(recipe);
    var matches=installed.GetValueOrDefault(arch+"/"+framework,[]).Where(v=>Version.TryParse(v,out var parsed)&&parsed.Major==latest.Major&&parsed.Minor==latest.Minor&&(section!="sdk"||parsed.Build/100==latest.Build/100)).ToArray();
    var state=matches.Length==0?"Kurulu değil":matches.Any(v=>Version.Parse(v)>=latest)?"Güncel veya daha yeni":"Güncelleme var";
    result.Add(new(id,label+(extra.Length>0?" / "+band+" bandı (elle seçim)":""),channel,version,arch,supported&&extra.Length==0,matches,state,recipe));
   }}
  }
  return result;
 }
 public static async Task<RuntimeScan> ScanAsync(bool includeLegacy,IProgress<string>? progress=null,CancellationToken cancellation=default)
 {
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(50)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.5");
  using var index=await GetJsonAsync(client,IndexUrl,cancellation);var installed=InstalledVersions();var packages=new List<RuntimePackage>();var warnings=new List<string>();int channels=0;
  foreach(var item in index.RootElement.GetProperty("releases-index").EnumerateArray()){
   var channel=item.GetProperty("channel-version").GetString()!;var phase=item.GetProperty("support-phase").GetString();
   if(!Version.TryParse(channel,out var version)||version.Major<3||!Version.TryParse(item.GetProperty("latest-runtime").GetString(),out _))continue;
   var supported=phase is "active" or "maintenance";if(!supported&&!includeLegacy)continue;
   cancellation.ThrowIfCancellationRequested();progress?.Report(".NET "+channel+" / resmi sürüm listesi…");
   try{using var channelDoc=await GetJsonAsync(client,item.GetProperty("releases.json").GetString()!,cancellation);packages.AddRange(ParseChannel(channelDoc.RootElement,channel,supported,installed));channels++;}
   catch(Exception ex) when(ex is HttpRequestException or IOException or JsonException or InvalidDataException or TaskCanceledException){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);warnings.Add(".NET "+channel+": "+ex.Message);}
  }
  return new(DateTimeOffset.Now,packages,warnings,channels);
 }
}
