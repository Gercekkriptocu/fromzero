using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using System.ComponentModel;
using System.Globalization;

namespace FromZero;
public static class UiTheme
{
 public static FontFamily Font {get;}=new(new Uri("pack://application:,,,/"),"./Assets/Fonts/#Pixelify Sans");
 public static ImageSource Logo {get;}=new BitmapImage(new Uri("pack://application:,,,/FromZero;component/Assets/FromZero.png"));
 private sealed class Original { public readonly Dictionary<DependencyProperty,(string Raw,string Rendered)> Values=[]; }
 private static readonly ConditionalWeakTable<DependencyObject,Original> Texts=new();
 private sealed class OriginalSize{public double Base,Rendered;}
 private static readonly ConditionalWeakTable<DependencyObject,OriginalSize> Sizes=new();
 public static double TextScale{get;private set;}=ReadScale();
 private static double ReadScale()
 {
  try{var path=LocalStore.FilePath("text-scale.json");if(File.Exists(path)){var scale=JsonSerializer.Deserialize<double>(File.ReadAllText(path));if(scale is 1 or 1.15 or 1.3)return scale;}}
  catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){}
  return 1;
 }
 public static void SetTextScale(double scale)
 {
  if(scale is not(1 or 1.15 or 1.3))throw new ArgumentOutOfRangeException(nameof(scale));
  var path=LocalStore.FilePath("text-scale.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(scale));File.Move(path+".tmp",path,true);
  TextScale=scale;foreach(Window window in Application.Current.Windows)ApplyTree(window);
 }
 private static void Scale(DependencyObject element,DependencyProperty property)
 {
  if(!Sizes.TryGetValue(element,out var original)&&DependencyPropertyHelper.GetValueSource(element,property).BaseValueSource==BaseValueSource.Inherited)return;
  var value=(double)element.GetValue(property);
  if(original==null){original=new(){Base=value,Rendered=value};Sizes.Add(element,original);}
  else if(Math.Abs(value-original.Rendered)>.001)original.Base=value;
  var rendered=original.Base*TextScale;
  original.Rendered=rendered;if(Math.Abs(rendered-value)>.001)element.SetCurrentValue(property,rendered);
 }
 private static DispatcherTimer? timer;
 private static readonly LanguageSignal Language=new();
 private sealed class LanguageSignal:INotifyPropertyChanged
 {
  public string Current=>Locale.Language;
  public event PropertyChangedEventHandler? PropertyChanged;
  public void Update()=>PropertyChanged?.Invoke(this,new(nameof(Current)));
 }
 private sealed class ItemTranslation:IMultiValueConverter
 {
  public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)
   =>values[0] is string value?Locale.Text(value):values[0]==DependencyProperty.UnsetValue?"":values[0]?.ToString()??"";
  public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>throw new NotSupportedException();
 }
 private static readonly ConditionalWeakTable<ComboBox,DataTemplate> ChoiceTemplates=new();
 private static void LocalizeChoices(ComboBox combo)
 {
  if(combo.IsEditable||combo.ItemTemplate!=null||combo.ItemTemplateSelector!=null||combo.ItemsSource==null||
   (combo.ItemsSource is not IEnumerable<string>&&string.IsNullOrEmpty(combo.DisplayMemberPath)))return;
  var template=ChoiceTemplates.GetValue(combo,box=>{
   var path=box.DisplayMemberPath;
   var text=new FrameworkElementFactory(typeof(TextBlock));
   text.SetValue(FrameworkElement.TagProperty,"FromZero.RawText");
   text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);
   var binding=new MultiBinding{Converter=new ItemTranslation()};
   binding.Bindings.Add(new Binding(path.Length==0?".":path));
   binding.Bindings.Add(new Binding(nameof(LanguageSignal.Current)){Source=Language});
   text.SetBinding(TextBlock.TextProperty,binding);
   return new DataTemplate{VisualTree=text};
  });
  combo.SetCurrentValue(ItemsControl.DisplayMemberPathProperty,"");
  combo.SetCurrentValue(ItemsControl.ItemTemplateProperty,template);
 }
 public static void Start()
 {
  Locale.Set(Locale.Language,false);
  EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>{if(sender is DependencyObject element)ApplyElement(element);}));
  timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};timer.Tick+=(_,_)=>{foreach(Window window in Application.Current.Windows)if(window.IsVisible)ApplyTree(window);};timer.Start();
  Locale.Changed+=()=>{Language.Update();foreach(Window window in Application.Current.Windows)ApplyTree(window);};
 }
 public static void SetLocalizedText(TextBlock block,string source)=>Text(block,TextBlock.TextProperty,source);
 private static void Text(DependencyObject element,DependencyProperty property,string? source=null)
 {
  if(element.GetValue(property) is not string value||(value.Length==0&&source==null))return;
  var values=Texts.GetValue(element,_=>new Original()).Values;
  var raw=source??(values.TryGetValue(property,out var old)&&value==old.Rendered?old.Raw:value);
  var translated=Locale.Text(raw);values[property]=(raw,translated);if(value!=translated)element.SetCurrentValue(property,translated);
 }
 public static void ApplyElement(DependencyObject element)
 {
  Text(element,System.Windows.Automation.AutomationProperties.NameProperty);
  if(element is TextBlock block){block.SetCurrentValue(TextBlock.FontFamilyProperty,Font);Scale(block,TextBlock.FontSizeProperty);if(block.Tag is not "FromZero.RawText")Text(block,TextBlock.TextProperty);}
  if(element is Control control){control.SetCurrentValue(Control.FontFamilyProperty,Font);Scale(control,Control.FontSizeProperty);Text(control,Control.ToolTipProperty);}
  if(element is ComboBox combo)LocalizeChoices(combo);
  if(element is TextBox{IsReadOnly:true} box&&box.Tag is not "FromZero.RawText")Text(box,TextBox.TextProperty);
  if(element is ContentControl content&&(content is not ComboBoxItem||ItemsControl.ItemsControlFromItemContainer(content)?.ItemsSource==null)&&content.Tag is not "FromZero.RawText")Text(content,ContentControl.ContentProperty);
  if(element is HeaderedContentControl header)Text(header,HeaderedContentControl.HeaderProperty);
  if(element is MenuItem menu)Text(menu,MenuItem.HeaderProperty);
  if(element is Window window){window.SetCurrentValue(Window.FontFamilyProperty,Font);Text(window,Window.TitleProperty);}
 }
 public static void ApplyTree(DependencyObject root)
 {
  var visited=new HashSet<DependencyObject>();
  void Walk(DependencyObject element)
  {
   if(!visited.Add(element))return;
   ApplyElement(element);
   if(element is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++)Walk(VisualTreeHelper.GetChild(element,i));
   if(element is FrameworkElement or FrameworkContentElement)
    foreach(var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())Walk(child);
   if(element is Popup{IsOpen:true,Child:{ } popup})Walk(popup);
   if(element is FrameworkElement{ContextMenu.IsOpen:true} host)Walk(host.ContextMenu);
  }
  Walk(root);
 }
 public static UIElement CaptionIcon(string role)
 {
  var canvas=new Canvas{Width=14,Height=14,IsHitTestVisible=false};var brush=new SolidColorBrush(Color.FromRgb(206,220,211));
  void Line(double x1,double y1,double x2,double y2)=>canvas.Children.Add(new System.Windows.Shapes.Line{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=brush,StrokeThickness=1.3});
  if(role=="close"){Line(2,2,12,12);Line(12,2,2,12);}else if(role=="minimize")Line(2,9,12,9);else{canvas.Children.Add(new System.Windows.Shapes.Rectangle{Width=10,Height=10,Stroke=brush,StrokeThickness=1.3,Margin=new Thickness(2)});if(role=="restore"){Line(4,0,14,0);Line(14,0,14,10);}}
  return canvas;
 }
 public static void DialogChrome(Window window)
 {
  if(window is MainWindow||window.Content is not UIElement content||content is FrameworkElement{Tag:"FromZero.DialogChrome"})return;
  window.Icon=Logo;window.FontFamily=Font;window.WindowStyle=WindowStyle.None;
  WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=34,ResizeBorderThickness=new Thickness(window.ResizeMode==ResizeMode.NoResize?0:5),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
  window.Content=null;
  var grid=new Grid{Tag="FromZero.DialogChrome",ClipToBounds=true};grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(34)});grid.RowDefinitions.Add(new RowDefinition());
  grid.Children.Add(new PixelWorld{Camera=PixelWorld.CurrentCamera,IsHitTestVisible=false});Grid.SetRowSpan(grid.Children[0],2);
  var shade=new Border{Background=new SolidColorBrush(Color.FromArgb(222,14,29,35)),IsHitTestVisible=false};Grid.SetRowSpan(shade,2);grid.Children.Add(shade);
  var title=new DockPanel{Margin=new Thickness(12,0,0,0)};TextElement.SetForeground(title,new SolidColorBrush(Color.FromRgb(216,232,211)));var close=new Button{Content=CaptionIcon("close"),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Margin=new Thickness(0),Padding=new Thickness(10,7,10,7),Width=40,ToolTip="Close"};WindowChrome.SetIsHitTestVisibleInChrome(close,true);close.Click+=(_,_)=>window.Close();DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);
  var icon=new Image{Source=Logo,Width=16,Height=16,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(icon,Dock.Left);title.Children.Add(icon);title.Children.Add(new TextBlock{Text=window.Title,FontFamily=Font,FontSize=12,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});grid.Children.Add(title);
  Grid.SetRow(content,1);grid.Children.Add(content);window.Content=grid;ApplyTree(grid);
 }
}
public static class AppMessage
{
 public static MessageBoxResult Show(string text,string caption="FromZero",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None)=>Show(Application.Current?.MainWindow,text,caption,buttons,image);
 public static MessageBoxResult Show(Window? owner,string text,string caption="FromZero",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None)
 {
  var dialog=new Window{Title=caption,Width=Math.Min(660,SystemParameters.WorkArea.Width-30),Height=Math.Min(430,SystemParameters.WorkArea.Height-30),MinWidth=320,MinHeight=220,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(20,33,41))};if(owner is{IsVisible:true})dialog.Owner=owner;
  var result=MessageBoxResult.Cancel;var panel=new DockPanel{Margin=new Thickness(22)};var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
  var options=buttons switch{MessageBoxButton.YesNo=>new[]{("No",MessageBoxResult.No),("Yes",MessageBoxResult.Yes)},MessageBoxButton.YesNoCancel=>new[]{("Cancel",MessageBoxResult.Cancel),("No",MessageBoxResult.No),("Yes",MessageBoxResult.Yes)},MessageBoxButton.OKCancel=>new[]{("Cancel",MessageBoxResult.Cancel),("OK",MessageBoxResult.OK)},_=>new[]{("OK",MessageBoxResult.OK)}};
  foreach(var (label,value) in options){var button=new Button{Content=label,IsDefault=value is MessageBoxResult.No or MessageBoxResult.Cancel||options.Length==1,IsCancel=value is MessageBoxResult.Cancel or MessageBoxResult.No};button.Click+=(_,_)=>{result=value;dialog.Close();};actions.Children.Add(button);}
  panel.Children.Add(new ScrollViewer{Content=new TextBlock{Text=text,FontFamily=UiTheme.Font,FontSize=15,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White},HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});dialog.Content=panel;
  UiTheme.DialogChrome(dialog);dialog.ShowDialog();return result;
 }
}
