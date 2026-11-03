using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FromZero;

public partial class MainWindow
{
 private readonly ReclaimCleaner reclaimCleaner=new();
 private ReclaimScan? cleanupScan;
 private readonly HashSet<string> cleanupSelection=new(StringComparer.Ordinal);
 private CancellationTokenSource? cleanupCancellation;
 private static string SizeLabel(long bytes)=>bytes>=1073741824?$"{bytes/1073741824d:0.00} GB":$"{bytes/1048576d:0.0} MB";
 private void Cleaner()
 {
  Header("TEMİZLİK","Yer aç. Neyi sildiğini bil.","FromZero ile önbellekleri bul; listeyi inceleyip seçtiklerini temizle.");
  var selectedLabel=T("",15,"#B9E2A7",true);
  void UpdateSelection(){var items=cleanupScan?.Items.Where(i=>cleanupSelection.Contains(i.Rule.Id)).ToList()??[];selectedLabel.Text=$"{items.Count} hedef seçili • {SizeLabel(items.Sum(i=>i.Bytes))}";}
  var summary=cleanupScan==null?ReclaimRules.All.Count+" cleanup rules for Windows, browsers, apps, games and development tools.":$"{cleanupScan.RulesChecked} kural kontrol edildi • {cleanupScan.Items.Count(i=>i.Files>0)} hedefte {SizeLabel(cleanupScan.Items.Sum(i=>i.Bytes))} dosya bulundu.";
  Page.Children.Add(Card(Stack(T("Önce tara, sonra seç",21,"#B9E2A7",true),T(summary,13,"#D2DED6"),T("Önbellekler yeniden oluşabilir. Açık uygulamaların dosyaları engellenir; bağlantılar, bulut dosyaları ve uygulamanın kendi verileri korunur.",12,"#B9CBC1"),Row(Action("Önbellekleri tara",PreviewCleanupAsync,true),Action("Seçilenleri temizle",CleanReclaimAsync)),selectedLabel)));
  if(cleanupScan==null){
   Page.Children.Add(T("Tarama dosya silmez. Dosyalar ancak seçimini inceleyip kalıcı silmeyi onayladığında temizlenir.",13,"#B9CBC1"));
  }else{
   var search=new TextBox{Width=260,Height=40,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Hedef adı veya açıklamasında ara"};
   System.Windows.Automation.AutomationProperties.SetName(search,"Önbelleklerde ara");
   var review=new CheckBox{Content="İnceleme gerekenleri göster"};
   var caution=new CheckBox{Content="Dikkat: veri kaybı riski olanları göster"};
   var list=new StackPanel();var navigation=new WrapPanel();int page=0;const int pageSize=12;
   var counter=T("",12,"#B9CBC1");
   void Render(){
    list.Children.Clear();navigation.Children.Clear();
    var items=cleanupScan.Items.Where(i=>i.Files>0&&(i.Rule.Risk=="safe"||i.Rule.Risk=="review"&&review.IsChecked==true||i.Rule.Risk=="caution"&&caution.IsChecked==true)&&(search.Text.Length==0||(Locale.Text(i.Rule.NameTr)+" "+Locale.Text(i.Rule.DescriptionTr)+" "+i.Rule.NameTr).Contains(search.Text,StringComparison.OrdinalIgnoreCase))).OrderBy(i=>i.Blocked!=null).ThenByDescending(i=>i.Bytes).ToList();
    page=Math.Clamp(page,0,Math.Max(0,(items.Count-1)/pageSize));counter.Text=$"{items.Count} eşleşme • {page+1}/{Math.Max(1,(items.Count+pageSize-1)/pageSize)} sayfa";
    foreach(var item in items.Skip(page*pageSize).Take(pageSize)){
     var entry=item;var check=new CheckBox{Content=entry.Rule.NameTr+" • "+SizeLabel(entry.Bytes),IsChecked=cleanupSelection.Contains(entry.Rule.Id),IsEnabled=entry.Blocked==null};
     check.Checked+=(_,_)=>{cleanupSelection.Add(entry.Rule.Id);UpdateSelection();};check.Unchecked+=(_,_)=>{cleanupSelection.Remove(entry.Rule.Id);UpdateSelection();};
     var risk=entry.Rule.Risk=="safe"?"Yeniden oluşabilen önbellek / günlük":entry.Rule.Risk=="review"?"Seçmeden önce incele":"Dikkat: veri kaybı veya performans etkisi olabilir";
     var status=entry.Blocked??(entry.Partial?"Kısmi tarama; yalnızca listelenen dosyalar temizlenir.":entry.Files+" dosya");
     var actions=Row(Action("Dosyaları incele",()=>{ReviewReclaimFiles(entry);return Task.CompletedTask;}),Action("Tam listeyi kaydet",()=>ExportReclaimFilesAsync(entry)));
     list.Children.Add(Card(Stack(check,T(entry.Rule.DescriptionTr,12,"#B9CBC1"),T(risk+" • "+status,12,entry.Blocked!=null||entry.Rule.Risk!="safe"?"#EBC078":"#B9CBC1"),actions)));
    }
    if(items.Count==0)list.Children.Add(T("Bu filtrede temizlenecek dosya bulunmadı.",14,"#B9CBC1"));
    if(page>0)navigation.Children.Add(Action("← Önceki",()=>{page--;Render();return Task.CompletedTask;}));
    if((page+1)*pageSize<items.Count)navigation.Children.Add(Action("Sonraki →",()=>{page++;Render();return Task.CompletedTask;}));
    UpdateSelection();
   }
   search.TextChanged+=(_,_)=>{page=0;Render();};review.Checked+=(_,_)=>Render();review.Unchecked+=(_,_)=>Render();caution.Checked+=(_,_)=>Render();caution.Unchecked+=(_,_)=>Render();
   Page.Children.Add(T("Önbelleklerde ara",12,"#B9CBC1"));Page.Children.Add(Row(search,review));Advanced("Riskli hedefler",()=>Page.Children.Add(caution));
   Page.Children.Add(Row(Action("Uygun önbellekleri seç",()=>{foreach(var item in cleanupScan.Items.Where(i=>i.Files>0&&i.Rule.Risk=="safe"&&i.Blocked==null))cleanupSelection.Add(item.Rule.Id);Render();return Task.CompletedTask;}),Action("Seçimi temizle",()=>{cleanupSelection.Clear();Render();return Task.CompletedTask;})));
   Page.Children.Add(counter);Page.Children.Add(list);Page.Children.Add(navigation);Render();
  }
  Advanced("Windows depolama araçları",()=>{
   Page.Children.Add(Card(Stack(T("Windows'un yönettiği alanlar",18,"#B9E2A7",true),T("Bileşen deposu, eski Windows kurulumu, sanal diskler ve rapor kuralları doğrudan silinmez. Windows Depolama üzerinden yönetilir. Yönetici isteyen dosya hedefleri, uygulama yönetici olarak açıldıktan sonra tekrar taranabilir.",12,"#B9CBC1"),Row(Link("Windows depolama","ms-settings:storagesense"),Link("Geçici dosyalar","ms-settings:storagepolicies")))));
  });UpdateSelection();
 }
 private async Task PreviewCleanupAsync()
 {
  cleanupScan=null;cleanupSelection.Clear();using var cancel=new CancellationTokenSource();cleanupCancellation=cancel;CancelOperation.Visibility=Visibility.Visible;
  try{
   cleanupScan=await reclaimCleaner.ScanAsync(new Progress<string>(s=>OperationTitle.Text=s),cancel.Token);
   foreach(var item in cleanupScan.Items.Where(i=>i.Files>0&&i.Rule.Risk=="safe"&&i.Blocked==null))cleanupSelection.Add(item.Rule.Id);
  }finally{cleanupCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;Navigate("cleanup");}
 }
 private void CancelCleanup(object sender,RoutedEventArgs e){cleanupCancellation?.Cancel();updateCancellation?.Cancel();if(currentPage=="prepare"&&preparationJob!=null){PreparationWorker.Stop(preparationJob.Id);OperationTitle.Text="Durdurma istendi. Devam eden kurulum tamamlanınca duracak.";}}
 private void ReviewReclaimFiles(ReclaimItem item)
 {
  if(cleanupScan==null)return;var files=reclaimCleaner.FilesFor(cleanupScan,item.Rule.Id);
  var window=CreateDialog(item.Rule.NameTr,880,620);var panel=new DockPanel{Margin=new Thickness(20)};
  var head=Stack(T(item.Rule.NameTr,22,"#B9E2A7",true),T(item.Rule.DescriptionTr,13,"#B9CBC1"),T($"{files.Count} dosya • {SizeLabel(item.Bytes)}. Yalnızca bu taramadaki değişmemiş dosyalar silinebilir.",12,"#EBC078"));DockPanel.SetDock(head,Dock.Top);panel.Children.Add(head);
  var list=new ListBox{ItemsSource=files.Select(f=>SizeLabel(f.Size)+" • "+f.Path),Background=B("#18252D"),Foreground=B("#E8EEE5"),BorderThickness=new Thickness(0),Margin=new Thickness(0,16,0,0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
  list.ItemTemplate=(DataTemplate)FindResource("WrappingText");ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);VirtualizingPanel.SetIsVirtualizing(list,true);VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
  panel.Children.Add(list);window.Content=panel;window.ShowDialog();
 }
 private async Task ExportReclaimFilesAsync(ReclaimItem item)
 {
  if(cleanupScan==null)return;var dialog=new SaveFileDialog{FileName="FromZero-"+item.Rule.Id+".json",Filter="JSON|*.json"};if(dialog.ShowDialog(this)!=true)return;
  await File.WriteAllTextAsync(dialog.FileName,JsonSerializer.Serialize(new{cleanupScan.Created,Rule=item.Rule,Files=reclaimCleaner.FilesFor(cleanupScan,item.Rule.Id)},LocalStore.Json));
 }
 private async Task CleanReclaimAsync()
 {
  if(cleanupScan==null||cleanupSelection.Count==0)throw new InvalidOperationException("Önce tara ve temizlenecek hedefleri seç.");
  var scan=cleanupScan;var selected=scan.Items.Where(i=>cleanupSelection.Contains(i.Rule.Id)).ToList();
  if(!ConfirmReclaimSelection(selected))return;
  using var cancel=new CancellationTokenSource();cleanupCancellation=cancel;CancelOperation.Visibility=Visibility.Visible;
  try{
   var result=await reclaimCleaner.CleanAsync(scan,cleanupSelection.ToArray(),new Progress<string>(s=>OperationTitle.Text=s),cancel.Token);
   var directory=LocalStore.FilePath("CleanupReports");Directory.CreateDirectory(directory);var reportPath=Path.Combine(directory,DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");
   await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(result,LocalStore.Json));
   var text=$"{result.Files} dosya silindi • {SizeLabel(result.Bytes)}\n{result.Skipped} dosya atlandı."+(result.Cancelled?"\nİşlem durduruldu; tamamlanan silmeler geri alınamaz.":"")+"\n\nİşlem raporu: "+reportPath;
   Journal.Write("FromZero temizliği",text);Detail("Temizlik sonucu",text);
  }finally{cleanupCancellation=null;CancelOperation.Visibility=Visibility.Collapsed;cleanupScan=null;cleanupSelection.Clear();Navigate("cleanup");}
 }
 private bool ConfirmReclaimSelection(List<ReclaimItem> items)
 {
  var window=CreateDialog("Seçilen dosyaları kalıcı sil",760,620);var panel=new DockPanel{Margin=new Thickness(20)};
  var header=Stack(T("Silmeden önce seçimini incele",23,"#B9E2A7",true),T($"{items.Count} hedef • {items.Sum(i=>i.Files)} dosya • {SizeLabel(items.Sum(i=>i.Bytes))}",14,"#D2DED6"));DockPanel.SetDock(header,Dock.Top);panel.Children.Add(header);
  var cancel=new Button{Content="Vazgeç",IsCancel=true};cancel.Click+=(_,_)=>window.DialogResult=false;
  var accept=new CheckBox{Content="Listeyi inceledim; seçilen dosyalar kalıcı silinsin."};
  var confirm=new Button{Content="Seçilenleri kalıcı sil",IsEnabled=false,Background=B("#EBC078"),Foreground=B("#172D2D")};confirm.Click+=(_,_)=>window.DialogResult=true;
  accept.Checked+=(_,_)=>confirm.IsEnabled=true;accept.Unchecked+=(_,_)=>confirm.IsEnabled=false;
  var footer=Stack(accept,Row(cancel,confirm));DockPanel.SetDock(footer,Dock.Bottom);panel.Children.Add(footer);
  var description=Stack(T("Geri Dönüşüm Kutusu'na taşınmaz. Önbellekler yeniden oluşurken ilk açılış yavaşlayabilir. Açık uygulamaların, kilitli veya değişmiş dosyaların temizliği atlanır.",13,"#EBC078"));
  foreach(var item in items)description.Children.Add(T("• "+item.Rule.NameTr+" / "+SizeLabel(item.Bytes)+(item.Rule.Risk!="safe"?" / İNCELEME GEREKİR":""),13,item.Rule.Risk=="safe"?"#D2DED6":"#EBC078"));
  panel.Children.Add(new ScrollViewer{Content=description,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,16,0,12)});window.Content=panel;return window.ShowDialog()==true;
 }
}

