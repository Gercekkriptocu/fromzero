using System.Text;
using FromZero;

var root=Path.Combine(Path.GetTempPath(),"FromZero-Checks-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
AppContext.SetData("FromZero.TestData",Path.Combine(root,"AppData"));
var count=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++;}
void Reject(Action action,string name){try{action();}catch(Exception ex) when(ex is InvalidDataException or IOException){Check(true,name);return;}throw new Exception("FAIL (accepted): "+name);}
try{
 ApplicationChecks.Run(Check);
 CommunityChecks.Run(Check);
 await PreparationChecks.Run(root,Check);
 await PreparationExperienceChecks.Run(root,Check);
 StartupChecks.Unit(Check);
 PreflightChecks.Run(Check);
 await MemoryChecks.Run(Check);
 await DnsChecks.Run(Check);
 CrosshairChecks.Run(Check);
 WorkspaceUpgradeChecks.Run(Check);
 await ActivationChecks.Run(root,Check);
 DriverCatalogChecks.Run(Check);
 SilentUpdateChecks.Run(Check);
 MicrosoftDriverCatalogChecks.Run(root,Check);
 await RoadmapChecks.Run(root,Check);
 await FpsChecks.Run(root,Check);
 await GraphicsChecks.Run(root,Check);
 await UpdateChecks.Run(Check);
 await ReclaimChecks.Run(root,Check);
 Check(PackageSafety.QuotePs("it's $safe; `yes`")=="'it''s $safe; `yes`'","PowerShell literal escaping");
 Reject(()=>PackageSafety.ResolveInside(root,@"..\outside.txt"),"Capsule rejects parent traversal");
 Reject(()=>PackageSafety.ResolveInside(root,@"C:\outside.txt"),"Capsule rejects rooted path");
 Reject(()=>PackageSafety.ResolveInside(root,@"..\"+Path.GetFileName(root)+"-evil\\a"),"Capsule rejects sibling-prefix paths");
 var inf=Path.Combine(root,"test.inf");
 await File.WriteAllTextAsync(inf,"[Version]\r\nCatalogFile=test.cat\r\nCatalogFile.NTamd64=x64.cat\r\n[Models]\r\n%NIC%=Install, PCI\\VEN_8086&DEV_1234\r\n; USB\\VID_FAKE&PID_0000\r\n");
 Check(PackageSafety.ReadHardwareIds(inf).SetEquals([@"PCI\VEN_8086&DEV_1234"]),"INF extracts IDs and ignores comments");
 var arm=System.Runtime.InteropServices.RuntimeInformation.OSArchitecture==System.Runtime.InteropServices.Architecture.Arm64;
 Check(PackageSafety.ReadCatalog(inf)==Path.Combine(root,arm?"test.cat":"x64.cat"),"INF chooses architecture catalog");
 await File.WriteAllTextAsync(inf,"[Version]\r\nCatalogFile=..\\evil.cat\r\n");Reject(()=>PackageSafety.ReadCatalog(inf),"INF rejects external catalog");
 await File.WriteAllTextAsync(inf,"[Version]\nCatalogFile=test.cat\n[M]\n%NIC%=I,USB\\VID_1234&PID_5678",Encoding.Unicode);
 Check(PackageSafety.ReadHardwareIds(inf).Contains(@"USB\VID_1234&PID_5678"),"UTF-16 INF support");
 var entry=await PackageSafety.HashFileAsync(root,inf);var manifest=new CapsuleManifest(1,DateTimeOffset.Now,"test","X64","test",[entry],[]);
 Check((await PackageSafety.VerifyAsync(root,manifest)).Count==0,"Capsule verifies exact file bytes");
 await File.AppendAllTextAsync(inf,"tamper");Check((await PackageSafety.VerifyAsync(root,manifest)).Count==1,"Capsule detects changed content");
 File.Delete(inf);Check((await PackageSafety.VerifyAsync(root,manifest)).Count==1,"Capsule detects missing file");
 var duplicate=manifest with{Files=[entry,entry]};bool duplicateRejected=false;try{await PackageSafety.VerifyAsync(root,duplicate);}catch(InvalidDataException){duplicateRejected=true;}Check(duplicateRejected,"Capsule rejects duplicate manifest entries");
 var old=Path.Combine(root,"old.tmp");await File.WriteAllTextAsync(old,"old");File.SetCreationTimeUtc(old,DateTime.UtcNow.AddDays(-10));File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-10));
 var fresh=Path.Combine(root,"fresh.tmp");await File.WriteAllTextAsync(fresh,"fresh");
 var plan=CleanupPlan.Scan(root,DateTime.UtcNow.AddDays(-7));Check(plan.Count==1&&plan[0].Path==old,"Cleanup selects only old files");
 await File.AppendAllTextAsync(old,"changed");var changed=CleanupPlan.Execute(root,plan,DateTime.UtcNow.AddDays(-7));Check(changed.Count==0&&File.Exists(old),"Cleanup revalidates file before deletion");
 var outside=Path.Combine(Path.GetDirectoryName(root)!,"FromZero-outside-"+Guid.NewGuid()+".tmp");await File.WriteAllTextAsync(outside,"keep");
 try{var info=new FileInfo(outside);var forged=CleanupPlan.Execute(root,[new(outside,info.Length,info.LastWriteTimeUtc)],DateTime.UtcNow.AddDays(-7));Check(forged.Count==0&&File.Exists(outside),"Cleanup rejects forged outside path");}finally{File.Delete(outside);}
 File.SetCreationTimeUtc(old,DateTime.UtcNow.AddDays(-10));File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-10));var clean=CleanupPlan.Execute(root,CleanupPlan.Scan(root,DateTime.UtcNow.AddDays(-7)),DateTime.UtcNow.AddDays(-7));Check(clean.Count==1&&!File.Exists(old)&&File.Exists(fresh),"Cleanup preserves recent files");
 Check(FrameAnalysis.ParseCsvLine("\"game,name.exe\",\"escaped \"\"quote\"\"\",10").SequenceEqual(["game,name.exe","escaped \"quote\"","10"]),"CSV quoted fields");
 string Csv(double time,string app="game.exe")=>"Application,ProcessID,SwapChainAddress,MsBetweenPresents\n"+string.Join("\n",Enumerable.Repeat($"{app},12,0x1,{time.ToString(System.Globalization.CultureInfo.InvariantCulture)}",100));
 var frames=FrameAnalysis.Read(new StringReader(Csv(10)))[0];Check(Math.Abs(frames.AverageFps-100)<.001&&Math.Abs(frames.LowOnePercent-100)<.001,"FPS arithmetic at 10 ms");
 var mixed=Csv(10)+"\nother.exe,18,0x2,20\n"+string.Join("\n",Enumerable.Repeat("other.exe,18,0x2,20",99));Check(FrameAnalysis.Read(new StringReader(mixed)).Count==2,"FPS separates process/swapchain streams");
 var spike=Csv(10)+"\ngame.exe,12,0x1,1000";var stutter=FrameAnalysis.Read(new StringReader(spike))[0];Check(stutter.LowOnePercent<10&&stutter.AverageFps<100,"FPS low-percent metric includes stutter");
 var invalid=Csv(10)+"\ngame.exe,12,0x1,NaN\ngame.exe,12,0x1,0\ngame.exe,12,0x1,-1";Check(FrameAnalysis.Read(new StringReader(invalid))[0].Frames==100,"FPS excludes invalid intervals");
 Check(GameService.SplitArguments("-windowed +fps_max 144 -name \"my player\"").SequenceEqual(["-windowed","+fps_max","144","-name","my player"]),"Game argument tokenization");
 var percentileCsv="Application,ProcessID,SwapChainAddress,FrameTime\n"+string.Join("\n",Enumerable.Range(1,100).Select(i=>$"game,1,0x1,{i}"));
 Check(FrameAnalysis.Read(new StringReader(percentileCsv))[0].P99Milliseconds==99,"P99 uses nearest-rank percentile");
 Reject(()=>GameService.SplitArguments("-name \"unfinished"),"Game rejects unclosed argument quote");
 Reject(()=>GameService.SplitArguments("x\nnew-command"),"Game rejects multiline arguments");
 var fake=new FakeGamePlatform();var state=new MemoryGameState();var game=new GameService(fake,state);
 await game.BeginAsync("test","",true,true,true);
 Check(fake.Plan==FakeGamePlatform.High&&fake.Values.Values.SequenceEqual([1,0,0])&&state.Current!=null,"Game applies plan only after saving recovery state");
 await game.RestoreAsync();Check(fake.Plan==FakeGamePlatform.Balanced&&fake.Values.Values.SequenceEqual([0,1,1])&&state.Current==null,"Game restores original power and registry values");
 await game.BeginAsync("test","",true,true,false);fake.Values[@"Software\Microsoft\GameBar|AutoGameModeEnabled"]=7;fake.Plan=FakeGamePlatform.Other;
 await game.RestoreAsync();Check(fake.Plan==FakeGamePlatform.Other&&fake.Values[@"Software\Microsoft\GameBar|AutoGameModeEnabled"]==7,"Game preserves settings changed outside its session");
 fake=new FakeGamePlatform();state=new MemoryGameState();game=new GameService(fake,state);await game.BeginAsync("crash","",true,true,true);var recovered=new GameService(fake,state);await recovered.RestoreAsync();Check(fake.Plan==FakeGamePlatform.Balanced&&fake.Values.Values.SequenceEqual([0,1,1]),"Game can recover persisted interrupted session");
 fake=new FakeGamePlatform{FailWriteName="AppCaptureEnabled"};state=new MemoryGameState();game=new GameService(fake,state);bool failed=false;try{await game.BeginAsync("failure","",true,true,true);}catch(IOException){failed=true;}Check(failed&&fake.Plan==FakeGamePlatform.Balanced&&fake.Values.Values.SequenceEqual([0,1,1])&&game.Session==null,"Game rolls back partial application failure");
 fake=new FakeGamePlatform{Supported=false};state=new MemoryGameState();game=new GameService(fake,state);failed=false;try{await game.BeginAsync("unsupported","",true,false,false);}catch(InvalidOperationException){failed=true;}Check(failed&&state.Current==null&&fake.Plan==FakeGamePlatform.Balanced,"Game rejects unavailable power plan without mutation");
 fake=new FakeGamePlatform();fake.Values.Remove(@"Software\Microsoft\GameBar|AutoGameModeEnabled");state=new MemoryGameState();game=new GameService(fake,state);failed=false;try{await game.BeginAsync("unknown","",false,true,false);}catch(InvalidOperationException){failed=true;}Check(failed&&state.Current==null,"Game rejects missing registry preferences without creating values");
 fake=new FakeGamePlatform();state=new MemoryGameState();game=new GameService(fake,state);await game.BeginAsync("restore-failure","",true,false,false);fake.FailRestore=true;await game.RestoreAsync();Check(game.Session!=null&&state.Current!=null,"Game retains recovery state after failed restoration");fake.FailRestore=false;await game.RestoreAsync();Check(game.Session==null&&fake.Plan==FakeGamePlatform.Balanced,"Game retries failed restoration successfully");
 Check(Guid.TryParse(GameService.ActivePlan(),out _),"Read-only native power plan query");
 var devices=await new InventoryService().DevicesAsync();Check(devices.Count>0&&devices.All(d=>d.HardwareIds!=null),"Windows device inventory returns valid JSON");
 var drivers=await new InventoryService().DriversAsync();Check(drivers.Count>0,"Windows driver inventory returns valid JSON");
 var metrics=new LiveMetrics().Read();Check(metrics.TotalGb>0&&metrics.RamPercent is >=0 and <=100,"Native memory metrics");
 var adapterId=Guid.NewGuid().ToString();
 var adapter=new NetworkAdapterState(adapterId,"Wi-Fi","test",true,true,true,true,["169.254.1.2"],[],[]);
 var health=new NetworkHealth(DateTimeOffset.Now,[adapter],false,false,false,[]);
 Check(NetworkRepair.Plan(health,adapter).Select(s=>s.Id).SequenceEqual(["wlan","renew"]),"Network repair plan restores WLAN and DHCP before considering DNS");
 Check(NetworkRepair.Plan(health with{WlanRunning=true,InternetWorks=true},adapter with{Addresses=["192.0.2.5"]}).Select(s=>s.Id).SequenceEqual(["dns"]),"DNS maintenance is suggested only with a working direct route and failed system DNS");
 var staticAdapter=adapter with{Dhcp=false};Check(!NetworkRepair.Plan(health,staticAdapter).Any(s=>s.Id=="renew"),"Network repair preserves static IP configuration");
 var healthy=health with{WlanRunning=true,DnsWorks=true,InternetWorks=true};Check(NetworkRepair.Plan(healthy,adapter with{Addresses=["192.168.1.5"]}).Count==0,"Healthy connection gets no unnecessary repair actions");
 Reject(()=>NetworkRepair.Script("restart","'; Remove-Item"),"Network actions reject untrusted adapter identity");
 Reject(()=>NetworkRepair.Script("arbitrary-command",adapterId),"Network actions are restricted to implemented operations");
 Check(NetworkRepair.Script("renew",adapterId).Contains("$ip.Dhcp -ne 'Enabled'"),"DHCP repair rechecks DHCP before applying");
 Check(TcpOptimizer.Normalize("Normal")=="normal","TCP optimizer accepts Windows supported level");Reject(()=>TcpOptimizer.Normalize("normal; evil"),"TCP optimizer rejects arbitrary command text");
 Check(!NetworkRepair.PhysicalHardware(@"ROOT\VPN\0000")&&NetworkRepair.PhysicalHardware(@"PCI\VEN_10EC"),"VPN virtual adapter is excluded from physical repair targets");
 var tcpLevel=await TcpOptimizer.ReadAsync();Check(tcpLevel.Length>0,"Real Windows TCP optimization level can be read without changes");
 var animations=new WindowsPerformanceBackend().Animations();Console.WriteLine("Windows animation preference read: "+animations+" (unchanged)");
 var perfBackend=new FakePerformanceBackend();var perf=new PerformanceProfile(perfBackend,Path.Combine(root,"perf.json"));
 await perf.ApplyAsync(new(true,true,true,true));Check(perfBackend.CurrentPower==PerformanceProfile.HighPower&&!perfBackend.Animated&&perfBackend.NvidiaPower==1&&perfBackend.NvidiaQuality==20&&perf.Backup!=null,"Performance profile applies selected Windows and NVIDIA settings");
 await perf.RestoreAsync();Check(perfBackend.CurrentPower==FakeGamePlatform.Balanced&&perfBackend.Animated&&perfBackend.NvidiaPower==5&&perfBackend.NvidiaQuality==0&&perf.Backup==null,"Performance profile restores prior settings");
 perfBackend=new FakePerformanceBackend{FailApply=true};perf=new PerformanceProfile(perfBackend,Path.Combine(root,"perf-fail.json"));bool profileFailed=false;try{await perf.ApplyAsync(new(true,true,true,true));}catch(IOException){profileFailed=true;}Check(profileFailed&&perfBackend.CurrentPower==FakeGamePlatform.Balanced&&perfBackend.Animated&&perf.Backup==null,"Performance partial failure restores Windows settings");
 var componentsScan=GameComponents.Scan(InstalledSoftware.ReadDesktopApps());Check(componentsScan.Components.Count>=35&&componentsScan.Components.Any(c=>c.Id=="d3d12-x64"&&c.Present),"Game components detect Windows Direct3D and legacy/runtime architectures");
 var componentDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","artifacts"));Directory.CreateDirectory(componentDir);await File.WriteAllTextAsync(Path.Combine(componentDir,"component-verification.json"),System.Text.Json.JsonSerializer.Serialize(componentsScan,LocalStore.Json));
 if(File.Exists(Path.Combine(Environment.SystemDirectory,"nvapi64.dll"))){var nv=await NvidiaSettings.InspectAsync();Check(nv.Count==4&&nv.All(v=>NvidiaSettings.Valid(v.Id,v.Value)),"Real four NVIDIA DRS settings read without modifications");Console.WriteLine("NVIDIA READ: "+string.Join("; ",nv.Select(v=>v.Name+"="+v.Value)));}
 var installed=await InstalledSoftware.ReadAsync();Check(installed.Apps.Count>0,"Real desktop and Store application inventory");
 Check(new UpdateInfo("",1,"HP Firmware Update","HP","System Firmware").IsFirmware,"Firmware updates are identified for manufacturer-only handling");
 var firmwareRejected=false;try{await new DriverService().InstallUpdateAsync(new UpdateInfo(Guid.NewGuid().ToString(),1,"BIOS Update","OEM","Firmware"));}catch(InvalidOperationException){firmwareRejected=true;}Check(firmwareRejected,"Firmware installation is refused before backup or elevation");
 var gpuStates=GpuSoftware.Inspect(devices,drivers,installed.Apps);
 foreach(var gpuState in gpuStates.Where(s=>s.ExecutablePresent))Check(Authenticode.Verify(gpuState.Executable).Contains(gpuState.Tool.Publisher,StringComparison.OrdinalIgnoreCase),"Installed GPU app trust and publisher: "+gpuState.Tool.Name);
 Check(gpuStates.Any(s=>s.HardwarePresent),"GPU identified by PCI vendor hardware ID");
 Check(gpuStates.Where(s=>s.HardwarePresent).All(s=>s.Drivers.Count>0),"Detected GPU linked to real active driver record");
 Check(!GpuSoftware.Inspect([new("Removed NVIDIA",@"PCI\VEN_10DE&DEV_1234","Display",45,[@"PCI\VEN_10DE&DEV_1234"],false)],[],[]).Any(s=>s.HardwarePresent),"Removed graphics device is not reported as connected");
 Check(drivers.Any(GpuSoftware.InfPresent),"Active driver INF file exists in Windows INF folder");
 Check(GpuSoftware.Vendor(new("NVIDIA label",@"PCI\VEN_1002&DEV_1234","Display",0,[@"PCI\VEN_1002&DEV_1234"]))=="AMD","GPU identity does not trust device display name");
 Check(GpuSoftware.Vendor(new("Intel NIC",@"PCI\VEN_8086&DEV_1234","Net",0,[@"PCI\VEN_8086&DEV_1234"]))=="","Intel network adapter is not classified as an Intel GPU");
 Check(InstalledSoftware.FindCatalogApps([new("Steam game","1","","","","test"),new("Steam","2","Valve","","","test")],"Steam").Count==1,"Steam games are not mistaken for installed Steam client");
 Check(AppDownloads.Allowed(new Uri("https://us.download.nvidia.com/app.exe"),["nvidia.com"]),"Download accepts official subdomain");
 Check(!AppDownloads.Allowed(new Uri("https://nvidia.com.evil.test/app.exe"),["nvidia.com"]),"Download rejects lookalike host suffix");
 Check(!AppDownloads.Allowed(new Uri("http://nvidia.com/app.exe"),["nvidia.com"]),"Download rejects HTTP downgrade");
 Check(!AppDownloads.Allowed(new Uri("https://nvidia.com:8443/app.exe"),["nvidia.com"]),"Download rejects nonstandard ports");
 Check(!AppDownloads.Allowed(new Uri("https://name:secret@nvidia.com/app.exe"),["nvidia.com"]),"Download rejects embedded credentials");
 var nvRecipe=AppDownloads.Recipes.Single(r=>r.Id=="nvidia");
 Check(AppDownloads.ExtractDownload("<a href='https://us.download.nvidia.com/nvapp/client/11.0.9.565/NVIDIA_app_v11.0.9.565.exe'>download</a>",nvRecipe).Host=="us.download.nvidia.com","NVIDIA download resolved from official page");
 var nvArm="https://us.download.nvidia.com/nvapp/client/11.0.9.585/arm64/NVIDIA_app_v11.0.9.585_arm64.exe";
 var nvX64="https://us.download.nvidia.com/nvapp/client/11.0.9.585/x64/NVIDIA_app_v11.0.9.585_x64.exe";
 Check(AppDownloads.ExtractDownload($"<a href='{nvArm}'>ARM</a><a href='{nvX64}'>x64</a>",nvRecipe).AbsoluteUri==nvX64,
  "NVIDIA's architecture-specific links resolve to the x64 installer when ARM64 appears first");
 Reject(()=>AppDownloads.ExtractDownload("<a href='https://evil.test/NVIDIA.exe'>bad</a>",nvRecipe),"Missing vendor download fails explicitly");
 var amdRecipe=AppDownloads.Recipes.Single(r=>r.Id=="amd");
 Check(AppDownloads.ExtractDownload("https://drivers.amd.com/drivers/installer/26.20/whql/amd-software-adrenalin-edition-26.10.1-minimalsetup-261018_web.exe",amdRecipe).Host=="drivers.amd.com","AMD minimal setup resolved from official page");
 Reject(()=>Authenticode.Verify(Environment.ProcessPath!),"Unsigned executable is refused by native Windows trust check");
 var catalogRoot=Path.Combine(Environment.SystemDirectory,"DriverStore","FileRepository");
 var catalogOptions=new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint};
 var realCatalog=Directory.EnumerateFiles(catalogRoot,"*.cat",catalogOptions).FirstOrDefault();
 Check(realCatalog!=null&&!string.IsNullOrWhiteSpace(Authenticode.Verify(realCatalog)),"Real Windows driver catalog trust and publisher verified");
 var liveReport=new{
  Checked=DateTimeOffset.Now,DeviceCount=devices.Count,DriverCount=drivers.Count,ApplicationCount=installed.Apps.Count,
  Gpu=gpuStates.Select(s=>new{s.Tool.Name,s.Tool.Vendor,s.HardwarePresent,Devices=s.Devices.Select(d=>d.Name),Drivers=s.Drivers.Select(d=>new{d.Name,d.Version,d.Provider,d.IsSigned,InfExists=GpuSoftware.InfPresent(d)}),InstalledAppRecords=s.Apps.Count,s.ExecutablePresent,s.FileVersion}),
  Catalog=AppDownloads.Recipes.Where(r=>!r.DriverTool).Select(r=>new{r.Name,InstalledRecords=InstalledSoftware.FindCatalogApps(installed.Apps,r.Name).Count})
 };
 var artifactsPath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","artifacts"));Directory.CreateDirectory(artifactsPath);
 var startupArgument=Array.IndexOf(args,"--verify-startup");if(startupArgument>=0){if(startupArgument+1>=args.Length)throw new ArgumentException("Startup executable path required");await StartupChecks.RunProcesses(args[startupArgument+1],artifactsPath,Check);}
 var releaseArgument=Array.IndexOf(args,"--verify-release-integrity");if(releaseArgument>=0){if(releaseArgument+1>=args.Length)throw new ArgumentException("Release folder required");var release=await ReleaseIntegrity.InspectAsync(Path.GetFullPath(args[releaseArgument+1]),new(0,20,1));Check(release.IntegrityOk&&release.Files>=4&&release.Version=="0.20.1","Packaged release manifest verifies the published files");}
 await File.WriteAllTextAsync(Path.Combine(artifactsPath,"inventory-verification.json"),System.Text.Json.JsonSerializer.Serialize(liveReport,LocalStore.Json));
 if(args.Contains("--verify-faceit-public")){var ranking=await FaceitCatalog.RefreshAsync();await File.WriteAllTextAsync(Path.Combine(artifactsPath,"faceit-public-0.15-verification.json"),System.Text.Json.JsonSerializer.Serialize(ranking,LocalStore.Json));Check(ranking.Entries.Count==10&&ranking.Entries.All(e=>e.Source==FaceitCatalog.PublicGlobalUrl),"Live public HTML FACEIT ranking refresh succeeds without API credentials");}
 if(args.Contains("--verify-updates")){
  var available=await new DriverService().ScanUpdatesAsync();
  Check(available.All(u=>Guid.TryParse(u.Id,out _)&&u.Revision>=0),"Windows Update driver source queried successfully");
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"driver-source-verification.json"),System.Text.Json.JsonSerializer.Serialize(new{Checked=DateTimeOffset.Now,Source="Windows Update Agent",AvailableCount=available.Count,Updates=available.Select(u=>new{u.Title,u.Manufacturer,u.Model})},LocalStore.Json));
  Console.WriteLine("Windows Update currently offers "+available.Count+" driver packages. Nothing installed.");
 }
 if(args.Contains("--verify-update-catalogs")){
  var catalog=await RuntimeCatalog.ScanAsync(true,new Progress<string>(s=>Console.WriteLine(s)));Check(catalog.Packages.Count>=18&&catalog.Channels>=3,"Live official runtime feeds provide architecture-specific release lines");
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"runtime-source-verification.json"),System.Text.Json.JsonSerializer.Serialize(catalog,LocalStore.Json));
  var windowsCatalog=await WindowsUpdateCatalog.ScanAsync(true);Check(windowsCatalog.Offers.All(o=>Guid.TryParse(o.Id,out _)&&o.Revision>=0),"Live expanded Windows Update source enumerates offered packages");
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"windows-catalog-verification.json"),System.Text.Json.JsonSerializer.Serialize(windowsCatalog,LocalStore.Json));
 }
 if(args.Contains("--verify-catalog-300")){
  var catalog=await ApplicationCatalog.ScanAsync(installed.Apps,new Progress<string>(s=>Console.WriteLine(s)));await File.WriteAllTextAsync(Path.Combine(artifactsPath,"catalog-300-verification.json"),System.Text.Json.JsonSerializer.Serialize(catalog,LocalStore.Json));Console.WriteLine($"CATALOG: {catalog.Applications.Count} tracks / {catalog.Warnings.Count} warnings");foreach(var warning in catalog.Warnings)Console.WriteLine(warning);Check(catalog.Applications.Count==300,"Live broad scan returns three hundred distinct application tracks");
  var crosshair=await CrosshairCatalog.RefreshAsync("xantares");Check(CrosshairCode.Decode(crosshair.Code)==crosshair.Settings,"Live crosshair refresh returns the same preview and import parameters");
  var ranks=await FaceitCatalog.RefreshAsync();await File.WriteAllTextAsync(Path.Combine(artifactsPath,"faceit-5-region-verification.json"),System.Text.Json.JsonSerializer.Serialize(ranks,LocalStore.Json));Check(ranks.Entries.Count==10,"Live five-region FACEIT rankings merge successfully");
  var report=new List<object>();foreach(var id in new[]{"app-community-anki","app-community-alacritty","app-community-geany"}){try{var recipe=AppDownloads.Recipes.Single(r=>r.Id==id);var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());var record=AppDownloads.Cached(recipe)!;await AppDownloads.VerifyCachedAsync(recipe,record);report.Add(new{Status="PASS",Record=record});Console.WriteLine("COMMUNITY DOWNLOAD PASS: "+recipe.Name);}catch(Exception ex){report.Add(new{Status="FAIL",Id=id,Error=ex.Message});Console.WriteLine("COMMUNITY DOWNLOAD FAIL: "+id+" "+ex.Message);}}await File.WriteAllTextAsync(Path.Combine(artifactsPath,"community-download-verification.json"),System.Text.Json.JsonSerializer.Serialize(report,LocalStore.Json));
 }
 if(args.Contains("--verify-catalog-600")){
  var catalog=await ApplicationCatalog.ScanAsync(installed.Apps,new Progress<string>(s=>Console.WriteLine(s)));await File.WriteAllTextAsync(Path.Combine(artifactsPath,"catalog-600-verification.json"),System.Text.Json.JsonSerializer.Serialize(catalog,LocalStore.Json));Console.WriteLine($"CATALOG: {catalog.Applications.Count} tracks / {catalog.Warnings.Count} warnings");foreach(var warning in catalog.Warnings)Console.WriteLine(warning);Check(catalog.Applications.Count==600,"Live broad scan returns six hundred distinct application tracks");
  var crosshair=await CrosshairCatalog.RefreshAsync("xantares");Check(CrosshairCode.Decode(crosshair.Code)==crosshair.Settings,"Live crosshair refresh returns the same preview and import parameters");
  var ranks=await FaceitCatalog.RefreshAsync();await File.WriteAllTextAsync(Path.Combine(artifactsPath,"faceit-5-region-verification.json"),System.Text.Json.JsonSerializer.Serialize(ranks,LocalStore.Json));Check(ranks.Entries.Count==10,"Live five-region FACEIT rankings merge successfully");
 }
 if(args.Contains("--verify-preparation-experience"))await PreparationLiveChecks.Run(artifactsPath,Check,args.Contains("--download-preparation-experience"));
 if(args.Contains("--verify-preparation-packages")){
  var report=new List<object>();var passed=true;foreach(var id in new[]{"prepare-chrome","prepare-vlc","prepare-vscode"}){
   try{var recipe=await PreparationPackages.ResolveAsync(id);var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());var record=AppDownloads.Cached(recipe)!;await AppDownloads.VerifyCachedAsync(recipe,record);Check(File.Exists(result.Path),"Real signed preparation download without installation: "+id);report.Add(new{Status="PASS",Record=record});}
   catch(Exception ex){passed=false;report.Add(new{Status="FAIL",Id=id,Error=ex.Message});Console.WriteLine("PREPARATION DOWNLOAD FAIL: "+id+" / "+ex.Message);}
  }await File.WriteAllTextAsync(Path.Combine(artifactsPath,"preparation-package-verification.json"),System.Text.Json.JsonSerializer.Serialize(report,LocalStore.Json));Check(passed,"All preparation package recipes resolve, download and verify without running installers");
  foreach(var id in new[]{"firefox","prepare-chrome","prepare-vscode"}){var version=await PreparationPackages.TargetVersionAsync(id,default);Check(version!=null,"Live preparation target version from producer: "+id+" "+version);}
  var dependency=await PreparationPackages.ResolveAsync("dotnet-desktop-10-0-win-x64");Check(dependency.ExpectedSha512.Length==128&&dependency.Publisher=="Microsoft Corporation","Dell .NET Desktop 10 dependency resolves from signed Microsoft release metadata with SHA-512");
 }
 if(args.Contains("--verify-expanded-oem-tools")){
  var report=new List<object>();var passed=true;foreach(var recipe in AppDownloads.Recipes.Where(r=>r.Id is "logi-options" or "logi-ghub" or "corsair-icue" or "steelseries-gg" or "samsung-magician")){
   try{var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());var record=AppDownloads.Cached(recipe)!;await AppDownloads.VerifyCachedAsync(recipe,record);Check(File.Exists(result.Path),"Real signed expanded manufacturer tool: "+recipe.Name);report.Add(new{Status="PASS",Record=record});}
   catch(Exception ex){passed=false;Console.WriteLine("OEM DOWNLOAD FAIL: "+recipe.Name+" / "+ex.Message);report.Add(new{Status="FAIL",Name=recipe.Name,Error=ex.Message});}
  }await File.WriteAllTextAsync(Path.Combine(artifactsPath,"expanded-oem-tool-verification.json"),System.Text.Json.JsonSerializer.Serialize(report,LocalStore.Json));Check(passed,"All five new manufacturer tools downloaded and verified without running installers");
 }
 if(args.Contains("--verify-oem-tools")){
  var report=new List<object>();var passed=true;foreach(var recipe in AppDownloads.Recipes.Where(r=>r.Id is "dell-command" or "hp-image-assistant" or "lenovo-system-update")){
   try{var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());var record=AppDownloads.Cached(recipe)!;await AppDownloads.VerifyCachedAsync(recipe,record);Check(File.Exists(result.Path),"Real signed manufacturer driver tool: "+recipe.Name);report.Add(new{Status="PASS",Record=record});}
   catch(Exception ex){passed=false;Console.WriteLine("OEM DOWNLOAD FAIL: "+recipe.Name+" / "+ex.Message);report.Add(new{Status="FAIL",Name=recipe.Name,Error=ex.Message});}
  }await File.WriteAllTextAsync(Path.Combine(artifactsPath,"oem-tool-verification.json"),System.Text.Json.JsonSerializer.Serialize(report,LocalStore.Json));Check(passed,"All three new manufacturer tools downloaded and verified without running installers");
 }
 if(args.Contains("--verify-faceit-crosshairs")){
  var report=new List<FaceitCrosshair>();foreach(var account in FaceitCrosshairs.Read()){
   var current=await FaceitCrosshairs.RefreshAsync(account.Nickname);Check(CrosshairCode.Encode(CrosshairCode.Decode(current.Code))==current.Code,"Live FACEIT crosshair code and preview: "+current.Nickname);report.Add(current);
  }
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"faceit-crosshair-verification.json"),System.Text.Json.JsonSerializer.Serialize(report,LocalStore.Json));
 }
 if(args.Contains("--verify-live-apps")||args.Contains("--verify-expanded-apps")){
  if(args.Contains("--verify-expanded-apps")){var previous=Path.Combine(artifactsPath,"verified-feed-cache");if(Directory.Exists(previous)){var cache=LocalStore.FilePath("FeedCache");Directory.CreateDirectory(cache);foreach(var file in Directory.EnumerateFiles(previous,"*.json"))File.Copy(file,Path.Combine(cache,Path.GetFileName(file)),true);}}
  var appCatalog=await ApplicationCatalog.ScanAsync(installed.Apps,new Progress<string>(s=>Console.WriteLine(s)));Check(args.Contains("--verify-expanded-apps")?appCatalog.Applications.Count==ApplicationSources.Count&&appCatalog.Warnings.Count==0:appCatalog.Applications.Count>=8,"Live application manufacturer feeds return current download recipes");
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"application-source-verification.json"),System.Text.Json.JsonSerializer.Serialize(appCatalog,LocalStore.Json));
  Console.WriteLine("APP FEEDS: "+appCatalog.Applications.Count+" applications / "+appCatalog.Warnings.Count+" warnings.");foreach(var warning in appCatalog.Warnings)Console.WriteLine(warning);
  var expandedRuntimes=await RuntimeCatalog.ScanAsync(true);Check(expandedRuntimes.Packages.Any(p=>p.Name==".NET SDK"),"Live runtime catalog now includes architecture-specific SDK packages");
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"runtime-source-verification.json"),System.Text.Json.JsonSerializer.Serialize(expandedRuntimes,LocalStore.Json));
  var report=new List<object>();
  var verifyIds=args.Contains("--verify-expanded-apps")?ApplicationSources.Github.Skip(5).Select(s=>"app-"+s.Id).Concat(new[]{"app-thunderbird","app-node-lts","app-node-current","app-vivaldi"}).ToArray():new[]{"app-7zip","app-chrome","app-vlc","xna","dotnet-sdk-10-0-win-x64"};
  var requestedIds=args.FirstOrDefault(a=>a.StartsWith("--download-ids="));if(requestedIds!=null)verifyIds=requestedIds[15..].Split(',');var allVerified=true;
  foreach(var recipe in AppDownloads.Recipes.Where(r=>verifyIds.Contains(r.Id))){
   Console.WriteLine("DOWNLOAD: "+recipe.Name);try{var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());var cached=AppDownloads.Cached(recipe)!;await AppDownloads.VerifyCachedAsync(recipe,cached);Check(File.Exists(result.Path),"Real installer integrity verification: "+recipe.Name);report.Add(new{Status="PASS",Record=cached});}catch(Exception ex){allVerified=false;Console.WriteLine("DOWNLOAD FAIL: "+ex.Message);report.Add(new{Status="FAIL",Name=recipe.Name,Error=ex.Message});}
  }
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,requestedIds!=null?"application-retry-download-verification.json":args.Contains("--verify-expanded-apps")?"application-expanded-download-verification.json":"application-download-verification.json"),System.Text.Json.JsonSerializer.Serialize(report,LocalStore.Json));
  if(args.Contains("--verify-expanded-apps")){var cacheRoot=LocalStore.FilePath("FeedCache");var destination=Path.Combine(artifactsPath,"verified-feed-cache");Directory.CreateDirectory(destination);foreach(var file in Directory.EnumerateFiles(cacheRoot,"*.json"))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)),true);}
  Check(allVerified&&report.Count==verifyIds.Length,"All requested real installer downloads passed integrity validation");
 }
 if(args.Contains("--verify-expanded-downloads")){
  var live=await RuntimeCatalog.ScanAsync(false);var downloadReports=new List<object>();
  foreach(var recipe in AppDownloads.Recipes.Where(r=>new[]{"vc2005-x64","vc2005-x86","vc2008-x64","vc2008-x86","vc2010-x64","vc2010-x86","vc2012-x64","vc2012-x86","dotnet-runtime-10-0-win-x64"}.Contains(r.Id))){
   Console.WriteLine("DOWNLOAD: "+recipe.Name);var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());var cached=AppDownloads.Cached(recipe)!;await AppDownloads.VerifyCachedAsync(recipe,cached);Check(File.Exists(result.Path),"Expanded real signed download: "+recipe.Name);downloadReports.Add(cached);
  }
  await File.WriteAllTextAsync(Path.Combine(artifactsPath,"expanded-download-verification.json"),System.Text.Json.JsonSerializer.Serialize(downloadReports,LocalStore.Json));
 }
 var registryOnly=new InstalledApp("NVIDIA App","1","NVIDIA Corporation","Z:\\missing","","test");
 var registryState=GpuSoftware.Inspect([],[],[registryOnly]).Single(s=>s.Tool.Id=="nvidia");
 Check(registryState.Apps.Count==1,"Vendor app registry evidence is kept separate from hardware evidence");
 if(args.Contains("--verify-downloads")){
  var reports=new List<object>();var allPassed=true;
  foreach(var recipe in AppDownloads.Recipes.Where(r=>!args.Contains("--new-components")||new[]{"directx","vc-x64","vc-x86","vc2013-x64","vc2013-x86"}.Contains(r.Id))){
   try{
    Console.WriteLine("DOWNLOAD: "+recipe.Name);
    var result=await AppDownloads.DownloadAsync(recipe,new Progress<string>());
    var record=AppDownloads.Cached(recipe)??throw new Exception("No verified download record");
    await AppDownloads.VerifyCachedAsync(recipe,record);
    reports.Add(new{Name=recipe.Name,Status="PASS",record.Publisher,record.Hash,record.Bytes,record.Source,record.Checked});
    Check(File.Exists(result.Path)&&record.Bytes>1024,"Real signed download: "+recipe.Name);
   }catch(Exception ex){allPassed=false;reports.Add(new{Name=recipe.Name,Status="FAIL",Error=ex.Message,Checked=DateTimeOffset.Now});Console.WriteLine("DOWNLOAD FAIL: "+recipe.Name+" / "+ex.Message);}
  }
  var reportDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","artifacts"));Directory.CreateDirectory(reportDir);
  await File.WriteAllTextAsync(Path.Combine(reportDir,args.Contains("--new-components")?"download-components-verification.json":"download-verification.json"),System.Text.Json.JsonSerializer.Serialize(reports,LocalStore.Json));
  Check(allPassed,"All configured download sources fetched and signatures validated (no installers launched)");
 }
 Console.WriteLine($"\n{count} checks passed. No system configuration changed.");
}finally{
 var absolute=Path.GetFullPath(root);var expected=Path.GetFullPath(Path.GetTempPath());
 if(!absolute.StartsWith(expected,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(absolute).StartsWith("FromZero-Checks-"))throw new InvalidOperationException("Unexpected test cleanup path");
 Directory.Delete(absolute,true);
}

sealed class MemoryGameState : IGameStateStore
{
 public GameSession? Current;
 public GameSession? Load()=>Current;
 public void Save(GameSession state)=>Current=state;
 public void Clear()=>Current=null;
}
sealed class FakeGamePlatform : IGamePlatform
{
 public const string Balanced="381b4222-f694-41f0-9685-ff5bb260df2e",High="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",Other="a1841308-3541-4fab-bc81-f71556f20b4a";
 public string Plan=Balanced;public bool Supported=true,FailRestore;public string? FailWriteName;
 public Dictionary<string,int> Values=new(){[@"Software\Microsoft\GameBar|AutoGameModeEnabled"]=0,[@"System\GameConfigStore|GameDVR_Enabled"]=1,[@"Software\Microsoft\Windows\CurrentVersion\GameDVR|AppCaptureEnabled"]=1};
 public string GetPlan()=>Plan;
 public Task<bool> HasPerformancePlanAsync()=>Task.FromResult(Supported);
 public Task SetPlanAsync(string id){if(FailRestore&&id==Balanced)throw new IOException("simulated restore failure");Plan=id;return Task.CompletedTask;}
 public int? ReadDword(string key,string name)=>Values.TryGetValue(key+"|"+name,out var value)?value:null;
 public void WriteDword(string key,string name,int value){if(FailWriteName==name){FailWriteName=null;throw new IOException("simulated apply failure");}Values[key+"|"+name]=value;}
}
sealed class FakePerformanceBackend:IPerformanceBackend
{
 public string CurrentPower=FakeGamePlatform.Balanced;public bool Animated=true,FailApply;public uint NvidiaPower=5,NvidiaQuality=0,NvidiaRefresh=0,NvidiaVsync=0x47814940;
 public string Power()=>CurrentPower;public Task<bool> HasHighPowerAsync()=>Task.FromResult(true);public Task SetPowerAsync(string id){CurrentPower=id;return Task.CompletedTask;}
 public bool Animations()=>Animated;public void SetAnimations(bool value)=>Animated=value;
 public List<NvidiaValue> ReadNvidia(PerformanceOptions options)=>new List<NvidiaValue>{new(NvidiaSettings.PowerId,NvidiaPower,true,5,"Power"),new(NvidiaSettings.QualityId,NvidiaQuality,true,0,"Quality"),new(NvidiaSettings.RefreshId,NvidiaRefresh,true,0,"Refresh"),new(NvidiaSettings.VsyncId,NvidiaVsync,true,0x47814940,"VSync")}.Where(s=>NvidiaSettings.Desired(options).ContainsKey(s.Id)).ToList();
 public void ApplyNvidia(PerformanceOptions options){if(FailApply)throw new IOException("simulated GPU setting failure");if(options.NvidiaPower)NvidiaPower=1;if(options.NvidiaQuality)NvidiaQuality=20;if(options.NvidiaRefresh)NvidiaRefresh=1;if(options.NvidiaVsyncOff)NvidiaVsync=NvidiaSettings.VsyncOff;}
 public void RestoreNvidia(List<NvidiaValue> original,PerformanceOptions options){foreach(var s in original){if(s.Id==NvidiaSettings.PowerId&&NvidiaPower==1)NvidiaPower=s.Value;if(s.Id==NvidiaSettings.QualityId&&NvidiaQuality==20)NvidiaQuality=s.Value;if(s.Id==NvidiaSettings.RefreshId&&NvidiaRefresh==1)NvidiaRefresh=s.Value;if(s.Id==NvidiaSettings.VsyncId&&NvidiaVsync==NvidiaSettings.VsyncOff)NvidiaVsync=s.Value;}}
}


