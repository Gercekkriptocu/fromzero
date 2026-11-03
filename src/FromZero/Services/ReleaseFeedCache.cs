using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace FromZero;
public record ReleaseFeed(string Url,string Body,DateTimeOffset Checked);
public static class ReleaseFeedCache
{
 private static string PathFor(string url)=>LocalStore.FilePath("FeedCache/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))+".json");
 public static ReleaseFeed? Read(string url)
 {
  var path=PathFor(url);try{if(!File.Exists(path)||new FileInfo(path).Length>16_500_000)return null;var item=JsonSerializer.Deserialize<ReleaseFeed>(File.ReadAllText(path));return item?.Url==url&&item.Body.Length<=8_000_000&&item.Checked<=DateTimeOffset.Now&&DateTimeOffset.Now-item.Checked<TimeSpan.FromMinutes(15)?item:null;}catch(Exception ex)when(ex is IOException or JsonException){return null;}
 }
 public static async Task<ReleaseFeed> GetAsync(HttpClient client,string url,CancellationToken token)
 {
  try{return await FetchAsync(client,url,token);}
  catch(Exception ex)when(ex is HttpRequestException or IOException or OperationCanceledException)
  {
   if(!token.IsCancellationRequested)SourceHealth.Note(url,"Unavailable",ex.GetType().Name);
   throw;
  }
 }
 private static async Task<ReleaseFeed> FetchAsync(HttpClient client,string url,CancellationToken token)
 {
  var cached=Read(url);if(cached!=null)return cached;
  using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token);
  if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)throw new IOException("Üretici kaynak sınırı/erişim engeli: "+(int)response.StatusCode+". Daha sonra tekrar dene.");response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>8_000_000)throw new IOException("Sürüm kaydı çok büyük.");await using var stream=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[81920];int read;
  while((read=await stream.ReadAsync(buffer,token))>0){if(output.Length+read>8_000_000)throw new IOException("Sürüm kaydı sınırı aşıldı.");output.Write(buffer,0,read);}var feed=new ReleaseFeed(url,Encoding.UTF8.GetString(output.ToArray()),DateTimeOffset.Now);
  var path=PathFor(url);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(feed),token);File.Move(temporary,path,true);}
  finally{if(File.Exists(temporary))File.Delete(temporary);}
  SourceHealth.Note(url,"Metadata received");return feed;
 }
}
