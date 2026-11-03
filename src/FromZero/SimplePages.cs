using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace FromZero;
public partial class MainWindow
{
 private NetworkHealth? networkHealth;
 private ComponentInventory? components;
 private readonly PerformanceProfile performanceProfile=new();
 private Expander Advanced(string label,Action build)
 {
  var start=Page.Children.Count;build();var children=Page.Children.Cast<UIElement>().Skip(start).ToList();
  foreach(var child in children)Page.Children.Remove(child);
  var content=new StackPanel();foreach(var child in children)content.Children.Add(child);
  var expander=new Expander{Header=label,Content=content,Foreground=B("#B9CBC1"),Margin=new Thickness(0,8,0,12),Padding=new Thickness(8),IsExpanded=false};Page.Children.Add(expander);return expander;
 }
 private void GroupSince(int start,string label)
 {
  var children=Page.Children.Cast<UIElement>().Skip(start).ToList();foreach(var child in children)Page.Children.Remove(child);
  var content=new StackPanel{Margin=new Thickness(0,12,0,0)};foreach(var child in children)content.Children.Add(child);
  Page.Children.Add(new Expander{Header=label,Content=content,Foreground=B("#B9CBC1"),Margin=new Thickness(0,8,0,12),Padding=new Thickness(8)});
 }
 private void Network()
 {
  Header("BAĞLANTI", "Wi-Fi'yi kontrol et. Sorunu onar.", "Önce arızayı bulur, sonra uygun onarımı uygular.");
  ConnectionQualityCard();
  DnsCard();
  var summary=networkHealth==null?"Henüz kontrol edilmedi.":string.Join("\n",networkHealth.Findings);
  Page.Children.Add(Card(Stack(T("Bağlantı durumu",20,"#B9E2A7",true),T(summary,14,"#D2DED6"),Row(Action("Kontrol et",async()=>{networkHealth=await NetworkRepair.InspectAsync();Navigate("network");}),Action("Önerilen onarımı uygula",RepairRecommendedAsync,true)))));
  Page.Children.Add(Card(Stack(T("Wi-Fi / Ethernet aktarım optimizasyonu",18,"#B9E2A7",true),T("Windows TCP alım penceresini kontrol eder. Kısıtlanmışsa Normal seviyesine alabilir; önceki ayarı geri yükleyebilirsin.",12,"#B9CBC1"),Row(Action("Aktarım ayarını optimize et",async()=>{
   var current=await TcpOptimizer.ReadAsync();
   if(current!="normal"&&!Confirm("Mevcut TCP otomatik ayarı: "+current+"\nNormal seviyesine alınacak. Tüm TCP bağlantılarını etkiler; önceki değer saklanır. Devam edilsin mi?"))return;
   Detail("Aktarım optimizasyonu",await TcpOptimizer.ApplyAsync());Navigate("network");
  }),Action("Aktarım profilini geri al",async()=>{Detail("TCP profili",await TcpOptimizer.RestoreAsync());Navigate("network");})))));
  if(networkHealth!=null){
   foreach(var adapter in networkHealth.Adapters.OrderByDescending(a=>a.IsWifi)){
    var a=adapter;var buttons=Row(Action("Yeniden başlat",()=>RepairAdapterAsync(a,"restart","Seçilen adaptör yeniden başlatılacak. Bağlantı geçici kesilecek.")));
    if(a.Dhcp)buttons.Children.Add(Action("IP adresini yenile",()=>RepairAdapterAsync(a,"renew","Bu adaptörün DHCP adresi yenilenecek. Bağlantı geçici kesilebilir.")));
    if(!a.Enabled)buttons.Children.Add(Action("Adaptörü etkinleştir",()=>RepairAdapterAsync(a,"enable","Bu adaptör etkinleştirilecek.")));
    var type=a.IsWifi?"Wi-Fi":"Ethernet";
    var detail=Stack(T(a.Name.Equals(type,StringComparison.OrdinalIgnoreCase)?a.Name:a.Name+" / "+type,16,"#D8E8D3",true),T(a.Description+"\n"+(a.Connected?"Bağlı":"Bağlı değil")+" • "+(a.Dhcp?"Otomatik IP":"Statik IP / bilgi yok"),12,"#B9CBC1"),buttons);
    Page.Children.Add(Card(detail));
   }
  }
  Advanced("Diğer onarım ve teşhis araçları",()=>{
   Page.Children.Add(Card(Stack(T("Bağlantı yazılımını sıfırla",17,"#EBC078",true),T("Winsock kataloğunu sıfırlar. Yeniden başlatma gerekir; VPN/ağ yazılımlarını etkileyebilir. Statik IP ve DNS sunucusu değiştirilmez.",12,"#B9CBC1"),Row(Action("Winsock sıfırla",async()=>{
    if(networkHealth==null)networkHealth=await NetworkRepair.InspectAsync();var a=networkHealth.Adapters.FirstOrDefault();if(a==null)throw new InvalidOperationException("Fiziksel adaptör bulunamadı.");
    await RepairAdapterAsync(a,"winsock","Winsock sıfırlanacak. Yeniden başlatma gerekir; VPN/ağ yazılımları etkilenebilir. Devam edilsin mi?");
   })))));
   LegacyNetwork();
  });
 }
 private async Task RepairRecommendedAsync()
 {
  var health=await NetworkRepair.InspectAsync();networkHealth=health;
  var adapter=health.Adapters.OrderByDescending(a=>a.IsWifi).ThenByDescending(a=>a.Connected).FirstOrDefault()??throw new InvalidOperationException("Fiziksel ağ adaptörü bulunamadı. Sürücüler bölümünü kontrol et.");
  var plan=NetworkRepair.Plan(health,adapter);
  if(plan.Count==0){Navigate("network");Detail("Bağlantı kontrolü","Otomatik düzeltilebilecek bir yapılandırma sorunu tespit edilmedi. Modem, sinyal, kablo veya servis sağlayıcısı kaynaklı sorunlar için ayrıntılı raporu kontrol et. Gerekirse adaptörü yeniden başlat seçeneğini kullan.");return;}
  if(!Confirm(adapter.Name+" için:\n\n"+string.Join("\n",plan.Select(s=>"• "+s.Label))+"\n\n"+(plan.Any(s=>s.Disconnects)?"Bağlantı geçici kesilebilir. ":"")+"Windows yönetici izni ister. Devam edilsin mi?","Önerilen ağ onarımı"))return;
  var before=await TryMeasureConnectionAsync(adapter);
  var results=new List<string>();foreach(var step in plan){OperationTitle.Text=step.Label+"…";results.Add(await NetworkRepair.ApplyAsync(step.Id,adapter.Id));}
  networkHealth=await NetworkRepair.InspectAsync();var fresh=networkHealth.Adapters.FirstOrDefault(a=>a.Id==adapter.Id);
  var after=fresh==null?null:await TryMeasureConnectionAsync(fresh);connectionQuality=after;
  connectionRepairSummary=$"System DNS: {health.DnsWorks} → {networkHealth.DnsWorks}\nDirect TCP: {health.InternetWorks} → {networkHealth.InternetWorks}\n\nBefore repair\n"+(before==null?"No valid adapter measurement":NetworkQuality.Describe(before))+"\n\nAfter repair\n"+(after==null?"No valid adapter measurement":NetworkQuality.Describe(after));
  Navigate("network");Detail("Onarım sonrası kontrol",string.Join("\n",results)+"\n\n"+string.Join("\n",networkHealth.Findings)+"\n\n"+connectionRepairSummary);
 }
 private async Task RepairAdapterAsync(NetworkAdapterState adapter,string action,string description)
 {
  if(!Confirm(adapter.Name+"\n\n"+description+"\nDevam edilsin mi?","Ağ onarımı"))return;
  var result=await NetworkRepair.ApplyAsync(action,adapter.Id);
  if(action!="winsock")networkHealth=await NetworkRepair.InspectAsync();Navigate("network");Detail("Ağ işlemi",result+(networkHealth==null?"":"\n\n"+string.Join("\n",networkHealth.Findings)));
 }
 private void Performance()
 {
  Header("PERFORMANS", "FPS için ekranını ve GPU'nu hazırla.", "Windows, NVIDIA 3D tercihleri ve ekran yenileme hızı. Değişiklikleri incele, uygula, geri al.");
  Page.Children.Add(Card(Stack(T("RAM optimizer",21,"#B9E2A7",true),T("Canlı bellek takibi, seçili uygulamalarda RAM boşaltma ve oyun öncesi bakım.",13,"#B9CBC1"),Row(Action("RAM optimizer'ı aç",OpenMemoryAsync,true)))));
  var running=Row();foreach(var process in InventoryService.Processes().Take(4))running.Children.Add(AppIcons.Heading(process.Name,AppIcons.ProcessPath(process.Id),Stack(T(process.Name,13,"#D8E8D3",true),T($"{process.Memory/1048576d:0} MB",12,"#9AAFAE"))));
  Page.Children.Add(Card(Stack(T("Running applications",18,"#B9E2A7",true),T("Local file icons, read without launching applications.",12,"#9AAFAE"),running)));
  var hasNvidia=devices.Any(d=>d.Present&&GpuSoftware.Vendor(d)=="NVIDIA");
  var active=performanceProfile.Backup;
  if(!new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))Page.Children.Add(Row(Action("Gerekirse yönetici olarak aç",()=>{
   if(gameService.Session!=null)throw new InvalidOperationException("Önce oyun oturumunu bitir.");
   var info=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",Arguments="--elevated"};
   using var process=System.Diagnostics.Process.Start(info);Dispatcher.BeginInvoke(new Action(()=>Close()));return Task.CompletedTask;
  })));
  if(active!=null)Page.Children.Add(Card(Stack(T("Performans profili etkin",22,"#B9E2A7",true),T("Uygulama tarihi: "+active.Created.ToLocalTime().ToString("g"),12,"#B9CBC1"),Row(Action("Önceki ayarlara dön",async()=>{if(gameService.Session!=null)throw new InvalidOperationException("Önce oyun oturumunu bitir.");Detail("Performans profili",await performanceProfile.RestoreAsync());Navigate("performance");},true)))));
  else{
   var power=new CheckBox{Content="Windows Yüksek Performans planı (mevcutsa)",IsChecked=true};
   var animations=new CheckBox{Content="Windows pencere animasyonlarını azalt",IsChecked=false};
   var gpuPower=new CheckBox{Content="NVIDIA güç yönetimi: maksimum performansı tercih et",IsChecked=hasNvidia,IsEnabled=hasNvidia};
   var quality=new CheckBox{Content="NVIDIA doku filtreleme: yüksek performans (kalite düşebilir)",IsChecked=hasNvidia,IsEnabled=hasNvidia};
   var refresh=new CheckBox{Content="NVIDIA tercih edilen yenileme hızı: en yüksek",IsChecked=hasNvidia,IsEnabled=hasNvidia};
   var vsync=new CheckBox{Content="NVIDIA VSync'i kapat (yırtılma olabilir; G-Sync akıcılığı değişebilir)",IsChecked=false,IsEnabled=hasNvidia};
   var panel=Stack(T("Tek tık FPS profili",22,"#B9E2A7",true),T(hasNvidia?"NVIDIA algılandı • Seçilen genel 3D tercihleri NVAPI ile kaydedilir.":"NVIDIA algılanmadı • Windows ve ekran ayarlarını kullanabilirsin.",13,"#B9CBC1"),new Expander{Header="Profil seçenekleri",Foreground=B("#B9CBC1"),Content=Stack(power,animations,gpuPower,quality,refresh,vsync),Margin=new Thickness(0,8,0,8)},Row(Action("FPS profilini uygula",async()=>{
    if(gameService.Session!=null)throw new InvalidOperationException("Önce oyun oturumunu bitir.");
    var options=new PerformanceOptions(power.IsChecked==true,animations.IsChecked==true,hasNvidia&&gpuPower.IsChecked==true,hasNvidia&&quality.IsChecked==true,hasNvidia&&refresh.IsChecked==true,hasNvidia&&vsync.IsChecked==true);
    if(!options.Power&&!options.Animations&&NvidiaSettings.Desired(options).Count==0)throw new InvalidOperationException("En az bir ayar seç.");
    var plan=(options.Power?"• Windows: mevcutsa Yüksek Performans\n":"")+(options.Animations?"• Windows pencere animasyonları kapalı\n":"")+string.Join("\n",NvidiaSettings.Desired(options).Select(s=>"• NVIDIA "+NvidiaSettings.Name(s.Key)+": "+NvidiaSettings.Describe(new(s.Key,s.Value,true,0,""))));
    if(!Confirm(plan+"\n\nNVIDIA ayarları GENEL 3D profilini etkiler; oyuna özel NVIDIA tercihleri bunu geçersiz kılabilir. Doku kalitesi azalabilir; ısı/güç tüketimi artabilir. VSync kapalıysa yırtılma olabilir. G-Sync/VRR için açık VSync + Reflex + uygun FPS sınırı daha akıcı olabilir.\n\nÖnceki ayarlar yedeklenir. Gerçek ekran Hz'si alttaki ayrı denemeyle değişir. FPS kazanımı donanım/oyuna bağlı. Uygulansın mı?","FPS profili"))return;
    Detail("Uygulanan ayarlar",await performanceProfile.ApplyAsync(options));Navigate("performance");
   },true)));
   Page.Children.Add(Card(panel));
  }
  if(hasNvidia)Page.Children.Add(Row(Action("NVIDIA ayarlarını oku",async()=>{var values=await NvidiaSettings.InspectAsync();Detail("NVIDIA genel 3D ayarları",string.Join("\n",values.Select(v=>v.Name+": "+NvidiaSettings.Describe(v)+(v.UserOverride?" / özel":" / varsayılan"))));})));

  Advanced("Ekran yenileme hızı",DisplayControls);
  Page.Children.Add(Row(Action("Başlangıç uygulamaları",()=>{Navigate("startup");return Task.CompletedTask;}),Action("Disk temizliği",()=>{Navigate("cleanup");return Task.CompletedTask;}),Action("Oyun bileşenlerini kontrol et",ScanComponentsAsync),Action("GPU araçları",()=>{Navigate("gaming");return Task.CompletedTask;})));

  Advanced("Ölçümler, bellek kullanımı ve bakım",LegacyPerformance);
 }
 private void PreviousUpdateHub()
 {
  Header("GÜNCELLEMELER", "Sürücüler ve oyun bileşenleri.", "Donanım sürücüleri, DirectX kütüphaneleri ve oyun çalışma ortamları tek yerde.");
  Page.Children.Add(Card(Stack(T(scanned?$"{devices.Count} cihaz kontrol edildi":"Donanım taranıyor…",21,"#B9E2A7",true),T(updates.Count>0?$"{updates.Count} güncelleme sunuluyor.":"Güncellemeleri görmek için çevrimiçi tarama başlat.",13,"#D2DED6"),Row(Action("Sürücü güncellemesi ara",ScanDriverUpdatesAsync,true),Action("Oyun bileşenlerini kontrol et",ScanComponentsAsync)))));
  if(updates.Count>0){
   foreach(var u in updates){var update=u;
    Page.Children.Add(Card(Stack(T(update.Title,15,"#D8E8D3",true),T(update.Manufacturer+" • "+update.Model,12,"#B9CBC1"),Row(Action(update.IsFirmware?"Üretici üzerinden işlem":"İncele ve kur",async()=>{
     if(update.IsFirmware){Detail("Firmware",update.Title+"\nBu paket cihaz üreticisinin resmi güncelleme akışıyla kurulmalı.");return;}
     if(update.Eula.Length>0)Detail("Üretici lisansı",update.Eula);
     if(!Confirm(update.Title+"\n\nÖnce sürücü yedeği alınır. Üretici lisansını kabul ederek indirip kurmak istiyor musun? Bağlantı/ekran kesilebilir; yeniden başlatma gerekebilir."))return;
     Detail("Sürücü sonucu",await driverService.InstallUpdateAsync(update));updates=[];await ScanAsync();
    })))));
   }
  }
  if(components!=null)RenderComponents();
  Advanced("Üretici araçları ve çevrimdışı kurulum",()=>{
   Page.Children.Add(Row(Action("Yerel INF seç",PickInfAsync),Action("Üretici kurulum EXE'si",InstallVendorDriverAsync),Action("Sürücüleri yedekle",BackupDriversAsync),Action("Kapsülden sürücü bul",FindCapsuleDriversAsync)));
   GpuTools();
  });
  Advanced("Tüm cihazlar ve sürücü ayrıntıları",LegacyDrivers);
 }
 private async Task ScanComponentsAsync()
 {
  if(software==null)software=await InstalledSoftware.ReadAsync();components=await Task.Run(()=>GameComponents.Scan(software.Apps));Navigate("drivers");
 }
 private void RenderComponents()
 {
  var scan=components!;Section("Oyun çalışma ortamı");
  Page.Children.Add(Card(Stack(T($"{scan.Components.Count} bileşen / mimari kontrolü",19,"#B9E2A7",true),T(scan.Direct3D+"\n"+scan.Components.Count(c=>c.Present)+" mevcut • "+scan.Components.Count(c=>!c.Present)+" tespit edilmedi",13,"#D2DED6"),T("Her eksik kütüphane gerekli değildir. Yalnızca oyunun istediği bileşeni kur. DirectX ek paketi Windows'un DirectX sürümünü yükseltmez.",12,"#B9CBC1"))));
  foreach(var group in scan.Components.GroupBy(c=>c.Group))Advanced(group.Key,()=>{
   foreach(var c in group){var item=c;var controls=Row(Link(item.Source.StartsWith("ms-settings:")?"Windows'tan kontrol et":"Resmi kaynak",item.Source));
    if(item.Recipe!=null)controls.Children.Add(Action(item.Present?"Kurucuyu indir / onar":"İndir ve kur",async()=>{
     var recipe=AppDownloads.Recipes.Single(r=>r.Id==item.Recipe);var downloaded=await AppDownloads.DownloadAsync(recipe,new Progress<string>(s=>OperationTitle.Text=s));var cached=AppDownloads.Cached(recipe)!;
     await AppDownloads.VerifyCachedAsync(recipe,cached);
     if(Confirm(recipe.Name+"\n\nİmza doğrulandı. Üreticinin kurucusu açılsın mı? Kurulum/onarım adımlarını kurucuda seçebilirsin."))Commands.Open(downloaded.Path);
    }));
    Page.Children.Add(Card(Stack(T((item.Present?"✓ ":"○ ")+item.Name+" / "+item.Architecture,14,item.Present?"#B9E2A7":"#EBC078",true),T(item.Evidence,11,"#B9CBC1"),controls)));
   }
  });
 }
}


