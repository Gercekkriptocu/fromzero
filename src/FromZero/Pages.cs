using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using QRCoder;
using System.Windows.Media.Imaging;

namespace FromZero;
public partial class MainWindow
{
 private void Capsule()
 {
  Header("02 / KURTARMA KAPSÜLÜ", "Her formattan önce bir kayıt noktası.", "Çalışan sürücülerini, donanım pasaportunu ve uygulama listeni tek klasörde yanında taşı.");
  Page.Children.Add(Card(Stack(T("▤  Kişisel kurtarma paketi",22,"#B9E2A7",true),T("01  Sürücülerini yedekle\n02  Donanım ve uygulama listesini kaydet\n03  Taşınabilir FromZero'yu ekle\n04  Paketin sağlamlığını kontrol et",15,"#BDCBC1"),Row(Action("Kapsül oluştur  →",CreateCapsuleAsync,true),Action("Kapsülü doğrula",VerifyCapsuleAsync)))));
  var capsuleDetails=Page.Children.Count;Page.Children.Add(Card(Stack(T("Neler dahil?",17,bold:true),T("Üçüncü taraf sürücü paketleri • Donanım bilgileri • Kurulu masaüstü uygulama listesi • Başlangıç girdilerinin referans listesi • FromZero uygulaması",14,"#9AAFAE"),T("Kişisel belgeler, uygulama ayarları, hesaplar ve Wi-Fi parolaları bu sürümde yedeklenmez. Paket Windows kurulum USB'sinin yerine geçmez. Çalışır sürücü yedeği farklı Windows sürümünde uyumluluk garantisi değildir.",13,"#EBC078"))));
  Page.Children.Add(Card(Stack(T("Format sonrası yol haritan",17,bold:true),T("USB → App / FromZero.exe → Kapsülü doğrula → Sürücüler / Kapsülden sürücü bul → Ağ bağlantısını kontrol et → Windows Update taraması → Kurulum çantası",14,"#9AAFAE"),Row(Action("Kapsülden sürücü bul",FindCapsuleDriversAsync),Action("Donanım pasaportunu kaydet",SavePassportAsync)))));GroupSince(capsuleDetails,"Kapsül ayrıntıları ve format sonrası");
 }
 private async Task CreateCapsuleAsync()
 {
  var parent=PickFolder("USB bellekte veya formatlanmayacak diskte bir konum seçin"); if(parent==null)return;
  var drive=new DriveInfo(System.IO.Path.GetPathRoot(parent)!);
  var systemDrive=System.IO.Path.GetPathRoot(Environment.SystemDirectory);
  var message="Şu konuma yeni kurtarma klasörü oluşturulacak:\n"+parent+"\n\nSürücü dışa aktarma için Windows yönetici izni isteyecek. Donanım ve uygulama adları bu yerel pakete kaydedilecek.";
  if(string.Equals(drive.Name,systemDrive,StringComparison.OrdinalIgnoreCase))message+="\n\nSeçtiğin konum Windows ile aynı diskte. Format sırasında silinmemesi için paketi USB'ye veya başka bir diske taşımalısın.";
  if(!Confirm(message,"Kapsül oluştur"))return;
  var progress=new Progress<string>(s=>OperationTitle.Text=s);
  var path=await new CapsuleService(inventory,driverService).CreateAsync(parent,progress);
  Detail("Kurtarma kapsülü hazır",path+"\n\nKapsülü doğrula düğmesiyle dosya bütünlüğünü kontrol edebilirsin. Paket cihazının donanım ve uygulama listesini içerir; güvenilir yerde sakla.");
  Commands.Open(path);
 }
 private async Task VerifyCapsuleAsync()
 {
  var dlg=new OpenFileDialog { Filter="FromZero kapsülü|capsule.json",Title="capsule.json seçin" };if(dlg.ShowDialog(this)!=true)return;
  Detail("Kapsül doğrulaması",await new CapsuleService(inventory,driverService).VerifyAsync(dlg.FileName));
 }
 private async Task FindCapsuleDriversAsync()
 {
  var dlg=new OpenFileDialog { Filter="FromZero kapsülü|capsule.json",Title="Sürücü aranacak kapsülü seçin" };if(dlg.ShowDialog(this)!=true)return;
  var root=System.IO.Path.GetDirectoryName(dlg.FileName)!;
  var manifest=JsonSerializer.Deserialize<CapsuleManifest>(await File.ReadAllTextAsync(dlg.FileName),LocalStore.Json)??throw new InvalidDataException("Kapsül okunamadı.");
  var errors=await PackageSafety.VerifyAsync(root,manifest);if(errors.Count>0)throw new InvalidDataException("Paket bütünlüğü doğrulanamadı. Kurulum durduruldu.");
  if(!scanned)await ScanAsync();
  var matches=new List<(string Inf,List<DeviceInfo> Devices)>();
  foreach(var file in manifest.Files.Where(f=>f.Path.EndsWith(".inf",StringComparison.OrdinalIgnoreCase))) {
   var full=PackageSafety.ResolveInside(root,file.Path);var ids=PackageSafety.ReadHardwareIds(full);
   var found=devices.Where(d=>d.HardwareIds.Any(ids.Contains)).ToList();if(found.Count>0)matches.Add((full,found));
  }
  Navigate("drivers");Section("Kapsüldeki eşleşen paketler");
  if(matches.Count==0)Page.Children.Add(Card(T("Bu bilgisayarın donanım kimlikleriyle eşleşen INF bulunamadı. Windows sürümü ve mimarisini de kontrol edin.",14,"#EBC078")));
  foreach(var match in matches.OrderByDescending(m=>m.Devices.Any(d=>d.Class=="Net" || d.Problem!=0))) {
   Page.Children.Add(Card(Stack(T(System.IO.Path.GetFileName(match.Inf),16,"#B9E2A7",true),T(string.Join(" • ",match.Devices.Select(d=>d.Name)),13,"#9AAFAE"),Row(Action("Paketi incele ve kur",()=>InstallLocalInfAsync(match.Inf))))));
  }
 }
 private void LegacyDrivers()
 {
  Header("03 / SÜRÜCÜ MERKEZİ", "Doğru donanım. Doğru sürücü.", "Windows Update + yerel INF + çalışan sürücü yedeği. Sürücü işlemlerinde WinGet kullanılmaz.");
  Page.Children.Add(Card(Stack(T(scanned?$"{devices.Count} cihaz • {drivers.Count} sürücü • {devices.Count(d=>d.Problem!=0)} dikkat gerektiren cihaz":"Henüz taranmadı",18,"#B9E2A7",true),Row(Action("Donanımı tara",ScanAsync),Action("Sürücüleri yedekle",BackupDriversAsync),Action("Yerel INF seç",PickInfAsync),Action("Kapsülden sürücü bul",FindCapsuleDriversAsync)),Row(Action("Windows Update'te sürücü ara",ScanDriverUpdatesAsync,true),Action("Üretici kurulum EXE'si",InstallVendorDriverAsync),Action("Aygıt Yöneticisi",()=>{Commands.Open("devmgmt.msc");return Task.CompletedTask;})))));
  Page.Children.Add(T("Kurulumdan önce mevcut sürücüler yedeklenir. Windows paket imzasını, uyumluluğunu ve sürücü sıralamasını denetler. Paket eklemek, daha iyi sıralanan mevcut sürücüyü zorla değiştirmez.",12,"#9AAFAE"));
  if(updates.Count>0) {
   Section("Windows Update'in sunduğu sürücüler");
   foreach(var update in updates)Page.Children.Add(Card(Stack(T(update.Title,15,"#D8E8D3",true),T(update.Manufacturer+" / "+update.Model,12,"#9AAFAE"),Row(Action(update.IsFirmware?"Üretici üzerinden işlem":"İncele ve kur",async()=>{
    if(update.IsFirmware){Detail("Üretici firmware güncellemesi",update.Title+"\n\nBu paket firmware/BIOS sınıfında. FromZero bu tür paketleri kurmaz. Cihaz üreticisinin resmi destek aracı ve kurtarma talimatlarıyla ilerle.");return;}
    if(update.Eula.Length>0)Detail("Üretici lisans koşulları",update.Eula);
    if(!Confirm(update.Title+"\n\nBu sürücü Windows Update'ten indirilecek. Gösterilen üretici lisans koşullarını kabul ederek devam edebilirsin. Önce sürücü yedeği alınacak. Ekran veya ağ bağlantısı geçici kesilebilir; yeniden başlatma gerekebilir.\n\nKoşulları kabul edip kuruluma devam edilsin mi?","Sürücü kurulum planı"))return;
    Detail("Sürücü kurulum sonucu",await driverService.InstallUpdateAsync(update));updates=[];await ScanAsync();
   })))));
  }
  var search=new TextBox { ToolTip="Cihaz veya sağlayıcı adına göre ara" };Page.Children.Add(T("ENVANTERDE ARA",10,"#9AAFAE"));Page.Children.Add(search);
  var list=new StackPanel();Page.Children.Add(list);
  void Render() {
   list.Children.Clear();var query=search.Text.Trim();
   var shown=devices.Where(d=>query.Length==0 || d.Name.Contains(query,StringComparison.OrdinalIgnoreCase) || d.Id.Contains(query,StringComparison.OrdinalIgnoreCase)).OrderByDescending(d=>d.Problem!=0).ThenByDescending(d=>d.Class is "Net" or "Display").ThenBy(d=>d.Name).Take(100).ToList();
   foreach(var device in shown) {
    var driver=drivers.FirstOrDefault(d=>d.DeviceId.Equals(device.Id,StringComparison.OrdinalIgnoreCase));
    var title=T((device.Problem!=0?"! ":"▪ ")+(string.IsNullOrWhiteSpace(device.Name)?"Tanımsız cihaz":device.Name),14,device.Problem!=0?"#EBC078":"#D8E8D3",true);
    var description=T((device.Class.Length>0?device.Class:"Sınıf bilinmiyor")+"  /  "+(device.Problem==0?"Windows hata bildirmiyor":"Cihaz hata kodu: "+device.Problem)+"\n"+(driver==null?"Sürücü bilgisi yok":driver.Provider+" • "+driver.Version+" • "+driver.Inf),12,"#9AAFAE");
    var details=Action("Kimlik / ayrıntı",()=>{Detail(device.Name,device.Id+"\n\nDonanım kimlikleri:\n"+string.Join("\n",device.HardwareIds)+"\n\n"+JsonSerializer.Serialize(driver,LocalStore.Json));return Task.CompletedTask;});
    details.Padding=new Thickness(9,6,9,6);details.Margin=new Thickness(0,8,0,0);
    var border=Card(Stack(title,description,details));border.Padding=new Thickness(15);border.Margin=new Thickness(0,0,0,9);list.Children.Add(border);
   }
   if(shown.Count==0)list.Children.Add(T("Eşleşen cihaz bulunamadı.",13,"#9AAFAE"));
   if(devices.Count>100 && query.Length==0)list.Children.Add(T("İlk 100 cihaz gösteriliyor. Diğer cihazlar için arama kullanın.",12,"#9AAFAE"));
  }
  search.TextChanged+=(_,_)=>Render();Render();
  GpuTools();
  Section("Resmi üretici kaynakları");
  Page.Children.Add(Row(Link("NVIDIA","https://www.nvidia.com/Download/index.aspx"),Link("AMD","https://www.amd.com/en/support"),Link("Intel","https://www.intel.com/content/www/us/en/download-center/home.html"),Link("Microsoft Update Catalog","https://www.catalog.update.microsoft.com/")));
 }
 private Button Link(string label,string url)=>Action(label,()=>{Commands.Open(url);return Task.CompletedTask;});
 private async Task BackupDriversAsync() {var parent=PickFolder("Sürücü yedeği konumunu seçin");if(parent==null)return;var folder=System.IO.Path.Combine(parent,"FromZero-Drivers-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));await driverService.BackupAsync(folder);Detail("Sürücü yedeği",folder+"\n\nÜçüncü taraf sürücü paketleri aktarıldı. Bu, tam sistem yedeği değildir.");}
 private async Task PickInfAsync() {var dlg=new OpenFileDialog {Filter="Sürücü paketi|*.inf",Title="Üreticinin çıkartılmış paketinden INF seçin"};if(dlg.ShowDialog(this)==true)await InstallLocalInfAsync(dlg.FileName);}
 private async Task InstallLocalInfAsync(string inf)
 {
  var currentDevices=await inventory.DevicesAsync();
  var check=await driverService.CheckInfAsync(inf,currentDevices);
  if(!Confirm(check+"\n\nDosya: "+inf+"\n\nÖnce çalışan sürücüler yedeklenecek, sonra bu paket Windows'a kurulmak üzere verilecek. Windows yönetici izni isteyecek. Yeniden başlatma veya geçici bağlantı kesintisi gerekebilir. Devam edilsin mi?","Yerel sürücü planı"))return;
  Detail("Kurulum sonucu",await driverService.InstallInfAsync(inf));await ScanAsync();
 }
 private async Task ScanDriverUpdatesAsync() {OperationTitle.Text="Windows Update sürücü kataloğu taranıyor; birkaç dakika sürebilir…";updates=await driverService.ScanUpdatesAsync();Journal.Write("Sürücü güncelleme taraması",updates.Count+" paket sunuluyor.");Navigate("drivers");if(updates.Count==0)AppMessage.Show(this,"Windows Update şu anda bu bilgisayar için sürücü güncellemesi sunmuyor.","FromZero");}
 private async Task InstallVendorDriverAsync()
 {
  var dlg=new OpenFileDialog{Filter="Üretici kurulum paketi|*.exe",Title="Donanım üreticisinin resmi kurulum EXE'sini seçin"};if(dlg.ShowDialog(this)!=true)return;
  var publisher=await DriverService.SignatureAsync(dlg.FileName);var hash=await PackageSafety.HashFileAsync(System.IO.Path.GetDirectoryName(dlg.FileName)!,dlg.FileName);
  if(!Confirm("Dosya: "+dlg.FileName+"\nYayıncı: "+publisher+"\nSHA-256: "+hash.Sha256+"\n\nMevcut sürücüler önce yedeklenecek, sonra üreticinin kurulum arayüzü açılacak. Donanım/işletim sistemi uyumluluğunu üreticinin kurucusu denetler. Bu EXE için INF eşleştirmesi yapılmaz. Devam edilsin mi?","Üretici sürücü paketi"))return;
  await driverService.BackupAsync(System.IO.Path.Combine(LocalStore.FilePath("Backups"),DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
  Commands.Open(dlg.FileName);Journal.Write("Üretici sürücü kurucusu açıldı",dlg.FileName+"\n"+publisher);Detail("Üretici kurucusu","Kurulum penceresinde devam et. İşlem tamamlanınca Donanımı tara ile güncel sürücüyü kontrol et. FromZero bu aşamada kurulumun başarılı olduğunu varsaymaz.");
 }
 private void LegacyNetwork()
 {
  Header("04 / BAĞLANTI İSTASYONU", "Bağlantının nerede koptuğunu bul.", "Adaptör, sürücü, ağ geçidi ve DNS durumunu ayrı ayrı kontrol et.");
  Page.Children.Add(Card(Stack(T("⌁  Ağ teşhisi",20,"#B9E2A7",true),T("Test, ağ geçidine ICMP; microsoft.com'a DNS ve 443 bağlantısı gönderir. Sonuç, hız testi değildir.",13,"#9AAFAE"),Row(Action("Bağlantıyı test et",async()=>Detail("Bağlantı raporu",await NetworkService.DiagnoseAsync()),true),Action("Wi-Fi raporu oluştur",WifiReportAsync),Action("Wi-Fi ayarları",()=>{Commands.Open("ms-settings:network-wifi");return Task.CompletedTask;})),Row(Action("DNS önbelleğini temizle",async()=>{
   if(!Confirm("DNS çözümleyici önbelleği temizlenecek. DNS sunucuların değişmeyecek. Devam edilsin mi?"))return;
   var r=await Commands.ElevatedAsync("Clear-DnsClientCache; 'DNS önbelleği temizlendi.'");Journal.Write("DNS önbelleği",r.Output,r.Success);if(!r.Success)throw new InvalidOperationException(r.Output);Detail("DNS",r.Output);
  }),Action("Ağ sıfırlama ayarları",()=>{Commands.Open("ms-settings:network-status");return Task.CompletedTask;})))));
  Page.Children.Add(Card(Stack(T("İnternet yoksa",17,"#EBC078",true),T("Kurtarma kapsülünden ağ sürücünü seç veya donanım kimliklerini bir yardım kartına kaydet. Telefon/başka bilgisayarla üreticinin sitesinden paketi indirip USB ile getir. Dosyaları çıkarttıktan sonra Sürücüler > Yerel INF seç yolunu kullan.",14,"#9AAFAE"),Row(Action("Kapsülden sürücü bul",FindCapsuleDriversAsync),Action("Telefondan QR ile ara",ShowNetworkQrAsync),Action("Telefon / USB yardım kartı",ExportNetworkHelpAsync)))));
  Section("Adaptörler ve yerel ağ bilgileri");Page.Children.Add(Card(T(NetworkService.Describe(),13,"#BDCBC1")));
 }
 private async Task WifiReportAsync()
 {
  var result=await Commands.ElevatedAsync("& $env:SystemRoot\\System32\\netsh.exe wlan show wlanreport; if ($LASTEXITCODE -ne 0) { throw 'Wi-Fi raporu üretilemedi. Kablosuz adaptör ve hizmet durumunu kontrol edin.' }");
  Journal.Write("Wi-Fi raporu",result.Output,result.Success);if(!result.Success)throw new InvalidOperationException(result.Output);
  var report=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Microsoft","Windows","WlanReport","wlan-report-latest.html");
  if(File.Exists(report))Commands.Open(report);else Detail("Wi-Fi raporu",result.Output);
 }
 private async Task ExportNetworkHelpAsync()
 {
  if(!scanned)await ScanAsync();var dlg=new SaveFileDialog{FileName="FromZero-ag-yardimi.html",Filter="HTML|*.html"};if(dlg.ShowDialog(this)!=true)return;
  var targets=devices.Where(d=>d.Class=="Net" || d.Problem!=0).ToList();
  string E(string v)=>WebUtility.HtmlEncode(v);
  var html="<!doctype html><html lang='tr'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>FromZero • Ağ kurtarma kartı</title><style>body{background:#142129;color:#e8eee5;font:16px system-ui;max-width:850px;margin:40px auto;padding:20px}h1,h2,a{color:#b9e2a7}article{border:1px solid #496561;padding:18px;margin:15px 0}code{overflow-wrap:anywhere}p{line-height:1.6}</style><h1>FROMZERO / Ağ kurtarma kartı</h1><p>Bu dosyayı telefonuna veya internetli bir bilgisayara aktar. Üreticinin resmi sitesinde cihaz modeline ve aşağıdaki donanım kimliğine uygun Windows sürücüsünü bul. Paketi USB ile bilgisayara taşı, çıkart ve FromZero'te Yerel INF seç.</p><p>Windows mimarisi: "+E(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString())+"</p>";
  foreach(var d in targets){
   html+="<article><h2>"+E(d.Name)+"</h2><p>"+E(d.Class)+" / hata "+d.Problem+"</p><code>"+string.Join("<br>",d.HardwareIds.Select(E))+"</code>";
   if(d.HardwareIds.Length>0){var url=CatalogUrl(d.HardwareIds[0]);var png=PngByteQRCodeHelper.GetQRCode(url,QRCodeGenerator.ECCLevel.Q,5);html+="<p><a href='"+E(url)+"'>Microsoft Catalog'da ara</a></p><img alt='Microsoft Catalog arama QR kodu' width='220' height='220' src='data:image/png;base64,"+Convert.ToBase64String(png)+"'><p>QR bağlantısını açmak, donanım kimliğini Microsoft'a arama sorgusu olarak iletir.</p>";}
   html+="</article>";
  }
  html+="<p>Resmi kaynaklar: <a href='https://www.intel.com/content/www/us/en/download-center/home.html'>Intel</a> · <a href='https://www.realtek.com/Download/Index?cate_id=194&menu_id=297'>Realtek</a> · <a href='https://support.microsoft.com/windows'>Microsoft</a>. Dizüstü/anakart üreticisinin model destek sayfasını da kontrol et.</p><p>Bu kart sürücü içermez. Donanım bilgilerini içerir; herkese açık paylaşma.</p></html>";
  await File.WriteAllTextAsync(dlg.FileName,html);Journal.Write("Ağ yardım kartı",dlg.FileName);Commands.Open(dlg.FileName);
 }
 private static string CatalogUrl(string hardwareId)=>"https://www.catalog.update.microsoft.com/Search.aspx?q="+Uri.EscapeDataString(hardwareId);
 private async Task ShowNetworkQrAsync()
 {
  if(!scanned)await ScanAsync();
  var targets=devices.Where(d=>d.HardwareIds.Length>0&&(d.Problem!=0||(d.Class=="Net"&&(d.Id.StartsWith("PCI\\",StringComparison.OrdinalIgnoreCase)||d.Id.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase)||d.Id.StartsWith("SD\\",StringComparison.OrdinalIgnoreCase))))).ToList();
  if(targets.Count==0)throw new InvalidOperationException("Donanım kimliği olan fiziksel ağ adaptörü veya sorunlu cihaz bulunamadı.");
  var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(T("Telefonun internetiyle devam et",24,"#B9E2A7",true));panel.Children.Add(T("QR'ı telefondan açmak donanım kimliğini Microsoft Update Catalog'a arama sorgusu olarak iletir. Windows sürümü/mimariyi kontrol ederek doğru paketi indir ve USB ile getir. CAB paketini Windows'ta çıkart, sonra eşleşen INF'yi seç. Bu akış paketi otomatik seçmez veya telefondan bilgisayara otomatik aktarmaz.",13,"#9AAFAE"));
  foreach(var d in targets){
   var png=PngByteQRCodeHelper.GetQRCode(CatalogUrl(d.HardwareIds[0]),QRCodeGenerator.ECCLevel.Q,6);using var stream=new MemoryStream(png);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
   var image=new Image{Source=bitmap,Width=220,Height=220,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,12)};System.Windows.Media.RenderOptions.SetBitmapScalingMode(image,System.Windows.Media.BitmapScalingMode.NearestNeighbor);
   panel.Children.Add(Card(Stack(T(d.Name,16,"#D8E8D3",true),T(d.HardwareIds[0],12,"#9AAFAE"),image)));
  }
  new Window{Owner=this,Title="Telefonla ağ kurtarma",Width=740,Height=720,Background=B("#142129"),Content=new ScrollViewer{Content=panel},WindowStartupLocation=WindowStartupLocation.CenterOwner}.ShowDialog();
 }
 private void LegacyPerformance()
 {
  Header("05 / PERFORMANS", "Önce ölç. Sonra iyileştir.", "Gerçek bellek baskısını, kaynak tüketimini ve depolama alanını incele.");
  var m=metrics.Read();
  Page.Children.Add(Card(Stack(T($"RAM  {m.UsedGb:0.0} / {m.TotalGb:0.0} GB  •  %{m.RamPercent:0}",23,"#B9E2A7",true),T(m.RamPercent>=85?"Bellek kullanımı yüksek. Aşağıdaki uygulamaları ve iş yükünü incele; bu tek başına bellek sızıntısı kanıtı değildir.":"Anlık RAM kullanımında yüksek doluluk görülmüyor. Doluluk tek başına yavaşlık ölçüsü değildir.",13,"#9AAFAE"),Row(Action("Ölçümü yenile",()=>{Navigate("performance");return Task.CompletedTask;}),Action("Ölçümü kaydet",SaveMeasurementAsync),Action("Son kayıtla karşılaştır",CompareMeasurementAsync),Action("Görev Yöneticisi",()=>{Commands.Open("taskmgr.exe");return Task.CompletedTask;})))));
  if(samples.Count>=2) {
   var canvas=new Canvas {Height=90,ClipToBounds=true,Background=B("#122129")};var line=new Polyline{Stroke=B("#B9E2A7"),StrokeThickness=2};canvas.Children.Add(line);
   canvas.SizeChanged+=(_,_)=>{line.Points=new PointCollection(samples.Select((s,i)=>new Point(i*canvas.ActualWidth/Math.Max(1,samples.Count-1),85-s.Ram*.75)));};
   Page.Children.Add(Card(Stack(T("BELLEK DOLULUĞU / BU OTURUM / 2 SANİYE",10,"#9AAFAE"),canvas)));
  }
  Section("En çok bellek kullanan uygulamalar");
  foreach(var p in InventoryService.Processes().Take(8)) {
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(AppIcons.Heading(p.Name,AppIcons.ProcessPath(p.Id),T(p.Name+"  /  PID "+p.Id,13)));var mem=T($"{p.Memory/1048576d:0} MB",13,"#B9E2A7");Grid.SetColumn(mem,1);row.Children.Add(mem);var c=Card(row);c.Padding=new Thickness(13,9,13,9);c.Margin=new Thickness(0,0,0,6);Page.Children.Add(c);
  }
  Section("Depolama bakımı");
  Page.Children.Add(Card(Stack(T("FromZero ile yer aç",18,bold:true),T("233 hedef kuralıyla uygulama, tarayıcı, Windows ve oyun önbelleklerini incele. Silmeden önce hedefleri ve tam dosya listesini gözden geçir.",13,"#9AAFAE"),Row(Action("Temizlenecek dosyaları tara",PreviewCleanupAsync)))));
  Page.Children.Add(Row(Action("Başlangıç uygulamaları",()=>{Navigate("startup");return Task.CompletedTask;}),Action("Güç ayarları",()=>{Commands.Open("ms-settings:powersleep");return Task.CompletedTask;}),Action("Sistem dosyalarını doğrula",VerifySystemAsync)));
 }
 private async Task SaveMeasurementAsync()
 {
  var m=metrics.Read();var measurement=new { Time=DateTimeOffset.Now,m.Cpu,m.RamPercent,m.UsedGb,m.TotalGb,SampleCount=samples.Count,AverageCpu=samples.Count==0?m.Cpu:samples.Average(s=>s.Cpu),AverageRam=samples.Count==0?m.RamPercent:samples.Average(s=>s.Ram) };
  await File.WriteAllTextAsync(LocalStore.FilePath("baseline.json"),JsonSerializer.Serialize(measurement,LocalStore.Json));Journal.Write("Performans ölçümü kaydedildi",$"RAM %{m.RamPercent:0}; CPU %{m.Cpu:0}");Detail("Ölçüm kaydedildi","Bu oturumun CPU ve RAM ölçümleri kaydedildi. Karşılaştırmayı aynı iş yükü ve benzer koşullarda yap.");
 }
 private async Task CompareMeasurementAsync()
 {
  var path=LocalStore.FilePath("baseline.json");if(!File.Exists(path))throw new InvalidOperationException("Önce bir ölçüm kaydet.");var old=JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement;var m=metrics.Read();
  Detail("Ölçüm karşılaştırması",$"Kayıt: {old.GetProperty("Time").GetDateTimeOffset():g}\n\nCPU: %{old.GetProperty("Cpu").GetDouble():0} → %{m.Cpu:0}\nRAM: %{old.GetProperty("RamPercent").GetDouble():0} → %{m.RamPercent:0}\n\nBunlar kaynak kullanım ölçümleridir; FPS veya hızlanma ölçümü değildir. Açık uygulamalar ve iş yükü değiştiyse doğrudan karşılaştırılamaz.");
 }
 private async Task VerifySystemAsync()
 {
  if(!Confirm("Windows sistem dosyaları SFC /verifyonly ile kontrol edilecek. Bu işlem dosyaları onarmaz, birkaç dakika sürebilir. Yönetici izni istenir. Devam edilsin mi?"))return;
  var result=await Commands.ElevatedAsync("& $env:SystemRoot\\System32\\sfc.exe /verifyonly; if ($LASTEXITCODE -ne 0) { throw ('SFC kodu: ' + $LASTEXITCODE) }");Journal.Write("Sistem dosyası doğrulama",result.Output,result.Success);Detail("SFC sonucu",result.Output);
 }
 private void Startup()
 {
  Header("06 / BAŞLANGIÇ", "Açılışta neler seninle geliyor?", "Kullanıcı başlangıç girdilerini yedekleyerek kapat; istediğinde aynı değeri geri getir.");
  Page.Children.Add(Card(Stack(T("Kayıt defterinde hedefli değişiklik",17,"#B9E2A7",true),T("Açılışta çalışan uygulamaları seç. Değişiklikler yedeklenir; istediğinde geri açabilirsin.",13,"#9AAFAE"),Row(Action("Windows başlangıç ayarları",()=>{Commands.Open("ms-settings:startupapps");return Task.CompletedTask;})))));
  foreach(var item in InventoryService.Startup()) {
   var panel=Stack(T(item.Name+ (item.Disabled?"  /  FROMZERO İLE KAPATILDI":"  /  "+item.Location.ToUpperInvariant()),15,item.Disabled?"#EBC078":"#D8E8D3",true),T(item.Command,12,"#9AAFAE"));
   if(item.Location=="Kullanıcı")panel.Children.Add(Row(Action(item.Disabled?"Geri aç":"Başlangıçtan kaldır",()=>{if(Confirm(item.Name+"\n\n"+(item.Disabled?"Yedeklenen kullanıcı başlangıç girdisi geri yüklenecek.":"Bu uygulamanın kullanıcı başlangıç girdisi yedek anahtara taşınacak. Açık uygulama kapatılmayacak.")+"\nDevam edilsin mi?")){InventoryService.SetStartup(item,item.Disabled);Navigate("startup");}return Task.CompletedTask;})));
   Page.Children.Add(Card(panel));
  }
 }
 private void LegacyApps()
 {
  Header("07 / KURULUM ÇANTASI", "Alıştığın çalışma alanına geri dön.", "Kurulu uygulamaları listele, kurulum listeni kaydet ve resmi kaynaklardan devam et.");
  var exportStart=Page.Children.Count;Page.Children.Add(Card(Stack(T("Kurulum listesi",19,"#B9E2A7",true),T("Bu sürüm uygulama adlarını dışa aktarır. Uygulama verilerini ve ayarlarını otomatik geri yüklemez. Üreticinin imzalı kurulum dosyasını seçerek kurulum başlatabilirsin.",13,"#9AAFAE"),Row(Action("Kurulu uygulamaları göster",async()=>Detail("Kurulu uygulamalar",JsonSerializer.Serialize(await InventoryService.AppsAsync(),LocalStore.Json))),Action("Uygulama listesini kaydet",async()=>{var dlg=new SaveFileDialog{FileName="FromZero-apps.json",Filter="JSON|*.json"};if(dlg.ShowDialog(this)==true){await File.WriteAllTextAsync(dlg.FileName,JsonSerializer.Serialize(await InventoryService.AppsAsync(),LocalStore.Json));Journal.Write("Uygulama listesi kaydedildi",dlg.FileName);}}),Action("İmzalı kurulum dosyası seç",InstallSignedAppAsync)))));
  GroupSince(exportStart,"İçe / dışa aktarma");
  Page.Children.Add(Card(Stack(T(software==null?"Uygulama envanteri bekleniyor…":$"{software.Apps.Count} gerçek uygulama kaydı • son kontrol {software.Checked.ToLocalTime():HH:mm:ss}",18,"#B9E2A7",true),T((string.IsNullOrWhiteSpace(software?.Warning)?"Masaüstü uygulamaları 32/64 bit kullanıcı ve sistem kayıtlarından; Store/MSIX paketleri Windows paket envanterinden okunur. Taşınabilir uygulamalar kayıt bırakmıyorsa görünmeyebilir.":software.Warning),12,"#B9CBC1"),Row(Action("Uygulamaları tekrar tara",ScanAsync)))));
  var inventorySearch=new TextBox{ToolTip="Kurulu uygulama adı veya yayıncısı"};
  Page.Children.Add(T("KURULU UYGULAMALARDA ARA",11,"#B9E2A7"));Page.Children.Add(inventorySearch);
  var installedRows=new StackPanel();Page.Children.Add(installedRows);
  void RenderApps(){
   installedRows.Children.Clear();var query=inventorySearch.Text.Trim();var all=(software?.Apps??[]).Where(a=>query.Length==0||a.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||a.Publisher.Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
   foreach(var item in all.Take(12))installedRows.Children.Add(Card(Stack(T(item.Name+"  /  "+item.Version,14,"#D8E8D3",true),T(item.Publisher+" • "+item.Source,11,"#B0C1B9"))));
   installedRows.Children.Add(T(all.Count>12?$"{all.Count} eşleşmenin ilk 12'si gösteriliyor. Aramayı daraltabilirsin.":all.Count+" eşleşme.",11,"#B0C1B9"));
  }
  inventorySearch.TextChanged+=(_,_)=>RenderApps();RenderApps();
  var installerStart=Page.Children.Count;Section("Kurulum araçları");
  (string Name,string Description,string Url)[] catalog=[("Firefox","Tarayıcı","https://www.mozilla.org/firefox/new/"),("7-Zip","Arşiv yöneticisi","https://www.7-zip.org/"),("VLC","Medya oynatıcı","https://www.videolan.org/vlc/"),("Steam","Oyun kütüphanesi","https://store.steampowered.com/about/"),("Discord","İletişim","https://discord.com/download"),("Visual Studio Code","Kod düzenleyici","https://code.visualstudio.com/download")];
  foreach(var app in catalog){
   var row=Row(Link("Resmi indirme sayfası  ↗",app.Url));var recipe=AppDownloads.Recipes.FirstOrDefault(r=>r.Name==app.Name);
   if(recipe!=null)row.Children.Add(Action("Doğrulanmış kurucuyu indir",async()=>{
    var downloaded=await AppDownloads.DownloadAsync(recipe,new Progress<string>(s=>OperationTitle.Text=s));
    if(Confirm(recipe.Name+" resmi kaynaktan indirildi.\nYayıncı: "+downloaded.Publisher+"\nSHA-256: "+downloaded.Hash+"\n\nÜreticinin kurulum penceresi açılsın mı?","Doğrulanmış kurulum")){Commands.Open(downloaded.Path);Journal.Write("İndirilen kurucu açıldı",recipe.Name);}else Detail("Kurulum paketi kaydedildi",downloaded.Path);
   }));
   var found=InstalledSoftware.FindCatalogApps(software?.Apps??[],app.Name);
   var status=found.Count>0?"KURULU KAYDI BULUNDU / "+string.Join(" • ",found.Select(a=>a.Version.Length>0?a.Version:"Sürüm yok")):"Kurulum kaydı tespit edilmedi.";
   if(recipe!=null&&AppDownloads.Cached(recipe) is {} cached)status+=$"\nResmi paket doğrulandı: {cached.Checked.ToLocalTime():dd.MM.yyyy HH:mm} • {cached.Bytes/1048576d:0.0} MB";
   Page.Children.Add(Card(Stack(T(app.Name,20,bold:true),T(app.Description,12,"#BDCBC1"),T(status,12,found.Count>0?"#B9E2A7":"#C7C2A0"),row)));
  }
  GroupSince(installerStart,"Hızlı kurulum araçları");
 }
 private async Task InstallSignedAppAsync()
 {
  var dlg=new OpenFileDialog{Filter="Kurulum dosyası|*.exe;*.msi",Title="Güvenilir üreticinin kurulum dosyasını seçin"};if(dlg.ShowDialog(this)!=true)return;
  var publisher=await DriverService.SignatureAsync(dlg.FileName);var hash=await PackageSafety.HashFileAsync(System.IO.Path.GetDirectoryName(dlg.FileName)!,dlg.FileName);
  if(!Confirm("Dosya: "+dlg.FileName+"\n\nYayıncı: "+publisher+"\nSHA-256: "+hash.Sha256+"\n\nBu dosyanın üretici kurulum arayüzü açılacak. Kurulum seçeneklerini orada seçebilirsin. Devam edilsin mi?","Uygulama kurulumu"))return;
  Commands.Open(dlg.FileName);Journal.Write("Kurulum dosyası açıldı",dlg.FileName+"\n"+publisher);
 }
 private void History()
 {
  Header("08 / GEÇMİŞ VE KURTARMA", "Her değişikliğin bir izi var.", "İşlem sonuçlarını incele, sürücü yedeklerine ulaş ve geri alma seçeneklerini kullan.");
  var recoveryStart=Page.Children.Count;Page.Children.Add(Card(Stack(T("Kurtarma araçları",18,"#B9E2A7",true),T("Sürücüyü geri almak için Aygıt Yöneticisi'ndeki Sürücüyü Geri Al seçeneğini kullan. Bu seçenek yoksa yedekteki eşleşen INF'yi incele. Windows daha yüksek sıralanan sürücüyü koruyabilir; zorla düşürme yapılmaz.",13,"#9AAFAE"),Row(Action("Sürücü yedeklerini aç",()=>{var p=LocalStore.FilePath("Backups");Directory.CreateDirectory(p);Commands.Open(p);return Task.CompletedTask;}),Action("Başlangıç girdilerini geri aç",()=>{Navigate("startup");return Task.CompletedTask;}),Action("Aygıt Yöneticisi",()=>{Commands.Open("devmgmt.msc");return Task.CompletedTask;}),Action("İşlem günlüğünü dışa aktar",ExportHistoryAsync)))));
  GroupSince(recoveryStart,"Kurtarma ve dışa aktarma");
  var entries=Journal.Read();if(entries.Count==0)Page.Children.Add(T("Henüz işlem kaydı yok.",14,"#9AAFAE"));
  foreach(var e in entries.Take(60)) {
   var detail=e.Detail.Length>260?e.Detail[..260]+"…":e.Detail;
   Page.Children.Add(Card(Stack(T((e.Success?"▪ ":"! ")+e.Action,15,e.Success?"#B9E2A7":"#EBC078",true),T(e.Time.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),11,"#68817F"),T(detail,12,"#9AAFAE"),Row(Action("Ayrıntıyı göster",()=>{Detail(e.Action,e.Detail);return Task.CompletedTask;})))));
  }
 }
 private async Task ExportHistoryAsync(){var dlg=new SaveFileDialog{Filter="JSON|*.json",FileName="FromZero-islem-gecmisi.json"};if(dlg.ShowDialog(this)==true)await File.WriteAllTextAsync(dlg.FileName,JsonSerializer.Serialize(Journal.Read(),LocalStore.Json));}
}



