using System.Windows.Controls;
using System.Windows;
namespace FromZero;
public partial class MainWindow
{
 private NetworkQualityReport? connectionQuality;
 private GameRouteReport? gameRoute;
 private string gameRouteTarget="";
 private string connectionRepairSummary="";
 private async Task<NetworkQualityReport?> TryMeasureConnectionAsync(NetworkAdapterState adapter)
 {
  try{return await NetworkQuality.MeasureAsync(adapter,progress:new Progress<string>(s=>OperationTitle.Text=s));}
  catch(InvalidOperationException){return null;}
 }
 private void ConnectionQualityCard()
 {
  var adapters=(networkHealth?.Adapters??[]).Where(a=>a.Enabled&&a.Connected).ToList();
  var select=new ComboBox{ItemsSource=adapters,DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=180};
  if(adapters.Count>0){select.SelectedIndex=0;if(connectionQuality!=null&&adapters.Any(a=>a.Id==connectionQuality.AdapterId))select.SelectedValue=connectionQuality.AdapterId;}
  var actions=Row(Action("Measure connection quality",async()=>
  {
   networkHealth=await NetworkRepair.InspectAsync();
   var chosen=select.SelectedValue as string;
   var current=networkHealth.Adapters.FirstOrDefault(a=>a.Id==chosen&&a.Connected)??networkHealth.Adapters.FirstOrDefault(a=>a.Enabled&&a.Connected);
   if(current==null)throw new InvalidOperationException("No connected physical adapter found.");
   connectionQuality=await NetworkQuality.MeasureAsync(current,progress:new Progress<string>(s=>OperationTitle.Text=s));
   Navigate("network");
  },true));
  if(connectionRepairSummary.Length>0)actions.Children.Add(Action("Review repair comparison",()=>{Detail("Before and after repair",connectionRepairSummary);return Task.CompletedTask;}));
  var body=Stack(T("Connection quality",19,"#B9E2A7",true),T("Five probes each for the gateway, direct internet access and your configured DNS. The test leaves network settings as they are.",13,"#B9CBC1"));
  if(adapters.Count>0)body.Children.Add(select);
  body.Children.Add(actions);
  var routeInput=new TextBox{Text=gameRouteTarget,MaxLength=253,ToolTip="Game server hostname or IP"};
  routeInput.TextChanged+=(_,_)=>gameRouteTarget=routeInput.Text;
  var gameHint="Use the server address shown by the game or your host. Some servers block ICMP. This test measures the route to that address.";
  var diagnostics=new ResponsiveTilePanel{MaxColumns=3,MinimumTileWidth=215,Gap=10};
  void Diagnose(string title,string summary,string description,bool attention)
  {
   diagnostics.Children.Add(Card(Stack(T(title,16,"#B9E2A7",true),T(summary,13,attention?"#EBC078":"#B9CBC1"),
    Row(Action("Open diagnosis",()=>{Detail(title,description);return Task.CompletedTask;})))));
  }
  void RenderDiagnostics()
  {
   diagnostics.Children.Clear();
   var adapter=(networkHealth?.Adapters??[]).FirstOrDefault(a=>a.Id==(string?)select.SelectedValue)??networkHealth?.Adapters.FirstOrDefault();
   var local=adapter==null?"Adapter status not measured":adapter.Connected?"Adapter connected":"Adapter disconnected";
   var quality=connectionQuality?.AdapterId==adapter?.Id?connectionQuality:null;
   Diagnose("Adapter",(adapter==null?"":adapter.Name+"\n")+local+(quality==null?"":"\n"+NetworkQuality.ProbeSummary(quality.Gateway)),
    (adapter?.Name??"")+"\n"+local+"\n"+(quality==null?"Run the connection check to see gateway details.":quality.Gateway.Note+"\n"+string.Join("\n",quality.Advice)),
    adapter is{Connected:false}||quality?.Gateway.LossPercent>0);
   var internet=quality!=null?(quality.DirectTcp.Received>0?"Direct internet probe succeeded":"Direct internet probe failed"):
    networkHealth==null?"Internet access not measured":networkHealth.InternetWorks?"Direct internet probe succeeded":"Direct internet probe failed";
   Diagnose("Internet access",internet+(quality==null?"":"\n"+NetworkQuality.ProbeSummary(quality.DirectTcp)),quality==null?internet:NetworkQuality.Describe(quality),
    quality?.DirectTcp.LossPercent>0||networkHealth is{InternetWorks:false});
   var game=gameRoute;
   var gameSummary=game==null?"Game route not measured":game.Target+"\n"+NetworkQuality.ProbeSummary(game.Probes);
   Diagnose("Game latency",gameSummary,game==null?gameHint:gameSummary+"\n"+game.Address+"\n"+game.Probes.Note+"\n"+gameHint,
    game!=null&&(game.Probes.LossPercent>0||game.Probes.JitterMs>=15));
   UiTheme.ApplyTree(diagnostics);
  }
  select.SelectionChanged+=(_,_)=>RenderDiagnostics();
  RenderDiagnostics();
  body.Children.Add(diagnostics);
  body.Children.Add(PreparationExpander("Test a game server",Stack(T(gameHint,12,"#B9CBC1"),T("Game server hostname or IP",12,"#B9CBC1"),routeInput,
   Row(Action("Measure game route",async()=>{
    using var cancel=new CancellationTokenSource();updateCancellation=cancel;CancelOperation.Visibility=Visibility.Visible;
    try{gameRoute=await NetworkQuality.MeasureGameRouteAsync(routeInput.Text,cancel.Token,new Progress<string>(s=>OperationTitle.Text=s));Navigate("network");}
    finally{updateCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;}
   })))));
  Page.Children.Add(Card(body));
 }
}
