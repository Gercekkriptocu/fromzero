using System.IO;
using System.Text.Json;
namespace FromZero;
public record CrosshairArchive(int Schema,DateTimeOffset Exported,List<CrosshairSnapshot> Pros,FaceitSnapshot Ranking,List<FaceitCrosshair> Codes);
public static class CrosshairArchives
{
 public static CrosshairArchive Capture()=>new(1,DateTimeOffset.Now,CrosshairCatalog.Read().Select(e=>e.Snapshot).ToList(),FaceitCatalog.Read(),FaceitCrosshairs.Read());
 public static void Validate(CrosshairArchive archive)
 {
  if(archive.Schema!=1||archive.Exported>DateTimeOffset.Now.AddDays(1)||archive.Pros==null||archive.Codes==null||archive.Ranking==null||archive.Pros.Count!=50||archive.Codes.Count>30||archive.Pros.Any(p=>p==null||p.Fields==null||p.Fields.Count>40)||archive.Codes.Any(c=>c==null)||archive.Pros.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=archive.Pros.Count||archive.Codes.Select(p=>p.Nickname).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=archive.Codes.Count)throw new InvalidDataException("Invalid offline crosshair catalogue.");
  foreach(var pro in archive.Pros)CrosshairCatalog.Entry(pro);
  FaceitCatalog.Validate(archive.Ranking);
  foreach(var code in archive.Codes)FaceitCrosshairs.Validate(code);
 }
 public static async Task<CrosshairArchive> ReadAsync(string path)
 {
  if(new FileInfo(path).Length>2_000_000)throw new InvalidDataException("Offline catalogue exceeds 2 MB.");
  var archive=JsonSerializer.Deserialize<CrosshairArchive>(await File.ReadAllTextAsync(path),LocalStore.Json)??throw new InvalidDataException("Invalid offline crosshair catalogue.");
  Validate(archive);return archive;
 }
 public static string RankingAge(FaceitSnapshot rank)=>DateTimeOffset.TryParse(rank.Checked,out var when)&&DateTimeOffset.Now-when<TimeSpan.FromDays(1)?"Ranking checked: "+rank.Checked:"Cached ranking: "+rank.Checked;
 public static string CodeAge(FaceitCrosshair code)=>DateOnly.TryParse(code.Observed,out var date)&&DateOnly.FromDateTime(DateTime.Now).DayNumber-date.DayNumber>30?"Older code observed: "+code.Observed:"Code observed: "+code.Observed;
}
