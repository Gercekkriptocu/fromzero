using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FromZero;

public record GraphicsTarget(string Game,string Label,string Root,string Path);
public record GraphicsChange(string Section,string Key,string Before,string After);
public record GraphicsPlan(Guid Id,GraphicsTarget Target,string Preset,string BeforeHash,byte[] Original,byte[] Updated,IReadOnlyList<GraphicsChange> Changes,IReadOnlyList<string> Unavailable);
public record GraphicsBackup(Guid Id,DateTimeOffset Created,GraphicsTarget Target,string Preset,string OriginalHash,string AppliedHash,string OriginalBase64,List<GraphicsChange> Changes);
public record GraphicsRestoreResult(int Restored,int Preserved,bool Exact);
public sealed class GameGraphicsService
{
 private readonly string store;
 private readonly Func<HashSet<string>> running;
 private readonly IReadOnlyList<GraphicsTarget>? fixtures;
 private GraphicsPlan? issued;
 public const string RiotSource="https://support.riotgames.com/en-us/riot/performance/optimize-graphics-settings-for-better-fps-windows-and-mac";
 public const string EngineSource="https://dev.epicgames.com/documentation/en-us/unreal-engine/scalability-reference-for-unreal-engine";
 public const string NvidiaSource="https://www.nvidia.com/en-us/geforce/news/counter-strike-2-released-featuring-nvidia-reflex/";
 public GameGraphicsService(string? store=null,Func<HashSet<string>>? running=null,IReadOnlyList<GraphicsTarget>? fixtures=null){this.store=store??LocalStore.FilePath("GameGraphics");this.running=running??ReclaimCleaner.RunningProcesses;this.fixtures=fixtures;}
 public IReadOnlyList<GraphicsTarget> Discover()
 {
  if(fixtures!=null)return fixtures;
  var result=new List<GraphicsTarget>();
  using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){
   if(key?.GetValue("SteamPath") is string steam){var root=Path.Combine(steam,"userdata");if(Directory.Exists(root))foreach(var account in Directory.EnumerateDirectories(root)){
    if(!Regex.IsMatch(Path.GetFileName(account),@"^\d+$"))continue;var file=Path.Combine(account,"730","local","cfg","cs2_video.txt");
    if(File.Exists(file))result.Add(new("cs2","CS2 / Steam hesabı "+Path.GetFileName(account),root,file));
   }}
  }
  var valorant=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VALORANT","Saved","Config");
  if(Directory.Exists(valorant))foreach(var account in Directory.EnumerateDirectories(valorant))foreach(var platform in new[]{"Windows","WindowsClient"}){
   var file=Path.Combine(account,platform,"GameUserSettings.ini");if(File.Exists(file))result.Add(new("valorant","Valorant / "+Path.GetFileName(account),valorant,file));
  }
  return result.AsReadOnly();
 }
 private void Validate(GraphicsTarget target)
 {
  if(target.Game is not ("cs2" or "valorant")||!Path.GetFileName(target.Path).Equals(target.Game=="cs2"?"cs2_video.txt":"GameUserSettings.ini",StringComparison.OrdinalIgnoreCase)||!Discover().Contains(target))throw new InvalidDataException("Bu oyun ayarı dosyası algılanmış hedefler arasında değil.");
  PackageSafety.ResolveInside(target.Root,Path.GetRelativePath(target.Root,target.Path));
  var attrs=File.GetAttributes(target.Path);if((attrs&(FileAttributes.ReadOnly|FileAttributes.ReparsePoint|FileAttributes.Offline))!=0)throw new IOException("Salt okunur, bağlantılı veya bulut dosyası değiştirilemez.");
  if(new FileInfo(target.Path).Length>2_000_000)throw new InvalidDataException("Oyun ayarı dosyası beklenenden büyük.");
 }
 private void RequireClosed(string game)
 {
  var processes=running();var blockers=game=="cs2"?new[]{"cs2.exe"}:new[]{"VALORANT.exe","VALORANT-Win64-Shipping.exe"};
  if(blockers.Any(processes.Contains))throw new InvalidOperationException("Önce oyunu kapat. Görüntü ayarları oyun çalışırken değiştirilmez.");
 }
 private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
 private static (string Text,Encoding Encoding,byte[] Preamble) Decode(byte[] bytes)
 {
  Encoding encoding;int offset;
  if(bytes.AsSpan().StartsWith(new byte[]{0xff,0xfe})){encoding=new UnicodeEncoding(false,true,true);offset=2;}
  else if(bytes.AsSpan().StartsWith(new byte[]{0xfe,0xff})){encoding=new UnicodeEncoding(true,true,true);offset=2;}
  else{encoding=new UTF8Encoding(false,true);offset=bytes.AsSpan().StartsWith(new byte[]{0xef,0xbb,0xbf})?3:0;}
  var text=encoding.GetString(bytes,offset,bytes.Length-offset);if(text.Contains('\0'))throw new InvalidDataException("Desteklenmeyen oyun ayarı kodlaması.");
  return(text,encoding,bytes.Take(offset).ToArray());
 }
 private static byte[] Encode(string text,Encoding encoding,byte[] preamble)=>preamble.Concat(encoding.GetBytes(text)).ToArray();
 private static Dictionary<string,string> Desired(string game,string preset)
 {
  if(preset is not ("competitive" or "balanced"))throw new InvalidDataException("Desteklenmeyen görüntü profili.");var low=preset=="competitive";var level=low?"0":"1";
  return game=="cs2"?new(StringComparer.OrdinalIgnoreCase){
   ["setting.cpu_level"]=level,["setting.gpu_level"]=level,["setting.gpu_mem_level"]=low?"0":"2",["setting.shaderquality"]=level,
   ["setting.r_texturefilteringquality"]=low?"0":"1",["setting.msaa_samples"]=low?"0":"2",
   ["setting.videocfg_texture_detail"]=level,["setting.videocfg_particle_detail"]=level,["setting.videocfg_ao_detail"]="0"
  }:new(StringComparer.OrdinalIgnoreCase){
   ["sg.AntiAliasingQuality"]=level,["sg.ShadowQuality"]="0",["sg.PostProcessQuality"]="0",["sg.TextureQuality"]=level,["sg.EffectsQuality"]=level,["sg.FoliageQuality"]="0",["sg.ShadingQuality"]=level
  };
 }
 private static Regex VdfPattern(string key)=>new("^(?<prefix>[ \\t]*\""+Regex.Escape(key)+"\"[ \\t]+\")(?<value>[^\"\\r\\n]*)(?<suffix>\"[^\\r\\n]*\\r?)$",RegexOptions.Multiline|RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
 internal static (string Text,List<GraphicsChange> Changes,List<string> Missing) Patch(string game,string preset,string text,bool disableVsync=false)
 {
  if(game is not ("cs2" or "valorant"))throw new InvalidDataException("Desteklenmeyen oyun.");
  var desired=Desired(game,preset);if(disableVsync)desired[game=="cs2"?"setting.mat_vsync":"bUseVSync"]=game=="cs2"?"0":"False";
  var changes=new List<GraphicsChange>();var missing=new List<string>();
  if(game=="cs2"){
   if(!Regex.IsMatch(text,"^\\s*\"(?:video\\.cfg|VideoConfig)\"",RegexOptions.IgnoreCase)||text.Count(c=>c=='{')!=1||text.Count(c=>c=='}')!=1)throw new InvalidDataException("CS2 görüntü şeması tanınmadı. Dosya değiştirilmedi.");
   foreach(var item in desired){var pattern=VdfPattern(item.Key);var matches=pattern.Matches(text);if(matches.Count>1)throw new InvalidDataException("Yinelenen görüntü anahtarı: "+item.Key);if(matches.Count==0){missing.Add(item.Key);continue;}
    var before=matches[0].Groups["value"].Value;if(!int.TryParse(before,out var number)||number is <0 or >8)throw new InvalidDataException("Bilinmeyen CS2 ayar değeri: "+item.Key);
    if(before!=item.Value){changes.Add(new("video.cfg",item.Key,before,item.Value));text=pattern.Replace(text,m=>m.Groups["prefix"].Value+item.Value+m.Groups["suffix"].Value);}
   }
   if(desired.Count-missing.Count<3)throw new InvalidDataException("CS2 görüntü anahtarları tanınmadı. Önce oyunda görüntü ayarını kaydet.");
  }else{
   var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var section="";var lines=Regex.Split(text,"(?<=\\n)");
   for(int i=0;i<lines.Length;i++){
    var trimmed=lines[i].Trim();if(trimmed.StartsWith('[')&&trimmed.EndsWith(']')){section=trimmed[1..^1];continue;}
    var match=Regex.Match(lines[i],@"^(?<prefix>[ \t]*)(?<key>[^=;#\r\n]+?)(?<middle>[ \t]*=[ \t]*)(?<value>[^;\r\n]*?)(?<suffix>[ \t]*(?:;[^\r\n]*)?\r?\n?)$");
    if(!match.Success)continue;var key=match.Groups["key"].Value.Trim();if(!desired.TryGetValue(key,out var after))continue;
    var allowed=key.StartsWith("sg.",StringComparison.OrdinalIgnoreCase)?section.Equals("ScalabilityGroups",StringComparison.OrdinalIgnoreCase):section.Equals("/Script/ShooterGame.ShooterGameUserSettings",StringComparison.OrdinalIgnoreCase)||section.Equals("/Script/Engine.GameUserSettings",StringComparison.OrdinalIgnoreCase);
    if(!allowed)continue;if(!found.Add(key))throw new InvalidDataException("Yinelenen görüntü anahtarı: "+key);
    var before=match.Groups["value"].Value.Trim();if(key.StartsWith("sg.",StringComparison.OrdinalIgnoreCase)&&(!int.TryParse(before,out var number)||number is <0 or >4))throw new InvalidDataException("Bilinmeyen Valorant kalite değeri: "+key);
    if(key=="bUseVSync"&&!bool.TryParse(before,out _))throw new InvalidDataException("Bilinmeyen VSync değeri.");
    if(!before.Equals(after,StringComparison.OrdinalIgnoreCase)){changes.Add(new(section,key,before,after));lines[i]=match.Groups["prefix"].Value+match.Groups["key"].Value+match.Groups["middle"].Value+after+match.Groups["suffix"].Value;}
   }
   missing.AddRange(desired.Keys.Where(k=>!found.Contains(k)));text=string.Concat(lines);
   if(found.Count<2)throw new InvalidDataException("Valorant kalite grubu tanınmadı. Önce oyunda görüntü ayarını kaydet; dosya şeması değişmiş olabilir.");
  }
  return(text,changes,missing);
 }
 public GraphicsPlan Preview(GraphicsTarget target,string preset,bool disableVsync=false)
 {
  Validate(target);RequireClosed(target.Game);var original=File.ReadAllBytes(target.Path);var doc=Decode(original);var patch=Patch(target.Game,preset,doc.Text,disableVsync);
  issued=new(Guid.NewGuid(),target,preset,Hash(original),original,Encode(patch.Text,doc.Encoding,doc.Preamble),patch.Changes.AsReadOnly(),patch.Missing.AsReadOnly());return issued;
 }
 public IReadOnlyList<GraphicsBackup> Backups()
 {
  if(!Directory.Exists(store))return [];var result=new List<GraphicsBackup>();PackageSafety.RejectReparsePoints(store,store);
  foreach(var path in Directory.EnumerateFiles(store,"*.json")){try{PackageSafety.RejectReparsePoints(store,path);var backup=JsonSerializer.Deserialize<GraphicsBackup>(File.ReadAllText(path),LocalStore.Json);if(backup!=null)result.Add(backup);}catch(Exception ex)when(ex is IOException or JsonException){}}
  return result.OrderByDescending(b=>b.Created).ToList();
 }
 public GraphicsBackup? Active(GraphicsTarget target)=>Backups().FirstOrDefault(b=>b.Target==target);
 public bool ProfileMatches(GraphicsBackup backup,bool disableVsync)
 {
  Validate(backup.Target);
  var current=Decode(File.ReadAllBytes(backup.Target.Path)).Text;
  var (_,changes,missing)=Patch(backup.Target.Game,backup.Preset,current,disableVsync);
  return changes.Count==0&&!backup.Changes.Any(c=>missing.Contains(c.Key,StringComparer.OrdinalIgnoreCase));
 }
 public async Task<GraphicsBackup> ApplyAsync(GraphicsPlan plan)
 {
  if(!ReferenceEquals(plan,issued))throw new InvalidDataException("Önce güncel değişiklik planını incele.");issued=null;
  Validate(plan.Target);RequireClosed(plan.Target.Game);if(Active(plan.Target)!=null)throw new InvalidOperationException("Önce bu dosyanın etkin görüntü profilini geri al.");
  if(plan.Changes.Count==0)throw new InvalidOperationException("Seçilen görüntü ayarları zaten etkin.");
  if(Hash(File.ReadAllBytes(plan.Target.Path))!=plan.BeforeHash)throw new IOException("Oyun ayarı önizlemeden sonra değişti. Yeniden incele.");
  Directory.CreateDirectory(store);PackageSafety.RejectReparsePoints(store,store);var backup=new GraphicsBackup(plan.Id,DateTimeOffset.Now,plan.Target,plan.Preset,plan.BeforeHash,Hash(plan.Updated),Convert.ToBase64String(plan.Original),plan.Changes.ToList());
  var state=Path.Combine(store,plan.Id.ToString("N")+".json");await File.WriteAllTextAsync(state,JsonSerializer.Serialize(backup,LocalStore.Json));
  try{RequireClosed(plan.Target.Game);WriteChecked(plan.Target,plan.BeforeHash,plan.Updated);}catch{if(Hash(File.ReadAllBytes(plan.Target.Path))==plan.BeforeHash)File.Delete(state);throw;}
  Journal.Write("Oyun görüntü profili",plan.Target.Label+" / "+plan.Changes.Count+" ayar; yedek: "+state);return backup;
 }
 private static void WriteChecked(GraphicsTarget target,string expected,byte[] updated)
 {
  var path=PackageSafety.ResolveInside(target.Root,Path.GetRelativePath(target.Root,target.Path));
  using var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);using var memory=new MemoryStream();locked.CopyTo(memory);
  if(Hash(memory.ToArray())!=expected)throw new IOException("Oyun ayarı değişti; dosyaya yazılmadı.");
  var original=memory.ToArray();try{locked.Position=0;locked.Write(updated);locked.SetLength(updated.Length);locked.Flush(true);}catch{locked.Position=0;locked.Write(original);locked.SetLength(original.Length);locked.Flush(true);throw;}
 }
 public Task<GraphicsRestoreResult> RestoreAsync(GraphicsBackup backup)
 {
  if(!Backups().Any(b=>b.Id==backup.Id&&b.Target==backup.Target&&b.OriginalHash==backup.OriginalHash&&b.AppliedHash==backup.AppliedHash&&b.Changes.SequenceEqual(backup.Changes)))throw new InvalidDataException("Yedek doğrulanamadı.");
  Validate(backup.Target);RequireClosed(backup.Target.Game);var original=Convert.FromBase64String(backup.OriginalBase64);if(Hash(original)!=backup.OriginalHash)throw new InvalidDataException("Görüntü yedeğinin bütünlüğü bozuk.");
  var current=File.ReadAllBytes(backup.Target.Path);var currentHash=Hash(current);bool exact=currentHash==backup.AppliedHash;int restored=0,preserved=0;byte[] updated;
  if(exact){updated=original;restored=backup.Changes.Count;}else{
   var document=Decode(current);var text=document.Text;
   foreach(var change in backup.Changes){
    if(backup.Target.Game=="cs2"){
     var pattern=VdfPattern(change.Key);var matches=pattern.Matches(text);if(matches.Count==1&&matches[0].Groups["value"].Value==change.After){text=pattern.Replace(text,m=>m.Groups["prefix"].Value+change.Before+m.Groups["suffix"].Value);restored++;}else preserved++;
    }else{
     var section="";var lines=Regex.Split(text,"(?<=\\n)");bool found=false;
     for(int i=0;i<lines.Length;i++){var trim=lines[i].Trim();if(trim.StartsWith('[')&&trim.EndsWith(']')){section=trim[1..^1];continue;}if(!section.Equals(change.Section,StringComparison.OrdinalIgnoreCase))continue;
      var match=Regex.Match(lines[i],"^(?<prefix>[ \\t]*"+Regex.Escape(change.Key)+"[ \\t]*=[ \\t]*)(?<value>[^;\\r\\n]*?)(?<suffix>[ \\t]*(?:;[^\\r\\n]*)?\\r?\\n?)$",RegexOptions.IgnoreCase);
      if(match.Success&&match.Groups["value"].Value.Trim().Equals(change.After,StringComparison.OrdinalIgnoreCase)){lines[i]=match.Groups["prefix"].Value+change.Before+match.Groups["suffix"].Value;found=true;restored++;break;}
     }
     if(!found)preserved++;text=string.Concat(lines);
    }
   }
   updated=Encode(text,document.Encoding,document.Preamble);
  }
  WriteChecked(backup.Target,currentHash,updated);File.Delete(Path.Combine(store,backup.Id.ToString("N")+".json"));Journal.Write("Oyun görüntü profili geri alındı",backup.Target.Label+" / "+restored+" değer; "+preserved+" dış değişiklik korundu.");return Task.FromResult(new GraphicsRestoreResult(restored,preserved,exact));
 }
}
