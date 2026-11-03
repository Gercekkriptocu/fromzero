using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace FromZero;
public partial class MainWindow
{
 private string appManagerQuery="";private int appManagerPage;
 private void Apps()
 {
  Header("APPLICATIONS","Your apps, under control.","Inspect installed software and launch the vendor's own uninstall flow. Nothing is removed during a scan.");
  var search=new TextBox{Text=appManagerQuery,ToolTip="Search by app or publisher",MinHeight=38};
  var showComponents=new CheckBox{Content="Show protected system components",IsChecked=false};
  var large=new CheckBox{Content="At least 1 GB reported"};var startupOnly=new CheckBox{Content="Starts with Windows"};
  var sort=new ComboBox{ItemsSource=new[]{"Name","Largest first"},SelectedIndex=0,MinWidth=150};
  var runningPaths=InventoryService.Processes().Take(250).Select(p=>AppIcons.ProcessPath(p.Id)).Where(p=>p.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  var startupEntries=InventoryService.Startup();
  var signals=(software?.Apps??[]).ToDictionary(a=>a,a=>AppLifecycle.Inspect(a,runningPaths,startupEntries));
  Page.Children.Add(Card(Stack(T("Installed applications",21,"#B9E2A7",true),T($"{software?.Apps.Count??0} records · sizes are estimates from the installer",12,"#B9CBC1"),search,showComponents,Row(Action("Refresh applications",async()=>{software=await InstalledSoftware.ReadAsync();Navigate("apps");}),Action("Windows Installed Apps",()=>{Commands.Open("ms-settings:appsfeatures");return Task.CompletedTask;}),Action("Export list",async()=>{var dialog=new SaveFileDialog{FileName="FromZero-apps.json",Filter="JSON|*.json"};if(dialog.ShowDialog(this)==true)await File.WriteAllTextAsync(dialog.FileName,JsonSerializer.Serialize(software,LocalStore.Json));})))));
  if(!string.IsNullOrWhiteSpace(software?.Warning))Page.Children.Add(T(software.Warning,12,"#EBC078"));
  Page.Children.Add(Row(large,startupOnly,sort));
  var paging=Row();var results=new StackPanel();Page.Children.Add(paging);Page.Children.Add(results);
  void Render(){results.Children.Clear();paging.Children.Clear();appManagerQuery=search.Text.Trim();
   var matches=signals.Keys.Where(a=>(showComponents.IsChecked==true||!signals[a].Shared)&&(large.IsChecked!=true||a.EstimatedBytes>=1073741824)&&(startupOnly.IsChecked!=true||signals[a].StartupEntries>0)&&(appManagerQuery.Length==0||(a.Name+" "+a.DisplayName+" "+a.Publisher).Contains(appManagerQuery,StringComparison.OrdinalIgnoreCase)));
   var filtered=(sort.SelectedIndex==1?matches.OrderByDescending(a=>a.EstimatedBytes).ThenBy(InstalledSoftware.DisplayLabel):matches.OrderBy(InstalledSoftware.DisplayLabel)).ToArray();
   appManagerPage=Math.Clamp(appManagerPage,0,Math.Max(0,(filtered.Length-1)/16));
   paging.Children.Add(T($"{filtered.Length} apps · Page {appManagerPage+1} / {Math.Max(1,(filtered.Length+15)/16)}",12,"#B9CBC1"));
   if(appManagerPage>0)paging.Children.Add(Action("← Previous",()=>{appManagerPage--;Render();return Task.CompletedTask;}));if((appManagerPage+1)*16<filtered.Length)paging.Children.Add(Action("Next →",()=>{appManagerPage++;Render();return Task.CompletedTask;}));
   foreach(var app in filtered.Skip(appManagerPage*16).Take(16)){
    var item=app;var label=InstalledSoftware.DisplayLabel(item);var removable=AppUninstaller.CanRemove(item);
    var actions=Row(Action("Details",()=>{Detail(label,$"Version: {item.Version}\nPublisher: {item.Publisher}\nInstalled: {item.InstallDate}\nEstimated size: {(item.EstimatedBytes>0?SizeLabel(item.EstimatedBytes):"Not reported")}\nLocation: {item.InstallLocation}\nInventory: {item.Source}\n\n"+(removable?"The vendor uninstaller will open. Review its prompts; your user data may also be removed if you choose that option.":"No supported remover is available here. Use Windows Installed Apps."));return Task.CompletedTask;}));
    var uninstall=Action("Uninstall",async()=>{
     if(!Confirm(label+"\n\nThe vendor or Windows uninstall flow will remove this application. Save your work and review any data-removal options. Continue?","Uninstall application"))return;
     var report=await AppUninstaller.RemoveAsync(item);software=report.Inventory;Navigate("apps");
     var remaining=report.Removed==true?AppLifecycle.RemainingFiles(item,software.Apps):[];
     Detail("Uninstall result",report.Message+(remaining.Count==0?"":"\n\nRemaining entries in the recorded installation folder (preview only)\n"+string.Join("\n",remaining)));
    });uninstall.IsEnabled=removable;actions.Children.Insert(0,uninstall);
    var evidence=signals[item];var activity=(evidence.Running?" · Running now":"")+(evidence.StartupEntries>0?" · Starts with Windows":"");
    results.Children.Add(Card(Stack(AppIcons.Heading(label,item.DisplayIcon.Length>0?item.DisplayIcon:item.Executable,T(label,18,"#D8E8D3",true)),T((item.Version.Length>0?item.Version+" · ":"")+(item.PackageFullName.Length>0?"Microsoft Store":item.Publisher)+" · "+(item.PackageFullName.Length>0?"MSIX / Store":"Desktop")+(item.EstimatedBytes>0?" · "+SizeLabel(item.EstimatedBytes):"")+activity,12,"#B9CBC1"),T(removable?"Vendor uninstall available":evidence.Shared?"Protected system or shared component":"Manage removal in Windows Settings",11,removable?"#B9E2A7":"#9AAFAE"),actions)));
   }
   if(filtered.Length==0)results.Children.Add(T("No matching applications.",14,"#9AAFAE"));
  }
  search.TextChanged+=(_,_)=>{appManagerPage=0;Render();};showComponents.Checked+=(_,_)=>Render();showComponents.Unchecked+=(_,_)=>Render();Render();
  large.Checked+=(_,_)=>Render();large.Unchecked+=(_,_)=>Render();startupOnly.Checked+=(_,_)=>Render();startupOnly.Unchecked+=(_,_)=>Render();sort.SelectionChanged+=(_,_)=>Render();
 }
}
