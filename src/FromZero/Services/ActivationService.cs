using System.IO;
using System.Diagnostics;

namespace FromZero;

public sealed class ActivationService
{
 internal static string InventoryScript=>OfficeDiscovery.Script+"\n"+InventoryBody;
 private const string InventoryBody="""
 $warnings=New-Object 'System.Collections.Generic.List[string]';
 $windowsId='55c92734-d682-4d71-983e-d6ec3f16059f';
 $products=@();
 try{
  $products+=@(Get-CimInstance SoftwareLicensingProduct -Filter 'PartialProductKey IS NOT NULL' | Where-Object {
   $_.ApplicationID -eq $windowsId -or $_.Name -like '*Office*'
  } | ForEach-Object {
   [pscustomobject]@{Id=[string]$_.ID;Kind=if($_.ApplicationID -eq $windowsId){'Windows'}else{'Office'};
    Name=[string]$_.Name;Description=[string]$_.Description;Status=[int]$_.LicenseStatus;
    PartialKey=[string]$_.PartialProductKey;GraceMinutes=[long]$_.GracePeriodRemaining;Channel=[string]$_.ProductKeyChannel;Addon=([bool]$_.LicenseDependsOn -or [bool]$_.LicenseIsAddon)}
  });
 }catch{$warnings.Add('Windows licensing inventory unavailable: '+$_.Exception.Message)};
 try{
  if(Get-CimClass OfficeSoftwareProtectionProduct -ErrorAction SilentlyContinue){
   $products+=@(Get-CimInstance OfficeSoftwareProtectionProduct -Filter 'PartialProductKey IS NOT NULL' | ForEach-Object {
    [pscustomobject]@{Id=[string]$_.ID;Kind='Office';Name=[string]$_.Name;Description=[string]$_.Description;
     Status=[int]$_.LicenseStatus;PartialKey=[string]$_.PartialProductKey;
     GraceMinutes=[long]$_.GracePeriodRemaining;Channel=[string]$_.ProductKeyChannel}
   });
  }
 }catch{$warnings.Add('Office licensing inventory unavailable: '+$_.Exception.Message)};
 $remaining=-1;
 try{$service=Get-CimInstance SoftwareLicensingService;$remaining=[int]$service.RemainingWindowsReArmCount}
 catch{$warnings.Add('Windows licensing service unavailable: '+$_.Exception.Message)};
 $registry=@();
 try{
  $key=Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform';
  foreach($name in @('KeyManagementServiceName','KeyManagementServicePort','VLActivationInterval','VLRenewalInterval')){
   if($null -ne $key.$name){$registry+=[pscustomobject]@{Name=$name;Value=[string]$key.$name}};
  }
 }catch{$warnings.Add('Licensing configuration unavailable: '+$_.Exception.Message)};
 $installations=@(Get-OfficeInstallations);
 $office=@($installations | ForEach-Object {$_.OsppScript} | Where-Object {$_} | Select-Object -Unique);
 $subscription=@($installations | ForEach-Object {$_.SubscriptionScript} | Where-Object {$_} | Select-Object -Unique);
 $installed=$installations.Count -gt 0 -or @($products | Where-Object {$_.Kind -eq 'Office'}).Count -gt 0;
 [pscustomobject]@{Checked=[DateTimeOffset]::Now.ToString('o');Products=@($products | Sort-Object Kind,Id -Unique);
  Registry=@($registry);OfficeScripts=@($office | Select-Object -Unique);SubscriptionScripts=@($subscription | Select-Object -Unique);
  OfficeInstalled=$installed;RemainingRearms=$remaining;Warnings=@($warnings);OfficeInstallations=@($installations)} | ConvertTo-Json -Depth 5
 """;

 private const string NativeHelpers="""
 function Assert-LocalFile([string]$path){
  if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw 'Required licensing component is missing.'};
  $item=Get-Item -LiteralPath $path;
  while($item){
   if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked licensing components are not supported.'};
   if($item -is [IO.FileInfo]){$item=$item.Directory}else{$item=$item.Parent}
  }
 }
 function Run-LicenseScript([string]$script,[string]$argument){
  Assert-LocalFile $script;
  $hostPath=Join-Path $env:SystemRoot 'System32\cscript.exe';
  Assert-LocalFile $hostPath;
  & $hostPath '//Nologo' $script $argument;
  if($LASTEXITCODE -ne 0){throw ('Licensing command failed: '+$LASTEXITCODE)};
 }
 """;

 public async Task<ActivationSnapshot> InspectAsync(CancellationToken token=default)
 {
  var snapshot=await Commands.JsonAsync<ActivationSnapshot>(InventoryScript,90,token);
  var subscriptions=new List<SubscriptionLicense>();
  foreach(var script in snapshot.SubscriptionScripts){
   try{
    var result=await ReadOfficeDetailsAsync(script,token);
    if(result.Success){
     var parsed=ActivationPolicy.ParseSubscriptions(result.Output);
     subscriptions.AddRange(parsed);
     if(parsed.Count==0)snapshot.Warnings.Add("No subscription license state was returned. Check Office Account for the final status.");
    }else snapshot.Warnings.Add("Subscription status could not be read.");
   }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or TimeoutException){
    snapshot.Warnings.Add("Subscription status could not be read: "+ex.Message);
   }
  }
  return snapshot with{Subscriptions=subscriptions.Distinct().ToList()};
 }

 internal static string OperationScript(ActivationMethod method)
 {
  _=ActivationPolicy.Title(method);
  if(ActivationPolicy.UsesExternalEngine(method))return EngineScript(method);
  if(method==ActivationMethod.OfficeLicense)return NativeHelpers+"\n"+OfficeDiscovery.Script+"\n"+"""
   $roots=@($env:ProgramW6432,$env:ProgramFiles,${env:ProgramFiles(x86)}) | Where-Object {$_} | Select-Object -Unique;
   $found=@(Get-OfficeInstallations | ForEach-Object {$_.OsppScript} | Where-Object {$_} | Select-Object -Unique);
   if($found.Count -eq 0){throw 'No volume-licensed Office activation script was found.'};
   foreach($script in $found){
    $protected=$false;
    foreach($root in $roots){if($script.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){$protected=$true}};
    if(-not $protected){throw 'The Office licensing script is outside protected installation folders.'};
    Run-LicenseScript $script '/act';
   }
   """;
  var argument=method==ActivationMethod.WindowsRearm?"/rearm":"/ato";
  return NativeHelpers+"\n"+@"Run-LicenseScript (Join-Path $env:SystemRoot 'System32\slmgr.vbs') "+PackageSafety.QuotePs(argument);
 }

 private static string EngineScript(ActivationMethod method)
 {
  return NativeHelpers+"\n"+"""
   if($env:PROCESSOR_ARCHITEW6432 -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64'){throw 'The integrated activation engine requires native Windows x64.'};
   $root=[IO.Path]::GetFullPath($env:ProgramW6432);
   $folder=Join-Path $root ('FromZero-Activation-'+[guid]::NewGuid().ToString('N'));
   if([IO.Path]::GetDirectoryName($folder) -ne $root){throw 'Invalid activation workspace.'};
   if(Test-Path -LiteralPath $folder){throw 'Activation workspace already exists.'};
   $script=Join-Path $folder 'MAS_AIO.cmd';
   try{
    [void](New-Item -ItemType Directory -Path $folder);
    $item=Get-Item -LiteralPath $folder;
    while($item){if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked activation workspace is not supported.'};$item=$item.Parent}
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12;
    $null=Invoke-WebRequest -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60 -Uri __SOURCE__ -OutFile $script;
    Assert-LocalFile $script;
    if((Get-Item -LiteralPath $script).Length -gt 2000000){throw 'Activation engine exceeds the size limit.'};
    if((Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash -ne __DIGEST__){throw 'Activation engine integrity verification failed.'};
    $start=New-Object Diagnostics.ProcessStartInfo;
    $start.FileName=Join-Path $env:SystemRoot 'System32\cmd.exe';
    Assert-LocalFile $start.FileName;
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.RedirectStandardInput=$true;
    $start.WorkingDirectory=$folder;
    $start.Arguments='/d /s /c ""'+$script+'" __ARGUMENTS__"';
    $p=New-Object Diagnostics.Process;
    $p.StartInfo=$start;
    try{
     $null=$p.Start();$p.StandardInput.Close();
     $stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync();
     $p.WaitForExit();
     $text=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult();
     if($text.Length -gt 300000){$text=$text.Substring(0,300000)};
     Write-Output $text;
     if($p.ExitCode -ne 0){throw ('Activation engine exited with code '+$p.ExitCode)};
    }finally{$p.Dispose()};
   }finally{
    if(Test-Path -LiteralPath $script){Remove-Item -LiteralPath $script -Force -ErrorAction SilentlyContinue};
    if(Test-Path -LiteralPath $folder){try{[IO.Directory]::Delete($folder)}catch{}};
   }
   """.Replace("__SOURCE__",PackageSafety.QuotePs(ActivationPolicy.EngineUrl))
    .Replace("__DIGEST__",PackageSafety.QuotePs(ActivationPolicy.EngineSha256))
    .Replace("__ARGUMENTS__",ActivationPolicy.EngineArguments(method));
 }

 public async Task<ActivationResult> ExecuteAsync(ActivationMethod method)
 {
  var before=await InspectAsync();
  if(method is ActivationMethod.OfficeOhook or ActivationMethod.RemoveOhook){
   foreach(var name in new[]{"WINWORD","EXCEL","POWERPNT","OUTLOOK","MSACCESS","ONENOTE","VISIO","WINPROJ","MSPUB","lync"}){
    var processes=Process.GetProcessesByName(name);
    var open=processes.Length>0;
    foreach(var process in processes)process.Dispose();
    if(open)throw new InvalidOperationException("Close Office applications before changing Ohook components.");
   }
  }
  var wrapped="""
   $events=New-Object 'System.Collections.Generic.List[string]';$code=0;
   try{& {
   """+"\n"+OperationScript(method)+"\n"+"""
   } | ForEach-Object {$events.Add([string]$_)}}
   catch{$code=1;$events.Add($_.Exception.Message)};
   [pscustomobject]@{ExitCode=$code;Output=($events -join [Environment]::NewLine)} | ConvertTo-Json -Depth 3
   """;
  var elevated=await Commands.ElevatedAsync(wrapped);
  var result=elevated.Success?
   System.Text.Json.JsonSerializer.Deserialize<CommandResult>(elevated.Output.Trim().TrimStart('\uFEFF'),LocalStore.Json)??throw new InvalidDataException("Activation returned an empty result."):
   elevated;
  ActivationSnapshot? after=null;string? failure=null;
  try{after=await InspectAsync();}
  catch(Exception ex)when(ex is InvalidOperationException or IOException or System.Text.Json.JsonException or TimeoutException){failure=ex.Message;}
  var verification=ActivationPolicy.Verify(method,result.ExitCode,before,after);
  if(verification==ActivationVerification.Unverified&&after!=null&&(method is ActivationMethod.WindowsLicense or ActivationMethod.WindowsHwid or ActivationMethod.OfficeLicense)){
   await Task.Delay(1500);
   try{
    after=await InspectAsync();
    verification=ActivationPolicy.Verify(method,result.ExitCode,before,after);
   }catch(Exception ex)when(ex is InvalidOperationException or IOException or System.Text.Json.JsonException or TimeoutException){failure=ex.Message;}
  }
  Journal.Write(ActivationPolicy.Title(method),"Execution result: "+result.ExitCode+
   "; verification: "+verification,result.Success);
  return new(ActivationPolicy.Title(method),result.ExitCode,ActivationPolicy.CleanOutput(result.Output),after,failure,verification);
 }

 public async Task<string> DetailsAsync(ActivationSnapshot snapshot)
 {
  var sections=new List<string>();
  var windows=Path.Combine(Environment.SystemDirectory,"slmgr.vbs");
  sections.Add((await Commands.PsAsync(NativeHelpers+"\nRun-LicenseScript "+PackageSafety.QuotePs(windows)+" '/dlv'")).Output);
  foreach(var script in snapshot.OfficeScripts.Concat(snapshot.SubscriptionScripts).Distinct(StringComparer.OrdinalIgnoreCase)){
   var result=await ReadOfficeDetailsAsync(script);
   sections.Add(Path.GetFileName(script)+"\n"+result.Output);
  }
  return ActivationPolicy.CleanOutput(string.Join("\n\n",sections));
 }
 private static Task<CommandResult> ReadOfficeDetailsAsync(string script,CancellationToken token=default)
 {
  var path=Path.GetFullPath(script);
  var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
   Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}.Where(p=>p.Length>0);
  if(!roots.Any(root=>path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
   throw new InvalidDataException("Unexpected Office licensing path.");
  PackageSafety.RejectReparsePoints(Path.GetDirectoryName(path)!,path);
  var name=Path.GetFileName(path);
  if(name.Equals("OSPP.VBS",StringComparison.OrdinalIgnoreCase))
   return Commands.PsAsync(NativeHelpers+"\nRun-LicenseScript "+PackageSafety.QuotePs(path)+" '/dstatusall'",90,token);
  if(name.Equals("vnextdiag.ps1",StringComparison.OrdinalIgnoreCase))
   return Commands.PsAsync("& "+PackageSafety.QuotePs(path)+" -action list",90,token);
  throw new InvalidDataException("Unknown Office licensing component.");
 }

}
