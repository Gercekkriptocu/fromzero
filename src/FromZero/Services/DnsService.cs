using System.IO;
using System.Text;
using System.Text.Json;

namespace FromZero;
public interface IDnsBackend
{
 Task<IReadOnlyList<DnsAdapter>> ReadAsync();
 Task SetAsync(DnsAdapter expected, DnsFamily[] desired);
}
public sealed class DnsService(IDnsBackend? backend = null)
{
 private readonly IDnsBackend backend = backend ?? new WindowsDnsBackend();
 private readonly SemaphoreSlim gate = new(1, 1);
 private static string BackupPath(string id) => Guid.TryParse(id, out var guid) ? LocalStore.FilePath("dns-" + guid.ToString("N") + ".json") : throw new InvalidDataException("Adaptör kimliği geçersiz.");
 public Task<IReadOnlyList<DnsAdapter>> ReadAsync() => backend.ReadAsync();
 public bool HasBackup(string id) => File.Exists(BackupPath(id));
 private static DnsBackup? Load(string id)
 {
  var path = BackupPath(id); if (!File.Exists(path)) return null;
  var backup = JsonSerializer.Deserialize<DnsBackup>(File.ReadAllText(path), LocalStore.Json) ?? throw new InvalidDataException("DNS yedeği okunamadı.");
  DnsPolicy.Validate(backup.Original); DnsPolicy.Validate(backup.Original with { Families = backup.Applied });
  if (Guid.Parse(backup.Original.Id) != Guid.Parse(id)) throw new InvalidDataException("DNS yedeği başka adaptöre ait.");
  return backup;
 }
 private static void Save(string id, DnsBackup backup) { var path = BackupPath(id); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(backup, LocalStore.Json)); File.Move(path + ".tmp", path, true); }
 private async Task<DnsAdapter> Current(string id) => (await backend.ReadAsync()).SingleOrDefault(a => Guid.Parse(a.Id) == Guid.Parse(id)) ?? throw new InvalidOperationException("Adaptör artık mevcut değil; yeniden tara.");
 public async Task<string> ApplyAsync(string id, string profileId)
 {
  await gate.WaitAsync();
  try
  {
   var current = await Current(id); DnsPolicy.Validate(current);
   var profile = DnsPolicy.Profiles.SingleOrDefault(p => p.Id == profileId) ?? throw new InvalidDataException("DNS profili desteklenmiyor.");
   var desired = DnsPolicy.Desired(current, profile); var previous = Load(id);
   if (previous != null && !DnsPolicy.Same(current.Families, previous.Applied)) throw new InvalidOperationException("DNS ayarı FromZero dışında değişmiş. Eski yedek korundu; mevcut ayarın üzerine yazılmadı.");
   if (DnsPolicy.Same(current.Families, desired)) return "Bu DNS profili zaten etkin. Değişiklik gerekmedi.";
   var backup = new DnsBackup(previous?.Created ?? DateTimeOffset.Now, previous?.Original ?? current, desired);
   Save(id, backup);
   try
   {
    await backend.SetAsync(current, desired);
    if (!DnsPolicy.Same((await Current(id)).Families, desired)) throw new IOException("DNS ayarı doğrulanamadı. Geri alma yedeği korundu.");
   }
   catch
   {
    // A canceled elevation or a successfully rolled-back native transaction must not replace a prior recovery point.
    try { if (DnsPolicy.Same((await Current(id)).Families, current.Families)) { if (previous == null) File.Delete(BackupPath(id)); else Save(id, previous); } } catch { }
    throw;
   }
   Journal.Write("DNS profili", current.Name + " → " + profile.Name);
   return current.Name + " için " + profile.Name + " DNS ayarı doğrulandı. Önceki ayarın yedeği saklandı.";
  }
  finally { gate.Release(); }
 }
 public async Task<string> RestoreAsync(string id)
 {
  await gate.WaitAsync();
  try
  {
   var backup = Load(id) ?? throw new InvalidOperationException("Bu adaptör için FromZero DNS yedeği yok.");
   var current = await Current(id);
   if (DnsPolicy.Same(current.Families, backup.Original.Families)) { File.Delete(BackupPath(id)); return "Önceki DNS ayarı zaten etkin. Profil kapatıldı."; }
   if (!DnsPolicy.Same(current.Families, backup.Applied)) throw new InvalidOperationException("DNS ayarı sonradan değişmiş. Üzerine yazılmadı; yedek korundu.");
   await backend.SetAsync(current, backup.Original.Families);
   if (!DnsPolicy.Same((await Current(id)).Families, backup.Original.Families)) throw new IOException("Önceki DNS doğrulanamadı; yedek korundu.");
   File.Delete(BackupPath(id)); Journal.Write("DNS geri alındı", current.Name);
   return "Önceki statik / otomatik DNS tercihleri geri yüklendi.";
  }
  finally { gate.Release(); }
 }
}
public sealed class WindowsDnsBackend : IDnsBackend
{
 private const string Functions = """
function Get-Families($a) {
 $result=@(); foreach($family in @('IPv4','IPv6')) {
  $d=Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily $family -ErrorAction SilentlyContinue
  if(-not $d){continue}
  $service=if($family -eq 'IPv4'){'Tcpip'}else{'Tcpip6'}
  $key='HKLM:\SYSTEM\CurrentControlSet\Services\'+$service+'\Parameters\Interfaces\'+([guid]$a.InterfaceGuid).ToString('B')
  if(-not (Test-Path -LiteralPath $key)){throw 'DNS otomatik/statik tercihi okunamadı.'}
  $raw=[string](Get-ItemProperty -LiteralPath $key).NameServer
  $configured=if([string]::IsNullOrWhiteSpace($raw)){@($d.ServerAddresses)}else{@($raw -split '[,;\s]+' | Where-Object {$_})}
  $servers=@($configured | ForEach-Object { ([Net.IPAddress]::Parse($_)).ToString() })
  $result += [pscustomobject]@{Family=$family;Automatic=[string]::IsNullOrWhiteSpace($raw);Servers=$servers}
 }; return $result
}
function Same-Family($x,$y) {
 if($x.Family -ne $y.Family -or [bool]$x.Automatic -ne [bool]$y.Automatic){return $false}
 if($x.Automatic){return $true}
 $one=@($x.Servers | ForEach-Object { ([Net.IPAddress]::Parse($_)).ToString() }) -join ','
 $two=@($y.Servers | ForEach-Object { ([Net.IPAddress]::Parse($_)).ToString() }) -join ','
 return $one -eq $two
}
function Set-Family($a,$f) {
 $d=Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily $f.Family
 if($f.Automatic){$d | Set-DnsClientServerAddress -ResetServerAddresses}
 else {$d | Set-DnsClientServerAddress -ServerAddresses @($f.Servers)}
}
""";
 public async Task<IReadOnlyList<DnsAdapter>> ReadAsync()
 {
  var adapters = await Commands.JsonAsync<List<DnsAdapter>>(Functions + "\nConvertTo-Json -Depth 6 -InputObject @(Get-NetAdapter -Physical | ForEach-Object { $a=$_; [pscustomobject]@{Id=([guid]$a.InterfaceGuid).ToString();Name=$a.Name;Connected=($a.Status -eq 'Up');Families=@(Get-Families $a)} })");
  adapters = adapters.Where(a => a.Families.Length > 0).ToList();
  foreach (var adapter in adapters) DnsPolicy.Validate(adapter);
  return adapters.OrderByDescending(a => a.Connected).ThenBy(a => a.Name).ToArray();
 }
 public static string Script(DnsAdapter expected, DnsFamily[] desired)
 {
  DnsPolicy.Validate(expected); DnsPolicy.Validate(expected with { Families = desired });
  if (!expected.Families.Select(f => f.Family).Order().SequenceEqual(desired.Select(f => f.Family).Order())) throw new InvalidDataException("DNS adres aileleri eşleşmiyor.");
  var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Id = Guid.Parse(expected.Id).ToString(), Expected = expected.Families, Desired = desired })));
  return Functions + "\n$p=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + payload + "')) | ConvertFrom-Json; " + """
$a=Get-NetAdapter -Physical | Where-Object { [guid]$_.InterfaceGuid -eq [guid]$p.Id } | Select-Object -First 1
if(-not $a){throw 'Fiziksel adaptör artık mevcut değil.'}
$old=@(Get-Families $a)
if($old.Count -ne $p.Expected.Count){throw 'Adaptörün adres aileleri değişti; yeniden tara.'}
foreach($f in $p.Expected){$now=$old | Where-Object {$_.Family -eq $f.Family}; if(-not $now -or -not (Same-Family $now $f)){throw 'DNS ayarı değişti; yeniden tara.'}}
$touched=@()
try {
 foreach($f in $p.Desired){$touched+=$f.Family; Set-Family $a $f}
 $now=@(Get-Families $a)
 foreach($f in $p.Desired){$actual=$now | Where-Object {$_.Family -eq $f.Family}; if(-not $actual -or -not (Same-Family $actual $f)){throw 'DNS ayarı Windows tarafından doğrulanamadı.'}}
 Clear-DnsClientCache -ErrorAction SilentlyContinue
 'DNS ayarı uygulandı ve doğrulandı.'
} catch {
 $cause=$_.Exception.Message; $issues=@()
 foreach($family in $touched){try {
  $current=Get-Families $a | Where-Object {$_.Family -eq $family}
  $before=$old | Where-Object {$_.Family -eq $family}; $wanted=$p.Desired | Where-Object {$_.Family -eq $family}
  if(Same-Family $current $before){continue}
  if(-not (Same-Family $current $wanted)){throw 'Sonradan değişen DNS ayarına dokunulmadı.'}
  Set-Family $a $before
  $check=Get-Families $a | Where-Object {$_.Family -eq $family}; if(-not (Same-Family $check $before)){throw 'Geri alma doğrulanamadı.'}
 }catch{$issues+=$_.Exception.Message}}
 if($issues.Count){throw ($cause+' Geri alma: '+($issues -join '; '))}
 throw ($cause+' Önceki ayar korundu / geri alındı.')
}
""";
 }
 public async Task SetAsync(DnsAdapter expected, DnsFamily[] desired)
 {
  var result = await Commands.ElevatedAsync(Script(expected, desired));
  if (!result.Success) throw new InvalidOperationException(result.Output);
 }
}
