using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FromZero;

public record CatalogDriverCandidate(string Id,string Title,string Version,string Date,string Architecture,string HardwareId,
 string DetailsUrl,string Products="",string[]? SupportedHardwareIds=null);
public record CatalogDriverDownload(string Folder,string[] InfFiles,string[] Sources,string Sha256);

/// <summary>Explicit hardware searches against the public Microsoft Update Catalog.</summary>
public static class MicrosoftDriverCatalog
{
 private const string CatalogHost="www.catalog.update.microsoft.com";
 private const long MaximumCabBytes=512L*1024*1024;
 private static readonly TimeSpan RegexTimeout=TimeSpan.FromSeconds(2);
 private const RegexOptions IgnoreCase=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant;

 public static async Task<IReadOnlyList<CatalogDriverCandidate>> SearchAsync(DeviceInfo device,IProgress<string>? progress=null,CancellationToken token=default)
 {
  if(!device.Present)throw new InvalidDataException("The device is no longer connected.");
  var ids=device.HardwareIds.Where(ValidHardwareId).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
  if(ids.Length==0)throw new InvalidDataException("No supported hardware ID is available for this device.");
  using var client=Client();
  var result=new List<CatalogDriverCandidate>();
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var inspected=0;
  foreach(var id in ids)
  {
   token.ThrowIfCancellationRequested();
   progress?.Report("Searching Microsoft Update Catalog · "+id);
   var uri=new Uri("https://"+CatalogHost+"/Search.aspx?q="+Uri.EscapeDataString(id));
   var html=await ReadMetadataAsync(client,uri,null,token);
   var rows=ParseSearch(html,id);
   foreach(var candidate in rows.Take(12))
   {
    if(!seen.Add(candidate.Id))continue;
    if(++inspected>24)return Order(result);
    progress?.Report("Checking package hardware IDs · "+candidate.Title);
    var details=await ReadMetadataAsync(client,new Uri(candidate.DetailsUrl),null,token);
    var verified=ParseDetails(candidate,details,device.HardwareIds);
    if(verified!=null)result.Add(verified);
   }
   // A more specific ID has precedence over broader alternatives provided by Windows.
   if(result.Count>0)break;
  }
  return Order(result);
 }

 private static IReadOnlyList<CatalogDriverCandidate> Order(IEnumerable<CatalogDriverCandidate> records)=>records
  .OrderByDescending(record=>Version.TryParse(record.Version,out var version)?version:new Version())
  .ThenByDescending(record=>DateTime.TryParse(record.Date,CultureInfo.GetCultureInfo("en-US"),DateTimeStyles.None,out var date)?date:DateTime.MinValue)
  .ToArray();

 internal static bool ValidHardwareId(string id)=>id.Length is >3 and <=250&&Regex.IsMatch(id,
  @"^(?:PCI|USB|HID|ACPI|HDAUDIO|BTH|SD|ROOT|SWD)\\[A-Z0-9_&.\-\\]+$",IgnoreCase,RegexTimeout);

 internal static List<CatalogDriverCandidate> ParseSearch(string html,string hardwareId)
 {
  if(html.Length>4_000_000)throw new InvalidDataException("The driver search response exceeds the size limit.");
  var rows=new List<CatalogDriverCandidate>();
  foreach(Match row in Regex.Matches(html,"<tr\\b[^>]*id=[\"'](?<id>[a-f0-9-]{36})_R\\d+[\"'][^>]*>(?<body>[\\s\\S]*?)</tr>",IgnoreCase,RegexTimeout))
  {
   if(!Guid.TryParse(row.Groups["id"].Value,out var id))continue;
   var cells=Regex.Matches(row.Groups["body"].Value,@"<td\b[^>]*>([\s\S]*?)</td>",IgnoreCase,RegexTimeout)
    .Select(cell=>Text(cell.Groups[1].Value)).ToArray();
   if(cells.Length<6||!cells[3].StartsWith("Drivers",StringComparison.OrdinalIgnoreCase))continue;
   if(Regex.IsMatch(cells[1]+" "+cells[3],"Firmware|BIOS",IgnoreCase,RegexTimeout))continue;
   rows.Add(new(id.ToString(),cells[1],cells[5],cells[4],"Unverified",hardwareId,
    "https://"+CatalogHost+"/ScopedViewInline.aspx?updateid="+id,cells[2]));
  }
  if(rows.Count==0&&!html.Contains("ctl00_catalogBody_noResultText",StringComparison.Ordinal)&&
     !html.Contains("We did not find any results",StringComparison.OrdinalIgnoreCase)&&!html.Contains("No results",StringComparison.OrdinalIgnoreCase))
  {
   // A changed layout or server error must not be reported as a successful empty search.
   if(!html.Contains("headerRow",StringComparison.Ordinal))throw new InvalidDataException("Microsoft Catalog returned an unsupported search response.");
  }
  return rows;
 }

 internal static CatalogDriverCandidate? ParseDetails(CatalogDriverCandidate candidate,string html,IEnumerable<string> deviceIds)
 {
  var architectureBlock=Element(html,"div","archDiv");
  if(architectureBlock.Length==0)throw new InvalidDataException("The package architecture could not be verified.");
  var architecture=Text(architectureBlock);
  var expected=RuntimeInformation.OSArchitecture switch{Architecture.X64=>"AMD64",Architecture.Arm64=>"ARM64",_=>"X86"};
  if(!Regex.IsMatch(architecture,@"\b"+expected+@"\b",IgnoreCase,RegexTimeout))return null;
  var decoded=WebUtility.HtmlDecode(html);
  var supported=Regex.Matches(decoded,@"(?:PCI|USB|HID|ACPI|HDAUDIO|BTH|SD|ROOT|SWD)\\[A-Z0-9_&.\-\\]+",IgnoreCase,RegexTimeout)
   .Select(match=>match.Value.TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  var match=deviceIds.FirstOrDefault(id=>supported.Contains(id,StringComparer.OrdinalIgnoreCase));
  if(match==null)return null;
  if(Regex.IsMatch(Text(Element(html,"div","classificationDiv")),"Firmware",IgnoreCase,RegexTimeout))return null;
  return candidate with{Architecture=expected,HardwareId=match,SupportedHardwareIds=supported};
 }

 private static string Element(string html,string tag,string id)=>Regex.Match(html,"<"+tag+"\\b[^>]*id=[\"']"+
  Regex.Escape(id)+"[\"'][^>]*>([\\s\\S]*?)</"+tag+">",IgnoreCase,RegexTimeout).Groups[1].Value;
 private static string Text(string html)=>Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html,"<[^>]+>"," ",RegexOptions.None,RegexTimeout)),@"\s+"," ").Trim();

 public static async Task<CatalogDriverDownload> DownloadAsync(CatalogDriverCandidate candidate,IProgress<string>? progress=null,CancellationToken token=default)
 {
  if(!Guid.TryParse(candidate.Id,out var id)||!ValidHardwareId(candidate.HardwareId))throw new InvalidDataException("Invalid driver candidate.");
  using var client=Client();
  var selection=JsonSerializer.Serialize(new[]{new{size=0,updateID=id.ToString(),uidInfo=id.ToString()}});
  using var form=new FormUrlEncodedContent(new Dictionary<string,string>{{"updateIDs",selection}});
  progress?.Report("Resolving Microsoft driver package…");
  var dialog=await ReadMetadataAsync(client,new Uri("https://"+CatalogHost+"/DownloadDialog.aspx"),form,token);
  var packages=ParseDownloads(dialog,id);
  var root=LocalStore.FilePath("CatalogDrivers");
  Directory.CreateDirectory(root);
  PackageSafety.RejectReparsePoints(root,root);
  var folder=Path.Combine(root,id.ToString("N")+"-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  var sources=new List<string>();
  var hashes=new List<string>();
  try
  {
   long total=0;
   long expandedTotal=0;
   for(var index=0;index<packages.Count;index++)
   {
    var package=packages[index];
    var cab=Path.Combine(folder,"package-"+index+".cab");
    progress?.Report("Downloading driver package "+(index+1)+" / "+packages.Count);
    total+=await DownloadCabAsync(client,package.Url,cab,package.Hash,progress,token);
    if(total>1024L*1024*1024)throw new InvalidDataException("The driver download exceeds the total size limit.");
    var paths=ValidateCabinet(cab,out var expandedBytes);
    expandedTotal+=expandedBytes;
    if(expandedTotal>2L*1024*1024*1024)throw new InvalidDataException("The driver package expands beyond the total size limit.");
    var destination=Path.Combine(folder,"contents-"+index);
    Directory.CreateDirectory(destination);
    foreach(var path in paths)PackageSafety.ResolveInside(destination,path);
    var expand=Path.Combine(Environment.SystemDirectory,"expand.exe");
    progress?.Report("Extracting Microsoft driver package…");
    var extracted=await Commands.RunAsync(expand,[cab,"-F:*",destination],180,token);
    if(!extracted.Success)throw new IOException("Windows could not extract the driver cabinet. "+extracted.Output);
    var actual=PackageSafety.EnumerateSafeFiles(destination).Select(file=>Path.GetRelativePath(destination,file)).ToArray();
    if(actual.Any(path=>!paths.Contains(path,StringComparer.OrdinalIgnoreCase)))throw new InvalidDataException("Unexpected file was extracted from the driver cabinet.");
    sources.Add(package.Url.AbsoluteUri);
    hashes.Add(Convert.ToHexString(package.Hash));
   }
   var files=PackageSafety.EnumerateSafeFiles(folder).Where(file=>Path.GetExtension(file).Equals(".inf",StringComparison.OrdinalIgnoreCase)).ToArray();
   if(files.Length==0)throw new InvalidDataException("No driver INF was found in the package.");
   await File.WriteAllTextAsync(Path.Combine(folder,"package-source.json"),JsonSerializer.Serialize(new{
    candidate.Id,candidate.Title,candidate.HardwareId,Sources=sources,Sha256=hashes,Downloaded=DateTimeOffset.Now},LocalStore.Json),token);
   return new(folder,files,sources.ToArray(),string.Join(";",hashes));
  }
  catch
  {
   // Keep a visible marker so an interrupted package cannot be mistaken for a reviewed package.
   await File.WriteAllTextAsync(Path.Combine(folder,"INCOMPLETE.txt"),"Driver package preparation did not complete.");
   throw;
  }
 }

 internal static List<(Uri Url,byte[] Hash)> ParseDownloads(string html,Guid id)
 {
  if(!Regex.IsMatch(html,@"downloadInformation\[0\]\.updateID\s*=\s*'"+Regex.Escape(id.ToString())+"'",IgnoreCase,RegexTimeout))
   throw new InvalidDataException("The catalog download response did not identify the selected package.");
  var packages=new List<(Uri,byte[])>();
  foreach(Match match in Regex.Matches(html,@"downloadInformation\[0\]\.files\[(\d+)\]\.url\s*=\s*'([^']+)'",RegexOptions.None,RegexTimeout))
  {
   var uri=new Uri(WebUtility.HtmlDecode(match.Groups[2].Value));
   if(!AllowedDownload(uri))throw new InvalidDataException("The catalog package is outside the Microsoft driver download service or is not a CAB.");
   var hashMatch=Regex.Match(html,@"downloadInformation\[0\]\.files\["+match.Groups[1].Value+@"\]\.sha256\s*=\s*'([^']+)'",RegexOptions.None,RegexTimeout);
   byte[] hash;
   try{hash=Convert.FromBase64String(hashMatch.Groups[1].Value);}catch(FormatException){throw new InvalidDataException("The Microsoft package SHA-256 is invalid.");}
   if(hash.Length!=32)throw new InvalidDataException("The Microsoft package SHA-256 is missing.");
   packages.Add((uri,hash));
  }
  if(packages.Count is <1 or >8)throw new InvalidDataException("The catalog package count is unsupported.");
  return packages;
 }

 internal static bool AllowedDownload(Uri uri)=>uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&
  uri.AbsolutePath.EndsWith(".cab",StringComparison.OrdinalIgnoreCase)&&
  new[]{"download.windowsupdate.com","delivery.mp.microsoft.com"}.Any(host=>uri.Host.Equals(host,StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith("."+host,StringComparison.OrdinalIgnoreCase));

 private static HttpClient Client()
 {
  var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromMinutes(15)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("FromZero/0.20 Microsoft-Catalog-driver-review");
  return client;
 }

 private static async Task<string> ReadMetadataAsync(HttpClient client,Uri uri,HttpContent? content,CancellationToken token)
 {
  if(uri.Scheme!="https"||uri.Host!=CatalogHost||!uri.IsDefaultPort||uri.UserInfo.Length!=0)throw new InvalidDataException("Unexpected Microsoft Catalog metadata host.");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
  timeout.CancelAfter(TimeSpan.FromSeconds(35));
  using var request=new HttpRequestMessage(content==null?HttpMethod.Get:HttpMethod.Post,uri){Content=content};
  using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
  response.EnsureSuccessStatusCode();
  await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
  using var output=new MemoryStream();
  var buffer=new byte[32*1024];
  int read;
  while((read=await stream.ReadAsync(buffer,timeout.Token))>0)
  {
   if(output.Length+read>4_000_000)throw new InvalidDataException("Microsoft Catalog metadata exceeds the size limit.");
   output.Write(buffer,0,read);
  }
  return Encoding.UTF8.GetString(output.ToArray());
 }

 private static async Task<long> DownloadCabAsync(HttpClient client,Uri uri,string path,byte[] expected,IProgress<string>? progress,CancellationToken token)
 {
  if(!AllowedDownload(uri))throw new InvalidDataException("Untrusted driver package address.");
  using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
  response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>MaximumCabBytes)throw new InvalidDataException("The driver CAB exceeds the 512 MB limit.");
  await using var input=await response.Content.ReadAsStreamAsync(token);
  await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  var buffer=new byte[81920];
  long total=0;
  int read;
  while((read=await input.ReadAsync(buffer,token))>0)
  {
   total+=read;
   if(total>MaximumCabBytes)throw new InvalidDataException("The driver CAB exceeds the 512 MB limit.");
   hash.AppendData(buffer,0,read);
   await output.WriteAsync(buffer.AsMemory(0,read),token);
  }
  if(total<36||response.Content.Headers.ContentLength is long length&&length!=total||!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(),expected))
   throw new InvalidDataException("The downloaded driver CAB does not match Microsoft's SHA-256 record.");
  return total;
 }

 internal static string[] ValidateCabinet(string path)=>ValidateCabinet(path,out _);

 private static string[] ValidateCabinet(string path,out long expandedBytes)
 {
  using var stream=File.OpenRead(path);
  using var reader=new BinaryReader(stream,Encoding.UTF8,true);
  var header=reader.ReadBytes(36);
  if(header.Length!=36||!header.AsSpan(0,4).SequenceEqual("MSCF"u8))throw new InvalidDataException("The driver download is not a Microsoft cabinet.");
  var size=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
  var fileOffset=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16));
  var folders=BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26));
  var count=BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
  var flags=BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30));
  // Authenticode-signed CABs can append a signature beyond cbCabinet. The SHA-256 above covers the entire download.
  if(size>stream.Length||count is 0 or >5000||folders==0||(flags&3)!=0||fileOffset<36||fileOffset>=size)
   throw new InvalidDataException("The cabinet layout or multi-volume package is unsupported.");
  stream.Position=fileOffset;
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  expandedBytes=0;
  for(var index=0;index<count;index++)
  {
   if(stream.Position+16>size)throw new InvalidDataException("Truncated cabinet directory.");
   var entry=reader.ReadBytes(16);
   if(entry.Length!=16)throw new InvalidDataException("Truncated cabinet directory.");
   expandedBytes+=BinaryPrimitives.ReadUInt32LittleEndian(entry);
   if(expandedBytes>2L*1024*1024*1024||BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan(8))>=folders)
    throw new InvalidDataException("The cabinet expands beyond the size limit or spans volumes.");
   var bytes=new List<byte>();
   byte value;
   while(true)
   {
    if(stream.Position>=size||bytes.Count>=1024)throw new InvalidDataException("Cabinet filename is too long or truncated.");
    value=reader.ReadByte();
    if(value==0)break;
    bytes.Add(value);
   }
   var encoding=(BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan(14))&0x80)!=0?Encoding.UTF8:Encoding.Latin1;
   var name=encoding.GetString(bytes.ToArray()).Replace('/',Path.DirectorySeparatorChar);
   var parts=name.Split('\\','/');
   if(name.Length==0||name.Contains(':')||Path.IsPathRooted(name)||parts.Any(part=>part is "" or "." or ".."||
      part.EndsWith('.')||part.EndsWith(' ')||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0||
      Regex.IsMatch(part,@"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",IgnoreCase,RegexTimeout))||!names.Add(name))
    throw new InvalidDataException("Unsafe or duplicate cabinet entry.");
  }
  return names.ToArray();
 }
}
