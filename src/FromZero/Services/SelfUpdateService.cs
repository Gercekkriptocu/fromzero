using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FromZero;

internal static class SelfUpdateService
{
 private const string ReleaseRoot="https://github.com/Gercekkriptocu/fromzero/releases/";
 private const string Asset="FromZero-update-win-x64";
 private static readonly HttpClient Http=new(new HttpClientHandler{AllowAutoRedirect=false})
 {Timeout=TimeSpan.FromMinutes(2)};
 public static Version InstalledVersion
 {
  get
  {
   var version=typeof(SelfUpdateService).Assembly.GetName().Version!;
   return new(version.Major,version.Minor,version.Build);
  }
 }
 private static string StandardInstall=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"FromZero","App");
 public static bool Supported=>RuntimeInformation.ProcessArchitecture==Architecture.X64&&
  Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).Equals(StandardInstall,StringComparison.OrdinalIgnoreCase);
 public static string? LastFailure()
 {
  var path=Path.Combine(Path.GetDirectoryName(StandardInstall)!,"update-last.log");
  try
  {
   if(!File.Exists(path)||new FileInfo(path).Length>4096)return null;
   return FormatFailure(File.ReadAllText(path));
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return null;}
 }
 internal static string? FormatFailure(string text)
 {
  text=text.Trim();
  if(text is "" or "OK")return null;
  try
  {
   using var document=JsonDocument.Parse(text);
   var result=document.RootElement;
   if(result.GetProperty("Code").GetString()=="ok")return null;
   if(result.TryGetProperty("RollbackComplete",out var rollback)&&rollback.ValueKind==JsonValueKind.False)
    return Locale.Text("Update recovery needs attention. Install the latest FromZero setup.");
   if(result.GetProperty("Code").GetString()=="locked")
    return Locale.Text("Some application files are in use. Close other FromZero windows and retry.");
   return Locale.Text(result.GetProperty("Phase").GetString() switch
   {
    "wait"=>"FromZero did not close in time. Close it and retry.",
    "verify"=>"The downloaded update did not pass verification. Retry the update.",
    "startup"=>"The updated FromZero could not start. The previous version was restored.",
    _=>"The update could not be applied. The previous version was restored."
   });
  }
  catch(Exception ex) when(ex is JsonException or KeyNotFoundException or InvalidOperationException)
  {
   return Locale.Text(text.Contains("Move",StringComparison.OrdinalIgnoreCase)
    ?"Some application files are in use. Close other FromZero windows and retry."
    :"The previous update did not finish. Retry or install the latest FromZero setup.");
  }
 }

 public static async Task<ReleaseDelta?> CheckAsync(CancellationToken token)
 {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
  deadline.CancelAfter(TimeSpan.FromSeconds(25));
  var manifest=await ReadSmallAsync(new(ReleaseRoot+"latest/download/"+Asset+".json"),1_000_000,deadline.Token);
  var signature=await ReadSmallAsync(new(ReleaseRoot+"latest/download/"+Asset+".sig"),1024,deadline.Token);
  var key=ReleaseIntegrity.PublisherKey()??throw new InvalidDataException("FromZero update key is unavailable.");
  var release=ReleaseDelta.Read(manifest,signature,key,"win-x64");
  return Version.Parse(release.Manifest.Version)>InstalledVersion?release:null;
 }

 public static async Task PrepareAndScheduleAsync(ReleaseDelta release,IProgress<string> progress,CancellationToken token)
 {
  if(!Supported||Version.Parse(release.Manifest.Version)<=InstalledVersion)
   throw new InvalidOperationException("A newer FromZero release and a standard installation are required.");
  var installed=StandardInstall;
  var parent=Path.GetDirectoryName(installed)!;
  PackageSafety.RejectReparsePoints(Path.GetPathRoot(parent)!,installed);
  var drive=new DriveInfo(Path.GetPathRoot(parent)!);
  if(drive.AvailableFreeSpace<release.TotalBytes+64L*1024*1024)
   throw new IOException("Free up space on the Windows drive to update FromZero.");
  var stage=Path.Combine(parent,".fromzero-next-"+Guid.NewGuid().ToString("N"));
  var backup=Path.Combine(parent,".fromzero-previous-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(stage);
  bool scheduled=false;
  try
  {
   var url=new Uri(ReleaseRoot+"download/v"+release.Manifest.Version+"/"+Asset+".pack");
   var result=await release.StageAsync(installed,stage,(offset,length,ct)=>OpenRangeAsync(url,offset,length,release.TotalBytes,ct),progress,token);
   var inspection=await ReleaseIntegrity.InspectAsync(stage,InstalledVersion,candidate:true);
   if(!inspection.UpdateEligible||inspection.Version!=release.Manifest.Version)
    throw new InvalidDataException("The staged FromZero update did not pass verification.");
   token.ThrowIfCancellationRequested();
   Journal.Write("FromZero update",release.Manifest.Version+" / "+result.Changed+" changed files / "+result.Downloaded+" bytes");
   var info=new ProcessStartInfo(Commands.PowerShell)
   {
    UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.SystemDirectory
   };
   foreach(var arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",
    Convert.ToBase64String(Encoding.Unicode.GetBytes(ApplyScript(installed,stage,backup,Environment.ProcessId)))})
    info.ArgumentList.Add(arg);
   using var helper=Process.Start(info)??throw new IOException("The FromZero update worker did not start.");
   scheduled=true;
  }
  finally
  {
   if(!scheduled)
   {
    try{PackageSafety.RejectReparsePoints(parent,stage);Directory.Delete(stage,true);}
    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Journal.Write("FromZero update cleanup",ex.Message,false);}
   }
  }
 }

 private static bool Allowed(Uri uri)=>uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&
  uri.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";

 private static async Task<HttpResponseMessage> SendAsync(Uri uri,RangeHeaderValue? range,CancellationToken token)
 {
  for(int redirects=0;redirects<6;redirects++)
  {
   if(!Allowed(uri))throw new InvalidDataException("Unexpected FromZero release download host.");
   using var request=new HttpRequestMessage(HttpMethod.Get,uri);
   request.Headers.UserAgent.ParseAdd("FromZero-Updater/1.0");
   request.Headers.Range=range;
   request.Headers.AcceptEncoding.ParseAdd("identity");
   var response=await Http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
   if((int)response.StatusCode is >=300 and <400&&response.Headers.Location is {} next)
   {
    response.Dispose();
    uri=new Uri(uri,next);
    continue;
   }
   return response;
  }
  throw new IOException("The FromZero release has too many redirects.");
 }

 private static async Task<byte[]> ReadSmallAsync(Uri uri,int limit,CancellationToken token)
 {
  using var response=await SendAsync(uri,null,token);
  response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("FromZero release metadata is too large.");
  await using var input=await response.Content.ReadAsStreamAsync(token);
  using var output=new MemoryStream();
  var buffer=new byte[8192];
  int count;
  while((count=await input.ReadAsync(buffer,token))>0)
  {
   if(output.Length+count>limit)throw new InvalidDataException("FromZero release metadata is too large.");
   output.Write(buffer,0,count);
  }
  return output.ToArray();
 }

 private static async Task<Stream> OpenRangeAsync(Uri uri,long offset,long length,long total,CancellationToken token)
 {
  var last=offset+length-1;
  var response=await SendAsync(uri,new RangeHeaderValue(offset,last),token);
  try
  {
   var range=response.Content.Headers.ContentRange;
   if(response.StatusCode!=HttpStatusCode.PartialContent||range?.Unit!="bytes"||range.From!=offset||range.To!=last||
      range.Length!=total||response.Content.Headers.ContentLength!=length||response.Content.Headers.ContentEncoding.Count!=0)
    throw new IOException("The server did not return the requested FromZero update bytes.");
   // The caller owns the content stream and disposes it after consuming the requested range.
   return await response.Content.ReadAsStreamAsync(token);
  }
  catch{response.Dispose();throw;}
 }

 internal static string ApplyScript(string installed,string stage,string backup,int parentId)=>
  "$ErrorActionPreference='Stop';$installed="+PackageSafety.QuotePs(installed)+";$stage="+PackageSafety.QuotePs(stage)+
  ";$backup="+PackageSafety.QuotePs(backup)+";$parentId="+parentId+";"+"""
  $log=Join-Path (Split-Path -Parent $installed) 'update-last.log';
  $changes=[Collections.Generic.List[object]]::new();$child=$null;$committed=$false;$restored=$true;$phase='wait';
  function Resolve-File($root,$relative){
   $base=[IO.Path]::GetFullPath($root).TrimEnd([char]92)+[char]92;
   $path=[IO.Path]::GetFullPath([IO.Path]::Combine($base,$relative));
   if(!$path.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid update path.'};
   for($parent=[IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($path));$null -ne $parent;$parent=$parent.Parent){
    if($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked update directory.'};
   };
   if([IO.File]::Exists($path) -and ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked update file.'};
   return $path;
  };
  function Move-File($source,$destination){
   [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination));
   for($attempt=0;;$attempt++){
    try{[IO.File]::Move($source,$destination);return}
    catch{
     $code=$_.Exception.GetBaseException().HResult -band 65535;
     if($code -notin @(32,33) -or $attempt -ge 39){throw};
     Start-Sleep -Milliseconds 500;
    }
   }
  };
  function Same-File($path,$size,$hash){
   for($attempt=0;;$attempt++){
    try{
     if(![IO.File]::Exists($path) -or ([IO.FileInfo]::new($path)).Length -ne $size){return $false};
     $algorithm=[Security.Cryptography.SHA256]::Create();$inputFile=$null;
     try{
      $inputFile=[IO.File]::OpenRead($path);
      $actual=[BitConverter]::ToString($algorithm.ComputeHash($inputFile)).Replace('-','');
      return $actual -eq $hash;
     }finally{if($inputFile){$inputFile.Dispose()};$algorithm.Dispose()};
    }catch{
     $errorCode=$_.Exception.GetBaseException().HResult -band 65535;
     if($errorCode -notin @(32,33) -or $attempt -ge 39){throw};
     Start-Sleep -Milliseconds 500;
    }
   }
  };
  function Write-Result($code,$message){
   $json=[pscustomobject]@{Code=$code;Phase=$phase;Message=$message;RollbackComplete=$restored}|ConvertTo-Json -Compress;
   [IO.File]::WriteAllText($log,$json,[Text.UTF8Encoding]::new($false));
  };
  try{
   $parent=Get-Process -Id $parentId -ErrorAction SilentlyContinue;
   if($parent -and !$parent.WaitForExit(30000)){throw 'FromZero did not close in time.'};
   Set-Location -LiteralPath $env:SystemRoot;
   $phase='verify';
   $manifest=Get-Content -LiteralPath (Join-Path $stage 'release-manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json;
   $next=@{};
   foreach($file in $manifest.Files){
    $source=Resolve-File $stage $file.Path;$target=Resolve-File $installed $file.Path;
    if(![IO.File]::Exists($source) -or !(Same-File $source $file.Size $file.Sha256)){throw 'The staged update file failed verification.'};
    if($next.ContainsKey($file.Path)){throw 'Duplicate update file.'};
    $next[$file.Path]=$true;
    if(!(Same-File $target $file.Size $file.Sha256)){
     $changes.Add([pscustomobject]@{Source=$source;Target=$target;Saved=(Resolve-File $backup $file.Path);OldMoved=$false;NewMoved=$false});
    }
   };
   $oldManifest=Join-Path $installed 'release-manifest.json';
   if([IO.File]::Exists($oldManifest)){
    $old=Get-Content -LiteralPath $oldManifest -Raw -Encoding UTF8|ConvertFrom-Json;
    foreach($file in $old.Files){
     if(!$next.ContainsKey($file.Path)){
      $target=Resolve-File $installed $file.Path;
      if(Same-File $target $file.Size $file.Sha256){
       $changes.Add([pscustomobject]@{Source='';Target=$target;Saved=(Resolve-File $backup $file.Path);OldMoved=$false;NewMoved=$false});
      }
     }
    }
   };
   foreach($name in @('release-manifest.json','release-manifest.sig')){
    $changes.Add([pscustomobject]@{Source=(Resolve-File $stage $name);Target=(Resolve-File $installed $name);Saved=(Resolve-File $backup $name);OldMoved=$false;NewMoved=$false});
   };
   $phase='apply';
   foreach($change in $changes){
    if([IO.File]::Exists($change.Target)){Move-File $change.Target $change.Saved;$change.OldMoved=$true};
    if($change.Source){Move-File $change.Source $change.Target;$change.NewMoved=$true};
   };
   $phase='startup';
   $child=Start-Process -FilePath (Join-Path $installed 'FromZero.exe') -WorkingDirectory $installed -ArgumentList '--elevated' -PassThru;
   $ready=$false;
   for($i=0;$i -lt 100;$i++){
    Start-Sleep -Milliseconds 250;$child.Refresh();
    if($child.HasExited){throw 'The updated FromZero process exited during startup.'};
    if($child.MainWindowHandle -ne [IntPtr]::Zero -and $child.Responding){$ready=$true;break};
   };
   if(!$ready){throw 'The updated FromZero did not finish starting.'};
   Write-Result 'ok' '';$committed=$true;
  }catch{
   $failure=$_.Exception;$message=$failure.Message;$code='failed';
   if(($failure.GetBaseException().HResult -band 65535) -in @(32,33)){$code='locked'};
   try{
    if($child -and !$child.HasExited){$child.Kill();[void]$child.WaitForExit(10000)};
    for($i=$changes.Count-1;$i -ge 0;$i--){
     $change=$changes[$i];
     if($change.NewMoved){Move-File $change.Target $change.Source};
     if($change.OldMoved){Move-File $change.Saved $change.Target};
    }
   }catch{$restored=$false;$message+=' / Rollback: '+$_.Exception.Message};
   try{Write-Result $code $message}catch{};
   if($restored -and [IO.File]::Exists((Join-Path $installed 'FromZero.exe'))){
    try{Start-Process -FilePath (Join-Path $installed 'FromZero.exe') -WorkingDirectory $installed -ArgumentList '--elevated'}catch{};
   };
  }finally{
   if($committed -or $restored){
    foreach($folder in @($stage,$backup)){
     if([IO.Directory]::Exists($folder)){try{[IO.Directory]::Delete($folder,$true)}catch{}};
    }
   }
  }
  """;
}
