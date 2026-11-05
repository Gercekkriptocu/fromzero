using System.Diagnostics;
using System.IO;

namespace FromZero;

internal static class UpdatePackageInstaller
{
 internal static PreparationPackage? Definition(DownloadRecipe recipe, RuntimePackage? runtime)
 {
  if(recipe.CatalogSigned||recipe.PublisherHashOnly||recipe.DriverTool)return null;
  if(runtime is {Supported:true}&&runtime.Recipe==recipe)
   return new(recipe.Id,["/install","/quiet","/norestart"],[]);
  if(recipe.Id is "directx-offline" or "xna"||recipe.Id.StartsWith("vc",StringComparison.Ordinal)&&!recipe.Id.StartsWith("vscode",StringComparison.Ordinal))
   return recipe.Id=="xna"?new(recipe.Id,["/qn","/norestart"],["Microsoft XNA Framework"]):PreparationPackages.Definition(recipe.Id);
  if(recipe.Id is "app-firefox" or "app-vlc")
   return PreparationPackages.Definition(recipe.Id=="app-firefox"?"firefox":"prepare-vlc");
  if(recipe.Id=="app-chrome")return PreparationPackages.Definition("prepare-chrome");
  if(recipe.Id.StartsWith("app-",StringComparison.Ordinal)&&PreparationExperience.Apps.Any(app=>app.Id==recipe.Id[4..]&&app.Id is not ("vscode" or "steam")))
  {
   var definition=PreparationPackages.Definition(recipe.Id);
   if((recipe.Extension==".msi")==definition.Arguments.Contains("/qn"))return definition;
  }
  return null;
 }

 internal static async Task<VerifiedDownload> AcquireAsync(DownloadRecipe recipe,IProgress<string> progress,CancellationToken token)
 {
  // A versionless URL can serve a different build, so only reuse a matching release record.
  if(AppDownloads.Cached(recipe) is {} cached&&cached.Name==recipe.Name&&
     (recipe.ExpectedSha256.Length>0||recipe.ExpectedSha512.Length>0||recipe.Id.StartsWith("app-",StringComparison.Ordinal)))
  {
   try{await AppDownloads.VerifyCachedAsync(recipe,cached);return cached;}
   catch(Exception ex) when(ex is IOException or InvalidDataException){ }
  }
  await AppDownloads.DownloadAsync(recipe,progress,token);
  return AppDownloads.Cached(recipe)??throw new IOException("Verified installer record is missing.");
 }

 internal static bool Present(DownloadRecipe recipe,PreparationPackage definition,ApplicationRelease? app,RuntimePackage? runtime)
 {
  if(runtime!=null)
   return ApplicationCatalog.State(RuntimeVersions(runtime),runtime.Version)=="Güncel veya daha yeni";
  if(app!=null)
   return ApplicationCatalog.State(ApplicationVersions(definition),app.Version)=="Güncel veya daha yeni";
  var componentId=recipe.Id=="directx-offline"?"directx":recipe.Id;
  var related=GameComponents.Scan(InstalledSoftware.ReadDesktopApps()).Components.Where(c=>c.Recipe==componentId||componentId=="xna"&&c.Id=="xna").ToArray();
  return related.Length>0&&related.All(c=>c.Present);
 }

 internal static string[] ApplicationVersions(PreparationPackage definition)=>InstalledSoftware.ReadDesktopApps()
  .Where(app=>ApplicationCatalog.Matches(app,definition.Patterns)).Select(app=>app.Version).Distinct().ToArray();

 internal static string[] RuntimeVersions(RuntimePackage runtime)
 {
  var framework=runtime.Id.Split('-')[1] switch
  {
   "runtime"=>"Microsoft.NETCore.App", "desktop"=>"Microsoft.WindowsDesktop.App", "aspnet"=>"Microsoft.AspNetCore.App", _=>"sdk"
  };
  var target=Version.Parse(runtime.Version);
  return RuntimeCatalog.InstalledVersions().GetValueOrDefault(runtime.Architecture+"/"+framework,[]).Where(version=>
   Version.TryParse(version,out var v)&&v.Major==target.Major&&v.Minor==target.Minor&&
   (framework!="sdk"||v.Build/100==target.Build/100)).ToArray();
 }

 internal static bool AppIsRunning(PreparationPackage definition)
 {
  if(definition.ProcessName.Length==0)return false;
  var processes=Process.GetProcessesByName(definition.ProcessName);
  foreach(var process in processes)process.Dispose();
  return processes.Length>0;
 }
}
