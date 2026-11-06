using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace FromZero;

public partial class MainWindow
{
 private WindowsScan? windowsScan;
 private RuntimeScan? runtimeScan;
 private ApplicationScan? applicationScan;
 private string updateCategory="all";
 private TextBlock? queueSummary;
 private readonly HashSet<string> windowsSelection=new(),packageSelection=new();
 private readonly List<string> updateWarnings=[];
 private string updateInstallationReport="",updateInstallationStatus="";
 private readonly Dictionary<string,string> downloadStatus=new(StringComparer.OrdinalIgnoreCase);
 private bool extendedUpdateScan=true,legacyRuntimeScan=true;
 private bool automaticDownloads=ReadAutoDownload();
 private CancellationTokenSource? updateCancellation;
 private void ShowUpdateScanAnimation(bool visible)=>UpdateScanOverlay.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
 private static bool ReadAutoDownload(){try{return File.Exists(LocalStore.FilePath("auto-download.json"))&&JsonSerializer.Deserialize<bool>(File.ReadAllText(LocalStore.FilePath("auto-download.json")));}catch(Exception ex)when(ex is IOException or JsonException){return false;}}
 private void Drivers()
 {
  var manufacturer=updateCategory=="oem";
  Header("GÜNCELLEME MERKEZİ",manufacturer?"Üretici desteği":"Your updates",manufacturer?$"{OemSources.Tools.Count} araç • {OemSources.All.Count} destek sayfası • {AppDownloads.Recipes.Count(r=>r.DriverTool)} uygulama içi indirme":"Scan your PC, review outdated versions and install your selections.");
  var summaries=new List<string>();if(windowsScan!=null)summaries.Add(windowsScan.Offers.Count+" Windows / sürücü teklifi");if(applicationScan!=null)summaries.Add(applicationScan.Applications.Count+" uygulama");if(runtimeScan!=null)summaries.Add(runtimeScan.Packages.Count+" .NET / SDK paketi");if(components!=null)summaries.Add(components.Components.Count+" oyun bileşeni");var scanText=summaries.Count==0?"Henüz tarama yapılmadı.":string.Join(" • ",summaries);
  queueSummary=T("",12,"#B9E2A7");
  if(manufacturer)Page.Children.Add(Row(Action("Seçilenleri indir",()=>DownloadSelectionAsync(false),true),queueSummary));
  else Page.Children.Add(Card(Stack(Row(Action("Güncellemeleri tara",ScanAllUpdatesAsync),
   Action("Select all missing updates",SelectAllMissingUpdatesAsync),Action("Download and update",InstallSelectedUpdatesAsync,true)),
   T(scanText,13,"#D2DED6"),queueSummary,
   T("Save your work before updating. Selected applications are asked to close. Package licences are accepted and Windows may require a restart.",11,"#9AAFAE"))));UpdateQueueSummary();
  if(updateInstallationReport.Length>0)Page.Children.Add(Card(Stack(T(updateInstallationStatus,17,"#B9E2A7",true),
   PreparationExpander("Installation results",T(updateInstallationReport,12,"#B9CBC1")))));
  var tabs=Row();foreach(var (id,label) in new[]{("all","Results"),("hardware","Hardware"),("catalog","Driver finder"),("oem","Üretici araçları")}){var category=id;var button=Action(label,()=>{updateCategory=category;Navigate("drivers");return Task.CompletedTask;},updateCategory==id);tabs.Children.Add(button);}Page.Children.Add(tabs);
  if(updateCategory=="apps")Page.Children.Add(Row(Action("Yalnız uygulamaları tara",ScanApplicationsAsync,true)));
  if(!manufacturer){var settingsStart=Page.Children.Count;
  var auto=new CheckBox{Content="Taramadan sonra uygun sürücü, uygulama ve kurulu .NET güncellemelerini otomatik indir",IsChecked=automaticDownloads};
  auto.Checked+=(_,_)=>{if(!Confirm("Her kapsamlı tarama sonrasında desteklenen, kurulu .NET hatlarının yeni yamaları, kurulu uygulamaların kararlı güncellemeleri ve lisansı daha önce kabul edilmiş güncel sürücü teklifleri indirilir. Eski, gizli, BIOS/firmware ve destek dışı paketler otomatik seçilmez. Kurulum yapılmaz. Windows paketleri için yönetici izni gerekebilir. Mobil bağlantıda veri kullanabilir. Açılsın mı?","Otomatik indirme")){auto.IsChecked=false;return;}automaticDownloads=true;File.WriteAllText(LocalStore.FilePath("auto-download.json"),"true");};
  auto.Unchecked+=(_,_)=>{automaticDownloads=false;File.WriteAllText(LocalStore.FilePath("auto-download.json"),"false");};Page.Children.Add(auto);
  Advanced("Tarama kapsamı",()=>{
   var extended=new CheckBox{Content="Windows'un sunduğu gizli ve yerine yeni sürüm geçen teklifleri de göster",IsChecked=extendedUpdateScan};extended.Checked+=(_,_)=>extendedUpdateScan=true;extended.Unchecked+=(_,_)=>extendedUpdateScan=false;
   var legacy=new CheckBox{Content=".NET 3.0 ve sonrası destek dışı sürüm hatlarını da incele",IsChecked=legacyRuntimeScan};legacy.Checked+=(_,_)=>legacyRuntimeScan=true;legacy.Unchecked+=(_,_)=>legacyRuntimeScan=false;
   Page.Children.Add(Stack(extended,legacy,T("Windows Update bu bilgisayara sunduğu paketleri döndürür. "+ApplicationSources.Count+" uygulama/sürüm hattı: 30 doğrudan üretici kaydı + 570 Scoop Extras topluluk manifesti. Scoop/WinGet kurulumu gerekmez; manifest betikleri çalıştırılmaz. Geniş katalog paketleri SHA-256 ve güvenilir Windows imzası gerektirir; imzasızsa indirme reddedilir. .NET'in kararlı hatları ve SDK bantları ayrıca taranır. Metadata 15 dakika önbellekte tutulur; kaynak hataları ve kontrol zamanı gösterilir.",12,"#B9CBC1")));
  });
  Advanced("Source views",()=>{
   var views=Row();
   foreach(var (id,label) in new[]{("windows","Windows / sürücü"),("apps","Uygulamalar"),("runtimes",".NET / SDK"),("games","Oyun bileşenleri")}){
    var source=id;views.Children.Add(Action(label,()=>{updateCategory=source;Navigate("drivers");return Task.CompletedTask;}));
   }
   Page.Children.Add(views);
  });
  Page.Children.Add(Row(Action("Seçilenleri indir",()=>DownloadSelectionAsync(false)),Action("Seçimi boşalt",()=>{windowsSelection.Clear();packageSelection.Clear();catalogDriverQueue.Clear();Navigate("drivers");return Task.CompletedTask;}),Action("İndirme klasörü",()=>{var path=LocalStore.FilePath("Downloads");Directory.CreateDirectory(path);Commands.Open(path);return Task.CompletedTask;}),Link("Windows Update","ms-settings:windowsupdate")));
  GroupSince(settingsStart,"Tarama ve indirme seçenekleri");
  }
  if(updateWarnings.Count>0)Advanced(updateWarnings.Count+" kayıt kontrol edilemedi",()=>Page.Children.Add(T(string.Join("\n",updateWarnings),12,"#EBC078")));
  if(updateCategory is "apps" or "runtimes" or "windows"&&windowsScan==null&&runtimeScan==null&&applicationScan==null)Page.Children.Add(Card(T("Listeyi doldurmak için Tüm kaynakları tara düğmesini kullan. "+ApplicationSources.Count+" uygulama/sürüm hattı, Windows teklifleri ve .NET SDK bantları kontrol edilir.",14,"#B9CBC1")));
  if(updateCategory=="all")RenderUnifiedUpdateResults();
  if(updateCategory=="hardware")RenderHardwareUpdates();
  if(updateCategory=="catalog")RenderCatalogDrivers();
  if(windowsScan!=null&&updateCategory=="windows")RenderWindowsOffers();
  if(applicationScan!=null&&updateCategory=="apps")RenderApplicationOffers();
  if(runtimeScan!=null&&updateCategory=="runtimes")RenderRuntimeOffers();
  if(updateCategory=="games")Advanced("DirectX, XNA ve Visual C++ 2005–v14",()=>{
   Page.Children.Add(Row(Action("Bileşenleri kontrol et",ScanComponentsAsync),Action("Seçilen kurucuları indir",()=>DownloadSelectionAsync(false))));
   if(components!=null){RenderComponents();foreach(var recipe in AppDownloads.Recipes.Where(r=>r.Id.StartsWith("vc",StringComparison.Ordinal)||r.Id is "directx" or "directx-offline" or "xna")){
    var r=recipe;var state=downloadStatus.GetValueOrDefault(r.Id,AppDownloads.Cached(r)!=null?"Doğrulanmış paket hazır":"İndirilmedi");
    var box=new CheckBox{Content=r.Name+" • "+state,IsChecked=packageSelection.Contains(r.Id)};box.Checked+=(_,_)=>{packageSelection.Add(r.Id);UpdateQueueSummary();};box.Unchecked+=(_,_)=>{packageSelection.Remove(r.Id);UpdateQueueSummary();};Page.Children.Add(box);
   }}
  });
  if(updateCategory=="oem")RenderManufacturerCatalog();
  if(updateCategory=="windows")Advanced("Cihaz envanteri ve çevrimdışı sürücüler",()=>{
   Page.Children.Add(Row(Action("Donanımı yeniden tara",ScanAsync),Action("Yerel INF seç",PickInfAsync),Action("Sürücüleri yedekle",BackupDriversAsync),Action("Kapsülden sürücü bul",FindCapsuleDriversAsync)));LegacyDrivers();
  });
 }
 private void UpdateQueueSummary(){if(queueSummary!=null){var windows=windowsScan?.Offers.Where(o=>windowsSelection.Contains(o.Key)).ToList()??[];var count=windows.Count+packageSelection.Count+catalogDriverQueue.Count;queueSummary.Text=Locale.Text(count==0?"Paket seçilmedi.":$"{count} paket seçili"+(windows.Count>0?" • Windows: "+SizeLabel(windows.Sum(o=>o.Bytes)):""));}}
 private void SelectRecommended(){if(windowsScan!=null)foreach(var offer in WindowsUpdateCatalog.RecommendedDrivers(windowsScan.Offers))windowsSelection.Add(offer.Key);SelectPackageUpdates();UpdateQueueSummary();}
 private void SelectPackageUpdates()
 {
  if(runtimeScan!=null)foreach(var package in runtimeScan.Packages.Where(p=>p.Supported&&p.NeedsUpdate))packageSelection.Add(package.Id);
  if(applicationScan!=null)foreach(var app in applicationScan.Applications.Where(a=>a.NeedsUpdate))packageSelection.Add(app.Id);
 }
 private async Task SelectAllMissingUpdatesAsync()
 {
  if(windowsScan==null||runtimeScan==null||applicationScan==null)await ScanAllUpdatesAsync(downloadAfterScan:false);
  windowsSelection.Clear();packageSelection.Clear();
  foreach(var offer in WindowsUpdateCatalog.MissingUpdates(windowsScan?.Offers??[]))windowsSelection.Add(offer.Key);
  SelectPackageUpdates();
  if(components!=null)foreach(var component in components.Components.Where(c=>!c.Present&&c.Recipe!=null&&AppDownloads.Recipes.Any(r=>r.Id==c.Recipe)))
   packageSelection.Add(component.Recipe!);
  Navigate("drivers");
 }
 private void RenderApplicationOffers()
 {
  Section("Uygulama sürümleri");
  Page.Children.Add(Card(Stack(T("Installed apps first. Optional apps stay optional.",18,"#B9E2A7",true),T($"{applicationScan!.Applications.Count(a=>a.NeedsUpdate)} updates available · {applicationScan.Applications.Count(a=>a.State=="Güncel veya daha yeni")} up to date · {applicationScan.Applications.Count(a=>a.Installed.Length>0&&!a.NeedsUpdate&&a.State!="Güncel veya daha yeni")} versions not verified",13,"#B9CBC1"),T("No missing catalogue app is selected automatically. Uncheck the installed-only filter only when you deliberately want an optional app.",12,"#9AAFAE"))));
  var search=new TextBox{Height=38,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Uygulama adı"};var only=new CheckBox{Content="Yalnızca güncellemesi olanlar"};var installedOnly=new CheckBox{Content="Yalnız kurulu olanlar",IsChecked=true};var category=new ComboBox{ItemsSource=new[]{"Tümü"}.Concat(applicationScan!.Applications.Select(a=>a.Category).Distinct().Order()),SelectedIndex=0,MinHeight=34};Page.Children.Add(T("Uygulamalarda ara",12,"#B9CBC1"));Page.Children.Add(search);Page.Children.Add(Row(only,installedOnly,category));var paging=Row();Page.Children.Add(paging);var rows=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=330};Page.Children.Add(rows);int page=0;
  void Render(){rows.Children.Clear();paging.Children.Clear();var filtered=applicationScan!.Applications.Where(a=>(only.IsChecked!=true||a.NeedsUpdate)&&(installedOnly.IsChecked!=true||a.Installed.Length>0)&&(category.SelectedIndex==0||a.Category==category.SelectedItem?.ToString())&&(search.Text.Length==0||a.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase))).OrderByDescending(a=>a.NeedsUpdate).ThenBy(a=>a.Name).ToList();page=Math.Clamp(page,0,Math.Max(0,(filtered.Count-1)/20));paging.Children.Add(T($"{filtered.Count} sonuç • Sayfa {page+1}/{Math.Max(1,(filtered.Count+19)/20)}",12,"#B9CBC1"));if(page>0)paging.Children.Add(Action("← Önceki",()=>{page--;Render();return Task.CompletedTask;}));if((page+1)*20<filtered.Count)paging.Children.Add(Action("Sonraki →",()=>{page++;Render();return Task.CompletedTask;}));foreach(var app in filtered.Skip(page*20).Take(20)){
   var item=app;var selected=new CheckBox{Content=item.Name,IsChecked=packageSelection.Contains(item.Id)};selected.Checked+=(_,_)=>{packageSelection.Add(item.Id);UpdateQueueSummary();};selected.Unchecked+=(_,_)=>{packageSelection.Remove(item.Id);UpdateQueueSummary();};
   var advice=UpdateAssessment.For(item);var state=downloadStatus.GetValueOrDefault(item.Id,advice.Title);
   var versions="Installed: "+(item.Installed.Length==0?"Not installed":string.Join(", ",item.Installed))+"\nAvailable stable release: "+item.Version;
   var info=new Expander{Header="Paket bilgisi",Foreground=B("#B9CBC1"),Content=T(item.Category+"\nSon kontrol: "+(item.SourceChecked?.ToLocalTime().ToString("dd.MM HH:mm")??"Kontrol gerekli")+(item.Recipe.CatalogSigned?"\nSHA-256 ve Windows imzası zorunlu.":item.Recipe.PublisherHashOnly?"\nÜretici SHA-256 kaydı; dijital imza yok.":"\nYayıncı imzası doğrulanır."),12,"#B9CBC1"),Margin=new Thickness(0,6,0,0)};
   var installedApp=(software?.Apps??[]).FirstOrDefault(a=>item.Installed.Contains(a.Version)&&ApplicationCatalog.Matches(a,[item.Name]));
   rows.Children.Add(Card(Stack(AppIcons.Heading(item.Name,installedApp?.DisplayIcon,selected),T(state,14,advice.Color,true),T(versions,12,"#B9CBC1"),T(advice.Reason,12,"#B9CBC1"),T(advice.Action,12,advice.Color),info)));
  }}
  void Filter(){page=0;Render();}search.TextChanged+=(_,_)=>Filter();only.Checked+=(_,_)=>Filter();only.Unchecked+=(_,_)=>Filter();installedOnly.Checked+=(_,_)=>Filter();installedOnly.Unchecked+=(_,_)=>Filter();category.SelectionChanged+=(_,_)=>Filter();Render();
 }
 private void RenderWindowsOffers()
 {
  Section("Windows ve donanım teklifleri");var items=windowsScan!.Offers;
  Page.Children.Add(T($"{items.Count(i=>i.Kind=="Driver")} sürücü • {items.Count(i=>i.Kind=="Software")} Windows/yazılım • {items.Count(i=>i.Hidden||i.Superseded)} gizli/eski • son kontrol {windowsScan.Checked.ToLocalTime():HH:mm}",12,"#B9CBC1"));
  var group=new ComboBox{ItemsSource=driverReviewGroups,SelectedIndex=0,MinWidth=180};
  var old=new CheckBox{Content="Gizli ve eski teklifleri listede göster"};
  var search=new TextBox{Height=38,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Üretici, cihaz veya güncelleme adı"};Page.Children.Add(T("Güncellemelerde ara",12,"#B9CBC1"));Page.Children.Add(search);Page.Children.Add(Row(group,old));
  var rows=new StackPanel();var paging=new WrapPanel();int page=0;
  void Render(){rows.Children.Clear();paging.Children.Clear();var filtered=items.Where(i=>(group.SelectedIndex<=0||i.ReviewGroup==driverReviewGroups[group.SelectedIndex])&&(old.IsChecked==true||!i.Hidden&&!i.Superseded)&&(search.Text.Length==0||(i.Title+" "+i.Model+" "+i.Manufacturer).Contains(search.Text,StringComparison.OrdinalIgnoreCase))).OrderBy(i=>i.Firmware).ThenBy(i=>i.Kind).ToList();page=Math.Clamp(page,0,Math.Max(0,(filtered.Count-1)/10));
   foreach(var item in filtered.Skip(page*10).Take(10)){
    var offer=item;var blocked=offer.Firmware||offer.Hidden||offer.Superseded;var selection=new CheckBox{Content=offer.Title,IsEnabled=!blocked,IsChecked=windowsSelection.Contains(offer.Key)};selection.Checked+=(_,_)=>{windowsSelection.Add(offer.Key);UpdateQueueSummary();};selection.Unchecked+=(_,_)=>{windowsSelection.Remove(offer.Key);UpdateQueueSummary();};
    var state=offer.Firmware?"Firmware: üretici akışı gerekli":offer.Hidden?"Gizli teklif: indirme dışı":offer.Superseded?"Yerine yeni sürüm geçmiş: indirme dışı":offer.Downloaded?"Downloaded · ready to install":"İndirilebilir";
    if(downloadStatus.TryGetValue(offer.Key,out var status))state=status;
    var actions=Row(Action("Lisans / paket ayrıntısı",()=>{Detail(offer.Title,$"Kaynak: Windows Update\nKimlik: {offer.Id}\nRevizyon: {offer.Revision}\nSürüm: {offer.Version}\nTarih: {offer.Date}\n\n"+offer.Eula);return Task.CompletedTask;}));
    var body=Stack(selection,T(offer.ReviewGroup+" • "+SizeLabel(offer.Bytes)+"\n"+state,12,blocked?"#EBC078":"#B9CBC1"));
    if(offer.Kind=="Driver"){
     var deviceIds=devices.Where(d=>WindowsUpdateCatalog.MatchesDevice(offer,d)).Select(d=>d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
     body.Children.Add(T("Installed version: "+InstalledVersions(drivers.Where(d=>deviceIds.Contains(d.DeviceId)))+"  →  Offered version: "+
      (string.IsNullOrWhiteSpace(offer.Version)?"Version unavailable":offer.Version),12,"#B9CBC1"));
     actions.Children.Add(DriverRollback(offer));
     if(!blocked)actions.Children.Add(Action("Yedekle ve kur",()=>InstallDriverOfferAsync(offer)));
    }
    body.Children.Add(actions);rows.Children.Add(Card(body));
   }
   if(filtered.Count==0)rows.Children.Add(T("Bu filtrede sunulan güncelleme yok.",13,"#B9CBC1"));paging.Children.Add(T($"{filtered.Count} teklif • sayfa {page+1}/{Math.Max(1,(filtered.Count+9)/10)}",12,"#B9CBC1"));
   if(page>0)paging.Children.Add(Action("←",()=>{page--;Render();return Task.CompletedTask;}));if((page+1)*10<filtered.Count)paging.Children.Add(Action("Sonraki →",()=>{page++;Render();return Task.CompletedTask;}));
  }
  search.TextChanged+=(_,_)=>{page=0;Render();};old.Checked+=(_,_)=>Render();old.Unchecked+=(_,_)=>Render();group.SelectionChanged+=(_,_)=>{page=0;Render();};Page.Children.Add(rows);Page.Children.Add(paging);Render();
 }
 private void RenderRuntimeOffers()
 {
  Advanced($".NET sürüm hatları • {runtimeScan!.Channels} hat / {runtimeScan.Packages.Count} paket",()=>{
   Page.Children.Add(T("Kurulu bütün kararlı yama sürümleri okunur. Destek dışı hatlar yalnızca eski uygulama uyumluluğu içindir; yeni programlara önerilmez. x86 ve x64 ayrı çalışma ortamlarıdır.",12,"#B9CBC1"));
   foreach(var channel in runtimeScan.Packages.GroupBy(p=>p.Channel))Advanced(".NET "+channel.Key+(channel.First().Supported?" / desteklenen":" / destek dışı"),()=>{
    foreach(var item in channel){var p=item;var check=new CheckBox{Content=$"{p.Name} {p.Architecture} • {p.State} • son sürüm {p.Version}",IsChecked=packageSelection.Contains(p.Id)};
     check.Checked+=(_,_)=>{packageSelection.Add(p.Id);UpdateQueueSummary();};check.Unchecked+=(_,_)=>{packageSelection.Remove(p.Id);UpdateQueueSummary();};
     var state=downloadStatus.GetValueOrDefault(p.Id,AppDownloads.Cached(p.Recipe)!=null?"Doğrulanmış kurucu hazır":"");
     Page.Children.Add(Card(Stack(check,T("Kurulu: "+(p.Installed.Length==0?"Bulunamadı":string.Join(", ",p.Installed))+"\n"+state,12,"#B9CBC1"))));
    }
   });
   Page.Children.Add(Row(Action("Seçilen çalışma ortamlarını indir",()=>DownloadSelectionAsync(false))));
  });
 }
 private async Task ScanApplicationsAsync()
 {
  ShowUpdateScanAnimation(true);
  using var cancel=new CancellationTokenSource();updateCancellation=cancel;CancelOperation.Visibility=Visibility.Visible;
  try{
   software=await InstalledSoftware.ReadAsync();
   var result=await ApplicationCatalog.ScanAsync(software.Apps,new Progress<string>(s=>OperationTitle.Text=s),cancel.Token);
   applicationScan=result;packageSelection.RemoveWhere(id=>id.StartsWith("app-",StringComparison.Ordinal));
   foreach(var app in result.Applications.Where(a=>a.NeedsUpdate))packageSelection.Add(app.Id);
   updateWarnings.RemoveAll(w=>w.StartsWith("Uygulama: ",StringComparison.Ordinal));updateWarnings.AddRange(result.Warnings.Select(w=>"Uygulama: "+w));
   Journal.Write("Uygulama sürüm taraması",$"{result.Applications.Count} kayıt; {result.Warnings.Count} kaynak uyarısı. İndirme başlatılmadı.",result.Warnings.Count==0);
  }finally{ShowUpdateScanAnimation(false);updateCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;updateCategory="apps";Navigate("drivers");}
 }
 private Task ScanAllUpdatesAsync()=>ScanAllUpdatesAsync(downloadAfterScan:true);
 private async Task ScanAllUpdatesAsync(bool downloadAfterScan)
 {
  ShowUpdateScanAnimation(true);
  updateWarnings.Clear();windowsSelection.Clear();packageSelection.Clear();windowsScan=null;runtimeScan=null;applicationScan=null;hardwareInventoryReady=false;
  using var cancel=new CancellationTokenSource();updateCancellation=cancel;CancelOperation.Visibility=Visibility.Visible;
  try{
   OperationTitle.Text="Windows / sürücüler ve resmi çalışma ortamı katalogları taranıyor…";
   async Task Windows(){try{windowsScan=await WindowsUpdateCatalog.ScanAsync(extendedUpdateScan,cancel.Token);updateWarnings.AddRange(windowsScan.Warnings);}catch(Exception ex)when(ex is not OperationCanceledException){updateWarnings.Add("Windows kaynağı tamamlanamadı: "+ex.Message);}}
   async Task Runtimes(){try{runtimeScan=await RuntimeCatalog.ScanAsync(legacyRuntimeScan,new Progress<string>(s=>OperationTitle.Text=s),cancel.Token);updateWarnings.AddRange(runtimeScan.Warnings);}catch(Exception ex)when(ex is not OperationCanceledException){updateWarnings.Add(".NET kaynağı tamamlanamadı: "+ex.Message);}}
   var softwareTask=InstalledSoftware.ReadAsync();async Task Apps(){try{var appInventory=await softwareTask;applicationScan=await ApplicationCatalog.ScanAsync(appInventory.Apps,new Progress<string>(s=>OperationTitle.Text=s),cancel.Token);updateWarnings.AddRange(applicationScan.Warnings);}catch(Exception ex)when(ex is not OperationCanceledException){updateWarnings.Add("Uygulama kaynakları tamamlanamadı: "+ex.Message);}}
   async Task Hardware(){try{await RefreshHardwareInventoryAsync(cancel.Token);}catch(Exception ex)when(ex is not OperationCanceledException){updateWarnings.Add("Hardware inventory unavailable: "+ex.Message);}}
   await Task.WhenAll(Windows(),Runtimes(),Apps(),Hardware());software=await softwareTask;
   components=await Task.Run(()=>GameComponents.Scan(software.Apps));
   if(windowsScan!=null){updates=windowsScan.Offers.Where(i=>i.Kind=="Driver"&&!i.Hidden&&!i.Superseded).Select(i=>new UpdateInfo(i.Id,i.Revision,i.Title,i.Manufacturer,i.Model,i.Eula,i.Class)).ToList();foreach(var offer in WindowsUpdateCatalog.RecommendedDrivers(windowsScan.Offers))windowsSelection.Add(offer.Key);}
   SelectRecommended();
   Journal.Write("Kapsamlı güncelleme taraması",$"{windowsScan?.Offers.Count??0} Windows teklifi; {runtimeScan?.Packages.Count??0} çalışma ortamı; {updateWarnings.Count} uyarı.",updateWarnings.Count==0);
   ShowUpdateScanAnimation(false);
   if(automaticDownloads&&downloadAfterScan){windowsSelection.RemoveWhere(key=>windowsScan?.Offers.FirstOrDefault(o=>o.Key==key)?.EulaAccepted!=true);await DownloadSelectionAsync(true);}
  }finally{ShowUpdateScanAnimation(false);updateCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;Navigate("drivers");}
 }
 private async Task DownloadSelectionAsync(bool automatic,bool driversOnly=false)
 {
  if(automatic){await DownloadSelectionCoreAsync(true,driversOnly);return;}
  using var cancel=new CancellationTokenSource();updateCancellation=cancel;CancelOperation.Visibility=Visibility.Visible;
  try{await DownloadSelectionCoreAsync(false);}finally{updateCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;}
 }
 private async Task DownloadSelectionCoreAsync(bool automatic,bool driversOnly=false)
 {
  var offers=windowsScan?.Offers.Where(o=>windowsSelection.Contains(o.Key)&&!o.Downloaded).ToList()??[];
  var recipes=AppDownloads.Recipes.Where(r=>!driversOnly&&packageSelection.Contains(r.Id)).ToList();
  if(offers.Count+recipes.Count==0){if(!automatic)Detail("İndirme kuyruğu","Önce kapsamlı tarama yap ve paket seç. Güncel olmayan kurulu sürüm hatları otomatik seçilir.");return;}
  if(!automatic&&!Confirm($"{offers.Count} Windows / sürücü paketi ve {recipes.Count} kurucu indirilecek.\nWindows paketleri Windows Update önbelleğine, kurucular FromZero indirme klasörüne kaydedilir. Kurulum yapılmaz. Windows paketlerinin ayrıntısındaki lisans koşullarını kabul ederek devam edilsin mi?","Seçilen paketleri indir"))return;
  var results=new List<string>();
  if(offers.Count>0){try{OperationTitle.Text="Windows paketleri indiriliyor…";var report=await WindowsUpdateCatalog.DownloadAsync(offers,!automatic);foreach(var result in report){var state=result.Code==2&&result.Downloaded?"Downloaded · ready to install":"İndirme başarısız: "+result.Code+" / "+result.HResult;downloadStatus[result.Id+":"+result.Revision]=state;results.Add(result.Title+" • "+state);}}catch(Exception ex)when(ex is not OperationCanceledException){results.Add("Windows indirme: "+ex.Message);}}
  foreach(var recipe in recipes){
   updateCancellation?.Token.ThrowIfCancellationRequested();
   try{
    var cached=AppDownloads.Cached(recipe);bool verified=false;
    if(cached!=null){try{await AppDownloads.VerifyCachedAsync(recipe,cached);verified=true;}catch(Exception ex)when(ex is IOException or InvalidDataException){OperationTitle.Text="Önbellek eski/değişmiş; güncel paket yeniden indiriliyor…";}}
    if(!verified)await AppDownloads.DownloadAsync(recipe,new Progress<string>(s=>OperationTitle.Text=s),updateCancellation?.Token??default);
    downloadStatus[recipe.Id]=recipe.PublisherHashOnly?"Üretici SHA-256 kaydı doğrulandı (dijital imza yok)":"İmza ve bütünlük doğrulandı";results.Add(recipe.Name+" • hazır");
   }catch(Exception ex)when(ex is not OperationCanceledException){downloadStatus[recipe.Id]="İndirilemedi: "+ex.Message;results.Add(recipe.Name+" • "+ex.Message);}
  }
  var path=LocalStore.FilePath("last-update-download.json");await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Checked=DateTimeOffset.Now,Results=results},LocalStore.Json));
  if(!automatic){Navigate("drivers");Detail("İndirme sonuçları",string.Join("\n",results)+"\n\nWindows paketleri Windows Update önbelleğindedir. Kurulumlar ayrı seçilir.");}
 }
}




