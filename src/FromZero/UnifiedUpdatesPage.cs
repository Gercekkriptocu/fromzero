using System.Windows;
using System.Windows.Controls;

namespace FromZero;

public partial class MainWindow
{
 private sealed record UpdateRow(string Id,string Name,string Category,string Installed,string Available,string State,
  string Reason,bool Selectable,Func<bool> Selected,System.Action<bool> Select,Func<Task> Details,string Icon="");

 private List<UpdateRow> UnifiedUpdateRows()
 {
  var result=new List<UpdateRow>();
  foreach(var offer in windowsScan?.Offers.Where(o=>!o.Hidden&&!o.Superseded&&!o.Firmware)??[])
  {
   var matching=drivers.Where(driver=>devices.Any(device=>device.Id==driver.DeviceId&&WindowsUpdateCatalog.MatchesDevice(offer,device)));
   result.Add(new(offer.Key,offer.Title,offer.Kind=="Driver"?"Drivers":"Windows",
    offer.Kind=="Driver"?InstalledVersions(matching):Locale.Text("Pending Windows update"),
    string.IsNullOrWhiteSpace(offer.Version)?offer.Title:offer.Version,"Outdated",
    downloadStatus.GetValueOrDefault(offer.Key,offer.Downloaded?"Downloaded · ready to install":offer.Optional?"Optional update":"Update available"),
    offer.QueueInstallable,()=>windowsSelection.Contains(offer.Key),selected=>{if(selected)windowsSelection.Add(offer.Key);else windowsSelection.Remove(offer.Key);},
    ()=>{Detail(offer.Title,offer.Eula);return Task.CompletedTask;}));
  }
  foreach(var app in applicationScan?.Applications.Where(app=>app.Installed.Length>0)??[])
  {
   var advice=UpdateAssessment.For(app);
   var known=app.State=="Güncel veya daha yeni";
   var icon=software?.Apps.FirstOrDefault(record=>ApplicationCatalog.Matches(record,[app.Name]))?.DisplayIcon??"";
   result.Add(new(app.Id,app.Name,"Applications",string.Join(", ",app.Installed),app.Version,
    app.NeedsUpdate?"Outdated":known?"Up to date":"Needs review",downloadStatus.GetValueOrDefault(app.Id,advice.Reason),
    app.NeedsUpdate,()=>packageSelection.Contains(app.Id),selected=>{if(selected)packageSelection.Add(app.Id);else packageSelection.Remove(app.Id);},
    ()=>{Detail(app.Name,app.Source+"\n\n"+Locale.Text(advice.Reason));return Task.CompletedTask;},icon));
  }
  foreach(var runtime in runtimeScan?.Packages.Where(package=>package.Installed.Length>0)??[])
   result.Add(new(runtime.Id,runtime.Name+" · "+runtime.Architecture,"Runtimes",string.Join(", ",runtime.Installed),runtime.Version,
    runtime.NeedsUpdate?"Outdated":runtime.State=="Güncel veya daha yeni"?"Up to date":"Needs review",
    downloadStatus.GetValueOrDefault(runtime.Id,runtime.Supported?"Supported runtime release":"Unsupported runtime channel"),
    runtime.Supported&&runtime.NeedsUpdate,()=>packageSelection.Contains(runtime.Id),selected=>{if(selected)packageSelection.Add(runtime.Id);else packageSelection.Remove(runtime.Id);},
    ()=>{Detail(runtime.Name,runtime.Recipe.Url);return Task.CompletedTask;}));
  foreach(var component in (components?.Components??[]).Where(component=>!component.Present&&component.Recipe!=null).DistinctBy(component=>component.Recipe))
  {
   var recipe=AppDownloads.Recipes.FirstOrDefault(recipe=>recipe.Id==component.Recipe);
   if(recipe==null)continue;
   result.Add(new(recipe.Id,recipe.Name,"Game components",Locale.Text("Not installed"),recipe.Name,"Missing",
    downloadStatus.GetValueOrDefault(recipe.Id,component.Evidence),true,()=>packageSelection.Contains(recipe.Id),
    selected=>{if(selected)packageSelection.Add(recipe.Id);else packageSelection.Remove(recipe.Id);},
    ()=>{Detail(component.Name,component.Evidence);return Task.CompletedTask;}));
  }
  foreach(var device in devices.Where(device=>device.Present&&device.Problem!=0&&
   !(windowsScan?.Offers.Any(offer=>WindowsUpdateCatalog.MatchesDevice(offer,device))??false)))
   result.Add(new("device-"+device.Id,device.Name,"Drivers",Locale.Text("Device error code: ")+device.Problem,"Driver finder","Needs review",
    "Search by hardware ID to find a matching signed package.",false,()=>false,_=>{},()=>SearchCatalogDeviceAsync(device)));
  return result.DistinctBy(row=>row.Id).OrderBy(row=>row.State=="Up to date").ThenBy(row=>row.State=="Needs review")
   .ThenBy(row=>row.Category!="Drivers").ThenBy(row=>row.Name).ToList();
 }

 private void RenderUnifiedUpdateResults()
 {
  if(windowsScan==null&&runtimeScan==null&&applicationScan==null)
  {
   Page.Children.Add(Card(Stack(T("One scan. Your updates in one list.",23,"#B9E2A7",true),
    T("Scan to compare installed drivers, applications and runtimes. Missing components and outdated versions appear first.",14,"#B9CBC1"),
    Row(Action("Scan now",ScanAllUpdatesAsync,true)))));
   return;
  }
  var entries=UnifiedUpdateRows();
  var outdated=entries.Count(row=>row.State=="Outdated");
  var missing=entries.Count(row=>row.State=="Missing");
  var current=entries.Count(row=>row.State=="Up to date");
  Page.Children.Add(Card(Stack(T("Your update results",23,"#B9E2A7",true),
   T($"{outdated} "+Locale.Text("outdated")+" · "+missing+" "+Locale.Text("missing")+" · "+current+" "+Locale.Text("up to date"),14,"#B9CBC1"))));
  var status=new ComboBox{ItemsSource=new[]{Locale.Text("Updates and missing"),Locale.Text("Up to date"),Locale.Text("All results")},SelectedIndex=0,MinWidth=180};
  var categories=new[]{"All categories","Drivers","Windows","Applications","Runtimes","Game components"};
  var category=new ComboBox{ItemsSource=categories.Select(Locale.Text).ToArray(),SelectedIndex=0,MinWidth=160};
  var search=new TextBox{MinHeight=38,ToolTip=Locale.Text("Search update results")};
  Page.Children.Add(Row(status,category));Page.Children.Add(search);
  var listing=new StackPanel();var navigation=new WrapPanel();Page.Children.Add(listing);Page.Children.Add(navigation);
  int page=0;
  void Render()
  {
   listing.Children.Clear();navigation.Children.Clear();
   var filtered=entries.Where(row=>(status.SelectedIndex==2||(status.SelectedIndex==1?row.State=="Up to date":row.State!="Up to date"))&&
    (category.SelectedIndex==0||row.Category==categories[category.SelectedIndex])&&
    (row.Name+" "+row.Installed+" "+row.Available).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
   page=Math.Clamp(page,0,Math.Max(0,(filtered.Length-1)/12));
   foreach(var row in filtered.Skip(page*12).Take(12))
   {
    var selected=new CheckBox{Content=Locale.Text(row.Category),IsChecked=row.Selected(),IsEnabled=row.Selectable};
    selected.Checked+=(_,_)=>{row.Select(true);UpdateQueueSummary();};selected.Unchecked+=(_,_)=>{row.Select(false);UpdateQueueSummary();};
    var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var text=Stack(AppIcons.Heading(row.Name,row.Icon,selected),
     T(Locale.Text(row.State),13,row.State=="Up to date"?"#B9E2A7":"#EBC078",true),
     T(Locale.Text("Installed: ")+row.Installed+"\n"+Locale.Text("Available: ")+row.Available,12,"#B9CBC1"),
     T(row.Reason,12,"#9AAFAE"));
    layout.Children.Add(text);
    var details=Action(row.Id.StartsWith("device-",StringComparison.Ordinal)?"Find driver":"Details",row.Details);
    details.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(details,1);layout.Children.Add(details);
    listing.Children.Add(Card(layout));
   }
   if(filtered.Length==0)listing.Children.Add(Card(T("No results in this filter.",14,"#B9CBC1")));
   navigation.Children.Add(T($"{page+1} / {Math.Max(1,(filtered.Length+11)/12)}",12,"#B9CBC1"));
   if(page>0)navigation.Children.Add(Action("← Previous",()=>{page--;Render();return Task.CompletedTask;}));
   if((page+1)*12<filtered.Length)navigation.Children.Add(Action("Next →",()=>{page++;Render();return Task.CompletedTask;}));
   UiTheme.ApplyTree(listing);
  }
  void Filter(){page=0;Render();}
  status.SelectionChanged+=(_,_)=>Filter();category.SelectionChanged+=(_,_)=>Filter();search.TextChanged+=(_,_)=>Filter();Render();
 }
}
