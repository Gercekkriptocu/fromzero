using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FromZero;

public partial class MainWindow
{
 private record ComparisonCrosshair(string Id,string Name,string Team,string TeamChecked,string Code);
 private readonly List<ComparisonCrosshair> csComparison=[];
 private readonly List<ComparisonCrosshair> valorantComparison=[];

 private static TextBlock CrosshairTeamDate(string date)
 {
  var label=T(date.Length==0?"":"Team checked: "+date,11,"#9AAFAE");
  if(date.Length==0)label.Visibility=Visibility.Collapsed;
  return label;
 }

 private Action<ComparisonCrosshair> CrosshairComparisonPanel(Border host,List<ComparisonCrosshair> selected,
  Func<string,FrameworkElement> preview,Action<string,string> copy,out Action refresh)
 {
  void Render()
  {
   host.Visibility=selected.Count==0?Visibility.Collapsed:Visibility.Visible;
   if(selected.Count==0){host.Child=null;return;}
   var clear=new Button{Content="Clear comparison"};
   clear.Click+=(_,_)=>{selected.Clear();Render();};
   var grid=new UniformGrid{Columns=2,Rows=1};
   foreach(var entry in selected){
    var image=preview(entry.Code);
    image.Height=165;
    var copyButton=new Button{Content="Copy code"};
    copyButton.Click+=(_,_)=>copy(entry.Code,entry.Name);
    var remove=new Button{Content="Remove from comparison"};
    remove.Click+=(_,_)=>{selected.Remove(entry);Render();};
    var panel=Card(Stack(T(entry.Name,18,"#B9E2A7",true),CrosshairTeamLabel(entry.Team),CrosshairTeamDate(entry.TeamChecked),image,Row(copyButton,remove)));
    panel.Margin=new Thickness(4,0,4,0);
    grid.Children.Add(panel);
   }
   if(selected.Count==1)grid.Children.Add(Card(T("Choose one more crosshair to compare.",13,"#B9CBC1")));
   host.Child=Card(Stack(Row(T("Side-by-side preview",18,"#B9E2A7",true),clear),grid));
   UiTheme.ApplyTree(host);
  }
  refresh=Render;
  return entry=>{
   var index=selected.FindIndex(e=>e.Id==entry.Id);
   if(index>=0)selected[index]=entry;
   else{if(selected.Count==2)selected.RemoveAt(0);selected.Add(entry);}
   Render();
   host.BringIntoView();
  };
 }
}
