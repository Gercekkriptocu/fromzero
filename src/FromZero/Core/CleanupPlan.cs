using System.IO;
namespace FromZero;
public record CleanupFile(string Path, long Size, DateTime LastWrite);
public static class CleanupPlan
{
 public static List<CleanupFile> Scan(string root, DateTime olderThan)
 {
  PackageSafety.RejectReparsePoints(root, root);
  var result = new List<CleanupFile>();
  var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System };
  foreach (var path in Directory.EnumerateFiles(root, "*", options)) {
   try {
    var file = new FileInfo(path);
    if (file.LastWriteTimeUtc < olderThan.ToUniversalTime() && file.CreationTimeUtc < olderThan.ToUniversalTime()) result.Add(new(file.FullName, file.Length, file.LastWriteTimeUtc));
   } catch (IOException) { } catch (UnauthorizedAccessException) { }
  }
  return result;
 }
 public static (long Bytes, int Count, int Skipped) Execute(string root, IEnumerable<CleanupFile> plan, DateTime olderThan)
 {
  long bytes = 0; int count = 0, skipped = 0;
  foreach (var file in plan) {
   try {
    var relative = Path.GetRelativePath(root, file.Path);
    var full = PackageSafety.ResolveInside(root, relative);
    var current = new FileInfo(full);
    if (!current.Exists || current.LastWriteTimeUtc != file.LastWrite || current.Length != file.Size || current.LastWriteTimeUtc >= olderThan.ToUniversalTime() || current.CreationTimeUtc >= olderThan.ToUniversalTime() || (current.Attributes & FileAttributes.System) != 0) { skipped++; continue; }
    File.Delete(full); bytes += file.Size; count++;
   } catch (IOException) { skipped++; } catch (InvalidDataException) { skipped++; } catch (UnauthorizedAccessException) { skipped++; }
  }
  return (bytes, count, skipped);
 }
}
