using System.Globalization;
using System.IO;
namespace FromZero;
public static class Cs2LaunchOptions
{
 public const string SteamSource="https://help.steampowered.com/en/faqs/view/7D01-D2DD-D75E-2955";
 public const string VideoSource="https://help.steampowered.com/en/faqs/view/418E-7A04-B0DA-9032";
 public static string Build(int fps,int menu=120)
 {
  if(fps!=0&&fps is <60 or >1000)throw new InvalidDataException("FPS sınırı 0 (sınırsız) veya 60–1000 olmalı.");
  if(menu is <60 or >200)throw new InvalidDataException("Menü sınırı 60–200 olmalı.");
  return "+fps_max "+fps.ToString(CultureInfo.InvariantCulture)+" +fps_max_ui "+menu.ToString(CultureInfo.InvariantCulture);
 }
 public const string Explanation="Steam → Kütüphane → Counter-Strike 2 → Özellikler → Genel → Başlatma seçenekleri. Önce mevcut satırını bir yere kaydet. Çakışan +fps_max / +fps_max_ui komutlarını kaldırıp bu satırı yapıştır; diğer kişisel seçeneklerini koru. Oyunu yeniden başlatıp Gelişmiş Video'daki oyun/menü FPS sınırlarından sonucu kontrol et.\n\nSınırsız seçenek yalnız oyunun FPS üst sınırını kaldırır; donanımın üretemediği FPS'yi oluşturmaz. Isı/güç kullanımı ve kare süresi dalgalanması artabilir. Menü 120 FPS ile sınırlanır. Dış sürücü/VSync sınırı ayrıca geçerli olabilir. G-Sync/VRR için sınırsız yerine uygun bir sınırla akıcılığı karşılaştır.\n\n-high, -threads, -heapsize, -dxlevel, -tickrate ve eski CS:GO paketleri otomatik eklenmez. Bu sürüm renderer, anti-cheat, ağ veya Steam dosyalarını değiştirmez. Geri almak için eklediğin iki komutu kaldırıp önceki FPS değerlerini oyun menüsünden geri seç.";
}
