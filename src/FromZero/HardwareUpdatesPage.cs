using System.Windows.Controls;
using System.Windows;
using System.IO;

namespace FromZero;

public partial class MainWindow
{
 private bool hardwareInventoryReady;

 private static readonly string[] driverReviewGroups=["All offers","Recommended","Optional","Manufacturer check required"];
 private static string InstalledVersions(IEnumerable<DriverInfo> records)
 {
  var versions=records.Where(d=>!string.IsNullOrWhiteSpace(d.Version)).Select(d=>d.Version).Distinct().ToArray();
  return versions.Length==0?"Version unavailable":string.Join(", ",versions);
 }
 private Button DriverRollback(WindowsOffer offer)
 {
  return Action(offer.Firmware?"Manufacturer recovery":"Rollback options",()=>{
   var backup=LocalStore.FilePath("Backups");
   var body=offer.Firmware?
    "Use the computer or board manufacturer's recovery procedure for this exact model.":
    "Open Device Manager → device Properties → Driver → Roll Back Driver. Availability depends on the previous package. FromZero exports third-party drivers before installation. If Windows cannot roll back, use the matching signed INF from your backup.";
   var window=CreateDialog(offer.Firmware?"Manufacturer recovery":"Rollback options",650,320);
   var actions=Row();
   void Add(string caption,System.Action open)
   {
    var button=new Button{Content=caption};
    button.Click+=(_,_)=>{window.Close();open();};
    actions.Children.Add(button);
   }
   if(offer.Firmware)Add("Manufacturer tools",()=>{updateCategory="oem";Navigate("drivers");});
   else{
    Add("Open Device Manager",()=>Commands.Open(Path.Combine(Environment.SystemDirectory,"devmgmt.msc")));
    if(Directory.Exists(backup))Add("Open driver backups",()=>Commands.Open(backup));
   }
   var panel=Stack(T(offer.Title,17,"#B9E2A7",true),T(body,14,"#B9CBC1"),actions);
   panel.Margin=new Thickness(20);
   window.Content=new ScrollViewer{Content=panel};
   window.ShowDialog();
   return Task.CompletedTask;
  });
 }
 private async Task InstallDriverOfferAsync(WindowsOffer offer)
 {
  if(!offer.SelectableDriver)return;
  Detail("Paket lisansı",offer.Eula);
  if(!Confirm(offer.Title+"\nÖnce sürücü yedeği alınır. Lisans koşullarını kabul edip kurmak istiyor musun? Ekran/ağ kesilebilir, yeniden başlatma gerekebilir."))return;
  Detail("Sürücü kurulumu",await driverService.InstallUpdateAsync(new(offer.Id,offer.Revision,offer.Title,offer.Manufacturer,offer.Model,offer.Eula,offer.Class)));
  windowsSelection.Clear();windowsScan=null;await ScanAsync();
 }

 private async Task RefreshHardwareInventoryAsync(CancellationToken token)
 {
  var found=inventory.DevicesAsync();
  var installed=inventory.DriversAsync();
  await Task.WhenAll(found,installed);
  token.ThrowIfCancellationRequested();
  devices=await found;
  drivers=await installed;
  hardwareInventoryReady=true;
 }

 private async Task ScanHardwareUpdatesAsync()
 {
  ShowUpdateScanAnimation(true);
  using var cancellation=new CancellationTokenSource();
  updateCancellation=cancellation;
  CancelOperation.Visibility=System.Windows.Visibility.Visible;
  updateWarnings.Clear();
  hardwareInventoryReady=false;
  windowsScan=null;
  windowsSelection.Clear();
  try{
   OperationTitle.Text="Reading hardware and Windows Update…";
   var local=RefreshHardwareInventoryAsync(cancellation.Token);
   var online=WindowsUpdateCatalog.ScanAsync(extendedUpdateScan,cancellation.Token);
   try{await local;}
   catch(Exception ex)when(ex is not OperationCanceledException){updateWarnings.Add("Hardware inventory unavailable: "+ex.Message);}
   try{windowsScan=await online;updateWarnings.AddRange(windowsScan.Warnings);}
   catch(Exception ex)when(ex is not OperationCanceledException){updateWarnings.Add("Windows Update unavailable: "+ex.Message);}
   foreach(var offer in WindowsUpdateCatalog.RecommendedDrivers(windowsScan?.Offers??[]))windowsSelection.Add(offer.Key);
   ShowUpdateScanAnimation(false);
   if(automaticDownloads&&windowsScan!=null){
    windowsSelection.RemoveWhere(key=>windowsScan.Offers.FirstOrDefault(o=>o.Key==key)?.EulaAccepted!=true);
    await DownloadSelectionAsync(true,true);
   }
  }
  finally{ShowUpdateScanAnimation(false);updateCancellation=null;CancelOperation.Visibility=System.Windows.Visibility.Collapsed;Navigate("drivers");}
 }

 private void RenderHardwareUpdates()
 {
  Page.Children.Add(Row(Action("Scan hardware and drivers",ScanHardwareUpdatesAsync,true)));
  if(!hardwareInventoryReady){
   Page.Children.Add(T("Scan to list connected devices, installed driver versions and Windows Update matches.",14,"#B9CBC1"));
   return;
  }
  var present=devices.Where(d=>d.Present&&!string.IsNullOrWhiteSpace(d.Id)).DistinctBy(d=>d.Id,StringComparer.OrdinalIgnoreCase).ToArray();
  var installed=drivers.ToLookup(d=>d.DeviceId,StringComparer.OrdinalIgnoreCase);
  var gpuTools=GpuSoftware.Inspect(present,drivers,software?.Apps??[]).Where(s=>s.HardwarePresent).ToDictionary(s=>s.Tool.Vendor,StringComparer.OrdinalIgnoreCase);
  var offers=(windowsScan?.Offers??[]).Where(o=>o.Kind=="Driver"&&!o.Hidden&&!o.Superseded&&!string.IsNullOrWhiteSpace(o.HardwareId))
   .ToLookup(o=>o.HardwareId,StringComparer.OrdinalIgnoreCase);
  var rows=present.Select(device=>{
   var matches=device.HardwareIds.Concat(device.CompatibleIds??[]).Distinct(StringComparer.OrdinalIgnoreCase)
    .SelectMany(id=>offers[id]).DistinctBy(o=>o.Key).OrderBy(o=>o.Optional).ToArray();
   return (Device:device,Driver:installed[device.Id].FirstOrDefault(),Matches:matches);
  }).OrderByDescending(r=>r.Device.Problem!=0).ThenByDescending(r=>r.Matches.Length>0).ThenBy(r=>r.Device.Class!="Display").ThenBy(r=>r.Device.Name).ToArray();
  Page.Children.Add(T($"{present.Length} connected devices · {rows.Count(r=>r.Device.Problem!=0)} with a device error · {rows.Count(r=>r.Matches.Length>0)} with a matched offer",13,"#B9E2A7"));
  Page.Children.Add(T("A missing Windows Update offer does not confirm the latest manufacturer version.",12,"#B9CBC1"));
  var search=new TextBox{MinHeight=38,ToolTip="Search device name, class or hardware ID"};
  var actionOnly=new CheckBox{Content="Needs attention only"};
  var group=new ComboBox{ItemsSource=driverReviewGroups,SelectedIndex=0,MinWidth=180};
  Page.Children.Add(search);
  Page.Children.Add(Row(group,actionOnly,Action("Manufacturer tools",()=>{updateCategory="oem";Navigate("drivers");return Task.CompletedTask;}),
   Action("Windows and driver offers",()=>{updateCategory="windows";Navigate("drivers");return Task.CompletedTask;})));
  var nav=Row();
  var cards=new StackPanel();
  Page.Children.Add(nav);
  Page.Children.Add(cards);
  int page=0;
  const int size=12;
  void Render()
  {
   nav.Children.Clear();cards.Children.Clear();
   var query=search.Text.Trim();
   var category=driverReviewGroups[Math.Max(0,group.SelectedIndex)];
   var filtered=rows.Where(r=>(actionOnly.IsChecked!=true||r.Device.Problem!=0||r.Matches.Length>0)&&
    (group.SelectedIndex<=0||r.Matches.Any(o=>o.ReviewGroup==category)||
     (group.SelectedIndex==3&&windowsScan!=null&&windowsScan.Warnings.Count==0&&r.Matches.Length==0))&&
    (r.Device.Name+" "+r.Device.Class+" "+r.Device.Id+" "+string.Join(" ",r.Device.HardwareIds)).Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
   var pages=Math.Max(1,(filtered.Length+size-1)/size);
   page=Math.Clamp(page,0,pages-1);
   nav.Children.Add(T($"{page+1} / {pages}",12,"#B9CBC1"));
   if(page>0)nav.Children.Add(Action("← Previous",()=>{page--;Render();return Task.CompletedTask;}));
   if(page+1<pages)nav.Children.Add(Action("Next →",()=>{page++;Render();return Task.CompletedTask;}));
   foreach(var (device,driver,matches) in filtered.Skip(page*size).Take(size)){
    var label=device.Name.Length==0?device.Id:device.Name;
    var state=device.Problem!=0?"Device error code: "+device.Problem:driver==null?"No signed driver record":
     string.IsNullOrWhiteSpace(driver.Version)?"Driver version unavailable":
     "Installed driver: "+driver.Version+(string.IsNullOrWhiteSpace(driver.Provider)?"":" · "+driver.Provider);
    var shown=matches.Where(o=>group.SelectedIndex<=0||o.ReviewGroup==category).ToArray();
    var actions=Row(Action("Device details",()=>{Detail(label,device.Id+"\n\n"+string.Join("\n",device.HardwareIds.Concat(device.CompatibleIds??[]).Distinct())+
     (driver==null?"":"\n\n"+driver.Provider+" / "+driver.Version+" / "+driver.Date+" / "+driver.Inf));return Task.CompletedTask;}));
    actions.Children.Add(Action("Search driver catalogue",()=>SearchCatalogDeviceAsync(device)));
    var gpu=GpuSoftware.Vendor(device);
    var support=gpu switch{
     "NVIDIA"=>"https://www.nvidia.com/en-us/drivers/",
     "AMD"=>"https://www.amd.com/en/support/download/drivers.html",
     "Intel"=>"https://www.intel.com/content/www/us/en/download-center/home.html",
     _=>null
    };
    if(support!=null)actions.Children.Add(Link("Official GPU drivers",support));
    if(gpuTools.TryGetValue(gpu,out var tool)){
     var recipe=AppDownloads.Recipes.Single(r=>r.Id==tool.Tool.Id);
     actions.Children.Add(Action("Get "+tool.Tool.Name,()=>DownloadGpuToolAsync(tool,recipe)));
    }
    var body=Stack(T(label,17,"#B9E2A7",true),T(state,13,device.Problem!=0?"#EBC078":"#B9CBC1"));
    if(shown.Length==0)body.Children.Add(T(windowsScan==null?"Windows Update check unavailable":
     windowsScan.Warnings.Count>0?"No matched offer in partial results":"Manufacturer check required · No matched Windows Update offer",12,"#B9CBC1"));
    foreach(var offer in shown){
     var offerActions=Row(DriverRollback(offer));
     if(offer.SelectableDriver){
      var queue=Action("Add to download queue",()=>{windowsSelection.Add(offer.Key);UpdateQueueSummary();return Task.CompletedTask;});
      queue.IsEnabled=!offer.Downloaded;
      offerActions.Children.Add(queue);
      offerActions.Children.Add(Action("Yedekle ve kur",()=>InstallDriverOfferAsync(offer)));
     }
     body.Children.Add(Stack(T(offer.ReviewGroup+" · "+offer.Title,13,"#B9E2A7",true),
      T("Installed version: "+InstalledVersions(driver==null?[]:[driver])+"  →  Offered version: "+
       (string.IsNullOrWhiteSpace(offer.Version)?"Version unavailable":offer.Version),12,"#B9CBC1"),offerActions));
    }
    body.Children.Add(actions);
    cards.Children.Add(Card(body));
   }
   if(filtered.Length==0)cards.Children.Add(T("No matching devices.",14,"#B9CBC1"));
  }
  search.TextChanged+=(_,_)=>{page=0;Render();};
  actionOnly.Checked+=(_,_)=>{page=0;Render();};
  actionOnly.Unchecked+=(_,_)=>{page=0;Render();};
  group.SelectionChanged+=(_,_)=>{page=0;Render();};
  Render();
 }
}
