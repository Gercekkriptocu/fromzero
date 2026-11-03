using System.Windows;
using System.Windows.Controls;

namespace FromZero;
public partial class MainWindow
{
 private readonly DnsService dnsService = new();
 private IReadOnlyList<DnsAdapter> dnsAdapters = [];
 private string? dnsScanError;
 private async Task<IReadOnlyList<DnsAdapter>> ReadDnsSafeAsync()
 {
  try { var result = await dnsService.ReadAsync(); dnsScanError = null; return result; }
  catch (Exception ex) { dnsScanError = ex.Message; return []; }
 }
 private void DnsCard()
 {
  var adapterPicker = new ComboBox { ItemsSource = dnsAdapters, DisplayMemberPath = "Name", SelectedItem = dnsAdapters.FirstOrDefault(a => a.Connected) ?? dnsAdapters.FirstOrDefault(), MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Stretch };
  System.Windows.Automation.AutomationProperties.SetName(adapterPicker, "DNS değiştirilecek adaptör");
  var current = T("", 12, "#B9CBC1");
  void ShowCurrent()
  {
   current.Text = adapterPicker.SelectedItem is DnsAdapter a ? string.Join("\n", a.Families.Select(f => f.Family + " • " + (f.Automatic ? "Otomatik" : "Özel") + " • " + (f.Servers.Length == 0 ? "DNS adresi yok" : string.Join(", ", f.Servers)))) : dnsScanError == null ? "Uygun adaptör bulunamadı. Listeyi yenile." : "DNS okunamadı: " + dnsScanError;
  }
  adapterPicker.SelectionChanged += (_, _) => ShowCurrent(); ShowCurrent();
  var buttons = Row();
  foreach (var profile in DnsPolicy.Profiles)
  {
   var p = profile; var button = Action(p.Name, async () =>
   {
    var adapter = adapterPicker.SelectedItem as DnsAdapter ?? throw new InvalidOperationException("DNS değiştirilecek adaptörü seç.");
    var result = await dnsService.ApplyAsync(adapter.Id, p.Id);
    dnsAdapters = await ReadDnsSafeAsync(); Navigate("network"); StatusLabel.Text = result; operationCompletion = result;
   }, p.Id == "cloudflare");
   button.IsEnabled = dnsAdapters.Count > 0; buttons.Children.Add(button);
  }
  var restore = Action("Önceki DNS'e dön", async () =>
  {
   var adapter = adapterPicker.SelectedItem as DnsAdapter ?? throw new InvalidOperationException("Bir adaptör seç.");
   var result = await dnsService.RestoreAsync(adapter.Id);
   dnsAdapters = await ReadDnsSafeAsync(); Navigate("network"); operationCompletion = result;
  });
  void RestoreState() => restore.IsEnabled = adapterPicker.SelectedItem is DnsAdapter a && dnsService.HasBackup(a.Id);
  adapterPicker.SelectionChanged += (_, _) => RestoreState(); RestoreState();
  Page.Children.Add(Card(Stack(T("Tek tık DNS", 20, "#B9E2A7", true), T("Adaptörünü seç, DNS profiline bas. Önceki ayarın yedeklenir; gerekirse Windows yönetici izni ister.", 13, "#B9CBC1"), adapterPicker, current, buttons,
   Row(restore, Action("DNS listesini yenile", async () => { dnsAdapters = await ReadDnsSafeAsync(); Navigate("network"); })),
   T("Cloudflare: 1.1.1.1 • Google: 8.8.8.8 • Quad9: 9.9.9.9. Yedek ve IPv6 adresleri de uygulanır. DNS sorguları seçilen sağlayıcıya gider; VPN / iş ağı adlarını etkileyebilir. Bu seçenek DNS şifrelemesi açmaz ve oyun pingini düşürme garantisi vermez.", 11, "#9AAFAE"))));
 }
 private void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
 private void MaximizeWindow(object sender, RoutedEventArgs e)
 {
  if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this);
 }
 private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
