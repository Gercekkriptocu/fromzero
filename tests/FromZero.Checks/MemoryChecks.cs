using FromZero;

public static class MemoryChecks
{
 public static async Task Run(Action<bool, string> check)
 {
  const ulong gb = 1073741824;
  var now = DateTimeOffset.UtcNow;
  MemorySnapshot Sample(int seconds, double percent = 90) => new(now.AddSeconds(seconds), 16 * gb, (ulong)(16 * gb * (1 - percent / 100)), 20 * gb, 32 * gb);
  var options = new MemoryOptions(true, 85, true, [@"C:\Apps\browser.exe"]);
  var pressure = new MemoryPressureGate();
  check(!pressure.Ready(Sample(0), options, false) && !pressure.Ready(Sample(10), options, false) && !pressure.Ready(Sample(20), options, false) && pressure.Ready(Sample(30), options, false), "RAM auto requires 30 seconds sustained pressure");
  pressure.Attempted(now.AddSeconds(30));
  for (var time = 40; time < 630; time += 10) checkQuiet(!pressure.Ready(Sample(time), options, false));
  check(pressure.Ready(Sample(630), options, false), "RAM automatic attempts wait at least ten minutes");
  pressure = new(); pressure.Ready(Sample(0), options, false); pressure.Ready(Sample(10), options, false);
  check(!pressure.Ready(Sample(20, 40), options, false) && !pressure.Ready(Sample(30), options, false) && !pressure.Ready(Sample(40), options, false), "RAM pressure resets when usage drops");
  check(!pressure.Ready(Sample(50), options, true) && !pressure.Ready(Sample(60), options, false), "RAM auto pauses and resets during a game or blocked operation");
  pressure = new(); pressure.Ready(Sample(0), options, false);
  check(!pressure.Ready(Sample(300), options, false), "RAM resume after sleep cannot use stale pressure samples");
  check(!new MemoryPressureGate().Ready(Sample(0), options with { Automatic = false }, false) && !new MemoryPressureGate().Ready(Sample(0), options with { Applications = [] }, false), "RAM automatic mode is opt-in and requires selected apps");
  var rejected = false; try { (options with { Threshold = 1 }).Validate(); } catch (System.IO.InvalidDataException) { rejected = true; }
  check(rejected, "RAM settings reject aggressive thresholds");
  rejected = false; try { (options with { Applications = ["browser.exe"] }).Validate(); } catch (System.IO.InvalidDataException) { rejected = true; }
  check(rejected, "RAM app allowlist requires full executable paths");
  var a = new MemoryTarget(10, 100, "browser", @"C:\Apps\browser.exe", gb, true, "Hazır");
  var b = a with { Id = 11, Eligible = false, Reason = "Ön planda" };
  var c = a with { Id = 12, Path = @"C:\Other\browser.exe" };
  check(MemoryPolicy.Select([a, a, b, c], options.Allowed).SequenceEqual([a]), "RAM selection excludes active apps, duplicates and same-name executables outside allowlist");
  check(MemoryPolicy.ProtectedName("cs2", []) && MemoryPolicy.ProtectedName("MsMpEng", []) && MemoryPolicy.ProtectedName("customgame", ["CUSTOMGAME"]) && !MemoryPolicy.ProtectedName("browser", []), "RAM games, anti-virus and configured game processes are protected");
  var backend = new FakeMemoryBackend([a, b]); var optimizer = new MemoryOptimizer(backend);
  var report = await optimizer.OptimizeAsync([a, a, b], "test");
  check(backend.Calls == 1 && report.Items.Count == 1 && report.Items[0].Status == "Uygulandı", "RAM service deduplicates and never sends ineligible targets to backend");
  check(report.AvailableDifference == 128 * 1024 * 1024 && report.WorkingSetDifference == 256 * 1024 * 1024, "RAM physical availability and working-set reduction are measured separately");
  backend = new FakeMemoryBackend([a]) { Skip = true }; optimizer = new(backend);
  report = await optimizer.OptimizeAsync([a], "test");
  check(report.Items[0].Status.Contains("atlandı") && report.WorkingSetDifference == 0, "RAM process exit or identity change is reported as skipped");
  backend = new FakeMemoryBackend([a]); optimizer = new(backend);
  var first = optimizer.OptimizeAsync([a], "first");
  rejected = false; try { await optimizer.OptimizeAsync([a], "second"); } catch (InvalidOperationException) { rejected = true; }
  await first;
  check(rejected && backend.Calls == 1, "RAM overlapping manual and automatic operations are rejected");
  backend = new FakeMemoryBackend([a]) { FailRead = true }; optimizer = new(backend);
  try { await optimizer.OptimizeAsync([a], "fail"); } catch (System.IO.IOException) { }
  backend.FailRead = false;
  check((await optimizer.OptimizeAsync([a], "retry")).Items.Count == 1, "RAM operation lock is released after failure");
  MemoryOptimizer.SaveOptions(options); check(MemoryOptimizer.LoadOptions().Allowed.SequenceEqual(options.Allowed), "RAM opt-in preferences persist in isolated test storage");
  var real = new WindowsMemoryBackend(); var live = real.Read();
  check(live.Total > 0 && live.Available <= live.Total && live.CommitLimit >= live.Commit && live.UsedPercent is >= 0 and <= 100, "RAM native read-only physical and commit counters are valid");
  var apps = real.Applications();
  check(apps.All(p => p.Started > 0 && System.IO.Path.IsPathFullyQualified(p.Path)) && apps.All(p => !MemoryPolicy.ProtectedName(p.Name, []) || !p.Eligible), "RAM native enumeration records stable identities and protects system and game apps without modifying them");
  check(WorldStations.ForPage("memory").Page == "performance", "RAM screen shares the performance station and pixel background");
  void checkQuiet(bool value) { if (!value) throw new Exception("RAM cooldown failed before ten minutes"); }
 }
 private sealed class FakeMemoryBackend(IReadOnlyList<MemoryTarget> targets) : IMemoryBackend
 {
  public int Calls; public bool Skip, FailRead; private int reads;
  public MemorySnapshot Read() { if (FailRead) throw new System.IO.IOException("test failure"); return new(DateTimeOffset.UtcNow, 16UL * 1073741824, 1073741824UL + (ulong)(reads++ % 2) * 128 * 1024 * 1024, 8UL * 1073741824, 32UL * 1073741824); }
  public IReadOnlyList<MemoryTarget> Applications() => targets;
  public MemoryItemResult Trim(MemoryTarget target) { Calls++; return Skip ? new(target.Name, "İşlem değişti; atlandı") : new(target.Name, "Uygulandı", target.WorkingSet, target.WorkingSet - 256 * 1024 * 1024); }
 }
}
