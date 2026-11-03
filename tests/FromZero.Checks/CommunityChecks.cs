using FromZero;
using System.Text.Json;
static class CommunityChecks
{
 public static void Run(Action<bool,string> check)
 {
  var definitions=CommunityPackageCatalog.Definitions;check(definitions.Count==570&&definitions.Select(d=>d.Id).Distinct().Count()==570,"Extended catalog contains 570 distinct typed HTTPS package definitions");
  var definition=definitions.First(d=>new Uri(d.Url).Host=="github.com");
  string Manifest(string url,string hash)=>JsonSerializer.Serialize(new{version="26.10.1",architecture=new Dictionary<string,object>{{"64bit",new{url,hash}}},installer=new{script="SHOULD NEVER RUN"}});
  var parsed=CommunityPackageCatalog.Parse(definition,Manifest(definition.Url,definition.Hash));check(parsed.Version=="26.10.1"&&parsed.Url==definition.Url,"Catalog parser reads only version, x64 URL and digest without executing manifest scripts");
  bool Reject(Action action){try{action();return false;}catch(Exception ex)when(ex is InvalidDataException or KeyNotFoundException){return true;}}
  check(Reject(()=>CommunityPackageCatalog.Parse(definition,Manifest("https://evil.invalid/setup.exe",definition.Hash))),"Community manifest cannot move its download to a new host");
  check(Reject(()=>CommunityPackageCatalog.Parse(definition,Manifest("https://github.com/unrelated/project/releases/download/v1/setup.exe",definition.Hash))),"Community manifest cannot replace the manufacturer repository");
  check(Reject(()=>CommunityPackageCatalog.Parse(definition,Manifest(definition.Url,"not-a-hash"))),"Community catalog requires a valid SHA-256 source digest");
  check(Reject(()=>CommunityPackageCatalog.Parse(definition,Manifest(definition.Url.Replace(".exe",".zip").Replace(".msi",".zip"),definition.Hash))),"Community catalog refuses archive masquerading as executable packages");
  var recipe=new DownloadRecipe(definition.Name,definition.Url,"",definition.Hosts,Id:"app-"+definition.Id,ExpectedSha256:definition.Hash,Extension:Path.GetExtension(new Uri(definition.Url).AbsolutePath),CatalogSigned:true);
  check(CommunityPackageCatalog.Allowed(recipe)&&!CommunityPackageCatalog.Allowed(recipe with{PublisherHashOnly=true})&&!CommunityPackageCatalog.Allowed(recipe with{ExpectedSha256=""}),"Extended recipes require trusted Windows signatures as well as catalog hashes");
  check(AppDownloads.Recipes.Count(r=>r.DriverTool)==11&&OemSources.All.Count==164&&OemSources.Tools.Count==55,"Official directory has 164 support pages, 55 tools and 11 signed-download recipes");
  check(OemSources.All.Select(s=>s.Url).Distinct().Count()==OemSources.All.Count&&OemSources.Tools.Select(s=>s.Id).Distinct().Count()==OemSources.Tools.Count,"Manufacturer pages and tool IDs are unique");
  check(OemSources.All.Select(s=>s.Category).Distinct().Count()==9&&OemSources.All.All(s=>s.Terms.Length>0&&OemSources.ValidateUrl(s.Url)==s.Url),"Official directory covers nine hardware categories with valid HTTPS URLs and matching terms");
  check(OemSources.Tools.Where(t=>t.RecipeId.Length>0).All(t=>AppDownloads.Recipes.Count(r=>r.Id==t.RecipeId&&r.DriverTool)==1)&&AppDownloads.Recipes.Where(r=>r.DriverTool).All(r=>OemSources.Tools.Count(t=>t.RecipeId==r.Id)==1),"Every download button maps to exactly one registered driver-tool recipe");
  check(Reject(()=>OemSources.ValidateUrl("http://www.dell.com/"))&&Reject(()=>OemSources.ValidateUrl("https://user@www.dell.com/"))&&Reject(()=>OemSources.ValidateUrl("https://www.dell.com:444/")),"Support directory refuses insecure, credentialed and nonstandard-port URLs");
  check(OemSources.Matches("HP","HP EliteBook")&&!OemSources.Matches("HP","Ralph device")&&OemSources.Matches("VEN_10DE","PCI\\VEN_10DE&DEV_2704"),"Brand filtering uses short-word boundaries and actual vendor hardware identifiers");
  var framework=OemSources.All.Single(s=>s.Name=="Framework");var zoom=OemSources.All.Single(s=>s.Name=="Zoom Audio");check(!OemSources.Matches(framework.Terms,"Microsoft Driver Framework")&&OemSources.Matches(framework.Terms,"Framework Laptop 13")&&!OemSources.Matches(zoom.Terms,"Zoom Virtual Audio")&&OemSources.Matches(zoom.Terms,"ZOOM H6 USB Audio"),"Generic Windows Framework and conferencing Zoom devices are not mistaken for hardware manufacturers");
  var hardware=OemSources.HardwareText([new("NVIDIA GPU","id","Display",0,["VEN_10DE"],true),new("Disconnected Logitech","old","USB",0,["VID_046D"],false)],"ASUSTeK");check(hardware.Contains("ASUSTeK")&&hardware.Contains("VEN_10DE")&&!hardware.Contains("VID_046D"),"Hardware matching excludes disconnected devices and retains system manufacturer context");
  var ranks=FaceitCatalog.Read();check(ranks.Entries.Count==10&&ranks.Entries.Select(r=>r.Rank).SequenceEqual(Enumerable.Range(1,10)),"FACEIT global snapshot has ten ordered distinct ranking entries");
  check((ranks.Scope.Contains("Global ELO")||ranks.Scope.Contains("SEA")&&ranks.Scope.Contains("OCE"))&&ranks.Entries.Zip(ranks.Entries.Skip(1),(a,b)=>a.Elo>=b.Elo).All(v=>v),"Global FACEIT view includes all five regions and sorts by ELO");
  check(Reject(()=>FaceitCatalog.Validate(ranks with{Entries=ranks.Entries.Select(r=>r with{Source="https://evil.invalid/"}).ToList()})),"FACEIT source substitution is refused");
 }
}

