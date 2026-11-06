using System.IO;

namespace FromZero;

/// <summary>Same-computer WUA cache export; never substitutes a similarly named public catalogue update.</summary>
public static class WindowsDriverPackageCache
{
 public static string ExportScript(WindowsOffer offer,string destination)
 {
  if(!offer.QueueInstallable||!offer.SelectableDriver||!Guid.TryParse(offer.Id,out var id)||offer.Revision<0||
     !Version.TryParse(offer.Version,out _)||!MicrosoftDriverCatalog.ValidHardwareId(offer.HardwareId))
   throw new InvalidDataException("Invalid driver cache selection.");
  return "$destination="+PackageSafety.QuotePs(destination)+";$hardware="+PackageSafety.QuotePs(offer.HardwareId)+";"+
   "$s=New-Object -ComObject Microsoft.Update.Session;$s.ClientApplicationID='FromZero signed driver fallback';"+
   "$searcher=$s.CreateUpdateSearcher();$r=$searcher.Search(\"IsInstalled=0 and IsHidden=0 and Type='Driver' and UpdateID='"+id+"' and RevisionNumber="+offer.Revision+"\");"+"""
   if([int]$r.ResultCode -ne 2 -or $r.Updates.Count -ne 1){throw 'The selected driver is no longer offered. Rescan updates.'};
   $u=$r.Updates.Item(0);
   if($u.DriverClass -eq 'Firmware' -or $u.Title -match 'Firmware|BIOS' -or $u.DriverHardwareID -ine $hardware){throw 'Driver identity or hardware changed.'};
   if(!$u.EulaAccepted){$u.AcceptEula()};
   if(!$u.IsDownloaded){
    $c=New-Object -ComObject Microsoft.Update.UpdateColl;[void]$c.Add($u);$d=$s.CreateUpdateDownloader();$d.Updates=$c;$dr=$d.Download();
    if([int]$dr.ResultCode -ne 2 -or !$u.IsDownloaded){throw ('Windows driver download did not complete. (0x{0:X8})' -f [int]$dr.GetUpdateResult(0).HResult)};
   };
   $script:count=0;
   function Export-Payload($update,[int]$depth){
    if($depth -gt 4 -or $script:count -ge 32){throw 'Unsupported bundled driver layout.'};
    if($update.BundledUpdates.Count -gt 0){foreach($child in $update.BundledUpdates){Export-Payload $child ($depth+1)}}else{
     $path=Join-Path $destination ('payload-'+$script:count);$script:count++;
     [void][IO.Directory]::CreateDirectory($path);
     $update.CopyFromCache($path,$false);
    }
   };
   Export-Payload $u 0;
   'Driver cache exported'
   """;
 }

 public static async Task<CatalogDriverDownload> PrepareAsync(WindowsOffer offer,IProgress<string> progress,CancellationToken token)
 {
  token.ThrowIfCancellationRequested();
  var folder=Path.Combine(PreparationPackages.StageRoot(Guid.NewGuid().ToString("N")),"WindowsDriver");
  Directory.CreateDirectory(folder);
  progress.Report("Preparing the downloaded Windows driver package…");
  // CopyFromCache verifies WUA file hashes. Downloads are not interrupted halfway through COM operations.
  var export=await Commands.ElevatedAsync(ExportScript(offer,folder));
  if(!export.Success)throw new IOException(export.Output);
  return await InspectExportAsync(folder,offer,progress,token);
 }

 internal static async Task<CatalogDriverDownload> InspectExportAsync(string folder,WindowsOffer offer,IProgress<string> progress,CancellationToken token)
 {
  var files=PackageSafety.EnumerateSafeFiles(folder).Take(10001).ToArray();
  if(files.Length>10000||files.Sum(path=>new FileInfo(path).Length)>4L*1024*1024*1024)
   throw new InvalidDataException("The driver cache exceeds the package limit.");
  long expandedTotal=0;
  foreach(var cab in files.Where(path=>Path.GetExtension(path).Equals(".cab",StringComparison.OrdinalIgnoreCase)))
  {
   token.ThrowIfCancellationRequested();
   var paths=MicrosoftDriverCatalog.ValidateCabinet(cab,out var size);
   expandedTotal+=size;
   if(expandedTotal>4L*1024*1024*1024)throw new InvalidDataException("The driver package expands beyond the total size limit.");
   if(new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace<size*2+256L*1024*1024)
    throw new IOException("Not enough free space to extract and stage the driver package.");
   var contents=Path.Combine(Path.GetDirectoryName(cab)!,"contents-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(contents);
   foreach(var path in paths)PackageSafety.ResolveInside(contents,path);
   progress.Report("Extracting Microsoft driver package…");
   var extracted=await Commands.RunAsync(Path.Combine(Environment.SystemDirectory,"expand.exe"),[cab,"-F:*",contents],180,token);
   if(!extracted.Success)throw new IOException("Windows could not extract the driver cabinet. "+extracted.Output);
   var actual=PackageSafety.EnumerateSafeFiles(contents).Select(path=>Path.GetRelativePath(contents,path)).ToArray();
   if(!actual.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(paths))throw new InvalidDataException("The extracted driver cabinet is incomplete or unexpected.");
   File.Delete(cab);
  }
  var infs=PackageSafety.EnumerateSafeFiles(folder).Where(path=>Path.GetExtension(path).Equals(".inf",StringComparison.OrdinalIgnoreCase)).ToArray();
  if(infs.Length==0)throw new InvalidDataException("No driver INF was found in the downloaded Windows package.");
  return new(folder,infs,["Windows Update / "+offer.Key],"");
 }
}
