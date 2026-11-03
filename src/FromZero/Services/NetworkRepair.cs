using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
namespace FromZero;
public record AdapterConfig(string Id,string Name,bool Physical,bool Enabled,bool Dhcp,string PnpId);
public record NetworkAdapterState(string Id,string Name,string Description,bool Enabled,bool Dhcp,bool IsWifi,bool Connected,string[] Addresses,string[] Gateways,string[] Dns);
public record NetworkHealth(DateTimeOffset Checked,List<NetworkAdapterState> Adapters,bool WlanRunning,bool DnsWorks,bool InternetWorks,List<string> Findings);
public record NetworkRepairStep(string Id,string Label,bool Disconnects,bool Reboot,string Detail);
public static class NetworkRepair
{
 public static bool PhysicalHardware(string pnp)=>new[]{@"PCI\",@"USB\",@"SD\"}.Any(prefix=>pnp.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
 public static async Task<NetworkHealth> InspectAsync()
 {
  var config=await Commands.JsonAsync<List<AdapterConfig>>("$c=@(Get-CimInstance Win32_NetworkAdapterConfiguration); ConvertTo-Json -Depth 4 -InputObject @(Get-CimInstance Win32_NetworkAdapter | Where-Object { $_.PhysicalAdapter -and $_.GUID } | ForEach-Object { $a=$_; $ip=$c | Where-Object { $_.SettingID -eq $a.GUID } | Select-Object -First 1; [pscustomobject]@{ Id=[string]$a.GUID; Name=[string]$a.NetConnectionID; Physical=[bool]$a.PhysicalAdapter; Enabled=($a.ConfigManagerErrorCode -ne 22); Dhcp=[bool]$ip.DHCPEnabled; PnpId=[string]$a.PNPDeviceID } })");
  var service=await Commands.JsonAsync<string>("$s=Get-CimInstance Win32_Service -Filter \"Name='WlanSvc'\"; [string]$s.State | ConvertTo-Json");
  var adapters=new List<NetworkAdapterState>();
  foreach(var c in config.Where(c=>PhysicalHardware(c.PnpId))){
   var network=NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(a=>a.Id.Trim('{','}').Equals(c.Id.Trim('{','}'),StringComparison.OrdinalIgnoreCase));
   var ip=network?.GetIPProperties();
   adapters.Add(new(c.Id,c.Name,network?.Description??c.Name,c.Enabled,c.Dhcp,network?.NetworkInterfaceType==NetworkInterfaceType.Wireless80211,network?.OperationalStatus==OperationalStatus.Up,ip?.UnicastAddresses.Where(i=>i.Address.AddressFamily==AddressFamily.InterNetwork).Select(i=>i.Address.ToString()).ToArray()??[],ip?.GatewayAddresses.Where(i=>i.Address.AddressFamily==AddressFamily.InterNetwork).Select(i=>i.Address.ToString()).ToArray()??[],ip?.DnsAddresses.Select(i=>i.ToString()).ToArray()??[]));
  }
  bool dns=false,internet=false;
  try{using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(5));dns=(await System.Net.Dns.GetHostAddressesAsync("www.microsoft.com",cts.Token)).Length>0;}catch(Exception ex)when(ex is SocketException or OperationCanceledException){}
  try{using var tcp=new TcpClient();await tcp.ConnectAsync(System.Net.IPAddress.Parse("1.1.1.1"),443).WaitAsync(TimeSpan.FromSeconds(5));internet=true;}catch(Exception ex)when(ex is SocketException or TimeoutException){}
  var findings=new List<string>();
  if(!service.Equals("Running",StringComparison.OrdinalIgnoreCase)&&adapters.Any(a=>a.IsWifi))findings.Add("Wi-Fi hizmeti çalışmıyor.");
  foreach(var a in adapters){if(!a.Enabled)findings.Add(a.Name+": adaptör kapalı veya devre dışı.");else if(a.Connected&&a.Dhcp&&a.Addresses.Any(x=>x.StartsWith("169.254.")))findings.Add(a.Name+": modemden geçerli IP alınamamış (APIPA).");else if(a.IsWifi&&!a.Connected)findings.Add(a.Name+": kablosuz ağa bağlı değil.");}
  if(!dns)findings.Add("DNS çözümlemesi başarısız.");if(!internet)findings.Add("Direct TCP test to Cloudflare 1.1.1.1:443 did not respond.");
  if(findings.Count==0)findings.Add("Bu kontrollerde bağlantı arızası görülmedi. Hız veya sinyal kalitesi ayrıca incelenebilir.");
  return new(DateTimeOffset.Now,adapters,service.Equals("Running",StringComparison.OrdinalIgnoreCase),dns,internet,findings);
 }
 public static List<NetworkRepairStep> Plan(NetworkHealth health,NetworkAdapterState adapter)
 {
  var result=new List<NetworkRepairStep>();
  if(adapter.IsWifi&&!health.WlanRunning)result.Add(new("wlan","Wi-Fi hizmetini başlat",false,false,"WLAN AutoConfig hizmeti başlatılır; otomatik başlangıç türü değiştirilmez."));
  if(!adapter.Enabled)result.Add(new("enable","Adaptörü etkinleştir",false,false,adapter.Name+" tekrar etkinleştirilir."));
  if(adapter.Enabled&&adapter.Connected&&adapter.Dhcp&&(adapter.Addresses.Length==0||adapter.Addresses.Any(a=>a.StartsWith("169.254."))))result.Add(new("renew","DHCP adresini yenile",true,false,"Yalnızca seçilen, DHCP kullanan adaptöre yeni IP kiralaması istenir. Statik IP değiştirilmez."));
  if(!health.DnsWorks&&health.InternetWorks&&adapter.Enabled&&adapter.Connected&&adapter.Addresses.Any(a=>!a.StartsWith("169.254.")))result.Add(new("dns","DNS önbelleğini yenile",false,false,"Önbellek temizlenir. DNS sunucusu değiştirilmez."));
  return result;
 }
 public static string Script(string action,string adapterId)
 {
  if(!Guid.TryParse(adapterId,out var guid))throw new InvalidDataException("Geçerli adaptör kimliği seç.");
  var prefix="$a=Get-NetAdapter -IncludeHidden | Where-Object { $_.InterfaceGuid -eq [guid]'"+guid+"' } | Select-Object -First 1; if (-not $a) { throw 'Adaptör artık mevcut değil. Yeniden tara.' }; ";
  return action switch{
   "wlan"=>"Start-Service -Name WlanSvc; if ((Get-Service WlanSvc).Status -ne 'Running') { throw 'Wi-Fi hizmeti başlatılamadı.' }; 'Wi-Fi hizmeti çalışıyor.'",
   "enable"=>prefix+"Enable-NetAdapter -InputObject $a -Confirm:$false; 'Adaptör etkinleştirildi.'",
   "restart"=>prefix+"Restart-NetAdapter -InputObject $a -Confirm:$false; 'Adaptör yeniden başlatıldı.'",
   "renew"=>prefix+"$ip=Get-NetIPInterface -InterfaceIndex $a.ifIndex -AddressFamily IPv4; if ($ip.Dhcp -ne 'Enabled') { throw 'Statik IP ayarlı; DHCP yenilemesi yapılmadı.' }; & $env:SystemRoot\\System32\\ipconfig.exe /renew $a.Name; if ($LASTEXITCODE -ne 0) { throw ('DHCP yenileme kodu: '+$LASTEXITCODE) }",
   "dns"=>prefix+"Clear-DnsClientCache; 'DNS önbelleği yenilendi. Sunucu adresleri değiştirilmedi.'",
   "winsock"=>"& $env:SystemRoot\\System32\\netsh.exe winsock reset; if ($LASTEXITCODE -ne 0) { throw 'Winsock sıfırlanamadı.' }; 'Yeniden başlatma gerekiyor. VPN/ağ yazılımlarını kontrol et.'",
   _=>throw new InvalidDataException("Desteklenmeyen ağ işlemi.")
  };
 }
 public static async Task<string> ApplyAsync(string action,string adapterId)
 {
  var result=await Commands.ElevatedAsync(Script(action,adapterId));Journal.Write("Ağ onarımı / "+action,result.Output,result.Success);
  if(!result.Success)throw new InvalidOperationException(result.Output);return result.Output;
 }
}
