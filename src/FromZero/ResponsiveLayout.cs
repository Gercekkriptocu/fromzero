using System.Windows;
using System.Windows.Controls;
namespace FromZero;

public sealed class WrappingContentSelector:DataTemplateSelector
{
 public override DataTemplate? SelectTemplate(object item,DependencyObject container)=>item is string?(DataTemplate)Application.Current.FindResource("WrappingText"):null;
}
public sealed class BoundedWrapPanel:WrapPanel
{
 protected override Size MeasureOverride(Size available)
 {
  if(double.IsFinite(available.Width))foreach(var child in InternalChildren.OfType<FrameworkElement>())child.MaxWidth=Math.Max(1,available.Width-child.Margin.Left-child.Margin.Right);
  return base.MeasureOverride(available);
 }
}
public sealed class ResponsiveTilePanel:Panel
{
 public int MaxColumns {get;set;}=2;
 public double MinimumTileWidth {get;set;}=300;
 public double Gap {get;set;}=12;
 private int Columns(double width)=>width>=MaxColumns*MinimumTileWidth+(MaxColumns-1)*Gap?MaxColumns:MaxColumns>2&&width>=2*MinimumTileWidth+Gap?2:1;
 protected override Size MeasureOverride(Size available)
 {
  var width=double.IsFinite(available.Width)?available.Width:1000;var columns=Columns(width);var cell=Math.Max(1,(width-(columns-1)*Gap)/columns);double height=0;
  for(int start=0;start<InternalChildren.Count;start+=columns){double row=0;foreach(var child in InternalChildren.Cast<UIElement>().Skip(start).Take(columns)){child.Measure(new Size(cell,double.PositiveInfinity));row=Math.Max(row,child.DesiredSize.Height);}height+=row+(start>0?Gap:0);}
  return new Size(width,height);
 }
 protected override Size ArrangeOverride(Size final)
 {
  var columns=Columns(final.Width);var cell=Math.Max(1,(final.Width-(columns-1)*Gap)/columns);double y=0;
  for(int start=0;start<InternalChildren.Count;start+=columns){var children=InternalChildren.Cast<UIElement>().Skip(start).Take(columns).ToArray();var row=children.Max(c=>c.DesiredSize.Height);for(int i=0;i<children.Length;i++)children[i].Arrange(new Rect(i*(cell+Gap),y,cell,row));y+=row+Gap;}
  return final;
 }
}
