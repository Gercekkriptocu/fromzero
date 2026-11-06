using System.IO;

namespace FromZero;

internal record UpdateStorage(string Drive,long AvailableBytes)
{
 public const long MinimumBytes=1024L*1024*1024;
 public bool Ready=>AvailableBytes>=MinimumBytes;

 public static UpdateStorage Read()
 {
  var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
  return new(drive.Name,drive.AvailableFreeSpace);
 }

 public static bool IsDiskFull(Exception error)=>error is IOException&&
  error.HResult is unchecked((int)0x80070070) or unchecked((int)0x80070027);
}
