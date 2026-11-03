using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;
public record ReleaseManifest(int Schema,string Version,string Runtime,List<CapsuleFile> Files);
public record ReleaseInspection(string Version,int Files,bool IntegrityOk,bool PublisherVerified,bool UpdateEligible,List<string> Findings);
public static class ReleaseIntegrity
{
 public static string? PublisherKey()
 {
  using var stream=typeof(ReleaseIntegrity).Assembly.GetManifestResourceStream("FromZero.release-public-key.pem");
  if(stream==null)return null;
  using var reader=new StreamReader(stream);return reader.ReadToEnd();
 }
 public static bool VerifySignature(byte[] manifest,byte[] signature,string publicKey)
 {
  using var rsa=RSA.Create();rsa.ImportFromPem(publicKey);
  return rsa.KeySize>=2048&&rsa.VerifyData(manifest,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
 }
 public static async Task<ReleaseInspection> InspectAsync(string folder,Version installed,bool candidate=false,string? publicKey=null)
 {
  publicKey??=PublisherKey();
  var path=PackageSafety.ResolveInside(folder,"release-manifest.json");var signature=PackageSafety.ResolveInside(folder,"release-manifest.sig");
  if(!File.Exists(path)||new FileInfo(path).Length>8_000_000)throw new InvalidDataException("Release manifest missing or too large.");
  var bytes=await File.ReadAllBytesAsync(path);var signed=false;List<string> findings=[];
  if(publicKey!=null&&File.Exists(signature)&&new FileInfo(signature).Length<=16_384)
   signed=VerifySignature(bytes,await File.ReadAllBytesAsync(signature),publicKey);
  if(candidate&&!signed)return new("",0,false,false,false,["A matching pinned publisher signature is required to verify an update."]);
  var manifest=JsonSerializer.Deserialize<ReleaseManifest>(bytes,LocalStore.Json)??throw new InvalidDataException("Invalid release manifest.");
  if(manifest.Schema!=1||!Version.TryParse(manifest.Version,out var version)||manifest.Files==null||manifest.Files.Count is <4 or >10_000)throw new InvalidDataException("Unsupported release manifest.");
  var runtime="win-"+RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
  if(manifest.Runtime!=runtime)findings.Add("Release architecture differs from this application.");
  var listed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var file in manifest.Files)
  {
   if(file==null||string.IsNullOrWhiteSpace(file.Path)||file.Path.Contains(':')||file.Size<0||!Regex.IsMatch(file.Sha256??"",@"^[0-9A-Fa-f]{64}$"))throw new InvalidDataException("Invalid release file record.");
   var full=PackageSafety.ResolveInside(folder,file.Path);
   if(!listed.Add(full))throw new InvalidDataException("Duplicate release file path.");
   if(!File.Exists(full)||new FileInfo(full).Length!=file.Size){findings.Add(file.Path+": missing or size changed");continue;}
   var actual=await PackageSafety.HashFileAsync(folder,full);
   if(!actual.Sha256.Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))findings.Add(file.Path+": hash changed");
  }
  foreach(var required in new[]{"FromZero.exe","FromZero.dll","FromZero.deps.json","FromZero.runtimeconfig.json"})
   if(!listed.Contains(Path.Combine(Path.GetFullPath(folder),required)))findings.Add(required+": missing from manifest");
  var folders=new Stack<string>();folders.Push(Path.GetFullPath(folder));var visited=0;
  while(folders.TryPop(out var current))foreach(var entry in Directory.EnumerateFileSystemEntries(current))
  {
   if(++visited>20_000)throw new InvalidDataException("Release folder exceeds the entry limit.");
   var attributes=File.GetAttributes(entry);var relative=Path.GetRelativePath(folder,entry);
   if((attributes&FileAttributes.ReparsePoint)!=0){findings.Add(relative+": linked entry");continue;}
   if((attributes&FileAttributes.Directory)!=0){folders.Push(entry);continue;}
   if(!listed.Contains(entry)&&!entry.Equals(path,StringComparison.OrdinalIgnoreCase)&&!entry.Equals(signature,StringComparison.OrdinalIgnoreCase))findings.Add(relative+": unlisted file");
  }
  var intact=findings.Count==0;
  if(!signed)findings.Add(publicKey==null?"No publisher signing key configured. File hashes check package consistency.":"Publisher signature is missing or invalid.");
  if(candidate&&version<=installed)findings.Add("Only a newer version is eligible.");
  return new(manifest.Version,manifest.Files.Count,intact,signed,candidate&&intact&&signed&&version>installed,findings);
 }
}
