using System.IO;
namespace FromZero;
public static class BrandMigration
{
 // Only compatibility code uses the previous preview name. Never overwrite newer user data.
 public const string PreviousName="Veyrakit";
 public static void CopySettings()
 {
  var previous=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),PreviousName);
  if(!Directory.Exists(previous))return;
  Directory.CreateDirectory(LocalStore.Root);
  var marker=Path.Combine(LocalStore.Root,"previous-preview-imported.txt");if(File.Exists(marker))return;
  foreach(var name in new[]{"games.json","baseline.json","fps-baseline.json","history.jsonl","game-session.json"}){
   var old=PackageSafety.ResolveInside(previous,name);var target=PackageSafety.ResolveInside(LocalStore.Root,name);
   if(File.Exists(old)&&!File.Exists(target))File.Copy(old,target,false);
  }
  File.WriteAllText(marker,"Önceki ön izleme ayarları kopyalandı. Eski yedekler silinmedi.");
 }
 public static string PreviousBackups=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),PreviousName,"Backups");
}
