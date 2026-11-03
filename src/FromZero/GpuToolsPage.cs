using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace FromZero;
public partial class MainWindow
{
 private void GpuTools()
 {
  Section("Ekran kartı ve üretici uygulamaları");
  Page.Children.Add(Card(Stack(T("Donanım, sürücü ve uygulama ayrı kontrol edilir.",18,"#B9E2A7",true),T(software==null?"Envanter henüz okunmadı. Tarama tamamlandığında gerçek cihaz ve uygulama bilgileri görünür.":$"Son yerel kontrol: {software.Checked.ToLocalTime():dd.MM.yyyy HH:mm:ss} • {software.Apps.Count} masaüstü / Store kaydı.\nKurulum kaydı tek başına çalışır uygulama kanıtı değildir; açma düğmesi için gerçek EXE bulunmalıdır.",12,"#B9CBC1"),Row(Action("GPU ve uygulamaları yeniden kontrol et",ScanAsync)))));
  var states=GpuSoftware.Inspect(devices,drivers,software?.Apps??[]);
  foreach(var state in states.OrderByDescending(s=>s.HardwarePresent)){
   var tool=state.Tool;var recipe=AppDownloads.Recipes.Single(r=>r.Id==tool.Id);
   var panel=Stack(AppIcons.Heading(tool.Name,state.Executable,T(tool.Name,22,state.HardwarePresent?"#B9E2A7":"#C8D1CD",true)));
   panel.Children.Add(T(state.HardwarePresent?"DONANIM BULUNDU  /  "+string.Join(" • ",state.Devices.Select(d=>d.Name)):"Bu üreticiye ait ekran kartı tespit edilmedi. Araç indirilebilir; uyumluluğu üreticinin kurucusu denetler.",13,state.HardwarePresent?"#B9E2A7":"#C0B99B"));
   foreach(var driver in state.Drivers)panel.Children.Add(T("Sürücü: "+driver.Version+" • "+driver.Provider+"\n"+driver.Inf+" / "+(GpuSoftware.InfPresent(driver)?"Windows INF dosyası mevcut":"INF dosyası doğrulanamadı")+" / "+(driver.IsSigned?"Windows envanterinde imzalı":"İmza bilgisi doğrulanmadı"),12,"#BDCBC1"));
   if(state.HardwarePresent&&state.Drivers.Count==0)panel.Children.Add(T("Bu GPU için etkin sürücü kaydı eşleştirilemedi. Sürücüler bölümünde cihaz hata kodunu kontrol et.",12,"#EBC078"));
   if(state.ExecutablePresent)panel.Children.Add(T("UYGULAMA DOSYASI BULUNDU  /  "+(state.FileVersion.Length>0?state.FileVersion:"Sürüm bilgisi yok")+"\n"+state.Executable,12,"#B9E2A7"));
   else if(state.Apps.Count>0)panel.Children.Add(T("KURULUM KAYDI BULUNDU / "+string.Join(" • ",state.Apps.Select(a=>a.Name+" "+a.Version))+"\nBaşlatılabilir EXE doğrulanamadı. Kayıt kalıntısı veya farklı kurulum yolu olabilir.",12,"#EBC078"));
   else panel.Children.Add(T("Yardımcı uygulama tespit edilmedi. Sürücü ve yardımcı uygulama birbirinden bağımsızdır.",12,"#BDCBC1"));
   var cached=AppDownloads.Cached(recipe);
   if(cached!=null)panel.Children.Add(T($"İNDİRİLMİŞ PAKET / {cached.Checked.ToLocalTime():dd.MM.yyyy HH:mm} • {cached.Bytes/1048576d:0.0} MB\nYayıncı: {cached.Publisher}\nAçmadan önce dosya özeti ve imza yeniden kontrol edilir.",11,"#B9E2A7"));
   var controls=Row(Action("Resmi paketi indir ve doğrula",()=>DownloadGpuToolAsync(state,recipe),state.HardwarePresent),Link("Üreticinin resmi sayfası  ↗",tool.SupportUrl));
   if(state.ExecutablePresent)controls.Children.Add(Action("Uygulamayı aç / güncellemeleri kontrol et",async()=>{
    var publisher=await DriverService.SignatureAsync(state.Executable);
    if(!publisher.Contains(tool.Publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Uygulamanın imza yayıncısı beklenen üretici değil.");
    Commands.Open(state.Executable);Journal.Write("GPU uygulaması açıldı",tool.Name+" / "+state.FileVersion);
    StatusLabel.Text="Üretici uygulamasında sürücü güncelleme ve oyun ayarlarını kontrol et. FromZero kurulu sürücüyü gösterir; güncellik sonucu varsaymaz.";
   }));
   if(state.ExecutablePresent)controls.Children.Add(Action("Kurulu dosyanın imzasını doğrula",async()=>{
    var publisher=await DriverService.SignatureAsync(state.Executable);
    if(!publisher.Contains(tool.Publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Kurulu dosyanın yayıncısı beklenen üreticiyle eşleşmiyor.");
    Detail("GPU uygulaması doğrulandı",tool.Name+"\nSürüm: "+state.FileVersion+"\nDosya: "+state.Executable+"\n\nWindows güven doğrulaması başarılı.\nYayıncı: "+publisher);
   }));
   if(tool.Id=="intel"&&state.ExecutablePresent)controls.Children.Add(Link("Intel güncelleme tarayıcısını aç","https://www.intel.com/content/www/us/en/support/detect.html"));
   if(cached!=null)controls.Children.Add(Action("İndirilen paketi kontrol et / kur",()=>OfferGpuInstallAsync(recipe,cached)));
   panel.Children.Add(controls);
   var instructions=tool.Id switch{
    "nvidia"=>"NVIDIA App: sürücü güncellemeleri, Game Ready / Studio seçimi ve desteklenen oyunların optimizasyonu uygulamanın kendi arayüzünde yönetilir.",
    "amd"=>"AMD Auto-Detect, donanımını ve Windows sürümünü kontrol edip uygun Adrenalin/sürücü paketlerini bulur. İndirilen küçük kurucu tam ekran kartı sürücüsü değildir; internet gerektirir. AMD Software içinde grafik ve oyun profillerini yönetebilirsin.",
    _=>"Intel DSA, desteklenen Intel ürünlerinde uygun güncellemeleri tarar. Her Intel GPU ve donanım nesli desteklenmeyebilir; üretici sonucunu kontrol et."
   };
   panel.Children.Add(T(instructions,12,"#B0C1B9"));
   Page.Children.Add(Card(panel));
  }
 }
 private async Task DownloadGpuToolAsync(GpuToolState state,DownloadRecipe recipe)
 {
  if(!state.HardwarePresent&&!Confirm(state.Tool.Vendor+" ekran kartı bu bilgisayarda tespit edilmedi. Resmi aracı yine de indirmek istiyor musun?","Donanım eşleşmesi"))return;
  await AppDownloads.DownloadAsync(recipe,new Progress<string>(s=>OperationTitle.Text=s));
  var cached=AppDownloads.Cached(recipe)??throw new IOException("İndirilen paket kaydı okunamadı.");
  Navigate(currentPage);await OfferGpuInstallAsync(recipe,cached);
 }
 private async Task OfferGpuInstallAsync(DownloadRecipe recipe,VerifiedDownload cached)
 {
  OperationTitle.Text="İndirilen paketin bütünlüğü ve üretici imzası tekrar kontrol ediliyor…";
  await AppDownloads.VerifyCachedAsync(recipe,cached);
  if(!Confirm(recipe.Name+"\n\nDoğrulanan yayıncı: "+cached.Publisher+"\nBoyut: "+(cached.Bytes/1048576d).ToString("0.0")+" MB\nSHA-256: "+cached.Hash+"\n\nÖnce sürücülerin yedeği alınacak, ardından üreticinin kurulum penceresi açılacak. Donanım uyumluluğu ve güncelleme seçimi kurucuda yapılır. Ekran kesintisi/yeniden başlatma gerekebilir. Kurucu açılsın mı?","GPU uygulaması kurulumu"))return;
  await driverService.BackupAsync(Path.Combine(LocalStore.FilePath("Backups"),DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
  // Recheck after backup: another process must not silently replace an already verified installer.
  await AppDownloads.VerifyCachedAsync(recipe,cached);Commands.Open(cached.Path);
  Journal.Write("GPU üretici kurucusu açıldı",recipe.Name+"\n"+cached.Path);
  Detail("Kurulum başlatıldı","Üreticinin penceresinde devam et. Bittiğinde GPU ve uygulamaları yeniden kontrol et düğmesine bas. Kurucu açılması başarılı kurulum sayılmaz.");
 }
}
