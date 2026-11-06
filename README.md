# FromZero

Windows için piksel sanatından ilham alan yerel kurtarma, sürücü ve oyun hazırlık uygulaması. C# / .NET 10 / WPF. Sürüm **0.20.2 ön izleme**.

## İndir / Download

**[FromZero Setup — Windows x64](https://github.com/Gercekkriptocu/fromzero/releases/latest/download/FromZero-Setup-win-x64.exe)** · [SHA-256](https://github.com/Gercekkriptocu/fromzero/releases/latest/download/FromZero-Setup-win-x64.exe.sha256) · [Sürümler / Releases](https://github.com/Gercekkriptocu/fromzero/releases)

Kurulum sihirbazı İngilizce ve Türkçe seçenekleri sunar. Uygulama mevcut dil tercihini korur ve ilk kurulumda sihirbazın dilini kullanır. Windows 10 (21H2 veya sonrası) ve Windows 11 için x64 paket kendi .NET çalışma zamanını içerir.

Kurucu varsayılan olarak Program Files klasörüne yükler. FromZero açılışta Windows yönetici onayı alır ve aynı oturumdaki bakım komutları bu yetkiyi kullanır.

0.20 sürümünde tek listede güncelleme sonuçları, ortada hareketli piksel radar ve donanım kimliğiyle Microsoft Update Catalog sürücü araması bulunur. [Sürüm notları](docs/SURUM-0.20.md) · [Driver Booster REA incelemesi ve kanıtlar](docs/DRIVER-BOOSTER-REA.md)

Kaynak kodu derlemek için Windows'ta .NET 10 SDK ile `dotnet build FromZero.slnx` çalıştır. Kurucu üretmek için [Inno Setup](https://jrsoftware.org/isdl.php) ve `pwsh -File scripts/build-setup.ps1` kullan. Derleyici farklı bir konumdaysa `-Compiler` ile ISCC.exe yolunu ver. [0.19 sürüm notları](docs/SURUM-0.19.md)

0.16 sürümünde açıklamalı hazırlık planı, ağ öncelikli doğrulanabilir USB sürücü dizini, deneme günlüğü, tekrarlı FPS karşılaştırması, oyun bağımlılığı incelemesi, bağlantı kalitesi ve Çalışma Alanı eklendi. [Sürüm notları](docs/SURUM-0.16.md)

## 0.15.0 — dil, uygulama yönetimi ve güncelleme açıklığı

İngilizce varsayılan arayüz ve anlık Türkçe seçimi; tek retro font; tüm uygulama pencerelerinde simge ve ortak şeffaf başlık; görünür ana sayfa scrollbarı gizli. CS2 kartında simgeli uygulama içi crosshair kataloğu ve API anahtarsız kamusal global FACEIT sıralama yenilemesi. Yerel simgeli masaüstü/MSIX kaldırma yöneticisi, performans uygulama simgeleri, güncel/eski/bilinmeyen/isteğe bağlı ayrımı ve gereksiz varsayılan uygulama seçiminin kaldırılması.

Kullanım ve kapsam: [docs/KULLANIM.md](docs/KULLANIM.md). Ayrıntılı gelişim önerileri: [docs/GELISIM-0.15.md](docs/GELISIM-0.15.md).

## 0.14.2 — sade kontrol odası

Sol menüdeki kaydırma alanı, büyük FromZero/Windows bakım istasyonu başlığı ve B planı kartı kaldırıldı. Sekiz ana bölüm sabit menüde, Başlangıç/Uygulamalar/Geçmiş ise klavye ile de kullanılabilen Diğer araçlar menüsünde bulunur. Kartlar, düğmeler, arama kutuları, seçim alanları ve açılır listeler yuvarlatıldı; kenarlıklar ve panel renkleri yumuşatıldı. İçerik sayfaları gerektiğinde kaydırılabilir; sol menü kaymaz.

## 0.14.1 — tek tık DNS ve şeffaf başlık

Bağlantı bölümünden Cloudflare, Google, Quad9 veya otomatik DNS seçilebilir. Fiziksel adaptör bazında IPv4/IPv6 ayarlanır; ilk statik/otomatik DNS tercihi yedeklenir. Profil geçişleri ilk yedeği korur, geri alma dışarıdan değişen ayarların üzerine yazmaz. Yönetici işlemi GUID ve mevcut ayarı tekrar kontrol eder; kısmi hatada değişiklikleri geri almayı dener. Gerçek DNS yazımı geliştirme bilgisayarında yapılmadı; geçiş/geri alma/iptal senaryoları sahte backend ve PowerShell sözdizimi kontrolüyle, gerçek adaptör taraması salt okunur olarak doğrulandı.

Şeffaf özel başlık WPF WindowChrome ile piksel manzarasına bağlandı; sürükleme, büyütme/geri yükleme, küçültme ve kapatma korunur. Dayanaklar: [Microsoft DNS istemci ayarları](https://learn.microsoft.com/en-us/powershell/module/dnsclient/set-dnsclientserveraddress), [Cloudflare adresleri](https://developers.cloudflare.com/1.1.1.1/setup/windows/), [Google adresleri](https://developers.google.com/speed/public-dns/docs/using), [Quad9 adresleri](https://docs.quad9.net/services/), [WPF WindowChrome](https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome).

## 0.14.0 — RAM optimizer

Performans ve Oyunlar ekranlarından erişilen bellek merkezi: canlı fiziksel RAM ve commit ölçümü, seçili küçültülmüş uygulamalarda Windows EmptyWorkingSet ile boşaltma, uygulama bazında sonuç ve önce/sonra kullanılabilir bellek. Otomatik bakım isteğe bağlıdır; seçili EXE yollarıyla, 30 saniye süren bellek baskısında ve en az 10 dakika aralıkla çalışır. Bilinen oyunlar açıkken durur; oyun başlatmadan önce isteğe bağlı tek seferlik bakım uygulanabilir.

Sistem/güvenlik/oyun işlemleri, ön plandaki veya küçültülmemiş dış uygulamalar korunur. Kullanıcı, oturum, PID başlangıç zamanı ve EXE yolu işlem anında tekrar doğrulanır. Uygulamalar kapatılmaz, pagefile/registry/standby list değiştirilmez. Çalışma kümesi azalması fiziksel RAM kazancıyla eş tutulmaz; FPS artışı garantisi verilmez. Teknik dayanak: [Microsoft EmptyWorkingSet](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset) ve [bellek performans sayaçları](https://learn.microsoft.com/en-us/windows/win32/memory/memory-performance-information).

## 0.13.2 — görünür hazırlama engelleri

Disk/bağlantı/sistem ön kontrolleri, yükseltme ve indirme başlamadan önce hazırlama ekranında gösterilir. Disk yetersizse sistem sürücüsü, gereken alan ve eksik miktar ile Temizlik/Depolama bağlantıları görünür. Aynı ön kontrol kurulum çalışanında tekrar kullanılır; 5 GiB koruması azaltılmadı. Engellenen veya kullanıcı tarafından onaylanmayan hazırlama tamamlandı olarak gösterilmez. Salt okunur worker-check raporu açılabilir plan ile kuruluma hazır sistem durumlarını ayrı bildirir.

## 0.13.1 — hazırlama başlangıcı düzeltmesi

Yönetici olarak da görülen hazırlama çalışanının kapanma hatası düzeltildi. Başlangıç penceresi yalnız normal uygulama modunda açılır; arka plan çalışanına geçersiz boş StartupUri atanmaz. Yönetici uygulama mevcut yetkisini kullanır; standart uygulama gerektiğinde yükseltme ister. Erken hataların gerçek nedeni plan kaydına ve worker-error.json raporuna yazılır. Penceresiz başlangıç denetimi kurulum, yeniden başlatma veya açılış kaydı değişikliği yapmaz.

## 0.13.0 — kişisel hazırlama ve çalışırlık

Hazırlama ekranı **Hazırla / Durumu kontrol et / USB ve kurtarma** olarak sadeleşti. Beş gerçek kullanım profili, **21 uygulamada** derlenmiş otomatik kurulum tarifi ve CS2, VALORANT, Fortnite, GTA V, Minecraft, Apex için ayrı yerel kurulum/başlatıcı kontrolleri var. Model ve tam donanım kimlikleri tarifte görünür; marka tek başına uyumluluk sayılmaz.

Ağ geçidi, DNS, HTTPS, grafik sürücüsü, etkin ses uçları, uygulama dosyaları ve ekran modları gerçek kaynaklardan okunur. Duyma, eşleşme, kodlayıcı ve oyun FPS'si gibi ölçülmeyen sonuçlar kullanıcı testi olarak kalır. Kritik adımlar çevresindeki kayıtlar önce/sonra karşılaştırılır; olası bozulmada kuyruk kontrol için durur. İsteğe bağlı güç ve CS2/VALORANT görüntü tercihleri yedekli uygulanır; geri alma sonradan yapılan dış değişiklikleri korur. Piksel karakterin çalışması gerçek adımları izler.

277 temel kontrol, 307 canlı kaynak/indirme/readiness kontrolü geçti. 21 kurulum kaynağı çözümlendi; Git, Node.js LTS, OBS, KeePassXC ve Audacity gerçek indirme/imza/bütünlük testinden geçti. Kurucular ve sistem değişiklikleri geliştirme bilgisayarında çalıştırılmadı; temiz Windows'ta tam kurulum/reboot kabul testi gereklidir. [0.13 ayrıntıları ve kullanım](docs/HAZIRLIK-0.13.md).

## 0.12.0 — format sonrası hazırlama motoru

Ana ekran ve menüde **Bilgisayarımı hazırla** akışı bulunur. Temel ve oyun profili; beş seçilebilir uygulama (Firefox, Chrome, VLC, Steam, VS Code); Windows/sürücü/oyun bileşeni/üretici uygulaması seçenekleri; kurulum yapmadan donanıma göre plan oluşturma vardır. Onaylanan plan bir yönetici çalışanına iletilir. Yalnız derlenmiş kurulum tarifleri çalışır; 600 kayıtlık genel katalogdaki manifestler yönetici komutlarına dönüşmez.

Akış: sistem/alan/ağ kontrolü → üçüncü taraf sürücü yedeği → seçilmiş USB'deki tam donanım kimliği eşleşen imzalı INF'ler → Windows Update'in sunduğu sürücüler (ağ/yonga/grafik önceliği) → desteklenen Dell/HP iş modellerinde üretici motoru → Windows güvenlik/kararlılık güncellemeleri → Visual C++ / oyun bileşenleri → eksik GPU yardımcı uygulaması → seçili uygulamalar → cihaz ve kurulum kayıtlarının kontrolü. Her Windows aşaması en fazla üç tarama turu yapar. Önizlemeler, firmware/BIOS ve Windows özellik yükseltmeleri dışarıda kalır.

Kurulum kuyruğu her adımda atomik kaydedilir. Başarısız uygulama diğer bağımsız işleri engellemez; sürücü yedeği başarısızsa sürücü yazımı yapılmaz. Durdurma, devam eden kurucuyu öldürmeden adım/paket sınırında uygulanır. Yeniden başlatma gerektiğinde kuyruk bekler. Oturum açılışında açılan FromZero, yeniden başlatmayı doğruladıktan sonra yönetici izniyle devam eder. Tamamlanmış uygulamalar tekrar kurulmaz; son doğrulama yenilenir. Dört yeniden başlatma turu ve adım başına dört denemeden sonra inceleme istenir.

**USB hazırlama paketi**, mevcut sürücüleri ve seçilen uygulama/bileşen kurucularını, profil ve SHA-256 listesiyle taşır. Oyun profilinde tam DirectX redist kullanılır. USB'den App/FromZero.exe açıldığında seçimler ve paket kökü okunur; kendiliğinden kuruluma başlanmaz. Ağ sürücüsü USB'de yoksa bağlantı sağlamak için telefon USB interneti veya başka bilgisayarda hazırlanmış uygun sürücü gerekir. Windows güncellemeleri çevrimdışı bu paketle sağlanmaz.

**Gerçek kapsam:** Windows'un donanıma sunduğu uygun grafik sürücüsü otomatik kurulabilir; tüm NVIDIA/AMD/Intel tarihsel sürücülerine bağımsız evrensel erişim iddiası yoktur. NVIDIA App/AMD/Intel yardımcı kurucuları gerekli olduğunda üreticinin etkileşimli ekranını açar; sessiz parametre tahmini yapılmaz. Dell Command Update sürücülerle sınırlı çalıştırılır; gerekirse resmi .NET Desktop 10 ön koşulu tamamlanır. HP HPIA yalnız belirlenen iş modeli ailelerinde denenir, destek dışı sonuçlar raporlanır. Lenovo ve diğer markaların model özel portal/araçları genel dizinde kalır; bu akışta temel otomatik kaynak Windows Update'tir. Steam zaten kuruluysa kendi güncelleyicisine bırakılır. Tüm 600 katalog kaydı için otomatik kurulum desteği verilmez.

Yeni Chrome MSI, VLC ve VS Code sistem kurucuları gerçek indirme, imza/yayıncı ve yeniden bütünlük kontrolünden geçti. Kaynak hedef sürümleri ve Dell .NET bağımlılık metadata'sı canlı kontrol edildi. Geliştirme sırasında gerçek kurucu/sürücü/Windows güncellemesi çalıştırılmadı; yönetici kurulumları ve gerçek yeniden başlatma döngüsü temiz Windows test makinesinde ayrıca kabul testine ihtiyaç duyar. [Kurulum mimarisi ve kaynaklar](docs/AUTOMATIK-KURULUM.md).

## 0.11.0 — geniş üretici desteği ve logosuz görünüm

Üretici dizini **16 → 164 resmi destek sayfası** ve **55 araç / kurulum merkezi** içerir. Bilgisayar, anakart/GPU, yonga, ağ, depolama, çevre birimleri, ses, yazıcı/tarayıcı ve USB/görüntü/dock olarak 9 kategoriye ayrılır. Araçlar ve destek sayfaları ayrı uygulama içi sekmelerdedir; marka/araç araması, kategori, algılanan markalar filtresi ve 12 kartlık sayfalar bulunur. Marka eşleşmesi yerel sistem/anakart üreticisi ile mevcut cihaz adları ve donanım kimliklerinden yapılır; sürücü/model uyumluluğu olarak sunulmaz. Model özel OEM indirmeleri ve Store/MSIX/arşiv dağıtımları üretici sayfasında açılır.

Üretici aracı indirme tarifleri **6 → 11**. Logi Options+, Logitech G HUB, Corsair iCUE, SteelSeries GG ve Samsung Magician eklendi; gerçek dosyalar indirildi ve Windows imzası/yayıncısı, SHA-256 kaydı ve önbelleği yeniden doğrulama kontrolünden geçti. Kurucular çalıştırılmadı. SteelSeries'in resmi künyesindeki GN Hearing A/S adı indirme imzasıyla eşleşir: [şirket künyesi](https://steelseries.com/policies). Dört yeni tarif güncel bağlantıyı resmi sayfadan çözer; GG üreticinin güncel kurucu yönlendirmesini kullanır. HTTPS/alan adı/yayıncı kuralları devam eder; belirsiz paket reddedilir. Meta sayfaların erişimi ayrıca `scripts/verify-manufacturer-pages.ps1` ile denetlenir; engellenen veya zaman aşımına uğrayan kaynaklar canlı doğrulanmış sayılmaz. Resmi sayfa sayısı, otomatik taranan sürücü sayısına eklenmez. WinGet/Scoop kurulumu gerekmez.

Özel piksel amblemi, paketlenmiş uygulama simgesi ve pencere başlığındaki simge kaldırıldı. FromZero adı ve bağlantılı piksel dünya korundu.

## 0.10.0 — sade ekranlar ve iki kat katalog

Crosshair açıklama banner'ları, ProSettings referans metni ve kaynak düğmeleri kaldırıldı; sekme adı “FACEIT ilk 10” oldu. Kaynak geçmişi verilerde ve belgelerde tutulur. Uygulama genelinde metin kutusu boşlukları, satır aralıkları ve dar penceredeki düğme hizaları düzeltildi. Güncelleme, oyun, performans, format kiti ve kurulum ekranlarındaki ek araçlar katlanır bölümlere taşındı.

Uygulama/sürüm kataloğu **600 kayıt**: 30 üretici kaydı ve 570 topluluk manifesti. Sayısal karşılaştırılamayan sekiz yeni aday alınmadı. Kaynak erişilemediğinde üretici satırı kaybolmaz; gömülü son kayıt “Kayıt kontrolü gerekli” olarak kalır ve otomatik önerilmez. Canlı doğrulanmış gibi sunulmaz.

Sürücü edinme araçları **3 → 6**: NVIDIA, AMD, Intel'e Dell Command Update 5.7.1, HP Image Assistant 5.3.7 ve Lenovo System Update 5.08.04.85 eklendi. Üçü de resmi üreticiden gerçek indirme ve Windows yayıncı imzası kontrolünden geçti; Dell/Lenovo için üretici SHA-256 değeri de denetlendi. Kurucular çalıştırılmadı. OEM destek sayfaları **8 → 16**; bu bağlantılar otomatik taranmış sürücü paketi değildir. Lenovo System Update bakım sonu 31 Ekim 2026, kullanım sonu 31 Mart 2027 olarak üretici tarafından duyurulmuştur; Commercial Vantage'a geçiş önerilir. Gerçek sürücü teklif sayısı donanıma ve Windows Update'e bağlıdır, katalog sayısına eklenmez.

## 0.9.1 — uygulama içi crosshair ve gerçek Dust2

Katalog, FACEIT ve kendi kodunu önizleme ana uygulama içindeki sekmelere taşındı. Büyütülmüş önizleme de aynı sayfada açılır. Dust2 piksel çizimi kaldırıldı; Valve'ın resmi CS2 sayfasındaki çift kapı görüntüsü orijinal JPEG olarak gömüldü. Kaynak ve telif bilgisi `Dust2-SOURCE.txt` içindedir.

27 Ekim 2026 tarihli global ilk 10 kaydındaki on FACEIT hesabı için crosshair.club'un öne çıkan güncel biçimde CS kodları kaydedildi. FACEIT adı ve oyuncu adı eşlemesi açıktır (ör. CEMEN_BAKIN → kyousuke). Kodun kaynakta görüldüğü tarih, kontrol tarihi ve kaynak bağlantısı kartta gösterilir; kodun kendisi çözülerek önizlenir. Her kart ayrı veya on kayıt birlikte yenilenebilir. Bu, doğrudan FACEIT API'sinden canlı crosshair okuma değildir. Gelecekte sıralamaya giren yeni bir hesap için açık kaynak eşlemesi yoksa başka oyuncunun kodu atanmaz.

## 0.9.0 — 50 oyuncu, global ELO ve 300 paket kaydı

Pro crosshair kataloğu 50 kaynak kaydına çıktı. Dust2'nin uygulama içinde çizilen piksel yorumu dördüncü ve varsayılan önizleme zeminidir. Kendi CS2 kodunu yapıştırıp aynı kod çözücüsüyle önizleyebilirsin. Eksik tablolardan değer tahmin edilmez.

FACEIT global ilk 10 görünümü EU, NA, SA, SEA ve OCE bölge ilk 10 kayıtlarını ELO ile birleştirir. Kaynak Faceit Tracker'dır; resmi FACEIT anlık sıralaması iddiası yoktur. Kontrol tarihi, kaynak kayıt tarihi ve kaynak bağlantıları gösterilir. Beş bölgenin biri yenilenemezse önceki bütün kayıt korunur. Sıralamadaki hesabın crosshair'i doğrulanamıyorsa kod üretilmez. Aynı adlı pro katalog kaydı ayrıca belirtilir; sıralama tarihi crosshair tarihi yerine geçmez.

Uygulama kataloğu 30'dan 300 ürün/sürüm hattına çıktı: mevcut 30 üretici kaydı ve 270 Scoop Extras topluluk manifesti. Scoop veya WinGet çalıştırılmaz, manifest betikleri yürütülmez. Yalnız tek dosyalı x64/ortak EXE ve MSI kayıtları kabul edilir. Yeni 270 kayıtta katalog SHA-256 değeri ve Windows tarafından güvenilen dijital imza birlikte zorunludur. Bunlar üretici adı ayrıca sabitlenmiş 30 kayıtla aynı güven sınıfında değildir. İmzasız paket reddedilir; katalogda görünmek tüm paketlerin indirme testinden geçtiği anlamına gelmez. EXE paketlerinin bir kısmı taşınabilirdir. Canlı kaynak başarısızlığı kayıt kartında gösterilir ve otomatik öneri oluşturmaz. Arama, kurulu olanlar filtresi ve 20 kayıtlık sayfalar geniş listeyi yönetir.

## 0.8.0 — pro crosshair kataloğu ve geniş kaynaklar

CS2 için 18 oyuncunun 27 Ekim 2026'da kontrol edilen ProSettings kayıtlı crosshair ayarları gömülüdür. Arama, favori, üç zemin, büyütme, kod kopyalama ve oyuncu kaydını internetten yenileme bulunur. Yeni CS2 v1 paylaşım kodları kaynak ayarlarından üretilir; önizleme üretilen kod çözülerek çizilir. Oyuncunun bizzat yayımladığı kod veya birebir oyun render'ı iddiası yoktur. Durağan şematik görünüm hareket/geri tepme simülasyonu yapmaz. Yeni codec, MIT lisanslı akiver/csgo-sharecode ikili biçim tanımlarına dayanır; lisansı dağıtılır. Eski CSGO kodları yenisi gibi gösterilmez, bilinmeyen şema veya bozuk kod reddedilir. Oyunun ayar dosyaları crosshair için değiştirilmez.

Uygulama kataloğu 30 ürün/sürüm hattını kapsar; Node.js LTS/Current ayrı sayılır. Brave, Thunderbird, Vivaldi, PowerShell, ShareX, Audacity, HandBrake, Rufus, WinMerge, KeePassXC, MPC-HC, RustDesk, LocalSend, ScreenToGif, DBeaver, HeidiSQL, Joplin ve Obsidian eklendi. Kategori, kaynak kontrol zamanı ve 15 dakikalık metadata önbelleği vardır. İndirme adresi/yayıncı/hash doğrulamaları korunur; bir kaynağın hatası diğer kayıtları silmez. Daha geniş katalog bütün Windows uygulamaları için eksiksiz kapsam iddiası değildir.

.NET SDK feature band'larının her birindeki en yeni kararlı yama ayrı taranır. Kurulu SDK, aynı bandın yamasıyla karşılaştırılır; başka banda otomatik geçirilmez. Eski SDK bantları elle seçilir. Sekiz OEM destek sayfası ve Microsoft Store güncelleme ekranı eklenmiştir; bunlar otomatik taranmış paket olarak sayılmaz.

## 0.7.0 — NVIDIA, ekran ve CS2 başlatma seçenekleri

Performans ekranı dört gerçek NVIDIA DRS ayarını NVAPI ile okuyup seçerek uygular: güç yönetimi, doku filtreleme kalitesi, tercih edilen yenileme hızı ve isteğe bağlı VSync kapatma. Varsayılan FPS reçetesi VSync'i değiştirmez. Genel profil kapsamı, program profillerinin önceliği ve kalite/güç etkisi açıkça belirtilir. Yazımdan sonra değerler doğrulanır; yedekleme, hata halinde geri alma ve daha sonraki kullanıcı değişikliklerini koruma sürer. Önceki 0.6 kurtarma kayıtları ek seçenekleri kapalı olarak okuyabilir.

Windows ekran envanteri aynı çözünürlükte desteklenen Hz değerlerini listeler. Ekran denemesi sürücü testinden sonra geçici uygulanır ve 15 saniye içinde onaylanmazsa bağımsız zamanlayıcıyla geri döner. Onaylanan seçim kalıcı kaydedilir; eski hız ayrıca geri alınabilir. Çözünürlük/yerleşim değişmez. Gecikmiş veya kesilmiş denemeler için yerel kurtarma durumu tutulur.

Oyunlar ekranında CS2 için kopyalanabilir Steam satırı üretilir: `+fps_max 0 +fps_max_ui 120` veya seçilen FPS sınırı. Panoya kopyalama, özel sayısal hedef ve Steam adımları var. Eski motor/öncelik/anti-cheat paketleri eklenmez. Oyunun FPS sınırını kaldırmak donanımı hızlandırmak anlamına gelmez; gerçek oyun içi FPS ölçülmedi.

## 0.6.0 — tek dünya, uygulama güncellemeleri ve oyun görüntü profilleri

Ayrı üst mahalle şeridi kaldırıldı; bölüm binaları ve karakterin yürüyüşü tek arka planda devam eder. Güncelleme kategorileri, canlı kuyruk sayacı, önerilenleri seçme, arama ve yalnız güncelleme gerektiren uygulama filtresi eklendi. Genel görünüm kaynak özetlerini gösterir; ayrıntılar kategoriyle açılır.

Kapsam artık Microsoft'un canlı kayıtlarında 8 .NET hattında Runtime/Desktop/ASP.NET/SDK için 64 paket; Windows/sürücü teklifleri; 10 uygulama için üretici sürüm kontrolü; DirectX çevrimdışı paket ve XNA 4.0 MSI dahil oyun bileşenleri içerir. Uygulama kaynakları: Mozilla, VS Code Update API, Google Chrome Version History, Microsoft Edge Enterprise Update API, VideoLAN ve yazılım üreticilerinin GitHub yayınları. Kararlı sürümler sayısal karşılaştırılır. Bütün Windows uygulamaları veya tarihsel sürücüler için evrensel kapsam iddiası yoktur.

Chrome, VLC, XNA ve .NET SDK gerçek indirme ve imza testlerinden geçti. 7-Zip'in resmi kurucusu imzasızdır; yalnız resmi ip7z/7zip yayın varlığının SHA-256 kaydı ile doğrulanan özel yol kullanılır ve arayüz bunu belirtir. Bu izin diğer paketlere uygulanmaz. VLC HTTPS yansısı VideoLAN'ın resmi yönlendirmesinde doğrulanmıştır; VideoLAN yayıncı imzası zorunludur. Birleşik indirme kanıtında 25 paket vardır.

CS2 ve Valorant için rekabetçi/dengeli görüntü profilleri, hesap/dosya algılama, önizleme, özgün dosyanın yedeği ve geri alma eklendi. Oyun kapalı olmalı; salt okunur/link dosya değiştirilmez. Yalnız tanınan mevcut görüntü anahtarları değişir. Çözünürlük, yenileme, çözünürlük ölçeği ve giriş tercihleri korunur. VSync kapatma ayrıca seçilir. Geri alma, sonradan değiştirilen ayarları korur. Gerçek CS2 dosyasında 9 değişikliklik plan salt okunur olarak doğrulandı; gerçek oyun ayarı uygulanmadı. Valorant dosyası bu bilgisayarda yok; INI uygulama/geri alma testleri geçici verilerde yapıldı. Oyunların ayarları fiilen kabul etmesi ve FPS etkisi canlı oyun içinde ölçülmedi.

Araştırma dayanakları: [Riot'un güncel FPS rehberi](https://support.riotgames.com/en-us/riot/performance/optimize-graphics-settings-for-better-fps-windows-and-mac), [Epic kalite grupları](https://dev.epicgames.com/documentation/en-us/unreal-engine/scalability-reference-for-unreal-engine), [NVIDIA CS2 rehberi](https://www.nvidia.com/en-us/geforce/news/counter-strike-2-released-featuring-nvidia-reflex/). Düşük kalite yükü azaltmaya yönelik bir tercihtir; donanım ve oyuna göre kazanım değişir. Reflex ve çok çekirdekli çizim gibi oyuna özel seçenekler desteklenen menüden doğrulanır; bilinmeyen dosya anahtarı eklenmez.

## 0.5.0 — mahalle, yürüme ve geniş güncelleme kataloğu

Kullanıcı arayüzü yalnızca FromZero markasını kullanır. Üçüncü taraf ad ve kaynak bağlantıları temizleme sayfasından, düğmelerden ve paket kullanım kılavuzundan çıkarılmıştır. Gerekli MIT telif/izin metni THIRD-PARTY-NOTICES.txt içinde korunur; geliştirme kaynaklarındaki lisans ve köken kayıtları saklanır.

On bölüm için ayrı piksel binalar aynı yola yerleştirildi. Temizlik şirketi, arcade salonu, güncelleme atölyesi, Wi-Fi istasyonu ve performans garajı kendi tabela/nesnelerine sahiptir. Karakter kamera takibi, yön dönüşü, kol/bacak adımları ve kapıya yaklaşma animasyonuyla seçilen binaya yürür. Yolculuk tamamlanınca zamanlayıcı durur. Aynı sayfanın yeniden çizilmesi tekrar yolculuk başlatmaz. Arka plan ve görünür mahalle şeridi aynı konuma bağlanır.

Kapsamlı güncelleme taraması Windows Update'in sunduğu Windows/yazılım ve sürücü tekliflerini birlikte toplar. İsteğe bağlı teklifler, kimlik, revizyon, tarih, sürüm, lisans ve indirme boyutu gösterilir. Gizli ve superseded teklifler ayrıca taranabilir; indirmeye seçilmez. Sistem/kurum ilkeleri değiştirilmez. Sonuçlar arama ve sayfalama içerir. Bir kaynağın başarısızlığı diğer kaynakların sonuçlarını silmez.

Visual C++ 2005/2008/2010/2012/2013/v14 x86-x64 için toplam 12 kurucu, DirectX ve üç GPU üretici aracı mevcut. .NET Runtime, Windows Desktop Runtime ve ASP.NET Core Runtime için Microsoft'un canlı yayın indeksindeki 3.0 ve sonraki kararlı sürüm hatları taranır. Her hattın son yaması, ilgili mimariler ve sistemde bulunan bütün kararlı yama sürümleri karşılaştırılır. Canlı testte 8 hat / 48 paket bulundu; destek dışı hatlar otomatik indirmeye katılmaz.

İsteğe bağlı otomatik indirme, kapsamlı tarama sonunda kurulu desteklenen .NET hatlarının yeni yamalarını ve lisansı daha önce kabul edilmiş uygun sürücü tekliflerini indirir. Seçilen kurucular imza ve bütünlük doğrulamasıyla FromZero Downloads'a; Windows paketleri WUA ile Windows Update önbelleğine kaydedilir. .NET dosyaları ayrıca yayın indeksindeki SHA-512 ile doğrulanır. Kurulum ayrı seçilir, BIOS/firmware otomatik indirilmez/kurulmaz. Windows yazılım güncellemelerinin kurulumu Windows Ayarları'na bırakılır. Tarama kullanıcı başlatmalıdır; zamanlanmış tarama yoktur.

Doğrulama: 115 temel kontrol ve 11 canlı kaynak/indirme kontrolü; canlı Windows Update/.NET kaynak taraması ve 9 ek gerçek imzalı indirme başarılı. 20 kurucuya ilişkin birleşik indirme kanıtı artifacts/download-verification.json içindedir. Testlerde Windows paketleri indirilmedi veya kurulmadı; gerçek sürücü/güç/ağ ayarları değiştirilmedi. Her üreticinin bütün tarihsel sürücü sürümlerine eksiksiz erişim garantisi verilmez.

## 0.4.0 — ReClaim temizleyici ve uyarlanabilir yerleşim

[İstenen ReClaim Cleaner deposunun](https://github.com/Gercekkriptocu/reclaim-cleaner) `c154b018c715c38b1362da0ad8c3f016cf2c6b5d` sürümündeki **233 hedef kuralı değiştirilmeden** uygulamaya gömülmüştür. Rust `rules.rs`, `paths.rs`, `junk.rs` ve `clean.rs` kodlarının kural doğrulama, joker eşleme, yol koruma, tarama ve gözden geçirilmiş temizlik mantığı C#'a uyarlanmıştır. Kaynak ve MIT lisansı uygulama paketinde bulunur; özgün Rust/Tauri uygulaması çalıştırılmaz.

Sol menüdeki **Temizlik** bölümünden tarama yapılır. Yeniden oluşabilen önbellek/günlük hedefleri uygun olduğunda seçilir; inceleme isteyen hedefler ayrıca açılır, dikkat düzeyindekiler varsayılan olarak gizlidir. Arama, sayfalama, bütün dosyaları inceleme ve tam dosya listesini JSON olarak kaydetme desteklenir. Bir hedefin açık uygulaması varsa seçim engellenir. Temizlik öncesinde seçilen hedefler ve kalıcı silme yöntemi gösterilir. Tarama ve temizlik durdurulabilir; tamamlanan silmeler geri alınamaz.

Temizleme yalnızca taramada kaydedilmiş, sonradan değişmemiş dosyaları siler. Dosyanın son yolu, oluşturulma/değiştirilme zamanı, boyutu, bağlantı sayısı ve özellikleri açık dosya tutamacıyla yeniden doğrulanır. Junction/symlink, bulut yer tutucusu, çoklu sabit bağlantı, kilitli ve salt okunur dosyalar atlanır. Otomatik kurtarma dosyaları için ReClaim'in istisna kalıpları korunur. Şahsi klasör kökleri, kritik Windows dizinleri ve FromZero verileri korunur. Yeni dosyalar tarama sonrasında topluca silinmez. Klasör yapıları yerinde bırakılır.

233 kuralın 19'u rapor/üretici aracı içindir; doğrudan dosya silme planı oluşturmaz. Kural verisindeki kabuk komutları otomatik çalıştırılmaz. Bu alanlar Windows Depolama araçlarından yönetilir. Yönetici isteyen dosya hedefleri için uygulamayı kullanıcı yönetici olarak açıp yeniden taramalıdır. Bu entegrasyon ReClaim'in disk haritası, kopya dosya, proje çıktısı keşfi ve AI model yönetiminin tamamını içermez. Tarama en fazla hedef başına 20.000, toplam 100.000 dosya planlar; sınıra veya erişim hatasına takılan hedefler kısmi olarak işaretlenir. Gösterilen boyutlar mantıksal dosya baytlarıdır, diskin fiziksel boş alan farkından ayrıdır.

Yerleşim: tanıtım kartının yüksekliği içeriğe göre büyür; özet kartları dar pencerede dört sütundan iki/bir sütuna geçer. Uzun düğme ve seçim etiketleri satıra geçer. Sol menü kısa pencerede kaydırılır. Alt durum satırı kendi sütunundadır. Pencereler ekranın çalışma alanını aşmayacak şekilde sınırlandırılır; asgari ana pencere 860×560'tır (daha küçük ekranda çalışma alanına uyarlanır).

Entegrasyon ayrıntıları: `src/FromZero/ThirdParty/Reclaim/UPSTREAM.md`. Test silmeleri yalnızca geçici test klasörlerinde çalıştırılır; bilgisayardaki gerçek önbellekler otomatik temizlenmez.

## 0.3.0 — sade arayüz, gerçek onarım ve performans profili

Sol menüde altı ana bölüm var; simgeler ve metinler sola hizalı. Diğer araçlar, cihaz listeleri ve teknik açıklamalar kapalı ayrıntı bölümlerinde. Ortak piksel dünya daha düşük kontrastla korunur.

**Wi-Fi ve ağ:** Kontrol, önerilen onarım ve aktarım optimizasyonu ayrı işlemlerdir. Teşhis tek başına ayar değiştirmez. Önerilen onarım; durmuş WLAN hizmetini başlatır, kapalı fiziksel adaptörü etkinleştirir, DHCP kaynaklı APIPA/boş adres durumunda yalnızca ilgili adaptörün IP kiralamasını yeniler ve DNS çözümlemesi başarısızsa önbelleği temizler. Manuel adaptör yeniden başlatma ve Winsock sıfırlama da vardır. Statik IP otomatik olarak DHCP'ye çevrilmez. VPN sanal adaptörleri fiziksel onarım listesinden çıkarılır. Bağlantıyı kesebilen işlemler uygulama içinde önceden gösterilir ve kullanıcı seçimiyle çalışır.

**Aktarım optimizasyonu:** Windows TCP alım penceresi otomatik ayarı okunur; kısıtlı durumdaysa `Normal` seçilebilir. Zaten Normal ise değişiklik yapılmaz. Önceki seviye kaydedilir ve geri alınabilir. Bu, bütün TCP bağlantılarına etki eden bir Windows ayarıdır; kablosuz sinyali güçlendirmez ve internet paketinin hız sınırını artırmaz.

**Performans:** Tek profil, mevcutsa Yüksek Performans güç planını seçer, istemci alanı animasyonlarını kapatır ve desteklenen NVIDIA sürücüsünde genel 3D güç yönetimini “maksimum performansı tercih et” değerine getirir. İsteğe bağlı doku filtreleme “yüksek performans” ayarı görsel kaliteyi düşürebilir. Profil kalıcıdır; “Önceki ayarlara dön” ile geri alınır. Kısmi hata halinde geri alma denenir ve tamamlanamazsa kurtarma kaydı korunur. Oyun oturumu açıkken profil değiştirilemez. Yetki gerektiren NVIDIA sürücülerinde uygulama içinden yönetici olarak tekrar açılabilir.

NVIDIA ayarları, resmi NVAPI DRS arayüzüyle okunup yazılır; sürücü dosyası yamalanmaz. Değişiklikler genel profiledir, oyuna özel profiller bunları geçersiz kılabilir. NVIDIA dışındaki GPU'larda üretici ayarı taklit edilmez; Windows profili ve mevcut AMD/Intel araçları kullanılabilir. Değişikliklerin FPS etkisi aynı oyun/sahneyle ölçülmelidir. Gerçek NVAPI okuması ve sahte platformda uygulama/geri alma testleri yapılmıştır; geliştirme sırasında kullanıcının gerçek NVIDIA ayarları değiştirilmemiştir.

**Oyun bileşenleri:** Direct3D 9/11/12 sistem dosyaları, gerçek donanım özellik seviyesi, D3DX9/10/11, XInput 1.3, XAudio 2.7, D3DCompiler 43, XACT, Visual C++ 2005/2008/2010/2012/2013/v14 x86-x64, .NET Framework 3.5/4.8.1, OpenAL, Vulkan, PhysX ve XNA kontrol edilir. Toplam 40 bileşen/mimari durumu listelenir. Bir dosyanın eksik olması her oyun için hata anlamına gelmez; oyunun istediği bileşen kurulmalıdır. Windows'un DirectX çekirdeği Windows Update ile güncellenir. DirectX ek paketi eski oyun kütüphanelerini tamamlar; Windows DirectX sürümünü yükseltmez.

DirectX, VC++ v14 x64/x86 ve VC++ 2013 x64/x86 için beş ek gerçek indirme/üretici imzası testi başarılıdır. Eski bileşenler ve PhysX/OpenAL/XNA için resmi kaynaklar sunulur. İndirme testleri kurucuları çalıştırmaz. Driver Booster'ın özel sürücü veritabanının tamamıyla eşdeğerlik doğrulanmış değildir; donanım güncellemeleri Windows Update, Microsoft Catalog, üretici araçları ve yerel imzalı paketler üzerinden yürür.

Kaynaklar: [NVIDIA NVAPI DRS](https://developer.nvidia.com/nvapi/get-started), [NVIDIA ayar tanımları](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h), [Microsoft TCP ayarları](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-interface), [DirectX paketinin kapsamı](https://www.microsoft.com/en-us/download/details.aspx?id=35), [Visual C++ resmi indirmeleri](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist).

## 0.2.0 güncellemesi

- Bütün sayfalar aynı piksel dünyasını paylaşır. Ortak gökyüzü, dağlar, ağaçlar ve yol devam eder; modül değişince kamera haritanın ilgili istasyonuna kayar. Paneller yarı saydamdır. Uygulamanın kendi ek pencerelerinde de aynı dünya kullanılır.
- Oyun istasyonunda ve sürücü merkezinde NVIDIA App, AMD Software: Adrenalin Edition ve Intel DSA araçları bulunur. GPU donanımı PCI üretici kimliğinden tespit edilir; sürücü ve yardımcı uygulama ayrı değerlendirilir.
- NVIDIA/AMD için güncel kurucu bağlantısı resmi sayfadan çözülür. Intel DSA resmi sabit indirme adresinden alınır. İndirmeler WinGet kullanmaz.
- Her HTTPS yönlendirmesi izinli üretici alan adlarına karşı kontrol edilir. İndirilen dosyanın PE başlığı, boyutu, Windows güven zinciri, imza yayıncısı ve SHA-256 özeti denetlenir. Kurulumdan önce tekrar kontrol ve sürücü yedeği yapılır. İndirme/kurucu açılışı başarılı kurulum olarak gösterilmez.
- İmza kontrolü PowerShell modülüne ihtiyaç duymadan Windows `WinVerifyTrust` üzerinden yapılır. Yayıncı bilgisi doğrulanan imzanın sertifika zincirinden okunur.
- Kurulu uygulamalar kullanıcı/sistem 32 ve 64 bit kayıtları ile Store/MSIX envanterinden taranır. Kurulum çantasında aranabilir. GPU aracının kayıt bilgisi ve fiziksel EXE varlığı ayrı gösterilir; dosya imzası ayrıca doğrulanabilir.
- Etkin sürücü kaydı GPU cihaz kimliğiyle eşleştirilir; Windows INF dosyasının varlığı ve Windows envanterinin imza durumu gösterilir. Bu, yeni sürümün kurulu olduğu veya bütün donanımlarda uyum garantisi anlamına gelmez.
- Canlı Windows Update Agent taraması da doğrulandı; test bilgisayarında NVIDIA görüntü sürücüsü ve HP firmware paketi sunuldu. Firmware/BIOS paketleri üretici akışına yönlendirilir ve FromZero tarafından kurulmaz. Tarama hiçbir güncelleme yüklemedi.
- Önceki ön izlemenin profil, ölçüm, geçmiş ve yarım oyun oturumu kayıtları yeni veri klasörüne üzerine yazmadan kopyalanır. Eski yedekler silinmez. Başlangıçtan daha önce kaldırılan girdiler geri açılabilir.

26 Ekim 2026 tarihinde altı gerçek kurucu indirildi ve doğrulandı: **Firefox, VS Code, Steam, NVIDIA App, AMD Auto-Detect ve Intel DSA**. Dosya boyutu, kaynak URL, yayıncı, SHA-256 ve kontrol tarihi `artifacts/download-verification.json` içindedir. Bu test kurucuları çalıştırmaz ve sürücü kurmaz. Üretici paketleri değişebileceğinden uygulama her indirmede doğrulamayı yeniden yapar.

## Çalıştırma

`dist/FromZero-win-x64/FromZero.exe` dosyasını açın. **Klasörün tamamını birlikte taşıyın**; yalnızca EXE'yi kopyalamayın. Taşınabilir dağıtım .NET masaüstü çalışma zamanını içerir, ayrıca .NET indirmeniz gerekmez. Windows 11 x64 hedeflenmiştir. ARM64 paketi bu teslimata dahil değildir.

Normal kullanıcı olarak açılır. Sürücü dışa aktarma/kurulum, DNS önbelleği ve sistem dosyası doğrulama gibi gerekli işlemlerde Windows'un yönetici izni penceresi görünür. EXE'nin sağ tık menüsünden yönetici olarak açılması da mümkündür. Yönetici olarak açılan uygulamadan başlatılan alt işlemler bu yetkiyi devralabilir; günlük kullanım için normal açılış önerilir.

## Uygulanan özellikler

| Modül | Gerçek işlev |
| --- | --- |
| Genel bakış | Gerçek CPU/bellek ölçümü, sistem diski boş alanı, adaptör durumu, donanım tarama |
| Sürücüler | CIM/PnP envanteri, donanım kimlikleri, yerel INF eşleştirme, katalog imzası kontrolü, PnPUtil yedekleme/kurulum |
| Çevrimiçi sürücüler | Windows Update Agent COM API ile yalnızca sürücü güncellemelerini arama; seçilen kimlik ve revizyonu indirme/kurma |
| Üretici paketleri | Kullanıcının seçtiği imzalı EXE'yi yayıncı bilgisiyle inceleme; önce sürücü yedeği alma, sonra resmi kurucuyu açma |
| Format hazırlığı | Çalışan üçüncü taraf sürücüleri, uygulama listesi, donanım pasaportu, başlangıç referansı ve uygulama dosyalarından kapsül |
| Kapsül doğrulama | SHA-256/boyut denetimi, eksik/değişmiş dosyaları raporlama, paket dışı yol ve bağlantı noktalarını reddetme |
| İnternetsiz kurtarma | Kapsüldeki donanımla eşleşen INF'leri bulma, seçilen paketi kontrol ederek Windows'a verme |
| Telefon desteği | Yerelde QR oluşturma; telefonla Microsoft Catalog'da donanım kimliği arama; çevrimdışı HTML yardım kartı |
| Bağlantı | Yerel adaptör/IP/DNS bilgileri; ağ geçidi/DNS/TCP testleri, WLAN raporu, onayla DNS önbelleği temizleme |
| Performans | Bellek tüketen işlemler, oturum grafiği, ölçüm kaydetme/karşılaştırma, eski TEMP dosyaları için silme ön izlemesi |
| Başlangıç | HKCU Run girdilerini yedek anahtara taşıyarak kapatma ve çakışma kontrolüyle geri açma |
| Kurulum çantası | Kurulu masaüstü uygulamalarını listeleme/dışa aktarma; imzalı yerel kurucuyu açma; resmi indirme sayfaları |
| Doğrudan indirme | Firefox, VS Code, Steam, NVIDIA App, AMD Auto-Detect ve Intel DSA için resmi HTTPS kaynaklarından gerçek indirme, beklenen yayıncı/imza kontrolü, SHA-256 bilgisi, kullanıcı seçimiyle kurucu açma |
| GPU araçları | Gerçek PCI donanımı, etkin sürücü/INF, kurulum kaydı, EXE dosyası ve sürüm tespiti; imza denetimi, üretici arayüzünü açma ve resmi kurucu indirme |
| Oyun istasyonu | Steam kitaplıklarını keşfetme, yerel/Steam oyun profilleri, oyuna özel argümanlar, işlem adıyla oturum takibi |
| Oyun modu | Var olan Yüksek Performans planını seçme, isteğe bağlı mevcut Game Mode/Game DVR DWORD tercihlerini oturumluk değiştirme |
| Oturum kurtarma | Değişiklikten önce kalıcı kayıt, oyun çıkışında/uygulama kapanışında geri alma, sonraki açılışta yarım oturumu kurtarma, dışarıdan değişmiş değeri koruma |
| Arka plan uygulamaları | Kullanıcının seçtiği aynı oturumdaki pencerelere normal kapatma isteği; zorla sonlandırma yok |
| FPS laboratuvarı | PresentMon CSV, süreç/swapchain ayrımı, ortalama FPS, en yavaş %1 ortalamasından düşük FPS, P99 kare süresi ve iki kayıt karşılaştırması |
| Geçmiş | Yerel işlem günlüğü, ayrıntılar, JSON dışa aktarma, sürücü yedeklerine erişim |

**Sürücü motorunda veya uygulama kurulumunda WinGet bağımlılığı bulunmaz.**

## Oyun modu nasıl kullanılır?

1. Oyun istasyonunda Steam oyunlarını bulun veya EXE seçerek profil ekleyin.
2. Profilde, otomatik bitiş için gerçek oyun işlem adını `.exe` olmadan girin. Örneğin CS2 için `cs2`. Steam başlatıcısının adı oyunun adı yerine kullanılmamalıdır.
3. Oyunun kendi desteklediği başlatma seçeneklerini yazın. Steam'in kalıcı ayar dosyası değiştirilmez; argümanlar bu başlatmaya iletilir. Mevcut Steam seçenekleriyle çakışmaları kontrol edin.
4. İstediğiniz oturum ayarlarını seçip **Hazırla ve başlat** düğmesine basın. Değişiklik planını inceleyin.
5. İzlenen oyun görüldükten sonra kapanırsa oturum ayarları geri alınır. Oyun 5 dakika içinde hiç görülmezse geri alma denenir. İşlem adı boşsa **Oturumu bitir** düğmesini kullanın.
6. Game Mode ve Game DVR kayıt seçenekleri **deneyseldir**. Yalnızca önceden var olan, DWORD tipindeki tercihler desteklenir. İşletim sistemi bu değerleri her sürümde aynı şekilde uygulamayabilir; Windows Ayarları üzerinden doğrulayın. Geri alma, eski değeri tam olarak korur; oturum dışında değiştirilmiş değerlerin üzerine yazmaz.

Yüksek Performans planı yalnızca cihaz sunuyorsa seçilebilir. OEM güç yönetimi, termal sınırlar ve bataryalı kullanım sonucu etkiler. Overclock, güvenlik özelliklerini kapatma, anti-cheat dosyalarını değiştirme veya rastgele kayıt temizliği uygulanmaz. FPS artışı garanti edilmez.

FPS analizi için PresentMon'un `MsBetweenPresents` veya `FrameTime` sütununu içeren CSV kullanılır. Her akış için en az 100 geçerli kare gerekir. Ortalama FPS `1000 / ortalama(ms)`; %1 düşük FPS, en yavaş `ceil(n*0.01)` karenin ortalamasının tersinden hesaplanır. P99 en yakın sıra yöntemiyle hesaplanır. Bu metrik sunum temposunu temsil eder; gerçek ekrana gösterim FPS'si, frame generation ve gecikme metrikleri ayrıca değerlendirilmelidir. Kayıt aracı/overlay uygulamaya gömülü değildir.

## Sürücü kurulumu ve geri alma

- Yerel kurulum öncesinde güncel donanım tekrar okunur. INF içindeki tam donanım kimliği eşleşmesi ve uygun mimari katalog dosyasının geçerli imzası aranır. Geniş tahmin eşleşmeleri kullanılmaz. Bazı geçerli, daha karmaşık paketler bu ilk eşleştirici tarafından reddedilebilir.
- Katalog imzasının geçerliliği tek başına bütün dosyaların katalog üyeliğini doğrulamaz. Nihai paket içeriği/imza/işletim sistemi uyumluluğu Windows sürücü yükleyicisinin denetimine bırakılır. İmza denetimi veya sürücü sıralaması atlatılmaz.
- Paket depoya eklenebilir fakat daha iyi sıralanan sürücü nedeniyle etkin sürücü değişmeyebilir. Kurulum çıktısını okuyun ve envanteri yeniden tarayın.
- Üreticinin EXE paketi için INF analizi yapılmaz; uyumluluğu resmi kurucu denetler. EXE açılması, başarılı kurulum olarak raporlanmaz.
- İşlem öncesi üçüncü taraf sürücüler `%LOCALAPPDATA%\FromZero\Backups` içine aktarılır. Ağ ve görüntü kısa süre kesilebilir; yeniden başlatma gerekebilir.
- Windows Update taraması internet ve kullanılabilir Windows Update hizmeti gerektirir; sunucunun önerdiği paketler dışındaki evrensel bir sürücü veritabanı değildir.
- Aygıt Yöneticisi'ndeki **Sürücüyü Geri Al** kullanılabilir. Eski sürücüyü zorla düşürme otomatikleştirilmez. Windows açılmayan durumlar için bu uygulama bir WinPE kurtarma ortamı değildir.

## Kapsül ve gizlilik

Kapsülü formatlanmayacak USB/başka diskte saklayın. Donanım ve uygulama listesi yerelde tutulur; otomatik telemetri veya bulut yüklemesi yoktur. QR'ı telefonda açmak seçili donanım kimliğini Microsoft'a arama sorgusu olarak gönderir. İnternet teşhisi Microsoft'a DNS/TCP isteği gönderir. İndirme ve Windows Update işlemleri ilgili resmi servislerle bağlantı kurar.

SHA-256 manifesti dosya bozulmasını denetler, **paketin kaynağını imzalamaz**. Kaynağı güvenilir olmayan bir kapsülü çalıştırmayın. Kişisel dosyalar, tarayıcı profilleri, uygulama ayarları, lisans anahtarları ve parolalar yedeklenmez. Uygulama listesi kayıt amaçlıdır; tüm uygulamaları otomatik yeniden kurmaz. Genel çevrimdışı adaptör deposu dağıtılmaz; kişisel yedek veya getirilen resmi paket gerekir.

## Derleme ve doğrulama

Gereksinimler: Windows, .NET 10 SDK. İlk paket geri yüklemesinde QRCoder için NuGet erişimi gerekir. Kaynaklar `src/FromZero`; bağımsız kontrol programı `tests/FromZero.Checks` altındadır.

```powershell
dotnet build FromZero.slnx -c Release
dotnet run --project tests/FromZero.Checks -c Release
dotnet run --project tests/FromZero.Checks -c Release -- --verify-downloads
dotnet run --project tests/FromZero.Checks -c Release -- --verify-updates
./scripts/publish.ps1
```

Kontroller geçici test dosyalarında ve sahte güç/kayıt platformunda çalışır. Canlı kontroller yalnızca cihaz/sürücü envanteri, güç planı okuma ve bellek ölçümüdür. Gerçek sürücü kurulumu, sürücü düşürme, kullanıcı TEMP temizliği ve güç/kayıt değişiklikleri geliştirme bilgisayarında otomatik test edilmez. Yayın öncesi ayrı test cihazlarında aşağıdaki matris tamamlanmalıdır.

## Üretime geçişte kalanlar

Bu teslimat çalışan bir ön izleme sürümüdür. İmzalı ticari dağıtım için uygulama kod imzası/kurucu, OEM paket dağıtım izinleri ve cihaz testleri gerekir. Kesintisiz kurulumun yeniden başlatma sonrası otomatik devamı, imzalı çevrimiçi OEM kataloğu, cihaz bazlı sürücü başarı verisi, otomatik uygulama ayarı geri yükleme, teknisyen çoklu cihaz paneli ve oyun içi otomatik FPS yakalama bu sürümde bulunmaz.

Test matrisi: Intel/AMD/NVIDIA GPU; farklı Wi-Fi/Ethernet üreticileri; OEM dizüstü; Modern Standby/klasik güç planı; yönetici olmayan kullanıcı; UAC iptali; internetsiz temiz Windows; sürücü sonrası yeniden başlatma; disk dolması; bozuk USB; oyun/başlatıcı ayrımı; uygulamanın zorla kapanması; kayıt geri alma başarısızlığı.

FromZero adı kullanıcının tercihiyle seçildi. Bu ad için marka/alan adı uygunluğu veya tescil araştırması yapılmadı.

## Teknik kaynaklar ve üçüncü taraf bileşenler

- [Microsoft PnPUtil](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax)
- [Windows Update Agent API](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/using-the-windows-update-agent-api)
- [Powercfg](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options)
- [Windows pencere oyun optimizasyonları](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11)
- [Steam oyun başlatma seçenekleri](https://help.steampowered.com/en/faqs/view/7D01-D2DD-D75E-2955)
- [PresentMon](https://github.com/GameTechDev/PresentMon) — uygulamayla birlikte dağıtılmaz.
- [QRCoder 1.8.0](https://github.com/codebude/QRCoder) — MIT, QR üretimi.
- [Pixelify Sans](https://github.com/google/fonts/tree/main/ofl/pixelifysans) — SIL Open Font License. Font uygulamada gömülüdür, lisans `src/FromZero/Assets/Fonts/OFL.txt` içinde bulunur.

Piksel manzara ve amblem bu proje için kodla çizildi; referans görseldeki posterler ve karakterler kullanılmadı.
