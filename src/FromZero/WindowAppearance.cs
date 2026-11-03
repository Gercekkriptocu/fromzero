using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace FromZero;
internal static class WindowAppearance
{
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetStyle(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW")]private static extern int SetStyle(IntPtr window,int index,int value);
 [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr type,IntPtr value);
 [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 internal static void HideTitleIcon(Window window)
 {
  var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
  SetStyle(handle,-20,GetStyle(handle,-20)|1);
  SendMessage(handle,0x80,IntPtr.Zero,IntPtr.Zero);SendMessage(handle,0x80,new IntPtr(1),IntPtr.Zero);
  SetWindowPos(handle,IntPtr.Zero,0,0,0,0,0x37);
 }
}
