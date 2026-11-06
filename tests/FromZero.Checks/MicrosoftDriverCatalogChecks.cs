using FromZero;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

internal static class MicrosoftDriverCatalogChecks
{
 public static void Run(string root,Action<bool,string> check)
 {
  var id=Guid.NewGuid();
  const string hardwareId=@"PCI\VEN_10EC&DEV_8168&SUBSYS_337517AA&REV_1B";
  var row=$"<tr id=\"{id}_R0\"><td></td><td>Realtek &amp; Network</td><td>Windows 11</td><td>Drivers (Networking)</td><td>10/2/2025</td><td>10.79.50.1003</td></tr>";
  var found=MicrosoftDriverCatalog.ParseSearch(row,hardwareId);
  check(found.Count==1&&found[0].Id==id.ToString()&&found[0].Title=="Realtek & Network"&&found[0].Version=="10.79.50.1003",
   "Microsoft Catalog search parser binds GUID, driver classification and version from result cells");
  var architecture=RuntimeInformation.OSArchitecture switch{Architecture.X64=>"AMD64",Architecture.Arm64=>"ARM64",_=>"X86"};
  var details=$"<div id='archDiv'>Architecture: {architecture}</div><div id='classificationDiv'>Drivers</div><div>pci\\ven_10ec&amp;dev_8168&amp;subsys_337517aa&amp;rev_1b</div>";
  var candidate=MicrosoftDriverCatalog.ParseDetails(found[0],details,[hardwareId]);
  check(candidate?.HardwareId==hardwareId&&candidate.Architecture==architecture&&
   MicrosoftDriverCatalog.ParseDetails(found[0],details,[@"PCI\VEN_10EC&DEV_9999"])==null&&
   MicrosoftDriverCatalog.ParseDetails(found[0],details.Replace(architecture,"WrongArchitecture"),[hardwareId])==null,
   "Catalog candidates require package-detail architecture and exact supported hardware identity");
  bool Rejected(Action action)
  {
   try{action();return false;}catch(Exception exception)when(exception is InvalidDataException or EndOfStreamException){return true;}
  }
  check(Rejected(()=>MicrosoftDriverCatalog.ParseSearch("<html>Service unavailable</html>",hardwareId))&&
   MicrosoftDriverCatalog.ParseSearch("We did not find any results",hardwareId).Count==0,
   "Catalog response errors are distinct from a successfully empty search");
  var hash=Convert.ToBase64String(new byte[32]);
  var dialog=$"downloadInformation[0].updateID ='{id}'; downloadInformation[0].files[0].url = 'https://catalog.s.download.windowsupdate.com/driver/test.cab'; downloadInformation[0].files[0].sha256 = '{hash}';";
  check(MicrosoftDriverCatalog.ParseDownloads(dialog,id).Count==1&&
   Rejected(()=>MicrosoftDriverCatalog.ParseDownloads(dialog,Guid.NewGuid()))&&
   Rejected(()=>MicrosoftDriverCatalog.ParseDownloads(dialog.Replace(hash,""),id))&&
   Rejected(()=>MicrosoftDriverCatalog.ParseDownloads(dialog.Replace("catalog.s.download.windowsupdate.com","download.windowsupdate.com.attacker.test"),id)),
   "Catalog downloads require the selected package GUID, official HTTPS CAB host and SHA-256");
  var directory=Path.Combine(root,"catalog-cab-preflight");
  Directory.CreateDirectory(directory);
  string Cabinet(string name)
  {
   var encoded=Encoding.UTF8.GetBytes(name);
   var bytes=new byte[44+16+encoded.Length+1];
   "MSCF"u8.CopyTo(bytes);
   BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8),(uint)bytes.Length);
   BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16),44);
   bytes[24]=3;bytes[25]=1;
   BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26),1);
   BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28),1);
   encoded.CopyTo(bytes.AsSpan(60));
   var path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".cab");
   File.WriteAllBytes(path,bytes);
   return path;
  }
  var signed=Cabinet(@"driver\signed.inf");
  using(var append=new FileStream(signed,FileMode.Append))append.Write([1,2,3,4]);
  check(MicrosoftDriverCatalog.ValidateCabinet(Cabinet(@"driver\device.inf")).SequenceEqual([@"driver\device.inf"])&&
   MicrosoftDriverCatalog.ValidateCabinet(signed).SequenceEqual([@"driver\signed.inf"])&&
   new[]{@"..\escape.inf",@"C:\outside.inf",@"\\server\outside.inf",@"driver\NUL.inf",@"driver\bad.inf.",@"driver\bad:stream"}
    .All(name=>Rejected(()=>MicrosoftDriverCatalog.ValidateCabinet(Cabinet(name)))),
   "Cabinet preflight permits signed trailers and rejects traversal, streams, reserved Windows names and trailing aliases");
 }
}
