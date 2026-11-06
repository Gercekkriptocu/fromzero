using System.Windows;
using System.Windows.Controls;

namespace FromZero;

public partial class MainWindow
{
 private string catalogDeviceId="";
 private readonly Dictionary<string,IReadOnlyList<CatalogDriverCandidate>> catalogResults=new(StringComparer.OrdinalIgnoreCase);
 private readonly Dictionary<string,IReadOnlyList<CatalogDriverReview>> catalogReviews=new(StringComparer.OrdinalIgnoreCase);
 private readonly Dictionary<string,CatalogDriverReview> catalogDriverQueue=new(StringComparer.OrdinalIgnoreCase);
 private readonly Dictionary<string,string> catalogMessages=new(StringComparer.OrdinalIgnoreCase);
 private static string CatalogKey(DeviceInfo device,CatalogDriverCandidate candidate)=>device.Id+"/"+candidate.Id;

 private void RenderCatalogDrivers()
 {
  var connected=devices.Where(device=>device.Present&&device.HardwareIds.Length>0).OrderByDescending(device=>device.Problem!=0)
   .ThenBy(device=>device.Class!="Display").ThenBy(device=>device.Name).ToArray();
  if(connected.Length==0)
  {
   Page.Children.Add(Card(Stack(T("Read hardware to find driver matches.",16),Row(Action("Read hardware",async()=>{
    await RefreshHardwareInventoryAsync(default);Navigate("drivers");
   })))));
   return;
  }
  if(!connected.Any(device=>device.Id==catalogDeviceId))catalogDeviceId=connected[0].Id;
  var picker=new ComboBox{ItemsSource=connected.Select(device=>device.Name+" · "+device.Class).ToArray(),
   SelectedIndex=Array.FindIndex(connected,device=>device.Id==catalogDeviceId),MinHeight=38};
  picker.SelectionChanged+=(_,_)=>{if(picker.SelectedIndex>=0){catalogDeviceId=connected[picker.SelectedIndex].Id;Navigate("drivers");}};
  Page.Children.Add(Card(Stack(T("Find drivers by hardware ID",20,"#B9E2A7",true),
   T("Search the Microsoft Update Catalog for this device. Download a candidate to check its INF, architecture and signed catalogue before adding it to the install queue.",13,"#B9CBC1"),
   picker,Row(Action("Search driver catalogue",()=>SearchCatalogDeviceAsync(connected.Single(device=>device.Id==catalogDeviceId)),true)))));
  var device=connected.Single(device=>device.Id==catalogDeviceId);
  var installed=drivers.FirstOrDefault(driver=>driver.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
  Page.Children.Add(T(device.HardwareIds[0]+"\n"+Locale.Text("Installed driver: ")+(installed?.Version??Locale.Text("Not detected")),12,"#B9CBC1"));
  if(catalogMessages.TryGetValue(device.Id,out var message))Page.Children.Add(T(message,13,"#EBC078"));
  if(!catalogResults.TryGetValue(device.Id,out var candidates))return;
  var cards=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=340,Gap=12};
  foreach(var candidate in candidates)
  {
   var key=CatalogKey(device,candidate);
   var body=Stack(T(candidate.Title,16,"#B9E2A7",true),T(candidate.Architecture+" · "+candidate.Version+" · "+candidate.Date,12,"#B9CBC1"),
    T(candidate.HardwareId,11,"#9AAFAE"),T(candidate.Products,11,"#9AAFAE"),
    Row(Action("Download and review driver",()=>ReviewCatalogDriverAsync(device,candidate)),Link("Package details",candidate.DetailsUrl)));
   if(catalogReviews.TryGetValue(key,out var reviews))
   {
    if(reviews.Count==0)body.Children.Add(T("No installable INF with this device's exact hardware ID was found.",12,"#EBC078"));
    foreach(var review in reviews)
    {
     var selected=catalogDriverQueue.TryGetValue(device.Id,out var queued)&&queued.Candidate.Id==candidate.Id&&queued.Package.Inf==review.Package.Inf;
     body.Children.Add(T(review.Package.Inf+"\n"+review.Match.Version+" · "+review.Package.Publisher,12,"#B9CBC1"));
     body.Children.Add(T(review.Match.Reason,12,review.Match.Installable?"#B9E2A7":"#EBC078"));
     if(review.Match.Installable)
      body.Children.Add(Row(Action(selected?"Remove from install queue":"Add to install queue",()=>{
       if(selected)catalogDriverQueue.Remove(device.Id);else catalogDriverQueue[device.Id]=review;
       Navigate("drivers");return Task.CompletedTask;
      },selected)));
    }
   }
   cards.Children.Add(Card(body));
  }
  Page.Children.Add(cards);
 }

 private async Task SearchCatalogDeviceAsync(DeviceInfo device)
 {
  ShowUpdateScanAnimation(true);
  using var cancellation=new CancellationTokenSource();
  updateCancellation=cancellation;CancelOperation.Visibility=Visibility.Visible;
  catalogDeviceId=device.Id;updateCategory="catalog";
  try
  {
   var candidates=await MicrosoftDriverCatalog.SearchAsync(device,new Progress<string>(message=>OperationTitle.Text=Locale.Text(message)),cancellation.Token);
   catalogResults[device.Id]=candidates;
   catalogMessages[device.Id]=Locale.Text(candidates.Count==0?"No candidates were found in this catalogue search. Try the manufacturer's support page.":
    "Search matches require package review. A newer version alone does not establish compatibility.");
   Journal.Write("Driver catalogue search",device.Name+" / "+candidates.Count+" candidates");
  }
  catch(Exception ex) when(ex is not OperationCanceledException)
  {
   catalogMessages[device.Id]=Locale.Text("Catalogue search could not be completed. Previous results were kept.")+" "+ex.Message;
   Journal.Write("Driver catalogue search",ex.Message,false);
  }
  finally{ShowUpdateScanAnimation(false);updateCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;Navigate("drivers");}
 }

 private async Task ReviewCatalogDriverAsync(DeviceInfo device,CatalogDriverCandidate candidate)
 {
  using var cancellation=new CancellationTokenSource();
  updateCancellation=cancellation;CancelOperation.Visibility=Visibility.Visible;
  try
  {
   var download=await MicrosoftDriverCatalog.DownloadAsync(candidate,new Progress<string>(message=>OperationTitle.Text=Locale.Text(message)),cancellation.Token);
   var installed=drivers.FirstOrDefault(driver=>driver.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
   var reviews=await CatalogDriverInstaller.ReviewAsync(candidate,device,installed,download);
   cancellation.Token.ThrowIfCancellationRequested();
   catalogReviews[CatalogKey(device,candidate)]=reviews;
   Journal.Write("Driver package reviewed",candidate.Title+" / "+reviews.Count+" matching INF files");
  }
  finally{updateCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;Navigate("drivers");}
 }

 private async Task<(bool Restart,bool Attention)> InstallCatalogQueueAsync(List<string> results,IProgress<string> progress,CancellationToken token)
 {
  bool attention=false;
  foreach(var item in catalogDriverQueue.ToArray())
  {
   token.ThrowIfCancellationRequested();
   try
   {
    var result=await CatalogDriverInstaller.InstallAsync(item.Value,progress,token);
    results.Add(item.Value.Device.Name+" · "+result.Detail);
    if(result.Verified)catalogDriverQueue.Remove(item.Key);
    attention|=!result.Verified;
    if(result.Restart)return(true,attention);
   }
   catch(Exception ex) when(ex is not OperationCanceledException){results.Add(item.Value.Device.Name+" · "+ex.Message);attention=true;}
  }
  if(catalogDriverQueue.Count==0)hardwareInventoryReady=false;
  return(false,attention);
 }
}
