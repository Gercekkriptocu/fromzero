using System.IO;
namespace FromZero;

public record PreparationApp(string Id,string Name,string Category,string[] Arguments,string[] Patterns,string Process);
public record PreparationProfile(string Id,string Name,string Description,string[] Apps,bool Gaming=false);
public record PreparationGame(string Id,string Name,string Launcher,string SteamId,string Source,string Notes);
public static class PreparationExperience
{
 private static readonly string[] Inno=["/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-"];
 private static readonly string[] Msi=["/qn","/norestart"];
 public static IReadOnlyList<PreparationApp> Apps{get;}=[
  new("firefox","Firefox","Tarayıcı",["/S"],["Mozilla Firefox"],"firefox"),new("chrome","Google Chrome","Tarayıcı",Msi,["Google Chrome"],"chrome"),new("vlc","VLC","Medya",["/S"],["VLC media player"],"vlc"),new("steam","Steam","Oyun",["/S"],["Steam"],"steam"),new("vscode","Visual Studio Code","Geliştirme",Inno,["Microsoft Visual Studio Code"],"Code"),
  new("git","Git","Geliştirme",Inno,["Git","Git version"],"git"),new("notepad","Notepad++","Araçlar",["/S"],["Notepad++"],"notepad++"),new("obs","OBS Studio","Yayın",["/S"],["OBS Studio"],"obs64"),new("powershell","PowerShell 7","Geliştirme",Msi,["PowerShell 7"],"pwsh"),new("audacity","Audacity","Yayın",Msi,["Audacity"],"Audacity"),new("winmerge","WinMerge","Araçlar",Inno,["WinMerge"],"WinMergeU"),new("keepassxc","KeePassXC","Araçlar",Msi,["KeePassXC"],"KeePassXC"),new("localsend","LocalSend","Araçlar",Inno,["LocalSend"],"localsend_app"),new("screentogif","ScreenToGif","Medya",Msi,["ScreenToGif"],"ScreenToGif"),new("dbeaver","DBeaver Community","Geliştirme",["/S"],["DBeaver"],"dbeaver"),new("heidisql","HeidiSQL","Geliştirme",Inno,["HeidiSQL"],"heidisql"),new("thunderbird","Thunderbird","Üretkenlik",["/S"],["Mozilla Thunderbird","Thunderbird"],"thunderbird"),new("node-lts","Node.js LTS","Geliştirme",Msi,["Node.js"],"node"),new("edge","Microsoft Edge","Tarayıcı",Msi,["Microsoft Edge"],"msedge"),new("brave","Brave","Tarayıcı",["--silent","--install"],["Brave"],"brave"),new("powertoys","Microsoft PowerToys","Araçlar",["/install","/quiet","/norestart"],["PowerToys"],"PowerToys")
 ];
 public static IReadOnlyList<PreparationProfile> Profiles{get;}=[
  new("basic","Günlük kullanım","Temel donanım ve Windows güncellemeleri. Ek uygulamaları sen seç.",[]),
  new("gaming","Rekabetçi oyun","Oyun bileşenleri, Steam ve seçtiğin oyunlara özel kontrol.",["steam"],true),
  new("streaming","Yayın ve içerik","OBS, ses düzenleme, mikrofon ve grafik kontrolleri.",["obs"]),
  new("developer","Yazılım geliştirme","Editör, Git, Node.js LTS, PowerShell ve terminal kontrolü.",["vscode","git","node-lts","powershell","notepad"]),
  new("laptop","Dizüstü kullanım","Touchpad, kamera, ses, pil ve üretici bileşenlerinin kontrolü.",[])
 ];
 public static IReadOnlyList<PreparationGame> Games{get;}=[
  new("cs2","Counter-Strike 2","steam","730","https://store.steampowered.com/app/730/CounterStrike_2/","Steam, etkin GPU sürücüsü ve mevcut oyun ayarları kontrol edilir. Başlatma satırı kopyalanabilir."),
  new("valorant","VALORANT","riot","","https://support-valorant.riotgames.com/","Riot / Vanguard durumu ve Windows 11'de TPM / Secure Boot okunur. Firmware ayarı değiştirilmez."),
  new("fortnite","Fortnite","epic","","https://www.epicgames.com/help/en-US/c-Category_Fortnite","Epic Games Launcher, GPU ve oyun kayıtları kontrol edilir; hesap girişi kullanıcıya aittir."),
  new("gtav","Grand Theft Auto V","rockstar","271590","https://support.rockstargames.com/","Steam/Epic/Rockstar kütüphaneleri okunur. Oyun indirilmez veya dosyaları taşınmaz."),
  new("minecraft","Minecraft","minecraft","","https://www.minecraft.net/en-us/download","Launcher / Store kaydı ve mevcut kurulum kontrol edilir. Java/Bedrock seçimine göre gereksinimler değişir."),
  new("apex","Apex Legends","steam","1172470","https://store.steampowered.com/app/1172470/Apex_Legends/","Steam/EA başlatıcısı, oyun kaydı ve grafik sürücüsü kontrol edilir.")
 ];
 public static PreparationOptions Profile(string id){var profile=Profiles.SingleOrDefault(p=>p.Id==id)??throw new InvalidDataException("Hazırlama profili bilinmiyor.");return new(){Profile=id,Apps=profile.Apps.ToList(),CoreLibraries=false,GamingLibraries=false};}
 public static PreparationOptions ChooseGame(PreparationOptions options,string id,bool selected){var game=Games.Single(g=>g.Id==id);var games=options.Games.Where(g=>g!=id).ToList();var apps=options.Apps.ToList();if(selected){games.Add(id);if(game.Launcher=="steam"&&!apps.Contains("steam"))apps.Add("steam");}return options with{Games=games,Apps=apps};}
 public static string Name(string id)=>Apps.SingleOrDefault(a=>a.Id==id)?.Name??throw new InvalidDataException("Hazırlama uygulaması bilinmiyor.");
 public static string Phase(PreparationStep step)=>step.Kind switch{"preflight" or "baseline"=>"Kontrol","backup" or "offline" or "drivers" or "oem" or "windows"=>"Sistem","package" or "app" or "gpu-apps"=>"Uygulamalar","preferences" or "games"=>"Kullanım","verify"=>"Sonuç",_=>"Kontrol"};
 public static int Sector(PreparationStep step)=>step.Kind switch{"backup" or "offline"=>1,"drivers" or "oem" or "windows"=>2,"app" or "package" or "gpu-apps"=>7,"games"=>4,"preferences"=>5,_=>0};
}
public record HardwareRecipe(string Model,string Kind,List<HardwareRecipeItem> Items);
public record HardwareRecipeItem(string Area,string Choice,string Basis,string Source);
public static class HardwareRecipes
{
 public static HardwareRecipe Build(string maker,string model,bool portable,IEnumerable<DeviceInfo> devices)
 {
  var present=devices.Where(d=>d.Present).ToList();List<HardwareRecipeItem> items=[];
  var portal=OemSources.All.FirstOrDefault(s=>s.Category=="Bilgisayar"&&OemSources.Matches(s.Terms,maker));
  var businessHp=(maker.Equals("HP",StringComparison.OrdinalIgnoreCase)||maker.Contains("Hewlett",StringComparison.OrdinalIgnoreCase))&&new[]{"EliteBook","ProBook","ZBook","EliteDesk","ProDesk","EliteOne","ProOne","HP Z","HP Pro","HP Elite"}.Any(p=>model.Contains(p,StringComparison.OrdinalIgnoreCase));
  items.Add(new("Üretici",maker.Contains("Dell",StringComparison.OrdinalIgnoreCase)?"Dell Command Update: sürücü kategorisi":businessHp?"HP Image Assistant: model / OS doğrulaması":"Windows Update + model özel resmi destek",maker+" / "+model+"; marka eşleşmesi tek başına paket uyumluluğu değildir.",portal?.Url??"https://support.microsoft.com/windows"));
  foreach(var area in new[]{("Net","Ağ"),("System","Yonga / sistem"),("Display","Grafik"),("Bluetooth","Bluetooth"),("MEDIA","Ses")}){
   var list=present.Where(d=>d.Class.Equals(area.Item1,StringComparison.OrdinalIgnoreCase)).ToList();var identities=list.SelectMany(d=>d.HardwareIds).Where(id=>id.StartsWith("PCI\\",StringComparison.OrdinalIgnoreCase)||id.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase)).Distinct().Take(4).ToArray();
   items.Add(new(area.Item2,list.Count>0?string.Join(" / ",list.Select(d=>d.Name).Distinct().Take(3)):"Aygıt tespit edilmedi",identities.Length>0?string.Join("\n",identities):"Otomatik karar için mevcut cihaz envanteri ve Windows teklifleri kullanılır.",portal?.Url??"https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process"));
  }
  if(portable)items.Add(new("Dizüstü bileşenleri","Touchpad, kamera, pil ve işlev tuşu / üretici bileşenleri","Mevcut aygıt ve hizmet durumları kontrol edilir; işlev tuşları kullanıcı testi gerektirir.",portal?.Url??"https://support.microsoft.com/windows"));
  return new((maker+" "+model).Trim(),portable?"Dizüstü / taşınabilir":"Masaüstü / türü doğrulanamadı",items);
 }
}
public record ReadinessCheck(string Id,string Area,string Label,string State,string Evidence,string Action="");
public record ReadinessReport(DateTimeOffset Checked,List<ReadinessCheck> Checks)
{
 public int Passed=>Checks.Count(c=>c.State=="pass");public int Attention=>Checks.Count(c=>c.State=="attention");public int Manual=>Checks.Count(c=>c.State=="manual");
}
public record PreparationSnapshot(DateTimeOffset Checked,List<DeviceInfo> Devices,List<DriverInfo> Drivers,List<InstalledApp> Apps,bool NetworkUp,List<string> AudioOutputs,List<string> AudioInputs,List<DisplayScreen> Displays,string Manufacturer,string Model,bool Portable,List<string> Warnings,string PowerPlan="");
public record PreparationChange(string Area,string Title,string Before,string After,bool Regression,string Step);
public static class PreparationComparison
{
 public static List<PreparationChange> Compare(PreparationSnapshot before,PreparationSnapshot after,string step)
 {
  List<PreparationChange> changes=[];
  foreach(var device in after.Devices.Where(d=>d.Present)){
   var previous=before.Devices.FirstOrDefault(d=>d.Present&&d.Id.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
   if(previous!=null&&previous.Problem!=device.Problem)changes.Add(new("Aygıt",device.Name,"Aygıt kodu "+previous.Problem,"Aygıt kodu "+device.Problem,previous.Problem==0&&device.Problem!=0,step));
   var old=before.Drivers.FirstOrDefault(d=>d.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));var current=after.Drivers.FirstOrDefault(d=>d.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
   if(old!=null&&current!=null&&(old.Version!=current.Version||old.Provider!=current.Provider))changes.Add(new("Sürücü",device.Name,old.Provider+" "+old.Version,current.Provider+" "+current.Version,false,step));
  }
  foreach(var missing in before.Devices.Where(d=>d.Present&&d.Problem==0&&!after.Devices.Any(a=>a.Present&&a.Id.Equals(d.Id,StringComparison.OrdinalIgnoreCase))))changes.Add(new("Aygıt",missing.Name,"Mevcuttu","Son taramada görünmüyor",true,step));
  if(before.NetworkUp!=after.NetworkUp)changes.Add(new("Ağ","Yerel bağlantı",before.NetworkUp?"Etkin":"Yok",after.NetworkUp?"Etkin":"Yok",before.NetworkUp&&!after.NetworkUp,step));
  if(before.AudioOutputs.Count>0&&after.AudioOutputs.Count==0)changes.Add(new("Ses","Etkin ses çıkışı",string.Join(", ",before.AudioOutputs),"Etkin çıkış bulunamadı",true,step));
  if(before.Displays.Count>0&&after.Displays.Count==0)changes.Add(new("Ekran","Etkin ekran","Mevcuttu","Son taramada okunamadı",true,step));
  foreach(var display in after.Displays){var previous=before.Displays.FirstOrDefault(d=>d.Name==display.Name);if(previous!=null&&previous.Current.Label!=display.Current.Label)changes.Add(new("Ekran",display.Name,previous.Current.Label,display.Current.Label,false,step));}
  if(before.PowerPlan.Length>0&&after.PowerPlan.Length>0&&!before.PowerPlan.Equals(after.PowerPlan,StringComparison.OrdinalIgnoreCase))changes.Add(new("Güç","Etkin güç planı",before.PowerPlan,after.PowerPlan,false,step));
  foreach(var app in after.Apps){var previous=before.Apps.FirstOrDefault(a=>a.Name.Equals(app.Name,StringComparison.OrdinalIgnoreCase));if(previous==null||previous.Version!=app.Version)changes.Add(new("Uygulama",app.Name,previous?.Version??"Kurulu değildi",app.Version,false,step));}
  return changes;
 }
}
