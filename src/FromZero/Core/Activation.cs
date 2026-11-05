using System.IO;
using System.Text.RegularExpressions;

namespace FromZero;

public record ActivationProduct(string Id,string Kind,string Name,string Description,int Status,string PartialKey,long GraceMinutes,string Channel,bool Addon=false);
public record OfficeInstallation(string Path,string Version,string Architecture,string[] Products,string Source,string OsppScript,string SubscriptionScript,string[] Applications);
public record SubscriptionLicense(string Product,string State);
public record ActivationRegistryValue(string Name,string Value);
public record ActivationSnapshot(DateTimeOffset Checked,List<ActivationProduct> Products,List<ActivationRegistryValue> Registry,
 List<string> OfficeScripts,List<string> SubscriptionScripts,bool OfficeInstalled,int RemainingRearms,List<string> Warnings,
 List<OfficeInstallation>? OfficeInstallations=null,List<SubscriptionLicense>? Subscriptions=null);
public record ActivationResult(string Method,int ExitCode,string Output,ActivationSnapshot? After,string? VerificationError,
 ActivationVerification Verification=ActivationVerification.Unverified);
public enum ActivationVerification { Verified,AlreadyLicensed,PartiallyVerified,Unverified,Failed,ManualCheck }
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
 public static ActivationVerification Verify(ActivationMethod method,int exitCode,ActivationSnapshot? before,ActivationSnapshot? after)
 {
  if(exitCode!=0)return ActivationVerification.Failed;
  if(after==null)return ActivationVerification.Unverified;
  if(method is ActivationMethod.OfficeOhook or ActivationMethod.RemoveOhook or ActivationMethod.WindowsRearm)return ActivationVerification.ManualCheck;
  var kind=method==ActivationMethod.OfficeLicense?"Office":"Windows";
  var products=after.Products.Where(p=>p.Kind==kind&&!p.Addon).ToArray();
  if(products.Length==0||products.All(p=>p.Status!=1))return ActivationVerification.Unverified;
  if(products.Any(p=>p.Status!=1))return ActivationVerification.PartiallyVerified;
  var prior=before?.Products.Where(p=>p.Kind==kind&&!p.Addon).ToArray()??[];
  return products.All(p=>prior.Any(old=>old.Id==p.Id&&old.Status==1))?
   ActivationVerification.AlreadyLicensed:ActivationVerification.Verified;
 }
 public static string VerificationMessage(ActivationVerification result)=>result switch{
  ActivationVerification.Verified=>"Activation verified by the licensing provider.",
  ActivationVerification.AlreadyLicensed=>"The detected license was already active before this operation.",
  ActivationVerification.PartiallyVerified=>"Some products are licensed. Other detected products still need attention.",
  ActivationVerification.ManualCheck=>"The command finished. Verify the result in the product's Account or Activation screen.",
  ActivationVerification.Failed=>"The activation command reported a failure.",
  _=>"Activation could not be verified. Review the license status and command output."
 };
 public static List<SubscriptionLicense> ParseSubscriptions(string output)
 {
  if(output.Length>300_000)return [];
  var licenses=new List<SubscriptionLicense>();
  string product="",state="";
  void Add(){if(product.Length>0&&state.Length>0)licenses.Add(new(product,state));product="";state="";}
  foreach(var line in output.Replace("\r","").Split('\n')){
   var match=Regex.Match(line,@"^\s*(Product|License\s*State|LicenseState)\s*:\s*(.{1,150}?)\s*$",
    RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
   if(!match.Success)continue;
   if(match.Groups[1].Value.Equals("Product",StringComparison.OrdinalIgnoreCase)){
    if(product.Length>0)Add();
    product=match.Groups[2].Value;
   }else state=match.Groups[2].Value;
   if(product.Length>0&&state.Length>0)Add();
  }
  return licenses.Distinct().ToList();
 }
 public static string OfficeProductName(string product)
 {
  if(product.StartsWith("O365",StringComparison.OrdinalIgnoreCase))return "Microsoft 365";
  var app=product.StartsWith("Visio",StringComparison.OrdinalIgnoreCase)?"Visio":
   product.StartsWith("Project",StringComparison.OrdinalIgnoreCase)?"Project":"Office";
  foreach(var year in new[]{"2024","2021","2019","2016"})
   if(product.Contains(year,StringComparison.Ordinal))return app+(product.EndsWith("Volume",StringComparison.OrdinalIgnoreCase)&&(year is "2024" or "2021")?" LTSC ":" ")+year;
  return product;
 }
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
