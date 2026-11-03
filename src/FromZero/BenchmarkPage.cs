using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FromZero;

public partial class MainWindow
{
 private BenchmarkConditions benchmarkConditions=new("Counter-Strike 2","","1920 × 1080",60,"");
 private record ProfileComparison(Guid Id,GraphicsTarget Target,string Preset,bool DisableVsync,Guid? BackupId=null);
 private ProfileComparison? profileComparison;
 private static string ProfileComparisonPath(string game)=>LocalStore.FilePath(game=="cs2"?"benchmark-profile-cs2.json":"benchmark-profile-valorant.json");
 private static void SaveProfileComparison(ProfileComparison session)
 {
  var path=ProfileComparisonPath(session.Target.Game);
  File.WriteAllText(path+".tmp",JsonSerializer.Serialize(session,LocalStore.Json));
  File.Move(path+".tmp",path,true);
 }
 private Task OpenProfileComparisonAsync(GraphicsTarget target,string preset,bool disableVsync)
 {
  var path=ProfileComparisonPath(target.Game);
  ProfileComparison? saved=null;
  try{if(File.Exists(path)&&new FileInfo(path).Length<32_000)saved=JsonSerializer.Deserialize<ProfileComparison>(File.ReadAllText(path),LocalStore.Json);}
  catch(Exception ex)when(ex is IOException or JsonException){}
  if(saved is {Id:var id}&&id!=Guid.Empty&&saved.Target==target&&saved.Preset==preset&&saved.DisableVsync==disableVsync)profileComparison=saved;
  else{
   if(saved!=null&&!Confirm("Start a new comparison for the selected account and profile? Existing capture files will be kept.","Profile FPS comparison"))return Task.CompletedTask;
   profileComparison=new(Guid.NewGuid(),target,preset,disableVsync);
   SaveProfileComparison(profileComparison);
  }
  Navigate("benchmark");
  return Task.CompletedTask;
 }
 private void Benchmark()
 {
  var profile=profileComparison;
  Header("FPS LAB",profile==null?"Compare repeated game captures":"Profile FPS comparison","Use the same scene, resolution and duration. Record the conditions and the setting you changed.");
  var runs=BenchmarkProtocol.Read(profile?.Id);
  var gameName=profile?.Target.Game=="valorant"?"VALORANT":"Counter-Strike 2";
  var fieldsLocked=profile!=null&&runs.Count>0;
  var declared=runs.Count>0&&(profile!=null||benchmarkConditions.Scene.Length==0)?runs[0].Protocol:
   profile==null?benchmarkConditions:new(gameName,"","1920 × 1080",60,"");
  var game=new TextBox{Text=declared.Game,IsReadOnly=profile!=null};
  var scene=new TextBox{Text=declared.Scene,IsReadOnly=fieldsLocked};
  var resolution=new TextBox{Text=declared.Resolution,IsReadOnly=fieldsLocked};
  var seconds=new TextBox{Text=declared.Seconds.ToString(),IsReadOnly=fieldsLocked};
  var conditions=new TextBox{Text=declared.Conditions,IsReadOnly=fieldsLocked};
  var change=new TextBox{MaxLength=400};
  var active=profile==null?null:graphicsService.Active(profile.Target);
  var applied=profile?.BackupId is Guid expected&&active?.Id==expected;
  var hasBaseline=runs.Any(r=>r.Phase=="before");
  var hasAfter=runs.Any(r=>r.Phase=="after");
  if(profile!=null){
   var preset=profile.Preset=="competitive"?"Rekabetçi FPS":"Dengeli görüntü";
   var apply=Action("Apply profile for after captures",async()=>{
    var plan=graphicsService.Preview(profile.Target,profile.Preset,profile.DisableVsync);
    if(plan.Changes.Count==0)throw new InvalidOperationException("The selected profile has no settings to change.");
    var changes=string.Join("\n",plan.Changes.Select(c=>c.Key+"  "+c.Before+" → "+c.After));
    if(!Confirm(profile.Target.Label+"\n\n"+changes+"\n\n"+"Apply the profile and save a backup?","Profile FPS comparison"))return;
    var pending=profile with{BackupId=plan.Id};
    SaveProfileComparison(pending);
    profileComparison=pending;
    await graphicsService.ApplyAsync(plan);
    Navigate("benchmark");
   });
   apply.IsEnabled=active==null&&hasBaseline&&!hasAfter;
   var stages=Row(Action("← Games",()=>{Navigate("gaming");return Task.CompletedTask;}),apply);
   if(active==null&&runs.Count>0)stages.Children.Add(Action("Start a new comparison",()=>{
    if(!Confirm("Start a new comparison for the selected account and profile? Existing capture files will be kept.","Profile FPS comparison"))return Task.CompletedTask;
    profileComparison=profile with{Id=Guid.NewGuid(),BackupId=null};
    SaveProfileComparison(profileComparison);
    Navigate("benchmark");
    return Task.CompletedTask;
   }));
   Page.Children.Add(Card(Stack(T(profile.Target.Label+" · "+preset,17,"#B9E2A7",true),
    T(applied?"Profile applied. Capture the same scene again.":active!=null?"Restore the other active profile before starting a comparison.":
     hasAfter?"This profile comparison has saved results.":"Capture three baseline runs if possible, close the game, then apply the profile.",13,"#B9CBC1"),
    T("Record the same replay or practice route with PresentMon and import its CSV in each phase.",12,"#B9CBC1"),
    stages)));
  }
  StackPanel Field(string name,TextBox box)
  {
   System.Windows.Automation.AutomationProperties.SetName(box,name);
   return Stack(T(name,13,"#B9CBC1"),box);
  }
  async Task Import(string phase)
  {
   if(!int.TryParse(seconds.Text,out var duration))throw new InvalidDataException("Enter a valid capture duration.");
   var protocol=new BenchmarkConditions(game.Text.Trim(),scene.Text.Trim(),resolution.Text.Trim(),duration,conditions.Text.Trim());
   BenchmarkProtocol.Validate(protocol);
   if(runs.Count(r=>r.Phase==phase)>=10)throw new InvalidOperationException("Keep at most 10 runs per phase.");
   var dialog=new OpenFileDialog{Filter="PresentMon CSV|*.csv",Title="Select a game capture"};
   if(dialog.ShowDialog(this)!=true)return;
   var (hash,summaries)=await Task.Run(()=>
   {
    using var stream=File.OpenRead(dialog.FileName);
    if(stream.Length>256_000_000)throw new InvalidDataException("Capture exceeds 256 MB.");
    var digest=Convert.ToHexString(SHA256.HashData(stream));stream.Position=0;
    using var reader=new StreamReader(stream);
    return(digest,FrameAnalysis.Read(reader));
   });
   if(runs.Any(r=>r.Hash==hash))throw new InvalidDataException("This capture is already in the comparison.");
   if(summaries.Count==0)throw new InvalidDataException("No stream with at least 100 valid frames.");
   var selected=ChooseBenchmarkStream(summaries);if(selected==null)return;
   if(profile!=null){
    if(phase=="after"&&(active==null||!graphicsService.ProfileMatches(active,profile.DisableVsync)))
     throw new InvalidOperationException("The game configuration no longer matches the applied profile.");
    var executable=selected.Stream.Split(" / PID ",2)[0];
    if(profile.Target.Game=="cs2"&&!executable.Equals("cs2.exe",StringComparison.OrdinalIgnoreCase)||
     profile.Target.Game=="valorant"&&!executable.Equals("VALORANT-Win64-Shipping.exe",StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("Select the game process for this profile.");
   }
   if(Math.Abs(selected.DurationSeconds-duration)>duration*.1)throw new InvalidDataException($"Capture duration {selected.DurationSeconds:0.0}s differs from the protocol by more than 10%.");
   if(profile==null)benchmarkConditions=protocol;
   runs.Add(new(phase,protocol,hash,DateTimeOffset.Now,selected,profile==null?change.Text.Trim():
    phase=="before"?"Baseline":string.Join(", ",active!.Changes.Select(c=>c.Key+" "+c.Before+" → "+c.After))));
   BenchmarkProtocol.Save(runs,profile?.Id);Navigate("benchmark");
  }
  var fields=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=270};
  foreach(var field in new[]{Field("Game",game),Field("Repeatable scene",scene),Field("Resolution",resolution),Field("Capture seconds",seconds),Field("Conditions kept equal",conditions)})fields.Children.Add(field);
  if(profile==null)fields.Children.Add(Field("Setting changed for this run",change));
  var before=Action("Import before capture",()=>Import("before"),true);
  var after=Action("Import after capture",()=>Import("after"),true);
  before.IsEnabled=profile==null||active==null&&!hasAfter;
  after.IsEnabled=profile==null||applied&&hasBaseline;
  Page.Children.Add(Card(Stack(fields,Row(before,after,Link("PresentMon","https://github.com/GameTechDev/PresentMon")))));
  var outcome=BenchmarkProtocol.Compare(runs,profile!=null);
  var summary=Stack(T(outcome.Message,15,outcome.Conclusive?"#B9E2A7":"#EBC078"));
  if(outcome.Comparable)summary.Children.Add(T($"Average FPS (median across runs): {outcome.BeforeFps:0.0} → {outcome.AfterFps:0.0} ({outcome.FpsPercent:+0.0;-0.0;0.0}%)\n1% low: {outcome.BeforeLow:0.0} → {outcome.AfterLow:0.0} FPS ({outcome.LowPercent:+0.0;-0.0;0.0}%)\nP99 frame time: {outcome.BeforeP99:0.0} → {outcome.AfterP99:0.0} ms ({outcome.P99Percent:+0.0;-0.0;0.0}%)\nStutters/min: {outcome.BeforeStuttersPerMinute:0.0} → {outcome.AfterStuttersPerMinute:0.0}",15,"#D8E8D3"));
  summary.Children.Add(T("A stutter is a frame above 50 ms and twice that run's median. PresentMon presentation timing may differ from displayed FPS.",12,"#B9CBC1"));
  Page.Children.Add(Card(summary));
  foreach(var run in runs)
  {
   var title=$"{(run.Phase=="before"?"Before":"After")} · {run.Frames.AverageFps:0.0} FPS · {run.Frames.DurationSeconds:0.0}s";
   Page.Children.Add(Card(Stack(T(title,15,"#B9E2A7",true),T(run.Protocol.Scene+" · "+run.Protocol.Resolution+" · "+run.Frames.Stream+"\n"+run.Changes,12,"#B9CBC1"),Row(Action("Remove this capture",()=>{runs.Remove(run);BenchmarkProtocol.Save(runs,profile?.Id);Navigate("benchmark");return Task.CompletedTask;})))));
  }
 }
 private FrameSummary? ChooseBenchmarkStream(IReadOnlyList<FrameSummary> summaries)
 {
  if(summaries.Count==1)return summaries[0];
  FrameSummary? selected=null;var window=CreateDialog("Select game stream",680,470);
  var panel=Stack(T("Select the same game executable for each run",18,"#B9E2A7",true));panel.Margin=new Thickness(18);
  foreach(var summary in summaries.Take(30))
  {
   var button=new Button{Content=$"{summary.Stream} · {summary.Frames} frames · {summary.DurationSeconds:0.0}s"};
   button.Click+=(_,_)=>{selected=summary;window.Close();};panel.Children.Add(button);
  }
  window.Content=new ScrollViewer{Content=panel};window.ShowDialog();return selected;
 }
 private async Task InspectGameDependenciesAsync(bool prepare)
 {
  var dialog=new OpenFileDialog{Title="Choose the game's actual executable",Filter="Windows executable|*.exe"};
  if(dialog.ShowDialog(this)!=true)return;
  var analysis=await Task.Run(()=>ExecutableDependencies.Inspect(dialog.FileName));
  var known=analysis.Requirements.Where(r=>r.Recipe!=null||r.Library is "d3d11.dll" or "d3d12.dll" or "vulkan-1.dll" or "openal32.dll").ToArray();
  var missing=known.Where(r=>!r.Present&&r.Recipe!=null).Select(r=>r.Recipe!).Distinct().ToArray();
  var report=$"{Path.GetFileName(dialog.FileName)} · {analysis.Imports.Architecture}\n{analysis.Imports.Libraries.Count} static or delayed imports\n\n"+string.Join("\n\n",known.Select(r=>$"{r.Library} · {r.Architecture}\n{r.Evidence}\n{r.Recipe??r.Source}"))+"\n\nThis is static evidence. Dynamically loaded libraries and launcher dependencies may need a separate check.";
  if(!prepare||missing.Length==0){Detail("Game runtime review",report);return;}
  if(!Confirm(report+"\n\nAdd these missing runtime recipes to the preparation plan?\n"+string.Join(", ",missing),"Game runtime review"))return;
  preparationOptions=preparationOptions with{RequiredLibraries=preparationOptions.RequiredLibraries.Concat(missing).Distinct().ToList()};
  PreparationSelectionChanged();Navigate("prepare");
 }
}
