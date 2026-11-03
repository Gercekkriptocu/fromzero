using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record OfflineDriverEntry(string Inf,string Catalog,string InfHash,string CatalogHash,string Class,string DriverVersion,string[] Architectures,string[] HardwareIds,string[] Devices,string Publisher,string Warning);
public record OfflineDriverIndex(int Schema,DateTimeOffset Created,string Architecture,int WindowsBuild,List<OfflineDriverEntry> Drivers,string[] NetworkHardwareIds,List<string> Warnings);
public static class OfflineDriverCatalog
{
 public static string[] Architectures(string text){var list=new List<string>();foreach(var (term,arch) in new[]{("NTamd64","X64"),("NTarm64","Arm64"),("NTx86","X86")})if(Regex.IsMatch(text,@"\b"+term+@"(?:\b|\.)",RegexOptions.IgnoreCase))list.Add(arch);return list.ToArray();}
 public static bool Matches(OfflineDriverEntry entry,IEnumerable<DeviceInfo> devices)=>devices.Any(d=>d.Present&&d.HardwareIds.Any(id=>entry.HardwareIds.Contains(id,StringComparer.OrdinalIgnoreCase)));
 public static async Task<OfflineDriverIndex> BuildAsync(string root,IReadOnlyList<DeviceInfo> devices,Func<string,Task<string>>? signer=null)
 {
  signer??=DriverService.SignatureAsync;List<OfflineDriverEntry> rows=[];List<string> warnings=[];var signatures=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  var files=PackageSafety.EnumerateSafeFiles(root).Where(p=>Path.GetExtension(p).Equals(".inf",StringComparison.OrdinalIgnoreCase)).Take(5001).ToArray();if(files.Length>5000)throw new InvalidDataException("More than 5000 INF packages; choose a smaller driver folder.");
  foreach(var path in files){string warning="",publisher="",cat="",catHash="";if(new FileInfo(path).Length>8_000_000){warnings.Add(Path.GetRelativePath(root,path)+": INF too large");continue;}
   var text=PackageSafety.InfText(path);var ids=PackageSafety.ReadHardwareIds(path).Order(StringComparer.OrdinalIgnoreCase).ToArray();string Field(string field)=>Regex.Match(text,@"(?im)^\s*"+field+@"\s*=\s*([^;\r\n]+)").Groups[1].Value.Trim();
   try{var catalog=PackageSafety.ReadCatalog(path)??throw new InvalidDataException("No catalogue for this architecture.");cat=Path.GetRelativePath(root,catalog);PackageSafety.ResolveInside(root,cat);if(!File.Exists(catalog))throw new IOException("Catalogue file missing.");if(!signatures.TryGetValue(catalog,out publisher!)){publisher=await signer(catalog);signatures[catalog]=publisher;}catHash=(await PackageSafety.HashFileAsync(root,catalog)).Sha256;}
   catch(Exception ex)when(ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException){warning=ex.Message;}
   var matches=devices.Where(d=>d.Present&&d.HardwareIds.Any(id=>ids.Contains(id,StringComparer.OrdinalIgnoreCase))).Select(d=>d.Name).Distinct().ToArray();
   rows.Add(new(Path.GetRelativePath(root,path),cat,(await PackageSafety.HashFileAsync(root,path)).Sha256,catHash,Field("Class"),Field("DriverVer"),Architectures(text),ids,matches,publisher,warning));
  }
  return new(1,DateTimeOffset.Now,RuntimeInformation.OSArchitecture.ToString(),Environment.OSVersion.Version.Build,rows.OrderByDescending(r=>r.Class.Equals("Net",StringComparison.OrdinalIgnoreCase)).ThenBy(r=>r.Inf).ToList(),devices.Where(d=>d.Present&&d.Class.Equals("Net",StringComparison.OrdinalIgnoreCase)).SelectMany(d=>d.HardwareIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),warnings);
 }
 public static OfflineDriverIndex? Read(string root)
 {
  var path=PackageSafety.ResolveInside(root,"driver-index.json");if(!File.Exists(path))return null;if(new FileInfo(path).Length>12_000_000)throw new InvalidDataException("Driver index too large.");var index=JsonSerializer.Deserialize<OfflineDriverIndex>(File.ReadAllText(path),LocalStore.Json)??throw new InvalidDataException("Invalid driver index.");if(index.Schema!=1||index.Drivers==null||index.NetworkHardwareIds==null||index.Drivers.Count>5000||index.Architecture is not("X64" or "Arm64" or "X86")||index.Drivers.Any(e=>e==null||e.HardwareIds==null||e.Devices==null||e.Class==null||e.Warning==null||string.IsNullOrWhiteSpace(e.Inf)))throw new InvalidDataException("Unsupported driver index.");return index;
 }
 public static async Task<List<string>> VerifyCapsuleAsync(string root)
 {
  if(File.Exists(PackageSafety.ResolveInside(root,"INCOMPLETE.txt")))return ["The USB export is incomplete."];
  var path=PackageSafety.ResolveInside(root,"capsule.json");if(!File.Exists(path))return [];
  if(new FileInfo(path).Length>12_000_000)throw new InvalidDataException("USB manifest too large.");
  var manifest=JsonSerializer.Deserialize<CapsuleManifest>(await File.ReadAllTextAsync(path),LocalStore.Json)??throw new InvalidDataException("Invalid USB manifest.");
  if(manifest.Files==null||manifest.Files.Count is <1 or >50_000)throw new InvalidDataException("USB manifest file count is invalid.");
  if(manifest.Architecture!=RuntimeInformation.OSArchitecture.ToString())return ["USB architecture differs from this PC."];
  return await PackageSafety.VerifyAsync(root,manifest);
 }
 public static async Task<List<string>> VerifyAsync(string root,OfflineDriverIndex index,IReadOnlyList<DeviceInfo> devices,Func<string,Task<string>>? signer=null)
 {
  signer??=DriverService.SignatureAsync;List<string> errors=[];if(!index.Architecture.Equals(RuntimeInformation.OSArchitecture.ToString(),StringComparison.OrdinalIgnoreCase))errors.Add("USB architecture differs from this PC.");
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var entry in index.Drivers.Where(e=>Matches(e,devices))){
   try{var inf=PackageSafety.ResolveInside(root,entry.Inf);if(!seen.Add(inf))throw new InvalidDataException("Duplicate INF entry.");var hash=await PackageSafety.HashFileAsync(root,inf);if(hash.Sha256!=entry.InfHash)throw new InvalidDataException("INF content changed.");var liveIds=PackageSafety.ReadHardwareIds(inf);if(!devices.Any(d=>d.Present&&d.HardwareIds.Any(liveIds.Contains)))throw new InvalidDataException("Actual hardware IDs no longer match.");var actual=PackageSafety.ReadCatalog(inf)??throw new InvalidDataException("Architecture catalogue missing.");if(!Path.GetFullPath(actual).Equals(PackageSafety.ResolveInside(root,entry.Catalog),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Catalogue path changed.");if((await PackageSafety.HashFileAsync(root,actual)).Sha256!=entry.CatalogHash)throw new InvalidDataException("Catalogue content changed.");await signer(actual);}
   catch(Exception ex)when(ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException){errors.Add(entry.Inf+": "+ex.Message);}
  }return errors;
 }
 public static string Describe(OfflineDriverIndex index,IReadOnlyList<DeviceInfo> devices)
 {
  var matching=index.Drivers.Where(e=>Matches(e,devices)).ToArray();var networks=matching.Where(e=>e.Class.Equals("Net",StringComparison.OrdinalIgnoreCase)).ToArray();return $"Created: {index.Created:g}\nArchitecture: {index.Architecture} / exported Windows build {index.WindowsBuild}\nPackages: {index.Drivers.Count} · matching: {matching.Length} · matching network: {networks.Length}\n\n"+string.Join("\n\n",matching.Take(100).Select(e=>$"{e.Class} · {e.Inf}\n{e.DriverVersion}\n{string.Join(", ",e.Devices)}\n{(e.Warning.Length==0?e.Publisher:e.Warning)}"))+"\n\nWindows validates final package integrity, OS decorations and driver rank. A matching backup does not guarantee every missing device is covered. SHA-256 detects changes; it does not authenticate the USB creator.";
 }
}
