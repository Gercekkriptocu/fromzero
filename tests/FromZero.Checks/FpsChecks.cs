using FromZero;
static class FpsChecks
{
 public static async Task Run(string root,Action<bool,string> check)
 {
  check(Cs2LaunchOptions.Build(0)=="+fps_max 0 +fps_max_ui 120"&&Cs2LaunchOptions.Build(240)=="+fps_max 240 +fps_max_ui 120","CS2 copy recipe generates only explicit game and menu FPS limits");
  bool invalid=false;try{Cs2LaunchOptions.Build(-1);}catch(InvalidDataException){invalid=true;}check(invalid,"CS2 generator rejects invalid FPS target");
  check(!Cs2LaunchOptions.Build(300).Contains('-'),"CS2 recipe does not inject old engine or priority flags");
  var options=new PerformanceOptions(false,false,true,true,true,true);var backend=new FakePerformanceBackend();var profile=new PerformanceProfile(backend,Path.Combine(root,"gpu-expanded.json"));
  await profile.ApplyAsync(options);check(backend.NvidiaPower==1&&backend.NvidiaQuality==20&&backend.NvidiaRefresh==1&&backend.NvidiaVsync==NvidiaSettings.VsyncOff&&backend.Animated,"Four NVIDIA settings apply together without unwanted Windows animation changes");
  backend.NvidiaRefresh=0;await profile.RestoreAsync();check(backend.NvidiaPower==5&&backend.NvidiaQuality==0&&backend.NvidiaRefresh==0&&backend.NvidiaVsync==0x47814940,"Expanded NVIDIA rollback restores selected values and preserves later changes");
  check(NvidiaSettings.Desired(new(false,false,true,true,true)).Count==3&&!NvidiaSettings.Desired(new(false,false,true,true,true)).ContainsKey(NvidiaSettings.VsyncId),"VSync off is explicit opt-in in FPS preset");
  var display=new FakeDisplayBackend();var path=Path.Combine(root,"display-test.json");using var service=new DisplaySettings(display,path);var high=display.Original with{Hz=144};
  service.Begin(high);check(display.Current==high&&service.Backup is{Confirmed:false}&&File.Exists(path)&&display.TestCalls==1,"Display preview tests supported mode and saves recovery before applying");
  service.Keep();check(service.Backup is{Confirmed:true}&&display.Persisted==high,"Display keep persists selected supported frequency after verification");
  service.Restore();check(display.Current==display.Original&&service.Backup==null&&!File.Exists(path),"Display restore returns original frequency and clears recovery");
  service.Begin(high,TimeSpan.FromMilliseconds(80));await Task.Delay(220);check(display.Current==display.Original&&service.Backup==null,"Independent display timeout restores without UI confirmation");
  service.Begin(high);display.Current=display.Original with{Hz=120};service.Restore();check(display.Current.Hz==120,"Display rollback preserves a later external display change");display.Current=display.Original;
  invalid=false;try{service.Begin(high with{Width=1280});}catch(InvalidDataException){invalid=true;}check(invalid&&service.Backup==null,"Display profile rejects resolution changes and unlisted modes");
  display.FailNext=true;invalid=false;try{service.Begin(high);}catch(IOException){invalid=true;}check(invalid&&display.Current==display.Original&&service.Backup==null,"Failed display change restores and clears pending state");
  using(var pending=new DisplaySettings(display,path)){pending.Begin(high);}
  check(display.Current==display.Original&&!File.Exists(path),"Closing an unconfirmed display preview restores prior mode");
  var real=new WindowsDisplayBackend().Read();check(real.Count>0&&real.All(s=>s.Modes.Count>0&&s.Modes.All(m=>m.SameSurface(s.Current))),"Real Windows display inventory reads modes without changing settings");
  Console.WriteLine("DISPLAY READ: "+string.Join("; ",real.Select(s=>s.Name+" "+s.Current.Label+" supported Hz: "+string.Join(',',s.Modes.Select(m=>m.Hz)))));
 }
 private sealed class FakeDisplayBackend:IDisplayBackend
 {
  public readonly DisplayMode Original=new(@"\\.\DISPLAY1",1920,1080,32,60,0,0,0,0);
  public DisplayMode Current,Persisted;public int TestCalls;public bool FailNext;
  public FakeDisplayBackend(){Current=Persisted=Original;}
  public IReadOnlyList<DisplayScreen> Read()=>[new("Test display",Current,[Original,Original with{Hz=120},Original with{Hz=144}])];
  public void Test(DisplayMode mode)=>TestCalls++;
  public void Apply(DisplayMode mode,bool persist){if(FailNext){FailNext=false;throw new IOException("simulated rejected display change");}Current=mode;if(persist)Persisted=mode;}
 }
}
