using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
namespace FromZero;

public static class PreparationDiagnostics
{
 public static async Task<PreparationSnapshot> CaptureAsync(CancellationToken token=default)
 {
  var inventory=new InventoryService();var devicesTask=inventory.DevicesAsync();var driversTask=inventory.DriversAsync();var passportTask=inventory.PassportAsync();await Task.WhenAll(devicesTask,driversTask,passportTask);token.ThrowIfCancellationRequested();
  var devices=await devicesTask;var passport=await passportTask;List<string> warnings=[];List<string> outputs=[],inputs=[];List<DisplayScreen> displays=[];
  try{(outputs,inputs)=AudioEndpoints.Read(devices);}catch(Exception ex)when(ex is COMException or InvalidCastException){warnings.Add("Etkin ses uçları okunamadı: "+ex.Message);}
  try{displays=new WindowsDisplayBackend().Read().ToList();}catch(Exception ex)when(ex is InvalidOperationException or ExternalException){warnings.Add("Ekran modları okunamadı: "+ex.Message);}
  bool portable=false;try{portable=(await Commands.PsAsync("@(Get-CimInstance Win32_SystemEnclosure | ForEach-Object { $_.ChassisTypes } | Where-Object { $_ -in 8,9,10,11,14,30,31,32 }).Count -gt 0",15)).Output.Trim().Equals("True",StringComparison.OrdinalIgnoreCase);}catch(Exception ex)when(ex is IOException or InvalidOperationException or TimeoutException){warnings.Add("Kasa türü okunamadı.");}
  string Value(string key)=>passport.ValueKind==JsonValueKind.Object&&passport.TryGetProperty(key,out var value)?value.ToString():"";
  string power="";try{power=GameService.ActivePlan();}catch(InvalidOperationException ex){warnings.Add("Güç planı okunamadı: "+ex.Message);}return new(DateTimeOffset.Now,devices,await driversTask,InstalledSoftware.ReadDesktopApps(),PreparationBackend.HasNetwork(),outputs,inputs,displays,Value("Manufacturer"),Value("Model"),portable,warnings,power);
 }
 public static async Task<ReadinessReport> InspectAsync(PreparationOptions options,PreparationSnapshot snapshot,CancellationToken token=default)
 {
  List<ReadinessCheck> checks=[];void Add(string id,string area,string label,string state,string evidence,string action="")=>checks.Add(new(id,area,label,state,evidence,action));
  var problems=snapshot.Devices.Where(d=>d.Present&&d.Problem!=0).ToList();Add("devices","Donanım","Aygıt durumu",problems.Count==0?"pass":"attention",problems.Count==0?"Mevcut aygıtlarda sorun kodu bildirilmedi.":string.Join("; ",problems.Select(d=>d.Name+" / kod "+d.Problem)),"drivers");
  Add("network-link","Ağ","Yerel bağlantı",snapshot.NetworkUp?"pass":"attention",snapshot.NetworkUp?"Etkin ağ bağdaştırıcısında ağ geçidi mevcut.":"Etkin ağ geçidi tespit edilmedi.","network");
  try{var addresses=await Dns.GetHostAddressesAsync("www.microsoft.com",token).WaitAsync(TimeSpan.FromSeconds(6),token);Add("dns","Ağ","DNS çözümleme",addresses.Length>0?"pass":"attention","www.microsoft.com / "+addresses.Length+" adres","network");}catch(Exception ex)when(ex is System.Net.Sockets.SocketException or TimeoutException){Add("dns","Ağ","DNS çözümleme","attention",ex.Message,"network");}
  try{using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(7),MaxResponseContentBufferSize=32768};using var response=await client.GetAsync("https://www.microsoft.com/robots.txt",token);var body=await response.Content.ReadAsStringAsync(token);bool okay=HttpsEvidence((int)response.StatusCode,response.Content.Headers.ContentType?.MediaType??"",body);Add("internet","Ağ","İnternet / HTTPS",okay?"pass":"attention",okay?"Microsoft HTTPS hedefinde TLS, HTTP 200 ve beklenen metin türü doğrulandı.":"HTTPS test hedefine beklenen yanıt alınamadı; bu sonuç tek başına tüm internetin kesik olduğunu kanıtlamaz.","network");}catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException){token.ThrowIfCancellationRequested();Add("internet","Ağ","İnternet / HTTPS","attention","HTTPS hedefi doğrulanamadı: "+ex.Message,"network");}
  try{using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(5),MaxResponseContentBufferSize=8192};using var response=await client.GetAsync("http://www.msftconnecttest.com/connecttest.txt",token);var body=await response.Content.ReadAsStringAsync(token);bool okay=response.StatusCode==HttpStatusCode.OK&&body.Trim()=="Microsoft Connect Test";Add("portal","Ağ","Giriş sayfası / HTTP testi",okay?"pass":"manual",okay?"Standart Microsoft bağlantı test metni alındı. Bu HTTP ölçümü HTTPS güvenini doğrulamaz.":"Standart test metni alınamadı. Ağ giriş sayfası veya HTTP hedef engeli olabilir; HTTPS sonucunu ayrıca incele.","network");}catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException){token.ThrowIfCancellationRequested();Add("portal","Ağ","Giriş sayfası / HTTP testi","manual","HTTP hedefi okunamadı. Kurumsal / filtreli ağlarda bu test engellenebilir; HTTPS sonucunu ayrıca incele.","network");}
  foreach(var gpu in snapshot.Devices.Where(d=>d.Present&&d.Class.Equals("Display",StringComparison.OrdinalIgnoreCase))){var driver=snapshot.Drivers.FirstOrDefault(d=>d.DeviceId.Equals(gpu.Id,StringComparison.OrdinalIgnoreCase));bool proper=gpu.Problem==0&&driver?.IsSigned==true&&!driver.Provider.Equals("Microsoft",StringComparison.OrdinalIgnoreCase);Add("gpu-"+gpu.Id,"Grafik",gpu.Name,proper?"pass":"attention",driver==null?"Etkin sürücü kaydı bulunamadı.":driver.Provider+" / "+driver.Version+" / "+(driver.IsSigned?"imzalı":"imza doğrulanamadı"),"drivers");}
  if(!snapshot.Devices.Any(d=>d.Present&&d.Class.Equals("Display",StringComparison.OrdinalIgnoreCase)))Add("gpu","Grafik","Grafik sürücüsü","attention","Etkin ekran aygıtı bulunamadı.","drivers");
  foreach(var display in snapshot.Displays)Add("display-"+display.Name,"Ekran",display.Name,"pass",display.Current.Label+"; listelenen aynı çözünürlükte en yüksek yenileme: "+display.Modes.Where(m=>m.Width==display.Current.Width&&m.Height==display.Current.Height).Select(m=>m.Hz).DefaultIfEmpty(display.Current.Hz).Max()+" Hz. Bu değer FPS ölçümü değildir.","performance");
  Add("audio-output","Ses","Etkin ses çıkışı",snapshot.AudioOutputs.Count>0?"pass":"attention",snapshot.AudioOutputs.Count>0?string.Join(" / ",snapshot.AudioOutputs):"Etkin ses çıkışı okunamadı.");
  Add("audio-heard","Ses","Sesi duyma testi","manual","Test sesini çal ve sonucu kendin doğrula.","sound");
  var bt=snapshot.Devices.Where(d=>d.Present&&d.Class.Equals("Bluetooth",StringComparison.OrdinalIgnoreCase)).ToList();Add("bluetooth","Donanım","Bluetooth",bt.Count==0?"not-applicable":bt.All(d=>d.Problem==0)?"pass":"attention",bt.Count==0?"Bluetooth donanımı tespit edilmedi.":bt.Count+" aygıt; bağlantı/eşleşme kullanıcı testi gerektirir.","drivers");
  foreach(var id in options.Apps){var definition=PreparationPackages.Definition(PreparationPackages.RecipeId(id));var installed=snapshot.Apps.Where(a=>PreparationPackages.Matches(PreparationPackages.RecipeId(id),a)).ToList();if(installed.Count==0){Add("app-"+id,"Uygulamalar",PreparationExperience.Name(id),"attention","Kurulum kaydı bulunamadı.","apps");continue;}
   var executable=installed.Select(a=>a.Executable).FirstOrDefault(p=>!string.IsNullOrWhiteSpace(p)&&Path.GetFileNameWithoutExtension(p).Equals(definition.ProcessName,StringComparison.OrdinalIgnoreCase)&&File.Exists(p));
   executable??=FindExecutable(id,installed);Add("app-"+id,"Uygulamalar",PreparationExperience.Name(id),executable!=null?"pass":"manual",string.Join(" / ",installed.Select(a=>a.Version).Distinct())+(executable!=null?" • Çalıştırılabilir dosya: "+executable:" • Kurulum kaydı var; çalıştırılabilir dosya ayrıca doğrulanmalı."),"apps");
  }
  var components=GameComponents.Scan(snapshot.Apps);
  foreach(var id in PreparationPolicy.Create(options,"inspection",Environment.ProcessPath??"inspection.exe").Steps.Where(s=>s.Kind=="package").Select(s=>s.Id[8..])){
   var matching=components.Components.Where(c=>c.Recipe==id||(id=="directx-offline"&&c.Recipe=="directx")).ToList();foreach(var component in matching)Add("runtime-"+component.Id,"Bileşenler",component.Name,component.Present?"pass":"attention",component.Evidence+". Dosya varlığı oyun içi çalışmayı garanti etmez.","drivers");
  }
  if(options.Profile=="streaming"){Add("microphone","Yayın","Etkin mikrofon",snapshot.AudioInputs.Count>0?"pass":"attention",snapshot.AudioInputs.Count>0?string.Join(" / ",snapshot.AudioInputs):"Etkin kayıt ucu bulunamadı.");Add("encoder","Yayın","OBS kodlayıcı ve ses kaydı","manual","OBS otomatik yapılandırma sihirbazında kodlayıcı, mikrofon seviyesi ve kısa kayıt doğrulanmalı. Ekran kartı markası kodlayıcı çalışırlığı kanıtı değildir.");}
  if(options.Profile=="developer")foreach(var id in new[]{"git","node-lts","powershell"}){var name=id switch{"node-lts"=>"node.exe","powershell"=>"pwsh.exe",_=>"git.exe"};Add("path-"+id,"Geliştirme",name+" / PATH",FindInPath(name)!=null?"pass":"manual",FindInPath(name)??"Yeni terminal / oturum açılışı veya uygulama yolu kontrolü gerekli.");}
  if(options.Profile=="laptop"){foreach(var pair in new[]{("Battery","Pil"),("Camera","Kamera"),("Mouse","Touchpad / işaret aygıtı")}){var found=snapshot.Devices.Where(d=>d.Present&&d.Class.Equals(pair.Item1,StringComparison.OrdinalIgnoreCase)).ToList();Add("laptop-"+pair.Item1,"Dizüstü",pair.Item2,found.Count==0?"manual":found.All(d=>d.Problem==0)?"pass":"attention",found.Count==0?"Aygıt sınıfında tespit edilemedi; modelin desteklediği özellik kullanıcı tarafından doğrulanmalı.":string.Join(" / ",found.Select(d=>d.Name)));}Add("hotkeys","Dizüstü","Parlaklık ve işlev tuşları","manual","Parlaklık, ses ve touchpad kısayollarını klavyeden dene. Kamera görüntüsü ve pil sağlığı bu taramada ölçülmedi.");}
  if(options.Games.Count>0)checks.AddRange(await PreparationGameReadiness.InspectAsync(options.Games,snapshot,token));foreach(var warning in snapshot.Warnings)Add("warning-"+checks.Count,"Tarama","Okunamayan bilgi","unavailable",warning);
  return new(DateTimeOffset.Now,checks);
 }
 internal static bool HttpsEvidence(int status,string type,string body)=>status==200&&type.Equals("text/plain",StringComparison.OrdinalIgnoreCase)&&body.Contains("User-agent:",StringComparison.OrdinalIgnoreCase);
 private static string? FindInPath(string name)=>Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Select(p=>Path.Combine(p.Trim('"'),name)).FirstOrDefault(File.Exists);
 private static string? FindExecutable(string id,IEnumerable<InstalledApp> apps)
 {
  var relative=id switch{"firefox"=>"firefox.exe","chrome"=>"Google\\Chrome\\Application\\chrome.exe","vlc"=>"VideoLAN\\VLC\\vlc.exe","steam"=>"Steam\\steam.exe","vscode"=>"Microsoft VS Code\\Code.exe","git"=>"Git\\cmd\\git.exe","obs"=>"obs-studio\\bin\\64bit\\obs64.exe","node-lts"=>"nodejs\\node.exe","powershell"=>"PowerShell\\7\\pwsh.exe","notepad"=>"Notepad++\\notepad++.exe","edge"=>"Microsoft\\Edge\\Application\\msedge.exe","thunderbird"=>"Mozilla Thunderbird\\thunderbird.exe",_=>""};
  var process=PreparationExperience.Apps.Single(a=>a.Id==id).Process+".exe";var candidates=apps.Where(a=>!string.IsNullOrWhiteSpace(a.InstallLocation)).Select(a=>Path.Combine(a.InstallLocation,process)).ToList();if(relative.Length>0)foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")})candidates.Add(Path.Combine(root,relative));
  return candidates.FirstOrDefault(File.Exists);
 }
 public static async Task SaveReportAsync(PreparationJob job,PreparationSnapshot snapshot,ReadinessReport report){var folder=PreparationStore.Folder(job.Id);await File.WriteAllTextAsync(Path.Combine(folder,"readiness.json"),JsonSerializer.Serialize(report,LocalStore.Json));await File.WriteAllTextAsync(Path.Combine(folder,"snapshot-after.json"),JsonSerializer.Serialize(snapshot,LocalStore.Json));}
}

internal static class AudioEndpoints
{
 [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]private class Enumerator{}
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IEnumerator{void EnumAudioEndpoints(int flow,uint state,out ICollection collection);void GetDefaultAudioEndpoint(int flow,int role,out IDevice device);void GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IDevice device);void RegisterEndpointNotificationCallback(nint callback);void UnregisterEndpointNotificationCallback(nint callback);}
 [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface ICollection{void GetCount(out uint count);void Item(uint index,out IDevice device);}
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevice{void Activate(ref Guid iid,uint context,nint activation,[MarshalAs(UnmanagedType.IUnknown)]out object value);void OpenPropertyStore(uint mode,out nint value);void GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);void GetState(out uint state);}
 internal static (List<string> Outputs,List<string> Inputs) Read(IReadOnlyList<DeviceInfo> devices)
 {
  var enumerator=(IEnumerator)new Enumerator();List<string> ReadFlow(int flow){enumerator.EnumAudioEndpoints(flow,1,out var collection);try{collection.GetCount(out var count);List<string> names=[];for(uint i=0;i<count;i++){collection.Item(i,out var device);try{device.GetId(out var id);names.Add(devices.FirstOrDefault(d=>d.Id.EndsWith(id,StringComparison.OrdinalIgnoreCase))?.Name??(flow==0?"Etkin çıkış ":"Etkin mikrofon ")+(i+1));}finally{Marshal.ReleaseComObject(device);}}return names;}finally{Marshal.ReleaseComObject(collection);}}
  try{return(ReadFlow(0),ReadFlow(1));}finally{Marshal.ReleaseComObject(enumerator);}
 }
}




