using System.IO;

namespace FromZero;

public static class SilentDriverUpdates
{
 public static async Task<CatalogDriverInstallResult> InstallAsync(WindowsOffer offer,IProgress<string> progress,CancellationToken token)
 {
  if(!offer.SelectableDriver)throw new InvalidDataException("This offer is not an installable driver.");
  var inventory=new InventoryService();
  var matching=(await inventory.DevicesAsync()).Where(device=>WindowsUpdateCatalog.MatchesDevice(offer,device)).ToArray();
  if(matching.Length==0)throw new InvalidDataException("The update no longer matches a connected device.");
  var reports=new List<string>();
  foreach(var device in matching)
  {
   token.ThrowIfCancellationRequested();
   progress.Report("Finding the signed driver package · "+device.Name);
   var installed=(await inventory.DriversAsync()).FirstOrDefault(driver=>driver.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
   var target=Version.Parse(offer.Version);
   if(installed is {IsSigned:true}&&Version.TryParse(installed.Version,out var current)&&current>=target)
   {
    reports.Add(device.Name+" · "+Locale.Text("Already up to date"));
    continue;
   }
   // The public catalogue can lag behind WUA. Reuse the exact identity downloaded for THIS PC.
   var download=await WindowsDriverPackageCache.PrepareAsync(offer,progress,token);
   var candidate=new CatalogDriverCandidate(offer.Id,offer.Title,offer.Version,offer.Date,"",offer.HardwareId,"Windows Update / "+offer.Key);
   var inspected=await CatalogDriverInstaller.ReviewAsync(candidate,device,installed,download);
   var review=inspected.FirstOrDefault(item=>item.Match.Installable&&Version.TryParse(item.Match.Version,out var version)&&version==target);
   if(review==null)throw new InvalidDataException(Locale.Text("No verified compatible driver package was found.")+" "+string.Join(" / ",inspected.Select(item=>item.Match.Reason).Distinct()));
   var result=await CatalogDriverInstaller.InstallAsync(review,progress,token);
   reports.Add(device.Name+" · "+result.Detail);
   if(!result.Verified||result.Restart)return new(result.Verified,result.Restart,string.Join("\n",reports));
  }
  return new(true,false,string.Join("\n",reports));
 }
}
