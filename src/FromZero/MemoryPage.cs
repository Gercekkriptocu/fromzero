using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace FromZero;
public partial class MainWindow
{
 private readonly MemoryOptimizer memoryOptimizer = new();
 private readonly MemoryPressureGate memoryPressure = new();
 private MemoryOptions memoryOptions = MemoryOptimizer.LoadOptions();
 private IReadOnlyList<MemoryTarget> memoryTargets = [];
 private readonly HashSet<(int Id, long Started)> memorySelection = [];
 private MemoryReport? memoryReport;
 private TextBlock? memoryUsage, memoryAvailable, memoryCommit, memoryHint;
 private ProgressBar? memoryUsageBar;
 private bool memoryPolling;
 private DateTimeOffset lastMemoryPoll;

 private async Task OpenMemoryAsync()
 {
  Navigate("memory");
  await RefreshMemoryApplicationsAsync();
 }
 private async Task RefreshMemoryApplicationsAsync()
 {
  memoryTargets = await Task.Run(memoryOptimizer.Applications);
  memorySelection.RemoveWhere(key => !memoryTargets.Any(p => (p.Id, p.Started) == key && p.Eligible));
  foreach (var target in memoryTargets.Where(p => p.Eligible && (p.Id == Environment.ProcessId || memoryOptions.Allowed.Contains(p.Path, StringComparer.OrdinalIgnoreCase)))) memorySelection.Add((target.Id, target.Started));
  if (currentPage == "memory") Navigate("memory");
 }
 private void Memory()
 {
  Header("RAM / BELLEK", "Belleğine yer aç.", "Canlı kullanım, seçili uygulamalarda RAM boşaltma ve bellek baskısına göre otomatik bakım.");
  var tiles = new ResponsiveTilePanel { MaxColumns = 3, MinimumTileWidth = 200 };
  memoryUsage = T("—", 24, "#B9E2A7", true); memoryAvailable = T("—", 24, "#B9E2A7", true); memoryCommit = T("—", 22, "#D8E8D3", true);
  memoryUsageBar = new ProgressBar { Maximum = 100 };
  foreach (var tile in new[] { Card(Stack(T("RAM KULLANIMI", 11, "#9AAFAE"), memoryUsage, memoryUsageBar)), Card(Stack(T("KULLANILABİLİR RAM", 11, "#9AAFAE"), memoryAvailable)), Card(Stack(T("TAAHHÜT / SINIR", 11, "#9AAFAE"), memoryCommit, T("RAM ve disk destekli bellek bütçesi", 11, "#9AAFAE"))) }) { tile.Margin = new Thickness(0); tiles.Children.Add(tile); }
  Page.Children.Add(tiles);
  memoryHint = T("", 13, "#B9CBC1"); memoryHint.Margin = new Thickness(0, 14, 0, 12); Page.Children.Add(memoryHint);
  Page.Children.Add(Card(Stack(T("RAM boşaltıcı", 21, "#B9E2A7", true), T("FromZero ve aşağıda seçtiğin küçültülmüş uygulamaların çalışma belleği boşaltılır. Uygulamalar açık kalır; ihtiyaç duydukları veriyi tekrar yükleyebilirler. İşlem sonrası ilk geçiş yavaşlayabilir; FPS artışı garantilemez.", 13, "#B9CBC1"),
   Row(Action("RAM boşalt", () => OptimizeMemoryAsync(false), true), Action("Uygulamaları tara", RefreshMemoryApplicationsAsync), Action("Görev Yöneticisi", () => { Commands.Open("taskmgr.exe"); return Task.CompletedTask; })))));
  if (memoryReport != null)
  {
   var report = memoryReport;
   Page.Children.Add(Card(Stack(T("Son işlem • " + report.Time.ToLocalTime().ToString("HH:mm:ss"), 17, "#B9E2A7", true),
    T($"Kullanılabilir RAM: {MemorySize(report.Before.Available)} → {MemorySize(report.After.Available)}  ({MemoryDelta(report.AvailableDifference)})", 14),
    T($"{report.Items.Count(i => i.Status == "Uygulandı")} uygulamaya uygulandı • Çalışma kümesi değişimi: {MemoryDelta(-report.WorkingSetDifference)}", 12, "#B9CBC1"),
    T("Toplam RAM farkı diğer uygulamaların çalışmasından da etkilenir. Çalışma kümesi toplamı, paylaşılan sayfalar nedeniyle fiziksel RAM kazancı değildir.", 11, "#9AAFAE"),
    new Expander { Header = "İşlem ayrıntıları", Foreground = B("#B9CBC1"), Content = Stack(report.Items.Select(i => (UIElement)T(i.Name + " • " + i.Status + (i.Status == "Uygulandı" ? $" • {MemorySize(i.Before)} → {MemorySize(i.After)}" : ""), 12)).ToArray()) })));
  }
  var auto = new CheckBox { Content = "Bellek baskısında otomatik boşalt", IsChecked = memoryOptions.Automatic };
  var beforeGame = new CheckBox { Content = "FromZero ile oyun başlatmadan önce seçili uygulamalarda boşalt", IsChecked = memoryOptions.BeforeGame };
  var threshold = new ComboBox { MinWidth = 130, ItemsSource = new[] { 80, 85, 90, 95 }, SelectedItem = memoryOptions.Threshold };
  var notice = T(memoryOptions.Allowed.Length + " uygulama otomatik bakım için seçili.", 12, "#9AAFAE");
  var preferences = Stack(auto, Row(T("RAM eşiği (%)", 12), threshold), beforeGame,
   T("Eşik 30 saniye boyunca aşılırsa, seçili ve o anda küçültülmüş uygulamalara uygulanır. İşlemler arasında en az 10 dakika bekler. Bilinen oyunlar veya FromZero oyun oturumu açıkken otomatik bakım durur. Yalnızca FromZero açıkken çalışır.", 12, "#9AAFAE"), notice);
  preferences.Children.Add(Row(Action("Tercihleri kaydet", () =>
  {
   var paths = memoryTargets.Where(p => memorySelection.Contains((p.Id, p.Started))).Select(p => p.Path).Concat(memoryOptions.Allowed.Where(path => !memoryTargets.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   memoryOptions = new(auto.IsChecked == true, threshold.SelectedItem is int value ? value : 85, beforeGame.IsChecked == true, paths);
   MemoryOptimizer.SaveOptions(memoryOptions); memoryPressure.Attempted(DateTimeOffset.Now - MemoryPressureGate.Cooldown);
   Navigate("memory"); return Task.CompletedTask;
  }, true), Action("Otomatik uygulama listesini sıfırla", () =>
  {
   memoryOptions = memoryOptions with { Automatic = false, BeforeGame = false, Applications = [] }; MemoryOptimizer.SaveOptions(memoryOptions); memorySelection.Clear(); Navigate("memory"); return Task.CompletedTask;
  })));
  Page.Children.Add(new Expander { Header = "Otomatik bakım ve oyun öncesi ayarlar", Foreground = B("#B9CBC1"), Content = Card(preferences), Margin = new Thickness(0, 0, 0, 16) });
  Section("Uygulamaları seç");
  Page.Children.Add(T("Sistem, güvenlik ve bilinen oyun işlemleri korunur. Diğer uygulamalar için önce pencereyi küçült, sonra listeyi yenile. Seçim otomatik bakıma yalnızca Tercihleri kaydet ile eklenir.", 12, "#9AAFAE"));
  var checks = new List<(MemoryTarget Target, CheckBox Check)>();
  Page.Children.Add(Row(Action("Uygun küçültülmüşleri seç", () => { foreach (var entry in checks.Where(c => c.Target.Eligible)) entry.Check.IsChecked = true; return Task.CompletedTask; }), Action("Seçimi kaldır", () => { foreach (var entry in checks) entry.Check.IsChecked = false; return Task.CompletedTask; })));
  var list = new StackPanel();
  foreach (var target in memoryTargets.Take(80))
  {
   var p = target;
   var box = new CheckBox { Content = AppIcons.Heading(p.Name,p.Path,T($"{p.Name}  •  {MemorySize(p.WorkingSet)}  •  {p.Reason}", 13, p.Eligible ? "#D8E8D3" : "#9AAFAE")), IsEnabled = p.Eligible, IsChecked = memorySelection.Contains((p.Id, p.Started)), ToolTip = p.Path };
   box.Checked += (_, _) => memorySelection.Add((p.Id, p.Started)); box.Unchecked += (_, _) => memorySelection.Remove((p.Id, p.Started));
   checks.Add((p, box)); list.Children.Add(box);
  }
  if (memoryTargets.Count == 0) list.Children.Add(T("Uygulamaları tara ile bu Windows kullanıcısının açık pencerelerini listele.", 13, "#9AAFAE"));
  Page.Children.Add(Card(new ScrollViewer { Content = list, MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }));
  Page.Children.Add(Row(Action("← Performans", () => { Navigate("performance"); return Task.CompletedTask; })));
  RefreshMemoryReadout(memoryOptimizer.Read());
 }
 private static string MemorySize(ulong value) => value >= 1073741824 ? $"{value / 1073741824d:0.00} GB" : $"{value / 1048576d:0} MB";
 private static string MemoryDelta(long value) => (value < 0 ? "−" : "+") + MemorySize((ulong)Math.Abs(value));
 private void RefreshMemoryReadout(MemorySnapshot snapshot)
 {
  if (currentPage != "memory") return;
  if (memoryUsage != null) memoryUsage.Text = $"%{snapshot.UsedPercent:0} • {MemorySize(snapshot.Total)}";
  if (memoryUsageBar != null) memoryUsageBar.Value = snapshot.UsedPercent;
  if (memoryAvailable != null) memoryAvailable.Text = MemorySize(snapshot.Available);
  if (memoryCommit != null) memoryCommit.Text = $"{MemorySize(snapshot.Commit)} / {MemorySize(snapshot.CommitLimit)}";
  if (memoryHint != null) memoryHint.Text = snapshot.UsedPercent >= memoryOptions.Threshold ? "Bellek kullanımı yüksek. Gereksiz uygulamaları kapatmak daha kalıcı yer açar." : "Bellekte yeterli alan var. Şu anda boşaltma gerekli görünmüyor.";
 }
 private async Task OptimizeMemoryAsync(bool automatic, bool beforeGame = false)
 {
  var targets = await Task.Run(memoryOptimizer.Applications);
  var chosen = automatic || beforeGame ? MemoryPolicy.Select(targets, memoryOptions.Allowed) : targets.Where(p => p.Eligible && memorySelection.Contains((p.Id, p.Started))).ToArray();
  if (chosen.Count == 0)
  {
   if (!automatic && !beforeGame) throw new InvalidOperationException("Uygulamaları tara ve uygun bir uygulama seç. Diğer uygulamalar önce küçültülmelidir.");
   return;
  }
  memoryPressure.Attempted(DateTimeOffset.Now);
  memoryReport = await memoryOptimizer.OptimizeAsync(chosen, automatic ? "Otomatik" : beforeGame ? "Oyun öncesi" : "Elle");
  Journal.Write("RAM boşaltıcı / " + memoryReport.Mode, $"{memoryReport.Items.Count(i => i.Status == "Uygulandı")} uygulama; kullanılabilir RAM {MemorySize(memoryReport.Before.Available)} → {MemorySize(memoryReport.After.Available)}.");
  var path = LocalStore.FilePath("memory-last-result.json"); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(memoryReport, LocalStore.Json)); File.Move(path + ".tmp", path, true);
  if (currentPage == "memory") await RefreshMemoryApplicationsAsync();
 }
 private static bool KnownGameRunning()
 {
  var names = new HashSet<string>(GameService.ReadProfiles().Select(p => p.ProcessName), StringComparer.OrdinalIgnoreCase);
  names.UnionWith(["cs2", "valorant", "valorant-win64-shipping", "fortniteclient-win64-shipping", "r5apex", "tslgame", "dota2", "gta5", "gta5_enhanced", "cod", "overwatch", "rocketleague", "league of legends", "javaw", "minecraft.windows"]);
  var running = false;
  foreach (var process in Process.GetProcesses()) using (process) { try { if (names.Contains(process.ProcessName)) running = true; } catch (InvalidOperationException) { } }
  return running;
 }
 private async Task MemoryTickAsync()
 {
  if (memoryPolling || busy || !IsVisible || (currentPage != "memory" && !memoryOptions.Automatic)) return;
  memoryPolling = true;
  try
  {
   var snapshot = memoryOptimizer.Read(); RefreshMemoryReadout(snapshot);
   if (!memoryOptions.Automatic) return;
   if (snapshot.Time - lastMemoryPoll < TimeSpan.FromSeconds(10)) return;
   lastMemoryPoll = snapshot.Time;
   var blocked = gameService.Session != null || await Task.Run(KnownGameRunning);
   if (busy || !IsVisible || !memoryPressure.Ready(snapshot, memoryOptions, blocked)) return;
   // Consume an attempt even if all selected applications are currently ineligible.
   memoryPressure.Attempted(snapshot.Time);
   await RunAsync("Otomatik RAM bakımı", () => OptimizeMemoryAsync(true));
  }
  catch (Exception ex) { memoryPressure.Attempted(DateTimeOffset.Now); Journal.Write("RAM izleme", ex.Message, false); }
  finally { memoryPolling = false; }
 }
 private async Task PrepareGameMemoryAsync()
 {
  if (!memoryOptions.BeforeGame || gameService.Session != null || await Task.Run(KnownGameRunning)) return;
  try { await OptimizeMemoryAsync(false, true); }
  catch (Exception ex) { Journal.Write("Oyun öncesi RAM", ex.Message, false); }
 }
}
