using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
namespace FromZero;

// One continuous world is rendered behind every page; changing modules moves the camera.
public sealed class PixelWorld : FrameworkElement
{
 private static readonly Dictionary<string,Brush> Paint=[];
 private static Brush ColorBrush(string color){if(Paint.TryGetValue(color,out var brush))return brush;var created=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));created.Freeze();Paint[color]=created;return created;}
 public static readonly DependencyProperty CameraProperty=DependencyProperty.Register(nameof(Camera),typeof(double),typeof(PixelWorld),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Camera{get=>(double)GetValue(CameraProperty);set=>SetValue(CameraProperty,value);}
 public static readonly DependencyProperty CharacterPositionProperty=DependencyProperty.Register(nameof(CharacterPosition),typeof(double),typeof(PixelWorld),new FrameworkPropertyMetadata(249d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double CharacterPosition{get=>(double)GetValue(CharacterPositionProperty);set=>SetValue(CharacterPositionProperty,value);}
 public static readonly DependencyProperty WalkFrameProperty=DependencyProperty.Register(nameof(WalkFrame),typeof(double),typeof(PixelWorld),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double WalkFrame{get=>(double)GetValue(WalkFrameProperty);set=>SetValue(WalkFrameProperty,value);}
 public static readonly DependencyProperty WalkingProperty=DependencyProperty.Register(nameof(Walking),typeof(bool),typeof(PixelWorld),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool Walking{get=>(bool)GetValue(WalkingProperty);set=>SetValue(WalkingProperty,value);}
 public static readonly DependencyProperty FacingProperty=DependencyProperty.Register(nameof(Facing),typeof(double),typeof(PixelWorld),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Facing{get=>(double)GetValue(FacingProperty);set=>SetValue(FacingProperty,value);}
 public static readonly DependencyProperty FootPositionProperty=DependencyProperty.Register(nameof(FootPosition),typeof(double),typeof(PixelWorld),new FrameworkPropertyMetadata(236d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double FootPosition{get=>(double)GetValue(FootPositionProperty);set=>SetValue(FootPositionProperty,value);}
 public bool Panorama{get;set;}
 private readonly DispatcherTimer walkClock=new(){Interval=TimeSpan.FromMilliseconds(70)};
 private readonly Stopwatch walkTime=new();
 private double destination=249,previous=249,duration;
 private int selectedSector=-1;
 private string preparationState="idle";private int workTick;
 private readonly DispatcherTimer workClock=new(){Interval=TimeSpan.FromMilliseconds(450)};
 public void PreparationWork(int sector,string state)
 {
  if(sector>=0)Travel(sector);preparationState=state;
  if(state=="working"){if(!workClock.IsEnabled){workClock.Tick-=WorkTick;workClock.Tick+=WorkTick;workClock.Start();}}else workClock.Stop();InvalidateVisual();
 }
 private void WorkTick(object? sender,EventArgs e){workTick++;InvalidateVisual();}
 public PixelWorld(){walkClock.Tick+=(_,_)=>{WalkFrame=walkTime.Elapsed.TotalSeconds;if(walkTime.Elapsed.TotalMilliseconds>=duration){Walking=false;walkClock.Stop();walkTime.Stop();}};Unloaded+=(_,_)=>{workClock.Stop();walkClock.Stop();walkTime.Stop();Walking=false;};}
 public static double CurrentCamera{get;private set;}
 public void Travel(int sector)
 {
  if(selectedSector==sector)return;
  previous=CharacterPosition;destination=WorldStations.Door(sector);selectedSector=sector;CurrentCamera=destination-365;
  if(Math.Abs(previous-destination)<1){Camera=CurrentCamera;return;}
  duration=Math.Clamp(650+Math.Abs(previous-destination)*1.4,900,2600);
  Facing=destination>=previous?1:-1;
  var travel=TimeSpan.FromMilliseconds(duration);var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};
  BeginAnimation(CameraProperty,new DoubleAnimation(Camera,CurrentCamera,travel){EasingFunction=ease});
  BeginAnimation(CharacterPositionProperty,new DoubleAnimation(previous,destination,travel){EasingFunction=ease});
  var approach=new DoubleAnimationUsingKeyFrames();approach.KeyFrames.Add(new LinearDoubleKeyFrame(FootPosition,TimeSpan.Zero));approach.KeyFrames.Add(new LinearDoubleKeyFrame(252,TimeSpan.FromMilliseconds(170)));approach.KeyFrames.Add(new LinearDoubleKeyFrame(252,TimeSpan.FromMilliseconds(duration-220)));approach.KeyFrames.Add(new LinearDoubleKeyFrame(236,travel));BeginAnimation(FootPositionProperty,approach);
  Walking=true;walkTime.Restart();walkClock.Start();
 }
 protected override void OnRender(DrawingContext dc)
 {
  if(ActualWidth<1||ActualHeight<1)return;
  const double w=500;double h=Panorama?145:330;double camera=Math.Floor(Camera);
  dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));
  dc.PushTransform(new ScaleTransform(ActualWidth/w,ActualHeight/h));
  if(Panorama)dc.PushTransform(new TranslateTransform(0,-135));
  void R(double x,double y,double width,double height,string color){if(width<=0||height<=0)return;dc.DrawRectangle(ColorBrush(color),null,new Rect(Math.Floor(x),Math.Floor(y),width,height));}
  R(0,0,w,h,"#152C38");R(0,68,w,70,"#1B3A46");R(0,138,w,192,"#204139");
  for(var x=-2;x<502;x+=2){var world=x+camera*.18;var y=105-Math.Sin(world*.019)*17-Math.Sin(world*.049)*9;R(x,y,2,170-y,"#30515B");}
  for(var i=0;i<180;i++){double x=(i*79-camera*.09)%560;if(x<0)x+=560;var y=7+(i*37)%96;R(x,y,i%7==0?2:1,1,i%7==0?"#C1C895":"#587D80");}
  var moon=420-camera*.025;R(moon,32,18,18,"#D4D19F");R(moon-3,36,24,10,"#D4D19F");R(moon+1,34,5,4,"#AFBB96");
  for(var x=-2;x<502;x+=2){var world=x+camera*.44;var y=143-Math.Sin(world*.024+2)*18-Math.Sin(world*.061)*7;R(x,y,2,184-y,"#294D48");}
  void Tree(double x,double y,int s){R(x,y,2,s*6,"#466555");for(var tier=0;tier<5;tier++)R(x-(tier+1)*s/2,y+tier*s,(tier+1)*s+2,s+1,"#133733");}
  for(var index=(int)Math.Floor(camera*.7/20)-1;index<(camera*.7+540)/20;index++){var x=index*20-camera*.7;Tree(x,130+(index*7%21),5+(Math.Abs(index)%3));}
  R(0,210,w,120,"#173A30");R(0,209,w,2,"#45684D");
  // A road connects all nine stations across the same map.
  for(var y=244;y<294;y+=2){R(0,y,w,2,y%8==0?"#405745":"#354D3D");}
  R(0,242,w,2,"#72816A");R(0,294,w,2,"#72816A");
  for(var x=-((camera*1.1)%24);x<w;x+=24)R(x,268,11,2,"#9AA684");
  void Station(double x,int sector){
   var station=WorldStations.All[Math.Clamp(sector,0,WorldStations.All.Count-1)];var roof=station.Roof;
   R(x,178,48,53,station.Wall);R(x-4,173,56,6,roof);R(x+1,167,46,6,roof);R(x+7,161,34,6,roof);R(x+12,155,24,6,roof);
   R(x+6,187,14,14,"#D4BA79");R(x+12,187,2,14,"#6D8060");R(x+6,193,14,2,"#6D8060");
   R(x+28,204,12,27,"#132D2B");R(x+36,216,2,2,"#D3C488");R(x+21,232,24,2,"#74886B");R(x+25,235,18,3,"#74886B");R(x+29,238,10,4,"#74886B");
   R(x+4,207,15,13,"#1D3B36");for(var i=0;i<=sector%5;i++)R(x+6+i*2,210,1,6,"#9EC797");
   void Sign(string text){R(x-8,177,64,9,"#102E30");var lettering=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(UiTheme.Font,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),6,Brushes.LightGoldenrodYellow,1);dc.DrawText(lettering,new Point(x+24-lettering.Width/2,178));}
   Sign(station.Sign);
   switch(station.Page){
    case "cleanup":
     R(x+54,213,12,17,"#9BC89C");R(x+52,211,16,3,"#D9E9B7");R(x+57,215,2,12,"#416C58");R(x+62,215,2,12,"#416C58");
     R(x-12,193,2,36,"#D7BD83");R(x-16,224,10,6,"#E5D093");for(int b=0;b<3;b++)R(x-16+b*4,228,2,5,"#9BC89C");
     R(x+18,159,12,10,"#D9F2D0");R(x+21,156,6,16,"#D9F2D0");R(x+16,162,16,4,"#D9F2D0");break;
    case "gaming":
     R(x-23,202,16,28,"#44365A");R(x-21,204,12,10,"#8E65BD");R(x-19,206,8,6,"#A6E6DC");R(x-23,215,16,3,"#D4A4D9");R(x-20,220,3,3,"#E5D093");R(x-12,219,2,2,"#D4A4D9");
     R(x+53,199,13,32,"#44365A");R(x+55,202,9,12,"#93BFB4");R(x+56,160,3,12,"#D4A4D9");R(x+52,164,11,3,"#D4A4D9");break;
    case "drivers":
     R(x+53,194,22,35,"#466D86");for(int b=0;b<4;b++){R(x+55,197+b*7,18,4,"#1B394C");R(x+69,198+b*7,2,2,"#B9E2A7");}
     R(x-21,209,15,20,"#557D8C");R(x-18,212,9,9,"#98C6C0");break;
    case "network":
     R(x+37,136,2,31,"#87A78A");R(x+26,138,24,2,"#B9E2A7");R(x+30,131,16,2,"#B9E2A7");R(x+35,125,6,3,"#D7EDB5");R(x-19,213,13,15,"#74A7A1");break;
    case "performance":
     R(x-22,213,20,14,"#BD8F56");R(x-18,209,12,6,"#D8B780");R(x-21,225,5,5,"#142A2A");R(x-8,225,5,5,"#142A2A");R(x+52,199,16,30,"#AE7750");R(x+56,202,8,10,"#BFE0BD");break;
    case "capsule":R(x-23,214,13,16,"#A99164");R(x-7,209,13,21,"#C2AE7C");R(x-20,220,7,2,"#506351");R(x+54,204,17,26,"#8D7954");break;
    case "startup":R(x+54,202,18,28,"#879FC1");R(x+58,205,10,12,"#C8E0C5");R(x+62,209,2,14,"#365451");break;
    case "apps":R(x-4,187,56,5,"#CBA8B4");for(int b=0;b<7;b++)R(x-4+b*8,187,4,5,"#6C516C");R(x+53,214,16,16,"#BA92A5");break;
    case "history":for(int b=0;b<3;b++){R(x-20,207+b*8,16,6,"#A99877");R(x-16,209+b*8,8,2,"#41594F");}break;
    default:R(x+54,215,19,3,"#9EBD83");R(x+56,218,2,13,"#64816A");R(x+69,218,2,13,"#64816A");break;
   }
   R(x-13,211,2,30,"#7E9675");R(x-18,204,12,8,"#D9BE78");R(x-17,205,10,5,"#EAD294");
  }
  foreach(var station in WorldStations.All){double x=170*station.Sector+215-camera;if(x>-110&&x<530)Station(x,station.Sector);}
  for(var i=0;i<120;i++){var x=(i*53-camera)%540;if(x<0)x+=540;var y=299+(i*13%31);R(x,y,3,1,i%4==0?"#6D8A59":"#2D513A");}
  var character=CharacterPosition-camera;var moving=Walking;var direction=Facing;
  var phase=(int)(WalkFrame*9)%4;double stride=moving?(phase==0?-2:phase==2?2:0):0;double foot=FootPosition;
  R(character-7,foot+1,18,2,"#183329");
  var bob=moving&&phase%2==0?-1:0;var head=foot-23+bob;
  R(character-3,head,10,9,"#E0BE91");R(character-4,head-2,11,4,"#34413E");R(character-4,head+1,3,5,"#34413E");
  R(character+(direction>0?5:-2),head+3,1,2,"#14322F");R(character-4,head+9,11,9,"#B9E2A7");R(character-1,head+10,3,7,"#719889");
  R(character-7+stride,head+10,3,7,"#D2AD83");R(character+7-stride,head+10,3,7,"#D2AD83");
  R(character-3-stride,head+18,4,5,"#667C94");R(character+3+stride,head+18,4,5,"#667C94");R(character-4-stride,foot-1,5,2,"#DDCAA1");R(character+3+stride,foot-1,5,2,"#DDCAA1");
  if(!moving&&preparationState=="working"){var lift=workTick%2==0?0:2;R(character+10,head+8-lift,3,9,"#D8B780");R(character+8,head+6-lift,7,3,"#98C6C0");R(character+15,head+3-lift,2,2,"#EAD294");}
  if(preparationState=="done"){R(character+13,head+2,2,5,"#B9E2A7");R(character+15,head+5,2,2,"#B9E2A7");R(character+17,head+1,2,6,"#B9E2A7");}
  if(preparationState=="attention"){R(character+14,head,2,5,"#EBC078");R(character+14,head+7,2,2,"#EBC078");}
  if(Panorama)dc.Pop();
  dc.Pop();dc.Pop();
 }
}
public static class PixelTheme
{
 public static void Apply(Window window)
 {
  var area=SystemParameters.WorkArea;window.MinWidth=Math.Min(window.MinWidth,area.Width-24);window.MinHeight=Math.Min(window.MinHeight,area.Height-24);
  if(window.WindowState==WindowState.Normal){window.Width=Math.Min(window.Width,area.Width-24);window.Height=Math.Min(window.Height,area.Height-24);}
  if(window is MainWindow)return;
  UiTheme.DialogChrome(window);return;

 }
}

