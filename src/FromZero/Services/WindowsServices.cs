using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace FromZero;
public static class Commands
{
 public static string PowerShell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
 public static async Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments, int timeoutSeconds = 90, CancellationToken cancellation = default)
 {
  var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
  foreach (var arg in arguments) info.ArgumentList.Add(arg);
  using var process = Process.Start(info) ?? throw new IOException("İşlem başlatılamadı.");
  using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
  timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
  var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
  var stderr = process.StandardError.ReadToEndAsync(cancellation);
  try { await process.WaitForExitAsync(timeout.Token); }
  catch (OperationCanceledException) { try { process.Kill(true); } catch (InvalidOperationException) { } throw new TimeoutException("İşlem durduruldu veya zaman sınırına ulaştı."); }
  var output = await stdout; var errors = await stderr;
  return new(process.ExitCode, process.ExitCode == 0 ? output : output + errors);
 }
 public static Task<CommandResult> PsAsync(string script, int timeoutSeconds = 90, CancellationToken cancellation = default) => RunAsync(PowerShell, ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; $ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " + script))], timeoutSeconds, cancellation);
 // Installers are not killed on cancellation or an arbitrary timeout. Their completion is recorded first.
 public static async Task<CommandResult> InstallProcessAsync(string executable,IEnumerable<string> arguments)
 {
  var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};foreach(var argument in arguments)info.ArgumentList.Add(argument);
  using var process=Process.Start(info)??throw new IOException("Kurulum işlemi başlatılamadı.");var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();return new(process.ExitCode,(await output)+(await error));
 }
 public static Task<CommandResult> InstallPsAsync(string script)=>InstallProcessAsync(PowerShell,["-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.Encoding]::UTF8;$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';"+script))]);
 public static async Task<T> JsonAsync<T>(string script, int timeoutSeconds = 90, CancellationToken cancellation = default)
 {
  var result = await PsAsync(script, timeoutSeconds, cancellation);
  if (!result.Success) throw new InvalidOperationException(result.Output.Trim());
  return JsonSerializer.Deserialize<T>(result.Output.Trim().TrimStart('\uFEFF'), LocalStore.Json) ?? throw new InvalidDataException("Windows boş sonuç döndürdü.");
 }
 // The elevated shell receives only scripts assembled by the application, never user-entered commands.
 public static async Task<CommandResult> ElevatedAsync(string script)
 {
  var resultPath = LocalStore.FilePath("operation-" + Guid.NewGuid().ToString("N") + ".json");
  var escaped = PackageSafety.QuotePs(resultPath);
  var wrapped = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; try { $r = & { " + script + " }; $out=[pscustomobject]@{ ExitCode=0; Output=($r | Out-String) } } catch { $out=[pscustomobject]@{ ExitCode=1; Output=$_.Exception.Message } }; $out | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath " + escaped + " -Encoding UTF8";
  var info = new ProcessStartInfo(PowerShell) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped)) };
  try {
   using var process = Process.Start(info) ?? throw new IOException("Yetkili işlem başlatılamadı.");
   await process.WaitForExitAsync();
   if (!File.Exists(resultPath)) throw new IOException("İşlem sonuç üretmeden kapandı.");
   var result = JsonSerializer.Deserialize<CommandResult>(await File.ReadAllTextAsync(resultPath), LocalStore.Json) ?? throw new InvalidDataException("İşlem sonucu okunamadı.");
   return result;
  } catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { throw new OperationCanceledException("Windows yetki isteği iptal edildi."); }
  finally { if (File.Exists(resultPath)) File.Delete(resultPath); }
 }
 public static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}

public sealed class InventoryService
{
 public Task<List<DeviceInfo>> DevicesAsync() => Commands.JsonAsync<List<DeviceInfo>>( "ConvertTo-Json -Depth 5 -InputObject @(Get-CimInstance Win32_PnPEntity | ForEach-Object { [pscustomobject]@{ Name=[string]$_.Name; Id=[string]$_.PNPDeviceID; Class=[string]$_.PNPClass; Problem=[int]$_.ConfigManagerErrorCode; Present=($_.Present -ne $false); HardwareIds=@($_.HardwareID | Where-Object { $_ }); CompatibleIds=@($_.CompatibleID | Where-Object { $_ }) } })");
 public Task<List<DriverInfo>> DriversAsync() => Commands.JsonAsync<List<DriverInfo>>("ConvertTo-Json -Depth 4 -InputObject @(Get-CimInstance Win32_PnPSignedDriver | ForEach-Object { [pscustomobject]@{ Name=[string]$_.DeviceName; Version=[string]$_.DriverVersion; Provider=[string]$_.DriverProviderName; Inf=[string]$_.InfName; DeviceId=[string]$_.DeviceID; IsSigned=[bool]$_.IsSigned; Date=if ($_.DriverDate) { $_.DriverDate.ToString('yyyy-MM-dd') } else { '' } } })");
 public Task<JsonElement> PassportAsync() => Commands.JsonAsync<JsonElement>("$c=Get-CimInstance Win32_ComputerSystem; $b=Get-CimInstance Win32_BIOS; $o=Get-CimInstance Win32_OperatingSystem; [pscustomobject]@{ Manufacturer=$c.Manufacturer; Model=$c.Model; Windows=$o.Caption; Build=$o.BuildNumber; BIOS=$b.SMBIOSBIOSVersion; CPU=@(Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name); GPU=@(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion); MemoryBytes=$c.TotalPhysicalMemory } | ConvertTo-Json -Depth 5");
 public static List<ProcessInfo> Processes()
 {
  var result = new List<ProcessInfo>();
  foreach (var process in Process.GetProcesses()) { using (process) { try { result.Add(new(process.Id, process.ProcessName, process.WorkingSet64)); } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { } } }
  return result.OrderByDescending(p => p.Memory).Take(15).ToList();
 }
 public static List<StartupEntry> Startup()
 {
  var result = new List<StartupEntry>();
  foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) {
   using var key = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
   if (key != null) foreach (var name in key.GetValueNames()) result.Add(new(name, key.GetValue(name)?.ToString() ?? "", hive == Registry.CurrentUser ? "Kullanıcı" : "Sistem", false));
  }
  using var disabled = Registry.CurrentUser.OpenSubKey(@"Software\FromZero\DisabledStartup");
  if (disabled != null) foreach (var name in disabled.GetValueNames()) result.Add(new(name, disabled.GetValue(name)?.ToString() ?? "", "Kullanıcı", true));
  using var previous=Registry.CurrentUser.OpenSubKey(@"Software\"+BrandMigration.PreviousName+@"\DisabledStartup");
  if(previous!=null)foreach(var name in previous.GetValueNames())result.Add(new(name,previous.GetValue(name)?.ToString()??"","Kullanıcı",true,true));
  return result;
 }
 public static void SetStartup(StartupEntry item, bool enabled)
 {
  if (item.Location != "Kullanıcı") throw new InvalidOperationException("Sistem başlangıç girdileri Windows Ayarları üzerinden yönetilir.");
  using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
  using var disabled = Registry.CurrentUser.CreateSubKey(@"Software\"+(item.Legacy?BrandMigration.PreviousName:"FromZero")+@"\DisabledStartup");
  var source = enabled ? disabled : run;
  var target = enabled ? run : disabled;
  if (source.GetValue(item.Name)?.ToString() != item.Command) throw new InvalidOperationException("Girdi değişti; listeyi yenileyin.");
  if (target.GetValue(item.Name) != null) throw new InvalidOperationException("Hedefte aynı adlı girdi var. Üzerine yazılmadı.");
  var kind = source.GetValueKind(item.Name);
  target.SetValue(item.Name, source.GetValue(item.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!, kind);
  source.DeleteValue(item.Name);
  Journal.Write("Başlangıç " + (enabled ? "geri açıldı" : "kapatıldı"), item.Name);
 }
 public static async Task<JsonElement> AppsAsync()=>JsonSerializer.SerializeToElement((await InstalledSoftware.ReadAsync()).Apps,LocalStore.Json);
}

public sealed class DriverService
{
 public async Task BackupAsync(string destination)
 {
  Directory.CreateDirectory(destination);
  PackageSafety.RejectReparsePoints(destination, destination);
  var result = await Commands.ElevatedAsync("& $env:SystemRoot\\System32\\pnputil.exe /export-driver '*' " + PackageSafety.QuotePs(destination) + "; if ($LASTEXITCODE -notin @(0,3010)) { throw ('PnPUtil kodu: ' + $LASTEXITCODE) }");
  Journal.Write("Sürücü yedeği", result.Output, result.Success);
  if (!result.Success) throw new InvalidOperationException(result.Output);
 }
 public async Task<string> CheckInfAsync(string inf, List<DeviceInfo> devices)
 {
  if (!File.Exists(inf) || !Path.GetExtension(inf).Equals(".inf", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Bir INF dosyası seçin.");
  PackageSafety.RejectReparsePoints(Path.GetDirectoryName(inf)!, inf);
  var ids = PackageSafety.ReadHardwareIds(inf);
  var matches = devices.Where(d => d.HardwareIds.Any(ids.Contains)).ToList();
  if (matches.Count == 0) throw new InvalidDataException("Bu INF içinde mevcut cihazlarla tam donanım kimliği eşleşmesi bulunamadı. Geniş veya tahmini eşleşme ile kurulum yapılmadı.");
  var catalog = PackageSafety.ReadCatalog(inf);
  if (catalog is null || !File.Exists(catalog)) throw new InvalidDataException("Bu mimari için sürücü katalog dosyası bulunamadı.");
  var signature = await SignatureAsync(catalog);
  return "Eşleşen cihazlar: " + string.Join(", ", matches.Select(d => d.Name)) + "\nKatalog: " + signature + "\nNihai paket bütünlüğünü ve sürücü sıralamasını Windows doğrular.";
 }
 public static Task<string> SignatureAsync(string path)=>Task.Run(()=>Authenticode.Verify(path));
 public async Task<string> InstallInfAsync(string inf)
 {
  var backup = LocalStore.FilePath("Backups");
  backup = Path.Combine(backup, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
  await BackupAsync(backup);
  var result = await Commands.ElevatedAsync("& $env:SystemRoot\\System32\\pnputil.exe /add-driver " + PackageSafety.QuotePs(inf) + " /install; if ($LASTEXITCODE -notin @(0,3010)) { throw ('PnPUtil kodu: ' + $LASTEXITCODE) }; if ($LASTEXITCODE -eq 3010) { 'Yeniden başlatma gerekiyor.' }");
  Journal.Write("Yerel sürücü paketi", result.Output + "\nYedek: " + backup, result.Success);
  if (!result.Success) throw new InvalidOperationException(result.Output);
  return result.Output + "\nÖnceki sürücüler: " + backup + "\nPaketin depoya eklenmesi, etkin sürücünün değiştiği anlamına gelmez. Envanteri yeniden tarayın.";
 }
 public Task<List<UpdateInfo>> ScanUpdatesAsync(CancellationToken token = default) => Commands.JsonAsync<List<UpdateInfo>>("$s=New-Object -ComObject Microsoft.Update.Session; $s.ClientApplicationID='FromZero'; $r=$s.CreateUpdateSearcher().Search(\"IsInstalled=0 and IsHidden=0 and Type='Driver'\"); ConvertTo-Json -Depth 4 -InputObject @($r.Updates | ForEach-Object { [pscustomobject]@{ Id=$_.Identity.UpdateID; Revision=$_.Identity.RevisionNumber; Title=$_.Title; Manufacturer=[string]$_.DriverManufacturer; Model=[string]$_.DriverModel; Eula=[string]$_.EulaText; Class=[string]$_.DriverClass } })", 240, token);
 public async Task<string> InstallUpdateAsync(UpdateInfo update)
 {
  if(update.IsFirmware)throw new InvalidOperationException("Firmware/BIOS paketleri bu sürümde FromZero üzerinden kurulmaz. Cihaz üreticisinin kendi güncelleme ve kurtarma akışını kullan.");
  if (!Guid.TryParse(update.Id, out var guid) || update.Revision < 0) throw new InvalidDataException("Geçersiz güncelleme kimliği.");
  var backup=Path.Combine(LocalStore.FilePath("Backups"), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
  await BackupAsync(backup);
  var script = "$s=New-Object -ComObject Microsoft.Update.Session; $s.ClientApplicationID='FromZero'; $r=$s.CreateUpdateSearcher().Search(\"IsInstalled=0 and IsHidden=0 and Type='Driver' and UpdateID='" + guid + "' and RevisionNumber=" + update.Revision + "\"); if ($r.Updates.Count -ne 1) { throw 'Güncelleme artık sunulmuyor; yeniden tarayın.' }; $u=$r.Updates.Item(0); if ($u.DriverClass -eq 'Firmware' -or $u.Title -match 'Firmware|BIOS') { throw 'Firmware kurulumu bu uygulamada desteklenmiyor.' }; if (-not $u.EulaAccepted) { $u.AcceptEula() }; $c=New-Object -ComObject Microsoft.Update.UpdateColl; [void]$c.Add($u); $d=$s.CreateUpdateDownloader(); $d.Updates=$c; $dr=$d.Download(); if ([int]$dr.ResultCode -ne 2) { throw ('İndirme sonucu: ' + $dr.ResultCode) }; $i=$s.CreateUpdateInstaller(); $i.Updates=$c; $ir=$i.Install(); if ([int]$ir.ResultCode -ne 2) { throw ('Kurulum sonucu: ' + $ir.ResultCode + ', hata: ' + $ir.HResult) }; 'Kurulum tamamlandı. Yeniden başlatma: ' + $ir.RebootRequired";
  var result = await Commands.ElevatedAsync(script);
  var report=result.Output+"\n"+Locale.Text("Driver backup: ")+backup;
  Journal.Write("Windows Update sürücüsü", update.Title + "\n" + report, result.Success);
  if (!result.Success) throw new InvalidOperationException(report);
  return report;
 }
}

public sealed class CapsuleService(InventoryService inventory, DriverService drivers)
{
 public async Task<string> CreateAsync(string parent, IProgress<string> progress)
 {
  var destination = Path.Combine(parent, "FromZero-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
  var appRoot = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
  if (Path.GetFullPath(destination).StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException("Kapsülü uygulamanın klasörü dışında bir konuma kaydet.");
  if (Directory.Exists(destination)) throw new IOException("Paket klasörü zaten var.");
  Directory.CreateDirectory(destination);
  progress.Report("Sürücüler USB paketine aktarılıyor…");
  try {
   await drivers.BackupAsync(Path.Combine(destination, "Drivers"));
   progress.Report("Donanım ve uygulama listesi kaydediliyor…");
   var devices = await inventory.DevicesAsync();
   progress.Report("Indexing signed driver packages and network recovery coverage…");
   var driverIndex=await OfflineDriverCatalog.BuildAsync(Path.Combine(destination,"Drivers"),devices);
   driverIndex=driverIndex with{Drivers=driverIndex.Drivers.Select(e=>e with{Inf=Path.Combine("Drivers",e.Inf),Catalog=e.Catalog.Length>0?Path.Combine("Drivers",e.Catalog):""}).ToList()};
   await File.WriteAllTextAsync(Path.Combine(destination,"driver-index.json"),JsonSerializer.Serialize(driverIndex,LocalStore.Json));
   await File.WriteAllTextAsync(Path.Combine(destination, "passport.json"), JsonSerializer.Serialize(await inventory.PassportAsync(), LocalStore.Json));
   await File.WriteAllTextAsync(Path.Combine(destination, "apps.json"), JsonSerializer.Serialize(await InventoryService.AppsAsync(), LocalStore.Json));
   await File.WriteAllTextAsync(Path.Combine(destination, "startup.json"), JsonSerializer.Serialize(InventoryService.Startup(), LocalStore.Json));
   var appTarget = Path.Combine(destination, "App"); Directory.CreateDirectory(appTarget);
   foreach (var file in PackageSafety.EnumerateSafeFiles(AppContext.BaseDirectory)) {
    if (new[] { ".exe", ".dll", ".json", ".config", ".txt", ".md", ".sig" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) {
     var target = PackageSafety.ResolveInside(appTarget, Path.GetRelativePath(AppContext.BaseDirectory, file));
     Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, false);
    }
   }
   await File.WriteAllTextAsync(Path.Combine(destination, "BASLA.txt"), "FROMZERO / KURTARMA KAPSÜLÜ\r\nApp\\FromZero.exe dosyasını açın. Format Hazırlığı > Kapsülü doğrula ile dosyaları kontrol edin. Sürücüler > Yerel INF ile cihazınıza eşleşen paketi seçin.\r\nBu paket Windows kurulum ortamı veya kişisel dosya yedeği değildir. Uygulama listesi ayar/parola yedeği içermez. Wi-Fi parolaları kaydedilmez.\r\nSHA-256 denetimi bozulmayı tespit eder; paketin kaynağını doğrulamaz. Paketi güvenilir bir USB'de saklayın.");
   progress.Report("Dosyalar SHA-256 ile doğrulanabilir hale getiriliyor…");
   var files = new List<CapsuleFile>();
   foreach (var file in PackageSafety.EnumerateSafeFiles(destination)) files.Add(await PackageSafety.HashFileAsync(destination, file));
   var manifest = new CapsuleManifest(1, DateTimeOffset.Now, Environment.MachineName, RuntimeInformation.OSArchitecture.ToString(), Environment.OSVersion.VersionString, files, devices);
   await File.WriteAllTextAsync(Path.Combine(destination, "capsule.json"), JsonSerializer.Serialize(manifest, LocalStore.Json));
   Journal.Write("Kurtarma kapsülü oluşturuldu", destination + "; " + files.Count + " dosya");
   return destination;
  } catch {
   await File.WriteAllTextAsync(Path.Combine(destination, "INCOMPLETE.txt"), "Paket tamamlanamadı. Bu klasörü kurtarma için kullanmayın.");
   throw;
  }
 }
 public async Task<string> VerifyAsync(string manifestPath)
 {
  var manifest = JsonSerializer.Deserialize<CapsuleManifest>(await File.ReadAllTextAsync(manifestPath), LocalStore.Json) ?? throw new InvalidDataException("Manifest okunamadı.");
  if (manifest.Files.Count == 0) throw new InvalidDataException("Kapsül boş.");
  var errors = await PackageSafety.VerifyAsync(Path.GetDirectoryName(manifestPath)!, manifest);
  if (File.Exists(Path.Combine(Path.GetDirectoryName(manifestPath)!, "INCOMPLETE.txt"))) errors.Add("Kapsül tamamlanmamış.");
  var result = errors.Count == 0 ? $"{manifest.Files.Count} dosyanın bütünlüğü doğrulandı.\nCihaz: {manifest.Machine}\nMimari: {manifest.Architecture}\nTarih: {manifest.Created:g}\nBu denetim paketin kaynağının güvenilir olduğunu kanıtlamaz." : "Kapsül doğrulanamadı:\n" + string.Join("\n", errors.Take(20));
  Journal.Write("Kapsül doğrulaması", result, errors.Count == 0);
  return result;
 }
}

public static class NetworkService
{
 public static string Describe()
 {
  var lines = new List<string>();
  var adapters = NetworkInterface.GetAllNetworkInterfaces().Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();
  var configured = adapters.Where(a => a.GetIPProperties().UnicastAddresses.Count > 0).ToList();
  foreach (var adapter in configured) {
   var ip = adapter.GetIPProperties();
   lines.Add(adapter.Name + "  /  " + adapter.Description + "\nDurum: " + adapter.OperationalStatus + " • Tür: " + adapter.NetworkInterfaceType + "\nIPv4: " + string.Join(", ", ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address)) + "\nAğ geçidi: " + string.Join(", ", ip.GatewayAddresses.Select(g => g.Address)) + "\nDNS: " + string.Join(", ", ip.DnsAddresses) + "\n");
  }
  return lines.Count > 0 ? string.Join("\n", lines) + "\nIP adresi olmayan alt ağ filtreleri listelenmez. Bütün cihazlar Sürücüler bölümündedir." : adapters.Count > 0 ? "Adaptörler var, ancak henüz IP adresi alınmamış.\n" + string.Join("\n", adapters.Take(12).Select(a => a.Name + " / " + a.OperationalStatus)) : "Ağ adaptörü bulunamadı. Sürücüler bölümünden eksik cihazları kontrol edin.";
 }
 public static async Task<string> DiagnoseAsync()
 {
  var lines = new List<string>();
  var active = NetworkInterface.GetAllNetworkInterfaces().Where(a => a.OperationalStatus == OperationalStatus.Up && a.NetworkInterfaceType != NetworkInterfaceType.Loopback && a.GetIPProperties().UnicastAddresses.Count > 0).ToList();
  lines.Add(active.Count > 0 ? "✓ Etkin ağ bağlantısı var." : "! Etkin ağ bağlantısı bulunamadı.");
  foreach (var gateway in active.SelectMany(a => a.GetIPProperties().GatewayAddresses).Select(g => g.Address).Where(a => a.AddressFamily == AddressFamily.InterNetwork).Distinct()) {
   try { using var ping = new Ping(); var r = await ping.SendPingAsync(gateway, 1800); lines.Add(r.Status == IPStatus.Success ? "✓ Ağ geçidi yanıtı: " + r.RoundtripTime + " ms" : "? Ağ geçidi ICMP yanıtı yok; bu tek başına arıza kanıtı değil."); } catch (PingException ex) { lines.Add("? Ağ geçidi testi: " + ex.Message); }
  }
  try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)); var addresses = await Dns.GetHostAddressesAsync("www.microsoft.com", cts.Token); lines.Add("✓ DNS çözümlemesi başarılı (microsoft.com)."); } catch (Exception ex) when (ex is SocketException or OperationCanceledException) { lines.Add("! DNS testi başarısız veya zaman aşımı."); }
  try { using var client = new TcpClient(); await client.ConnectAsync("www.microsoft.com", 443).WaitAsync(TimeSpan.FromSeconds(8)); lines.Add("✓ Dış bağlantı: microsoft.com:443 erişilebilir."); } catch (Exception ex) when (ex is SocketException or TimeoutException) { lines.Add("! Dış bağlantı testi başarısız. VPN, güvenlik duvarı veya servis sağlayıcısını kontrol edin."); }
  lines.Add("\nBu testler bant genişliğini ölçmez. Test hedefi Microsoft; ağ yöneticiniz sorgu ve bağlantıyı görebilir.");
  var report = string.Join("\n", lines);
  Journal.Write("Bağlantı teşhisi", report);
  return report;
 }
}

public sealed class LiveMetrics
{
 [StructLayout(LayoutKind.Sequential)] private sealed class MemoryStatus { public uint Length = (uint)Marshal.SizeOf<MemoryStatus>(); public uint Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
 [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);
 [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
 private long oldIdle, oldKernel, oldUser;
 public (double Cpu, double UsedGb, double TotalGb, double RamPercent) Read()
 {
  var memory = new MemoryStatus();
  if (!GlobalMemoryStatusEx(memory)) throw new Win32Exception(Marshal.GetLastWin32Error());
  double cpu = 0;
  if (GetSystemTimes(out var idle, out var kernel, out var user)) {
   var total = kernel - oldKernel + user - oldUser;
   if (oldKernel != 0 && total > 0) cpu = Math.Clamp(100d * (total - (idle - oldIdle)) / total, 0, 100);
   oldIdle = idle; oldKernel = kernel; oldUser = user;
  }
  return (cpu, (memory.Total - memory.Available) / 1073741824d, memory.Total / 1073741824d, memory.Load);
 }
}
