# FromZero 0.15 — tamamlananlar ve gelişim planı

## Bu sürümde

- Varsayılan dil İngilizce. Sağ üstteki English / Türkçe seçimi anında uygulanır ve yerel olarak saklanır. Uygulama metinleri için 2.525 eşleme vardır; yazılım adları ve nişangah kodları çevrilmez. Windows ve üretici kurucularının dili işletim sistemine bağlıdır.
- Tek uygulama fontu Pixelify Sans. Yeni özgün simge EXE, görev çubuğu, ana pencere ve uygulamaya ait alt pencerelerde kullanılır. Ana sayfanın görünür kaydırma çubuğu kaldırılmıştır; fare ve klavyeyle içeriğe erişim sürer.
- Uygulamaya ait ileti/ayrıntı/onay pencereleri ortak, manzarayla birleşen başlık kullanır. Beyaz Windows başlığı yoktur. Şeffaflık uygulamanın arka planını gösterir. Windows dosya seçicileri, yönetici izni ve başka yazılımların pencereleri kapsam dışındadır.
- CS2 kartında Restore düğmesinin sağındaki nişangah simgesi kataloğu aynı uygulama içinde açar. FACEIT ilk 10 yenilemesi API anahtarı olmadan kamusal global ELO HTML sayfasını okur. Görünür ELO, global kapsam ve oyuncu kimlikleri doğrulanır; başarısız veya eksik sonuç eski geçerli önbelleğin üzerine yazılmaz. Kaynak sitenin güncelliği ve erişilebilirliği dış bağımlılıktır. Sıralamanın değişmesi, yeni oyuncunun doğrulanmış nişangah kodunun otomatik bulunacağı anlamına gelmez.
- More tools → Applications: masaüstü ve mevcut kullanıcının Store uygulamaları, arama, sayfalama, ayrıntı, tahmini boyut, yerel logo ve kaldırma. Üreticinin EXE/MSI kaldırıcısı veya Windows MSIX kaldırması kullanılır. Korunan bileşenler varsayılan gizlidir ve buradan kaldırılamaz. Geçersiz girişlerde Windows Installed Apps yönlendirmesi vardır. Bilinmeyen kayıt defteri girdileri topluca silinmez.
- Uygulama, RAM ve performans listelerinde erişilebilen gerçek yerel simgeler gösterilir. Windows simge dosyasına erişim vermezse harfli yer tutucu kullanılır.
- Güncellemeler: kurulu uygulamalar varsayılan görünüm; kurulu / mevcut kararlı sürüm, durum, neden ve önerilen eylem ayrı gösterilir. Bilinmeyen sürüm eski ilan edilmez. Kurulu olmayan katalog girdileri isteğe bağlıdır. Temel ve laptop hazırlığında uygulama seçimi boştur; oyun profilinde yalnız Steam, yayın profilinde yalnız OBS seçilidir. Önceden kaydedilmiş kullanıcı seçimleri korunur.

## Sonraki geliştirmeler — öncelik sırası

### 1. Neden gerektiğini açıklayan hazırlık planı

Her işlem için cihaz/uygulama, saptanan sorun, mevcut durum, hedef durum, kaynak, indirme boyutu, yeniden başlatma ihtiyacı ve geri alma olanağı tek satırda gösterilsin. Ana gruplar: eksik cihaz sürücüsü, mevcut yazılım güncellemesi, seçilen kullanım için gereken bileşen ve kullanıcının seçtiği uygulama. Bir GPU aracı sadece donanımı varsa önerilsin; ikinci tarayıcı, cleaner veya optimizer kendiliğinden kurulmasın.

Kabul ölçütü: Kullanıcı başlatmadan önce neyin neden kurulacağını görebilir; ikinci tarama aynı tamamlanmış işleri tekrar önermez. Süre: orta. Etki: çok yüksek.

### 2. Güvenilir sürücü kapsamı ve çevrimdışı ilk bağlantı

Mevcut sürücü/format altyapısı üzerine cihaz modeli + donanım kimliği + Windows sürümü + mimari eşleştirmesi genişletilsin. Her üretici kaynağı için son başarılı kontrol, desteklenen modeller ve indirme yöntemi kaydedilsin. Ağ sürücüleri format öncesi USB paketine alınsın; paket manifesti sürüm, imza ve dosya özetlerini içersin. İnternete bağlanınca kalan işler devam etsin.

Windows Driver Store doğrudan dosya silerek yönetilmemeli; imzalı paket doğrulaması ve desteklenen kurulum yolu kullanılmalı. [Microsoft Driver Store açıklaması](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/driver-store).

Kabul ölçütü: Yanlış model/mimari reddedilir, bozuk veya imzası doğrulanmayan paket çalıştırılmaz; ağsız temiz kurulum senaryosu gerçek test makinesinde sınanır. BIOS/firmware ayrı, üretici denetiminde kalır. Süre: yüksek. Etki: çok yüksek.

### 3. Kesintiye dayanıklı hazırlık ve geri dönüş

Mevcut hazırlık günlüğüne adım başına önceki/sonraki durum ve yeniden başlatma noktası eklensin. Kurulumun çıkış kodu kadar yeniden taramada doğrulanmış sonucu da saklansın. Yalnız FromZero'nun değiştirdiği ve sonradan kullanıcı tarafından değiştirilmeyen ayarlar geri alınsın. Üretici kaldırmasının veya sürücü kurulumunun her zaman tam geri alınabileceği vaat edilmesin.

Kabul ölçütü: Her adım sınırında kapanma/iptal/yeniden başlatma denemesi yapılır; tamamlanan indirme boşa tekrarlanmaz ve hata görünür kalır. Süre: orta-yüksek. Etki: çok yüksek.

### 4. Kanıtlı oyun performansı

Mevcut FPS analizi ve profillerin üzerine aynı sahne, çözünürlük ve süreyle önce/sonra oturumu eklenmeli. Ortalama FPS yanında %1 düşük FPS, P99 kare süresi ve takılma sayısı gösterilmeli. Güç modu ve ayar değişiklikleri karşılaştırmada görünmeli. Çözünürlük düşürme, kalite kaybı ve güç tüketimi açıkça belirtilmeli; her bilgisayara sabit FPS artışı vaat edilmemeli.

[PresentMon](https://github.com/GameTechDev/PresentMon) Windows grafik uygulamalarının performansını yakalayıp analiz etmek için kullanılabilir; sürüm ve veri alanları sabitlenerek mevcut analiz katmanına bağlanmalı.

Kabul ölçütü: Ölçüm alınamayan deneme kazanç olarak sunulmaz; en az üç eş koşullu deneme karşılaştırılır. Oyunlara özel dosya değişiklikleri yedeklenir. Süre: orta. Etki: yüksek.

### 5. Gerçek soruna yönelik ağ onarımı

Mevcut teşhis/onarım/DNS araçlarına modem erişimi, DNS çözüm süresi, paket kaybı, gecikme değişkenliği ve Wi-Fi sinyal/kanal bilgisi tek açıklamada bağlansın. Örneğin DHCP başarısızsa yenileme, DNS sorunu varsa sağlayıcı denemesi önerilsin. Sağlıklı bağlantıda değişiklik önerilmesin. VPN ve statik IP kullanan adaptörler ayrı değerlendirilsin.

Kabul ölçütü: Öneri öncesi bulgu ve onarım sonrası aynı test gösterilir. DNS değişince ping mutlaka düşer denmez; başarısız değişiklik geri alınabilir. Süre: orta. Etki: yüksek.

### 6. Uygulama yaşam döngüsü

Kaldırma yöneticisine büyük/az kullanılan uygulamalar, başlangıç etkisi ve son güncelleme bilgisi eklenebilir. Kaldırıcı tamamlandıktan sonra kayıt gerçekten gitmiş mi yeniden kontrol edilmeli. Artık dosya önerisi eklenecekse yalnız o uygulamaya kesin bağlanan yollar önizlenmeli; paylaşılan çalışma zamanları, kullanıcı belgeleri ve profil klasörleri otomatik temizlenmemeli. Sistem bileşenleri ve paket bağımlılıkları görünür olmalı.

Kabul ölçütü: Kullanıcı uygulama verisini silmeden önce dosya listesini görür; iptal ve yeniden başlatma isteyen kaldırıcılar doğru raporlanır. Süre: orta. Etki: yüksek.

### 7. Oyun ve bileşen farkındalığı

DirectX ve Visual C++ taramasını kurulu oyunların gereksinimleriyle ilişkilendir. Eski bir oyunun gerektirdiği x86 bileşen, x64 işletim sisteminde de açıklanabilsin. Çalışır oyun için gereksiz eski paket kurulmasın. RAM bakımı mevcut oyun duraklatma ve bekleme kurallarını korusun; çalışma kümesi küçülmesi fiziksel RAM kazancıyla karıştırılmasın.

Kabul ölçütü: Gereksinim kaynağı ve mimari gösterilir, eksik olduğu doğrulanmayan paket varsayılan kurulmaz. Süre: orta. Etki: yüksek.

### 8. Sürdürülebilir FACEIT ve nişangah kataloğu

Sıralama ve crosshair güncelliği ayrı tutulmalı. Oyuncu kimliği, doğrulama tarihi ve hangi kodun hangi oyuncuya ait olduğu takip edilmeli. Bilinmeyen ilk 10 oyuncusu için başka oyuncunun kodu gösterilmemeli. HTML şema testleri, hata anında son geçerli veri ve tarih, kaynak değişimini fark eden kontrol eklenebilir. Kamusal [global sıralama](https://cssurf.site/leaderboard) mevcut API'siz kaynaktır; uygulama FACEIT resmî API'sine veya kullanıcı tokenına ihtiyaç duymaz.

Kabul ölçütü: Boş/eksik/yanlış bölge sonucu önbelleği bozamaz; güncel olduğu doğrulanmayan kod açıkça belirtilir. Süre: düşük-orta. Etki: orta.

### 9. Dağıtım, gizlilik ve erişilebilirlik

İmzalı uygulama güncellemesi, imzalı katalog manifesti, bağımlılık envanteri ve geri alınabilir sürüm dağıtımı eklenmeli. Tanılama dışa aktarımında kullanıcı adı, özel yollar ve ağ bilgileri önizlenip ayıklanmalı. Telemetri eklenirse kullanıcı tercihine bağlı olmalı. Tek retro font korunurken metin boyutu, klavye sırası, ekran okuyucu etiketleri ve %125/%150 ölçekleme ayrı sınanmalı.

Kabul ölçütü: Değiştirilmiş güncelleme reddedilir; temel görevler klavyeyle tamamlanır; dar pencerede taşma olmaz. Süre: orta-yüksek. Etki: yüksek.

## Önerilen sıra

Önce hazırlık planının açıklığı + kesintiye dayanıklılık; ardından çevrimdışı ağ sürücüsü paketi ve kaynak sağlığı. Sonra ölçümlü FPS ve hedefli ağ onarımı. Paket sayısı artışını bu doğrulama altyapısı izlemeli: kullanıcıya binlerce giriş göstermekten daha değerli sonuç, kendi donanımında gerçekten gerekli ve doğrulanmış işi bitirmektir.

Bu plan gelecekteki çalışmadır; üstteki “Bu sürümde” listesi uygulanmış özellikleri gösterir. 2 Kasım 2026.
