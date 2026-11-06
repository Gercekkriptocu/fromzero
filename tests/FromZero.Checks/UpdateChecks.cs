using FromZero;
using System.Text.Json;
internal static class UpdateChecks
{
 public static async Task Run(Action<bool,string> check)
 {
  var stations=WorldStations.All;check(stations.Select(s=>s.Sector).Distinct().Count()==10&&stations.Select(s=>s.Page).Distinct().Count()==10,"World has ten distinct module destinations");
  check(WorldStations.ForPage("cleanup").Sign=="ZERO CLEAN"&&WorldStations.ForPage("gaming").Sign=="ARCADE","Cleaning and gaming have themed destinations");
  var offers=new WindowsOffer(Guid.NewGuid().ToString(),1,"NVIDIA Display 32.0.1.2","Driver","NVIDIA","GPU","Display","",false,false,false,false,123,"32.0.1.2","2026-10-20");
  check(offers.AutoEligible&&!((offers with{Hidden=true}).AutoEligible)&&!((offers with{Superseded=true}).AutoEligible),"Automatic driver selection excludes hidden and superseded packages");
  check((offers with{Interactive=true}).QueueInstallable&&!(offers with{Interactive=true,Class="Firmware"}).QueueInstallable&&
   !(offers with{Kind="Software",Interactive=true}).QueueInstallable,
   "Driver metadata that can request input remains eligible while firmware and interactive software stay excluded");
  var recommended=WindowsUpdateCatalog.RecommendedDrivers([offers,offers with{Id=Guid.NewGuid().ToString(),Version="32.0.2.0"},offers with{Class="Firmware"}]).ToList();
  check(recommended.Count==1&&recommended[0].Version=="32.0.2.0","Automatic driver queue chooses one newest offered version per device model");
  var exact=offers with{HardwareId=@"PCI\VEN_10DE&DEV_1234"};
  var second=exact with{Id=Guid.NewGuid().ToString(),HardwareId=@"PCI\VEN_10DE&DEV_2345"};
  var regular=exact with{Id=Guid.NewGuid().ToString(),Optional=false,Version="31.1.0.0"};
  var optional=exact with{Optional=true};
  check(WindowsUpdateCatalog.RecommendedDrivers([optional,second,regular]).Select(o=>o.Id).ToHashSet().SetEquals([regular.Id,second.Id])&&
   optional.SelectableDriver&&optional.ReviewGroup=="Optional"&&(offers with{Class="Firmware"}).ReviewGroup=="Manufacturer check required",
   "Hardware IDs keep models separate and optional drivers are available only for manual selection");
  check(WindowsUpdateCatalog.MatchesDevice(exact,new("Display","instance","Display",0,[],true,[exact.HardwareId.ToLowerInvariant()]))&&
   !WindowsUpdateCatalog.MatchesDevice(second,new("Display","instance","Display",0,[exact.HardwareId])),"Driver offers match exact hardware and compatible IDs only");
  bool Reject(Action action){try{action();return false;}catch(Exception ex)when(ex is InvalidDataException or UriFormatException){return true;}}
  check(Reject(()=>WindowsUpdateCatalog.DownloadScript([offers with{Class="Firmware"}])),"Firmware download is refused before elevation");
  check(Reject(()=>WindowsUpdateCatalog.DownloadScript([offers with{Id="'; unsafe"}])),"Update download rejects untrusted identities");
  var script=WindowsUpdateCatalog.DownloadScript([offers]);check(script.Contains(".Download()")&&!script.Contains(".Install()"),"Batch update download does not install anything");
  var parsed=await Commands.PsAsync("$tokens=$null;$errors=$null;[void][System.Management.Automation.Language.Parser]::ParseInput("+PackageSafety.QuotePs(script)+",[ref]$tokens,[ref]$errors);if($errors.Count){throw (($errors | ForEach-Object Message)-join '; ')}");
  check(parsed.Success,"Windows download queue script parses without executing downloads");
  var cached=offers with{Downloaded=true};
  var software=offers with{Id=Guid.NewGuid().ToString(),Title="Windows security update",Kind="Software",Class=""};
  var preview=software with{Id=Guid.NewGuid().ToString(),Title="Windows preview update"};
  var feature=software with{Id=Guid.NewGuid().ToString(),FeatureUpgrade=true};
  check(WindowsUpdateCatalog.MissingUpdates([cached,software,preview,feature,optional]).Select(o=>o.Key).ToHashSet().SetEquals([cached.Key,software.Key]),
   "Select all missing includes cached drivers and regular Windows updates while excluding previews and upgrades");
  var install=PreparationWindows.SelectionScript([cached,software]);
  var installParsed=await Commands.PsAsync("$tokens=$null;$errors=$null;[void][System.Management.Automation.Language.Parser]::ParseInput("+PackageSafety.QuotePs(install)+",[ref]$tokens,[ref]$errors);if($errors.Count){throw (($errors | ForEach-Object Message)-join '; ')}");
  check(installParsed.Success&&install.Contains(cached.Id)&&install.Contains(software.Id)&&install.Contains("if(!$update.IsDownloaded)")&&
   install.Contains("$installer.Install()")&&Reject(()=>PreparationWindows.SelectionScript([feature])),
   "Selected Windows install targets exact identities, reuses cache and rejects feature upgrades");
  var interactive=exact with{Interactive=true};
  var quiet=PreparationWindows.SelectionScript([interactive]);
  check(quiet.Contains("$installer.ForceQuiet=$true")&&quiet.Contains("(!$isDriver -and ($update.InstallationBehavior.CanRequestUserInput")&&
   !quiet.Contains("$upgrade -or $update.InstallationBehavior.CanRequestUserInput"),
   "Interactive-capable drivers reach the Windows quiet installer instead of being skipped");
  var exported=WindowsDriverPackageCache.ExportScript(interactive,Path.Combine(Path.GetTempPath(),"FromZero driver fixture"));
  var exportParsed=await Commands.PsAsync("$tokens=$null;$errors=$null;[void][System.Management.Automation.Language.Parser]::ParseInput("+PackageSafety.QuotePs(exported)+",[ref]$tokens,[ref]$errors);if($errors.Count){throw (($errors | ForEach-Object Message)-join '; ')}");
  check(exportParsed.Success&&exported.Contains(interactive.Id)&&exported.Contains("$update.CopyFromCache($path,$false)")&&
   exported.Contains("DriverHardwareID -ine $hardware")&&!exported.Contains(".Install()")&&
   Reject(()=>WindowsDriverPackageCache.ExportScript(interactive with{Class="Firmware"},Path.GetTempPath())),
   "Native fallback exports the same PC's exact WUA identity with hardware and firmware checks, without installing in the export step");
  check(WindowsUpdateCatalog.DownloadScript([offers],false).Contains("$allowLicense=$false")&&script.Contains("} catch { $report"),"Automatic update queue refuses new licenses and records each package failure");
  check(WindowsUpdateCatalog.ScanScript(true).Contains("IncludePotentiallySupersededUpdates=$true")&&WindowsUpdateCatalog.ScanScript(true).Contains("IsHidden=1")&&!WindowsUpdateCatalog.ScanScript(false).Contains("IsHidden=1"),"Expanded Windows scan explicitly includes hidden and superseded offers");
  check(AppDownloads.Recipes.Count(r=>r.Id.StartsWith("vc"))==12,"Visual C++ catalog covers six generations in x86 and x64");
  var hash=new string('a',128);
  object Metadata(string url)=>new{releases=new[]{new Dictionary<string,object>{["release-version"]="10.0.14",["runtime"]=new{version="10.0.14",files=new[]{new{name="dotnet-runtime-win-x64.exe",rid="win-x64",url,hash}}}}}};
  var installed=new Dictionary<string,string[]>{{"x64/Microsoft.NETCore.App",["10.0.1","10.0.2","9.0.1"]}};
  using var document=JsonDocument.Parse(JsonSerializer.Serialize(Metadata("https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-win-x64.exe")));
  var packages=RuntimeCatalog.ParseChannel(document.RootElement,"10.0",true,installed);
  check(packages.Count==1&&packages[0].NeedsUpdate&&packages[0].Installed.Length==2,"Runtime scan retains all installed patches and compares within the same channel");
  check(packages[0].Recipe.ExpectedSha512==hash,"Runtime package binds to the official SHA-512 digest");
  using var bad=JsonDocument.Parse(JsonSerializer.Serialize(Metadata("https://example.invalid/installer.exe")));
  check(Reject(()=>RuntimeCatalog.ParseChannel(bad.RootElement,"10.0",true,installed)),"Runtime metadata rejects non-vendor installer hosts");
  check(Reject(()=>AppDownloads.RegisterRuntime(packages[0].Recipe with{ExpectedSha512="bad"})),"Runtime registration rejects malformed release hashes");
  check(RuntimeCatalog.InstalledVersions().Keys.Any(k=>k=="x64/Microsoft.NETCore.App"),"Runtime inventory reads architecture-specific installed frameworks without changes");
  object Sdk(string version)=>new{version,files=new[]{new{name="dotnet-sdk-win-x64.exe",rid="win-x64",url="https://builds.dotnet.microsoft.com/dotnet/Sdk/"+version+"/dotnet-sdk.exe",hash}}};
  using var sdkDoc=JsonDocument.Parse(JsonSerializer.Serialize(new{releases=new[]{new Dictionary<string,object>{["release-version"]="10.0.14",["sdk"]=Sdk("10.0.403"),["sdks"]=new[]{Sdk("10.0.403"),Sdk("10.0.108"),Sdk("10.0.107")}}}}));
  var sdkPackages=RuntimeCatalog.ParseChannel(sdkDoc.RootElement,"10.0",true,new Dictionary<string,string[]>{{"x64/sdk",["10.0.101"]}});
  check(sdkPackages.Count==2&&sdkPackages.Single(p=>p.Version=="10.0.108").Installed.SequenceEqual(["10.0.101"]),"SDK scan retains latest patch in each feature band");
  check(!sdkPackages.Single(p=>p.Version=="10.0.403").NeedsUpdate,"SDK scan does not auto-upgrade an installed SDK across feature bands");
 }
}
