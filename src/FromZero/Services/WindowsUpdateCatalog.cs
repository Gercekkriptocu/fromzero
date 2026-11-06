using System.IO;
namespace FromZero;

public record WindowsOffer(string Id,int Revision,string Title,string Kind,string Manufacturer,string Model,string Class,string Eula,bool Hidden,bool Downloaded,bool Optional,bool Superseded,long Bytes,string Version,string Date,bool EulaAccepted=false,string HardwareId="",bool FeatureUpgrade=false,bool Interactive=false)
{
 public bool Firmware=>new UpdateInfo(Id,Revision,Title,Manufacturer,Model,Eula,Class).IsFirmware;
 public bool SelectableDriver=>!Firmware&&!Hidden&&!Superseded&&Kind=="Driver";
 public bool AutoEligible=>SelectableDriver&&!Optional;
 public bool QueueInstallable=>!Firmware&&!Hidden&&!Superseded&&!FeatureUpgrade&&
  (Kind=="Driver"||Kind=="Software"&&!Interactive&&!Optional&&!System.Text.RegularExpressions.Regex.IsMatch(Title,"Preview|Önizleme",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant));
 public string ReviewGroup=>Hidden||Superseded?"Archived":Firmware?"Manufacturer check required":Optional?"Optional":"Recommended";
 public string Key=>Id+":"+Revision;
}
public record WindowsScan(DateTimeOffset Checked,List<WindowsOffer> Offers,List<string> Warnings,bool Extended);
public record WindowsDownloadResult(string Id,int Revision,string Title,int Code,int HResult,bool Downloaded);
public static class WindowsUpdateCatalog
{
 public static IEnumerable<WindowsOffer> RecommendedDrivers(IEnumerable<WindowsOffer> offers,bool includeDownloaded=false)=>offers.Where(i=>i.AutoEligible&&(includeDownloaded||!i.Downloaded)).GroupBy(i=>!string.IsNullOrWhiteSpace(i.HardwareId)?i.HardwareId:string.IsNullOrWhiteSpace(i.Model)?i.Id:i.Manufacturer+"/"+i.Model+"/"+i.Class,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(i=>Version.TryParse(i.Version,out var v)?v:new Version()).ThenByDescending(i=>i.Date).First());
 public static IEnumerable<WindowsOffer> MissingUpdates(IEnumerable<WindowsOffer> offers)
 {
  var eligible=offers.Where(offer=>offer.QueueInstallable).ToArray();
  return eligible.Where(offer=>offer.Kind=="Software").Concat(RecommendedDrivers(eligible,true));
 }
 public static bool MatchesDevice(WindowsOffer offer,DeviceInfo device)=>offer.Kind=="Driver"&&device.Present&&
  !string.IsNullOrWhiteSpace(offer.HardwareId)&&device.HardwareIds.Concat(device.CompatibleIds??[]).Contains(offer.HardwareId,StringComparer.OrdinalIgnoreCase);
 internal static WindowsOffer? RefreshSelection(WindowsOffer selected,IEnumerable<WindowsOffer> current)=>current
  .Where(offer=>offer.QueueInstallable&&offer.Kind==selected.Kind&&offer.Revision>=selected.Revision&&
   offer.Id.Equals(selected.Id,StringComparison.OrdinalIgnoreCase)&&
   (offer.Kind!="Driver"||offer.HardwareId.Equals(selected.HardwareId,StringComparison.OrdinalIgnoreCase)))
  .OrderByDescending(offer=>offer.Revision).FirstOrDefault();
 public static string ScanScript(bool extended)=>"""
 $s=New-Object -ComObject Microsoft.Update.Session; $s.ClientApplicationID='FromZero';
 $searcher=$s.CreateUpdateSearcher(); $searcher.Online=$true;
 """+"$searcher.IncludePotentiallySupersededUpdates=$"+(extended?"true":"false")+"; "+"""
 $result=$searcher.Search(
 """+PackageSafety.QuotePs(extended?"(IsInstalled=0 and IsHidden=0) or (IsInstalled=0 and IsHidden=1)":"IsInstalled=0 and IsHidden=0")+"""
 );
 if([int]$result.ResultCode -notin @(2,3)){throw ('Windows Update tarama kodu: '+$result.ResultCode)};
 $warnings=@($result.Warnings | ForEach-Object { [string]$_.Message });
 if([int]$result.ResultCode -eq 3){$warnings+= 'Windows Update kısmi sonuç döndürdü; yeniden tarama gerekebilir.'};
 $items=@($result.Updates | ForEach-Object {
  $u=$_; $driver=([int]$u.Type -eq 2);
  [pscustomobject]@{Id=$u.Identity.UpdateID;Revision=$u.Identity.RevisionNumber;Title=$u.Title;
   Kind=if($driver){'Driver'}else{'Software'};Manufacturer=if($driver){[string]$u.DriverManufacturer}else{''};
   HardwareId=if($driver){[string]$u.DriverHardwareID}else{''};Model=if($driver){[string]$u.DriverModel}else{''};Class=if($driver){[string]$u.DriverClass}else{''};
   Eula=[string]$u.EulaText;EulaAccepted=[bool]$u.EulaAccepted;Hidden=[bool]$u.IsHidden;Downloaded=[bool]$u.IsDownloaded;Optional=[bool]$u.BrowseOnly;
   FeatureUpgrade=@($u.Categories | Where-Object {$_.CategoryID -eq '3689BDC8-B205-4AF4-8D4A-A63924C5E9D5'}).Count -gt 0;
   Interactive=[bool]$u.InstallationBehavior.CanRequestUserInput;
   Superseded=$false;Bytes=[long]$u.MaxDownloadSize;
   Version=if($driver -and $u.Title -match '(\d+\.\d+\.\d+(?:\.\d+)?)'){$Matches[1]}else{''};Date=if($driver -and $u.DriverVerDate){$u.DriverVerDate.ToString('yyyy-MM-dd')}else{''}
  }
 });
 $superseded=@{};foreach($u in $result.Updates){foreach($id in $u.SupersededUpdateIDs){$superseded[[string]$id]=$true}};
 foreach($i in $items){$i.Superseded=$superseded.ContainsKey($i.Id)};
 [pscustomobject]@{Checked=[DateTimeOffset]::Now.ToString('o');Offers=$items;Warnings=@($warnings);Extended=
 """+(extended?"$true":"$false")+" } | ConvertTo-Json -Depth 6";
 public static Task<WindowsScan> ScanAsync(bool extended,CancellationToken token=default)=>Commands.JsonAsync<WindowsScan>(ScanScript(extended),600,token);
 public static string DownloadScript(IEnumerable<WindowsOffer> selection,bool acceptLicense=true)
 {
  var items=selection.DistinctBy(o=>o.Key).ToList();if(items.Count==0||items.Count>200)throw new InvalidDataException("1–200 güncelleme seç.");
  foreach(var item in items)if(!Guid.TryParse(item.Id,out _)||item.Revision<0||item.Firmware||item.Hidden||item.Superseded)throw new InvalidDataException("Geçersiz veya otomatik indirmeye kapalı güncelleme.");
  var identities=string.Join(",",items.Select(i=>"@{Id='"+Guid.Parse(i.Id)+"';Revision="+i.Revision+"}"));
  return "$wanted=@("+identities+"); $allowLicense=$"+(acceptLicense?"true":"false")+"; "+"""
  $s=New-Object -ComObject Microsoft.Update.Session;$s.ClientApplicationID='FromZero';$searcher=$s.CreateUpdateSearcher();
  $report=@();foreach($item in $wanted){
   try {
   $r=$searcher.Search("IsInstalled=0 and IsHidden=0 and UpdateID='"+$item.Id+"' and RevisionNumber="+$item.Revision);
   if([int]$r.ResultCode -ne 2 -or $r.Updates.Count -ne 1){throw 'Paket artık sunulmuyor; yeniden tarayın.'};
   $u=$r.Updates.Item(0);
   if($u.Title -match 'Firmware|BIOS' -or ([int]$u.Type -eq 2 -and $u.DriverClass -eq 'Firmware')){throw 'Firmware indirmesi üreticinin akışında yapılmalı.'};
   if(-not $u.EulaAccepted){if(-not $allowLicense){throw 'Yeni lisans kullanıcı incelemesi gerektiriyor.'};$u.AcceptEula()};
   $c=New-Object -ComObject Microsoft.Update.UpdateColl;[void]$c.Add($u);$d=$s.CreateUpdateDownloader();$d.Updates=$c;
   $dr=$d.Download();$ir=$dr.GetUpdateResult(0);
   $report += [pscustomobject]@{Id=$item.Id;Revision=$item.Revision;Title=$u.Title;Code=[int]$ir.ResultCode;HResult=[int]$ir.HResult;Downloaded=[bool]$u.IsDownloaded};
   } catch { $report += [pscustomobject]@{Id=$item.Id;Revision=$item.Revision;Title=('İndirilemedi: '+$_.Exception.Message);Code=4;HResult=[int]$_.Exception.HResult;Downloaded=$false} }
  };ConvertTo-Json -Depth 4 -InputObject @($report)
  """;
 }
 public static async Task<List<WindowsDownloadResult>> DownloadAsync(IEnumerable<WindowsOffer> selection,bool acceptLicense=true)
 {
  var result=await Commands.ElevatedAsync(DownloadScript(selection,acceptLicense));if(!result.Success)throw new IOException(result.Output);
  var report=System.Text.Json.JsonSerializer.Deserialize<List<WindowsDownloadResult>>(result.Output.Trim().TrimStart('\uFEFF'),LocalStore.Json)??throw new InvalidDataException("İndirme sonucu okunamadı.");
  Journal.Write("Windows paketleri indirildi",string.Join("\n",report.Select(r=>r.Title+" / "+r.Code)),report.All(r=>r.Code==2));return report;
 }
}
