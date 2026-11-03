param([int]$Throttle=6,[int]$Timeout=18,[switch]$RetryFailed)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$entries=@(
 foreach($line in Get-Content -LiteralPath (Join-Path $projectRoot 'src/FromZero/Assets/manufacturers.txt')){if($line -and !$line.StartsWith('#')){$p=$line.Split('|');[pscustomobject]@{Kind='Support';Name=$p[1];Url=$p[2]}}}
 foreach($line in Get-Content -LiteralPath (Join-Path $projectRoot 'src/FromZero/Assets/manufacturer-tools.txt')){if($line -and !$line.StartsWith('#')){$p=$line.Split('|');[pscustomobject]@{Kind='Tool';Name=$p[1];Url=$p[2]}}}
)
$reportPath=Join-Path $projectRoot 'artifacts/manufacturer-page-verification.json'
$previous=@{};if($RetryFailed -and (Test-Path -LiteralPath $reportPath)){foreach($item in Get-Content -LiteralPath $reportPath -Raw|ConvertFrom-Json){if($item.Status -in @(200,206,403,429)){$previous[$item.Url]=$item}}}
$unique=@($entries|Group-Object Url|ForEach-Object {$_.Group[0]})
$results=@($unique|Where-Object {!$previous.ContainsKey($_.Url)}|ForEach-Object -ThrottleLimit $Throttle -Parallel {
 $entry=$_
 try{
  $handler=[Net.Http.HttpClientHandler]::new();$handler.UseCookies=$false;$handler.MaxAutomaticRedirections=8;$handler.AutomaticDecompression=[Net.DecompressionMethods]::All
  $client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds($using:Timeout);$client.DefaultRequestHeaders.UserAgent.ParseAdd('FromZero-source-verification/0.11')
  try{
   $response=$client.GetAsync($entry.Url).GetAwaiter().GetResult();$content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
   $title=[Net.WebUtility]::HtmlDecode([regex]::Match($content,'(?is)<title[^>]*>(.*?)</title>').Groups[1].Value).Trim()
   [pscustomobject]@{Kind=$entry.Kind;Name=$entry.Name;Url=$entry.Url;Status=[int]$response.StatusCode;FinalUrl=$response.RequestMessage.RequestUri.AbsoluteUri;Title=$title;Error='';Checked=[DateTimeOffset]::Now.ToString('o')}
  }finally{$client.Dispose()}
 }catch{[pscustomobject]@{Kind=$entry.Kind;Name=$entry.Name;Url=$entry.Url;Status=0;FinalUrl='';Title='';Error=$_.Exception.Message;Checked=[DateTimeOffset]::Now.ToString('o')}}
})
$results+=@($unique|Where-Object {$previous.ContainsKey($_.Url)}|ForEach-Object {$previous[$_.Url]})
$results|Sort-Object Name|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/manufacturer-page-verification.json') -Encoding utf8
$results|Group-Object Status|Select-Object Name,Count|Format-Table
$results|Where-Object {$_.Status -notin @(200,206,403,429) -or $_.Title -match '404|not found|Access Denied|Attention Required'}|Select-Object Name,Status,Url,Title,Error|ConvertTo-Json -Depth 3
Write-Output ('Checked '+$results.Count+' unique official page addresses; HTTP availability is separate from model compatibility and binary verification.')
