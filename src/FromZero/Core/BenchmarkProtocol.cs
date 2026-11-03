using System.IO;
using System.Text.Json;
namespace FromZero;
public record BenchmarkConditions(string Game,string Scene,string Resolution,int Seconds,string Conditions);
public record BenchmarkRun(string Phase,BenchmarkConditions Protocol,string Hash,DateTimeOffset Imported,FrameSummary Frames,string Changes);
public record BenchmarkResult(bool Comparable,bool Conclusive,string Message,double? BeforeFps=null,double? AfterFps=null,double? FpsPercent=null,double? LowPercent=null,double? P99Percent=null,double? BeforeStuttersPerMinute=null,double? AfterStuttersPerMinute=null,double? BeforeLow=null,double? AfterLow=null,double? BeforeP99=null,double? AfterP99=null);
public static class BenchmarkProtocol
{
 public static void Validate(BenchmarkConditions p){if(new[]{p.Game,p.Scene,p.Resolution,p.Conditions}.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>400)||p.Seconds is <20 or >600)throw new InvalidDataException("Fill in game, repeatable scene, resolution, conditions and 20–600 seconds.");}
 private static bool Same(BenchmarkConditions a,BenchmarkConditions b)=>a.Seconds==b.Seconds&&new[]{(a.Game,b.Game),(a.Scene,b.Scene),(a.Resolution,b.Resolution),(a.Conditions,b.Conditions)}.All(p=>p.Item1.Trim().Equals(p.Item2.Trim(),StringComparison.OrdinalIgnoreCase));
 private static double Median(IEnumerable<double> values){var a=values.Order().ToArray();return a.Length%2==0?(a[a.Length/2-1]+a[a.Length/2])/2:a[a.Length/2];}
 public static BenchmarkResult Compare(IReadOnlyList<BenchmarkRun> runs,bool allowPreliminary=false)
 {
  if(runs.Count>20)return new(false,false,"Keep at most 10 runs per phase.");
  var before=runs.Where(r=>r.Phase=="before").ToArray();var after=runs.Where(r=>r.Phase=="after").ToArray();
  var repeated=before.Length>=3&&after.Length>=3;
  if(!repeated&&(!allowPreliminary||before.Length==0||after.Length==0))return new(false,false,$"Need at least 3 different captures before and after. Current: {before.Length} / {after.Length}.");
  var p=before[0].Protocol;try{Validate(p);}catch(InvalidDataException ex){return new(false,false,ex.Message);}
  if(runs.Any(r=>r.Protocol==null||!Same(p,r.Protocol)))return new(false,false,"Game, scene, resolution, conditions or target duration differs. These captures cannot establish a performance gain.");
  if(runs.Any(r=>r.Phase is not("before" or "after")||r.Frames==null||r.Frames.Frames<100||!double.IsFinite(r.Frames.DurationSeconds)||r.Frames.DurationSeconds<=0||Math.Abs(r.Frames.DurationSeconds-p.Seconds)>p.Seconds*.1||!double.IsFinite(r.Frames.AverageFps)||r.Frames.AverageFps<=0||!double.IsFinite(r.Frames.LowOnePercent)||r.Frames.LowOnePercent<=0||!double.IsFinite(r.Frames.P99Milliseconds)||r.Frames.P99Milliseconds<=0))return new(false,false,"Capture duration must be within 10% of the protocol, with valid FPS and frame-time data.");
  if(runs.Select(r=>r.Hash).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=runs.Count||runs.Any(r=>!System.Text.RegularExpressions.Regex.IsMatch(r.Hash??"",@"^[A-Fa-f0-9]{64}$")))return new(false,false,"Each run must be a different, hash-identified capture. Duplicating one CSV is not a repeated experiment.");
  var game=before[0].Frames.Stream.Split(" / PID ")[0];var metric=before[0].Frames.Metric;
  if(runs.Any(r=>r.Frames.Stream.Split(" / PID ")[0]!=game||r.Frames.Metric!=metric)||metric.Length==0)return new(false,false,"Executable or timing metric differs. Select the matching game stream in every capture.");
  double F(IEnumerable<BenchmarkRun> r)=>Median(r.Select(v=>v.Frames.AverageFps));double L(IEnumerable<BenchmarkRun> r)=>Median(r.Select(v=>v.Frames.LowOnePercent));double P(IEnumerable<BenchmarkRun> r)=>Median(r.Select(v=>v.Frames.P99Milliseconds));
  var bf=F(before);var af=F(after);bool stable=new[]{before,after}.All(group=>(group.Max(r=>r.Frames.AverageFps)-group.Min(r=>r.Frames.AverageFps))/F(group)<=.1);
  bool separated=before.Max(r=>r.Frames.AverageFps)<after.Min(r=>r.Frames.AverageFps)||after.Max(r=>r.Frames.AverageFps)<before.Min(r=>r.Frames.AverageFps);
  var conclusive=repeated&&stable&&separated;
  var message=!repeated?"Preliminary result. Capture at least three runs in each phase to assess consistency.":
   conclusive?"Consistent observed difference across repeated captures. Conditions are user-declared; this does not prove which setting caused it.":
   "Inconclusive: run-to-run variation or overlapping FPS ranges. Repeat the same scene before attributing a gain.";
  var bl=L(before);var al=L(after);var bp=P(before);var ap=P(after);
  return new(true,conclusive,message,bf,af,(af/bf-1)*100,(al/bl-1)*100,(ap/bp-1)*100,
   Median(before.Select(r=>r.Frames.Stutters/r.Frames.DurationSeconds*60)),Median(after.Select(r=>r.Frames.Stutters/r.Frames.DurationSeconds*60)),bl,al,bp,ap);
 }
 private static string RunsPath(Guid? comparison)=>LocalStore.FilePath(comparison is Guid id?$"benchmark-runs-{id:N}.json":"benchmark-runs.json");
 public static List<BenchmarkRun> Read(Guid? comparison=null){var path=RunsPath(comparison);if(!File.Exists(path))return [];if(new FileInfo(path).Length>1_000_000)throw new InvalidDataException("Benchmark data too large.");var runs=JsonSerializer.Deserialize<List<BenchmarkRun>>(File.ReadAllText(path),LocalStore.Json)??[];if(runs.Count>20||runs.Any(r=>r==null||r.Protocol==null||r.Frames==null))throw new InvalidDataException("Invalid benchmark history.");return runs;}
 public static void Save(List<BenchmarkRun> runs,Guid? comparison=null){if(runs.Count>20)throw new InvalidDataException("At most 20 runs.");var path=RunsPath(comparison);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(runs,LocalStore.Json));File.Move(path+".tmp",path,true);}
}
