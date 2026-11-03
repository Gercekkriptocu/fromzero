using System.Globalization;
namespace FromZero;
public record PreparationPreflightReport(string SystemDrive,long AvailableBytes,long RequiredBytes,bool Supported,bool Network,bool Offline)
{
 public long MissingBytes=>Math.Max(0,RequiredBytes-AvailableBytes);
 public bool Ready=>Supported&&MissingBytes==0&&(Network||Offline);
 public string SpaceLabel=>SystemDrive+" • "+(AvailableBytes/(double)(1024L*1024*1024)).ToString("F2",CultureInfo.CurrentCulture)+" GB boş";
 public List<string> Blockers{
  get{
   List<string> reasons=[];
   if(!Supported)reasons.Add("Otomatik kurulum Windows 10 2004 ve sonrası / Windows 11 x64 gerektirir.");
   if(MissingBytes>0)reasons.Add(SpaceLabel+". En az "+(RequiredBytes/(double)(1024L*1024*1024)).ToString("F2",CultureInfo.CurrentCulture)+" GB gerekli; yaklaşık "+Math.Max(1,(long)Math.Ceiling(MissingBytes/(double)(1024*1024)))+" MB daha alan aç. Başka bir diskteki boş alan, Windows'un sistem diski ihtiyacını karşılamaz.");
   if(!Network&&!Offline)reasons.Add("Etkin ağ geçidi yok. Bağlantını düzelt, telefonun USB internetini aç veya imzalı sürücü / kurucu içeren USB klasörünü seç.");
   return reasons;
  }
 }
 public string Message=>Ready?"Ön kontroller geçti. "+SpaceLabel+". "+(Network?"Yerel ağ etkin; indirme kaynakları ayrıca denetlenecek.":"Seçilen çevrimdışı paketler kullanılacak."):string.Join("\n",Blockers);
}
public static class PreparationPreflight
{
 public const long MinimumSystemBytes=5L*1024*1024*1024;
 public static PreparationPreflightReport Evaluate(string drive,long available,bool supported,bool network,bool offline)=>new(drive,Math.Max(0,available),MinimumSystemBytes,supported,network,offline);
}
