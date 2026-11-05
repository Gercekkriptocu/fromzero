using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace FromZero;

public partial class MainWindow
{
 private readonly ActivationService activationService=new();
 private ActivationSnapshot? activationSnapshot;
 private ActivationResult? activationResult;
 private string activationDetails="";
 private string activationTab="windows";

 private async Task RefreshActivationAsync()
 {
  activationSnapshot=await activationService.InspectAsync();
  Navigate("activation");
 }

 private void Activation()
 {
  Header("MORE TOOLS / ACTIVATOR","Windows & Office activation","Inspect installed licenses and run the selected activation method.");
  var tabs=Row(Action("Refresh license status",RefreshActivationAsync,true));
  foreach(var (id,title) in new[]{("windows","Windows"),("office","Office / Microsoft 365"),("analysis","Analysis")}){
   var tab=id;
   tabs.Children.Add(Action(title,()=>{activationTab=tab;Navigate("activation");return Task.CompletedTask;},activationTab==id));
  }
  Page.Children.Add(tabs);
  var snapshot=activationSnapshot;
  Page.Children.Add(T(snapshot==null?"Scan the license status to enable activation actions.":
   "Last licensing check: "+snapshot.Checked.ToLocalTime().ToString("g"),12,"#B9CBC1"));
  if(snapshot?.Warnings.Count>0)Page.Children.Add(PreparationExpander("Inventory warnings",T(string.Join("\n",snapshot.Warnings),12,"#EBC078")));
  if(activationResult is { } result){
   var color=result.Verification is ActivationVerification.Verified or ActivationVerification.AlreadyLicensed?"#B9E2A7":"#EBC078";
   var message=ActivationPolicy.VerificationMessage(result.Verification);
   Page.Children.Add(Card(Stack(T(result.Method,18,"#B9E2A7",true),T(message,13,color),
    result.VerificationError==null?T("License status was checked again after the operation.",12,"#B9CBC1"):
     T("Post-operation verification unavailable: "+result.VerificationError,12,"#EBC078"),
    PreparationExpander("Operation output",ActivationOutput(result.Output)))));
  }
  if(activationTab=="analysis"){
   Page.Children.Add(Card(Stack(T("Read-only analysis",19,"#B9E2A7",true),
    T("Shows licensing records, the installed key suffix, grace time and selected licensing configuration.",13,"#B9CBC1"),
    Row(ActivationAction("Read detailed licensing report",async()=>{
     activationDetails=await activationService.DetailsAsync(activationSnapshot!);Navigate("activation");
    },snapshot!=null),ActivationAction("Export license report",ExportActivationAsync,snapshot!=null)))));
   if(snapshot!=null){
    var config=Stack(T("Licensing configuration",18,"#B9E2A7",true));
    foreach(var entry in snapshot.Registry)config.Children.Add(T(entry.Name+" · "+entry.Value,12,"#B9CBC1"));
    if(snapshot.Registry.Count==0)config.Children.Add(T("No explicit KMS override was found in the inspected configuration.",12,"#B9CBC1"));
    Page.Children.Add(Card(config));
   }
   var methods=Enum.GetValues<ActivationMethod>();
   var choose=new ComboBox{ItemsSource=methods.Select(ActivationPolicy.Title).ToArray(),SelectedIndex=0,MinHeight=36};
   Page.Children.Add(Card(Stack(T("Simulation",18,"#B9E2A7",true),choose,Row(Action("Preview selected method",()=>{
    var method=methods[choose.SelectedIndex];
    Detail("Simulation",ActivationPolicy.Title(method)+"\n\n"+ActivationDescription(method)+"\n\n"+
     "This preview only describes the selected operation.");
    return Task.CompletedTask;
   })))));
   if(activationDetails.Length>0)Page.Children.Add(PreparationExpander("Detailed licensing report",ActivationOutput(activationDetails)));
   return;
  }
  var windows=activationTab=="windows";
  var kind=windows?"Windows":"Office";
  var products=snapshot?.Products.Where(p=>p.Kind==kind&&(!windows||!p.Addon)).ToArray()??[];
  var records=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=300,Gap=12};
  foreach(var product in products){
   var grace=product.GraceMinutes>0?"Grace remaining (minutes): "+product.GraceMinutes:"";
   records.Children.Add(Card(Stack(T(product.Name,17,"#B9E2A7",true),
    T(ActivationPolicy.Status(product.Status),20,product.Status==1?"#B9E2A7":"#EBC078",true),
    T(product.Description,12,"#B9CBC1"),T("Key suffix: "+product.PartialKey,12,"#B9CBC1"),
    T(product.Channel,12,"#B9CBC1"),T(grace,12,"#B9CBC1"))));
  }
  Page.Children.Add(records);
  if(snapshot!=null&&products.Length==0)Page.Children.Add(T(windows?"No Windows product with an installed key was returned.":
   snapshot.OfficeInstalled?"Office is installed. Subscription licensing may require the detailed report.":"Office was not detected in the standard installation locations.",13,"#EBC078"));
  if(windows){
   Page.Children.Add(Card(Stack(T("Windows activation",19,"#B9E2A7",true),
    T(ActivationDescription(ActivationMethod.WindowsLicense),13,"#B9CBC1"),
    Row(ActivationAction(ActivationPolicy.Title(ActivationMethod.WindowsLicense),()=>ActivateAsync(ActivationMethod.WindowsLicense),snapshot!=null),
     Link("Windows activation settings","ms-settings:activation")))));
   Page.Children.Add(Card(Stack(T("HWID",19,"#B9E2A7",true),T(ActivationDescription(ActivationMethod.WindowsHwid),13,"#B9CBC1"),
    Row(ActivationAction(ActivationPolicy.Title(ActivationMethod.WindowsHwid),()=>ActivateAsync(ActivationMethod.WindowsHwid),snapshot!=null)))));
   Page.Children.Add(PreparationExpander("Advanced licensing maintenance",Stack(
    T(ActivationDescription(ActivationMethod.WindowsRearm),13,"#EBC078"),
    T("Remaining Windows rearms: "+(snapshot is{RemainingRearms:>=0}?snapshot.RemainingRearms.ToString():"Unknown"),12,"#B9CBC1"),
    Row(ActivationAction(ActivationPolicy.Title(ActivationMethod.WindowsRearm),()=>ActivateAsync(ActivationMethod.WindowsRearm),snapshot is{RemainingRearms:>0})))));
  }
  else{
   var installations=snapshot?.OfficeInstallations??[];
   foreach(var installation in installations){
    var names=installation.Products.Select(ActivationPolicy.OfficeProductName).Distinct().ToArray();
    var label=names.Length>0?string.Join(" · ",names):"Detected Office installation";
    Page.Children.Add(Card(Stack(T(label,18,"#B9E2A7",true),
     T(installation.Source+" · "+installation.Version+" · "+installation.Architecture,12,"#B9CBC1"),
     T(string.Join(" · ",installation.Applications.Select(System.IO.Path.GetFileNameWithoutExtension)),12,"#B9CBC1"),
     T(installation.Path,11,"#9AAFAE"))));
   }
   foreach(var license in snapshot?.Subscriptions??[]){
    Page.Children.Add(Card(Stack(T(license.Product,17,"#B9E2A7",true),
     T("Microsoft 365 license state: "+license.State,15,license.State.Equals("Licensed",StringComparison.OrdinalIgnoreCase)?"#B9E2A7":"#EBC078"))));
   }
   Page.Children.Add(Card(Stack(T("Office / Microsoft 365",19,"#B9E2A7",true),
    T("Detects Microsoft 365, Office LTSC 2024 / 2021, Office 2019 / 2016, Project and Visio in x86 and x64 installations.",12,"#B9CBC1"),
    T(ActivationDescription(ActivationMethod.OfficeLicense),13,"#B9CBC1"),
    Row(ActivationAction(ActivationPolicy.Title(ActivationMethod.OfficeLicense),()=>ActivateAsync(ActivationMethod.OfficeLicense),snapshot?.OfficeScripts.Count>0)))));
   Page.Children.Add(Card(Stack(T("Ohook",19,"#B9E2A7",true),T(ActivationDescription(ActivationMethod.OfficeOhook),13,"#B9CBC1"),
    Row(ActivationAction(ActivationPolicy.Title(ActivationMethod.OfficeOhook),()=>ActivateAsync(ActivationMethod.OfficeOhook),snapshot?.OfficeInstalled==true),
     ActivationAction(ActivationPolicy.Title(ActivationMethod.RemoveOhook),()=>ActivateAsync(ActivationMethod.RemoveOhook),snapshot?.OfficeInstalled==true)))));
   Page.Children.Add(T("Confirm the final Office status in File → Account after reopening Office.",12,"#B9CBC1"));
  }
 }

 private Button ActivationAction(string title,Func<Task> action,bool enabled)
 {
  var button=Action(title,action);button.IsEnabled=enabled;return button;
 }

 private static TextBox ActivationOutput(string output)=>new(){Text=output,Tag="FromZero.RawText",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,
  MaxHeight=330,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};

 private static string ActivationDescription(ActivationMethod method)=>method switch{
  ActivationMethod.WindowsLicense=>"Asks Windows to activate the currently installed license using its existing activation configuration.",
  ActivationMethod.OfficeLicense=>"Runs the installed Office volume-license activation tool. Microsoft 365 subscriptions use a different licensing system.",
  ActivationMethod.WindowsHwid=>"Downloads and verifies MAS 3.12, then runs HWID activation. This can change the installed Windows key and digital activation state. An internet connection is required.",
  ActivationMethod.OfficeOhook=>"Downloads and verifies MAS 3.12, then installs Ohook components and changes Office licensing configuration. Close Office before continuing.",
  ActivationMethod.RemoveOhook=>"Uses the same verified engine to remove Ohook components. Office may require activation again.",
  ActivationMethod.WindowsRearm=>"Resets the Windows licensing grace state and uses one available rearm. A Windows restart may be required.",
  _=>throw new InvalidDataException("Unknown activation method.")
 };

 private async Task ActivateAsync(ActivationMethod method)
 {
  var title=ActivationPolicy.Title(method);
  if(!Confirm(Locale.Text(ActivationDescription(method))+"\n\n"+Locale.Text("Run this operation?"),Locale.Text(title)))return;
  OperationTitle.Text=Locale.Text("Waiting for activation result…");
  activationResult=await activationService.ExecuteAsync(method);
  activationSnapshot=activationResult.After;
  activationDetails="";
  operationCompletion=Locale.Text(ActivationPolicy.VerificationMessage(activationResult.Verification));
  Navigate("activation");
 }

 private async Task ExportActivationAsync()
 {
  var dialog=new SaveFileDialog{FileName="FromZero-activation.json",Filter="JSON|*.json"};
  if(dialog.ShowDialog(this)!=true)return;
  await File.WriteAllTextAsync(dialog.FileName,JsonSerializer.Serialize(activationSnapshot,LocalStore.Json));
 }
}
