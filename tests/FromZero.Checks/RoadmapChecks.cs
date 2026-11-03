using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FromZero;
static class RoadmapChecks
{
 public static async Task Run(string root,Action<bool,string> check)
 {
  var choices=PreparationExperience.Profile("gaming") with{RequiredLibraries=["vc2010-x86"]};
  var planned=PreparationPolicy.Create(choices,"test","C:\\FromZero.exe");
  check(planned.Steps.Where(s=>s.Kind=="package").Select(s=>s.Id).SequenceEqual(["package-vc2010-x86"]),"Game evidence selects the exact runtime and architecture without adding a bundle");
  var old=JsonSerializer.Deserialize<PreparationOptions>("{\"GamingLibraries\":true}")!;
  check(PreparationPolicy.Create(old,"test","C:\\FromZero.exe").Steps.Count(s=>s.Kind=="package")==13,"Existing plan options keep their original steps when new runtime fields are absent");
  var altered=choices with{RequiredLibraries=["nvidia"]};
  try{PreparationPolicy.Create(altered,"test","C:\\FromZero.exe");throw new Exception("Unrelated installer accepted as runtime");}catch(InvalidDataException){check(true,"Runtime selection rejects non-runtime installers");}
  var resume=PreparationPolicy.Create(new(){Apps=["steam"]},"test","C:\\FromZero.exe");
  resume.Consented=DateTimeOffset.Now;resume.State="paused";resume.Steps.Single(s=>s.Id=="app-steam").State="done";
  var backend=new ResumeFixture();
  await new PreparationEngine(backend,_=>{},()=>false).RunAsync(resume,DateTimeOffset.Now,new Progress<string>(_=>{}));
  check(resume.State=="attention"&&backend.Executed==0&&PreparationAudit.Read(resume).Single().Phase=="resume-review","Resume verifies a completed installation and pauses before new writes if it vanished");
  var audit=Path.Combine(PreparationStore.Folder(resume.Id),"attempt-journal.jsonl");
  await File.WriteAllTextAsync(audit,(await File.ReadAllTextAsync(audit)).Replace("resume-review","altered"));
  try{PreparationAudit.Read(resume);throw new Exception("Tampered journal accepted");}catch(InvalidDataException){check(true,"Interrupted or changed preparation audit evidence is detected");}
  var imports=ExecutableDependencies.Inspect(Environment.ProcessPath!);
  check(imports.Imports.Libraries.Count>0&&imports.Imports.Architecture is "x64" or "arm64" or "x86","Real Windows executable imports are read without execution");
  var requirements=ExecutableDependencies.Assess(new("x86",["msvcp100.dll","d3dx9_43.dll","d3d12.dll"]),root,p=>p.EndsWith("d3dx9_43.dll"));
  check(requirements.Single(r=>r.Library=="msvcp100.dll").Recipe=="vc2010-x86"&&requirements.Single(r=>r.Library=="d3dx9_43.dll").Present&&requirements.Single(r=>r.Library=="d3d12.dll").Recipe==null,"Game scan distinguishes x86 runtime, existing DLL and Windows Direct3D");
  var app=new InstalledApp("Fixture","1","Vendor",@"C:\Program Files\Fixture",@"C:\Program Files\Fixture\app.exe","Registry","fixture");
  var signal=AppLifecycle.Inspect(app,[@"C:\Program Files\Fixture-other\app.exe"],[new("entry","\"C:\\Program Files\\Fixture\\app.exe\" --start","Run",false)]);
  check(!signal.Running&&signal.StartupEntries==1&&AppLifecycle.Shared(app with{Name="Microsoft Visual C++ 2015-2022"}),"Application evidence uses exact folder boundaries and protects shared runtimes");
  check(AppLifecycle.StillRegistered(app,[app with{Version="2"}])&&!AppLifecycle.StillRegistered(app,[app with{RegistryKey="different"}]),"Removal verification uses registry identity independently of version");
  var query=DnsWire.Query(1234);var reply=query.Concat(new byte[]{0xc0,0x0c,0,1,0,1,0,0,0,30,0,4,192,0,2,1}).ToArray();reply[2]=0x81;reply[3]=0x80;reply[7]=1;
  check(DnsWire.ValidResponse(query,reply)&&!DnsWire.ValidResponse(query,reply[..^1])&&!DnsWire.ValidResponse(DnsWire.Query(1235),reply),"DNS probe validates transaction identity and complete IPv4 answer bytes");
  var samples=NetworkQuality.Summarize([new(true,10,""),new(true,14,""),new(false,0,""),new(true,18,"")],"");
  check(samples.Received==3&&samples.LossPercent==25&&samples.MedianMs==14&&samples.JitterMs==4,"Network results calculate loss, median and jitter from successful samples");
  check(NetworkQuality.IsProbeTarget("example.com")&&NetworkQuality.IsProbeTarget("::1")&&
   !NetworkQuality.IsProbeTarget("https://example.com/path")&&!NetworkQuality.IsProbeTarget("example.com & cmd"),"Game route accepts only an IP or hostname");
  var route=await NetworkQuality.MeasureGameRouteAsync("127.0.0.1");
  check(route.Probes.Sent==12&&route.Probes.Received==12&&route.Probes.LossPercent==0,"Game route collects 12 real ICMP replies on loopback without changing network settings");
  var protocol=new BenchmarkConditions("CS2","repeatable replay","1920x1080",30,"same system and driver");
  var runs=Enumerable.Range(0,6).Select(i=>new BenchmarkRun(i<3?"before":"after",protocol,Convert.ToHexString(SHA256.HashData([(byte)i])),DateTimeOffset.Now,new("cs2.exe / PID "+i,3000,i<3?60:75,i<3?45:60,i<3?25:20,30,2,"FrameTime"),"fixture")).ToList();
  var compare=BenchmarkProtocol.Compare(runs);
  check(compare.Comparable&&compare.Conclusive&&compare.FpsPercent==25&&compare.BeforeStuttersPerMinute==4,"Repeated FPS comparison computes medians and duration-normalized stutters");
  check(!BenchmarkProtocol.Compare(runs.Take(5).ToArray()).Comparable,"Insufficient repetitions cannot show an FPS gain");
  var preliminary=BenchmarkProtocol.Compare([runs[0],runs[3]],true);
  check(preliminary.Comparable&&!preliminary.Conclusive&&preliminary.BeforeLow==45&&preliminary.AfterLow==60&&preliminary.BeforeP99==25&&preliminary.AfterP99==20,
   "Profile comparison displays measured FPS and P99 values while keeping one pair preliminary");
  runs[5]=runs[5] with{Protocol=protocol with{Resolution="1280x720"}};
  check(!BenchmarkProtocol.Compare(runs).Comparable,"Resolution changes make an FPS comparison non-comparable");
  runs[5]=runs[5] with{Protocol=protocol,Hash=runs[0].Hash};
  check(!BenchmarkProtocol.Compare(runs).Comparable,"Duplicate CSV captures cannot stand in for repeated experiments");
  var device=new DeviceInfo("Private device",@"PCI\VEN_1234&DEV_ABCD\SERIALSECRET","Net",28,[@"PCI\VEN_1234&DEV_ABCD"]);
  var kit=Path.Combine(root,"offline-index");Directory.CreateDirectory(kit);
  await File.WriteAllTextAsync(Path.Combine(kit,"network.inf"),"[Version]\r\nClass=Net\r\nCatalogFile=network.cat\r\nDriverVer=11/03/2026,1.0.0.0\r\n[Models.NTamd64]\r\n%Device%=Install, PCI\\VEN_1234&DEV_ABCD",Encoding.Unicode);
  var cat=Path.Combine(kit,"network.cat");await File.WriteAllTextAsync(cat,"mock CAT bytes");
  Task<string> Signed(string _) =>Task.FromResult("Fixture signature");
  var index=await OfflineDriverCatalog.BuildAsync(kit,[device],Signed);
  check(index.Drivers.Single().Class=="Net"&&index.Drivers.Single().Devices.Length==1&&(await OfflineDriverCatalog.VerifyAsync(kit,index,[device],Signed)).Count==0,"Offline network index handles Unicode INF, exact hardware, and CAT hashes");
  await File.AppendAllTextAsync(cat,"tamper");
  check((await OfflineDriverCatalog.VerifyAsync(kit,index,[device],Signed)).Count==1,"USB verification rejects changed catalogue bytes before install");
  var archive=CrosshairArchives.Capture();CrosshairArchives.Validate(archive);
  check(archive.Pros.Count==50&&archive.Ranking.Entries.Count==10,"Offline crosshair archive preserves validated catalogue and ranking separately");
  var report=JsonSerializer.Serialize(SupportReport.Build("0.16.0",[device],[],2,resume,[],null));
  check(!report.Contains("SERIALSECRET")&&!report.Contains("Private device")&&!report.Contains(resume.Id)&&report.Contains("ProblemCodes"),"Support preview includes safe counts and omits raw identities and job IDs");
  SourceHealth.Note("https://source.example/release?private=sample","Verified");SourceHealth.Note("https://source.example/release?private=sample","Unavailable");
  var source=SourceHealth.Read().Single(s=>s.Host=="source.example");
  check(source.LastSuccess!=null&&source.Status=="Unavailable"&&!source.Address.Contains('?'),"Source diagnostics preserve last verification and omit query strings");
  await ReleaseChecks(root,check);
 }
 private static async Task ReleaseChecks(string root,Action<bool,string> check)
 {
  var folder=Path.Combine(root,"release-integrity");Directory.CreateDirectory(folder);List<CapsuleFile> files=[];
  foreach(var name in new[]{"FromZero.exe","FromZero.dll","FromZero.deps.json","FromZero.runtimeconfig.json"})
  {var file=Path.Combine(folder,name);await File.WriteAllTextAsync(file,"fixture "+name);files.Add(await PackageSafety.HashFileAsync(folder,file));}
  var release=new ReleaseManifest(1,"0.16.0","win-"+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),files);
  using var rsa=RSA.Create(2048);var key=rsa.ExportSubjectPublicKeyInfoPem();
  var bytes=JsonSerializer.SerializeToUtf8Bytes(release);var signature=rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
  var manifest=Path.Combine(folder,"release-manifest.json");await File.WriteAllBytesAsync(manifest,bytes);await File.WriteAllBytesAsync(Path.Combine(folder,"release-manifest.sig"),signature);
  var accepted=await ReleaseIntegrity.InspectAsync(folder,new(0,15,0),true,key);
  check(accepted.UpdateEligible&&accepted.PublisherVerified,"Pinned RSA signature, compatible architecture and exact package bytes allow a newer bundle");
  check(!(await ReleaseIntegrity.InspectAsync(folder,new(0,16,0),true,key)).UpdateEligible,"Signed releases cannot downgrade or re-install the same version as an update");
  await File.AppendAllTextAsync(manifest," ");
  check(!(await ReleaseIntegrity.InspectAsync(folder,new(0,15,0),true,key)).PublisherVerified,"Edited release manifest fails the pinned signature");
  await File.WriteAllBytesAsync(manifest,bytes);await File.WriteAllTextAsync(Path.Combine(folder,"unexpected.dll"),"extra");
  check(!(await ReleaseIntegrity.InspectAsync(folder,new(0,15,0),true,key)).UpdateEligible,"A signed manifest cannot authorize an extra unlisted executable library");
 }
 private sealed class ResumeFixture:IPreparationBackend,IPreparationResumeVerifier
 {
  public int Executed;
  public Task<PreparationOutcome> ExecuteAsync(PreparationJob job,PreparationStep step,IProgress<string> progress,CancellationToken token){Executed++;return Task.FromResult(new PreparationOutcome("done","fixture"));}
  public Task<string?> MissingCompletedAsync(PreparationJob job,PreparationStep step,CancellationToken token)=>Task.FromResult<string?>("App missing");
 }
}
