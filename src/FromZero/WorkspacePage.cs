using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FromZero;
public partial class MainWindow
{
 private List<SourceAttempt> sourceAttempts=[];
 private void Workspace()
 {
  Header("WORKSPACE","Preferences and verification","Adjust reading size, review source checks and preview a local support report.");
  ShowFromZeroUpdateCard();
  var sizes=new ComboBox{ItemsSource=new[]{"100%","115%","130%"},SelectedIndex=UiTheme.TextScale==1.3?2:UiTheme.TextScale==1.15?1:0,MinWidth=110};
  System.Windows.Automation.AutomationProperties.SetName(sizes,"Text scale");
  sizes.SelectionChanged+=(_,_)=>{if(sizes.SelectedIndex>=0)UiTheme.SetTextScale(new[]{1d,1.15,1.3}[sizes.SelectedIndex]);};
  Page.Children.Add(Card(Stack(T("Text scale",19,"#B9E2A7",true),T("Choose a reading size. The retro font and language preference remain available.",13,"#B9CBC1"),Row(sizes))));
  var version=typeof(MainWindow).Assembly.GetName().Version??new Version(0,20,4);
  async Task CheckPackage(bool candidate)
  {
   var folder=candidate?PickFolder("Select an extracted FromZero update bundle"):AppContext.BaseDirectory;
   if(folder==null)return;
   var report=await ReleaseIntegrity.InspectAsync(folder,version,candidate);
   Detail("Release verification",$"Version: {report.Version}\nFiles: {report.Files}\nIntegrity: {report.IntegrityOk}\nPublisher signature: {report.PublisherVerified}\n"+(candidate?$"Newer verified release: {report.UpdateEligible}\n":"")+"\n"+string.Join("\n",report.Findings.Take(40)));
  }
  Page.Children.Add(Card(Stack(T("Release integrity",19,"#B9E2A7",true),T("Check the installed package or inspect a newer extracted bundle. Trusted update verification uses a pinned publisher key.",13,"#B9CBC1"),Row(Action("Verify this package",()=>CheckPackage(false)),Action("Inspect update bundle",()=>CheckPackage(true))))));
  Page.Children.Add(Card(Stack(T("Private support report",19,"#B9E2A7",true),T("Preview counts and status only. Names, paths, IP addresses, device IDs and raw logs are excluded.",13,"#B9CBC1"),Row(Action("Preview support report",PreviewSupportReportAsync,true)))));
  SourceHealthCard();
 }
 private void SourceHealthCard()
 {
  var panel=Stack(T("Source health",19,"#B9E2A7",true),T("Shows recent metadata and validation checks from update scans and the crosshair catalogue.",13,"#B9CBC1"));
  var list=Stack();var problems=new CheckBox{Content="Show source failures only"};
  void Render()
  {
   list.Children.Clear();
   list.Children.Add(T($"{sourceAttempts.Count} sources checked · {sourceAttempts.Count(s=>s.Status=="Unavailable")} unavailable",13,"#B9CBC1"));
   foreach(var source in sourceAttempts.Where(s=>problems.IsChecked!=true||s.Status=="Unavailable").Take(60))
    list.Children.Add(Stack(T(source.Host+" · "+source.Status,13,source.Status=="Unavailable"?"#EBC078":"#B9E2A7",true),T(source.Address+"\n"+source.Checked.ToLocalTime().ToString("g")+(source.LastSuccess is {} good?" · Last verified: "+good.ToLocalTime().ToString("g"):"")+" · "+source.Detail,12,"#B9CBC1")));
  }
  problems.Checked+=(_,_)=>Render();problems.Unchecked+=(_,_)=>Render();
  panel.Children.Add(Row(Action("Load source checks",async()=>{sourceAttempts=await Task.Run(SourceHealth.Read);Render();}),problems));panel.Children.Add(list);Render();Page.Children.Add(Card(panel));
 }
 private async Task PreviewSupportReportAsync()
 {
  var sources=await Task.Run(SourceHealth.Read);
  var report=SupportReport.Build(typeof(MainWindow).Assembly.GetName().Version?.ToString()??"",devices,drivers,software?.Apps.Count??0,preparationJob,sources,connectionQuality);
  var text=JsonSerializer.Serialize(report,LocalStore.Json);
  var window=CreateDialog("Support report preview",760,590);var panel=new DockPanel{Margin=new Thickness(18)};
  var footer=Row();DockPanel.SetDock(footer,Dock.Bottom);panel.Children.Add(footer);
  var save=new Button{Content="Save this report"};save.Click+=async(_,_)=>
  {
   var dialog=new SaveFileDialog{FileName="FromZero-support.json",Filter="JSON|*.json"};if(dialog.ShowDialog(window)!=true)return;
   await File.WriteAllTextAsync(dialog.FileName,text);window.Close();
  };
  footer.Children.Add(save);
  panel.Children.Add(new TextBox{Text=text,Tag="FromZero.RawText",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  window.Content=panel;window.ShowDialog();
 }
}
