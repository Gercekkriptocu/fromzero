using System.IO;
using System.Text.Json;
namespace FromZero;
public record TcpBackup(string Original,DateTimeOffset Changed);
public static class TcpOptimizer
{
 public static string Normalize(string value)
 {
  var level=value.ToLowerInvariant();if(level is not "disabled" and not "highlyrestricted" and not "restricted" and not "normal" and not "experimental")throw new InvalidDataException("TCP otomatik ayar seviyesi desteklenmiyor.");return level;
 }
 public static async Task<string> ReadAsync()=>Normalize(await Commands.JsonAsync<string>("[string](Get-NetTCPSetting -SettingName Internet).AutoTuningLevelLocal | ConvertTo-Json"));
 private static string PathName=>LocalStore.FilePath("tcp-optimizer.json");
 public static bool HasBackup=>File.Exists(PathName);
 private static async Task SetAsync(string value)
 {
  value=Normalize(value);var r=await Commands.ElevatedAsync("& $env:SystemRoot\\System32\\netsh.exe interface tcp set global autotuninglevel="+value+"; if ($LASTEXITCODE -ne 0) { throw 'TCP ayarı değiştirilemedi.' }");if(!r.Success)throw new InvalidOperationException(r.Output);
  if(await ReadAsync()!=value)throw new IOException("TCP ayarının uygulandığı doğrulanamadı; yedek korundu.");
 }
 public static async Task<string> ApplyAsync()
 {
  if(HasBackup)throw new InvalidOperationException("Önce önceki aktarım profilini geri al.");
  var old=await ReadAsync();if(old=="normal")return "TCP pencere otomatik ayarı zaten Normal. Gereksiz değişiklik yapılmadı.";
  await File.WriteAllTextAsync(PathName,JsonSerializer.Serialize(new TcpBackup(old,DateTimeOffset.Now),LocalStore.Json));
  try{await SetAsync("normal");}catch{try{await SetAsync(old);File.Delete(PathName);}catch{ }throw;}
  Journal.Write("TCP aktarım ayarı",old+" → normal");return "TCP alım penceresi otomatik ayarı Normal olarak doğrulandı. Bu ayar bütün TCP bağlantılarını etkiler; sinyal gücünü veya internet paketinin hız sınırını değiştirmez. Geri al düğmesiyle önceki seviyeye dönebilirsin.";
 }
 public static async Task<string> RestoreAsync()
 {
  if(!HasBackup)return "Etkin aktarım profili yok.";
  var old=JsonSerializer.Deserialize<TcpBackup>(await File.ReadAllTextAsync(PathName),LocalStore.Json)??throw new InvalidDataException("TCP yedeği okunamadı.");Normalize(old.Original);
  if(await ReadAsync()=="normal")await SetAsync(old.Original);
  File.Delete(PathName);Journal.Write("TCP profili geri alındı",old.Original);return "TCP profili kapatıldı. Sonradan değiştirdiğin ayarlara dokunulmadı.";
 }
}
