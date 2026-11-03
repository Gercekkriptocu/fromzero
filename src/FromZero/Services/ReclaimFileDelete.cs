using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace FromZero;

// Deletes the reviewed file by handle. Denies writers, reparse points, cloud placeholders
// and hard links; rechecks metadata and the final path before setting delete disposition.
internal static class ReclaimFileDelete
{
 [StructLayout(LayoutKind.Sequential)]private struct Info
 {
  public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
  public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
 }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info info);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
 [DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetFileInformationByHandle(SafeFileHandle handle,int type,ref int value,uint size);
 private static long Time(System.Runtime.InteropServices.ComTypes.FILETIME time)=>((long)(uint)time.dwHighDateTime<<32)|(uint)time.dwLowDateTime;
 internal const FileAttributes SkipAttributes=FileAttributes.ReparsePoint|FileAttributes.Offline|FileAttributes.System|(FileAttributes)0x00400000|(FileAttributes)0x00040000;
 public static void Delete(ReclaimCandidate file)
 {
  using var handle=CreateFile(file.Path,0x10000|0x80,1,IntPtr.Zero,3,0x00200000,IntPtr.Zero);
  if(handle.IsInvalid)throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
  if(!GetFileInformationByHandle(handle,out var info))throw new IOException("Dosya bilgisi doğrulanamadı.");
  if(((FileAttributes)info.Attributes&(SkipAttributes|FileAttributes.Directory|FileAttributes.ReadOnly))!=0||info.Links!=1)throw new IOException("Bağlantılı, bulut, salt okunur veya korunan dosya atlandı.");
  var size=((long)info.SizeHigh<<32)|info.SizeLow;
  if(size!=file.Size||Time(info.Written)!=file.LastWrite.ToFileTimeUtc()||Time(info.Created)!=file.Created.ToFileTimeUtc())throw new IOException("Dosya taramadan sonra değişti.");
  var path=new StringBuilder(32768);var length=GetFinalPathNameByHandle(handle,path,(uint)path.Capacity,0);
  if(length==0||length>=path.Capacity||!ReclaimGuard.Key(path.ToString().Replace("\\\\?\\","",StringComparison.Ordinal)).Equals(ReclaimGuard.Key(file.Path),StringComparison.OrdinalIgnoreCase))throw new IOException("Dosyanın gerçek yolu değişti.");
  int delete=1;if(!SetFileInformationByHandle(handle,4,ref delete,4))throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
 }
}
