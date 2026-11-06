using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace FromZero;
public static class PackageSafety
{
 public static string InfText(string path)
 {
  if(new FileInfo(path).Length>8_000_000)throw new InvalidDataException("INF exceeds the size limit.");
  var bytes=File.ReadAllBytes(path);
  var encoding=bytes.Length>1&&bytes[0]==0xff&&bytes[1]==0xfe?Encoding.Unicode:bytes.Length>1&&bytes[0]==0xfe&&bytes[1]==0xff?Encoding.BigEndianUnicode:Encoding.UTF8;
  return encoding.GetString(bytes).TrimStart('\uFEFF');
 }
 public static string QuotePs(string value) => "'" + value.Replace("'", "''") + "'";
 public static string ResolveInside(string root, string relative)
 {
  if (Path.IsPathRooted(relative)) throw new InvalidDataException("Paket yolu göreli olmalı.");
  var normalized = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
  var full = Path.GetFullPath(Path.Combine(normalized, relative));
  if (!full.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Paket dışında dosya yolu reddedildi.");
  RejectReparsePoints(normalized.TrimEnd(Path.DirectorySeparatorChar), full);
  return full;
 }
 public static void RejectReparsePoints(string root, string path)
 {
  var current = new DirectoryInfo(Path.GetFullPath(root));
  for (var ancestor = current; ancestor != null; ancestor = ancestor.Parent)
   if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Bağlantılı klasörler desteklenmiyor.");
  var rel = Path.GetRelativePath(current.FullName, path);
  foreach (var part in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) {
   var next = Path.Combine(current.FullName, part);
   if ((File.Exists(next) || Directory.Exists(next)) && (File.GetAttributes(next) & FileAttributes.ReparsePoint) != 0) throw new IOException("Bağlantılı dosya veya klasör reddedildi.");
   current = new DirectoryInfo(next);
  }
 }
 public static IEnumerable<string> EnumerateSafeFiles(string root)
 {
  RejectReparsePoints(root, root);
  var stack = new Stack<string>(); stack.Push(root);
  while (stack.Count > 0) {
   var dir = stack.Pop();
   foreach (var item in Directory.EnumerateFileSystemEntries(dir)) {
    var attr = File.GetAttributes(item);
    if ((attr & FileAttributes.ReparsePoint) != 0) continue;
    if ((attr & FileAttributes.Directory) != 0) stack.Push(item); else yield return item;
   }
  }
 }
 public static HashSet<string> ReadHardwareIds(string inf)
 {
  var text = InfText(inf);
  text = Regex.Replace(text, @";[^\r\n]*", "");
  return Regex.Matches(text, @"(?:PCI|USB|HID|ACPI|HDAUDIO|BTH|SD|ROOT|SWD)\\[A-Z0-9_&.\-\\]+",
   RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(2))
   .Select(m => m.Value.TrimEnd('\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
 }
 public static string? ReadCatalog(string inf)
 {
  var text = InfText(inf);
  var section = "";
  var catalogs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
  foreach (var raw in text.Split('\n')) {
   var line = raw.Split(';')[0].Trim();
   if (line.StartsWith('[')) { section = line; continue; }
   if (!section.Equals("[Version]", StringComparison.OrdinalIgnoreCase)) continue;
   var pair = line.Split('=', 2);
   if (pair.Length == 2 && pair[0].Trim().StartsWith("CatalogFile", StringComparison.OrdinalIgnoreCase)) catalogs[pair[0].Trim()] = pair[1].Trim().Trim('"');
  }
  var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "NTarm64" : "NTamd64";
  var name = catalogs.GetValueOrDefault("CatalogFile." + arch) ?? catalogs.GetValueOrDefault("CatalogFile.NT") ?? catalogs.GetValueOrDefault("CatalogFile");
  return name is null ? null : ResolveInside(Path.GetDirectoryName(inf)!, name);
 }
 public static async Task<CapsuleFile> HashFileAsync(string root, string path)
 {
  await using var stream = File.OpenRead(path);
  var digest = await SHA256.HashDataAsync(stream);
  return new(Path.GetRelativePath(root, path), stream.Length, Convert.ToHexString(digest));
 }
 public static async Task<List<string>> VerifyAsync(string root, CapsuleManifest manifest)
 {
  if (manifest.Schema != 1) throw new InvalidDataException("Desteklenmeyen kapsül sürümü.");
  var errors = new List<string>();
  var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach (var entry in manifest.Files) {
   var path = ResolveInside(root, entry.Path);
   if (!seen.Add(path)) throw new InvalidDataException("Tekrarlanan paket yolu.");
   if (!File.Exists(path)) { errors.Add(entry.Path + ": eksik"); continue; }
   var actual = await HashFileAsync(root, path);
   if (actual.Size != entry.Size || !actual.Sha256.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) errors.Add(entry.Path + ": içerik değişmiş");
  }
  return errors;
 }
}
