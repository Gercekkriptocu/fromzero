using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FromZero;

internal record DeltaFile(CapsuleFile File,long Offset);
internal record ReleaseDelta(ReleaseManifest Manifest,byte[] ManifestBytes,byte[] Signature,DeltaFile[] Files,long TotalBytes)
{
 public static ReleaseDelta Read(byte[] metadata,byte[] signature,string key,string runtime)
 {
  if(metadata.Length>1_000_000||signature.Length is <256 or >1024||!ReleaseIntegrity.VerifySignature(metadata,signature,key))
   throw new InvalidDataException("The FromZero update signature could not be verified.");
  var manifest=JsonSerializer.Deserialize<ReleaseManifest>(metadata,LocalStore.Json)
   ??throw new InvalidDataException("Invalid release manifest.");
  if(manifest.Schema!=1||manifest.Runtime!=runtime||!Version.TryParse(manifest.Version,out var version)||
     version.Build<0||version.Revision>=0||manifest.Files is not {Count:>=4 and <=4000})
   throw new InvalidDataException("Unsupported release manifest.");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var files=new List<DeltaFile>();
  long offset=0;
  foreach(var file in manifest.Files)
  {
   if(file==null||file.Path==null||!ValidPath(file.Path)||!names.Add(file.Path)||
      file.Size<0||file.Size>256L*1024*1024||!Regex.IsMatch(file.Sha256??"",@"\A[0-9A-Fa-f]{64}\z"))
    throw new InvalidDataException("Invalid release file record.");
   files.Add(new(file,offset));
   offset+=file.Size;
   if(offset>1024L*1024*1024)throw new InvalidDataException("The FromZero update exceeds the size limit.");
  }
  if(!new[]{"FromZero.exe","FromZero.dll","FromZero.deps.json","FromZero.runtimeconfig.json"}.All(names.Contains))
   throw new InvalidDataException("The FromZero update is incomplete.");
  return new(manifest,metadata,signature,files.ToArray(),offset);
 }

 private static bool ValidPath(string path)=>path.Length is >0 and <=220&&
  !path.Equals("release-manifest.json",StringComparison.OrdinalIgnoreCase)&&
  !path.Equals("release-manifest.sig",StringComparison.OrdinalIgnoreCase)&&
  path.Split('\\').All(part=>Regex.IsMatch(part,@"\A[A-Za-z0-9_.-]+\z")&&!part.EndsWith('.')&&
   !Regex.IsMatch(part,@"\A(?:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|\z)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant));

 public async Task<(int Changed,long Downloaded)> StageAsync(string installed,string stage,
  Func<long,long,CancellationToken,Task<Stream>> openRange,IProgress<string> progress,CancellationToken token)
 {
  var missing=new List<DeltaFile>();
  foreach(var item in Files)
  {
   token.ThrowIfCancellationRequested();
   var source=PackageSafety.ResolveInside(installed,item.File.Path);
   var target=PackageSafety.ResolveInside(stage,item.File.Path);
   Directory.CreateDirectory(Path.GetDirectoryName(target)!);
   if(File.Exists(source)&&new FileInfo(source).Length==item.File.Size&&
      (await PackageSafety.HashFileAsync(installed,source)).Sha256.Equals(item.File.Sha256,StringComparison.OrdinalIgnoreCase))
    File.Copy(source,target,false);
   else missing.Add(item);
  }
  long downloaded=0;
  for(var index=0;index<missing.Count;)
  {
   var first=index++;
   var offset=missing[first].Offset;
   var length=missing[first].File.Size;
   while(index<missing.Count&&missing[index].Offset==offset+length)length+=missing[index++].File.Size;
   await using var stream=length==0?Stream.Null:await openRange(offset,length,token);
   for(var i=first;i<index;i++)
   {
    var file=missing[i].File;
    progress.Report("Downloading changed file · "+file.Path);
    var path=PackageSafety.ResolveInside(stage,file.Path);
    await using(var output=File.Create(path))
    {
     var buffer=new byte[81920];
     var remaining=file.Size;
     while(remaining>0)
     {
      var count=await stream.ReadAsync(buffer.AsMemory(0,(int)Math.Min(remaining,buffer.Length)),token);
      if(count==0)throw new EndOfStreamException("The FromZero update download ended early.");
      await output.WriteAsync(buffer.AsMemory(0,count),token);
      remaining-=count;
     }
    }
    var actual=await PackageSafety.HashFileAsync(stage,path);
    if(!actual.Sha256.Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("An update file failed verification: "+file.Path);
    downloaded+=file.Size;
   }
  }
  await File.WriteAllBytesAsync(Path.Combine(stage,"release-manifest.json"),ManifestBytes,token);
  await File.WriteAllBytesAsync(Path.Combine(stage,"release-manifest.sig"),Signature,token);
  return(missing.Count,downloaded);
 }
}
