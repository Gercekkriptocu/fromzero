using FromZero;
using System.Security.Cryptography;
using System.Text.Json;

internal static class SelfUpdateChecks
{
 public static async Task Run(string root,Action<bool,string> check)
 {
  var basePath=Path.Combine(root,"delta");
  var installed=Directory.CreateDirectory(Path.Combine(basePath,"installed")).FullName;
  var stage=Directory.CreateDirectory(Path.Combine(basePath,"stage")).FullName;
  var names=new[]{"FromZero.exe","FromZero.dll","FromZero.deps.json","FromZero.runtimeconfig.json","en\\resource.dll"};
  byte[][] payload=[[1,1],[2,2,2],[3],[4,4,4,4],[5,5]];
  var manifest=new ReleaseManifest(1,"9.8.7","win-x64",names.Select((name,i)=>
   new CapsuleFile(name,payload[i].Length,Convert.ToHexString(SHA256.HashData(payload[i])))).ToList());
  using var rsa=RSA.Create(2048);
  var publicKey=rsa.ExportSubjectPublicKeyInfoPem();
  byte[] Sign(byte[] bytes)=>rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
  var json=JsonSerializer.SerializeToUtf8Bytes(manifest,LocalStore.Json);
  var signature=Sign(json);
  var delta=ReleaseDelta.Read(json,signature,publicKey,"win-x64");
  for(var i=0;i<4;i++)await File.WriteAllBytesAsync(Path.Combine(installed,names[i]),i==1?[0,0,0]:payload[i]);
  var packed=payload.SelectMany(bytes=>bytes).ToArray();
  var ranges=new List<(long,long)>();
  Task<Stream> Fetch(long offset,long size,CancellationToken token)
  {
   ranges.Add((offset,size));
   return Task.FromResult<Stream>(new MemoryStream(packed,(int)offset,(int)size,false));
  }
  var result=await delta.StageAsync(installed,stage,Fetch,new Progress<string>(),default);
  var checkedStage=await ReleaseIntegrity.InspectAsync(stage,new(9,8,6),candidate:true,publicKey);
  check(result==(2,5L)&&ranges.SequenceEqual([(2L,3L),(10L,2L)])&&checkedStage.UpdateEligible,
   "A signed file delta downloads only the changed and missing byte ranges, and the reconstructed release verifies");
  bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
  var bad=JsonSerializer.SerializeToUtf8Bytes(manifest with{Files=[..manifest.Files,new(@"..\escape.dll",0,new string('0',64))]},LocalStore.Json);
  check(Reject(()=>ReleaseDelta.Read(json,[..new byte[256]],publicKey,"win-x64"))&&
   Reject(()=>ReleaseDelta.Read(bad,Sign(bad),publicKey,"win-x64"))&&
   Reject(()=>ReleaseDelta.Read(json,signature,publicKey,"win-arm64")),
   "The updater rejects bad signatures, signed path traversal and mismatched architectures");
  var corrupt=Directory.CreateDirectory(Path.Combine(basePath,"corrupt")).FullName;
  bool refused=false;
  try{await delta.StageAsync(installed,corrupt,(_,size,_)=>Task.FromResult<Stream>(new MemoryStream(new byte[(int)size])),new Progress<string>(),default);}
  catch(InvalidDataException){refused=true;}
  check(refused&&!File.Exists(Path.Combine(corrupt,"release-manifest.json")),"Corrupted range data is refused before staging can be published");

  var live=Directory.CreateDirectory(Path.Combine(basePath,"live")).FullName;
  var incoming=Directory.CreateDirectory(Path.Combine(basePath,"incoming")).FullName;
  var rollback=Path.Combine(basePath,"rollback");
  await File.WriteAllTextAsync(Path.Combine(live,"before.txt"),"old");
  await File.WriteAllTextAsync(Path.Combine(incoming,"FromZero.exe"),"invalid executable fixture");
  File.Copy(Path.Combine(Environment.SystemDirectory,"where.exe"),Path.Combine(live,"FromZero.exe"));
  var swap=await Commands.PsAsync(SelfUpdateService.ApplyScript(live,incoming,rollback,int.MaxValue),40);
  check(swap.Success&&File.Exists(Path.Combine(live,"before.txt"))&&!Directory.Exists(rollback)&&!Directory.Exists(incoming),
   "The Windows update worker restores the previous directory if the new executable cannot start");
 }
}
