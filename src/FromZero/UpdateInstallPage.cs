using System.IO;
using System.Windows;

namespace FromZero;

public partial class MainWindow
{
 private async Task InstallSelectedUpdatesAsync()
 {
  var storage=UpdateStorage.Read();
  if(!storage.Ready)
  {
   operationCompletion=Locale.Text("Free up space to update");
   Journal.Write("Update installation",UpdateSpaceMessage(storage),false);
   Navigate("drivers");
   return;
  }
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
  var withdrawnKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  bool restart=false,attention=false;
  try
  {
   var nativeDrivers=new List<WindowsOffer>();
   var allowed=offers.Where(o=>o.QueueInstallable).OrderBy(o=>o.Kind=="Driver").ToArray();
   if(allowed.Length>0)
   {
    OperationTitle.Text=Locale.Text("Refreshing Windows Update offers…");
    try
    {
     var fresh=await WindowsUpdateCatalog.ScanAsync(false,token);
     if(fresh.Warnings.Count==0)
     {
      var refreshed=new List<WindowsOffer>();
      foreach(var offer in allowed)
      {
       var match=WindowsUpdateCatalog.RefreshSelection(offer,fresh.Offers);
       if(match!=null)
       {
        refreshed.Add(match);
        if(match.Key!=offer.Key)
        {
         withdrawnKeys.Add(offer.Key);
         windowsScan?.Offers.Add(match);
         windowsSelection.Add(match.Key);
        }
       }
       else
       {
        withdrawnKeys.Add(offer.Key);
        results.Add(offer.Title+" · "+Locale.Text("Windows no longer offers this update"));
       }
      }
      allowed=refreshed.DistinctBy(o=>o.Key).ToArray();
     }
    }
    catch(Exception ex) when(ex is not OperationCanceledException){Journal.Write("Refresh Windows Update offers",ex.Message,false);}
   }
   foreach(var offer in offers.Where(o=>!o.QueueInstallable))
   {
    results.Add(offer.Title+" · "+Locale.Text("Requires manual review"));
    attention=true;
   }
   if(allowed.Any(o=>o.Kind=="Driver"))
   {
    OperationTitle.Text=Locale.Text("Backing up drivers…");
    try
    {
     var liveDevices=await inventory.DevicesAsync();
     var liveDrivers=await inventory.DriversAsync();
     var selectedDrivers=allowed.Where(offer=>offer.Kind=="Driver").ToArray();
     var matching=liveDevices.Where(device=>selectedDrivers.Any(offer=>WindowsUpdateCatalog.MatchesDevice(offer,device)));
     await driverService.BackupDevicesAsync(Path.Combine(LocalStore.FilePath("Backups"),"Updates-"+operationId),matching,liveDrivers);
    }
    catch(Exception ex) when(ex is not OperationCanceledException&&!UpdateStorage.IsDiskFull(ex))
    {
     Journal.Write("Update driver backup",ex.Message,false);
     results.Add(Locale.Text("Driver backup failed. Driver installations were skipped."));
     allowed=allowed.Where(o=>o.Kind!="Driver").ToArray();
     attention=true;
    }
   }
   foreach(var batch in allowed.Chunk(40))
   {
    token.ThrowIfCancellationRequested();
    EnsureUpdateSpace();
    OperationTitle.Text=Locale.Text("Downloading and installing Windows updates and drivers…");
    var command=await Commands.ElevatedAsync(PreparationWindows.SelectionScript(batch,stopFile));
    if(!command.Success){results.Add(command.Output);attention=true;break;}
    var report=System.Text.Json.JsonSerializer.Deserialize<List<PreparationWindowsResult>>(command.Output.Trim().TrimStart('\uFEFF'),LocalStore.Json)
     ??throw new InvalidDataException("Windows Update returned no installation report.");
    foreach(var item in report.Where(r=>r.Code!=5))
    {
     if(item.NoLongerOffered)
     {
      withdrawnKeys.Add(item.Key);
      results.Add(item.Title+" · "+Locale.Text("Windows no longer offers this update"));
      continue;
     }
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
    EnsureUpdateSpace();
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
    EnsureUpdateSpace();
    var catalogResult=await InstallCatalogQueueAsync(results,progress,token);
    restart=catalogResult.Restart;attention|=catalogResult.Attention;
   }
   foreach(var selected in restart?Array.Empty<DownloadRecipe>():recipes)
   {
    token.ThrowIfCancellationRequested();
    EnsureUpdateSpace();
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
     var result=await ApplyUpdatePackageAsync(recipe,definition,app,runtime,progress,token);
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
    catch(Exception ex) when(ex is not OperationCanceledException&&!UpdateStorage.IsDiskFull(ex))
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
  catch(IOException ex) when(UpdateStorage.IsDiskFull(ex))
  {
   results.Add(UpdateSpaceMessage(UpdateStorage.Read()));
   attention=true;
  }
  finally
  {
   if(File.Exists(stopFile))File.Delete(stopFile);
   updateCancellation=null;
   CancelOperation.Visibility=Visibility.Collapsed;
   windowsSelection.RemoveWhere(installedKeys.Contains);
   windowsSelection.RemoveWhere(withdrawnKeys.Contains);
   foreach(var key in installedKeys)downloadStatus.Remove(key);
   foreach(var key in withdrawnKeys)downloadStatus.Remove(key);
   windowsScan?.Offers.RemoveAll(o=>installedKeys.Contains(o.Key)||withdrawnKeys.Contains(o.Key));
   if(installedKeys.Count>0)hardwareInventoryReady=false;
  }
  if(restart)results.Add(Locale.Text("Restart Windows and run the queue again for remaining updates."));
  var reportText=string.Join("\n",results);
  Journal.Write("Update installation",reportText,!attention&&!restart);
  operationCompletion=Locale.Text(restart?"Restart required":attention?"Review update results":"Update installation finished");
  updateInstallationReport=reportText;
  updateInstallationStatus=operationCompletion;
  updateInstallationNeedsAttention=attention||restart;
  Navigate("drivers");
 }

 private static string UpdateSpaceMessage(UpdateStorage storage)
 {
  var drive=storage.Drive.TrimEnd(Path.DirectorySeparatorChar);
  return storage.Ready
   ?string.Format(Locale.Culture,Locale.Text("An update ran out of disk space. {0} now has {1} free. Free up more space and retry."),drive,SizeLabel(storage.AvailableBytes))
   :string.Format(Locale.Culture,Locale.Text("{0} has {1} free. Free up at least {2} on this drive, then retry."),
    drive,SizeLabel(storage.AvailableBytes),SizeLabel(UpdateStorage.MinimumBytes-storage.AvailableBytes));
 }

 private static void EnsureUpdateSpace()
 {
  if(!UpdateStorage.Read().Ready)throw new IOException("System drive is low on space.",unchecked((int)0x80070070));
 }

 private static async Task<(string Status,bool Verified,bool Restart)> ApplyUpdatePackageAsync(DownloadRecipe recipe,
  PreparationPackage? definition,ApplicationRelease? app,RuntimePackage? runtime,IProgress<string> progress,CancellationToken token)
 {
  if(definition!=null&&UpdatePackageInstaller.Present(recipe,definition,app,runtime))return("Already up to date",true,false);
  var download=await UpdatePackageInstaller.AcquireAsync(recipe,progress,token);
  if(definition==null)return("Downloaded · manual installation required",false,false);
  if(UpdatePackageInstaller.AppIsRunning(definition)&&!await UpdatePackageInstaller.TryCloseForUpdateAsync(definition,token))
   return("Close the application, then run the queue again",false,false);
  token.ThrowIfCancellationRequested();
  var stageId=Guid.NewGuid().ToString("N");
  (int Code,string Path) install;
  var keepStage=false;
  try
  {
   install=await PreparationPackages.InstallAsync(stageId,recipe,definition,download,progress);
   keepStage=install.Code is 3010 or 1641;
  }
  finally
  {
   if(!keepStage)PreparationPackages.RemoveUpdateStage(stageId);
  }
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
