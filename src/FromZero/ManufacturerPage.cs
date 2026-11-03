using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FromZero;
public partial class MainWindow
{
 private bool manufacturerTools=true,manufacturerDetectedOnly;
 private string manufacturerQuery="",manufacturerCategory="Tüm kategoriler";
 private int manufacturerPage;
 private void RenderManufacturerCatalog()
 {
  string system="";try{using var bios=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");if(bios!=null)system=string.Join(" ",new[]{"SystemManufacturer","SystemProductName","BaseBoardManufacturer","BaseBoardProduct"}.Select(key=>bios.GetValue(key)?.ToString()??""));}catch(System.Security.SecurityException){}
  var hardware=OemSources.HardwareText(devices,system);
  Advanced("Ekran kartı uygulamalarını yönet",GpuTools);
  Page.Children.Add(Row(Action("Araçlar",()=>{manufacturerTools=true;manufacturerPage=0;Navigate("drivers");return Task.CompletedTask;},manufacturerTools),Action("Destek sayfaları",()=>{manufacturerTools=false;manufacturerPage=0;Navigate("drivers");return Task.CompletedTask;},!manufacturerTools)));
  var query=new TextBox{Text=manufacturerQuery,Height=38,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Marka veya araç adı ara",Margin=new Thickness(0,4,0,6)};
  var category=new ComboBox{MinWidth=180,MaxWidth=270,Height=36,Margin=new Thickness(0,0,10,6)};category.Items.Add("Tüm kategoriler");foreach(var name in OemSources.All.Select(s=>s.Category).Concat(OemSources.Tools.Select(s=>s.Category)).Distinct().Order())category.Items.Add(name);category.SelectedItem=manufacturerCategory;
  var detected=new CheckBox{Content="Yalnız algılanan markalar",IsChecked=manufacturerDetectedOnly,VerticalAlignment=VerticalAlignment.Center,ToolTip="Yerel sistem üreticisi ve mevcut cihaz adları / donanım kimlikleriyle marka eşleşmesi yapılır. Sürücü uyumluluğu anlamına gelmez."};
  var status=T("",12,"#B9CBC1");var tiles=new ResponsiveTilePanel{MaxColumns=2,MinimumTileWidth=295};var pagination=Row();
  Page.Children.Add(T("Marka veya araç ara",12,"#B9CBC1"));Page.Children.Add(query);Page.Children.Add(Row(category,detected));Page.Children.Add(status);Page.Children.Add(pagination);Page.Children.Add(tiles);
  void Render()
  {
   tiles.Children.Clear();pagination.Children.Clear();
   var entries=manufacturerTools?OemSources.Tools.Select(t=>new{t.Name,t.Url,t.Category,t.Terms,t.RecipeId,t.Note}):OemSources.All.Select(t=>new{t.Name,t.Url,t.Category,t.Terms,RecipeId="",Note=t.Note.Length>0?t.Note:"Model ve Windows sürümü üreticinin sayfasında seçilir. Bu bağlantı otomatik taranan bir sürücü paketi değildir."});
   var filtered=entries.Where(t=>(manufacturerCategory=="Tüm kategoriler"||t.Category==manufacturerCategory)&&(!manufacturerDetectedOnly||OemSources.Matches(t.Terms,hardware))&&(manufacturerQuery.Length==0||(t.Name+" "+t.Category).Contains(manufacturerQuery,StringComparison.OrdinalIgnoreCase))).OrderByDescending(t=>OemSources.Matches(t.Terms,hardware)).ThenBy(t=>t.Name).ToList();
   const int size=12;manufacturerPage=Math.Clamp(manufacturerPage,0,Math.Max(0,(filtered.Count-1)/size));status.Text=$"{filtered.Count} sonuç • Sayfa {manufacturerPage+1}/{Math.Max(1,(filtered.Count+size-1)/size)}";
   foreach(var item in filtered.Skip(manufacturerPage*size).Take(size))
   {
    var recipe=AppDownloads.Recipes.FirstOrDefault(r=>r.Id==item.RecipeId&&r.DriverTool);var panel=new StackPanel{ToolTip=item.Note};
    panel.Children.Add(T(item.Name,17,"#D8E8D3",true));panel.Children.Add(T(item.Category+(OemSources.Matches(item.Terms,hardware)?" • Algılanan marka":""),12,"#B9CBC1"));
    if(recipe!=null){var id=recipe.Id;var check=new CheckBox{Content=downloadStatus.GetValueOrDefault(id,AppDownloads.Cached(recipe)!=null?"Kurucu hazır":"İndirme kuyruğuna ekle"),IsChecked=packageSelection.Contains(id)};check.Checked+=(_,_)=>{packageSelection.Add(id);UpdateQueueSummary();};check.Unchecked+=(_,_)=>{packageSelection.Remove(id);UpdateQueueSummary();};panel.Children.Add(check);}
    panel.Children.Add(Row(Link(manufacturerTools?"Üretici sayfası":"Destek sayfasını aç",item.Url),Action("Check page access",async()=>{var result=await OemSources.CheckPageAsync(item.Url);Detail(item.Name,result+"\n\nPage access check. Select the exact model and Windows version on the official support page.");})));
    if(manufacturerTools||item.Name is "MediaTek" or "Qualcomm")panel.Children.Add(new Expander{Header="Uyumluluk",Content=T(item.Note,12,"#B9CBC1"),Foreground=B("#B9CBC1")});
    tiles.Children.Add(Card(panel));
   }
   if(filtered.Count==0)tiles.Children.Add(Card(T("Bu filtrede sonuç yok. Marka filtresini kapatabilir veya aramayı değiştirebilirsin.",13,"#B9CBC1")));
   if(manufacturerPage>0)pagination.Children.Add(Action("← Önceki",()=>{manufacturerPage--;Render();return Task.CompletedTask;}));if((manufacturerPage+1)*size<filtered.Count)pagination.Children.Add(Action("Sonraki →",()=>{manufacturerPage++;Render();return Task.CompletedTask;}));
  }
  void Filter(){manufacturerQuery=query.Text.Trim();manufacturerCategory=category.SelectedItem?.ToString()??"Tüm kategoriler";manufacturerDetectedOnly=detected.IsChecked==true;manufacturerPage=0;Render();}
  query.TextChanged+=(_,_)=>Filter();category.SelectionChanged+=(_,_)=>Filter();detected.Checked+=(_,_)=>Filter();detected.Unchecked+=(_,_)=>Filter();Render();
 }
}
