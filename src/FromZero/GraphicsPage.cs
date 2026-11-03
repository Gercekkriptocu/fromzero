using System.Windows;
using System.Windows.Controls;
namespace FromZero;
public partial class MainWindow
{
 private readonly GameGraphicsService graphicsService=new();
 private void GameGraphicsCards()
 {
  Section("Tek tık görüntü profilleri");
  var targets=graphicsService.Discover();
  var cards=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=340,Gap=14};
  foreach(var (game,name,source) in new[]{("cs2","Counter-Strike 2",GameGraphicsService.NvidiaSource),("valorant","VALORANT",GameGraphicsService.RiotSource)}){
   var choices=targets.Where(t=>t.Game==game).OrderByDescending(t=>System.IO.File.GetLastWriteTimeUtc(t.Path)).ToList();
   var target=new ComboBox{ItemsSource=choices,DisplayMemberPath="Label",SelectedIndex=choices.Count>0?0:-1,MinHeight=34,Margin=new Thickness(0,10,0,10)};
   var preset=new ComboBox{ItemsSource=new[]{"Rekabetçi FPS","Dengeli görüntü"},SelectedIndex=0,MinHeight=34,Margin=new Thickness(0,0,0,8)};
   var vsync=new CheckBox{Content="VSync'i kapat (görüntü yırtılması olabilir)",IsChecked=false};
   var status=T(choices.Count==0?"Ayar dosyası bulunamadı. Oyunu bir kez açıp görüntü ayarlarını kaydet.":choices.Count+" hesap / ayar dosyası bulundu.",12,"#B9CBC1");
   var planLabel=T("",12,"#B9E2A7");
   void Refresh(){if(target.SelectedItem is GraphicsTarget current){var backup=graphicsService.Active(current);status.Text=backup==null?"Hazır • Oyun kapalıyken uygula.":"Profil etkin • "+backup.Changes.Count+" ayar yedekli.";}planLabel.Text=preset.SelectedIndex==0?"Doku, efekt ve kenar yumuşatma yükünü azaltır.":"Doku ve kenar yumuşatmada orta seviye tercih eder.";}
   target.SelectionChanged+=(_,_)=>Refresh();preset.SelectionChanged+=(_,_)=>Refresh();
   var apply=Action("FPS ayarlarını uygula",async()=>{
    if(target.SelectedItem is not GraphicsTarget selected)throw new InvalidOperationException("Önce oyunu açıp görüntü ayarını kaydet ve tekrar kontrol et.");
    var plan=graphicsService.Preview(selected,preset.SelectedIndex==0?"competitive":"balanced",vsync.IsChecked==true);
    if(plan.Changes.Count==0){Detail("Görüntü profili","Tanınan ayarlar zaten bu profille aynı. "+plan.Unavailable.Count+" anahtar dosyada bulunmadı.");return;}
    var summary=string.Join("\n",plan.Changes.Select(c=>"• "+c.Key+": "+c.Before+" → "+c.After));
    if(!Confirm(selected.Label+"\n\n"+summary+"\n\nGörüntü ayrıntısı azalabilir. Çözünürlük, yenileme hızı ve giriş ayarları korunur. Dosyanın tamamı önce yedeklenir. Oyun yeni ayarları başlangıçta okur; sürüm/bulut eşitlemesi bazı değerleri geri yazabilir. FPS kazanımı ölçülmedi. Uygulansın mı?","Oyun görüntü profili"))return;
    await graphicsService.ApplyAsync(plan);Navigate("gaming");Detail("Görüntü ayarları kaydedildi",plan.Changes.Count+" ayar dosyaya yazıldı ve yedeklendi. Oyunu açıp Video menüsünden ve aynı sahnede FPS ölçümüyle doğrula.\n\nDosyada bulunmayan anahtarlar eklenmedi: "+plan.Unavailable.Count);
   },true);apply.IsEnabled=choices.Count>0;
   var restore=Action("Geri al",async()=>{if(target.SelectedItem is not GraphicsTarget selected)return;var backup=graphicsService.Active(selected);if(backup==null){Detail("Görüntü yedeği","Bu hesap için etkin profil yok.");return;}var result=await graphicsService.RestoreAsync(backup);Navigate("gaming");Detail("Görüntü ayarları geri alındı",result.Restored+" değer geri alındı; "+result.Preserved+" dış değişiklik korundu.");});
   var preview=Action("Önizle",()=>{if(target.SelectedItem is not GraphicsTarget current)throw new InvalidOperationException("Ayar dosyası bulunamadı.");var plan=graphicsService.Preview(current,preset.SelectedIndex==0?"competitive":"balanced",vsync.IsChecked==true);Detail(name+" / değişiklik önizlemesi",string.Join("\n",plan.Changes.Select(c=>c.Key+": "+c.Before+" → "+c.After))+"\n\n"+plan.Changes.Count+" değişiklik • "+plan.Unavailable.Count+" anahtar bulunamadı. Önizleme dosya değiştirmez.");return Task.CompletedTask;});preview.IsEnabled=choices.Count>0;
   var actions=Row(apply,preview);
   {
    var crosshair=Action("Pro crosshair catalog",()=>{Navigate(game=="cs2"?"crosshairs":"valorant-crosshairs");return Task.CompletedTask;});crosshair.Content=CrosshairGlyph();crosshair.ToolTip="Pro crosshair catalog";crosshair.Padding=new Thickness(12);System.Windows.Automation.AutomationProperties.SetName(crosshair,"Pro crosshair catalog");
    var restoreAndCatalogue=new StackPanel{Orientation=Orientation.Horizontal};restoreAndCatalogue.Children.Add(restore);restoreAndCatalogue.Children.Add(crosshair);actions.Children.Add(restoreAndCatalogue);
   }
   var compare=Action("Compare profile FPS",()=>target.SelectedItem is GraphicsTarget selected?
    OpenProfileComparisonAsync(selected,preset.SelectedIndex==0?"competitive":"balanced",vsync.IsChecked==true):Task.CompletedTask);
   compare.IsEnabled=choices.Count>0;
   actions.Children.Add(compare);
   cards.Children.Add(Card(Stack(T(name,23,"#B9E2A7",true),status,T("Hesap / ayar dosyası",11,"#B9CBC1"),target,preset,planLabel,vsync,actions)));
   Refresh();
  }
  Page.Children.Add(cards);

 }
 private static UIElement CrosshairGlyph(){var canvas=new System.Windows.Controls.Canvas{Width=20,Height=20};var stroke=B("#B9E2A7");canvas.Children.Add(new System.Windows.Shapes.Ellipse{Width=12,Height=12,Stroke=stroke,StrokeThickness=1.5,Margin=new Thickness(4)});foreach(var (x1,y1,x2,y2) in new[]{(10d,0d,10d,6d),(10d,14d,10d,20d),(0d,10d,6d,10d),(14d,10d,20d,10d)})canvas.Children.Add(new System.Windows.Shapes.Line{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=stroke,StrokeThickness=1.5});return canvas;}
}

