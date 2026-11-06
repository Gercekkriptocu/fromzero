using System.IO;
using System.Text.Json;
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
  var queueFile=LocalStore.FilePath("update-progress-"+operationId+".json");
  using var stopRegistration=token.Register(()=>{
   try{File.WriteAllText(stopFile,"");}catch(IOException){}catch(UnauthorizedAccessException){}
  });
  updateCancellation=cancellation;
  var queuedItems=offers.OrderBy(o=>o.Kind=="Driver").Select(o=>(o.Key,o.Title,0))
   .Concat(catalogDriverQueue.Select(item=>("catalog:"+item.Key,item.Value.Device.Name+" · "+item.Value.Match.Version,2)))
   .Concat(recipes.Select(recipe=>(recipe.Id,recipe.Name,3)));
  using var queue=new UpdateQueueView(UpdateQueueOverlay,queuedItems,()=>cancellation.Cancel());
  OperationPanel.Visibility=Visibility.Collapsed;
  IProgress<string> ForPackage(string key)=>new Progress<string>(message=>queue.Detail(key,message));
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
    queue.Stage(null,"Refreshing Windows Update offers…");
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
         queue.Replace(offer.Key,match.Key,match.Title);
        }
       }
       else
       {
        withdrawnKeys.Add(offer.Key);
        results.Add(offer.Title+" · "+Locale.Text("Windows no longer offers this update"));
        queue.Finish(offer.Key,"Windows no longer offers this update");
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
    queue.Finish(offer.Key,"Requires manual review");
    attention=true;
   }
   if(allowed.Any(o=>o.Kind=="Driver"))
   {
    queue.Stage(null,"Backing up drivers…");
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
     foreach(var offer in allowed.Where(o=>o.Kind=="Driver"))queue.Finish(offer.Key,"Skipped because driver backup failed");
     allowed=allowed.Where(o=>o.Kind!="Driver").ToArray();
     attention=true;
    }
   }
   foreach(var batch in allowed.Chunk(8))
   {
    token.ThrowIfCancellationRequested();
    EnsureUpdateSpace();
    queue.Stage(null,"Checking the selected Windows packages…");
    RemoveQueueFile(queueFile);
    var command=await RunWindowsQueueAsync(batch,stopFile,queueFile,queue);
    if(!command.Success){results.Add(command.Output);attention=true;break;}
    var report=JsonSerializer.Deserialize<List<PreparationWindowsResult>>(command.Output.Trim().TrimStart('\uFEFF'),LocalStore.Json)
     ??throw new InvalidDataException("Windows Update returned no installation report.");
    foreach(var item in report.Where(r=>r.Code!=5))
    {
     if(item.NoLongerOffered)
     {
      withdrawnKeys.Add(item.Key);
      results.Add(item.Title+" · "+Locale.Text("Windows no longer offers this update"));
      queue.Finish(item.Key,"Windows no longer offers this update");
      continue;
     }
     var verified=item.Code==2&&item.Installed;
     var driver=batch.FirstOrDefault(o=>o.Kind=="Driver"&&o.Key==item.Key);
     if(!verified&&!item.Reboot&&item.Code!=2&&item.DriverCacheReady&&driver!=null)
     {
      nativeDrivers.Add(driver);
      queue.Defer(driver.Key);
      Journal.Write("Windows driver install fallback",item.Title+" / 0x"+unchecked((uint)item.HResult).ToString("X8"),false);
      continue;
     }
     var state=WindowsQueueResultStatus(item);
     queue.Finish(item.Key,state,verified);
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
    token.ThrowIfCancellationRequested();
   }

   foreach(var offer in restart?Array.Empty<WindowsOffer>():nativeDrivers.ToArray())
   {
    token.ThrowIfCancellationRequested();
    EnsureUpdateSpace();
    try
    {
     queue.Stage(offer.Key,"Preparing the signed driver retry…");
     var driverResult=await SilentDriverUpdates.InstallAsync(offer,ForPackage(offer.Key),token);
     restart=driverResult.Restart;
     attention|=!driverResult.Verified&&!restart;
     var state=driverResult.Verified?"Installation verified":restart?"Restart required":"Installation could not be verified";
     downloadStatus[offer.Key]=state;
     queue.Finish(offer.Key,state,driverResult.Verified);
     if(driverResult.Verified)installedKeys.Add(offer.Key);
     results.Add(offer.Title+" · "+Locale.Text(state)+"\n"+driverResult.Detail);
     if(restart)break;
    }
    catch(Exception ex) when(ex is not OperationCanceledException)
    {
     downloadStatus[offer.Key]="Installation could not be verified";
     queue.Finish(offer.Key,"Installation could not be verified");
     results.Add(offer.Title+" · "+ex.Message);attention=true;
    }
   }

   if(!restart&&catalogDriverQueue.Count>0)
   {
    EnsureUpdateSpace();
    var catalogResult=await InstallCatalogQueueAsync(results,queue,token);
    restart=catalogResult.Restart;attention|=catalogResult.Attention;
   }
   foreach(var selected in restart?Array.Empty<DownloadRecipe>():recipes)
   {
    token.ThrowIfCancellationRequested();
    EnsureUpdateSpace();
    try
    {
     queue.Stage(selected.Id,"Preparing package…");
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
     var result=await ApplyUpdatePackageAsync(recipe,definition,app,runtime,ForPackage(selected.Id),token);
     downloadStatus[selected.Id]=result.Status;
     queue.Finish(selected.Id,result.Status,result.Verified);
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
     queue.Finish(selected.Id,"Installation could not be verified");
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
  catch(Exception ex)
  {
   results.Add(ex.Message);
   attention=true;
  }
  finally
  {
   RemoveQueueFile(stopFile);
   RemoveQueueFile(queueFile);
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
  var pending=queue.PendingTitles();
  if(pending.Length>0)
  {
   attention=true;
   results.Add(Locale.Text("Packages left in the queue")+"\n"+string.Join("\n",pending));
  }
  var reportText=string.Join("\n",results);
  Journal.Write("Update installation",reportText,!attention&&!restart);
  operationCompletion=Locale.Text(restart?"Restart required":attention?"Review update results":"Update installation finished");
  updateInstallationReport=reportText;
  updateInstallationStatus=operationCompletion;
  updateInstallationNeedsAttention=attention||restart||withdrawnKeys.Count>0;
  Navigate("drivers");
 }

 private static string WindowsQueueResultStatus(PreparationWindowsResult item)=>
  item.NoLongerOffered?"Windows no longer offers this update":
  item.Reboot?"Restart required":
  item.Code==2&&item.Installed?"Installation verified":"Installation could not be verified";

 private static async Task<CommandResult> RunWindowsQueueAsync(WindowsOffer[] batch,string stopFile,string queueFile,UpdateQueueView queue)
 {
  var task=Commands.ElevatedAsync(PreparationWindows.SelectionScript(batch,stopFile,queueFile));
  var known=batch.ToDictionary(item=>item.Key,StringComparer.OrdinalIgnoreCase);
  string? last=null;
  var reported=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  void ReadProgress()
  {
   try
   {
    if(!File.Exists(queueFile)||new FileInfo(queueFile).Length>1_000_000)return;
    using var stream=new FileStream(queueFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
    using var reader=new StreamReader(stream);
    var contents=reader.ReadToEnd();
    if(contents==last)return;
    var snapshot=JsonSerializer.Deserialize<WindowsQueueSnapshot>(contents,LocalStore.Json);
    if(snapshot?.Results==null)return;
    last=contents;
    foreach(var item in snapshot.Results)
    {
     if(!known.TryGetValue(item.Key,out var offer)||!reported.Add(offer.Key))continue;
     if(!item.Reboot&&item.Code!=2&&item.DriverCacheReady&&offer.Kind=="Driver")queue.Defer(offer.Key);
     else queue.Finish(offer.Key,WindowsQueueResultStatus(item),item.Code==2&&item.Installed);
    }
    var status=snapshot.Phase switch
    {
     "windows-check"=>"Checking the selected Windows packages…",
     "prepare"=>"Preparing package…",
     "download"=>"Downloading package…",
     "install"=>"Installing package…",
     "verify"=>"Verifying the installed update…",
     "between"=>"Waiting for the next package…",
     _=>null
    };
    if(status!=null)queue.Stage(known.TryGetValue(snapshot.Key,out var current)?current.Key:null,status);
   }
   catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException)
   {
    // The writer can be replacing a snapshot. Read the next complete snapshot on the next tick.
   }
  }
  while(!task.IsCompleted)
  {
   await Task.WhenAny(task,Task.Delay(450));
   ReadProgress();
  }
  return await task;
 }

 private static void RemoveQueueFile(string path)
 {
  try{File.Delete(path);}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Journal.Write("Update queue cleanup",ex.Message,false);}
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
  progress.Report("Verifying the installed update…");
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
