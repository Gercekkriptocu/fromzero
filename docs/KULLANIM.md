# FromZero 0.16.0

Hazırlık planı, çevrimdışı sürücü incelemesi, FPS laboratuvarı, bağlantı ölçümü, uygulama yönetimi ve yeni Çalışma Alanı için [0.16 sürüm notlarına](SURUM-0.16.md) bak.

Varsayılan arayüz İngilizcedir; sağ üstten **Türkçe** seçebilirsin. Tercih kaydedilir. Uygulamanın tamamı Pixelify Sans kullanır; yeni simge ana ve alt pencerelerde görünür. Uygulamaya ait pencerelerde beyaz Windows başlığı yerine ortak şeffaf başlık vardır. Ana sayfanın görünür kaydırma çubuğu gizlidir.

**Gaming → CS2 → Restore sağındaki nişangah simgesi**, pro kataloğunu uygulama içinde açar. **FACEIT top 10** sıralaması API anahtarı istemeden kamusal global HTML sayfasından yenilenir. Eksik veya başarısız yenileme son geçerli veriyi korur. Sıralama kaynağına erişim dış bağımlılıktır; her yeni oyuncunun nişangah kodunun bilindiği varsayılmaz.

**More tools → Applications** kurulu masaüstü/Store uygulamalarını logolarıyla listeler. Arama ve Details ile incele; **Uninstall** onayından sonra üreticinin veya Windows'un kaldırma işlemi başlar. Verilerini silme seçeneklerini üretici penceresinde incele. Korunan bileşenler varsayılan gizlidir; kaldırma desteklenmezse Windows Installed Apps kullanılabilir. Logosu erişilemeyen yazılım için harfli simge gösterilir. Performance ve RAM listelerinde de erişilebilen yerel uygulama simgeleri vardır.

**Updates** varsayılan kurulu uygulamaları gösterir. Kurulu/mevcut sürüm, durum, neden ve eylem ayrı yazılır. **Update available** doğrulanmış eski sürümü; **Up to date** günceli; **Version not verified** karşılaştırılamayan sürümü; **Optional · not installed** isteğe bağlı kurulumu ifade eder. Bilinmeyen veya kurulu olmayan uygulamalar otomatik güncelleme önerisine seçilmez. Yeni temel/laptop hazırlığında uygulamalar boş başlar; eski kayıtlı seçimlerin korunur.

Ayrıntılı sürüm notları ve sonraki geliştirme önerileri: [GELISIM-0.15.md](GELISIM-0.15.md).

Kontrol odası menüsü kaydırma gerektirmez. Sekiz ana bölüm solda sabittir; Başlangıç, Uygulamalar ve Geçmiş için alttaki **Diğer araçlar** düğmesini kullan. Üstteki büyük marka alanı ve sol alttaki B planı kartı kaldırıldı; Format kiti bölümü menüden erişilebilir. Kartlar, düğmeler ve seçim alanları daha yumuşak kenarlar kullanır.

## Tek tık DNS ve şeffaf başlık

**Wi-Fi ve ağ → Tek tık DNS** bölümünde fiziksel adaptörü seçip **Cloudflare**, **Google**, **Quad9** veya **Otomatik (modem / ağ)** düğmesine bas. Profil, mevcut IPv4/IPv6 adres ailelerini birlikte ayarlar; otomatik IP adresine, Wi-Fi ağına ve diğer adaptörlere dokunmaz. Windows yönetici izni isteyebilir. İlk ayarın statik/otomatik tercihleri işlemden önce yedeklenir; profiller arasında geçmek bu ilk yedeği değiştirmez. **Önceki DNS'e dön** ile başlangıç ayarını geri alabilirsin. DNS ayarı başka bir uygulama, VPN veya kullanıcı tarafından sonradan değişmişse üzerine yazılmaz; yedek korunur.

Windows'a uygulanacak işlem, adaptör GUID'sini ve mevcut DNS ayarını tekrar doğrular. Bir adres ailesinde uygulama başarısız olursa değiştirilen ailelerde önceki ayara dönmeyi dener; geri alma doğrulanamazsa yedek saklanır ve hata açıkça gösterilir. Sadece yapılandırma doğrulanır; internet veya servis sağlayıcı erişimi garanti edilmez. DNS sorguları seçilen sağlayıcıya gider. Bu düğmeler DoH/DoT açmaz, VPN/kurum içi adların çözümünü etkileyebilir ve oyun pingini düşürme garantisi vermez. Geliştirme doğrulaması gerçek DNS ayarını değiştirmeden yapıldı; yetki, iptal, profil geçişi ve geri alma senaryoları sahte ağ arayüzüyle sınandı.

Üst Windows başlık çubuğu uygulamanın piksel manzarasıyla birleşen şeffaf bir başlıkla değiştirildi. Üst alanı sürükleyebilir, çift tıklayarak büyütebilir ve sağdaki küçült / büyüt / kapat düğmelerini kullanabilirsin. Şeffaflık uygulamanın kendi arka planını gösterir.

## RAM optimizer ve boşaltıcı

**Performans → RAM optimizer'ı aç** ile RAM kullanımı, kullanılabilir fiziksel bellek ve taahhüt/sınır değerlerini canlı izle. Oyunlar ekranında da aynı bölüme erişebilirsin. **Uygulamaları tara** yalnızca aynı Windows kullanıcısının açık pencerelerini listeler. FromZero varsayılan seçilidir; başka bir uygulamaya işlem yapmak için önce penceresini küçült, listeyi yenile ve seç. **RAM boşalt** seçilen uygun işlemlere Windows EmptyWorkingSet çağrısı uygular. Uygulamalar kapanmaz; sistem, güvenlik, bilinen oyunlar ve profillerdeki oyun işlemleri korunur. İşlem anında kullanıcı, oturum, başlangıç zamanı, dosya yolu, kritik işlem durumu ve pencere uygunluğu tekrar kontrol edilir. Erişilemeyen veya değişmiş işlemler atlanır; yönetici yetkisi zorlanmaz.

Sonuç kartı kullanılabilir fiziksel RAM'in önce/sonra değerini ve her uygulamanın çalışma kümesi değişimini ayrı gösterir. Paylaşılan sayfalar nedeniyle çalışma kümesi toplamı fiziksel RAM kazancı olarak sunulmaz. Arka plandaki diğer işler toplam farkı etkileyebilir. Windows boşaltılan veriyi gerektiğinde yeniden yükler; ilk geçiş yavaşlayabilir. Bu işlem RAM kapasitesini artırmaz ve FPS kazanımı garantilemez. Sayfa dosyası, bellek sıkıştırma, kayıt defteri veya sistem önbelleği değiştirilmez.

**Otomatik bakım ve oyun öncesi ayarlar** bölümünde %80/85/90/95 eşiklerinden birini seçebilirsin. Otomatik bakım başlangıçta kapalıdır. İşlem yapılmasını istediğin uygulamaları listede seçip **Tercihleri kaydet** düğmesine bas: yalnız bu EXE yolları otomatik bakıma dahil edilir. Eşik en az 30 saniye boyunca aşılmalı; iki deneme arasında en az 10 dakika beklenir. FromZero oyun oturumu veya bilinen/profilde kayıtlı bir oyun çalışırken otomatik bakım durur. Uyku veya tarama kesintisi sonrasında baskı süresi yeniden ölçülür. FromZero kapalıyken çalışmaz; servis ya da zamanlanmış görev kurulmaz.

Oyun öncesi bakım isteğe bağlıdır. Etkinleştirip tercihleri kaydettiğinde Oyunlar → **Hazırla ve başlat** ve manuel oyun modu, oyun açılmadan önce listedeki uygun küçültülmüş uygulamalara bir kez uygular. Başka bir bilinen oyun açıksa atlanır. Pencereler yeniden büyütülmüşse veya erişim yoksa o uygulama atlanır. Otomatik listeyi sıfırla, kayıtlı uygulamaları ve iki otomatik seçeneği kapatır.

0.13.2: Hazırlama ekranı, düğmeye basmadan önce sistem diski ve bağlantı engellerini gösterir. Yetersiz alan varsa gereken ve eksik miktar görünür; Temizlikle alan aç veya Windows depolama üzerinden yer açıp Tekrar kontrol et seç. Başka bir diskteki boş alan Windows sistem diskinin yerini tutmaz. Plan onayını reddetmek veya bir ön kontrolde durmak tamamlandı sonucu üretmez.

0.13.1: Yönetici yetkisine rağmen hazırlama çalışanının açılışta kapanmasına neden olan hata giderildi. Eski pencereyi kapatıp yeni FromZero.exe ile kayıtlı planına devam edebilirsin. Başlangıç hatalarında gerçek neden ve rapor dosyası gösterilir.

## Format sonrası tek akış

**Bilgisayarı hazırla** ekranında Hazırla, Durumu kontrol et, USB ve kurtarma bölümleri var. Günlük, oyun, yayın/içerik, geliştirme veya dizüstü profilini seç; uygulama/oyun seçimlerini ve isteğe bağlı güç/görüntü tercihlerini açılır bölümlerden düzenle. **Planı incele** kurulum yapmadan model, aygıt ve seçim özetini oluşturur. **Bilgisayarımı hazırla** planı gösterir; kabulünden sonra aynı kullanıcı için yönetici çalışanı başlar.

Otomatik uygulama desteği 21 ürün içerir; genel 600 kayıtlık katalog ayrı kapsamdadır. Açık uygulamalar zorla kapatılmaz, imzası doğrulanmayan paket kurulmaz. Yeniden başlatma gerekiyorsa kaldığın yer saklanır; UAC atlanmaz. BIOS/firmware ve büyük Windows yükseltmeleri plana alınmaz. Sürücü yedeği alınamazsa sürücü yazımı yapılmaz. Kritik adımlardan sonra olası bozulma görülürse kuyruk inceleme için durur.

Durumu kontrol et yalnız okur: DNS ve HTTPS ayrı ölçülür, etkin grafik sürücüsü, ses uçları, ekran ve uygulama dosyaları incelenir. Gerçek ses duyma, Bluetooth eşleşme, OBS kodlayıcı ve FPS kullanıcı testi olarak kalır. USB ve kurtarma kısmında dış disk kiti, raporlar ve önce/sonra farkları bulunur. Güç tercihi ve desteklenen oyun görüntü ayarları yedekten geri alınabilir; evrensel sürücü geri alma iddiası yoktur.

[Profiller, 21 uygulama, oyunlar, USB ve kurtarmanın ayrıntılı kılavuzu](HAZIRLIK-0.13.md). Gerçek kurulum ve yeniden başlatma temiz Windows test cihazında ayrıca kabul testi gerektirir; geliştirme bilgisayarında kurulmadı veya sistem ayarı değiştirilmedi.

## Üretici araçları ve resmi destek

Güncellemeler → Üretici araçları: **55 araç / kurulum merkezi**, **164 resmi destek sayfası**, **11 uygulama içi indirme tarifi**. Araçlar ve Destek sayfaları sekmeleri arasında geçebilir, marka veya araç adı arayabilir, 9 donanım kategorisine göre süzebilirsin. Liste 12 kartlık sayfalara ayrılır. “Yalnız algılanan markalar” yerel sistem/anakart üreticisini ve mevcut cihaz adları/donanım kimliklerini kullanır; marka eşleşmesi sürücü veya model uyumluluğu garantisi değildir.

İndirme kutusu bulunan 11 araç uygulama içindeki doğrulamalı kuyruğa eklenir. NVIDIA, AMD, Intel, Dell Command Update, HP Image Assistant, Lenovo System Update, Logi Options+, Logitech G HUB, Corsair iCUE, SteelSeries GG ve Samsung Magician desteklenir. Seçilenleri indir ile dosya indirilir, resmi HTTPS alan adı ve Windows yayıncı imzası denetlenir; kurulum ayrıca kullanıcı tarafından başlatılır. BIOS/firmware işlemleri üreticinin kendi model uyumlu akışında yapılmalıdır. Araçların tümü genel Windows sürücü tarayıcısı değildir; SSD, ses, çevre birimi ve firmware araçları yalnız kendi ürün kapsamlarında çalışır.

Diğer araçlarda Üretici sayfası, destek dizininde Destek sayfasını aç kullanılır. Store/MSIX/ZIP veya model seçimi gereken dağıtımlar üreticinin sayfasında tamamlanır. Her araçta Uyumluluk ayrıntısı vardır. MediaTek ve Qualcomm Wi-Fi için bilgisayar/adaptör üreticisinin model sayfası tercih edilir. Lenovo System Update eski araç olarak etiketlidir; bakım sonu 31 Ekim 2026, destek sonu 31 Mart 2027; Commercial Vantage önerilir. Üretici bağlantıları otomatik taranmış güncelleme teklifi değildir. Bağlantıların bölge, hesap veya bot koruması nedeniyle erişimi değişebilir.

FromZero 0.11.0 görünümünde özel amblem ve başlık simgesi bulunmaz.

Windows için yerel bakım, kurtarma, sürücü ve oyun hazırlık uygulaması.

Arşivi bir klasöre çıkar ve FromZero.exe dosyasını çalıştır. Yanındaki dosyalar gereklidir. .NET çalışma ortamı pakete dahildir. Bu ön izleme Windows x64 içindir.

## Arayüz

Tek piksel dünya bütün ekranların arkasında devam eder; üstte ayrı mahalle şeridi yoktur. Güncelleme merkezindeki kategori düğmeleri, arama ve filtrelerle kaynağa odaklanabilir; canlı kuyruk sayacından seçimlerini görebilirsin. “Önerilenleri seç” güncelleme gereken kurulu ürünleri, “Seçimi boşalt” kuyruk temizliğini sağlar.

## Güncellemeler

“Tüm kaynakları tara”, Windows Update'in bu bilgisayara sunduğu sürücü/Windows tekliflerini, .NET sürüm hatlarını, 600 uygulama/sürüm hattını ve DirectX/Visual C++ bileşenlerini kontrol eder. Uygulamalar 30 doğrudan üretici kaydı ve 570 Scoop Extras topluluk manifestinden oluşur. Scoop/WinGet kurulumu gerekmez; manifest betikleri çalıştırılmaz. Yeni geniş katalogda tek dosyalı x64/ortak EXE/MSI paketleri kabul edilir; SHA-256 ve güvenilir Windows dijital imzası birlikte zorunludur. İmzasız paketler reddedilir. Bu 600 kaydın hepsinin indirme testinden geçtiği anlamına gelmez. Kaynak ve tarama ayrıntıları “Tarama ve indirme seçenekleri” altındadır. Üreticiye erişilemiyorsa kayıt son bilinen sürümüyle listede kalır ve “Kayıt kontrolü gerekli” görünür. Kaynak hataları ayrıca gösterilir; başarısız tarama “güncel” olarak sunulmaz ve otomatik önerilmez. Metadata 15 dakika önbellekte tutulur. Uygulamalar sekmesinde “Yalnız uygulamaları tara” Windows taramasını beklemeden 600 kaydı kontrol eder; indirmeyi başlatmaz. Uygulama adıyla ara, yalnız kurulu olanları filtrele veya 20 kayıtlık sayfalarla ilerle. Kurum ve Windows Update ilkeleri değiştirilmez.

Visual C++ 2005, 2008, 2010, 2012, 2013 ve v14 x86/x64 paketleri desteklenir. .NET Runtime, Windows Desktop Runtime ve ASP.NET Core Runtime ve .NET SDK için 3.0 ve sonraki kararlı hatlar Microsoft'un sürüm kayıtlarından alınır. Her hattın en yeni yaması ve sistemde bulunan bütün kararlı yamaları listelenir. Destek dışı ve eksik hatlar gerektiğinde elle seçilebilir; otomatik indirmeye eklenmez.

XNA 4.0 MSI ve DirectX çevrimdışı paketi de ayrı seçilebilir. Uygulamalar: Firefox, VS Code, Chrome, Edge, VLC, 7-Zip, Git, Notepad++, OBS Studio, PowerToys, Brave, Thunderbird, Vivaldi, PowerShell 7, ShareX, Audacity, HandBrake, Rufus, WinMerge, KeePassXC, MPC-HC, RustDesk, LocalSend, ScreenToGif, DBeaver Community, HeidiSQL, Joplin, Obsidian, Node.js LTS ve Node.js Current. Node LTS/Current iki ayrı sürüm hattıdır; kurulu farklı ana sürüm otomatik değiştirilmez. Kategori ve arama ile listeyi daraltabilirsin. Kararlı üretici sürümleri kurulu sürümlerle karşılaştırılır; bütün Windows uygulamaları için genel kapsam iddiası yoktur.

.NET SDK'nın 100/200/300/400 gibi bantları ayrı taranır ve her birinin en yeni kararlı yaması gösterilir. Eski bantlar elle seçim içindir. Örneğin 10.0.1xx kuruluysa 10.0.4xx otomatik güncelleme gibi işaretlenmez. Her ürünün bütün tarihsel kurucuları indirilmez.

Üretici araçları bölümünde Dell, HP, Lenovo, ASUS, MSI, Acer, Gigabyte ve ASRock resmi destek sayfaları vardır. Modelini seçerek OEM Wi-Fi, yonga seti, Bluetooth, ses ve diğer sürücüleri arayabilirsin. Bu sayfalar otomatik taranmış paket sayısına eklenmez. Store uygulamaları için Microsoft Store güncelleme ekranı açılır; FromZero'nun bağımsız uygulama kataloğuyla karıştırılmaz.

Otomatik indirme, etkinleştirildiğinde kapsamlı tarama tamamlanınca kurulu desteklenen .NET hatlarının yeni yamalarını, kurulu desteklenen uygulamaların güncellemelerini ve lisansı önceden kabul edilmiş uygun sürücü tekliflerini indirir. Taramalar kullanıcı tarafından başlatılır; arka planda zamanlanmış tarama yoktur. Gizli, yerine yeni sürüm geçmiş ve firmware/BIOS teklifleri otomatik indirilmez. Mobil bağlantıda veri kullanabilir.

Kurucular FromZero'nun Downloads klasöründe tutulur. Resmi HTTPS adresleri, yayıncı imzası ve bütünlük doğrulanır; .NET paketleri ayrıca Microsoft'un SHA-512 kaydıyla karşılaştırılır. Windows paketleri Windows Update önbelleğine indirilir ve FromZero tarafından dışa aktarılmaz. 7-Zip, ShareX ve MPC-HC'nin doğrulanan resmi kurucuları imzasızdır; yalnızca her birinin sabit izinli üretici GitHub deposundaki yayın varlığının SHA-256 kaydıyla doğrulanır. Arayüz “dijital imza yok” bilgisini gösterir. Bu izin başka depo, uygulama veya hash kaydı olmayan pakete uygulanmaz. Diğer kurucularda Windows imza/yayıncı kontrolü zorunludur. VLC, üreticinin resmi ayna listesindeki HTTPS sunucusundan alınır ve VideoLAN imzası aranır. İndirme kurulumu başlatmaz. Sürücü kurulumu ayrı eylemdir ve önce yedek alır. Windows yazılım güncellemeleri Windows Ayarları'ndan kurulabilir. Üretici araçları, kendi destekledikleri donanımı ayrıca denetler.

Her üreticinin yayımladığı bütün tarihsel sürücü sürümlerine eksiksiz erişim garantisi yoktur. Windows, donanım ve üretici destek kapsamı belirleyicidir. En yeni sürüm her cihaz için en uygun paket olmayabilir.

## Temizlik

233 hedef kuralıyla önbellek/günlükleri tara; tam dosya listesini incele ve seç. Açık uygulamaların, kilitli veya tarama sonrası değişmiş dosyaların temizliği atlanır. Kritik Windows dizinleri, bağlantılar ve uygulamanın kendi verileri korunur. Riskli hedefler ayrıca açılır. Silme kalıcıdır ve ayrı seçim onayı ister. Rapor/Windows aracı gerektiren alanlar doğrudan silinmez. Testler dışında dosyalar kendiliğinden temizlenmez.

## Otomatik oyun görüntü ayarları

Oyunlar bölümünde Counter-Strike 2 ve Valorant için “Rekabetçi FPS” ve “Dengeli görüntü” profilleri bulunur. Steam hesaplarındaki mevcut cs2_video.txt ve Valorant hesaplarındaki mevcut GameUserSettings.ini dosyaları algılanır. Doğru hesabı seç; oyunu kapat; “FPS ayarlarını uygula” ile değişiklik listesini inceleyip onayla. Tanınan anahtarlar düzenlenir, eksik anahtar eklenmez. Çözünürlük, yenileme hızı, çözünürlük ölçeği, giriş ve hesap ayarları korunur. VSync kapatma isteğe bağlıdır.

Dosyanın tamamı değişiklikten önce yerelde yedeklenir. Geri al, dosya değişmemişse özgün baytları geri yükler; kullanıcı daha sonra ayar değiştirmişse yalnızca profilin hâlâ aynı olan değerlerini geri alır. Salt okunur, bağlantılı veya oyun çalışırken açık olan dosyalar değiştirilmez. Oyun/bulut eşitlemesi ayarları yeniden yazabilir; oyunu açınca Video menüsünden doğrula. Valorant dosya şeması sürüme göre değişebilir; tanınmıyorsa işlem reddedilir. Gerçek oyun içi FPS kazancı garanti edilmez veya ölçülmüş gibi gösterilmez.

Dayanaklar: [Riot grafik/FPS rehberi](https://support.riotgames.com/en-us/riot/performance/optimize-graphics-settings-for-better-fps-windows-and-mac), [Unreal kalite grupları](https://dev.epicgames.com/documentation/en-us/unreal-engine/scalability-reference-for-unreal-engine), [NVIDIA CS2 Reflex rehberi](https://www.nvidia.com/en-us/geforce/news/counter-strike-2-released-featuring-nvidia-reflex/). Reflex ve çok çekirdekli çizim, desteklenen oyun menüsünden ayrıca doğrulanmalıdır. Profiller görüntü seçeneklerine odaklanır.

## Performans ve oyun

Performans ekranındaki “FPS profilini uygula”, seçtiğin Windows güç planı/animasyon tercihini ve NVIDIA genel 3D ayarlarını yedekler. NVIDIA'da maksimum performans, yüksek performans doku filtreleme ve en yüksek yenileme hızı tercihi varsayılan seçilir; her biri kapatılabilir. VSync kapatma ayrıca seçilir ve varsayılan kapalıdır. Oyunlara özel NVIDIA profilleri genel tercihi geçersiz kılabilir. Doku kalitesi düşebilir, güç/ısı artabilir; VSync kapatma yırtılma oluşturabilir. Kaydedilen NVIDIA değerleri yeniden okunarak doğrulanır. Başarısızlıkta geri alma denenir; geri alma tamamlanamazsa kurtarma kaydı korunur. “Önceki ayarlara dön” sonradan değiştirdiğin değerleri korur. Profil kalıcıdır; oyun oturumu ayarları ise oturum sonunda geri alınır.

Ekran kartları mevcut çözünürlükte sürücünün sunduğu Hz değerlerini listeler. En yüksek değer başta seçilir. “Seçilen Hz'yi dene” önce Windows destek testini yapar, eski modu saklar ve geçici uygular. 15 saniyede “Görüntü iyi / koru” seçilmezse UI'dan bağımsız zamanlayıcı önceki hızı geri yükler. Onayla kayıt kalıcı olur; ayrıca geri alınabilir. Çözünürlük, renk derinliği, konum ve yön değiştirilmez. Desteklenmeyen/özel hız üretilmez. Yarım kalan deneme bir sonraki açılışta geri alınır; bağlantısı kopmuş ekran için yedek saklanır. Hz artışı render FPS'si artışı anlamına gelmez.

CS2/Valorant'ta Reflex seçeneği oyun menüsünden ayrıca denenmelidir. Reflex + Boost daha fazla güç kullanıp FPS'yi azaltabilir. G-Sync/VRR'de uygun FPS sınırı, VSync ve Reflex birlikte daha akıcı görüntü sağlayabilir. Otomatik profil G-Sync/HDR, hız aşırtma veya voltaj değiştirmez. FPS kazancı donanıma/oyuna bağlıdır ve bu bilgisayarda canlı oyunla ölçülmedi.

## CS2 Steam başlatma satırı

Oyunlar → “CS2 / Steam başlatma seçenekleri” bölümünde sınırsız, 240, 300 veya özel 60–1000 FPS sınırını seç. “Başlatma satırını kopyala” ile örneğin `+fps_max 0 +fps_max_ui 120` panoya alınır. Steam → Kütüphane → Counter-Strike 2 → Özellikler → Genel → Başlatma seçenekleri alanına yapıştır. Önce eski satırını sakla; aynı FPS komutlarını tekrarlama. Steam dosyaları otomatik düzenlenmez.

0 oyun FPS sınırını kaldırır; menü 120 FPS ile sınırlanır. Bu satır CPU/GPU kapasitesini artırmaz; mevcut oyun sınırına takılıysan üretilen FPS'nin daha yukarı çıkmasına izin verebilir. Dış sürücü/VSync sınırları geçerlidir. Oyunu yeniden başlatıp Gelişmiş Video oyun/menü sınırlarını kontrol et. Eski CS:GO `-high`, `-threads`, `-heapsize`, `-dxlevel`, `-tickrate` listeleri eklenmez. Geri almak için eklediğin iki komutu kaldırıp önceki değerleri oyun menüsünden geri seç.

Teknik dayanaklar: [NVIDIA resmi ayar tanımları](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h), [Windows ekran modu API'si](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw), [Steam başlatma ayarları](https://help.steampowered.com/en/faqs/view/7D01-D2DD-D75E-2955), [Valve 25 Haziran 2024 FPS sınırları ve görüntü önerileri](https://store.steampowered.com/news/posts/?appids=730&enddate=1719958202&feed=steam_community_announcements).

## CS2 pro crosshair kataloğu

Oyunlar → “Pro crosshair kataloğu” ana uygulama içinde açılır. “50 pro oyuncu”, “FACEIT ilk 10 crosshair” ve “Kendi kodun” sekmeleri vardır. Oyuncu adıyla ara; yıldız ile favoriye ekle; gece/açık duvar/orman/gerçek Dust2 zeminlerinde görünürlüğü incele. Dust2, Valve'ın resmi CS2 harita görüntüsüdür; piksel yorum kaldırıldı. Büyüt düğmesi aynı sayfada büyük önizleme açar. Kendi kodun sekmesinde yeni biçimde CS kodunu yapıştırıp kontrol et; oyun dosyası değişmez.

“FACEIT ilk 10 crosshair”, EU, NA, SA, SEA ve OCE bölge ilk 10 kayıtlarını ELO ile birleştirir. Sıralama kaynağı Faceit Tracker'dır. On hesabın crosshair kodu crosshair.club'un eşleşen oyuncu kayıtlarından gelir. Kodun kaynakta görüldüğü tarih, kontrol tarihi ve oyuncunun gerçek katalog adı gösterilir. “Kodu kopyala” kodu panoya alır; “Büyüt” aynı sayfada önizler. “Crosshair'i yenile” yalnız o hesabın kodunu; “10 crosshair'i yenile” mevcut listedeki on kodu kontrol eder. “ELO sıralamasını yenile” beş bölgeyi birlikte tarar; biri başarısızsa önceki sıralama korunur. Kaynak hatası eski crosshair'i silmez. Yeni bir hesap için kaynak eşlemesi yoksa kod tahmin edilmez. Bu resmi FACEIT anlık crosshair akışı değildir.

“Kodu kopyala” ile CS2'nin yeni biçimindeki paylaşım kodunu al; oyundaki Ayarlar → Crosshair/Scopes → Paylaş/İçe aktar alanına yapıştır. Önce kendi crosshair kodunu oyunda dışa aktararak sakla. Kodlar ProSettings'in kayıtlı sayısal ayarlarından FromZero tarafından üretilir; oyuncunun kendisinin yayımladığı kod iddiası taşımaz. Her önizleme aynı kodun çözülen değerlerinden çizilir. Kaynak ve kontrol tarihi kartta görünür; “Kaydı yenile” yalnız seçilen oyuncuyu internetten kontrol eder. Kaynak şeması değişir veya erişim olmazsa önceki veri korunur.

Gömülü kayıtlar 27 Ekim 2026 kontrolüne aittir. Oyuncunun sayfasının son değiştirilme tarihi, ayarın kullanıldığı maç tarihi anlamına gelmez. Önizleme durağan, büyütülmüş şematik çizimdir; dinamik açılma, recoil, scope ve çözünürlük ölçeğinin oyun içindeki görünümünü birebir simüle etmez. Oyunda doğrula. Crosshair FPS artırma aracı değildir. Otomatik oyun dosyası veya Steam hesabı değişikliği yapılmaz.

Kaynaklar: [ProSettings oyuncu kayıtları](https://prosettings.net/players/donk/), [Valve yeni crosshair piksel birimleri](https://www.counter-strike.net/newsentry/674006995886408819), [MIT lisanslı kod biçimi referansı](https://github.com/akiver/csgo-sharecode). `CrosshairCodec-LICENSE.txt` pakete dahildir.

## Veriler ve lisanslar

Yerel veriler: `%LOCALAPPDATA%\FromZero`. İndirme kayıtları, sürücü yedekleri, performans kurtarma kayıtları, crosshair favorileri/kayıtları, sürüm metadata önbelleği ve işlem geçmişi burada tutulur. Lisans bildirimleri `THIRD-PARTY-NOTICES.txt`, `PixelifySans-OFL.txt`, `QRCoder-LICENSE.txt`, `CrosshairCodec-LICENSE.txt` ve `NVIDIA-NVAPI-NOTICE.txt` dosyalarındadır.


## Geniş üretici desteği ve sade ekranlar

Üretici araçlarında NVIDIA/AMD/Intel yanında Dell Command Update, HP Image Assistant ve Lenovo System Update kurucuları bulunur. Altı araç desteklenen donanım ve Windows sürümleri için üreticinin kendi akışını kullanır; FromZero yalnız imzalı kurucuyu indirir, kendiliğinden çalıştırmaz. Dell/HP araçları desteklenen iş bilgisayarları içindir. Lenovo System Update bakımı 31 Ekim 2026'da, desteği 31 Mart 2027'de biter; Lenovo Commercial Vantage'a geçiş önerir. On altı marka bağlantısı destek sayfası açar; otomatik taranmış paket sayılmaz.

Oyun kütüphanesi ve FPS ölçümü, performans profili seçenekleri ve ekran yenileme ayarları, kapsül ayrıntıları ve kurulum araçları ilgili katlanır başlıklardan açılır. Crosshair kartlarında kaynak düğmesi veya ProSettings referans satırı bulunmaz. Kaynak/tarih kayıtları uygulama verilerinde korunur. FACEIT sekmesinin adı “FACEIT ilk 10”dur.



