using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace FromZero;
public record PreparationAuditEntry(int Sequence,DateTimeOffset Checked,string Step,string Phase,int Attempt,string State,string Detail,string Previous,string Hash);
public static class PreparationAudit
{
 private static readonly object Gate=new();
 private static string Digest(PreparationAuditEntry entry)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry with{Hash=""}))));
 public static List<PreparationAuditEntry> Read(PreparationJob job)
 {
  lock(Gate){var path=Path.Combine(PreparationStore.Folder(job.Id),"attempt-journal.jsonl");if(!File.Exists(path))return [];if(new FileInfo(path).Length>16_000_000)throw new InvalidDataException("Preparation journal too large.");var result=new List<PreparationAuditEntry>();var previous="";foreach(var line in File.ReadLines(path)){var entry=JsonSerializer.Deserialize<PreparationAuditEntry>(line)??throw new InvalidDataException("Invalid journal entry.");if(entry.Sequence!=result.Count+1||entry.Previous!=previous||entry.Hash!=Digest(entry))throw new InvalidDataException("Preparation journal is incomplete or changed. Keep the original files for review.");result.Add(entry);previous=entry.Hash;}return result;}
 }
 public static void Append(PreparationJob job,PreparationStep step,string phase)
 {
  lock(Gate){var entries=Read(job);var entry=new PreparationAuditEntry(entries.Count+1,DateTimeOffset.Now,step.Id,phase,step.Attempts,step.State,step.Detail,entries.LastOrDefault()?.Hash??"","");entry=entry with{Hash=Digest(entry)};var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry)+"\n");using var file=new FileStream(Path.Combine(PreparationStore.Folder(job.Id),"attempt-journal.jsonl"),FileMode.Append,FileAccess.Write,FileShare.Read);file.Write(bytes);file.Flush(true);}
 }
}
