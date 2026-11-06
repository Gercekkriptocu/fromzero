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
  await File.WriteAllTextAsync(Path.Combine(incoming,"before.txt"),"new");
  await File.WriteAllTextAsync(Path.Combine(live,"runtime.dll"),"unchanged");
  File.Copy(Path.Combine(live,"runtime.dll"),Path.Combine(incoming,"runtime.dll"));
  var original=await File.ReadAllBytesAsync(Path.Combine(live,"FromZero.exe"));
  var files=new List<CapsuleFile>();
  foreach(var file in Directory.GetFiles(incoming))files.Add(await PackageSafety.HashFileAsync(incoming,file));
  await File.WriteAllTextAsync(Path.Combine(incoming,"release-manifest.json"),JsonSerializer.Serialize(new ReleaseManifest(1,"9.8.7","win-x64",files)));
  await File.WriteAllTextAsync(Path.Combine(incoming,"release-manifest.sig"),"fixture signature");
  // This open file denies delete access, reproducing the folder move problem on Windows.
  using(var held=new FileStream(Path.Combine(live,"runtime.dll"),FileMode.Open,FileAccess.Read,FileShare.Read))
  {
   var transient=new FileStream(Path.Combine(live,"before.txt"),FileMode.Open,FileAccess.Read,FileShare.Read);
   var releaseLock=Task.Run(async()=>{await Task.Delay(2500);transient.Dispose();});
   var swap=await Commands.PsAsync(SelfUpdateService.ApplyScript(live,incoming,rollback,int.MaxValue),60);
   await releaseLock;
   using var last=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(basePath,"update-last.log")));
   check(swap.Success&&last.RootElement.GetProperty("Phase").GetString()=="startup"&&
    last.RootElement.GetProperty("RollbackComplete").GetBoolean()&&
    (await File.ReadAllBytesAsync(Path.Combine(live,"FromZero.exe"))).SequenceEqual(original)&&
    await File.ReadAllTextAsync(Path.Combine(live,"before.txt"))=="old"&&
    !Directory.Exists(rollback)&&!Directory.Exists(incoming),
    "Transient file locks are retried, unchanged locked runtimes are retained, and startup failure restores original bytes");
  }
  check(SelfUpdateService.FormatFailure("{\"Code\":\"ok\"}")==null&&
   SelfUpdateService.FormatFailure("Exception calling \"Move\" with \"2\" argument(s): broken text")?.Contains("application files")==true,
   "Old folder-lock errors become readable update feedback and successful structured logs clear the warning");
  await SuccessfulRestart(basePath,check);
 }

 private static async Task SuccessfulRestart(string basePath,Action<bool,string> check)
 {
  var root=Directory.CreateDirectory(Path.Combine(basePath,"successful-restart")).FullName;
  var installed=Directory.CreateDirectory(Path.Combine(root,"App")).FullName;
  var incoming=Directory.CreateDirectory(Path.Combine(root,"next")).FullName;
  var backup=Path.Combine(root,"previous");
  var source=Path.Combine(root,"Fixture.cs");
  await File.WriteAllTextAsync(source,"""
   using System;
   using System.Drawing;
   using System.Windows.Forms;
   class Fixture {
    [STAThread] static void Main() {
     var form=new Form {Text="FromZero update test",StartPosition=FormStartPosition.Manual,Location=new Point(-10000,-10000)};
     var timer=new Timer {Interval=2500};
     timer.Tick+=(s,e)=>{timer.Stop();form.Close();};
     form.Shown+=(s,e)=>timer.Start();
     Application.Run(form);
    }
   }
   """);
  var compiler=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Microsoft.NET","Framework64","v4.0.30319","csc.exe");
  var built=await Commands.RunAsync(compiler,["/nologo","/target:winexe","/reference:System.Windows.Forms.dll","/reference:System.Drawing.dll",
   "/out:"+Path.Combine(incoming,"FromZero.exe"),source]);
  if(!built.Success)throw new IOException(built.Output);
  await File.WriteAllTextAsync(Path.Combine(installed,"FromZero.exe"),"old executable fixture");
  await File.WriteAllTextAsync(Path.Combine(installed,"stable.dll"),"unchanged");
  File.Copy(Path.Combine(installed,"stable.dll"),Path.Combine(incoming,"stable.dll"));
  var files=new List<CapsuleFile>();
  foreach(var path in Directory.GetFiles(incoming))files.Add(await PackageSafety.HashFileAsync(incoming,path));
  await File.WriteAllTextAsync(Path.Combine(incoming,"release-manifest.json"),JsonSerializer.Serialize(new ReleaseManifest(1,"9.8.7","win-x64",files)));
  await File.WriteAllTextAsync(Path.Combine(incoming,"release-manifest.sig"),"fixture signature");
  using var held=new FileStream(Path.Combine(installed,"stable.dll"),FileMode.Open,FileAccess.Read,FileShare.Read);
  var applied=await Commands.PsAsync(SelfUpdateService.ApplyScript(installed,incoming,backup,int.MaxValue),60);
  using var report=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"update-last.log")));
  check(applied.Success&&report.RootElement.GetProperty("Code").GetString()=="ok"&&
   !Directory.Exists(incoming)&&!Directory.Exists(backup)&&
   (await File.ReadAllBytesAsync(Path.Combine(installed,"FromZero.exe"))).AsSpan(0,2).SequenceEqual("MZ"u8),
   "The updater replaces a real executable and confirms its window starts while an unchanged runtime remains locked");
 }
}
