// Scan / reviewed clean adapted from ReClaim junk.rs, clean.rs and paths.rs.
// Copyright (c) 2026 yusufky63 and contributors. MIT; see ThirdParty/Reclaim/LICENSE.txt.
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FromZero;

public record ReclaimCandidate(string Root,string Path,long Size,DateTime LastWrite,DateTime Created);
public record ReclaimItem(ReclaimRule Rule,long Bytes,int Files,bool Partial,string[] Running,string? Blocked,string[] Roots);
public record ReclaimScan(Guid Id,DateTimeOffset Created,IReadOnlyList<ReclaimItem> Items,int RulesChecked);
public record ReclaimFailure(string Path,string Reason);
public record ReclaimReport(long Bytes,int Files,int Skipped,bool Cancelled,IReadOnlyList<ReclaimFailure> Failures);

public sealed class ReclaimCleaner
{
 private readonly IReadOnlyDictionary<string,string[]> roots;
 private readonly Func<HashSet<string>> running;
 private readonly bool elevated;
 private readonly IReadOnlyList<ReclaimRule> rules;
 private readonly ReclaimGuard guard=new();
 private readonly Dictionary<string,List<ReclaimCandidate>> plans=new(StringComparer.Ordinal);
 private ReclaimScan? issued;
 private bool consumed;
 private const int RuleLimit=20000,TotalLimit=100000;
 public ReclaimCleaner():this(PlatformRoots(),RunningProcesses,IsElevated(),ReclaimRules.All){}
 internal ReclaimCleaner(IReadOnlyDictionary<string,string[]> roots,Func<HashSet<string>> running,bool elevated,IReadOnlyList<ReclaimRule> rules)
 {this.roots=roots;this.running=running;this.elevated=elevated;this.rules=rules;foreach(var rule in rules)ReclaimRules.Validate(rule);}
 public static bool IsElevated()=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
 public static HashSet<string> RunningProcesses()
 {
  var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var process in Process.GetProcesses())using(process){try{result.Add(process.ProcessName+".exe");}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){throw new IOException("Açık uygulamalar doğrulanamadı. Yeniden tara.");}}
  return result;
 }
 internal static Dictionary<string,string[]> PlatformRoots()
 {
  var result=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
  foreach(var variable in ReclaimRules.Variables){var value=Environment.GetEnvironmentVariable(variable);result[variable]=string.IsNullOrWhiteSpace(value)?[]:[value];}
  result["TEMP"]=[Path.GetTempPath()];
  result["SYSTEMDRIVE"]=[Path.GetPathRoot(Environment.SystemDirectory)!];
  using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");var steam=key?.GetValue("SteamPath")?.ToString();
  result["STEAM"]=!string.IsNullOrEmpty(steam)&&Directory.Exists(steam)?[steam]:[];
  var libraries=new HashSet<string>(result["STEAM"],StringComparer.OrdinalIgnoreCase);
  if(steam!=null){try{var vdf=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}catch(IOException){}catch(UnauthorizedAccessException){}}
  result["STEAMLIBRARY"]=libraries.ToArray();return result;
 }
 internal IEnumerable<string> Expand(string pattern)
 {
  var parts=pattern.Split('\\');var variable=parts[0].Trim('%');
  var current=roots.GetValueOrDefault(variable,[]).Where(p=>Path.IsPathFullyQualified(p)&&!p.StartsWith("\\\\",StringComparison.Ordinal)).Select(Path.GetFullPath).ToList();
  foreach(var segment in parts.Skip(1)){
   var next=new List<string>();
   foreach(var path in current){try{
    PackageSafety.RejectReparsePoints(path,path);
    if(segment.IndexOfAny(['*','?'])>=0){if(Directory.Exists(path))foreach(var entry in Directory.EnumerateFileSystemEntries(path)){if(ReclaimRules.Match(segment,Path.GetFileName(entry))&&(File.GetAttributes(entry)&ReclaimFileDelete.SkipAttributes)==0)next.Add(entry);}}
    else next.Add(Path.Combine(path,segment));
   }catch(IOException){}catch(UnauthorizedAccessException){}}
   current=next;
  }
  return current.Where(p=>File.Exists(p)||Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 }
 public IReadOnlyList<ReclaimCandidate> FilesFor(ReclaimScan scan,string id)
 {
  if(!ReferenceEquals(scan,issued))throw new InvalidDataException("Bu tarama artık geçerli değil.");
  return plans.TryGetValue(id,out var files)?files.AsReadOnly():[];
 }
 public Task<ReclaimScan> ScanAsync(IProgress<string>? progress=null,CancellationToken cancellation=default)=>Task.Run(()=>{
  issued=null;plans.Clear();consumed=false;var apps=running();var results=new List<ReclaimItem>();int total=0,index=0;
  foreach(var rule in rules){
   cancellation.ThrowIfCancellationRequested();progress?.Report($"{++index}/{rules.Count} • {rule.NameTr}");
   var active=rule.CloseApps.Where(apps.Contains).ToArray();var found=ExpandAll(rule);var files=new List<ReclaimCandidate>();bool partial=false;
   var blocked=rule.Mode=="report"?"Windows / üretici aracıyla yönetilir":active.Length>0?"Önce kapat: "+string.Join(", ",active):rule.Admin&&!elevated?"Yönetici yetkisi gerekli":null;
   // Caution is opt-in in the UI. Report rules never become filesystem deletion plans.
   if(rule.Mode!="report")foreach(var root in found){
    if(total>=TotalLimit||files.Count>=RuleLimit){partial=true;break;}
    try{
     if(rule.Mode=="files")PackageSafety.RejectReparsePoints(root,root);else guard.Check(root,rule.Mode!="folder");
     var pending=new Stack<string>();pending.Push(root);
     while(pending.Count>0){
      cancellation.ThrowIfCancellationRequested();var current=pending.Pop();
      if(files.Count>=RuleLimit||total>=TotalLimit){partial=true;break;}
      try{
       var attr=File.GetAttributes(current);if((attr&ReclaimFileDelete.SkipAttributes)!=0)continue;
       if((attr&FileAttributes.Directory)!=0){
        if(rule.Exclude.Any(p=>ReclaimRules.Match(p,Path.GetFileName(current))))continue;
        if(rule.Mode!="files")guard.Check(current,true);
        foreach(var entry in Directory.EnumerateFileSystemEntries(current)){
         var ea=File.GetAttributes(entry);if((ea&ReclaimFileDelete.SkipAttributes)!=0)continue;
         if(rule.Mode=="files"&&(ea&FileAttributes.Directory)!=0)continue;
         pending.Push(entry);
        }
       }else{
        var relative=Path.GetRelativePath(root,current);if(relative.Split('\\').Any(s=>rule.Exclude.Any(p=>ReclaimRules.Match(p,s))))continue;
        if(rule.Mode=="files"&&!rule.Include.Any(p=>ReclaimRules.Match(p,Path.GetFileName(current))))continue;
        var file=new FileInfo(current);var cutoff=rule.MinAgeHours is int hours?DateTime.UtcNow.AddHours(-hours):DateTime.MaxValue;
        if(file.LastWriteTimeUtc>cutoff||file.CreationTimeUtc>cutoff)continue;
        guard.Check(current);files.Add(new(root,file.FullName,file.Length,file.LastWriteTimeUtc,file.CreationTimeUtc));total++;
       }
      }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException){partial=true;}
     }
    }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException){partial=true;}
   }
   plans[rule.Id]=files;results.Add(new(rule,files.Sum(f=>f.Size),files.Count,partial,active,blocked,found));
  }
  issued=new(Guid.NewGuid(),DateTimeOffset.Now,results.AsReadOnly(),rules.Count);return issued;
 },cancellation);
 private string[] ExpandAll(ReclaimRule rule)=>rule.Paths.SelectMany(Expand).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 public Task<ReclaimReport> CleanAsync(ReclaimScan scan,IEnumerable<string> selected,IProgress<string>? progress=null,CancellationToken cancellation=default)=>Task.Run(()=>{
  if(!ReferenceEquals(scan,issued)||consumed)throw new InvalidDataException("Bu temizlik planı geçersiz veya kullanıldı. Yeniden tara.");
  consumed=true;var ids=selected.Distinct(StringComparer.Ordinal).ToArray();
  if(ids.Any(id=>!plans.ContainsKey(id)))throw new InvalidDataException("Taramada bulunmayan hedef reddedildi.");
  var failures=new List<ReclaimFailure>();long bytes=0;int count=0,skipped=0;var deleted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  void Fail(string path,string why){skipped++;if(failures.Count<200)failures.Add(new(path,why));}
  foreach(var id in ids){
   if(cancellation.IsCancellationRequested)break;
   var item=scan.Items.Single(i=>i.Rule.Id==id);var rule=item.Rule;progress?.Report(rule.NameTr);
   var apps=running();if(rule.Mode=="report"||rule.Admin&&!elevated||rule.CloseApps.Any(apps.Contains)){foreach(var file in plans[id])Fail(file.Path,"Uygulama açık, yönetici gerekiyor veya resmi araç gerekli.");continue;}
   var currentRoots=ExpandAll(rule).ToHashSet(StringComparer.OrdinalIgnoreCase);
   foreach(var file in plans[id]){
    if(cancellation.IsCancellationRequested)break;if(!deleted.Add(file.Path))continue;
    try{
     if(!currentRoots.Contains(file.Root))throw new IOException("Kuralın hedefi değişti.");
     if(rule.Mode=="files")PackageSafety.RejectReparsePoints(file.Root,file.Root);else guard.Check(file.Root,rule.Mode!="folder");guard.Check(file.Path);
     var path=PackageSafety.ResolveInside(file.Root,Path.GetRelativePath(file.Root,file.Path));
     if(!path.Equals(file.Path,StringComparison.OrdinalIgnoreCase))throw new IOException("Hedef yolu değişti.");
     ReclaimFileDelete.Delete(file);bytes+=file.Size;count++;
    }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException){Fail(file.Path,ex.Message);}
   }
  }
  return new ReclaimReport(bytes,count,skipped,cancellation.IsCancellationRequested,failures.AsReadOnly());
 });
}
