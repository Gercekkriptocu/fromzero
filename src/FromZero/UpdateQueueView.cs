using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FromZero;

internal sealed class UpdateQueueView : IDisposable
{
 private sealed record Entry(string Key,string Title,int Order,Border Visual,TextBlock Name,TextBlock State)
 {
  public bool Finished {get;set;}
 }
 private readonly Border host;
 private readonly List<Entry> entries=[];
 private readonly StackPanel list=new();
 private readonly TextBlock current=Text("",19,"#B9E2A7");
 private readonly TextBlock next=Text("",12,"#B9CBC1");
 private readonly TextBlock count=Text("",12,"#B9CBC1");
 private readonly TextBlock elapsed=Text("",12,"#9AAFAE");
 private readonly ProgressBar completion=new(){Height=5,Margin=new(0,0,0,10)};
 private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(700)};
 private string? currentKey;
 private string phase="";
 private long phaseStarted=Stopwatch.GetTimestamp();
 private bool stopping;

 public UpdateQueueView(Border host,IEnumerable<(string Key,string Title,int Order)> items,Action stop)
 {
  this.host=host;
  var root=new DockPanel();
  var header=new StackPanel{Children={Text("Update queue",23,"#DCE9DD"),current,next,elapsed,count,completion}};
  DockPanel.SetDock(header,Dock.Top);
  root.Children.Add(header);
  var stopButton=new Button{Content=Locale.Text("Stop update queue"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,12,0,0)};
  stopButton.Click+=(_,_)=>{stopping=true;stopButton.IsEnabled=false;UpdateClock();stop();};
  DockPanel.SetDock(stopButton,Dock.Bottom);
  root.Children.Add(stopButton);
  root.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
   HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  foreach(var (key,title,order) in items.DistinctBy(item=>item.Key))
  {
   var name=Text(title,14,"#E8EEE5");
   var state=Text("Waiting in queue",12,"#9AAFAE");
   var row=new Border{Background=Brush("#1A2E37"),CornerRadius=new(8),Padding=new(10,6,10,2),Margin=new(0,0,6,6),
    Child=new StackPanel{Children={name,state}}};
   entries.Add(new(key,title,order,row,name,state));
   list.Children.Add(row);
  }
  host.Child=root;
  host.Visibility=Visibility.Visible;
  Stage(null,"Preparing the update queue…");
  timer.Tick+=(_,_)=>UpdateClock();
  timer.Start();
 }

 private static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
 private static TextBlock Text(string text,int size,string color)=>new()
 {
  Text=Locale.Text(text),FontFamily=UiTheme.Font,FontSize=size,Foreground=Brush(color),
  TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,5)
 };
 public void Stage(string? key,string status)
 {
  var changed=currentKey!=key||phase!=status;
  currentKey=key;
  phase=status;
  if(changed)phaseStarted=Stopwatch.GetTimestamp();
  var entry=entries.Find(item=>item.Key==key);
  current.Text=entry==null?Locale.Text(status):entry.Title+" · "+Locale.Text(status);
  if(changed&&entry!=null)entry.Visual.BringIntoView();
  if(entry!=null&&!entry.Finished)
  {
   entry.State.Text=Locale.Text(status);
   entry.State.Foreground=Brush("#B9E2A7");
  }
  UpdateSummary();
 }
 public void Detail(string key,string status)
 {
  if(currentKey!=key)return;
  var entry=entries.Find(item=>item.Key==key);
  if(entry==null||entry.Finished)return;
  entry.State.Text=Locale.Text(status);
  current.Text=entry.Title+" · "+entry.State.Text;
 }
 public void Replace(string oldKey,string newKey,string title)
 {
  var index=entries.FindIndex(item=>item.Key==oldKey);
  if(index<0)return;
  if(entries.Any(item=>item.Key==newKey)){Finish(oldKey,"Windows no longer offers this update");return;}
  var entry=entries[index];
  entry.Name.Text=title;
  entries[index]=entry with{Key=newKey,Title=title};
  UpdateSummary();
 }
 public void Defer(string key)
 {
  var index=entries.FindIndex(item=>item.Key==key);
  if(index<0||entries[index].Order!=0)return;
  var entry=entries[index] with{Order=1};
  entries.RemoveAt(index);
  list.Children.Remove(entry.Visual);
  var insert=entries.FindIndex(item=>item.Order>1);
  if(insert<0)insert=entries.Count;
  entries.Insert(insert,entry);
  list.Children.Insert(insert,entry.Visual);
  entry.State.Text=Locale.Text("Waiting for the signed driver retry");
  entry.State.Foreground=Brush("#EBC078");
  UpdateSummary();
 }
 public void Finish(string key,string status,bool verified=false)
 {
  var entry=entries.Find(item=>item.Key==key);
  if(entry==null)return;
  entry.Finished=true;
  entry.State.Text=Locale.Text(status);
  entry.State.Foreground=Brush(verified?"#B9E2A7":"#EBC078");
  UpdateSummary();
 }
 public string[] PendingTitles()=>entries.Where(entry=>!entry.Finished).Select(entry=>entry.Title).ToArray();

 private void UpdateSummary()
 {
  var queued=entries.FirstOrDefault(item=>!item.Finished&&item.Key!=currentKey);
  next.Text=queued==null?Locale.Text("No other package waiting"):Locale.Text("Up next")+" · "+queued.Title;
  var completed=entries.Count(entry=>entry.Finished);
  count.Text=string.Format(Locale.Culture,Locale.Text("{0} of {1} packages processed"),completed,entries.Count);
  completion.Maximum=Math.Max(entries.Count,1);
  completion.Value=completed;
  UpdateClock();
 }
 private void UpdateClock()
 {
  var duration=Stopwatch.GetElapsedTime(phaseStarted);
  var time=$"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
  elapsed.Text=Locale.Text("Elapsed time")+" · "+time+
   (stopping?"  •  "+Locale.Text("Stop requested. Waiting for the current step."):
    duration.TotalSeconds>=90?"  •  "+Locale.Text("Still waiting for this step to finish."):"");
 }
 public void Dispose()
 {
  timer.Stop();
  host.Visibility=Visibility.Collapsed;
  host.Child=null;
 }
}
