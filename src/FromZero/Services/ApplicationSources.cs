namespace FromZero;
public record GithubAppSource(string Id,string Name,string Repository,string AssetPattern,string Publisher,string InstalledName,string Category);
public static class ApplicationSources
{
 public static IReadOnlyList<GithubAppSource> Github {get;}=new List<GithubAppSource>{
  new("7zip","7-Zip","ip7z/7zip",@"^7z\d+-x64\.exe$","Igor Pavlov","7-Zip","Araçlar"),
  new("git","Git","git-for-windows/git",@"^Git-[\d.]+-64-bit\.exe$","Johannes Schindelin","Git","Geliştirme"),
  new("notepad","Notepad++","notepad-plus-plus/notepad-plus-plus",@"^npp\.[\d.]+\.Installer\.x64\.exe$","Notepad++","Notepad++","Geliştirme"),
  new("obs","OBS Studio","obsproject/obs-studio",@"^OBS-Studio-[\d.]+-Windows-x64-Installer\.exe$","OBS Project","OBS Studio","Medya"),
  new("powertoys","Microsoft PowerToys","microsoft/PowerToys",@"^PowerToysUserSetup-[\d.]+-x64\.exe$","Microsoft Corporation","PowerToys","Araçlar"),
  new("brave","Brave","brave/brave-browser",@"^BraveBrowserStandaloneSetup\.exe$","Brave Software","Brave","Tarayıcı"),
  new("powershell","PowerShell 7","PowerShell/PowerShell",@"^PowerShell-[\d.]+-win-x64\.msi$","Microsoft Corporation","PowerShell 7","Geliştirme"),
  new("sharex","ShareX","ShareX/ShareX",@"^ShareX-[\d.]+-setup-x64\.exe$","ShareX","ShareX","Medya"),
  new("audacity","Audacity","Audacity/audacity",@"^audacity-win-[\d.]+-x86_64\.msi$","MuseCY","Audacity","Medya"),
  new("handbrake","HandBrake","HandBrake/HandBrake",@"^HandBrake-[\d.]+-x86_64-Win_GUI\.exe$","Open Source Developer, Scott Rae","HandBrake","Medya"),
  new("rufus","Rufus","pbatard/rufus",@"^rufus-[\d.]+\.exe$","Akeo Consulting","Rufus","Araçlar"),
  new("winmerge","WinMerge","WinMerge/winmerge",@"^WinMerge-[\d.]+-x64-Setup\.exe$","Takashi Sawanaka","WinMerge","Geliştirme"),
  new("keepassxc","KeePassXC","KeePassXReboot/keepassxc",@"^KeePassXC-[\d.]+-x64\.msi$","DroidMonkey Apps","KeePassXC","Araçlar"),
  new("mpchc","MPC-HC","clsid2/mpc-hc",@"^MPC-HC\.[\d.]+\.x64\.exe$","MPC-HC","MPC-HC","Medya"),
  new("rustdesk","RustDesk","rustdesk/rustdesk",@"^rustdesk-[\d.]+-x86_64\.msi$","Purslane","RustDesk","Araçlar"),
  new("localsend","LocalSend","localsend/localsend",@"^LocalSend-[\d.]+-windows-x86-64\.exe$","Tien Do Nam","LocalSend","Araçlar"),
  new("screentogif","ScreenToGif","NickeManarin/ScreenToGif",@"^ScreenToGif\.[\d.]+\.Setup\.x64\.msi$","N-Tech","ScreenToGif","Medya"),
  new("dbeaver","DBeaver Community","dbeaver/dbeaver",@"^dbeaver-ce-[\d.]+-windows-x86_64\.exe$","DBeaver","DBeaver","Geliştirme"),
  new("heidisql","HeidiSQL","HeidiSQL/HeidiSQL",@"^HeidiSQL_[\d.]+_Setup\.exe$","Ansgar Becker","HeidiSQL","Geliştirme"),
  new("joplin","Joplin","laurent22/joplin",@"^Joplin-Setup-[\d.]+\.exe$","Joplin","Joplin","Üretkenlik"),
  new("obsidian","Obsidian","obsidianmd/obsidian-releases",@"^Obsidian-[\d.]+\.exe$","Dynalist","Obsidian","Üretkenlik")
 }.AsReadOnly();
 private static readonly HashSet<string> AdditionalIds=new(["firefox","vscode","chrome","edge","vlc","thunderbird","node-lts","node-current","vivaldi"],StringComparer.Ordinal);
 public static int Count=>Github.Count+AdditionalIds.Count+CommunityPackageCatalog.Definitions.Count;
 public static bool Known(string id)=>AdditionalIds.Contains(id)||Github.Any(s=>s.Id==id)||CommunityPackageCatalog.Definitions.Any(d=>d.Id==id);
 public static string Category(string id)=>Github.FirstOrDefault(s=>s.Id==id)?.Category??(id is "node-lts" or "node-current" or "vscode"?"Geliştirme":id is "firefox" or "chrome" or "edge" or "vivaldi"?"Tarayıcı":id=="vlc"?"Medya":"Üretkenlik");
}

