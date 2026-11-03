using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace FromZero;

public partial class MainWindow
{
 private string crosshairTab="pros",crosshairSearch="",personalCrosshairCode="";
 private int crosshairBackdrop=3;
 private bool crosshairFavoritesOnly;
 private CrosshairArchive? offlineCrosshairs;
 private static bool IsCrosshairProfileStatus(string team)=>team is "Free Agent" or "Content Creator" or "Retired";
 private static bool CrosshairTeamMatches(string team,string query)=>team.Contains(query,StringComparison.OrdinalIgnoreCase)||
  (IsCrosshairProfileStatus(team)&&Locale.Text(team).Contains(query,StringComparison.OrdinalIgnoreCase));
 private static TextBlock CrosshairTeamLabel(string team)
 {
  var label=T(team,13,"#B9CBC1");
  if(!IsCrosshairProfileStatus(team))label.Tag="FromZero.RawText";
  if(string.IsNullOrWhiteSpace(team))label.Visibility=Visibility.Collapsed;
  return label;
 }
 private void Crosshairs()
 {
  Header("OYUNLAR / CROSSHAIR","Nişangâhını seç.","50 pro oyuncu ve FACEIT global ilk 10 • kodu gör, önizle, kopyala.");
  Button Button(string label,Action action){var button=new Button{Content=label};button.Click+=(_,_)=>action();return button;}
  var status=T("",13,"#B9CBC1");var tabs=Row(Button("← Oyunlar",()=>Navigate("gaming")));
  foreach(var (id,label) in new[]{("pros","50 pro oyuncu"),("faceit","FACEIT ilk 10"),("personal","Kendi kodun")}){
   var tab=id;var button=Button(label,()=>{crosshairTab=tab;crosshairSearch="";crosshairFavoritesOnly=false;Navigate("crosshairs");});if(tab==crosshairTab){button.Background=B("#B9E2A7");button.Foreground=B("#172D2D");}tabs.Children.Add(button);
  }Page.Children.Add(tabs);
  var backdrop=new ComboBox{ItemsSource=new[]{"Gece","Açık duvar","Orman","Dust2 / gerçek görüntü"},SelectedIndex=crosshairBackdrop,MinWidth=200,MinHeight=34};
  Page.Children.Add(Row(T("Önizleme",12,"#B9CBC1"),backdrop));status.Visibility=Visibility.Collapsed;Page.Children.Add(status);
  var detail=new Border{Visibility=Visibility.Collapsed,Margin=new Thickness(0,12,0,14)};Page.Children.Add(detail);
  void Copy(string code,string name){try{CrosshairCode.Decode(code);Clipboard.SetText(code);status.Visibility=Visibility.Visible;status.Text=name+" kodu kopyalandı • CS2 → Ayarlar → Crosshair/Scopes → Paylaş / İçe aktar";StatusLabel.Text=name+" • CROSSHAIR KODU KOPYALANDI";}catch(Exception ex){status.Visibility=Visibility.Visible;status.Text=ex.Message;}}
  var comparisonHost=new Border{Visibility=Visibility.Collapsed};
  Page.Children.Add(comparisonHost);
  var compare=CrosshairComparisonPanel(comparisonHost,csComparison,
   code=>new CrosshairPreview(CrosshairCode.Decode(code),crosshairBackdrop){Zoom=3},Copy,out var refreshComparison);
  refreshComparison();
  void Expand(string code,string name,string team="",string teamChecked=""){var decoded=CrosshairCode.Decode(code);detail.Child=Card(Stack(Row(T(name,22,"#B9E2A7",true),Button("Önizlemeyi kapat",()=>detail.Visibility=Visibility.Collapsed)),CrosshairTeamLabel(team),CrosshairTeamDate(teamChecked),new CrosshairPreview(decoded,crosshairBackdrop){Height=280,Zoom=6},new TextBox{Text=code,IsReadOnly=true,TextWrapping=TextWrapping.Wrap},T($"Uzunluk {decoded.Length}px • Kalınlık {decoded.Thickness}px • Boşluk {decoded.Gap}px • Referans {decoded.ScreenHeight}p",12,"#B9CBC1"),Row(Button("Kodu kopyala",()=>Copy(code,name)))));detail.Visibility=Visibility.Visible;detail.BringIntoView();}
  if(crosshairTab=="personal"){
   var input=new TextBox{Text=personalCrosshairCode,MinHeight=45,TextWrapping=TextWrapping.Wrap};input.TextChanged+=(_,_)=>personalCrosshairCode=input.Text;
   Page.Children.Add(Card(Stack(T("Kendi crosshair kodun",20,"#B9E2A7",true),input,Row(Button("Önizle",()=>{try{Expand(input.Text.Trim(),"Kendi crosshair'in");status.Visibility=Visibility.Visible;status.Text="Geçerli CS2 kodu. Oyun dosyaları değiştirilmedi.";}catch(Exception ex){detail.Visibility=Visibility.Collapsed;status.Visibility=Visibility.Visible;status.Text=ex.Message;}})),T("Yeni biçimde CS ile başlayan kodunu yapıştır.",12,"#B9CBC1"))));
   backdrop.SelectionChanged+=(_,_)=>{crosshairBackdrop=backdrop.SelectedIndex;refreshComparison();if(detail.Visibility==Visibility.Visible){try{Expand(input.Text.Trim(),"Kendi crosshair'in");}catch{detail.Visibility=Visibility.Collapsed;}}};return;
  }
  var favoritesPath=LocalStore.FilePath("crosshair-favorites.json");HashSet<string> favorites=[];try{if(File.Exists(favoritesPath))favorites=JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(favoritesPath))??[];}catch(Exception ex)when(ex is IOException or JsonException){}
  var search=new TextBox{Text=crosshairSearch,MinHeight=38,ToolTip="Search players or teams"};var onlyFavorites=new CheckBox{Content="Yalnız favoriler",IsChecked=crosshairFavoritesOnly};var rows=new ResponsiveTilePanel{MaxColumns=3,MinimumTileWidth=265,Gap=12};
  var pros=offlineCrosshairs?.Pros.Select(CrosshairCatalog.Entry).ToList()??CrosshairCatalog.Read();var ranks=offlineCrosshairs?.Ranking??FaceitCatalog.Read();var faceitCodes=offlineCrosshairs?.Codes.ToList()??FaceitCrosshairs.Read();
  var freshness=T(CrosshairArchives.RankingAge(ranks),12,"#B9CBC1");if(crosshairTab=="faceit")Page.Children.Add(freshness);
  var archiveActions=Row(Action("Export offline catalogue",async()=>{var dialog=new SaveFileDialog{FileName="FromZero-crosshairs.json",Filter="JSON|*.json"};if(dialog.ShowDialog(this)==true)await File.WriteAllTextAsync(dialog.FileName,JsonSerializer.Serialize(offlineCrosshairs??CrosshairArchives.Capture(),LocalStore.Json));}),Action("Open offline catalogue",async()=>{var dialog=new OpenFileDialog{Filter="FromZero catalogue|*.json"};if(dialog.ShowDialog(this)!=true)return;offlineCrosshairs=await CrosshairArchives.ReadAsync(dialog.FileName);Navigate("crosshairs");}));
  if(offlineCrosshairs!=null){Page.Children.Add(T("Offline copy · "+offlineCrosshairs.Exported.ToLocalTime().ToString("g"),13,"#EBC078"));archiveActions.Children.Add(Action("Return to live catalogue",()=>{offlineCrosshairs=null;Navigate("crosshairs");return Task.CompletedTask;}));}
  Page.Children.Add(PreparationExpander("Offline catalogue",archiveActions));
  var controls=Row(onlyFavorites);Page.Children.Add(T("Search players or teams",12,"#B9CBC1"));Page.Children.Add(search);Page.Children.Add(controls);Page.Children.Add(rows);
  Button RefreshButton(string label,Func<Task> action){var button=new Button{Content=label,IsEnabled=offlineCrosshairs==null};button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){status.Visibility=Visibility.Visible;status.Text="Yenilenemedi; önceki kayıt korundu: "+ex.Message;}finally{button.IsEnabled=offlineCrosshairs==null;}};return button;}
  bool Search(string text)=>crosshairSearch.Length==0||text.Contains(crosshairSearch,StringComparison.OrdinalIgnoreCase);
  bool Favorite(string id)=>!crosshairFavoritesOnly||favorites.Contains(id);
  Button Star(string id)=>Button(favorites.Contains(id)?"★":"☆",()=>{try{if(!favorites.Add(id))favorites.Remove(id);File.WriteAllText(favoritesPath,JsonSerializer.Serialize(favorites));Render();}catch(Exception ex){status.Visibility=Visibility.Visible;status.Text=ex.Message;}});
  void AddCodeCard(string title,string code,string name,string favoriteId,string team,string teamChecked,string note,UIElement links){var decoded=CrosshairCode.Decode(code);var heading=T(title,20,"#B9E2A7",true);if(crosshairTab=="faceit")heading.FontFamily=UiTheme.Font;rows.Children.Add(Card(Stack(heading,CrosshairTeamLabel(team),CrosshairTeamDate(teamChecked),new CrosshairPreview(decoded,crosshairBackdrop){Height=140},new TextBox{Text=code,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MinHeight=60},Row(Button("Kodu kopyala",()=>Copy(code,name)),Button("Büyüt",()=>Expand(code,name,team,teamChecked)),Button("Compare",()=>compare(new(favoriteId,name,team,teamChecked,code))),Star(favoriteId)),new TextBlock{Text=note,FontSize=12,Foreground=B("#B9CBC1"),Visibility=string.IsNullOrWhiteSpace(note)?Visibility.Collapsed:Visibility.Visible,Margin=new Thickness(0,4,0,4)},links)));}
  void Render(){rows.Children.Clear();detail.Visibility=Visibility.Collapsed;
   if(crosshairTab=="faceit"){
    foreach(var player in ranks.Entries){
     var entry=FaceitCrosshairs.MatchPlayer(faceitCodes,player);
     var (team,teamChecked)=FaceitCrosshairs.TeamFor(entry);
     if(!Favorite("faceit-"+player.Name)||(!Search(player.Name+" "+entry?.PlayerName)&&!CrosshairTeamMatches(team,crosshairSearch)))continue;
     if(entry==null){rows.Children.Add(Card(Stack(T($"#{player.Rank} {player.Name}",18,"#B9E2A7",true),T($"{player.Elo} ELO / {player.Region}",13,"#B9CBC1"),T("No verified code for this account.",13,"#EBC078"),Row(Link("Match history",player.Profile+"/stats/cs2")))));continue;}
     var item=entry;AddCodeCard($"#{player.Rank}  {player.Name}",item.Code,player.Name,"faceit-"+player.Name,team,teamChecked,$"{player.Elo} ELO / {player.Region} • Oyuncu: {item.PlayerName}\n{CrosshairArchives.CodeAge(item)}\nCrosshair kontrolü: {item.Checked}",Row(RefreshButton("Crosshair'i yenile",async()=>{var updated=await FaceitCrosshairs.RefreshAsync(item.Nickname);faceitCodes[faceitCodes.FindIndex(c=>c.Nickname==item.Nickname)]=updated;Render();status.Visibility=Visibility.Visible;status.Text=player.Name+" crosshair kodu ve önizlemesi yenilendi.";}),new Border()));
    }
   }else foreach(var entry in pros.Where(e=>(Search(e.Snapshot.Name)||CrosshairTeamMatches(e.Snapshot.Team,crosshairSearch))&&Favorite(e.Snapshot.Id)).OrderByDescending(e=>favorites.Contains(e.Snapshot.Id)).ThenBy(e=>e.Snapshot.Name)){
    var item=entry;AddCodeCard(item.Snapshot.Name,item.Code,item.Snapshot.Name,item.Snapshot.Id,item.Snapshot.Team,item.Snapshot.TeamChecked,"",Row(RefreshButton("Kaydı yenile",async()=>{var updated=await CrosshairCatalog.RefreshAsync(item.Snapshot.Id);pros[pros.FindIndex(e=>e.Snapshot.Id==item.Snapshot.Id)]=updated;Render();status.Visibility=Visibility.Visible;status.Text=updated.Snapshot.Name+" kaynak ayarları yenilendi.";}),new Border()));
   }if(rows.Children.Count==0)rows.Children.Add(T("Bu filtrede oyuncu bulunamadı.",14,"#B9CBC1"));
  }
  if(crosshairTab=="faceit"){

   controls.Children.Add(RefreshButton("10 crosshair'i yenile",async()=>{int refreshed=0;var failures=new List<string>();foreach(var rank in ranks.Entries){try{var mapped=FaceitCrosshairs.MatchPlayer(faceitCodes,rank);if(mapped==null){failures.Add(rank.Name);continue;}var updated=await FaceitCrosshairs.RefreshAsync(mapped.Nickname);var index=faceitCodes.IndexOf(mapped);if(index>=0)faceitCodes[index]=updated;refreshed++;status.Visibility=Visibility.Visible;status.Text=$"Crosshair kontrolü {refreshed+failures.Count}/10";}catch(Exception ex){failures.Add(rank.Name+": "+ex.Message);}}Render();status.Visibility=Visibility.Visible;status.Text=$"{refreshed}/10 crosshair yenilendi"+(failures.Count>0?" • Önceki kayıt korundu: "+string.Join("; ",failures):".");}));
   controls.Children.Add(RefreshButton("ELO sıralamasını yenile",async()=>{ranks=await FaceitCatalog.RefreshAsync();freshness.Text=CrosshairArchives.RankingAge(ranks);Render();status.Visibility=Visibility.Visible;status.Text="Public global ranking refreshed · "+ranks.Checked;}));
  }
  search.TextChanged+=(_,_)=>{crosshairSearch=search.Text;Render();};onlyFavorites.Checked+=(_,_)=>{crosshairFavoritesOnly=true;Render();};onlyFavorites.Unchecked+=(_,_)=>{crosshairFavoritesOnly=false;Render();};backdrop.SelectionChanged+=(_,_)=>{crosshairBackdrop=backdrop.SelectedIndex;refreshComparison();Render();};Render();
 }
}

public sealed class CrosshairPreview(CrosshairSettings settings,int backdrop):FrameworkElement
{
 private static readonly Lazy<ImageBrush> dust2=new(()=>{var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri("pack://application:,,,/FromZero;component/Assets/dust2-double-doors.jpg");image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=1600;image.EndInit();image.Freeze();var brush=new ImageBrush(image){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};brush.Freeze();return brush;});
 public double Zoom{get;set;}=4;
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));dc.DrawRectangle(backdrop==3?dust2.Value:new SolidColorBrush(backdrop switch{1=>Color.FromRgb(186,176,151),2=>Color.FromRgb(46,66,47),_=>Color.FromRgb(11,20,29)}),null,new Rect(0,0,ActualWidth,ActualHeight));
  if(backdrop!=3){var muted=new Pen(new SolidColorBrush(Color.FromArgb(25,180,200,180)),1);for(int x=0;x<ActualWidth;x+=24)dc.DrawLine(muted,new Point(x,0),new Point(x,ActualHeight));for(int y=0;y<ActualHeight;y+=24)dc.DrawLine(muted,new Point(0,y),new Point(ActualWidth,y));}
  var s=settings;double z=Zoom,cx=Math.Floor(ActualWidth/2),cy=Math.Floor(ActualHeight/2),gap=s.Gap*z,len=s.Length*z,thick=Math.Max(0,s.Thickness*z);var color=new SolidColorBrush(Color.FromArgb(s.Alpha,s.Red,s.Green,s.Blue));var outline=new SolidColorBrush(Color.FromArgb(s.OutlineAlpha,s.OutlineRed,s.OutlineGreen,s.OutlineBlue));
  void Bar(double x,double y,double width,double height){if(width<=0||height<=0)return;var rect=new Rect(x,y,width,height);if(s.Outline>0){var edge=s.Outline==2?z/2:z;dc.DrawRectangle(outline,null,new Rect(x-edge,y-edge,width+edge*2,height+edge*2));}dc.DrawRectangle(color,null,rect);}
  if(s.Style is 1 or 3){var radius=Math.Max(z,Math.Abs(gap)+len);if(s.Outline>0)dc.DrawEllipse(null,new Pen(outline,thick+2*z),new Point(cx,cy),radius,radius);dc.DrawEllipse(null,new Pen(color,thick),new Point(cx,cy),radius,radius);}
  else if(s.Style==8){var r=Math.Max(z,len+Math.Abs(gap));Bar(cx-r,cy-r,2*r,thick);Bar(cx-r,cy+r-thick,2*r,thick);Bar(cx-r,cy-r,thick,2*r);Bar(cx+r-thick,cy-r,thick,2*r);}
  else if(s.Style!=6){Bar(cx+gap,cy-thick/2,len,thick);Bar(cx-gap-len,cy-thick/2,len,thick);if(!s.TStyle)Bar(cx-thick/2,cy-gap-len,thick,len);Bar(cx-thick/2,cy+gap,thick,len);}
  if(s.Dot||s.Style==6)Bar(cx-thick/2,cy-thick/2,thick,thick);dc.Pop();
 }
}


