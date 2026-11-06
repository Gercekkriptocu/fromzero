using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FromZero;

/// <summary>An original pixel radar for the update discovery workflow.</summary>
public sealed class UpdateRadar : FrameworkElement
{
 private readonly DispatcherTimer timer;
 private double angle;
 private static readonly Brush Mint=new SolidColorBrush(Color.FromRgb(185,226,167));
 private static readonly Brush Orange=new SolidColorBrush(Color.FromRgb(255,85,0));
 private static readonly Brush Line=new SolidColorBrush(Color.FromRgb(48,75,79));

 public UpdateRadar()
 {
  Width=240;Height=240;
  timer=new DispatcherTimer(TimeSpan.FromMilliseconds(50),DispatcherPriority.Render,(_,_)=>{
   angle=(angle+4)%360;InvalidateVisual();
  },Dispatcher);
  timer.Stop();
  IsVisibleChanged+=(_,_)=>{if(IsVisible)timer.Start();else timer.Stop();};
  Loaded+=(_,_)=>{if(IsVisible)timer.Start();};
  Unloaded+=(_,_)=>timer.Stop();
 }

 protected override void OnRender(DrawingContext drawing)
 {
  base.OnRender(drawing);
  var center=new Point(RenderSize.Width/2,RenderSize.Height/2);
  var thin=new Pen(Line,1);
  for(var radius=40;radius<=96;radius+=28)drawing.DrawEllipse(null,thin,center,radius,radius);
  drawing.DrawLine(thin,new(center.X-108,center.Y),new(center.X+108,center.Y));
  drawing.DrawLine(thin,new(center.X,center.Y-108),new(center.X,center.Y+108));
  for(var index=0;index<48;index++)
  {
   var radians=index*Math.PI/24;
   var distance=(index*7.5-angle+360)%360;
   var brush=distance<35?Orange:distance<95?Mint:Line;
   var point=new Point(center.X+Math.Cos(radians)*108,center.Y+Math.Sin(radians)*108);
   drawing.DrawRectangle(brush,null,new(point.X-2,point.Y-2,4,4));
  }
  var sweep=angle*Math.PI/180;
  drawing.DrawLine(new Pen(Mint,2),center,new(center.X+Math.Cos(sweep)*95,center.Y+Math.Sin(sweep)*95));
  foreach(var point in new[]{new Point(-57,-40),new Point(57,29),new Point(-30,64)})
  {
   var brightness=(Math.Atan2(point.Y,point.X)*180/Math.PI-angle+720)%360;
   drawing.PushOpacity(brightness<100?1:0.25);
   drawing.DrawRectangle(Mint,null,new(center.X+point.X-3,center.Y+point.Y-3,6,6));
   drawing.Pop();
  }
  drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(20,36,43)),new Pen(Mint,2),
   new(center.X-22,center.Y-22,44,44),6,6);
  drawing.DrawRectangle(Mint,null,new(center.X-9,center.Y-9,18,18));
  for(var pin=-12;pin<=12;pin+=12)
  {
   drawing.DrawRectangle(Mint,null,new(center.X+pin-2,center.Y-29,4,6));
   drawing.DrawRectangle(Mint,null,new(center.X+pin-2,center.Y+23,4,6));
   drawing.DrawRectangle(Mint,null,new(center.X-29,center.Y+pin-2,6,4));
   drawing.DrawRectangle(Mint,null,new(center.X+23,center.Y+pin-2,6,4));
  }
 }
}
