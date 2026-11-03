using System.IO;
using System.Text.Json;
namespace FromZero;
public record DeviceInfo(string Name, string Id, string Class, int Problem, string[] HardwareIds, bool Present=true, string[]? CompatibleIds=null);
public record DriverInfo(string Name, string Version, string Provider, string Inf, string DeviceId, string Date, bool IsSigned=false);
public record StartupEntry(string Name, string Command, string Location, bool Disabled, bool Legacy=false);
public record ProcessInfo(int Id, string Name, long Memory);
public record JournalEntry(DateTimeOffset Time, string Action, string Detail, bool Success);
public record CapsuleFile(string Path, long Size, string Sha256);
public record CapsuleManifest(int Schema, DateTimeOffset Created, string Machine, string Architecture, string Windows, List<CapsuleFile> Files, List<DeviceInfo> Devices);
public record UpdateInfo(string Id, int Revision, string Title, string Manufacturer, string Model, string Eula = "",string Class="")
{
 public bool IsFirmware=>Class.Contains("Firmware",StringComparison.OrdinalIgnoreCase)||Model.Contains("Firmware",StringComparison.OrdinalIgnoreCase)||Title.Contains("Firmware",StringComparison.OrdinalIgnoreCase)||Title.Contains("BIOS",StringComparison.OrdinalIgnoreCase);
}
public record CommandResult(int ExitCode, string Output) { public bool Success => ExitCode is 0 or 3010; }
public static class LocalStore
{
 #if CHECKS
 public static string Root { get; } = (string?)AppContext.GetData("FromZero.TestData") ?? throw new InvalidOperationException("Test veri klasörü ayarlanmadı.");
 #else
 public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FromZero");
 #endif
 public static JsonSerializerOptions Json { get; } = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
 public static string FilePath(string name) { Directory.CreateDirectory(Root); return Path.Combine(Root, name); }
}
public static class Journal
{
 private static readonly object Gate = new();
 public static void Write(string action, string detail, bool success = true)
 {
  lock (Gate) { try { File.AppendAllText(LocalStore.FilePath("history.jsonl"), JsonSerializer.Serialize(new JournalEntry(DateTimeOffset.Now, action, detail, success)) + Environment.NewLine); } catch (IOException) { } }
 }
 public static List<JournalEntry> Read()
 {
  lock (Gate) {
   var path = LocalStore.FilePath("history.jsonl");
   if (!File.Exists(path)) return [];
   return File.ReadLines(path).TakeLast(200).Select(line => { try { return JsonSerializer.Deserialize<JournalEntry>(line); } catch (JsonException) { return null; } }).OfType<JournalEntry>().Reverse().ToList();
  }
 }
}
