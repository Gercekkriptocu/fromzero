using System.IO;
using System.Text.Json;
namespace FromZero;
public record PreparationWindowsResult(string Title,int Code,int HResult,bool Reboot,bool Installed,string Key="",bool DriverCacheReady=false,bool NoLongerOffered=false);
public static class PreparationWindows
{
 public static string Script(bool drivers,string stopFile="")=>BuildScript(drivers,stopFile,"");
 public static string SelectionScript(IEnumerable<WindowsOffer> offers,string stopFile="")
 {
  var items=offers.DistinctBy(o=>o.Key).ToArray();
  if(items.Length==0||items.Length>100||items.Any(o=>!o.QueueInstallable||!Guid.TryParse(o.Id,out _)||o.Revision<0))
   throw new InvalidDataException("Invalid or unsupported Windows update selection.");
  var identities=string.Join(",",items.Select(o=>"@{Id='"+Guid.Parse(o.Id)+"';Key="+PackageSafety.QuotePs(o.Key)+
   ";Revision="+o.Revision+";Title="+PackageSafety.QuotePs(o.Title)+";Kind="+PackageSafety.QuotePs(o.Kind)+
   ";Hardware="+PackageSafety.QuotePs(o.HardwareId)+"}"));
  return BuildScript(false,stopFile,identities);
 }
 private static string BuildScript(bool drivers,string stopFile,string identities)=>"$stopFile="+PackageSafety.QuotePs(stopFile)+";$progressFile="+PackageSafety.QuotePs(identities.Length==0&&stopFile.Length>0?Path.Combine(Path.GetDirectoryName(stopFile)!,"windows-progress.txt"):"")+";$wanted=@("+identities+");"+"""
 $session=New-Object -ComObject Microsoft.Update.Session;$session.ClientApplicationID='FromZero preparation';
 $installer=$session.CreateUpdateInstaller();
 if($installer.IsBusy){throw 'Başka bir Windows kurulumu sürüyor. Tamamlandıktan sonra tekrar dene.'};
 if($installer.RebootRequiredBeforeInstallation){ConvertTo-Json -InputObject @([pscustomobject]@{Title='Önceki Windows işlemi yeniden başlatma bekliyor';Code=2;HResult=0;Reboot=$true;Installed=$false});return};
 $searcher=$session.CreateUpdateSearcher();$searcher.Online=$true;
 """+"$drivers=$"+(drivers?"true":"false")+"; $type='"+(drivers?"Driver":"Software")+"';"+"""
 $report=@();
 if($wanted.Count){
  $current=$searcher.Search('IsInstalled=0 and IsHidden=0');
  if([int]$current.ResultCode -ne 2){throw ('Windows taraması tam sonuç vermedi: '+$current.ResultCode)};
  $byId=@{};foreach($u in $current.Updates){$byId[[string]$u.Identity.UpdateID]=$u};
  $ordered=$wanted;
 }else{
  $result=$searcher.Search("IsInstalled=0 and IsHidden=0 and Type='"+$type+"'");
  if([int]$result.ResultCode -ne 2){throw ('Windows taraması tam sonuç vermedi: '+$result.ResultCode)};
  $ordered=@($result.Updates|Sort-Object @{Expression={if($drivers -and $_.DriverClass -eq 'Net'){0}elseif($drivers -and $_.DriverClass -eq 'System'){1}elseif($drivers -and $_.DriverClass -eq 'Display'){2}else{3}}});
 };
 foreach($candidate in $ordered){
  if($stopFile -and (Test-Path -LiteralPath $stopFile)){$report+=[pscustomobject]@{Title='Durdurma istendi';Code=5;HResult=0;Reboot=$false;Installed=$false};break};
  $key='';$title=[string]$candidate.Title;$driverCacheReady=$false;
  try{
   if($wanted.Count){
    $key=$candidate.Key;$update=$byId[$candidate.Id];
    if(!$update -or [int]$update.Identity.RevisionNumber -ne $candidate.Revision -or
       (([int]$update.Type -eq 2) -ne ($candidate.Kind -eq 'Driver')) -or
       ($candidate.Kind -eq 'Driver' -and [string]$update.DriverHardwareID -ine $candidate.Hardware)){
     $report+=[pscustomobject]@{Title=$title;Key=$key;Code=0;HResult=0;Reboot=$false;Installed=$false;NoLongerOffered=$true};continue;
    };
    $title=$update.Title;
   }else{$update=$candidate};
   $isDriver=[int]$update.Type -eq 2;
   $firmware=$update.Title -match 'Firmware|BIOS' -or ($isDriver -and $update.DriverClass -eq 'Firmware');
   $upgrade=@($update.Categories|Where-Object {$_.CategoryID -eq '3689BDC8-B205-4AF4-8D4A-A63924C5E9D5'}).Count -gt 0;
   if($firmware -or $upgrade -or (!$isDriver -and ($update.InstallationBehavior.CanRequestUserInput -or $update.BrowseOnly -or $update.Title -match 'Preview|Önizleme'))){
    if($wanted.Count){throw 'Paket otomatik kuruluma uygun değil.'};continue;
   };
   if($progressFile){Set-Content -LiteralPath $progressFile -Encoding UTF8 -Value ('İndiriliyor: '+$update.Title)};
   if(!$update.EulaAccepted){$update.AcceptEula()};
   $collection=New-Object -ComObject Microsoft.Update.UpdateColl;[void]$collection.Add($update);
   if(!$update.IsDownloaded){
    $downloader=$session.CreateUpdateDownloader();$downloader.Updates=$collection;$download=$downloader.Download();
    if([int]$download.ResultCode -ne 2 -or !$update.IsDownloaded){
     $failedDownload=$download.GetUpdateResult(0);
     $report+=[pscustomobject]@{Title=$title;Key=$key;Code=[int]$failedDownload.ResultCode;HResult=[int]$failedDownload.HResult;Reboot=$false;Installed=$false};continue;
    };
   };
   $driverCacheReady=$isDriver;
   $installer=$session.CreateUpdateInstaller();$installer.Updates=$collection;$installer.AllowSourcePrompts=$false;$installer.ForceQuiet=$true;
   if($progressFile){Set-Content -LiteralPath $progressFile -Encoding UTF8 -Value ('Kuruluyor: '+$update.Title)};
   $resultInstall=$installer.Install();$item=$resultInstall.GetUpdateResult(0);
   $installed=$false;try{$verify=$searcher.Search("IsInstalled=1 and UpdateID='"+$update.Identity.UpdateID+"'");$installed=$verify.Updates.Count -gt 0}catch{};
   $restart=[bool]$resultInstall.RebootRequired;
   $report+=[pscustomobject]@{Title=$title;Key=$key;Code=[int]$item.ResultCode;HResult=[int]$item.HResult;Reboot=$restart;Installed=$installed;DriverCacheReady=$driverCacheReady};
   if($restart){break};
  }catch{$report+=[pscustomobject]@{Title=($title+' / '+$_.Exception.Message);Key=$key;Code=4;HResult=[int]$_.Exception.HResult;Reboot=$false;Installed=$false;DriverCacheReady=$driverCacheReady}};
 };ConvertTo-Json -Depth 4 -InputObject @($report)
 """;
 public static async Task<PreparationOutcome> ApplyAsync(bool drivers,IProgress<string> progress,CancellationToken token,string stopFile="")
 {
  List<string> history=[];
  for(var round=0;round<3;round++){
  token.ThrowIfCancellationRequested();if(stopFile.Length>0&&File.Exists(stopFile))throw new OperationCanceledException();
  progress.Report(drivers?"Windows'un bu donanıma sunduğu sürücüler indiriliyor ve kuruluyor…":"Windows güvenlik / kararlılık güncellemeleri indiriliyor ve kuruluyor…");
  // Running update installation is deliberately not killed on UI cancellation.
  var result=await Commands.InstallPsAsync(Script(drivers,stopFile));if(!result.Success)throw new IOException(result.Output);
  var text=result.Output.Trim().TrimStart('\uFEFF');List<PreparationWindowsResult> rows=text.StartsWith('[')?JsonSerializer.Deserialize<List<PreparationWindowsResult>>(text,LocalStore.Json)??[]:[JsonSerializer.Deserialize<PreparationWindowsResult>(text,LocalStore.Json)??throw new InvalidDataException("Windows kurulum sonucu eksik.")];
  if(rows.Any(r=>r.Code==5))throw new OperationCanceledException("Mevcut Windows paketi tamamlandı; kuyruk durduruldu.");
  var details=rows.Count==0?"Bu taramada kurulabilir yeni paket sunulmadı.":string.Join("\n",rows.Select(r=>r.Title+" • "+(r.Code==2&&(r.Installed||r.Reboot)?"Kuruldu / yeniden başlatma sonrası kontrol":"Kontrol gerekli")+" • Kod "+r.Code+" / 0x"+unchecked((uint)r.HResult).ToString("X8")));
  Journal.Write("Otomatik Windows kurulumu",details,rows.All(r=>r.Code==2));
  history.Add(details);
  if(rows.Count==0)return new("done",string.Join("\n",history));
  if(rows.Any(r=>r.Reboot))return new("done",string.Join("\n",history),true);
  if(rows.Any(r=>r.Code!=2||!r.Installed))return new("attention",string.Join("\n",history));
  }
  return new("attention",string.Join("\n",history)+"\nÜç tarama turunda yeni paketler kuruldu. Yeni uygun paketler için Devam et ile tekrar kontrol et.");
 }
}
