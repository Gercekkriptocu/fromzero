using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
namespace FromZero;

public static class Authenticode
{
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 private struct TrustFile {public uint Size;public IntPtr Path;public IntPtr File;public IntPtr Subject;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 private struct TrustData
 {
  public uint Size;public IntPtr PolicyCallback;public IntPtr SipClient;public uint UiChoice;public uint RevocationChecks;
  public uint UnionChoice;public IntPtr FileInfo;public uint StateAction;public IntPtr StateData;public IntPtr UrlReference;public uint ProviderFlags;public uint UiContext;
 }
 [DllImport("wintrust.dll",ExactSpelling=true,SetLastError=false)]
 private static extern int WinVerifyTrust(IntPtr window,[In]ref Guid action,[In,Out]ref TrustData data);
 [DllImport("wintrust.dll",ExactSpelling=true)]private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
 [DllImport("wintrust.dll",ExactSpelling=true)]private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider,uint signer,[MarshalAs(UnmanagedType.Bool)]bool counterSigner,uint counterIndex);
 [StructLayout(LayoutKind.Sequential)]private struct FileTime{public uint Low,High;}
 [StructLayout(LayoutKind.Sequential)]private struct ProviderSigner{public uint Size;public FileTime VerifyTime;public uint CertificateCount;public IntPtr Certificates;}
 [StructLayout(LayoutKind.Sequential)]private struct ProviderCertificate{public uint Size;public IntPtr Certificate;}
 [StructLayout(LayoutKind.Sequential)]private struct CertificateContext{public uint Encoding;public IntPtr Encoded;public uint Length;public IntPtr Info;public IntPtr Store;}
 public static string Verify(string path)
 {
  if(!System.IO.Path.IsPathFullyQualified(path)||!System.IO.File.Exists(path))throw new FileNotFoundException("İmza kontrolü için dosya bulunamadı.",path);
  var text=Marshal.StringToCoTaskMemUni(path);var fileMemory=IntPtr.Zero;
  var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
  var data=new TrustData{Size=(uint)Marshal.SizeOf<TrustData>(),UiChoice=2,UnionChoice=1,StateAction=1};
  try{
   var file=new TrustFile{Size=(uint)Marshal.SizeOf<TrustFile>(),Path=text};fileMemory=Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());Marshal.StructureToPtr(file,fileMemory,false);data.FileInfo=fileMemory;
   var status=WinVerifyTrust(new IntPtr(-1),ref action,ref data);
   if(status!=0)throw new InvalidDataException("Windows dijital imzayı güvenilir olarak doğrulayamadı (0x"+unchecked((uint)status).ToString("X8")+"). Paket çalıştırılmadı.");
   var provider=WTHelperProvDataFromStateData(data.StateData);
   var signerPointer=provider==IntPtr.Zero?IntPtr.Zero:WTHelperGetProvSignerFromChain(provider,0,false,0);
   if(signerPointer==IntPtr.Zero)throw new InvalidDataException("Doğrulanmış imza zinciri okunamadı.");
   var signer=Marshal.PtrToStructure<ProviderSigner>(signerPointer);
   if(signer.CertificateCount==0||signer.Certificates==IntPtr.Zero)throw new InvalidDataException("İmza sertifikası bulunamadı.");
   var cert=Marshal.PtrToStructure<ProviderCertificate>(signer.Certificates);
   if(cert.Certificate==IntPtr.Zero)throw new InvalidDataException("İmza sertifikası boş.");
   var context=Marshal.PtrToStructure<CertificateContext>(cert.Certificate);
   if(context.Length==0||context.Length>1_000_000)throw new InvalidDataException("İmza sertifikası boyutu geçersiz.");
   var raw=new byte[context.Length];Marshal.Copy(context.Encoded,raw,0,raw.Length);
   using var full=X509CertificateLoader.LoadCertificate(raw);
   if(string.IsNullOrWhiteSpace(full.Subject))throw new InvalidDataException("İmza yayıncısı okunamadı.");
   return full.Subject;
  }finally{
   data.StateAction=2;WinVerifyTrust(new IntPtr(-1),ref action,ref data);
   if(fileMemory!=IntPtr.Zero)Marshal.FreeHGlobal(fileMemory);Marshal.FreeCoTaskMem(text);
  }
 }
}
