using System.Globalization;
using System.IO;
using System.Text;
namespace FromZero;
public record FrameSummary(string Stream, int Frames, double AverageFps, double LowOnePercent, double P99Milliseconds,double DurationSeconds=0,int Stutters=0,string Metric="");
public static class FrameAnalysis
{
 public static List<string> ParseCsvLine(string line)
 {
  var fields=new List<string>();var field=new StringBuilder();bool quoted=false;
  for(int i=0;i<line.Length;i++){
   var c=line[i];
   if(c=='"'){if(quoted && i+1<line.Length && line[i+1]=='"'){field.Append('"');i++;}else quoted=!quoted;}
   else if(c==',' && !quoted){fields.Add(field.ToString());field.Clear();}else field.Append(c);
  }
  if(quoted)throw new InvalidDataException("Tamamlanmamış CSV alanı.");fields.Add(field.ToString());return fields;
 }
 public static List<FrameSummary> Read(TextReader reader)
 {
  var first=reader.ReadLine()??throw new InvalidDataException("CSV boş.");var header=ParseCsvLine(first.TrimStart('\uFEFF'));
  int Find(string name)=>header.FindIndex(h=>h.Equals(name,StringComparison.OrdinalIgnoreCase));
  var frame=Find("MsBetweenPresents");if(frame<0)frame=Find("FrameTime");
  if(frame<0)throw new InvalidDataException("MsBetweenPresents veya FrameTime sütunu bulunamadı. PresentMon CSV dosyası seçin.");
  var app=Find("Application");var pid=Find("ProcessID");var swap=Find("SwapChainAddress");
  var streams=new Dictionary<string,List<double>>();string? line;int rows=0;
  while((line=reader.ReadLine())!=null){
   if(++rows>2_000_000)throw new InvalidDataException("CSV çok büyük; daha kısa bir kayıt kullanın.");
   var fields=ParseCsvLine(line);if(frame>=fields.Count)continue;
   if(!double.TryParse(fields[frame],NumberStyles.Float,CultureInfo.InvariantCulture,out var ms)||!double.IsFinite(ms)||ms<=0)continue;
   string At(int index)=>index>=0 && index<fields.Count?fields[index]:"?";
   var key=At(app)+" / PID "+At(pid)+" / "+At(swap);
   if(!streams.TryGetValue(key,out var values)){values=[];streams[key]=values;}values.Add(ms);
  }
  return streams.Where(s=>s.Value.Count>=100).Select(s=>{
   var times=s.Value.OrderDescending().ToArray();var slowCount=Math.Max(1,(int)Math.Ceiling(times.Length*.01));
   var p99Index = Math.Clamp(times.Length - (int)Math.Ceiling(times.Length * .99), 0, times.Length - 1);
   var median=times[times.Length/2];return new FrameSummary(s.Key,times.Length,1000/times.Average(),1000/times.Take(slowCount).Average(),times[p99Index],times.Sum()/1000,times.Count(t=>t>50&&t>2*median),header[frame]);
  }).OrderByDescending(s=>s.Frames).ToList();
 }
}
