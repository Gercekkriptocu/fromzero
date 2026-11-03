using FromZero;
using System.Runtime.InteropServices;

internal static class ReclaimChecks
{
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CreateHardLink(string name,string existing,IntPtr security);
 public static async Task Run(string root,Action<bool,string> check)
 {
  check(ReclaimRules.All.Count==255&&ReclaimRules.All.Select(r=>r.Id).Distinct().Count()==255,"Cleanup loads 255 unique scoped rules");
  check(ReclaimRules.Match("THUMBCACHE_*.DB","thumbcache_1024.db")&&!ReclaimRules.Match("a?","abc"),"ReClaim port preserves wildcard matching");
  var baseRule=ReclaimRules.All.Single(r=>r.Id=="user-temp");
  bool Reject(Action action){try{action();return false;}catch(Exception e)when(e is IOException or InvalidDataException){return true;}}
  check(Reject(()=>ReclaimRules.Validate(baseRule with{Paths=[@"%TEMP%\..\Documents"]})),"ReClaim rejects parent traversal in rule data");
  check(Reject(()=>ReclaimRules.Validate(baseRule with{Paths=[@"%TEMP%\*\Cache"]})),"ReClaim rejects unrestricted first-folder wildcard");
  check(Reject(()=>ReclaimRules.Validate(baseRule with{Paths=[@"%SECRET%\cache"]})),"ReClaim rejects unapproved path variables");
  var guard=new ReclaimGuard();
  var protectionEngine=new ReclaimCleaner(ReclaimCleaner.PlatformRoots(),()=>new(StringComparer.OrdinalIgnoreCase),false,[baseRule with{Paths=["%WINDIR%"]}]);
  var protectedScan=await protectionEngine.ScanAsync();check(protectedScan.Items[0].Partial&&protectedScan.Items[0].Files==0,"ReClaim skips protected rule roots without aborting the scan");
  check(Reject(()=>guard.Check(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),true)),"ReClaim protects personal folder roots");
  check(Reject(()=>guard.Check(Path.Combine(Environment.SystemDirectory,"drivers"),true)),"ReClaim protects Windows driver files");
  check(Reject(()=>guard.Check(LocalStore.Root,true)),"ReClaim protects FromZero data");
  var sandbox=Path.Combine(root,"Reclaim");Directory.CreateDirectory(sandbox);
  var variables=ReclaimCleaner.PlatformRoots();variables["TEMP"]=[sandbox];
  HashSet<string> apps=new(StringComparer.OrdinalIgnoreCase);
  ReclaimCleaner Engine(params ReclaimRule[] rules)=>new(variables,()=>apps,false,rules);
  async Task<string> File(string name,bool old=true){var path=Path.Combine(sandbox,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await System.IO.File.WriteAllTextAsync(path,"fixture-"+name);if(old){System.IO.File.SetCreationTimeUtc(path,DateTime.UtcNow.AddDays(-3));System.IO.File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-3));}return path;}
  var old=await File("old.tmp");var fresh=await File("fresh.tmp",false);var blend=await File("recovery.blend");var autosave=await File(@"autosave\recovery.bin");
  var engine=Engine(baseRule);var scan=await engine.ScanAsync();var files=engine.FilesFor(scan,baseRule.Id);
  check(files.Count==1&&files[0].Path==old&&System.IO.File.Exists(old),"ReClaim scan is read-only and preserves age/exclusion filters");
  var late=await File("added-after-preview.tmp");var report=await engine.CleanAsync(scan,[baseRule.Id]);
  check(report.Files==1&&!System.IO.File.Exists(old)&&System.IO.File.Exists(late)&&System.IO.File.Exists(fresh)&&System.IO.File.Exists(blend)&&System.IO.File.Exists(autosave),"ReClaim deletes only reviewed files by handle and keeps new/autosave files");
  bool reused=false;try{await engine.CleanAsync(scan,[baseRule.Id]);}catch(InvalidDataException){reused=true;}check(reused,"ReClaim refuses reusing a consumed cleanup plan");
  engine=Engine(baseRule);scan=await engine.ScanAsync();await System.IO.File.AppendAllTextAsync(late,"changed");report=await engine.CleanAsync(scan,[baseRule.Id]);
  check(report.Files==0&&report.Skipped>=1&&System.IO.File.Exists(late),"ReClaim rechecks changed files before deleting");
  System.IO.File.SetLastWriteTimeUtc(late,DateTime.UtcNow.AddDays(-3));
  var appRule=baseRule with{CloseApps=["fixture.exe"]};engine=Engine(appRule);scan=await engine.ScanAsync();apps.Add("fixture.exe");report=await engine.CleanAsync(scan,[baseRule.Id]);
  check(report.Files==0&&report.Skipped>=1,"ReClaim blocks apps that started after scanning");
  scan=await engine.ScanAsync();check(scan.Items[0].Blocked!=null,"ReClaim marks running-app caches unavailable in preview");apps.Clear();
  var locked=await File("locked.tmp");engine=Engine(baseRule);scan=await engine.ScanAsync();using(var stream=new FileStream(locked,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){report=await engine.CleanAsync(scan,[baseRule.Id]);check(System.IO.File.Exists(locked)&&report.Skipped>=1,"ReClaim skips locked files without killing apps");}
  var direct=await File("dumps\\direct.dmp");var other=await File("dumps\\keep.txt");var nested=await File("dumps\\sub\\nested.dmp");var fileRule=baseRule with{Paths=[@"%TEMP%\dumps"],Mode="files",Include=["*.dmp"],MinAgeHours=null};
  engine=Engine(fileRule);scan=await engine.ScanAsync();report=await engine.CleanAsync(scan,[baseRule.Id]);check(report.Files==1&&!System.IO.File.Exists(direct)&&System.IO.File.Exists(other)&&System.IO.File.Exists(nested),"ReClaim files mode preserves unmatched and nested files");
  engine=Engine(baseRule with{Admin=true});scan=await engine.ScanAsync();report=await engine.CleanAsync(scan,[baseRule.Id]);check(scan.Items[0].Blocked!=null&&report.Files==0,"ReClaim refuses admin targets in a normal process");
  engine=Engine(baseRule with{Mode="report",Command="do-not-execute"});scan=await engine.ScanAsync();report=await engine.CleanAsync(scan,[baseRule.Id]);check(scan.Items[0].Files==0&&report.Files==0,"ReClaim report commands never become filesystem or shell actions");
  var survivor=await File("hardlink-source.tmp");var hard=Path.Combine(sandbox,"hardlink.tmp");check(CreateHardLink(hard,survivor,IntPtr.Zero),"Hard-link fixture created");
  engine=Engine(baseRule);scan=await engine.ScanAsync();report=await engine.CleanAsync(scan,[baseRule.Id]);check(System.IO.File.Exists(hard)&&System.IO.File.Exists(survivor)&&report.Skipped>=2,"ReClaim skips hard links and avoids false freed-space accounting");
  var target=Path.Combine(root,"OutsideCache");Directory.CreateDirectory(target);var outside=Path.Combine(target,"important.txt");await System.IO.File.WriteAllTextAsync(outside,"keep");var link=Path.Combine(sandbox,"linked");Directory.CreateSymbolicLink(link,target);
  try{engine=Engine(baseRule);scan=await engine.ScanAsync();check(!engine.FilesFor(scan,baseRule.Id).Any(f=>f.Path.Contains("linked",StringComparison.OrdinalIgnoreCase)),"ReClaim never follows directory links during scan");report=await engine.CleanAsync(scan,[baseRule.Id]);check(System.IO.File.Exists(outside),"ReClaim cleanup preserves files beyond a directory link");}finally{Directory.Delete(link);}
  using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await engine.ScanAsync(cancellation:cancelled.Token);}catch(OperationCanceledException){stopped=true;}check(stopped,"ReClaim scanning supports cancellation without deletion");
 }
}
