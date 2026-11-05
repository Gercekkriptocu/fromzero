using FromZero;

internal static class ActivationChecks
{
 public static async Task Run(string root,Action<bool,string> check)
 {
  var product=new ActivationProduct(Guid.NewGuid().ToString(),"Windows","Windows","",1,"ABCDE",1440,"Retail");
  check(ActivationPolicy.Status(product.Status)=="Licensed"&&product.GraceMinutes==1440&&
   ActivationPolicy.Status(99)=="Unknown","Activation keeps grace values in minutes and handles unknown license states");
  var privateOutput="Key: ABCDE-FGHIJ-KLMNO-PQRST-UVWXY\n\u001b[31merror\u001b[0m\0";
  check(ActivationPolicy.CleanOutput(privateOutput)=="Key: [Product key hidden]\nerror",
   "Activation reports redact full product keys and strip console control sequences");
  ActivationSnapshot Snapshot(params ActivationProduct[] products)=>new(DateTimeOffset.Now,products.ToList(),[],[],[],false,0,[]);
  var inactive=product with{Status=0};
  check(ActivationPolicy.Verify(ActivationMethod.WindowsHwid,0,Snapshot(inactive),Snapshot(inactive))==ActivationVerification.Unverified&&
   ActivationPolicy.Verify(ActivationMethod.WindowsHwid,0,Snapshot(inactive),null)==ActivationVerification.Unverified,
   "A zero command exit code cannot claim activation without a licensed provider result");
  check(ActivationPolicy.Verify(ActivationMethod.WindowsHwid,0,Snapshot(inactive),Snapshot(product))==ActivationVerification.Verified&&
   ActivationPolicy.Verify(ActivationMethod.WindowsHwid,0,Snapshot(product),Snapshot(product))==ActivationVerification.AlreadyLicensed&&
   ActivationPolicy.Verify(ActivationMethod.WindowsHwid,1,Snapshot(inactive),Snapshot(product))==ActivationVerification.Failed,
   "Verification distinguishes newly licensed, pre-existing licenses and command failures");
  check(ActivationPolicy.Verify(ActivationMethod.WindowsHwid,0,Snapshot(),Snapshot(product with{Addon=true}))==ActivationVerification.Unverified&&
   ActivationPolicy.Verify(ActivationMethod.OfficeOhook,0,Snapshot(),Snapshot(product with{Kind="Office"}))==ActivationVerification.ManualCheck,
   "Windows add-on licenses and Office hook execution cannot masquerade as verified base activation");
  var subscriptions=ActivationPolicy.ParseSubscriptions("Product: O365ProPlusRetail\nLicense State: Licensed\nEmail: private@example.com\nProduct: VisioProRetail\nLicenseState: Unlicensed");
  check(subscriptions.SequenceEqual([new SubscriptionLicense("O365ProPlusRetail","Licensed"),new("VisioProRetail","Unlicensed")])&&
   ActivationPolicy.ParseSubscriptions("Office is permanently activated.").Count==0,
   "Microsoft 365 status parsing requires product and exact state fields and drops account identifiers");
  check(ActivationPolicy.OfficeProductName("ProPlus2024Volume")=="Office LTSC 2024"&&
   ActivationPolicy.OfficeProductName("O365BusinessRetail")=="Microsoft 365"&&
   ActivationPolicy.OfficeProductName("VisioPro2021Volume")=="Visio LTSC 2021",
   "Modern Office product identities retain suite, subscription and standalone distinctions");
  bool rejected=false;
  try{ActivationPolicy.EngineArguments(ActivationMethod.WindowsRearm);}catch(InvalidDataException){rejected=true;}
  check(rejected&&ActivationPolicy.EngineArguments(ActivationMethod.WindowsHwid)=="/HWID /HWID-NoEditionChange -el",
   "Only fixed external-engine methods are accepted and HWID keeps the selected Windows edition");
  check(WorldStations.ForPage("activation").Page=="history"&&Locale.Translate("Activator",true)=="Aktivatör"&&
   Locale.Translate("Reset Windows grace period",true)=="Windows lisans süresini yeniden hazırla",
   "Activation participates in the existing world and language system");
  var scripts=new[]{ActivationService.InventoryScript}.Concat(Enum.GetValues<ActivationMethod>().Select(ActivationService.OperationScript)).ToArray();
  var errors=new List<string>();
  foreach(var script in scripts){
   var found=await Commands.JsonAsync<string[]>("$t=$null;$e=$null;[void][Management.Automation.Language.Parser]::ParseInput("+PackageSafety.QuotePs(script)+",[ref]$t,[ref]$e);ConvertTo-Json -InputObject @($e|ForEach-Object Message)");
   errors.AddRange(found);
  }
  check(errors.Count==0,"Windows parses every activation command without executing activation or rearm");
  var office64=Path.Combine(root,"OfficeFixture64","Microsoft Office");
  var office32=Path.Combine(root,"OfficeFixture32","Microsoft Office");
  foreach(var folder in new[]{office64,office32})Directory.CreateDirectory(Path.Combine(folder,"root","Office16"));
  File.WriteAllText(Path.Combine(office64,"root","Office16","EXCEL.EXE"),"fixture");
  File.WriteAllText(Path.Combine(office32,"root","Office16","VISIO.EXE"),"fixture");
  var mock="""
   function Test-Path {
    param([string]$LiteralPath,[string]$PathType)
    if($LiteralPath.StartsWith('HKLM:')){return $LiteralPath -in @('HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Office\ClickToRun\Configuration')};
    Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
   }
   function Get-ItemProperty {
    param([string]$LiteralPath)
    if($LiteralPath.Contains('WOW6432Node')){[pscustomobject]@{InstallationPath=__OFFICE32__;VersionToReport='16.0.17932';Platform='x86';ProductReleaseIds='VisioPro2024Volume'}}
    else{[pscustomobject]@{InstallationPath=__OFFICE64__;VersionToReport='16.0.17932';Platform='x64';ProductReleaseIds='O365ProPlusRetail'}}
   }
   function Get-AppxPackage {[CmdletBinding()]param([string]$Name)}
   $env:ProgramW6432=__ROOT64__;$env:ProgramFiles=__ROOT64__;${env:ProgramFiles(x86)}=__ROOT32__;
   """
   .Replace("__OFFICE32__",PackageSafety.QuotePs(office32)).Replace("__OFFICE64__",PackageSafety.QuotePs(office64))
   .Replace("__ROOT32__",PackageSafety.QuotePs(Path.GetDirectoryName(office32)!)).Replace("__ROOT64__",PackageSafety.QuotePs(Path.GetDirectoryName(office64)!));
  var detected=await Commands.JsonAsync<OfficeInstallation[]>(mock+"\n"+OfficeDiscovery.Script+"\nConvertTo-Json -Depth 5 -InputObject @(Get-OfficeInstallations)");
  check(detected.Length==2&&detected.Any(o=>o.Products.Contains("O365ProPlusRetail")&&o.Applications.SequenceEqual(["EXCEL.EXE"]))&&
   detected.Any(o=>o.Architecture=="x86"&&o.Applications.SequenceEqual(["VISIO.EXE"])),
   "Office discovery handles current x64 Microsoft 365 and standalone x86 Visio without Word");
  var service=new ActivationService();
  var snapshot=await service.InspectAsync();
  check(snapshot.Checked<=DateTimeOffset.Now.AddMinutes(1)&&snapshot.Products.All(p=>p.PartialKey.Length<=5&&p.GraceMinutes>=0)&&
   snapshot.OfficeScripts.All(p=>Path.GetFileName(p).Equals("OSPP.VBS",StringComparison.OrdinalIgnoreCase)),
   "Real licensing inventory returns only key suffixes, minutes and locally detected Office scripts");
 }
}
