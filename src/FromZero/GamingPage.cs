using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FromZero;
public partial class MainWindow
{
 private FrameSummary? baselineFrame;
 private void Gaming()
 {
  Header("05 / OYUN İSTASYONU", "Oyuna geçmeden istasyonu hazırla.", "Oyun profilleri, geri alınabilir sistem ayarları ve gerçek kare süresi karşılaştırması.");
  Page.Children.Add(Row(Action("RAM optimizer / oyun öncesi bakım",OpenMemoryAsync),Action("Repeated FPS lab",()=>{profileComparison=null;Navigate("benchmark");return Task.CompletedTask;}),Action("Inspect a game executable",()=>InspectGameDependenciesAsync(false))));
  GameGraphicsCards();
  Cs2LaunchCard();
  Page.Children.Add(Row(Action("NVIDIA / ekran FPS profili",()=>{Navigate("performance");return Task.CompletedTask;},true),Action("DirectX ve oyun bileşenleri",ScanComponentsAsync)));
  Advanced("Ekran kartı uygulamaları ve sürücüleri",GpuTools);
  var session=gameService.Session;
  if(session!=null)Page.Children.Add(Card(Stack(T("✚  OTURUM ETKİN / "+session.Game,19,"#B9E2A7",true),T("Başlangıç: "+session.Started.ToLocalTime().ToString("HH:mm")+" • "+(session.ProcessName.Length>0?"İzlenen işlem: "+session.ProcessName:"İşlem adı seçilmedi; oturumu elle kapat.")+"\nÖnceki güç planı ve kayıt değerleri yerelde saklandı.",13,"#9AAFAE"),Row(Action("Oturumu bitir ve ayarları geri al",async()=>{Detail("Geri alma",await gameService.RestoreAsync());Navigate("gaming");},true)))));
  var highPower=new CheckBox{Content="Mevcut Yüksek Performans güç planını oturum boyunca kullan"};
  var gameMode=new CheckBox{Content="Mevcut Windows Oyun Modu kayıt tercihini aç (deneysel)"};
  var noRecording=new CheckBox{Content="Mevcut Game DVR kayıt tercihlerini oturum boyunca kapat (deneysel)"};
  var settings=Stack(T("Oturum planın",20,"#B9E2A7",true),highPower,gameMode,noRecording,T("Yüksek Performans, yalnızca bilgisayarda bu güç planı varsa uygulanır; ısı ve güç tüketimi artabilir. Kayıt tercihleri sadece var olan DWORD değerlerine uygulanır. Windows sürümüne göre etkileri değişebilir; Ayarlar ekranından doğrula. Değişen değerler oturum sonunda geri alınır.",12,"#9AAFAE"));
  settings.Children.Add(Row(Action("Oyun başlatmadan modu etkinleştir",async()=>{
   if(!Confirm(GamePlanText(highPower.IsChecked==true,gameMode.IsChecked==true,noRecording.IsChecked==true)+"\n\nOyun işlem adı olmadığı için oturumu elle kapatmalısın. FromZero kapanırken de geri alınır.","Oyun modu planı"))return;
   await PrepareGameMemoryAsync();
   await gameService.BeginAsync("Manuel oyun modu","",highPower.IsChecked==true,gameMode.IsChecked==true,noRecording.IsChecked==true);Navigate("gaming");
  },true),Action("Güç planlarını incele",async()=>{var r=await Commands.RunAsync("powercfg.exe",["/list"]);Detail("Güç planları",r.Output);})));Advanced("Oyun oturumu ayarları",()=>Page.Children.Add(Card(settings)));
  var libraryStart=Page.Children.Count;Section("Oyun kütüphanen");
  Page.Children.Add(Row(Action("Steam oyunlarını bul",()=>{var found=GameService.DetectSteam();foreach(var p in found)if(!GameService.ReadProfiles().Any(g=>g.SteamId==p.SteamId))GameService.SaveProfile(p);Journal.Write("Steam oyun taraması",found.Count+" oyun bulundu.");Navigate("gaming");if(found.Count==0)AppMessage.Show(this,"Steam kitaplıklarında kurulu oyun bulunamadı. Oyun profili ekleyebilirsin.","FromZero");return Task.CompletedTask;}),Action("Oyun profili ekle",()=>{EditGameProfile(null);return Task.CompletedTask;})));
  var profiles=GameService.ReadProfiles();if(profiles.Count==0)Page.Children.Add(Card(T("Henüz oyun profili yok. Steam kitaplığını tara veya oyunun EXE dosyasını seçerek profil ekle.",14,"#9AAFAE")));
  foreach(var profile in profiles){
   var p=profile;
   Page.Children.Add(Card(Stack(T("✚  "+p.Name,18,"#D8E8D3",true),T((p.SteamId.Length>0?"STEAM / "+p.SteamId:"YEREL OYUN")+" • "+(p.ProcessName.Length>0?"İzleme: "+p.ProcessName:"Elle oturum bitirme")+"\nBaşlatma seçenekleri: "+(p.Arguments.Length>0?p.Arguments:"Varsayılan"),12,"#9AAFAE"),Row(Action("Hazırla ve başlat  →",async()=>{
    if(!Confirm(p.Name+"\n\n"+GamePlanText(highPower.IsChecked==true,gameMode.IsChecked==true,noRecording.IsChecked==true)+"\n\nBaşlatma seçenekleri:\n"+(p.Arguments.Length>0?p.Arguments:"Varsayılan")+"\n\n"+(p.ProcessName.Length>0?"Oyun işlemi görüldükten sonra kapanınca ayarlar geri alınır. İşlem 5 dakikada görülmezse oturum geri alınır.":"Otomatik bitiş için işlem adı yok. Oturumu elle bitirmelisin.")+"\nDevam edilsin mi?","Oyuna hazırlan"))return;
    GameService.SplitArguments(p.Arguments);
    await PrepareGameMemoryAsync();
    await gameService.BeginAsync(p.Name,p.ProcessName,highPower.IsChecked==true,gameMode.IsChecked==true,noRecording.IsChecked==true);
    try{using var process=GameService.Launch(p);}catch{await gameService.RestoreAsync();throw;}Navigate("gaming");
   },true),Action("Profili düzenle",()=>{EditGameProfile(p);return Task.CompletedTask;}),Action("Ayar önerileri",()=>{Detail(p.Name+" / oyun içi ayarlar",GameAdvice(p));return Task.CompletedTask;})))));
  }
  GroupSince(libraryStart,"Oyun kütüphanesi");var toolsStart=Page.Children.Count;Section("Oyun öncesi son kontroller");
  Page.Children.Add(Card(Stack(T("Arka plan uygulamalarını seçerek kapat",18,bold:true),T("Seçtiğin pencerelere normal kapatma isteği gönderilir. Kaydetme sorularını uygulamanın kendisi gösterir; işlemler zorla sonlandırılmaz.",13,"#9AAFAE"),Row(Action("Açık pencereleri incele",ReviewBackgroundAsync),Action("Başlangıç yönetimi",()=>{Navigate("startup");return Task.CompletedTask;})))));
  Page.Children.Add(Row(Action("Windows Oyun Modu",()=>{Commands.Open("ms-settings:gaming-gamemode");return Task.CompletedTask;}),Action("GPU ve pencere optimizasyonu",()=>{Commands.Open("ms-settings:display-advancedgraphics");return Task.CompletedTask;}),Action("Ekran yenileme hızı",()=>{Commands.Open("ms-settings:display-advanced");return Task.CompletedTask;}),Action("Oyun yakalama ayarları",()=>{Commands.Open("ms-settings:gaming-gamedvr");return Task.CompletedTask;})));
  Section("FPS laboratuvarı");
  Page.Children.Add(Card(Stack(T("Kare sürelerini karşılaştır",19,"#B9E2A7",true),T("PresentMon CSV kaydını içe aktar. İşlem ve swapchain akışları ayrı hesaplanır. En az 100 geçerli kare gerekir. Ortalama FPS = 1000 / ortalama kare süresi; %1 düşük FPS, en yavaş %1 karenin ortalamasından hesaplanır. Bu ölçüm CPU sunum temposudur; ekranda gösterilen FPS ile aynı olmayabilir.",13,"#9AAFAE"),Row(Action("Önceki FPS kaydını yükle",()=>ImportFramesAsync(true)),Action("Sonraki kaydı karşılaştır",()=>ImportFramesAsync(false)),Link("PresentMon resmi projesi  ↗","https://github.com/GameTechDev/PresentMon")))));
  Page.Children.Add(T("Aynı oyun, sahne, çözünürlük ve ölçüm süresiyle karşılaştır. Profiller FPS artışı garantilemez. Bu sürümde otomatik oyun içi ölçüm/overlay yok; CSV kayıtlarını karşılaştırır.",12,"#9AAFAE"));GroupSince(toolsStart,"FPS ölçümü ve ek araçlar");
 }
 private static string GamePlanText(bool power,bool mode,bool recording)=>"Oturum değişiklikleri:\n"+(power?"• Yüksek Performans güç planı\n":"• Mevcut güç planı korunur\n")+(mode?"• Mevcut AutoGameModeEnabled değeri 1\n":"")+(recording?"• Mevcut GameDVR_Enabled ve AppCaptureEnabled değerleri 0\n":"")+"\nDeğiştirilen ayarlar önce yedeklenir. Önceki değerler oturum sonunda geri yüklenir.";
 private void EditGameProfile(GameProfile? original)
 {
  var window=new Window{Owner=this,Title="Oyun profili",Width=700,Height=700,Background=B("#142129"),WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new Thickness(24)};
  var name=new TextBox{Text=original?.Name??""};var exe=new TextBox{Text=original?.Executable??""};var args=new TextBox{Text=original?.Arguments??""};var process=new TextBox{Text=original?.ProcessName??""};var steamId=new TextBox{Text=original?.SteamId??""};
  panel.Children.Add(T("Oyuna özel başlatma profili",22,"#B9E2A7",true));
  foreach(var item in new[]{("Oyun adı",name),("Oyun EXE veya steam.exe yolu",exe)}){panel.Children.Add(T(item.Item1,12,"#9AAFAE"));panel.Children.Add(item.Item2);}
  var pick=new Button{Content="EXE seç"};pick.Click+=(_,_)=>{var dlg=new OpenFileDialog{Filter="Uygulama|*.exe"};if(dlg.ShowDialog(window)==true)exe.Text=dlg.FileName;};panel.Children.Add(pick);
  foreach(var item in new[]{("Başlatma seçenekleri (oyunun desteklediği seçenekler)",args),("İzlenecek gerçek oyun işlem adı (.exe olmadan)",process),("Steam App ID (yerel oyun için boş bırak)",steamId)}){panel.Children.Add(T(item.Item1,12,"#9AAFAE"));panel.Children.Add(item.Item2);}
  panel.Children.Add(T("Parametreleri profilinde saklarız ve oyuna iletiriz. Steam'in kendi başlatma ayarları değiştirilmez. Evrensel -high, -threads veya -dx12 paketi eklenmez; her oyunun desteklediği seçenekler farklıdır. Steam/anti-cheat başlatıcısı gereken oyunlarda resmi başlatıcı kullanılmalı.",12,"#EBC078"));
  var save=new Button{Content="Profili kaydet",Margin=new Thickness(0,18,0,0)};save.Click+=(_,_)=>{
   try{
    if(string.IsNullOrWhiteSpace(name.Text)||!File.Exists(exe.Text)||!System.IO.Path.GetExtension(exe.Text).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Geçerli oyun adı ve EXE dosyası seç.");
    if(steamId.Text.Length>0&&!uint.TryParse(steamId.Text,out _))throw new InvalidDataException("Steam App ID sayısal olmalı.");
    var processName=System.IO.Path.GetFileNameWithoutExtension(process.Text.Trim());GameService.SplitArguments(args.Text);
    if(original!=null&&!original.Name.Equals(name.Text,StringComparison.OrdinalIgnoreCase)){var old=GameService.ReadProfiles();old.RemoveAll(p=>p.Name==original.Name);File.WriteAllText(LocalStore.FilePath("games.json"),JsonSerializer.Serialize(old,LocalStore.Json));}
    GameService.SaveProfile(new(name.Text.Trim(),exe.Text,args.Text,processName,steamId.Text.Trim()));window.Close();Navigate("gaming");
   }catch(Exception ex){AppMessage.Show(window,ex.Message,"Oyun profili");}
  };panel.Children.Add(save);window.Content=new ScrollViewer{Content=panel};window.ShowDialog();
 }
 private static string GameAdvice(GameProfile game)
 {
  var common="Ölçüm sırası:\n1. Aynı sahnede varsayılan ayarlarla kayıt al.\n2. Tek seferde bir ayar değiştir.\n3. Ortalama FPS ve %1 düşük FPS'yi karşılaştır.\n\nGPU sınırında: çözünürlük ölçeği, gölgeler, volumetrik efektler ve ray tracing'i sırayla dene. Desteklenen yükseltme yöntemini oyun menüsünden seç.\nCPU sınırında: kalabalık/simülasyon/çizim mesafesi ve arka plan iş yükünü incele.\nDoku kalitesini VRAM kapasitesine göre ayarla.\nEkranın yenileme hızı ve VRR desteğine göre V-Sync/FPS sınırını değerlendir.\n\n";
  return common+(game.SteamId switch{
   "730"=>"Counter-Strike 2: Çözünürlük, MSAA, gölgeler ve desteklenen gecikme azaltma seçeneğini oyun menüsünden dene. Önce mevcut FPS sınırını ve kare tutarlılığını ölç. Source 1 için yazılmış -threads/-high listelerini otomatik uygulama.",
   "570"=>"Dota 2: Efekt/gölge kalitesi ve render ölçeğini ayrı ayrı dene. Vulkan veya DirectX seçimini oyunun güncel menüsü ve donanım desteğine göre karşılaştır.",
   "578080"=>"PUBG: Render ölçeği, gölge ve efektleri karşılaştır. Uzaklık/kenar yumuşatma değişikliklerinin görünürlük üzerindeki etkisini de değerlendir. Başlatıcı ve anti-cheat akışını koru.",
   "1172470"=>"Apex Legends: VRAM bütçesini ekran kartına göre seç. Dinamik çözünürlük ve FPS sınırını aynı test sahnesinde karşılaştır. Oyun sürümünün desteklediği renderer'ı kullan.",
   _=>"Bu oyun için otomatik dosya düzenleme reçetesi yok. Üreticinin desteklediği başlatma seçeneklerini profiline ekle. Grafik ayarlarını yukarıdaki ölçüm sırasıyla karşılaştır."
  });
 }
 private Task ReviewBackgroundAsync()
 {
  var window=new Window{Owner=this,Title="Arka plan pencereleri",Width=760,Height=600,Background=B("#142129"),WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new Thickness(20)};panel.Children.Add(T("Kapatılacak uygulamaları seç",20,"#B9E2A7",true));panel.Children.Add(T("Kaydedilmemiş çalışmalarını kontrol et. Seçimlerin uygulamalara normal kapatma isteği gönderir; iptal edilen veya yanıtsız uygulamalar açık kalır.",13,"#9AAFAE"));
  var selected=new List<(int Id,DateTime Started,CheckBox Check)>();
  foreach(var p in Process.GetProcesses()){using(p){try{if(p.Id==Environment.ProcessId||p.MainWindowHandle==IntPtr.Zero||p.SessionId!=Process.GetCurrentProcess().SessionId)continue;var box=new CheckBox{Content=p.ProcessName+" / "+p.MainWindowTitle,MaxWidth=680};selected.Add((p.Id,p.StartTime,box));panel.Children.Add(box);}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}}}
  var close=new Button{Content="Seçilenlere kapatma isteği gönder",Margin=new Thickness(0,20,0,0)};close.Click+=(_,_)=>{
   var targets=selected.Where(s=>s.Check.IsChecked==true).ToList();if(targets.Count==0)return;if(!Confirm(targets.Count+" uygulamaya normal kapatma isteği gönderilecek. Devam edilsin mi?"))return;
   foreach(var target in targets){try{using var p=Process.GetProcessById(target.Id);if(p.StartTime!=target.Started)continue;var accepted=p.CloseMainWindow();Journal.Write("Uygulamaya kapatma isteği",p.ProcessName+"; istek: "+accepted);}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){Journal.Write("Kapatma isteği",ex.Message,false);}}
   window.Close();
  };panel.Children.Add(close);window.Content=new ScrollViewer{Content=panel};window.ShowDialog();return Task.CompletedTask;
 }
 private async Task ImportFramesAsync(bool baseline)
 {
  var dlg=new OpenFileDialog{Filter="PresentMon CSV|*.csv",Title=baseline?"Önceki ölçüm CSV'si":"Sonraki ölçüm CSV'si"};if(dlg.ShowDialog(this)!=true)return;
  var results=await Task.Run(()=>{using var reader=File.OpenText(dlg.FileName);return FrameAnalysis.Read(reader);});if(results.Count==0)throw new InvalidDataException("En az 100 geçerli kare içeren akış bulunamadı.");
  FrameSummary? chosen=null;
  var window=new Window{Owner=this,Title="Ölçüm akışını seç",Width=800,Height=500,Background=B("#142129"),WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new Thickness(20)};
  panel.Children.Add(T("İşlem ve swapchain akışını seç",20,"#B9E2A7",true));
  foreach(var item in results.Take(30)){var b=new Button{Content=item.Stream+"\n"+item.Frames+" kare / "+item.AverageFps.ToString("0.0")+" FPS"};b.Click+=(_,_)=>{chosen=item;window.Close();};panel.Children.Add(b);}window.Content=new ScrollViewer{Content=panel};window.ShowDialog();if(chosen==null)return;
  var text=$"Akış: {chosen.Stream}\nKare: {chosen.Frames}\nOrtalama: {chosen.AverageFps:0.0} FPS\n%1 düşük: {chosen.LowOnePercent:0.0} FPS\nP99 kare süresi: {chosen.P99Milliseconds:0.00} ms";
  if(baseline){baselineFrame=chosen;await File.WriteAllTextAsync(LocalStore.FilePath("fps-baseline.json"),JsonSerializer.Serialize(chosen,LocalStore.Json));text+="\n\nReferans ölçüm kaydedildi.";}
  else{
   if(baselineFrame==null&&File.Exists(LocalStore.FilePath("fps-baseline.json")))baselineFrame=JsonSerializer.Deserialize<FrameSummary>(await File.ReadAllTextAsync(LocalStore.FilePath("fps-baseline.json")),LocalStore.Json);
   if(baselineFrame==null)text+="\n\nKarşılaştırma için önce referans kayıt yükle.";
   else text+=$"\n\nReferans: {baselineFrame.Stream}\nOrtalama FPS değişimi: %{(chosen.AverageFps/baselineFrame.AverageFps-1)*100:+0.0;-0.0;0.0}\n%1 düşük değişimi: %{(chosen.LowOnePercent/baselineFrame.LowOnePercent-1)*100:+0.0;-0.0;0.0}\n\nSahne ve ayarlar aynı değilse değişim optimizasyona bağlanamaz.";
  }
  Journal.Write("FPS ölçümü içe aktarıldı",text);Detail("FPS laboratuvarı",text);
 }
}

