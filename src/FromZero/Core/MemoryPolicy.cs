namespace FromZero;

public record MemorySnapshot(DateTimeOffset Time, ulong Total, ulong Available, ulong Commit, ulong CommitLimit)
{
 public double UsedPercent => Total == 0 ? 0 : 100d * (Total - Math.Min(Total, Available)) / Total;
 public double CommitPercent => CommitLimit == 0 ? 0 : 100d * Commit / CommitLimit;
}
public record MemoryTarget(int Id, long Started, string Name, string Path, ulong WorkingSet, bool Eligible, string Reason);
public record MemoryOptions(bool Automatic = false, int Threshold = 85, bool BeforeGame = false, string[]? Applications = null)
{
 public string[] Allowed => Applications ?? [];
 public void Validate()
 {
  if (Threshold is < 80 or > 95 || Allowed.Length > 100 || Allowed.Any(p => string.IsNullOrWhiteSpace(p) || !System.IO.Path.IsPathFullyQualified(p)))
   throw new System.IO.InvalidDataException("RAM tercihleri geçersiz.");
 }
}
public record MemoryItemResult(string Name, string Status, ulong Before = 0, ulong After = 0)
{
 public long Difference => (long)Before - (long)After;
}
public record MemoryReport(DateTimeOffset Time, string Mode, MemorySnapshot Before, MemorySnapshot After, IReadOnlyList<MemoryItemResult> Items)
{
 public long AvailableDifference => (long)After.Available - (long)Before.Available;
 // Shared pages can appear in several working sets; never label this sum as freed physical RAM.
 public long WorkingSetDifference => Items.Where(i => i.Status == "Uygulandı").Sum(i => i.Difference);
}
public sealed class MemoryPressureGate
{
 private DateTimeOffset? highSince, lastSample, lastAttempt;
 public static TimeSpan Cooldown => TimeSpan.FromMinutes(10);
 public void Attempted(DateTimeOffset now) { lastAttempt = now; highSince = null; }
 public bool Ready(MemorySnapshot snapshot, MemoryOptions options, bool blocked)
 {
  options.Validate();
  var now = snapshot.Time;
  var continuous = lastSample != null && now >= lastSample && now - lastSample <= TimeSpan.FromSeconds(15);
  lastSample = now;
  if (blocked || !options.Automatic || snapshot.UsedPercent < options.Threshold || options.Allowed.Length == 0)
  { highSince = null; return false; }
  if (!continuous) highSince = now;
  highSince ??= now;
  return now - highSince >= TimeSpan.FromSeconds(30) && (lastAttempt == null || now - lastAttempt >= Cooldown);
 }
}
public static class MemoryPolicy
{
 public const ulong MinimumWorkingSet = 32 * 1024 * 1024;
 private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
 {
  "system", "idle", "registry", "memory compression", "smss", "csrss", "wininit", "services", "lsass", "winlogon", "svchost", "dwm", "explorer",
  "msmpeng", "securityhealthservice", "vgc", "vgtray", "faceit", "faceitclient", "faceitservice", "easyanticheat", "easyanticheat_eos", "beservice",
  "cs2", "valorant", "valorant-win64-shipping", "fortniteclient-win64-shipping", "r5apex", "tslgame", "dota2", "gta5", "gta5_enhanced", "cod", "overwatch", "rocketleague", "league of legends", "javaw", "minecraft.windows"
 };
 public static bool ProtectedName(string name, IEnumerable<string> games) => Protected.Contains(name) || games.Contains(name, StringComparer.OrdinalIgnoreCase);
 public static IReadOnlyList<MemoryTarget> Select(IEnumerable<MemoryTarget> candidates, IEnumerable<string> allowed) =>
  candidates.Where(c => c.Eligible && allowed.Contains(c.Path, StringComparer.OrdinalIgnoreCase)).DistinctBy(c => (c.Id, c.Started)).Take(100).ToArray();
}
