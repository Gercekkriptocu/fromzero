using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
namespace FromZero;
public record NetworkSample(bool Success,double Milliseconds,string Detail);
public record NetworkStatistics(int Sent,int Received,double LossPercent,double? MedianMs,double? JitterMs,string Note);
public record NetworkQualityReport(DateTimeOffset Checked,string AdapterId,string Adapter,NetworkStatistics Gateway,NetworkStatistics DirectTcp,NetworkStatistics Dns,List<string> Advice);
public record GameRouteReport(DateTimeOffset Checked,string Target,string Address,NetworkStatistics Probes);
public interface INetworkQualityProbe{Task<NetworkSample> PingAsync(string gateway,CancellationToken token);Task<NetworkSample> TcpAsync(string local,CancellationToken token);Task<NetworkSample> DnsAsync(string local,string server,CancellationToken token);}
public static class DnsWire
{
 public static byte[] Query(ushort id,string name="www.microsoft.com")
 {
  using var data=new MemoryStream();data.Write([(byte)(id>>8),(byte)id,1,0,0,1,0,0,0,0,0,0]);
  foreach(var label in name.Split('.')){if(label.Length is <1 or >63||label.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new InvalidDataException("Invalid DNS test name.");data.WriteByte((byte)label.Length);data.Write(System.Text.Encoding.ASCII.GetBytes(label));}data.Write([0,0,1,0,1]);return data.ToArray();
 }
 public static bool ValidResponse(byte[] query,byte[] response)
 {
  if(query.Length<17||response.Length<query.Length||response[0]!=query[0]||response[1]!=query[1]||(response[2]&0x80)==0||(response[2]&0x7a)!=0||(response[3]&15)!=0||response[4]!=0||response[5]!=1||!response.AsSpan(12,query.Length-12).SequenceEqual(query.AsSpan(12)))return false;
  int Read16(int at)=>(response[at]<<8)|response[at+1];
  var count=Read16(6);if(count is <1 or >64)return false;
  var offset=query.Length;var found=false;
  for(int i=0;i<count;i++)
  {
   while(true)
   {
    if(offset>=response.Length)return false;
    var size=response[offset++];if(size==0)break;
    if((size&0xc0)==0xc0){if(offset>=response.Length||((size&0x3f)<<8|response[offset++])>=response.Length)return false;break;}
    if(size>63||offset+size>response.Length)return false;offset+=size;
   }
   if(offset+10>response.Length)return false;
   var type=Read16(offset);var klass=Read16(offset+2);var length=Read16(offset+8);offset+=10;
   if(offset+length>response.Length)return false;
   found|=type==1&&klass==1&&length==4;offset+=length;
  }
  return found;
 }
}
public sealed class WindowsNetworkQualityProbe:INetworkQualityProbe
{
 private static async Task<NetworkSample> Timed(Func<CancellationToken,Task<bool>> probe,CancellationToken token)
 {
  using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(1500);var watch=Stopwatch.StartNew();try{return new(await probe(limit.Token),watch.Elapsed.TotalMilliseconds,"");}catch(Exception ex)when(ex is SocketException or OperationCanceledException or PingException or IOException){token.ThrowIfCancellationRequested();return new(false,watch.Elapsed.TotalMilliseconds,ex is OperationCanceledException?"Timed out":ex.GetType().Name);}
 }
 public Task<NetworkSample> PingAsync(string gateway,CancellationToken token)=>Timed(async ct=>{using var ping=new Ping();var reply=await ping.SendPingAsync(IPAddress.Parse(gateway),TimeSpan.FromMilliseconds(1200),new byte[24],null,ct);return reply.Status==IPStatus.Success;},token);
 public Task<NetworkSample> TcpAsync(string local,CancellationToken token)=>Timed(async ct=>{using var tcp=new TcpClient(new IPEndPoint(IPAddress.Parse(local),0));await tcp.ConnectAsync(IPAddress.Parse("1.1.1.1"),443,ct);return true;},token);
 public Task<NetworkSample> DnsAsync(string local,string server,CancellationToken token)=>Timed(async ct=>{using var udp=new UdpClient(new IPEndPoint(IPAddress.Parse(local),0));udp.Connect(IPAddress.Parse(server),53);var request=DnsWire.Query((ushort)RandomNumberGenerator.GetInt32(1,65536));await udp.SendAsync(request,ct);var response=await udp.ReceiveAsync(ct);return DnsWire.ValidResponse(request,response.Buffer);},token);
}
public static class NetworkQuality
{
 internal static bool IsProbeTarget(string target)=>target.Length is >0 and <=253&&
  (IPAddress.TryParse(target,out _)||Uri.CheckHostName(target)==UriHostNameType.Dns);
 public static async Task<GameRouteReport> MeasureGameRouteAsync(string target,CancellationToken token=default,IProgress<string>? progress=null)
 {
  target=target.Trim();
  if(!IsProbeTarget(target))throw new InvalidDataException("Enter a game server hostname or IP address.");
  using var resolution=CancellationTokenSource.CreateLinkedTokenSource(token);
  resolution.CancelAfter(TimeSpan.FromSeconds(5));
  IPAddress? address;
  try{address=IPAddress.TryParse(target,out var parsed)?parsed:(await Dns.GetHostAddressesAsync(target,resolution.Token)).FirstOrDefault();}
  catch(OperationCanceledException)when(!token.IsCancellationRequested){throw new IOException("Game server lookup timed out.");}
  if(address==null||address.Equals(IPAddress.Any)||address.Equals(IPAddress.IPv6Any)||address.Equals(IPAddress.Broadcast)||
   address.IsIPv6Multicast||address.AddressFamily==AddressFamily.InterNetwork&&address.GetAddressBytes()[0] is >=224 and <=239)
   throw new InvalidDataException("A unicast server address is required.");
  const int count=12;
  List<NetworkSample> samples=[];
  using var ping=new Ping();
  for(int i=0;i<count;i++){
   token.ThrowIfCancellationRequested();
   progress?.Report($"Game route {i+1}/{count}");
   try{
    var reply=await ping.SendPingAsync(address,TimeSpan.FromSeconds(1),new byte[24],null,token);
    samples.Add(new(reply.Status==IPStatus.Success,reply.RoundtripTime,""));
   }catch(PingException){samples.Add(new(false,0,""));}
   if(i+1<count)await Task.Delay(125,token);
  }
  return new(DateTimeOffset.Now,target,address.ToString(),Summarize(samples,"ICMP via Windows routing. In-game traffic may follow a different path."));
 }
 public static NetworkStatistics Summarize(IReadOnlyList<NetworkSample> samples,string note)
 {
  var times=samples.Where(s=>s.Success&&double.IsFinite(s.Milliseconds)&&s.Milliseconds>=0).Select(s=>s.Milliseconds).ToArray();var sorted=times.Order().ToArray();double? median=sorted.Length==0?null:sorted.Length%2==0?(sorted[sorted.Length/2-1]+sorted[sorted.Length/2])/2:sorted[sorted.Length/2];
  return new(samples.Count,times.Length,samples.Count==0?0:100d*(samples.Count-times.Length)/samples.Count,median,times.Length<2?null:times.Zip(times.Skip(1),(a,b)=>Math.Abs(a-b)).Average(),note);
 }
 public static List<string> Advice(NetworkStatistics gateway,NetworkStatistics tcp,NetworkStatistics dns)
 {
  List<string> advice=[];
  if(gateway.Sent>0&&gateway.Received==0)advice.Add("Gateway ICMP did not answer. ICMP filtering is possible; this alone is not a broken connection.");
  else if(gateway.LossPercent>0)advice.Add("Some gateway ICMP probes were lost. Check Wi-Fi signal, distance, interference or cable, then repeat.");
  if(tcp.Received>0&&dns.Sent>0&&dns.Received==0)advice.Add("Direct TCP worked but the configured DNS server did not answer this UDP test. Check VPN/policy and the system DNS test before changing providers.");
  if(tcp.Sent>0&&tcp.Received==0)advice.Add("The direct external TCP test failed. This target may be blocked; compare another service before resetting network settings.");
  if(dns.Sent==0)advice.Add("No configured IPv4 DNS server was available for a bound-interface test. System DNS/DoH may use a different path.");
  if(advice.Count==0)advice.Add("No failure in this sample. Do not reset a working connection. These tests do not measure bandwidth or game-server latency.");return advice;
 }
 public static async Task<NetworkQualityReport> MeasureAsync(NetworkAdapterState adapter,INetworkQualityProbe? probe=null,CancellationToken token=default,IProgress<string>? progress=null)
 {
  if(!adapter.Enabled||!adapter.Connected)throw new InvalidOperationException("Select a connected physical adapter.");var local=adapter.Addresses.FirstOrDefault(ip=>IPAddress.TryParse(ip,out var a)&&a.AddressFamily==AddressFamily.InterNetwork&&!ip.StartsWith("169.254."))??throw new InvalidOperationException("A valid IPv4 address is required; check DHCP or your static IP first.");
  probe??=new WindowsNetworkQualityProbe();var gateway=adapter.Gateways.FirstOrDefault(ip=>IPAddress.TryParse(ip,out var a)&&a.AddressFamily==AddressFamily.InterNetwork);var server=adapter.Dns.FirstOrDefault(ip=>IPAddress.TryParse(ip,out var a)&&a.AddressFamily==AddressFamily.InterNetwork);List<NetworkSample> g=[],t=[],d=[];
  for(int i=0;i<5;i++){token.ThrowIfCancellationRequested();progress?.Report($"Connection quality {i+1}/5");var gp=gateway!=null?probe.PingAsync(gateway,token):null;var tp=probe.TcpAsync(local,token);var dp=server!=null?probe.DnsAsync(local,server,token):null;if(gp!=null)g.Add(await gp);t.Add(await tp);if(dp!=null)d.Add(await dp);}
  var gs=Summarize(g,"Gateway ICMP uses Windows routing; a VPN can affect the route.");var ts=Summarize(t,"TCP to Cloudflare 1.1.1.1:443, bound to the selected local IPv4 address.");var ds=Summarize(d,"UDP DNS to the adapter's configured resolver, bound to its IPv4 address; no DNS setting is changed.");return new(DateTimeOffset.Now,adapter.Id,adapter.Name,gs,ts,ds,Advice(gs,ts,ds));
 }
 public static string ProbeSummary(NetworkStatistics s)=>$"{s.Received}/{s.Sent} replies · probe loss {s.LossPercent:0.#}% · median {(s.MedianMs?.ToString("0.0")??"—")} ms · jitter {(s.JitterMs?.ToString("0.0")??"—")} ms";
 public static string Describe(NetworkQualityReport r){string S(string label,NetworkStatistics s)=>label+": "+ProbeSummary(s)+"\n"+s.Note;return r.Adapter+" · "+r.Checked.ToString("g")+"\n\n"+S("Gateway",r.Gateway)+"\n\n"+S("Direct TCP",r.DirectTcp)+"\n\n"+S("Configured DNS",r.Dns)+"\n\n"+string.Join("\n",r.Advice)+"\n\nSmall sample: probe loss is not a universal packet-loss measurement. Jitter is the mean absolute difference between successful samples. The selected resolver, Cloudflare and your gateway receive test traffic.";}
}
