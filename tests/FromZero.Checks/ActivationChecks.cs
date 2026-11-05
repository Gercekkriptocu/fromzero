using FromZero;

internal static class ActivationChecks
{
 public static async Task Run(Action<bool,string> check)
 {
  var product=new ActivationProduct(Guid.NewGuid().ToString(),"Windows","Windows","",1,"ABCDE",1440,"Retail");
  check(ActivationPolicy.Status(product.Status)=="Licensed"&&product.GraceMinutes==1440&&
   ActivationPolicy.Status(99)=="Unknown","Activation keeps grace values in minutes and handles unknown license states");
  var privateOutput="Key: ABCDE-FGHIJ-KLMNO-PQRST-UVWXY\n\u001b[31merror\u001b[0m\0";
  check(ActivationPolicy.CleanOutput(privateOutput)=="Key: [Product key hidden]\nerror",
   "Activation reports redact full product keys and strip console control sequences");
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
  var service=new ActivationService();
  var snapshot=await service.InspectAsync();
  check(snapshot.Checked<=DateTimeOffset.Now.AddMinutes(1)&&snapshot.Products.All(p=>p.PartialKey.Length<=5&&p.GraceMinutes>=0)&&
   snapshot.OfficeScripts.All(p=>Path.GetFileName(p).Equals("OSPP.VBS",StringComparison.OrdinalIgnoreCase)),
   "Real licensing inventory returns only key suffixes, minutes and locally detected Office scripts");
 }
}
