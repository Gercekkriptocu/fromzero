using System.Text.Json;
using FromZero;
static class WorkspaceUpgradeChecks
{
 public static void Run(Action<bool,string> check)
 {
  bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
  check(new PreparationOptions().Apps.Count==0&&PreparationExperience.Profile("basic").Apps.Count==0&&PreparationExperience.Profile("laptop").Apps.Count==0,"Fresh everyday/laptop preparation does not preselect unrelated applications");
  check(PreparationExperience.Profile("gaming").Apps.SequenceEqual(["steam"]),"Gaming preparation does not bundle unrelated browsers or media players");
  var product=Guid.NewGuid().ToString("B");var app=new InstalledApp("Fixture","1","Vendor","","","LocalMachine/Registry64",product,UninstallCommand:"msiexec /I"+product,WindowsInstaller:true);
  check(InstalledSoftware.DisplayLabel(app with{DisplayName=null!})=="Fixture"&&InstalledSoftware.DisplayLabel(app with{DisplayName="Friendly fixture"})=="Friendly fixture","Missing or null Store display labels fall back to the inventory identity without breaking the app list");
  var plan=AppUninstaller.Plan(app,_=>true);check(Path.GetFileName(plan.FileName)=="msiexec.exe"&&plan.Arguments.StartsWith("/x {"),"Uninstall uses the system MSI remover and an interactive product identity");
  var exe=app with{WindowsInstaller=false,RegistryKey="fixture",UninstallCommand="\"C:\\Program Files\\Fixture\\uninstall.exe\" /remove"};plan=AppUninstaller.Plan(exe,_=>true);
  check(plan.FileName==@"C:\Program Files\Fixture\uninstall.exe"&&plan.Arguments=="/remove","Quoted vendor uninstall paths preserve arguments without a shell");
  check(Reject(()=>AppUninstaller.Plan(exe with{UninstallCommand="cmd.exe /c erase *"},_=>true))&&Reject(()=>AppUninstaller.Plan(exe with{UninstallCommand=@"C:\Windows\System32\rundll32.exe evil.dll,Run"},_=>true)),"Uninstall rejects command shells and script/DLL launchers");
  check(Reject(()=>AppUninstaller.Plan(exe with{SystemComponent=true},_=>true))&&Reject(()=>AppUninstaller.Plan(exe with{NoRemove=true},_=>true))&&Reject(()=>AppUninstaller.Plan(exe with{ProtectedPackage=true},_=>true)),"Protected system, nonremovable and framework entries cannot be removed");
  check(Reject(()=>AppUninstaller.Plan(exe with{UninstallCommand=@"\\server\share\remove.exe"},_=>true))&&Reject(()=>AppUninstaller.Plan(exe,_=>false)),"Uninstall rejects remote paths and missing executables");
  var msix=app with{WindowsInstaller=false,PackageFullName="Vendor.Fixture_1.0.0.0_x64__1234567890abc"};check(AppUninstaller.Plan(msix).PackageFullName==msix.PackageFullName&&Reject(()=>AppUninstaller.Plan(msix with{PackageFullName="x';Remove-Item *"})),"MSIX removal plans require a fixed valid package identity");
  check(Locale.Language=="en"&&Locale.EntryCount>1500,"English is the default and the embedded translation catalogue loads");
  check(Locale.Translate("Genel bakış",false)=="Overview"&&Locale.Translate("Overview",true)=="Genel bakış","English and Turkish labels translate in both directions");
  check(Locale.Translate("ETKİN",false)=="ACTIVE"&&Locale.Translate("ÇEVRİMDIŞI",false)=="OFFLINE"&&
   Locale.Translate("ACTIVE",true)=="ETKİN"&&Locale.Translate("OFFLINE",true)=="ÇEVRİMDIŞI",
   "Live adapter states have exact English and Turkish labels");
  check(Locale.Translate("12 uygulama • 3 güncelleme",false).Contains("apps")&&Locale.Translate("12 uygulama • 3 güncelleme",false).Contains("updates"),"Dynamic count fragments preserve numbers and translate labels");
  check(Locale.Translate("CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRe",false)=="CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRe","Localization preserves crosshair codes");
  check(Locale.Translate("Google",false)=="Google"&&Locale.Translate("driver.exe",true)=="driver.exe","Localization preserves executable identifiers and vendor names");
  var players=Enumerable.Range(1,10).Select(i=>new{nickname="Player"+i,region=FaceitCatalog.Regions[i%5],elo=5000-i}).ToArray();
  var state="{\"initialRegion\":\"Global\",\"initialPlayers\":"+JsonSerializer.Serialize(players)+"}";
  var html="<script>self.__next_f.push([1,"+JsonSerializer.Serialize(state)+"])</script>"+string.Join("",players.Select((p,i)=>"<a href=\"/player?id="+i+"\"><span class=\"text-text-primary\">"+p.nickname+"</span><span class=\"text-accent-soft\">"+(6000-i).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+"</span></a>"));
  var snapshot=FaceitCatalog.ParseGlobal(html);check(snapshot.Entries.Count==10&&snapshot.Entries[0].Elo==6000&&snapshot.Entries[^1].Elo==5991,"Public FACEIT parser uses visible ELO values and merges regional identities without API calls");
  check(Reject(()=>FaceitCatalog.ParseGlobal(html.Replace("Global","EU")))&&Reject(()=>FaceitCatalog.ParseGlobal("challenge page")),"Regional-only or changed HTML cannot overwrite the global ranking");
  check(Reject(()=>FaceitCatalog.ParseGlobal(html.Replace("text-accent-soft","unknown"))),"Missing public ranking fields preserve the previous verified snapshot");
  var recipe=AppDownloads.Recipes.First();var optional=new ApplicationRelease("fixture","Fixture","2",[],"Kurulu değil",recipe,"source");
  check(!UpdateAssessment.For(optional).Recommended&&UpdateAssessment.For(optional).Title.Contains("OPTIONAL"),"Missing optional catalogue apps are never presented as required updates");
  check(UpdateAssessment.For(optional with{Installed=["1"],State="Güncelleme var"}).Recommended&&!UpdateAssessment.For(optional with{Installed=["?"],State="Sürüm karşılaştırılamadı"}).Recommended,"Only verified updates to installed apps are recommended, not unknown versions");
 }
}
