using System.IO;
using System.Numerics;
namespace FromZero;

// CS2 v1 binary layout: akiver/csgo-sharecode (MIT); see Assets/CrosshairCodec-LICENSE.txt.
public record CrosshairSettings(int Style,bool Recoil,bool Dot,bool TStyle,int Outline,byte Red,byte Green,byte Blue,byte Alpha,byte OutlineRed,byte OutlineGreen,byte OutlineBlue,byte OutlineAlpha,int Gap,int Length,int Thickness,int Spread,int SplitDistance,double InnerAlpha,double OuterAlpha,double SplitRatio,int ScreenHeight,double ScopeScale,bool ScopeColor);
public static class CrosshairCode
{
 public const string Alphabet="ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";
 public static void Validate(CrosshairSettings s)
 {
  if(s.Style is <0 or >9||s.Outline is <0 or >2||s.Gap is <-3840 or >3840||s.Length is <0 or >255||s.Thickness is <0 or >32||s.Spread is <0 or >255||s.SplitDistance is <0 or >127||s.ScreenHeight is <240 or >65535||!double.IsFinite(s.InnerAlpha)||s.InnerAlpha is <0 or >1||!double.IsFinite(s.OuterAlpha)||s.OuterAlpha is <0.3 or >1||!double.IsFinite(s.SplitRatio)||s.SplitRatio is <0 or >1||!double.IsFinite(s.ScopeScale)||s.ScopeScale is <0.1 or >2)throw new InvalidDataException("Crosshair ayarları desteklenen CS2 aralığında değil.");
 }
 public static string Encode(CrosshairSettings s)
 {
  Validate(s);var b=new byte[32];b[1]=1;b[2]=(byte)s.ScreenHeight;b[3]=(byte)(s.ScreenHeight>>8);
  b[4]=(byte)(s.Style|(s.Recoil?32:0)|(s.Dot?64:0)|(s.TStyle?128:0));
  b[5]=s.Red;b[6]=s.Green;b[7]=s.Blue;b[8]=s.Alpha;b[9]=s.OutlineRed;b[10]=s.OutlineGreen;b[11]=s.OutlineBlue;b[12]=s.OutlineAlpha;b[13]=(byte)(s.Thickness|(s.Outline<<6));b[14]=(byte)s.Gap;b[15]=(byte)(s.Gap>>8);b[16]=(byte)s.Length;b[17]=(byte)s.Spread;
  var bits=(uint)s.SplitDistance|((uint)Math.Round(s.InnerAlpha*100)<<7)|((uint)(Math.Round(s.OuterAlpha*100)-30)<<14)|((uint)Math.Round(s.SplitRatio*100)<<21)|(s.ScopeColor?1u<<28:0);
  for(int i=0;i<4;i++)b[18+i]=(byte)(bits>>(i*8));b[22]=(byte)(Math.Round(s.ScopeScale*100)-10);b[0]=(byte)(b.Skip(1).Sum(v=>(int)v)&255);
  var value=new BigInteger(b,true,true);var chars=new char[44];for(int i=0;i<chars.Length;i++){chars[i]=Alphabet[(int)(value%57)];value/=57;}return "CS"+new string(chars);
 }
 public static CrosshairSettings Decode(string code)
 {
  code=code.Trim();if(code.Length!=46||!code.StartsWith("CS",StringComparison.Ordinal)||code.AsSpan(2).ContainsAnyExcept(Alphabet.AsSpan()))throw new InvalidDataException("Yeni biçimde CS ile başlayan 46 karakterlik bir crosshair kodu gir. Eski CSGO kodları bu önizlemede desteklenmiyor.");
  BigInteger value=0;for(int i=code.Length-1;i>=2;i--)value=value*57+Alphabet.IndexOf(code[i]);var raw=value.ToByteArray(true,true);if(raw.Length>32)throw new InvalidDataException("Crosshair kodu boyutu geçersiz.");var b=new byte[32];raw.CopyTo(b,32-raw.Length);
  if(b[0]!=(b.Skip(1).Sum(v=>(int)v)&255)||b[1]!=1)throw new InvalidDataException("Crosshair kodunun sağlama toplamı veya sürümü desteklenmiyor.");
  uint bits=(uint)(b[18]|b[19]<<8|b[20]<<16|b[21]<<24);
  var s=new CrosshairSettings(b[4]&31,(b[4]&32)!=0,(b[4]&64)!=0,(b[4]&128)!=0,b[13]>>6,b[5],b[6],b[7],b[8],b[9],b[10],b[11],b[12],(short)(b[14]|b[15]<<8),b[16],b[13]&63,b[17],(int)(bits&127),((bits>>7)&127)/100d,(((bits>>14)&127)+30)/100d,((bits>>21)&127)/100d,b[2]|b[3]<<8,(b[22]+10)/100d,(bits&(1u<<28))!=0);Validate(s);return s;
 }
}
