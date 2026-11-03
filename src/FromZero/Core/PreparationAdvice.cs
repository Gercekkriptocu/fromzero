using System.IO;
namespace FromZero;
public record PlanAdvice(string Id,string Title,string Category,string Current,string Target,string Reason,string Source,string Download,string Restart,string Recovery);
public static class PreparationAdvice
{
 public static List<PlanAdvice> Build(PreparationJob job,PreparationSnapshot snapshot,ComponentInventory components)
 {
  var result=new List<PlanAdvice>();
  foreach(var step in job.Steps){
   var category=PreparationExperience.Phase(step);string current="Read again at execution",target="Verify the selected step",reason="Selected in your preparation plan",source="Local Windows inventory",download="No download",restart="Not expected",recovery="Read-only check";
   switch(step.Kind){
    case "drivers":var bad=snapshot.Devices.Where(d=>d.Present&&d.Problem!=0).ToArray();current=$"{bad.Length} devices report a problem";target="Windows-ranked compatible driver offers";reason=bad.Length>0?string.Join("; ",bad.Take(6).Select(d=>d.Name+" / "+d.Problem)):"Check for applicable updates; no missing driver is assumed";source="Windows Update / exact device identity";download="Determined by the Windows offer";restart="May be required";recovery="Driver backup; Device Manager rollback if available";break;
    case "oem":current=snapshot.Manufacturer+" "+snapshot.Model;target="Model-supported driver updates";reason="OEM feature support; unsupported models are skipped";source="Official manufacturer tool / support";download="Only after model eligibility is checked";restart="May be required";recovery="Backup; vendor recovery varies";break;
    case "gpu-apps":var vendors=snapshot.Devices.Where(d=>d.Present).Select(GpuSoftware.Vendor).Where(v=>v.Length>0).Distinct().ToArray();current=vendors.Length>0?string.Join(", ",vendors):"GPU vendor not identified";target="Official app for detected hardware only";reason="Driver and display controls; unrelated GPU tools are not installed";source="NVIDIA / AMD / Intel official download";download="Verified installer size shown before download";restart="Vendor may request it";recovery="Vendor uninstall flow; not a full driver rollback";break;
    case "windows":current="Applicable offers checked at execution";target="Selected security and stability updates";source="Windows Update";download="Offer-specific";restart="May be required";recovery="Windows recovery / uninstall availability varies";break;
    case "offline":current=job.Options.OfflineFolder;target="Matching signed local packages, network first";source="Selected USB folder + signed CAT";download="Offline files only";restart="May be required";recovery="Existing driver backup";break;
    case "backup":current="Third-party installed driver packages";target="Exported copy before driver changes";reason="Recovery evidence before any driver installation";source="Windows PnPUtil export";recovery="Preserves exported files; not inbox drivers";break;
    case "app":var id=PreparationPackages.RecipeId(step.Id[4..]);var installed=snapshot.Apps.Where(a=>PreparationPackages.Matches(id,a)).ToArray();current=installed.Length>0?string.Join(", ",installed.Select(a=>a.Version)):"Not installed";target="Verified stable producer release";reason="You selected this app; not required by Windows";source="Pinned official producer source";download="Signed installer / reuses a verified cache";restart="Usually not required; installer decides";recovery="Vendor uninstall; user data is not backed up here";break;
    case "package":var package=step.Id[8..];var found=components.Components.Where(c=>c.Recipe==package||(package=="directx-offline"&&c.Recipe=="directx")).ToArray();current=found.Length==0?"Requirement needs review":found.All(c=>c.Present)?"Present in this scan":"Missing components in this scan";target="Selected runtime and architecture";reason=job.Options.RequiredLibraries.Contains(package)?"Explicit runtime selection from a dependency review":"Explicit compatibility-library selection";source="Microsoft signed runtime installer";download="Verified cache or official package";restart="Installer may request it";recovery="Shared runtime: do not remove as app leftovers";break;
    case "preferences":current="Power and game settings recorded before changes";target=job.Options.PowerPreference+" / "+job.Options.GraphicsPreference;reason="Explicit settings selection; FPS is not guaranteed";recovery="Compare and restore only settings still owned by this session";break;
   }
   result.Add(new(step.Id,step.Title,category,current,target,reason,source,download,restart,recovery));
  }
  return result;
 }
 public static string Describe(IEnumerable<PlanAdvice> entries)=>string.Join("\n\n",entries.Select(a=>$"{a.Title}\nCurrent: {a.Current}\nTarget: {a.Target}\nWhy: {a.Reason}\nSource: {a.Source}\nDownload: {a.Download}\nRestart: {a.Restart}\nRecovery: {a.Recovery}"));
}
