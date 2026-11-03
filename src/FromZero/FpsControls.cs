using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FromZero;
public partial class MainWindow
{
 private readonly DisplaySettings displaySettings=new();
 private void Cs2LaunchCard()
 {
  Advanced("CS2 / Steam başlatma seçenekleri",()=>{
   var choice=new ComboBox{ItemsSource=new[]{"Sınırsız FPS (sınırı kaldır)","240 FPS sınırı","300 FPS sınırı","Özel FPS sınırı"},SelectedIndex=0,MinHeight=34};
   var custom=new TextBox{Text="300",MinHeight=34,Visibility=Visibility.Collapsed,ToolTip="60–1000 arası FPS"};
   var output=new TextBox{Text=Cs2LaunchOptions.Build(0),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MinHeight=40,Margin=new Thickness(0,10,0,0)};
   var status=T("Oyun FPS sınırını kaldırır; donanım performansı garantisi değildir.",12,"#B9CBC1");
   int Value(){if(choice.SelectedIndex==3){if(!int.TryParse(custom.Text,out var fps))throw new System.IO.InvalidDataException("Sayısal FPS sınırı gir.");return fps;}return choice.SelectedIndex switch{1=>240,2=>300,_=>0};}
   void Refresh(){custom.Visibility=choice.SelectedIndex==3?Visibility.Visible:Visibility.Collapsed;try{output.Text=Cs2LaunchOptions.Build(Value());status.Text=Value()==0?"Oyun sınırı kalkar; ısı/güç tüketimi artabilir. Menü sınırı 120 FPS.":"Kare temposunu karşılaştırmak için seçtiğin üst sınır kullanılır. Menü sınırı 120 FPS.";}catch(Exception ex){output.Text="";status.Text=ex.Message;}}
   choice.SelectionChanged+=(_,_)=>Refresh();custom.TextChanged+=(_,_)=>Refresh();
   Page.Children.Add(Card(Stack(T("Kopyala. Steam'e yapıştır.",22,"#B9E2A7",true),T("FPS hedefi",12,"#B9CBC1"),choice,custom,output,status,
    Row(Action("Başlatma satırını kopyala",()=>{var line=Cs2LaunchOptions.Build(Value());Clipboard.SetText(line);status.Text="Kopyalandı • Steam → CS2 → Özellikler → Genel → Başlatma seçenekleri";return Task.CompletedTask;},true),
     Action("Nereye yapıştıracağım?",()=>{Detail("CS2 Steam başlatma seçenekleri",Cs2LaunchOptions.Explanation);return Task.CompletedTask;})),
    T("-high / -threads gibi evrensel hızlandırma paketi eklenmez. Sınırsız FPS, G-Sync için her zaman en akıcı tercih değildir. Eski seçeneklerini önce sakla; aynı komutları iki kez ekleme.",12,"#B9CBC1"))));
  });
 }
 private void DisplayControls()
 {
  Section("Ekran / yenileme hızı");
  if(displaySettings.Backup is{} backup){Page.Children.Add(Card(Stack(T(backup.Confirmed?"Ekran profili etkin":"Önceki ekran değişikliği bekliyor",20,"#B9E2A7",true),T(backup.Original.Label+" → "+backup.Applied.Label,13,"#B9CBC1"),Row(Action("Önceki ekran hızına dön",()=>{Detail("Ekran",displaySettings.Restore());Navigate("performance");return Task.CompletedTask;})))));return;}
  try{
   var screens=displaySettings.Read();if(screens.Count==0){Page.Children.Add(Card(T("Etkin ekran modu okunamadı. Windows ekran ayarlarından kontrol et.",13,"#B9CBC1")));return;}
   var cards=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=330};
   foreach(var screen in screens){
    var modes=screen.Modes.OrderByDescending(m=>m.Hz).ToList();
    var choices=new ComboBox{ItemsSource=modes,DisplayMemberPath="Label",SelectedIndex=modes.Count>0?0:-1,MinHeight=34};
    var highest=modes.FirstOrDefault();
    var apply=Action("Seçilen Hz'yi dene",()=>{if(choices.SelectedItem is not DisplayMode target)throw new InvalidOperationException("Desteklenen bir ekran hızı seç.");return TryDisplay(target);},true);apply.IsEnabled=modes.Count>0;
    cards.Children.Add(Card(Stack(T(screen.Name,16,"#B9E2A7",true),T("Şimdi: "+screen.Current.Label,14,"#D2DED6"),T(highest==null?"Desteklenen hız bulunamadı.":"Bu çözünürlükte en yüksek: "+highest.Hz+" Hz",12,"#B9CBC1"),choices,Row(apply),T("Çözünürlük ve ekran yerleşimi korunur. Hz artışı daha akıcı görüntü sağlar; render FPS'sini doğrudan artırmaz. 15 saniyede onaylanmazsa geri alınır.",12,"#B9CBC1"))));
   }
   Page.Children.Add(cards);
  }catch(Exception ex){Page.Children.Add(Card(T("Ekran bilgisi okunamadı: "+ex.Message,13,"#EBC078")));}
  Page.Children.Add(Row(Action("Ekranları yeniden oku",()=>{Navigate("performance");return Task.CompletedTask;}),Link("Windows ekran ayarları","ms-settings:display-advanced")));
 }
 private Task TryDisplay(DisplayMode target)
 {
  displaySettings.Begin(target);
  var window=CreateDialog("Ekran görüntüsü düzgün mü?",560,280);var panel=new StackPanel{Margin=new Thickness(24)};
  var until=DateTimeOffset.UtcNow.AddSeconds(15);var countdown=T("15 saniye içinde onayla; aksi halde önceki hıza dönülür.",15,"#EBC078");
  panel.Children.Add(T(target.Label,24,"#B9E2A7",true));panel.Children.Add(countdown);
  var row=new WrapPanel{Margin=new Thickness(0,18,0,0)};var keep=new Button{Content="Görüntü iyi / koru"};var cancel=new Button{Content="Geri dön",IsCancel=true};row.Children.Add(keep);row.Children.Add(cancel);panel.Children.Add(row);
  bool kept=false;keep.Click+=(_,_)=>{try{displaySettings.Keep();kept=true;window.Close();}catch(Exception ex){countdown.Text=ex.Message;keep.IsEnabled=false;}};cancel.Click+=(_,_)=>window.Close();
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(_,_)=>{var seconds=(int)Math.Ceiling((until-DateTimeOffset.UtcNow).TotalSeconds);countdown.Text=Math.Max(0,seconds)+" saniye • Onay gelmezse önceki ekran hızı geri yüklenir.";if(seconds<=0)window.Close();};
  window.Content=panel;timer.Start();try{window.ShowDialog();}finally{timer.Stop();if(!kept)displaySettings.Restore();Navigate("performance");}return Task.CompletedTask;
 }
}

