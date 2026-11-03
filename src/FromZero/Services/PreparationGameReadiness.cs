using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FromZero;

public static class PreparationGameReadiness
{
 public static List<(string Game,string Location)> DiscoverInstallations()
 {
  List<(string,string)> found=[];using var steam=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");var root=steam?.GetValue("SteamPath") as string;var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  if(root!=null&&Directory.Exists(root)){
   roots.Add(root);var libraries=Path.Combine(root,"steamapps","libraryfolders.vdf");if(File.Exists(libraries)&&new FileInfo(libraries).Length<2_000_000)foreach(Match m in Regex.Matches(File.ReadAllText(libraries),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
   foreach(var library in roots)foreach(var game in PreparationExperience.Games.Where(g=>g.SteamId.Length>0))foreach(var steamId in game.Id=="gtav"?new[]{game.SteamId,"3240220"}:new[]{game.SteamId}){
    var path=Path.Combine(library,"steamapps","appmanifest_"+steamId+".acf");if(!File.Exists(path)||new FileInfo(path).Length>2_000_000)continue;var text=File.ReadAllText(path);var directory=Regex.Match(text,"\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;
    if(directory.Length>0&&Path.GetFileName(directory)==directory){var install=Path.Combine(library,"steamapps","common",directory);if(Directory.Exists(install))found.Add((game.Id,install));}
   }
  }
  var manifests=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data","Manifests");
  if(Directory.Exists(manifests))foreach(var file in Directory.EnumerateFiles(manifests,"*.item").Take(200)){if(new FileInfo(file).Length>1_000_000)continue;try{using var json=JsonDocument.Parse(File.ReadAllText(file));var data=json.RootElement;var name=data.TryGetProperty("DisplayName",out var n)?n.GetString()??"":"";var location=data.TryGetProperty("InstallLocation",out var l)?l.GetString()??"":"";var game=name.Contains("Fortnite",StringComparison.OrdinalIgnoreCase)?"fortnite":name.Contains("Grand Theft Auto V",StringComparison.OrdinalIgnoreCase)?"gtav":"";if(game.Length>0&&Path.IsPathFullyQualified(location)&&Directory.Exists(location))found.Add((game,location));}catch(JsonException){}}
  var riotFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Riot Games","RiotClientInstalls.json");
  if(File.Exists(riotFile)&&new FileInfo(riotFile).Length<2_000_000)try{using var json=JsonDocument.Parse(File.ReadAllText(riotFile));if(json.RootElement.TryGetProperty("associated_client",out var clients))foreach(var client in clients.EnumerateObject())if(client.Name.Contains("VALORANT",StringComparison.OrdinalIgnoreCase)){var location=Directory.Exists(client.Name)?client.Name:File.Exists(client.Name)?Path.GetDirectoryName(client.Name):null;if(location!=null&&File.Exists(Path.Combine(location,"VALORANT.exe")))found.Add(("valorant",location));}}catch(JsonException){}
  var minecraft=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft");if(Directory.Exists(Path.Combine(minecraft,"versions")))found.Add(("minecraft",minecraft));return found.Distinct().ToList();
 }
 public static async Task<List<ReadinessCheck>> InspectAsync(IReadOnlyList<string> games,PreparationSnapshot snapshot,CancellationToken token)
 {
  var installations=DiscoverInstallations();var graphics=new GameGraphicsService().Discover();List<ReadinessCheck> checks=[];
  foreach(var id in games){var game=PreparationExperience.Games.Single(g=>g.Id==id);var paths=installations.Where(i=>i.Game==id).Select(i=>i.Location).ToList();var patterns=game.Launcher switch{"steam"=>new[]{"Steam"},"riot"=>["Riot Client"],"epic"=>["Epic Games Launcher"],"rockstar"=>["Rockstar Games Launcher","Steam","Epic Games Launcher"],_=>["Minecraft Launcher","Minecraft"]};var launcher=snapshot.Apps.Where(a=>ApplicationCatalog.Matches(a,patterns)).ToList();
   if(game.Launcher=="minecraft"){var packages=await Commands.PsAsync("@(Get-AppxPackage -Name '*Minecraft*' | Select-Object -ExpandProperty Name) -join ', '",20);if(packages.Success&&packages.Output.Trim().Length>0)checks.Add(new(id+"-store","Oyun / "+game.Name,"Store kaydı","pass",packages.Output.Trim(),"gaming"));}
   checks.Add(new(id+"-launcher","Oyun / "+game.Name,"Başlatıcı",launcher.Count>0?"pass":"manual",launcher.Count>0?string.Join(" / ",launcher.Select(a=>a.Name+" "+a.Version)):"Başlatıcı kurulum kaydı bulunamadı. Oyun mağazası veya mevcut kurulum konumunu kontrol et.","gaming"));
   checks.Add(new(id+"-files","Oyun / "+game.Name,"Yerel oyun kaydı / klasörü",paths.Count>0?"pass":"manual",paths.Count>0?string.Join("\n",paths):"Kayıtlı kütüphanelerde oyun klasörü doğrulanamadı. Tam disk taraması yapılmadı; oyunu başlatıcıdan kur veya mevcut klasörünü tanıt.","gaming"));
   if(id is "cs2" or "valorant")checks.Add(new(id+"-graphics","Oyun / "+game.Name,"Görüntü profili",graphics.Any(t=>t.Game==id)?"pass":"manual",graphics.Any(t=>t.Game==id)?"Desteklenen ayar dosyası bulundu. FPS / dengeli profili yedek alınarak uygulanabilir.":"Ayar dosyası yok. Oyunu en az bir kez açıp görüntü ayarını kaydet.","gaming"));
   checks.Add(new(id+"-launch","Oyun / "+game.Name,"İlk açılış ve FPS doğrulaması","manual",game.Notes+" Oyun açılışı, oturum açma ve gerçek FPS ölçümü ayrıca yapılmalı.","gaming"));
   if(id=="valorant"){
    var service=await Commands.PsAsync("$s=Get-Service -Name vgc -ErrorAction SilentlyContinue;if($s){$s.Status.ToString()}else{'NotInstalled'}",15);checks.Add(new("vanguard","Oyun / VALORANT","Vanguard hizmeti",service.Success&&service.Output.Trim()=="Running"?"pass":"manual",service.Output.Trim()+"; hizmet başlatılmadı veya kurulmadı.","gaming"));
    if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)){
     var security=await Commands.PsAsync("$t='Unknown';$s='Unknown';try{$t=[string](Get-Tpm -ErrorAction Stop).TpmPresent}catch{};try{$s=[string](Confirm-SecureBootUEFI -ErrorAction Stop)}catch{};[pscustomobject]@{TpmPresent=$t;SecureBoot=$s}|ConvertTo-Json",20);token.ThrowIfCancellationRequested();
     try{using var json=JsonDocument.Parse(security.Output);foreach(var key in new[]{"TpmPresent","SecureBoot"}){var value=json.RootElement.GetProperty(key).GetString()??"Unknown";checks.Add(new("valorant-"+key,"Oyun / VALORANT",key=="TpmPresent"?"TPM mevcut":"Secure Boot",value=="True"?"pass":value=="False"?"attention":"unavailable",value+". TPM sürümü / oyun kabulü ayrıca doğrulanmalıdır; güvenlik ayarı değiştirilmedi.","gaming"));}}catch(JsonException){checks.Add(new("valorant-security","Oyun / VALORANT","TPM / Secure Boot","unavailable","Bilgi okunamadı. Yönetici olarak durum taraması yapılabilir.","gaming"));}
    }
   }
  }
  return checks;
 }
}

