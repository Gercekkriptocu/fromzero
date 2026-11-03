using System.Windows;
using System.Windows.Media;
namespace FromZero;
public sealed class PixelLandscape : FrameworkElement
{
 protected override void OnRender(DrawingContext dc)
 {
  const int width = 320, height = 90;
  var sx = ActualWidth / width; var sy = ActualHeight / height;
  dc.PushTransform(new ScaleTransform(sx, sy));
  void R(double x, double y, double w, double h, string color) => dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), null, new Rect(x, y, w, h));
  R(0, 0, width, height, "#182F3B");
  R(0, 38, width, 52, "#254751");
  var random = new Random(42);
  for (int i = 0; i < 70; i++) R(random.Next(0,320), random.Next(0,42), 1,1, i % 5 == 0 ? "#C8C5A1" : "#54777D");
  R(254,8,11,11,"#DFD4A0"); R(251,11,17,5,"#DFD4A0"); R(255,9,3,3,"#BAC2A0");
  for (int x = 0; x < width; x += 3) {
   var ridge = 42 - Math.Sin(x * .032) * 8 - Math.Sin(x * .085) * 4;
   R(x, Math.Floor(ridge),3,height-Math.Floor(ridge),"#31505A");
   var near = 54 - Math.Sin(x * .043 + 2) * 7;
   R(x,Math.Floor(near),3,height-Math.Floor(near),"#24423F");
  }
  void Tree(int x, int y, int s) {
   R(x,y,2,s*4,"#476454");
   for (int i=0;i<4;i++) R(x-(i+1)*s/2,y+i*s, (i+1)*s+2,s+1,"#142F31");
  }
  for(int x=115;x<320;x+=13) Tree(x,random.Next(36,52),random.Next(3,6));
  R(0,78,320,12,"#172F2C"); R(0,78,320,2,"#517258");
  for (int i=0;i<65;i++) R(random.Next(0,320),random.Next(81,90),2,1,"#304C39");
  // Small off-grid recovery station, drawn on an integer pixel grid.
  R(220,57,31,21,"#536C58"); R(218,55,35,3,"#243E3B"); R(222,52,27,3,"#243E3B"); R(226,49,19,3,"#243E3B");
  R(225,63,8,8,"#DBB47A"); R(228,63,1,8,"#7D7958"); R(225,66,8,1,"#7D7958"); R(238,65,8,13,"#243B38");
  R(241,69,2,2,"#C8C68B"); R(229,78,3,2,"#949B78"); R(227,80,8,2,"#949B78"); R(225,82,12,2,"#949B78");
  R(205,70,7,7,"#B9E2A7"); R(204,72,9,4,"#B9E2A7"); R(207,72,1,2,"#1B3B38"); R(210,72,1,2,"#1B3B38"); R(206,77,2,2,"#739B79"); R(210,77,2,2,"#739B79");
  R(282,56,2,22,"#70866A"); R(276,56,14,2,"#70866A"); R(278,52,10,2,"#9EAC7B"); R(280,48,6,2,"#9EAC7B"); R(282,44,2,2,"#C5CF99");
  dc.Pop();
 }
}

