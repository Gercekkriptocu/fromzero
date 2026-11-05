using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FromZero;
public partial class MainWindow : Window
{
 private readonly InventoryService inventory = new();
 private readonly DriverService driverService = new();
 private readonly LiveMetrics metrics = new();
 private readonly GameService gameService = new();
 private readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(2) };
 private List<DeviceInfo> devices = [];
 private List<DriverInfo> drivers = [];
 private List<UpdateInfo> updates = [];
 private SoftwareInventory? software;
 private bool busy, scanned;private string? operationCompletion;
 private string currentPage = "overview";
 private readonly Dictionary<string, Button> nav = [];
 private TextBlock? cpuValue, ramValue, diskValue, netValue;
 private ProgressBar? cpuBar, ramBar, diskBar;
 private readonly Queue<(DateTimeOffset Time, double Cpu, double Ram)> samples = new();
 private bool gamePollRunning, closingAfterRestore;
 private static Brush B(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
 private static readonly FontFamily PixelFont = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Pixelify Sans");
 private static TextBlock T(string text, double size = 14, string color = "#E8EEE5", bool bold = false) => new() { Text = text, FontSize = size, Foreground = B(color), FontFamily = UiTheme.Font, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap,Margin=new Thickness(0,0,0,6) };
 private static StackPanel Stack(params UIElement[] children) { var s = new StackPanel(); foreach (var c in children) s.Children.Add(c); return s; }
 private static Border Card(UIElement child, string background = "#18252D") => new() { Background = B(background.Length==7?"#ED"+background[1..]:background), BorderBrush = B("#4052686B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 14), Child = child };
 private static WrapPanel Row(params UIElement[] children) { var r = new BoundedWrapPanel { Margin = new Thickness(0, 8, 0, 0) }; foreach (var c in children){if(c is FrameworkElement element){element.VerticalAlignment=VerticalAlignment.Center;if(c is TextBlock or ComboBox)element.Margin=new Thickness(0,0,10,8);}r.Children.Add(c);}return r; }
 private Button Action(string label, Func<Task> action, bool primary = false)
 {
  var button = new Button { Content = label };
  if (primary) { button.Background = B("#B9E2A7"); button.Foreground = B("#172D2D"); button.BorderBrush = B("#B9E2A7"); }
  button.Click += async (_, _) => { if (!busy) await RunAsync(label, action); };
  return button;
 }
 public MainWindow()
 {
  InitializeComponent();
  Icon=UiTheme.Logo;MinimizeButton.Content=UiTheme.CaptionIcon("minimize");CloseButton.Content=UiTheme.CaptionIcon("close");MaximizeButton.Content=UiTheme.CaptionIcon("maximize");
  LanguageChoice.SelectedIndex=Locale.IsTurkish?1:0;LanguageChoice.SelectionChanged+=(_,_)=>{var language=LanguageChoice.SelectedIndex==1?"tr":"en";if(Locale.Language!=language)Locale.Set(language);};
  StateChanged+=(_,_)=>{
   MaximizeButton.Content=UiTheme.CaptionIcon(WindowState==WindowState.Maximized?"restore":"maximize");
   WindowLayout.Margin=WindowState==WindowState.Maximized?new Thickness(Math.Max(8,SystemParameters.WindowResizeBorderThickness.Left)):new Thickness(0);
  };
  if(preparationJob!=null)preparationOptions=preparationJob.Options;
  else try{var portable=Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName;if(portable!=null&&File.Exists(Path.Combine(portable,"capsule.json"))&&File.Exists(Path.Combine(portable,"preparation-profile.json"))){var profile=JsonSerializer.Deserialize<PreparationOptions>(File.ReadAllText(Path.Combine(portable,"preparation-profile.json")),LocalStore.Json);if(profile!=null){profile=profile with{OfflineFolder=portable};PreparationPolicy.ValidateOptions(profile);preparationOptions=profile;}}}catch(Exception ex)when(ex is IOException or JsonException){Journal.Write("USB hazırlama profili",ex.Message,false);}
  MinWidth=Math.Min(MinWidth,SystemParameters.WorkArea.Width-30);MinHeight=Math.Min(MinHeight,SystemParameters.WorkArea.Height-30);
  Width = Math.Min(Width, SystemParameters.WorkArea.Width - 30);
  if(Environment.GetCommandLineArgs().Contains("--compact"))Width=Math.Min(900,SystemParameters.WorkArea.Width-30);
  Height = Math.Min(Height, SystemParameters.WorkArea.Height - 30);
  MachineLabel.Text = Environment.MachineName.ToUpperInvariant() + "  /  " + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToUpperInvariant();
  VersionLabel.Text="v"+typeof(MainWindow).Assembly.GetName().Version?.ToString(3)+" / ÖN İZLEME";
  (string Key, string Icon, string Name)[] pages = [("overview","▣","Genel bakış"),("prepare","▶","Bilgisayarı hazırla"),("drivers","▦","Güncellemeler"),("network","⌁","Wi-Fi ve ağ"),("gaming","✚","Oyunlar"),("performance","▥","Performans"),("cleanup","◇","Temizlik"),("capsule","▤","Format kiti"),("startup","↗","Başlangıç"),("apps","▧","Uygulamalar"),("history","◷","Geçmiş"),("workspace","◉","Workspace"),("activation","◇","Activator")];
  var otherNavigation=new ContextMenu{Style=(Style)FindResource("NavigationMenu"),Placement=System.Windows.Controls.Primitives.PlacementMode.Right,HorizontalOffset=8};
  foreach (var p in pages) {
   if(p.Key is "startup" or "apps" or "history" or "workspace" or "activation"){
   var entry=new MenuItem{Header=p.Name,FontFamily=UiTheme.Font,Style=(Style)FindResource("NavigationMenuItem")};
    entry.Click+=async(_,_)=>{if(busy)return;Navigate(p.Key);if(p.Key=="activation"&&activationSnapshot==null)await RunAsync("Refresh license status",RefreshActivationAsync);};otherNavigation.Items.Add(entry);continue;
   }
   var item=new Grid();item.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(26)});item.ColumnDefinitions.Add(new ColumnDefinition());
   var icon=T(p.Icon,14,"#B9CBC1");icon.Margin=new Thickness(0);item.Children.Add(icon);var label=T(p.Name,14,"#D2DED6");label.Margin=new Thickness(0);label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(label,1);item.Children.Add(label);
   var b = new Button { Content = item, ToolTip=p.Name, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 6, 7, 6), Margin = new Thickness(0, 0, 0, 4), BorderThickness = new Thickness(1), BorderBrush=Brushes.Transparent, Background = Brushes.Transparent, FontWeight = FontWeights.Normal };
   System.Windows.Automation.AutomationProperties.SetName(b, p.Name);
   b.Click += (_, _) => { if (!busy) Navigate(p.Key); };
   Navigation.Children.Add(b);nav[p.Key] = b;
  }
  otherNavigation.Opened+=(_,_)=>UiTheme.ApplyTree(otherNavigation);
  OtherToolsButton.Click+=(_,_)=>{if(busy)return;otherNavigation.PlacementTarget=OtherToolsButton;otherNavigation.IsOpen=true;};
  if(SystemParameters.ClientAreaAnimation)
   VoxxHighlight.BeginAnimation(GradientStop.OffsetProperty,new System.Windows.Media.Animation.DoubleAnimation(.12,.88,TimeSpan.FromSeconds(1.8)){AutoReverse=true,RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever});
  ticker.Tick += async (_, _) => {
   RefreshMetrics();
   await MemoryTickAsync();
   if (!busy && !gamePollRunning && gameService.Session != null) {
    gamePollRunning = true;
    try { var result = await gameService.PollAsync(); if (result != null) { StatusLabel.Text = "OYUN OTURUMU GERİ ALINDI"; if(currentPage=="gaming")Navigate("gaming"); } }
    catch(Exception ex) { Journal.Write("Oyun izleme",ex.Message,false); }
    finally { gamePollRunning = false; }
   }
  };
  ticker.Start();
  Closed += (_, _) => {ticker.Stop();try{displaySettings.Dispose();}catch(Exception ex){Journal.Write("Ekran kapanış geri alma",ex.Message,false);}};
  Navigate("overview");
  Loaded += async (_, _) => {
   if(displaySettings.Backup is{Confirmed:false})await RunAsync("Önceki ekran denemesi geri alınıyor",()=>{Detail("Ekran kurtarma",displaySettings.Restore());return Task.CompletedTask;});
   if(gameService.Session!=null)await RunAsync("Önceki oyun oturumu geri alınıyor",async()=>Detail("Oturum kurtarma",await gameService.RestoreAsync()));
   await RunAsync("Donanım taranıyor", ScanAsync);
   var resume=Environment.GetCommandLineArgs();
   if(resume.Length>=3&&resume[1]=="--resume-preparation"&&preparationJob?.Id==resume[2]&&preparationJob.Consented!=null){preparationOptions=preparationJob.Options;Navigate("prepare");if(preparationJob.State=="reboot"&&!PreparationPolicy.SameBoot(preparationJob.BootTime,PreparationBackend.Boot))await RunAsync("Hazırlamaya devam ediliyor",StartPreparationAsync);}
   else if(preparationJob!=null&&PreparationPolicy.Pending(preparationJob)){preparationOptions=preparationJob.Options;Navigate("prepare");}
  };
  Closing += async (_, e) => {
   if(closingAfterRestore)return;
   if(busy){e.Cancel=true;StatusLabel.Text="Önce devam eden işlemin tamamlanmasını bekle.";return;}
   if(gameService.Session!=null){e.Cancel=true;await RunAsync("Oyun ayarları geri alınıyor",async()=>await gameService.RestoreAsync());if(gameService.Session==null){closingAfterRestore=true;Close();}}
  };
 }
 private async Task RunAsync(string title, Func<Task> action)
 {
  busy = true;operationCompletion=null; Page.IsEnabled = false; OperationPanel.Visibility = Visibility.Visible; OperationTitle.Text = title + "…";
  OtherToolsButton.IsEnabled=false;
  foreach (var b in nav.Values) b.IsEnabled = false;
  try { await action(); StatusLabel.Text = operationCompletion??("TAMAMLANDI  /  " + title); }
  catch (OperationCanceledException ex) { StatusLabel.Text = "İPTAL  /  " + ex.Message; }
  catch (Exception ex) { Journal.Write(title, ex.Message, false); StatusLabel.Text = "İŞLEM TAMAMLANAMADI  /  Ayrıntı kaydedildi."; AppMessage.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning); }
  finally { busy = false; Page.IsEnabled = true; OperationPanel.Visibility = Visibility.Collapsed; OtherToolsButton.IsEnabled=true; foreach (var b in nav.Values) b.IsEnabled = true; }
 }
 private void Navigate(string page)
 {
  if(page!="prepare")World.PreparationWork(-1,"idle");
  currentPage = page; Page.Children.Clear(); PageScroll.ScrollToTop(); cpuValue = ramValue = diskValue = netValue = null; cpuBar = ramBar = diskBar = null;
  PageScroll.VerticalScrollBarVisibility=page=="overview"?ScrollBarVisibility.Hidden:ScrollBarVisibility.Auto;
  var station=WorldStations.ForPage(page);World.Travel(station.Sector);
  var navPage=page is "crosshairs" or "valorant-crosshairs" or "benchmark"?"gaming":page=="memory"?"performance":page;
  OtherToolsButton.Background=B(navPage is "startup" or "apps" or "history" or "workspace" or "activation"?"#2A403F":"#182B34");
  foreach (var pair in nav) { pair.Value.Background = pair.Key == navPage ? B("#2A403F") : Brushes.Transparent; pair.Value.Foreground = pair.Key == navPage ? B("#B9E2A7") : B("#9AAFAE"); }
  switch (page) { case "overview": Overview(); break; case "prepare": Preparation(); break; case "capsule": Capsule(); break; case "drivers": Drivers(); break; case "network": Network(); break; case "gaming": Gaming(); break; case "crosshairs": Crosshairs(); break; case "valorant-crosshairs": ValorantCrosshairCatalog(); break; case "benchmark": Benchmark(); break; case "performance": Performance(); break; case "memory": Memory(); break; case "cleanup": Cleaner(); break; case "startup": Startup(); break; case "apps": Apps(); break; case "history": History(); break; case "workspace": Workspace(); break; case "activation": Activation(); break; }
  UiTheme.ApplyTree(Page);
 }
 private void CapsuleNav(object sender, RoutedEventArgs e) { if (!busy) Navigate("capsule"); }
 private void OpenVoxx(object sender,RoutedEventArgs e)=>Commands.Open("https://voxx.app/");
 private void Header(string overline, string title, string subtitle)
 {
  var a = T(overline, 10, "#B9E2A7"); a.FontFamily = UiTheme.Font; a.Margin = new Thickness(0,0,0,8); Page.Children.Add(a);
  var b = T(title, 26, bold: true); b.Margin = new Thickness(0,0,0,7); Page.Children.Add(b);
  var c = T(subtitle, 13, "#9AAFAE"); c.Margin = new Thickness(0,0,0,23); Page.Children.Add(c);
 }
 private void Section(string text) { var t = T(text, 16, bold:true); t.Margin = new Thickness(0, 6, 0, 12); Page.Children.Add(t); }
 private void Overview()
 {
  Header("01 / KONTROL ODASI", "Bilgisayarın için yeni bir başlangıç.", "Hazırla, kontrol et, kaldığın yerden devam et. Bütün araçların tek istasyonda.");
  var hero = new Grid { MinHeight = 220 };
  hero.Children.Add(new Border{Background=B("#7015262E"),CornerRadius=new CornerRadius(14)});
  var scrim = new Border { CornerRadius=new CornerRadius(14), Background = new LinearGradientBrush(Color.FromArgb(245,18,39,45),Color.FromArgb(0,18,39,45),0), MaxWidth = 590, HorizontalAlignment = HorizontalAlignment.Stretch };
  hero.Children.Add(scrim);
  var words = Stack(T("FORMAT SONRASI TEK AKIŞ",11,"#D1C99B"),T("Bilgisayarını hazırla.\nKaldığın yerden devam et.",31,"#E5EDD6",true),T("Eksik sürücüler, Windows güncellemeleri ve\nseçtiğin uygulamalar tek sırada.",13,"#ABC4B2"),Row(Action("Bilgisayarımı hazırla  →",()=> { Navigate("prepare"); return Task.CompletedTask; },true),Action("Format öncesi USB hazırla",()=> { Navigate("prepare"); return Task.CompletedTask; })));
  words.Margin = new Thickness(25,24,25,20);
  foreach (var child in words.Children.OfType<TextBlock>()) child.Margin = new Thickness(0,0,0,8);
  hero.Children.Add(words);
  Page.Children.Add(new Border { BorderBrush=B("#405A746A"), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(16), Background=B("#7015262E"), Padding=new Thickness(2), Margin=new Thickness(0,0,0,20), Child=hero });
  var statGrid = new ResponsiveTilePanel { MaxColumns=4,MinimumTileWidth=210,Margin=new Thickness(0,0,0,18) };
  (string Label, string Unit)[] labels = [("İŞLEMCİ","CPU"),("BELLEK","RAM"),("SİSTEM DİSKİ","BOŞ ALAN"),("BAĞLANTI","AĞ DURUMU")];
  for(int i=0;i<4;i++) {
   var value=T("—",23,"#E8EEE5",true); value.FontFamily=UiTheme.Font; value.Margin=new Thickness(0,12,0,12);
   var bar=new ProgressBar { Maximum=100 };
   var s=Stack(T(labels[i].Label,10,"#9AAFAE"),value,bar); var card=Card(s); card.Margin=new Thickness(0);statGrid.Children.Add(card);
   if(i==0){cpuValue=value;cpuBar=bar;} if(i==1){ramValue=value;ramBar=bar;} if(i==2){diskValue=value;diskBar=bar;} if(i==3){netValue=value;bar.Visibility=Visibility.Collapsed;s.Children.Add(T("Yalnızca adaptör durumu",10,"#9AAFAE"));}
  }
  Page.Children.Add(statGrid);
  Section("İstasyondaki görevler");
  var grid = new ResponsiveTilePanel { MaxColumns=2,MinimumTileWidth=330 };
  var issues=devices.Count(d=>d.Problem!=0);
  var left=Card(Stack(T("▦  Sürücü kontrolü",17,"#D8E8D3",true),T(scanned ? $"{devices.Count} cihaz tarandı. {issues} cihaz dikkat gerektiriyor." : "Donanım envanteri hazırlanıyor…",13,"#9AAFAE"),Row(Action("Sürücü merkezini aç  →",()=>{Navigate("drivers");return Task.CompletedTask;}))));left.Margin=new Thickness(0,0,0,14);
  var right=Card(Stack(T("⌁  Bağlantını anla",17,"#D8E8D3",true),T("Adaptör, ağ geçidi ve DNS. Sorunu adım adım bul.",13,"#9AAFAE"),Row(Action("Bağlantıyı incele  →",()=>{Navigate("network");return Task.CompletedTask;})))); Grid.SetColumn(right,1); grid.Children.Add(left); grid.Children.Add(right); Page.Children.Add(grid);
  Page.Children.Add(Row(Action("Donanımı yeniden tara",ScanAsync),Action("Bilgisayar pasaportunu kaydet",SavePassportAsync)));
  RefreshMetrics();
 }
 private void RefreshMetrics()
 {
  var m=metrics.Read(); samples.Enqueue((DateTimeOffset.Now,m.Cpu,m.RamPercent)); while(samples.Count>180)samples.Dequeue();
  if(cpuValue!=null)cpuValue.Text=$"%{m.Cpu:0}"; if(cpuBar!=null)cpuBar.Value=m.Cpu;
  if(ramValue!=null)ramValue.Text=$"{m.UsedGb:0.0} / {m.TotalGb:0.0} GB"; if(ramBar!=null)ramBar.Value=m.RamPercent;
  if(diskValue!=null) { try {var disk=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!); diskValue.Text=$"{disk.AvailableFreeSpace/1073741824d:0} GB"; if(diskBar!=null)diskBar.Value=100d*(disk.TotalSize-disk.AvailableFreeSpace)/disk.TotalSize;}catch(IOException){diskValue.Text="Ölçülemedi";} }
  if(netValue!=null){
   var active=NetworkInterface.GetAllNetworkInterfaces().Any(n=>n.OperationalStatus==OperationalStatus.Up &&
    n.NetworkInterfaceType!=NetworkInterfaceType.Loopback && n.GetIPProperties().UnicastAddresses.Count>0);
   UiTheme.SetLocalizedText(netValue,active?"ETKİN":"ÇEVRİMDIŞI");
  }
 }
 private async Task ScanAsync()
 {
  var dt=inventory.DevicesAsync(); var dr=inventory.DriversAsync(); var apps=InstalledSoftware.ReadAsync(); var dns=ReadDnsSafeAsync(); await Task.WhenAll(dt,dr,apps,dns); devices=await dt;drivers=await dr;software=await apps;dnsAdapters=await dns;scanned=true;
  Journal.Write("Donanım taraması",$"{devices.Count} cihaz, {drivers.Count} sürücü, {devices.Count(d=>d.Problem!=0)} sorunlu cihaz."); Navigate(currentPage);
 }
 private async Task SavePassportAsync()
 {
  var dlg=new SaveFileDialog { FileName="FromZero-pasaport.json",Filter="JSON|*.json" };if(dlg.ShowDialog(this)!=true)return;
  var data=new { Created=DateTimeOffset.Now, System=await inventory.PassportAsync(), Devices=devices, Drivers=drivers };
  await File.WriteAllTextAsync(dlg.FileName,JsonSerializer.Serialize(data,LocalStore.Json));Journal.Write("Pasaport dışa aktarıldı",dlg.FileName);AppMessage.Show(this,"Bilgisayar pasaportu kaydedildi.","FromZero");
 }
 private static string? PickFolder(string title) { var dlg=new OpenFolderDialog { Title=title };return dlg.ShowDialog()==true?dlg.FolderName:null; }
 private bool Confirm(string text,string title="İşlem planı")=>AppMessage.Show(this,text,title,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
 private void Detail(string title,string text)
 {
  var window=CreateDialog(title,820,570);
  var box=new TextBox { Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=UiTheme.Font,FontSize=14,Margin=new Thickness(20) };window.Content=box;window.ShowDialog();
 }
 private Window CreateDialog(string title,double width,double height)=>new(){Title=title,Owner=this,Width=Math.Min(width,SystemParameters.WorkArea.Width-30),Height=Math.Min(height,SystemParameters.WorkArea.Height-30),MinWidth=Math.Min(420,SystemParameters.WorkArea.Width-30),MinHeight=Math.Min(280,SystemParameters.WorkArea.Height-30),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#142129"),Foreground=B("#E8EEE5")};
}



