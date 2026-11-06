using FromZero;
using System.Text.Json;

internal static class WindowsQueueChecks
{
 public static async Task Run(string root,Action<bool,string> check)
 {
  var folder=Directory.CreateDirectory(Path.Combine(root,"queue-progress")).FullName;
  var snapshot=Path.Combine(folder,"progress.json");
  var audit=Path.Combine(folder,"audit.jsonl");
  var offer=new WindowsOffer(Guid.NewGuid().ToString(),1,"Windows fixture update","Software","","","", "",false,false,false,false,12,"","");
  var prefix="$fixtureId="+PackageSafety.QuotePs(offer.Id)+";"+"""
  $global:fixtureInstalled=$false;
  $global:fixtureUpdate=[pscustomobject]@{
   Title='Windows fixture update';Type=1;Identity=[pscustomobject]@{UpdateID=$fixtureId;RevisionNumber=1};
   InstallationBehavior=[pscustomobject]@{CanRequestUserInput=$false};
   BrowseOnly=$false;Categories=@();EulaAccepted=$true;IsDownloaded=$false
  };
  $item=[pscustomobject]@{ResultCode=2;HResult=0};
  $global:fixtureResult=[pscustomobject]@{ResultCode=2;RebootRequired=$false};
  $global:fixtureResult|Add-Member ScriptMethod GetUpdateResult {param($index);[pscustomobject]@{ResultCode=2;HResult=0}};
  $global:fixtureSearcher=[pscustomobject]@{Online=$false};
  $global:fixtureSearcher|Add-Member ScriptMethod Search {
   param($query);
   if($query.StartsWith('IsInstalled=1') -and !$global:fixtureInstalled){return [pscustomobject]@{ResultCode=2;Updates=@()}};
   [pscustomobject]@{ResultCode=2;Updates=@($global:fixtureUpdate)}
  };
  $global:fixtureDownloader=[pscustomobject]@{Updates=$null};
  $global:fixtureDownloader|Add-Member ScriptMethod Download {$global:fixtureUpdate.IsDownloaded=$true;$global:fixtureResult};
  $global:fixtureInstaller=[pscustomobject]@{IsBusy=$false;RebootRequiredBeforeInstallation=$false;Updates=$null;AllowSourcePrompts=$false;ForceQuiet=$false};
  $global:fixtureInstaller|Add-Member ScriptMethod Install {$global:fixtureInstalled=$true;$global:fixtureResult};
  $global:fixtureSession=[pscustomobject]@{ClientApplicationID=''};
  $global:fixtureSession|Add-Member ScriptMethod CreateUpdateSearcher {$global:fixtureSearcher};
  $global:fixtureSession|Add-Member ScriptMethod CreateUpdateDownloader {$global:fixtureDownloader};
  $global:fixtureSession|Add-Member ScriptMethod CreateUpdateInstaller {$global:fixtureInstaller};
  function New-Object {
   param($ComObject);
   if($ComObject -eq 'Microsoft.Update.Session'){return $global:fixtureSession};
   if($ComObject -eq 'Microsoft.Update.UpdateColl'){return ,([System.Collections.ArrayList]::new())};
   throw 'Unexpected COM request in fixture';
  };
  """;
  var script=PreparationWindows.SelectionScript([offer],"",snapshot)
   .Replace("try{$writer.Write($bytes,0,$bytes.Length)}finally{$writer.Dispose()};",
    "try{$writer.Write($bytes,0,$bytes.Length)}finally{$writer.Dispose()};[IO.File]::AppendAllText("+PackageSafety.QuotePs(audit)+",$snapshot+[Environment]::NewLine);",StringComparison.Ordinal);
  var result=await Commands.PsAsync(prefix+script);
  var report=result.Success?JsonSerializer.Deserialize<PreparationWindowsResult[]>(result.Output,LocalStore.Json):null;
  var events=File.Exists(audit)?File.ReadAllLines(audit).Select(line=>JsonSerializer.Deserialize<WindowsQueueSnapshot>(line,LocalStore.Json)!).ToArray():[];
  check(result.Success&&report is [{Installed:true,Code:2}]&&
   events.Select(item=>item.Phase).SequenceEqual(["windows-check","prepare","download","install","verify","between"])&&
   events[^1].Results is [{Installed:true,Code:2}],
   "The actual Windows queue script publishes ordered package phases and verified results using a COM fixture without system installation");
 }
}
