using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace FromZero;
public record SourceAttempt(string Address,string Host,string Status,DateTimeOffset Checked,DateTimeOffset? LastSuccess,string Detail);
public static class SourceHealth
{
 private static string Folder()=>LocalStore.FilePath("SourceHealth");
 private static SourceAttempt? ReadOne(string path)
 {
  try{return File.Exists(path)&&new FileInfo(path).Length<8192?JsonSerializer.Deserialize<SourceAttempt>(File.ReadAllText(path),LocalStore.Json):null;}
  catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){return null;}
 }
 public static void Note(string url,string status,string detail="")
 {
  if(!Uri.TryCreate(url,UriKind.Absolute,out var address)||address.Scheme!="https"||address.UserInfo.Length>0)return;
  try
  {
   var folder=Folder();Directory.CreateDirectory(folder);
   var file=Path.Combine(folder,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))+".json");
   var old=ReadOne(file);var now=DateTimeOffset.Now;
   var entry=new SourceAttempt(address.GetLeftPart(UriPartial.Path),address.IdnHost,status,now,status is "Verified" or "Reachable"?now:old?.LastSuccess,detail[..Math.Min(detail.Length,450)]);
   var temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temporary,JsonSerializer.Serialize(entry,LocalStore.Json));File.Move(temporary,file,true);}
   finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){ }
 }
 public static List<SourceAttempt> Read()
 {
  var folder=Folder();if(!Directory.Exists(folder))return [];
  return Directory.EnumerateFiles(folder,"*.json").Take(3000).Select(ReadOne).OfType<SourceAttempt>().OrderByDescending(a=>a.Checked).ToList();
 }
}
