using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Text.Json;

namespace FromZero;

public partial class MainWindow
{
 private void ValorantCrosshairCatalog()
 {
  Header("VALORANT","Top 50 crosshairs","Ascent Mid · Static preview");
  var search=new TextBox{MinHeight=38,ToolTip="Search players or teams"};
  var onlyFavorites=new CheckBox{Content="Favorites only"};
  var detail=new Border{Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,12)};
  var comparisonHost=new Border{Visibility=Visibility.Collapsed};
  var status=T("",13,"#B9E2A7");
  var cards=new ResponsiveTilePanel{MaxColumns=3,MinimumTileWidth=270,Gap=12};
  var path=LocalStore.FilePath("valorant-favorites.json");
  HashSet<string> favorites=[];
  try{if(File.Exists(path))favorites=JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path))??[];}
  catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){}
  Button Button(string title,Action click){var button=new Button{Content=title};button.Click+=(_,_)=>click();return button;}
  Page.Children.Add(Row(Button("← Games",()=>Navigate("gaming")),onlyFavorites));
  Page.Children.Add(T("Search players or teams",13,"#B9CBC1"));
  Page.Children.Add(search);
  Page.Children.Add(status);
  Page.Children.Add(detail);
  Page.Children.Add(comparisonHost);
  Page.Children.Add(cards);

  void Copy(string code,string name)
  {
   Clipboard.SetText(code);
   status.Text=name+" • Code copied. Import it in VALORANT under Settings → Crosshair.";
   StatusLabel.Text=status.Text;
  }
  var compare=CrosshairComparisonPanel(comparisonHost,valorantComparison,
   code=>new ValorantPreview(ValorantCrosshairs.Decode(code),3),Copy,out var refreshComparison);
  refreshComparison();
  TextBox CodeBox(ValorantEntry entry)=>new(){Text=entry.Code,Tag="FromZero.RawText",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MinHeight=60};
  void Expand(ValorantEntry entry)
  {
   detail.Child=Card(Stack(Row(T(entry.Name,22,"#B9E2A7",true),Button("Close preview",()=>detail.Visibility=Visibility.Collapsed)),
    CrosshairTeamLabel(entry.Team),CrosshairTeamDate(entry.TeamChecked),new ValorantPreview(ValorantCrosshairs.Decode(entry.Code),4){Height=280},
    CodeBox(entry),Action("Copy code",()=>{Copy(entry.Code,entry.Name);return Task.CompletedTask;})));
   detail.Visibility=Visibility.Visible;
   detail.BringIntoView();
  }
  void Render()
  {
   cards.Children.Clear();
   var query=search.Text.Trim();
   foreach(var (entry,rank) in ValorantCrosshairs.All.Select((entry,i)=>(entry,i+1))){
    if(onlyFavorites.IsChecked==true&&!favorites.Contains(entry.Id))continue;
    if(!entry.Name.Contains(query,StringComparison.OrdinalIgnoreCase)&&!CrosshairTeamMatches(entry.Team,query))continue;
    var star=Action(favorites.Contains(entry.Id)?"★":"☆",()=>{
     if(!favorites.Add(entry.Id))favorites.Remove(entry.Id);
     File.WriteAllText(path,JsonSerializer.Serialize(favorites));
     Render();
     return Task.CompletedTask;
    });
    star.ToolTip="Toggle favorite";
    cards.Children.Add(Card(Stack(T($"#{rank} {entry.Name}",20,"#B9E2A7",true),CrosshairTeamLabel(entry.Team),CrosshairTeamDate(entry.TeamChecked),
     new ValorantPreview(ValorantCrosshairs.Decode(entry.Code),2){Height=142},CodeBox(entry),
     Row(Action("Copy code",()=>{Copy(entry.Code,entry.Name);return Task.CompletedTask;}),Button("Enlarge",()=>Expand(entry)),
      Button("Compare",()=>compare(new(entry.Id,entry.Name,entry.Team,entry.TeamChecked,entry.Code))),star))));
   }
   if(cards.Children.Count==0)cards.Children.Add(T("No matching players.",14,"#B9CBC1"));
  }
  search.TextChanged+=(_,_)=>Render();
  onlyFavorites.Checked+=(_,_)=>Render();
  onlyFavorites.Unchecked+=(_,_)=>Render();
  Render();
 }
}

public sealed class ValorantPreview(ValorantCrosshair crosshair,double zoom):FrameworkElement
{
 private static readonly ImageBrush Mid=new(new BitmapImage(new Uri("pack://application:,,,/FromZero;component/Assets/ascent-mid.jpg"))){Stretch=Stretch.UniformToFill};
 protected override void OnRender(DrawingContext dc)
 {
  if(ActualWidth<=0||ActualHeight<=0)return;
  var bounds=new Rect(0,0,ActualWidth,ActualHeight);
  dc.PushClip(new RectangleGeometry(bounds));
  dc.DrawRectangle(Mid,null,bounds);
  dc.PushTransform(new TranslateTransform(Math.Floor(ActualWidth/2),Math.Floor(ActualHeight/2)));
  dc.PushTransform(new ScaleTransform(zoom,zoom));
  var color=Color.FromRgb((byte)(crosshair.Rgb>>16),(byte)(crosshair.Rgb>>8),(byte)crosshair.Rgb);
  var shapes=new List<(Rect Rect,double Opacity)>();
  void Add(ValorantLines? lines)
  {
   if(lines==null||lines.Thickness<=0)return;
   var (h,v,t,o,a)=lines;
   if(h>0){shapes.Add((new Rect(o,-t/2,h,t),a));shapes.Add((new Rect(-o-h,-t/2,h,t),a));}
   if(v>0){shapes.Add((new Rect(-t/2,o,t,v),a));shapes.Add((new Rect(-t/2,-o-v,t,v),a));}
  }
  Add(crosshair.Outer);
  Add(crosshair.Inner);
  var dot=crosshair.DotSize;
  if(dot>0)shapes.Add((new Rect(-dot/2,-dot/2,dot,dot),crosshair.DotOpacity));
  if(crosshair.Outline>0)
   foreach(var (shape,opacity) in shapes)
    if(opacity>0){var outline=shape;outline.Inflate(crosshair.OutlineThickness,crosshair.OutlineThickness);dc.DrawRectangle(new SolidColorBrush(Colors.Black){Opacity=crosshair.Outline},null,outline);}
  foreach(var (shape,opacity) in shapes)dc.DrawRectangle(new SolidColorBrush(color){Opacity=opacity},null,shape);
  dc.Pop();dc.Pop();dc.Pop();
 }
}
