using System.IO;
using System.Text.RegularExpressions;

namespace FromZero;

public record ActivationProduct(string Id,string Kind,string Name,string Description,int Status,string PartialKey,long GraceMinutes,string Channel);
public record ActivationRegistryValue(string Name,string Value);
public record ActivationSnapshot(DateTimeOffset Checked,List<ActivationProduct> Products,List<ActivationRegistryValue> Registry,
 List<string> OfficeScripts,List<string> SubscriptionScripts,bool OfficeInstalled,int RemainingRearms,List<string> Warnings);
public record ActivationResult(string Method,int ExitCode,string Output,ActivationSnapshot? After,string? VerificationError);
public enum ActivationMethod { WindowsLicense,OfficeLicense,WindowsHwid,OfficeOhook,RemoveOhook,WindowsRearm }

public static class ActivationPolicy
{
 public const string WindowsApplicationId="55c92734-d682-4d71-983e-d6ec3f16059f";
 public const string EngineVersion="3.12";
 public const string EngineCommit="6401d2f36975dc07bcb6d22ea823a8498a75ec01";
 public const string EngineSha256="850F979665FB93999ACAE93F4790C1FF8ED2041532060B7966A121C2D29A0BFA";
 public const string EngineUrl="https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/"+EngineCommit+"/MAS/All-In-One-Version-KL/MAS_AIO.cmd";
 public static string Title(ActivationMethod method)=>method switch{
  ActivationMethod.WindowsLicense=>"Activate installed Windows license",
  ActivationMethod.OfficeLicense=>"Activate installed Office license",
  ActivationMethod.WindowsHwid=>"Windows HWID activation",
  ActivationMethod.OfficeOhook=>"Office / Microsoft 365 activation",
  ActivationMethod.RemoveOhook=>"Remove Office Ohook",
  ActivationMethod.WindowsRearm=>"Reset Windows grace period",
  _=>throw new InvalidDataException("Unknown activation method.")
 };
 public static string Status(int value)=>value switch{
  0=>"Unlicensed",1=>"Licensed",2=>"Initial grace period",3=>"Additional grace period",
  4=>"Non-genuine grace period",5=>"Notification",6=>"Extended grace period",_=>"Unknown"
 };
 public static bool UsesExternalEngine(ActivationMethod method)=>method is ActivationMethod.WindowsHwid or ActivationMethod.OfficeOhook or ActivationMethod.RemoveOhook;
 public static string EngineArguments(ActivationMethod method)=>method switch{
  ActivationMethod.WindowsHwid=>"/HWID /HWID-NoEditionChange -el",
  ActivationMethod.OfficeOhook=>"/Ohook -el",
  ActivationMethod.RemoveOhook=>"/Ohook-Uninstall -el",
  _=>throw new InvalidDataException("This method does not use the activation engine.")
 };
 public static string CleanOutput(string output)
 {
  if(output.Length>300_000)output=output[..300_000]+"\n[Output truncated]";
  output=Regex.Replace(output,@"\x1B\[[0-?]*[ -/]*[@-~]","",RegexOptions.None,TimeSpan.FromSeconds(1));
  output=Regex.Replace(output,@"\b[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}\b","[Product key hidden]",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
  return new string(output.Where(c=>!char.IsControl(c)||c is '\r' or '\n' or '\t').ToArray()).Trim();
 }
}
