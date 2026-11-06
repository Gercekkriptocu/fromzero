using System.Diagnostics;
using System.IO;

namespace FromZero;

internal static class UpdatePackageInstaller
{
 private static readonly string[] InnoArguments=["/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-","/NOCLOSEAPPLICATIONS","/NORESTARTAPPLICATIONS"];

 internal static DownloadRecipe InstallRecipe(DownloadRecipe recipe)
 {
  if(recipe.Id=="app-vscode"&&MachineScope(["Microsoft Visual Studio Code"]))
   return AppDownloads.Recipes.Single(candidate=>candidate.Id=="prepare-vscode");
  return recipe;
 }

 internal static PreparationPackage? Definition(DownloadRecipe recipe, RuntimePackage? runtime,bool? machineScope=null)
 {
  if(recipe.PublisherHashOnly||recipe.DriverTool)return null;
  if(recipe.CatalogSigned)
  {
   if(!CommunityPackageCatalog.Allowed(recipe))return null;
   var community=CommunityPackageCatalog.Definitions.Single(package=>"app-"+package.Id==recipe.Id);
   if(recipe.Extension==".msi")return new(recipe.Id,["/qn","/norestart"],community.Aliases,
    ProcessName:community.Slug=="transmission"?"transmission-qt":"");
   return community.Slug switch
   {
    "zed"=>new(recipe.Id,InnoArguments.Concat(["/CURRENTUSER"]).ToArray(),community.Aliases,ProcessName:"zed"),
    "zoom"=>new(recipe.Id,["/silent"],community.Aliases,ProcessName:"Zoom"),
    _=>null
   };
  }
  if(runtime is {Supported:true}&&runtime.Recipe==recipe)
   return new(recipe.Id,["/install","/quiet","/norestart"],[]);
  if(recipe.Id is "directx-offline" or "xna"||recipe.Id.StartsWith("vc",StringComparison.Ordinal)&&!recipe.Id.StartsWith("vscode",StringComparison.Ordinal))
   return recipe.Id=="xna"?new(recipe.Id,["/qn","/norestart"],["Microsoft XNA Framework"]):PreparationPackages.Definition(recipe.Id);
  if(recipe.Id is "app-firefox" or "app-vlc")
   return PreparationPackages.Definition(recipe.Id=="app-firefox"?"firefox":"prepare-vlc");
  if(recipe.Id=="app-chrome")return PreparationPackages.Definition("prepare-chrome");
  if(recipe.Id is "app-vscode" or "prepare-vscode")return new(recipe.Id,
   InnoArguments.Concat(["/MERGETASKS=!runcode",recipe.Id=="prepare-vscode"?"/ALLUSERS":"/CURRENTUSER"]).ToArray(),["Microsoft Visual Studio Code"],ProcessName:"Code");
  if(recipe.Id=="app-vivaldi")return new(recipe.Id,
   (machineScope??MachineScope(["Vivaldi"]))?["--vivaldi-silent","--do-not-launch-chrome","--system-level"]:["--vivaldi-silent","--do-not-launch-chrome"],
   ["Vivaldi"],ProcessName:"vivaldi");
  if(recipe.Id.StartsWith("app-",StringComparison.Ordinal)&&PreparationExperience.Apps.Any(app=>app.Id==recipe.Id[4..]&&app.Id!="steam"))
  {
   var definition=PreparationPackages.Definition(recipe.Id);
   if((recipe.Extension==".msi")==definition.Arguments.Contains("/qn"))return definition;
  }
  return null;
 }

 private static bool MachineScope(string[] patterns)
 {
  var records=InstalledSoftware.ReadDesktopApps().Where(app=>ApplicationCatalog.Matches(app,patterns)).ToArray();
  return records.Length>0&&records.All(app=>app.Source.StartsWith("LocalMachine/",StringComparison.Ordinal));
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

 internal static async Task<bool> TryCloseForUpdateAsync(PreparationPackage definition,CancellationToken token)
 {
  token.ThrowIfCancellationRequested();
  if(definition.ProcessName.Length==0)return true;
  var processes=Process.GetProcessesByName(definition.ProcessName);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
  timeout.CancelAfter(TimeSpan.FromSeconds(8));
  try
  {
   foreach(var process in processes)
    if(!process.HasExited&&process.MainWindowHandle!=IntPtr.Zero)process.CloseMainWindow();
   await Task.WhenAll(processes.Select(process=>process.WaitForExitAsync(timeout.Token)));
   return true;
  }
  catch(OperationCanceledException)when(!token.IsCancellationRequested){return false;}
  catch(Exception exception)when(exception is System.ComponentModel.Win32Exception or InvalidOperationException){return false;}
  finally{foreach(var process in processes)process.Dispose();}
 }
}
