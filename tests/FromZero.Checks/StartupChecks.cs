using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using FromZero;
static class StartupChecks
{
 public static void Unit(Action<bool,string> check)
 {
  var id=Guid.NewGuid().ToString("N");var elevated=PreparationWorker.StartInfo(@"C:\Program Files\FromZero\FromZero.exe",id,true);var standard=PreparationWorker.StartInfo(elevated.FileName,id,false);
  check(!elevated.UseShellExecute&&elevated.CreateNoWindow&&elevated.Verb.Length==0,"An elevated UI starts the worker with its existing token without a second elevation request");
  check(standard.UseShellExecute&&standard.Verb=="runas"&&standard.ArgumentList.SequenceEqual(new[]{"--preparation-worker",id}),"A standard UI requests elevation with fixed worker arguments");
  check(elevated.ArgumentList.SequenceEqual(standard.ArgumentList)&&elevated.FileName==@"C:\Program Files\FromZero\FromZero.exe","Worker executable paths with spaces and job IDs stay separate arguments");
  var previous=File.ReadAllText(LocalStore.FilePath("last-preparation.txt"));var job=PreparationPolicy.Create(new(),PreparationBackend.Sid,Environment.ProcessPath!);PreparationStore.SaveState(job,false);PreparationWorker.RecordStartupFailure(job.Id,new InvalidOperationException("Specific startup cause"));var loaded=PreparationStore.Load(job.Id)!;
  check(loaded.State=="attention"&&loaded.Message.Contains("Specific startup cause")&&loaded.Steps.All(s=>s.Attempts==0),"A startup failure saves the actual cause without pretending that an installer ran");
  check(File.ReadAllText(LocalStore.FilePath("last-preparation.txt"))==previous,"Diagnostic/error persistence does not replace the user's last preparation selection");
  check(PreparationWorker.FailureMessage(job.Id,1).Contains("Specific startup cause")&&!PreparationWorker.FailureMessage(Guid.NewGuid().ToString("N"),-532462766).Contains("Aynı Windows kullanıcısıyla yönetici"),"Worker errors use recorded evidence and do not misdiagnose every crash as a permission problem");
 }
 public static async Task RunProcesses(string executable,string artifacts,Action<bool,string> check)
 {
  executable=Path.GetFullPath(executable);if(!File.Exists(executable))throw new FileNotFoundException("Build the app before startup integration tests",executable);
  var store=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FromZero"));var root=Path.Combine(store,"Preparation");Directory.CreateDirectory(root);var last=Path.Combine(store,"last-preparation.txt");var previous=File.Exists(last)?File.ReadAllText(last):null;
  var sid=WindowsIdentity.GetCurrent().User!.Value;List<string> created=[];List<object> evidence=[];
  PreparationJob Fixture(){var job=PreparationPolicy.Create(new(){Apps=[],Drivers=false,WindowsUpdates=false,GpuApps=false,OemDrivers=false,ResumeAtLogon=false},sid,executable);var folder=Path.GetFullPath(Path.Combine(root,job.Id));if(!folder.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Fixture path escaped its root");Directory.CreateDirectory(folder);created.Add(folder);File.WriteAllText(Path.Combine(folder,"state.json"),JsonSerializer.Serialize(job,LocalStore.Json));return job;}
  async Task<int> Run(string argument,string id){var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};info.ArgumentList.Add(argument);info.ArgumentList.Add(id);using var process=Process.Start(info)??throw new IOException("Startup probe did not start");using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill();await process.WaitForExitAsync();throw new TimeoutException("Safe startup-only probe did not exit");}return process.ExitCode;}
  try{
   var probe=Fixture();using(var guiMutex=new Mutex(false,"Local\\FromZero-"+sid)){
    var exit=await Run("--preparation-worker-check",probe.Id);var folder=created[0];var proof=Path.Combine(folder,"startup-check.json");check(exit==0&&File.Exists(proof),"Real WPF executable enters its headless worker-check branch and exits cleanly");using var json=JsonDocument.Parse(File.ReadAllText(proof));check(json.RootElement.GetProperty("CheckOnly").GetBoolean()&&json.RootElement.GetProperty("PlanValid").GetBoolean(),"Real startup validates the same user, executable and compiled plan without running the engine");
    var state=JsonSerializer.Deserialize<PreparationJob>(File.ReadAllText(Path.Combine(folder,"state.json")),LocalStore.Json)!;check(state.State=="planned"&&state.Consented==null&&state.Steps.All(s=>s.Attempts==0),"Headless startup check creates no consent, installs and state transitions");check(!File.Exists(Path.Combine(folder,"worker-error.json")),"Read-only worker startup succeeds while the GUI instance mutex exists");evidence.Add(new{Mode="worker-check",ExitCode=exit,NoUnexpectedWindow=true,InstallStepsStarted=0});
   }
   var unapproved=Fixture();var refused=await Run("--preparation-worker",unapproved.Id);var refusedFolder=created[1];var error=JsonSerializer.Deserialize<PreparationWorkerError>(File.ReadAllText(Path.Combine(refusedFolder,"worker-error.json")),LocalStore.Json)!;var refusedState=JsonSerializer.Deserialize<PreparationJob>(File.ReadAllText(Path.Combine(refusedFolder,"state.json")),LocalStore.Json)!;
   check(refused==1&&error.Type==typeof(InvalidOperationException).FullName&&error.Message.Contains("henüz kabul edilmedi"),"Actual worker startup handles an unapproved plan with its real cause instead of an unhandled WPF crash");check(refusedState.State=="attention"&&refusedState.Steps.All(s=>s.Attempts==0),"Failed real startup leaves all installation steps untouched and saves a useful status");evidence.Add(new{Mode="unapproved-worker",ExitCode=refused,Error=error.Message,InstallStepsStarted=0});
   var invalid=await Run("--preparation-worker","invalid-id");check(invalid==1,"Malformed worker arguments exit through handled startup failure rather than opening the GUI");
   check((File.Exists(last)?File.ReadAllText(last):null)==previous,"Real executable startup probes preserve the user's selected preparation record");
   await File.WriteAllTextAsync(Path.Combine(artifacts,"startup-0.13.1-verification.json"),JsonSerializer.Serialize(new{Checked=DateTimeOffset.Now,ExecutableVersion=FileVersionInfo.GetVersionInfo(executable).FileVersion,ReadOnly=true,UserLastSelectionPreserved=true,Cases=evidence},LocalStore.Json));
  }finally{foreach(var folder in created){var full=Path.GetFullPath(folder);if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(full),"N",out _))throw new InvalidDataException("Unsafe startup fixture cleanup path");if(Directory.Exists(full)){PackageSafety.RejectReparsePoints(root,full);Directory.Delete(full,true);}}}
 }
}
