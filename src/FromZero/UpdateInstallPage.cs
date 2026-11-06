using System.IO;
using System.Windows;

namespace FromZero;

public partial class MainWindow
{
 private async Task InstallSelectedUpdatesAsync()
 {
  if(windowsSelection.Count+packageSelection.Count+catalogDriverQueue.Count==0)await SelectAllMissingUpdatesAsync();
  var offers=(windowsScan?.Offers??[]).Where(o=>windowsSelection.Contains(o.Key)).ToArray();
  var recipes=AppDownloads.Recipes.Where(r=>packageSelection.Contains(r.Id)).ToArray();
  if(offers.Length+recipes.Length+catalogDriverQueue.Count==0){Detail("Update installation","No applicable updates were found.");return;}

  using var cancellation=new CancellationTokenSource();
  var token=cancellation.Token;
  var operationId=Guid.NewGuid().ToString("N");
  var stopFile=LocalStore.FilePath("update-stop-"+operationId);
  using var stopRegistration=token.Register(()=>{
   try{File.WriteAllText(stopFile,"");}catch(IOException){}catch(UnauthorizedAccessException){}
  });
  updateCancellation=cancellation;
  CancelOperation.Visibility=Visibility.Visible;
  var progress=new Progress<string>(message=>OperationTitle.Text=Locale.Text(message));
  var results=new List<string>();
  var installedKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  bool restart=false,attention=false;
  try
  {
   var nativeDrivers=new List<WindowsOffer>();
   var allowed=offers.Where(o=>o.QueueInstallable).OrderBy(o=>o.Kind=="Driver").ToArray();
   foreach(var offer in offers.Where(o=>!o.QueueInstallable))
   {
    results.Add(offer.Title+" · "+Locale.Text("Requires manual review"));
    attention=true;
   }
   if(allowed.Any(o=>o.Kind=="Driver"))
   {
    OperationTitle.Text=Locale.Text("Backing up drivers…");
    try{await driverService.BackupAsync(Path.Combine(LocalStore.FilePath("Backups"),"Updates-"+operationId));}
    catch(Exception ex) when(ex is not OperationCanceledException)
    {
     results.Add(Locale.Text("Driver backup failed. Driver installations were skipped.")+" "+ex.Message);
     allowed=allowed.Where(o=>o.Kind!="Driver").ToArray();
     attention=true;
    }
   }
   foreach(var batch in allowed.Chunk(40))
   {
    token.ThrowIfCancellationRequested();
    OperationTitle.Text=Locale.Text("Downloading and installing Windows updates and drivers…");
    var command=await Commands.ElevatedAsync(PreparationWindows.SelectionScript(batch,stopFile));
    if(!command.Success){results.Add(command.Output);attention=true;break;}
    var report=System.Text.Json.JsonSerializer.Deserialize<List<PreparationWindowsResult>>(command.Output.Trim().TrimStart('\uFEFF'),LocalStore.Json)
     ??throw new InvalidDataException("Windows Update returned no installation report.");
    foreach(var item in report.Where(r=>r.Code!=5))
    {
     var verified=item.Code==2&&item.Installed;
     var driver=batch.FirstOrDefault(o=>o.Kind=="Driver"&&o.Key==item.Key);
     if(!verified&&!item.Reboot&&item.Code!=2&&item.DriverCacheReady&&driver!=null)
     {
      nativeDrivers.Add(driver);
      Journal.Write("Windows driver install fallback",item.Title+" / 0x"+unchecked((uint)item.HResult).ToString("X8"),false);
      continue;
     }
     var state=verified?"Installation verified":item.Code==2&&item.Reboot?"Restart required":"Installation could not be verified";
     if(!string.IsNullOrEmpty(item.Key))
     {
      if(verified)installedKeys.Add(item.Key);
      else downloadStatus[item.Key]=state;
     }
     results.Add(item.Title+" · "+Locale.Text(state)+(item.Code==2?"":$" (0x{unchecked((uint)item.HResult):X8})"));
     restart|=item.Reboot;
     attention|=!verified&&!item.Reboot;
    }
    if(restart)break;
   }

   foreach(var offer in restart?Array.Empty<WindowsOffer>():nativeDrivers.ToArray())
   {
    token.ThrowIfCancellationRequested();
    try
    {
     var driverResult=await SilentDriverUpdates.InstallAsync(offer,progress,token);
     restart=driverResult.Restart;
     attention|=!driverResult.Verified&&!restart;
     var state=driverResult.Verified?"Installation verified":restart?"Restart required":"Installation could not be verified";
     downloadStatus[offer.Key]=state;
     if(driverResult.Verified)installedKeys.Add(offer.Key);
     results.Add(offer.Title+" · "+Locale.Text(state)+"\n"+driverResult.Detail);
     if(restart)break;
    }
    catch(Exception ex) when(ex is not OperationCanceledException)
    {
     downloadStatus[offer.Key]="Installation could not be verified";
     results.Add(offer.Title+" · "+ex.Message);attention=true;
    }
   }

   if(!restart&&catalogDriverQueue.Count>0)
   {
    var catalogResult=await InstallCatalogQueueAsync(results,progress,token);
    restart=catalogResult.Restart;attention|=catalogResult.Attention;
   }
   foreach(var selected in restart?Array.Empty<DownloadRecipe>():recipes)
   {
    token.ThrowIfCancellationRequested();
    try
    {
     var app=applicationScan?.Applications.FirstOrDefault(a=>a.Id==selected.Id);
     var runtime=runtimeScan?.Packages.FirstOrDefault(p=>p.Id==selected.Id);
     var recipe=selected.Id switch
     {
      "app-chrome"=>AppDownloads.Recipes.Single(r=>r.Id=="prepare-chrome"),
      "directx"=>AppDownloads.Recipes.Single(r=>r.Id=="directx-offline"),
      _=>UpdatePackageInstaller.InstallRecipe(selected)
     };
     var definition=UpdatePackageInstaller.Definition(recipe,runtime)??UpdatePackageInstaller.Definition(selected,runtime);
     if(selected.Id=="directx")definition=UpdatePackageInstaller.Definition(recipe,null);
     var result=await ApplyUpdatePackageAsync(recipe,definition,app,runtime,operationId,progress,token);
     downloadStatus[selected.Id]=result.Status;
     results.Add(selected.Name+" · "+Locale.Text(result.Status));
     restart=result.Restart;
     attention|=!result.Verified&&!restart;
     if(result.Verified)
     {
      packageSelection.Remove(selected.Id);
      if(app!=null)ReplaceApplicationRelease(app);
      if(runtime!=null)ReplaceRuntimePackage(runtime);
     }
     if(restart)break;
    }
    catch(Exception ex) when(ex is not OperationCanceledException)
    {
     downloadStatus[selected.Id]="Installation could not be verified";
     results.Add(selected.Name+" · "+ex.Message);
     attention=true;
    }
   }
  }
  catch(OperationCanceledException)
  {
   results.Add(Locale.Text("Stopped between updates. Remaining selections are kept."));
   attention=true;
  }
  finally
  {
   if(File.Exists(stopFile))File.Delete(stopFile);
   updateCancellation=null;
   CancelOperation.Visibility=Visibility.Collapsed;
   windowsSelection.RemoveWhere(installedKeys.Contains);
   foreach(var key in installedKeys)downloadStatus.Remove(key);
   windowsScan?.Offers.RemoveAll(o=>installedKeys.Contains(o.Key));
   if(installedKeys.Count>0)hardwareInventoryReady=false;
   Navigate("drivers");
  }
  if(restart)results.Add(Locale.Text("Restart Windows and run the queue again for remaining updates."));
  var reportText=string.Join("\n",results);
  Journal.Write("Update installation",reportText,!attention&&!restart);
  operationCompletion=Locale.Text(restart?"Restart required":attention?"Review update results":"Update installation finished");
  updateInstallationReport=reportText;
  updateInstallationStatus=operationCompletion;
  Navigate("drivers");
 }

 private static async Task<(string Status,bool Verified,bool Restart)> ApplyUpdatePackageAsync(DownloadRecipe recipe,
  PreparationPackage? definition,ApplicationRelease? app,RuntimePackage? runtime,string operationId,IProgress<string> progress,CancellationToken token)
 {
  if(definition!=null&&UpdatePackageInstaller.Present(recipe,definition,app,runtime))return("Already up to date",true,false);
  var download=await UpdatePackageInstaller.AcquireAsync(recipe,progress,token);
  if(definition==null)return("Downloaded · manual installation required",false,false);
  if(UpdatePackageInstaller.AppIsRunning(definition)&&!await UpdatePackageInstaller.TryCloseForUpdateAsync(definition,token))
   return("Close the application, then run the queue again",false,false);
  token.ThrowIfCancellationRequested();
  var install=await PreparationPackages.InstallAsync(operationId,recipe,definition,download,progress);
  if(install.Code is 3010 or 1641)return("Restart required",false,true);
  if(install.Code==6&&recipe.Id=="app-obs")return("OBS files are in use. Close OBS, browsers and camera applications, then retry.",false,false);
  if(install.Code!=0)throw new IOException(Locale.Text("Installer exit code")+" "+install.Code);
  for(var attempt=0;attempt<5;attempt++)
  {
   if(UpdatePackageInstaller.Present(recipe,definition,app,runtime))return("Installation verified",true,false);
   await Task.Delay(1200);
  }
  return("Installation could not be verified",false,false);
 }

 private void ReplaceApplicationRelease(ApplicationRelease app)
 {
  var index=applicationScan!.Applications.IndexOf(app);
  var definition=UpdatePackageInstaller.Definition(app.Recipe,null)!;
  var versions=UpdatePackageInstaller.ApplicationVersions(definition);
  applicationScan.Applications[index]=app with{State=ApplicationCatalog.State(versions,app.Version),Installed=versions};
 }

 private void ReplaceRuntimePackage(RuntimePackage runtime)
 {
  var index=runtimeScan!.Packages.IndexOf(runtime);
  var versions=UpdatePackageInstaller.RuntimeVersions(runtime);
  runtimeScan.Packages[index]=runtime with{State=ApplicationCatalog.State(versions,runtime.Version),Installed=versions};
 }
}
