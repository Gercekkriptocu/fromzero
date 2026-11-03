using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FromZero;
public static class AppIcons
{
 private static readonly ConcurrentDictionary<string,ImageSource> Cache=new(StringComparer.OrdinalIgnoreCase);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern uint ExtractIconEx(string file,int index,IntPtr[]? large,IntPtr[]? small,uint count);
 [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
 public static ImageSource? Read(string? raw)
 {
  if(string.IsNullOrWhiteSpace(raw))return null;
  var value=Environment.ExpandEnvironmentVariables(raw.Trim());int index=0;var match=Regex.Match(value,@",\s*(-?\d+)$");if(match.Success){int.TryParse(match.Groups[1].Value,out index);value=value[..match.Index];}value=value.Trim('"');
  if(!Regex.IsMatch(value,@"^[A-Za-z]:[\\/]"))return null;
  if(!File.Exists(value)){
   try{var folder=Path.GetDirectoryName(value);if(folder==null||!Directory.Exists(folder))return null;var variant=Directory.EnumerateFiles(folder,Path.GetFileNameWithoutExtension(value)+"*"+Path.GetExtension(value),SearchOption.TopDirectoryOnly).Take(40).OrderByDescending(p=>p.Contains("targetsize-48",StringComparison.OrdinalIgnoreCase)).ThenByDescending(p=>p.Contains("scale-200",StringComparison.OrdinalIgnoreCase)).ThenByDescending(p=>p.Contains("scale-100",StringComparison.OrdinalIgnoreCase)).FirstOrDefault();if(variant==null)return null;value=variant;}
   catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){return null;}
  }
  var key=value+","+index;if(Cache.TryGetValue(key,out var existing))return existing;
  try{
   ImageSource? result=null;
   if(new[]{".png",".jpg",".jpeg",".ico"}.Contains(Path.GetExtension(value).ToLowerInvariant())){if(new FileInfo(value).Length>20_000_000)return null;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(value);bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=48;bitmap.EndInit();bitmap.Freeze();result=bitmap;}
   else if(new[]{".exe",".dll"}.Contains(Path.GetExtension(value).ToLowerInvariant())){var handles=new IntPtr[1];ExtractIconEx(value,index,handles,null,1);if(handles[0]!=IntPtr.Zero){try{var bitmap=Imaging.CreateBitmapSourceFromHIcon(handles[0],Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(48,48));bitmap.Freeze();result=bitmap;}finally{DestroyIcon(handles[0]);}}}
   if(result!=null){if(Cache.Count<1024)Cache[key]=result;return result;}
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.ComponentModel.Win32Exception){}
  return null;
 }
 public static string ProcessPath(int id){try{using var process=Process.GetProcessById(id);return process.MainModule?.FileName??"";}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){return "";}}
 public static FrameworkElement Badge(string name,string? path,double size=32)
 {
  var source=Read(path);if(source!=null)return new Image{Source=source,Width=size,Height=size,Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};
  return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(8),Background=new SolidColorBrush(Color.FromRgb(49,74,80)),Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock{Text=name.Length==0?"?":name[..1].ToUpperInvariant(),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,FontSize=size*.5,Foreground=Brushes.White}};
 }
 public static FrameworkElement Heading(string name,string? path,UIElement content)
 {
  var row=new DockPanel();var icon=Badge(name,path);DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);row.Children.Add(content);return row;
 }
}
