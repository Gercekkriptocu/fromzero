using System.IO;
using System.Text.Json;

namespace FromZero;

public record CatalogDriverReview(CatalogDriverCandidate Candidate,DeviceInfo Device,string Folder,OfflineDriverEntry Package,DriverPackageMatch Match);
public record CatalogDriverInstallResult(bool Verified,bool Restart,string Detail);

public static class CatalogDriverInstaller
{
 public static async Task<IReadOnlyList<CatalogDriverReview>> ReviewAsync(CatalogDriverCandidate candidate,DeviceInfo device,
  DriverInfo? installed,CatalogDriverDownload download)
 {
  var index=await OfflineDriverCatalog.BuildAsync(download.Folder,[device]);
  return index.Drivers.Where(package=>package.HardwareIds.Any(id=>device.HardwareIds.Contains(id,StringComparer.OrdinalIgnoreCase)))
   .Select(package=>new CatalogDriverReview(candidate,device,download.Folder,package,DriverPackageMatching.Assess(package,device,installed)))
   .OrderByDescending(review=>review.Match.Installable).ThenBy(review=>review.Match.HardwareIdOrder).ThenByDescending(review=>
    Version.TryParse(review.Match.Version,out var version)?version:new Version()).ToArray();
 }

 public static async Task<CatalogDriverInstallResult> InstallAsync(CatalogDriverReview review,IProgress<string> progress,CancellationToken token)
 {
  var inventory=new InventoryService();
  var device=(await inventory.DevicesAsync()).SingleOrDefault(d=>d.Id.Equals(review.Device.Id,StringComparison.OrdinalIgnoreCase));
  if(device==null)throw new InvalidDataException("Device is no longer connected");
  var installed=(await inventory.DriversAsync()).FirstOrDefault(d=>d.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
  var match=DriverPackageMatching.Assess(review.Package,device,installed);
  if(!match.Installable)throw new InvalidDataException(match.Reason);
  var sourceInf=PackageSafety.ResolveInside(review.Folder,review.Package.Inf);
  var sourceCatalog=PackageSafety.ResolveInside(review.Folder,review.Package.Catalog);
  if((await PackageSafety.HashFileAsync(review.Folder,sourceInf)).Sha256!=review.Package.InfHash||
     (await PackageSafety.HashFileAsync(review.Folder,sourceCatalog)).Sha256!=review.Package.CatalogHash)
   throw new InvalidDataException("Reviewed driver package changed. Download and review it again.");

  var stage=Path.Combine(PreparationPackages.StageRoot(Guid.NewGuid().ToString("N")),"Driver");
  Directory.CreateDirectory(stage);
  foreach(var source in PackageSafety.EnumerateSafeFiles(review.Folder))
  {
   token.ThrowIfCancellationRequested();
   if(Path.GetDirectoryName(source)!.Equals(review.Folder,StringComparison.OrdinalIgnoreCase))continue;
   var target=PackageSafety.ResolveInside(stage,Path.GetRelativePath(review.Folder,source));
   Directory.CreateDirectory(Path.GetDirectoryName(target)!);
   File.Copy(source,target,false);
  }
  var inf=PackageSafety.ResolveInside(stage,review.Package.Inf);
  var catalog=PackageSafety.ResolveInside(stage,review.Package.Catalog);
  if((await PackageSafety.HashFileAsync(stage,inf)).Sha256!=review.Package.InfHash||
     (await PackageSafety.HashFileAsync(stage,catalog)).Sha256!=review.Package.CatalogHash)
   throw new InvalidDataException("Driver package changed while staging.");
  var service=new DriverService();
  progress.Report("Checking signed driver package…");
  await service.CheckInfAsync(inf,[device]);
  progress.Report("Backing up drivers…");
  await service.BackupAsync(Path.Combine(LocalStore.FilePath("Backups"),"Catalogue-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
  token.ThrowIfCancellationRequested();
  progress.Report("Installing reviewed driver…");
  var command=await Commands.ElevatedAsync("$output=& "+PackageSafety.QuotePs(Path.Combine(Environment.SystemDirectory,"pnputil.exe"))+
   " /add-driver "+PackageSafety.QuotePs(inf)+" /install 2>&1;$code=$LASTEXITCODE;[pscustomobject]@{ExitCode=$code;Output=($output|Out-String)}|ConvertTo-Json");
  if(!command.Success)throw new IOException(command.Output);
  var result=JsonSerializer.Deserialize<CommandResult>(command.Output.Trim().TrimStart('\uFEFF'),LocalStore.Json)
   ??throw new InvalidDataException("Driver installation returned no result.");
  if(result.ExitCode is not (0 or 3010))throw new IOException(result.Output);
  for(var attempt=0;attempt<4;attempt++)
  {
   var actual=(await inventory.DriversAsync()).FirstOrDefault(d=>d.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
   if(actual is {IsSigned:true}&&Version.TryParse(actual.Version,out var current)&&current==Version.Parse(match.Version))
    return new(true,result.ExitCode==3010,"Installation verified · "+actual.Provider+" "+actual.Version+"\n"+result.Output);
   if(result.ExitCode==3010)break;
   await Task.Delay(1000);
  }
  return new(false,result.ExitCode==3010,result.Output+"\n"+Locale.Text("Windows retained the active driver or final verification is pending. Rescan after restarting."));
 }
}
