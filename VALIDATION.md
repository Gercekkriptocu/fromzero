# FromZero 0.13.2 doğrulama kaydı
## 0.11.0 — üretici dizini ve logosuz görünüm

- Son temel çalışma **194 kontrol**, canlı kurucu çalışması **199 kontrol** geçti (`artifacts/checks-0.11-core.log`, `artifacts/checks-0.11-live.log`). Yedi yeni dizin/URL/indirme eşlemesi/donanım filtre kontrolü ve beş gerçek kurucu + tümünün geçmesi koşulu eklendi. Gerçek PC/GPU/ağ/oyun ayarları değiştirilmedi ve kurucu çalıştırılmadı.
- Dizin **164 resmi destek sayfası**, **55 üretici aracı / kurulum merkezi**, **9 donanım kategorisi** içerir. Araçlar ve destek sayfaları ayrı, uygulama içi sekmelerdedir; arama, kategori, mevcut cihaz ve sistem/anakart üreticisi eşleşmesi, 12 kartlık sayfalama vardır. Marka eşleşmesi sürücü uyumluluğu değildir; mevcut olmayan cihazlar eşleşmeye katılmaz. Store/MSIX/ZIP ve model seçimi gereken paketler üretici sayfasında açılır.
- İmzalı üretici aracı indirme tarifleri **6 → 11**: Logi Options+, Logitech G HUB, Corsair iCUE, SteelSeries GG, Samsung Magician eklendi. Gerçek indirme, Windows güven zinciri/yayıncı, SHA-256 kaydı ve önbelleği tekrar doğrulama geçti (`artifacts/expanded-oem-tool-verification.json`). Yeni kurucular çalıştırılmadı. SteelSeries GG'nin beklenen yayıncısı, üreticinin https://steelseries.com/policies künyesindeki **GN Hearing A/S / CVR 55082715** ile doğrulandı; ilk farklı-yayıncı denemesi reddedildi, imza koşulu gevşetilmedi. Toplam **55 başarılı indirme kaydı** bulunmaktadır.
- İlk HTTP kontrolünde bulunan taşınmış / 404 sayfalar resmi güncel yollarla değiştirildi. Son dizin kontrolü **197 benzersiz URL: 164 HTTP 200, 31 HTTP 403, 1 HTTP 202 ve 1 zaman aşımı**. 403/202 koruma veya bölge kısıtı ve Acer zaman aşımı canlı sayfa doğrulaması olarak sayılmaz. Son listede HTTP 404 yoktur. Bu çalışma tüm dosyaların veya tüm modellerin sürücü uyumluluğunu doğrulamaz. Ham rapor `artifacts/manufacturer-page-verification.json`; tekrar aracı `scripts/verify-manufacturer-pages.ps1`.
- Özel PixelEmblem kaldırıldı; ICO proje kaynaklarından ve uygulama simgesinden çıkarıldı. Yerel pencere çerçevesindeki simge gizlendi, küçült/büyüt/kapat işlevleri korundu. Önizleme penceresinde başlık ve üst solda özel amblem bulunmadığı gözlendi; FromZero adı ve piksel arka planı korunur.

- Paketlenmiş son pencerede arama (Logi → iki araç), indirme kuyruğuna ekleme/çıkarma, Araçlar/Destek sayfaları geçişi, algılanan marka filtresi ve 1/14 ↔ 2/14 sayfalama gözlendi. Framework için Windows bileşeni kaynaklı yanlış eşleşme kaldırıldı; gerçek cihazdaki marka sonucu 9'dan 8'e düştü. Ayrıca Zoom sanal ses aygıtı ile Zoom kayıt cihazı karışması için veri/test düzeltildi. Geniş pencerede görülen kartlar ve düğmeler hizalıdır; yeni dizinin dar pencere görünümü ayrıca doğrulanmadı. PC ayarları, sürücüler veya oyun dosyaları değiştirilmedi.
- Son dağıtım **0.11.0.0 / 410 giriş / 68.188.366 bayt**. ZIP'in bütün girişleri okundu; SHA-256 yan dosyayla eşleşti. 55 indirme kanıtı, üretici sayfa kontrol raporu ve 0.11.0 kullanım kılavuzu pakette. Paketlenmiş uygulama üretici desteği sayfasında açık bırakıldı (`artifacts/package-0.11-verification.json`, `artifacts/publish-0.11.log`).
## 0.10.0 doğrulama geçmişi
## 0.12.0 — format sonrası hazırlama motoru

- **225 temel kontrol**, canlı paket/sürüm çalışmasında **233 kontrol** geçti (`artifacts/checks-0.12-core.log`, `artifacts/checks-0.12-live.log`). 194 önceki temel kontrole 31 hazırlama kontrolü eklendi. Canlı çalışmadaki ek 8 kontrol: 3 gerçek indirme, üçünün geçmesi, 3 üretici hedef sürümü ve Microsoft .NET Desktop 10 metadata/SHA-512 kontrolü.
- Kuyruk testleri: seçeneklerden sabit sıralı plan, kullanıcı kimliği ve değiştirilen/boş adım reddi, yalnız bilinen uygulamalar, kalıcı atomik kayıt, başarısız uygulama sonrası bağımsız devam, yedek başarısızlığında sürücü yazımının engellenmesi, adım sınırında durdurma, aynı boot'ta yeniden başlatma bekleme, sonraki boot'ta devam, tamamlanmış uygulamaların tekrarlanmaması, son kontrolün tekrar çalışması ve dört reboot sınırı. Bunlar sahte kurulum arka ucuyla yapılır; gerçek kurucu çalıştırılmaz.
- Windows kurulum PowerShell metinleri, yerel Windows ayrıştırıcısına **çalıştırılmadan** verildi; iki tür için sözdizimi hatası yok. Windows API'sinde sürücü/yazılım ayrı, ağ-yonga-grafik sırası, firmware/BIOS/preview/feature-upgrade filtresi, paket kimliğiyle kurulum sonrası doğrulama, her aşamada üç tarama turu vardır. Kurucu/Windows Update işlemleri iptalde öldürülmez. Güvenli durdurma ve sonuç kayıtları kullanılır.
- Yeni Chrome sistem MSI ve VS Code sistem EXE tarifleri, VLC hazırlama tarifi gerçek indirme / Windows imza-yayıncı / hash / önbelleği tekrar doğrulama kontrolünden geçti (`artifacts/preparation-package-verification.json`). VLC'nin rastgele ayna yönlendirmesi ilk denemede reddedildi; üretici sürüm kaydı + sabit izinli Verinomi aynası + VideoLAN imzası kullanıldı. Hedef sürümler canlı okundu. Önceden doğrulanmış aynı VLC kayıt adı birleştirildiği için dağıtım kanıtı **57 başarılı adlandırılmış indirme kaydı** içerir; 57 ayrı uygulama iddiası değildir.
- Dell için resmi .NET Desktop 10 ön koşulu, kurucu ve yalnız sürücü CLI akışı; HP için iş modeli filtresi / imzalı HPIA çıkartma / Category:Drivers akışı eklendi. Bunlar bu geliştirme bilgisayarında **çalıştırılmadı**. NVIDIA/AMD/Intel yardımcı kurucuları etkileşimlidir; tahmin edilmiş sessiz seçenek kullanılmaz. Sürücü güncelliği yardımcı uygulamanın varlığıyla eşitlenmez.
- USB dışa aktarımı mevcut kurtarma kapsülünü seçili imzalı kurucular, profil, hata raporu ve yenilenen dosya hash listesiyle genişletir. Oyun profili tam DirectX redist içerir; korumalı klasörde Microsoft imzalı DXSETUP /silent kullanılır. USB'den profil içe alınması otomatik kurulum izni vermez. Dosya yolu kaçışı, bilinmeyen paket, null profil ve sahte adım türü reddedilir.
- Gerçek geniş WPF penceresinde yeni ana ekran, temel/oyun profili, seçeneklerin açılması ve yalnız donanım planı incelendi. Plan, HP Victus / 185 mevcut cihaz / 0 cihaz sorunu / 0 seçili uygulama eksik sonucu döndürdü. Planın onayı boş ve başlatılmış kurulum sayısı sıfırdır (`artifacts/preparation-ui-plan-verification.json`, kimlik bilgileri bu kanıta dahil edilmedi). PC ayarları, sürücüler, Windows güncellemeleri ve uygulama kurulumları değiştirilmedi.
- **Kabul testi sınırı:** gerçek yönetici kurulumları, OEM komutları ve gerçek reboot/logon döngüsü temiz Windows 10/11 x64 test cihazında ayrıca denenmelidir. Bu kayıt tam donanım uyumluluğu veya 600 uygulamanın otomatik kurulumu iddiası içermez. Hazırlama akışı beş uygulamayı destekler; Windows/OEM paketleri kaynakların model/OS kapsamına bağlıdır. İlk derlemede açık test penceresi dosya kopyasını kilitledi; pencere kapatılarak son derleme 0 hata / 0 uyarıyla tamamlandı.

- Son dar pencere kontrolü `--compact` ile 900 mantıksal pikselde yapıldı: ana ekrandaki kartlar iki sütuna geçti; hazırlama sayfasında üçüncü ana eylem alt satıra taşındı; seçenekler ve durum metni gözlenen alanda yatay taşmadı. Hazırlama profilleri/planı dışında kurulum başlatan düğmeye basılmadı. Visual C++ 2005, yeniden başlatmayı bastıran MSI yoluna ayrıldı; imzalı Microsoft arşivi korumalı klasöre çıkartılır ve sabit vcredist.msi sessiz kurulumu kullanılır. Bu kurulum yolu geliştirme bilgisayarında çalıştırılmadı. Son kaynak tekrarında temel 225 kontrol yine geçti.
- Son dağıtım **0.12.0.0 / 411 giriş / 68.236.961 bayt**. ZIP'in bütün girişleri okundu, SHA-256 yan dosyayla eşleşti. 57 indirme kaydı ve otomatik kurulum kapsamı/kabul testi sınırını açıklayan kılavuz pakette (`artifacts/package-0.12-verification.json`). Son yayımlama derlemesi başarılı (`artifacts/publish-0.12.log`). Uygulama konumu değiştiğinde başlatma düğmesi yeni plan üretir; eski önizleme planı başka yürütülebilir dosyayı yönetici olarak açamaz.
## 0.11.0 doğrulama geçmişi
## 0.13.2 — ön kontrol duruşunu görünür yapma

- Son gerçek kullanıcı denemesi 17:18:16'da preflight adımında durdu: sistem diskinde 5 GiB boş alan şartı karşılanmıyordu. Ön kontrol 2 kez denenmişti; sonraki bütün kurulum adımları 0 denemeydi. Ardından yeni, onaylanmamış bir plan oluşturulması eski nedenin ana özette görünmemesine yol açtı. Çalışan başlıyordu; bu vaka 0.13.1'de giderilen StartupUri çökmesi değildi.
- Canlı C: ölçümü yaklaşık 4,98 GiB idi. Önceki geliştirme testlerinin geçici indirme klasörleri zaten temizdi. Yalnız FromZero.exe / FromZero.Checks.exe için oluşmuş 10 çökme dökümü (57,31 MiB) E:/Kit/artifacts/crash-dumps altına korundu: kaynak/hedef tam yolları doğrulandı, hedef ACL'si kullanıcı/SYSTEM/yöneticilerle sınırlandı, SHA-256 eşleştirmesinden sonra kaynak dosyalar kaldırıldı. Kayıtlar manifestte; dosya içeriği kaybedilmedi veya dağıtıma eklenmedi. İlk sonraki C: ölçümü 5,043 GiB idi.
- 5 GiB sınırı düşürülmedi. Disk/OS/bağlantı için tek bir ön kontrol modeli hem UI hem çalışan tarafından kullanılır. UI yükseltme/kurulumdan önce kontrol eder; engel varsa sistem sürücüsü, boş/gerekli/eksik alan ve Temizlik/Depolama eylemleri görünür. Çalışan disk durumunu tekrar denetler.
- Gerçek preflight duruşu artık görünür mesajla bildirilir; attention/paused/reboot ve kullanıcı onayı reddi koşulsuz TAMAMLANDI sonucuna dönüştürülmez. Worker bitiş durumu ve nedeni geçmişe kaydedilir. Salt okunur worker-check planın açılabilir olması ile ReadyToInstall sonucunu ayrı verir.
- Dokuz yeni sınır/geri bildirim kontrolü: bir bayt eksik alan, tam sınır, gerçek 4,98 GiB durumu, sistem sürücüsü ve eksik miktar, çevrimdışı kaynak ve disk ayrımı, birleşik engeller, negatif değer ve ağ geçidi/Internet ayrımı. **292 temel + 8 gerçek süreç = 300 kontrol**.
- Tam gerçek Windows/üretici kurulumunu bu doğrulama çalıştırmaz. Mevcut kayıtlı planın onayı veya başlanmamış kurulum adımları otomatik değiştirilmedi; kullanıcı uygulama planını kabul ettiğinde kurulum başlayabilir.

- Yayımlanan 0.13.2 gerçek penceresinde Başlatmaya hazır / C: 5,04 GB yazısı görüldü; Bilgisayarımı hazırla düğmesi tıklandı ve 24 adımlık gerçek planın Evet/Hayır onayı açıldı. Son Evet seçilmedi; onay kullanıcıya bırakıldı. Yerel kanıt: artifacts/ui-start-0.13.2-verification.json. Böylece yalnız CLI çalışanı değil gerçek düğmeden onay penceresine geçiş de gözlendi.

## 0.13.1 — gerçek başlangıç çökmesi düzeltmesi

- Kullanıcının iki başarısız denemesi Windows Application / .NET Runtime 1026 kaydından doğrulandı: System.ArgumentNullException, value, System.Windows.Application.set_StartupUri. Worker dalında try bloğundan önce StartupUri=null ataması yapılıyordu. Bu yüzden yönetici izni de çözmüyordu ve hiçbir kurulum adımı başlamıyordu (`artifacts/startup-crash-cause-0.13.json`).
- App.xaml otomatik StartupUri kaldırıldı; ana pencere yalnız normal GUI yolunda açıkça oluşturulur. Worker ve salt okunur worker-check kendi dispatcher'ında pencere açmadan çalışır. Tek uygulama / önceki ön izleme dallarındaki diğer null StartupUri atamaları da kaldırıldı. WPF belgeleri bu atamanın ArgumentNullException ürettiğini açıklar.
- Yönetici uygulama mevcut token'la çocuk süreç açar; standart uygulama runas ister. Erken hata kaydı worker-error.json ve planın Message/attention durumuna yazılır. Sonuçsuz kapanma, gerçek çıkış koduyla gösterilir; bütün hatalar aynı kullanıcı/yönetici sorununa bağlanmaz.
- **283 temel + 8 gerçek süreç kontrolü = 291 kontrol geçti.** Testler hem geliştirme EXE'si hem yayımlanan self-contained EXE üzerinde çalıştırıldı (`artifacts/checks-0.13.1-startup.log`, `artifacts/checks-0.13.1-published.log`). Gerçek WPF çalışanı GUI mutex'i varken penceresiz başlatılıp planı kontrol etti. Onaysız gerçek worker ve bozuk argüman dalı yakalanmış hata kodu 1 ile kapandı; bu CLR çökmesi değildi. Hiçbir kurucu/Windows/sürücü motoru başlatılmadı; kullanıcının son plan seçimi değişmedi (`artifacts/startup-0.13.1-verification.json`).
- Kullanıcının hata veren asıl kayıtlı planı yayımlanan 0.13.1 EXE'siyle ayrıca salt okunur worker-check modunda doğrulandı: exit 0, kullanıcı/yürütülebilir/plan eşleşiyor; kayıt ve son seçim birebir aynı; 0 kurulum adımı (`artifacts/actual-plan-startup-0.13.1.json`). Doğrulama betiğinin ilk tarih karşılaştırması PowerShell otomatik tarih dönüşümü nedeniyle başarısız oldu; dosyanın UTC yazım zamanıyla tekrar doğrulandı. Bu, uygulama başlangıcı hatası değildi.
- Yayımlama başarılı ve ZIP'in bütün girişleri okunabilir; sürüm/hash kontrolü `artifacts/package-0.13.1-verification.json` kaydında. Uygulama kayıtlı planned planı göstermek üzere açıldı; otomatik kurulum veya reboot devamı başlatılmadı. Tam gerçek güncelleme/kurulum kabul testi bu salt okunur başlangıç testinin kapsamı değildir.

## 0.13.0 — kişisel hazırlama, gerçek durum ve kurtarma

- **277 temel kontrol** (`artifacts/checks-0.13-core.log`) ve temel kontrolleri de içeren **307 kontrollük canlı koşu** (`artifacts/checks-0.13-live.log`) geçti. 0.12'deki 225 kontrole 52 profil, kurucu kapsamı, hedef sürüm hattı, delil durumu, şema geçişi, karşılaştırma, regresyonda durma ve güç geri alma kontrolü eklendi. Canlı koşudaki 30 ek kontrol: 21 kaynak kaydı, tümünün çözülmesi, 5 gerçek kurucu, beşinin geçmesi ve 2 gerçek readiness kontrolü. Sistem/oyun/GPU/güç ayarı değiştirilmedi; kurucu çalıştırılmadı.
- Beş profil gerçek uygulama/bileşen/kontrol seçimlerini değiştirir. Varsayılan profiller güç veya grafik yazımını açmaz. 21 ayrı otomatik uygulama tarifi vardır; genel katalog 600 kayıt olarak korunur. Steam sürüm metadata'sı varmış gibi davranılmaz: mevcut istemci kendi güncelleyicisine bırakılır. Resmi Steam bootstrapper bağlantısı ayrıca HTTP 200 / 2.380.800 bayt HEAD kontrolünden geçti (`artifacts/steam-source-0.13.json`).
- 21 üretici kaynağı çözümlendi (`artifacts/preparation-experience-sources.json`); Steam için sabit resmi bootstrapper/kendi güncelleyicisi ayrımı kayıtta bulunur. GitHub API kullanılamadığında aynı üreticinin kararlı sürüm sayfası, repo/etiket/x64 varlık deseniyle okunur. Kaynaklar topluluk kurulum komutu sağlayamaz.
- Git, Node.js LTS, OBS, KeePassXC, Audacity gerçek dosya indirme, Windows imza/yayıncı, varsa yayınlanan SHA ve önbelleği yeniden bütünlük kontrolünden geçti (`artifacts/preparation-experience-downloads.json`). Git'in güncel imzasında CN/O Johannes Schindelin görüldü; eski uzun yayıncı metni eşleşmediği için ilk dosya reddedildi. Kural gerçek üretici adıyla düzeltildi; Windows güven kontrolü kapatılmadı. Adla birleştirilmiş indirme kanıtı 59 başarılı kayıttır; 59 ayrı otomatik uygulama değildir.
- Gerçek salt okunur tarama 185 aygıt, 1 etkin ses çıkışı, 1 etkin ses girişi ve 1 ekran okudu. Yayın profili + altı oyun taramasında 17 doğrulanan, 0 ilgi gerektiren ve 19 kullanıcı testi sonucu var (`artifacts/preparation-readiness-verification.json`). Kullanıcı testi sonuçları başarıya eklenmez. HTTPS testinde başlangıçta HTTP için tasarlanmış NCSI adresi kullanıldığı görüldü; Microsoft HTTPS metin hedefi ve ayrı standart HTTP giriş sayfası ölçümü ayrıldı. Sertifika doğrulaması kapatılmadı.
- Önce/sonra karşılaştırmaları aygıt sorun/kaybı, ağ/ses/ekran kaybı, sürücü/uygulama, güç planı ve ekran modu değişimini ayırır. Zaman ilişkisi neden kanıtı olarak sunulmaz. Güç geri alma sahte arka uçla gerçek önce-yedekle / sonra-yaz / yeniden-oku sırasıyla sınandı; dış değişiklikler korundu. Pilde yüksek performans ve mevcut olmayan güç planı yazımı yapılmadığı doğrulandı. 0.12 geçişi eklenen işlemler için onayı temizler; başka bilgisayarın kaydı yerel kayda dönüştürülemez.
- Donanım tarifinde gerçek model ve kimlik delilleri var. HP tüketici/iş modeli ayrımı kontrol edildi. Altı oyun için kayıtlı kütüphane/başlatıcı/ayar dosyası incelenir; GTA V Legacy ve Enhanced Steam kayıtları okunabilir. Oyun indirmesi, kullanıcı hesabı girişi veya firmware güvenlik değişikliği yapılmaz. Desteklenen CS2/VALORANT görüntü tercihleri mevcut yedekli motoru kullanır; ölçülmüş FPS kazancı iddia edilmez.
- Dağıtım **0.13.0.0 / 412 ZIP girişi / 68.281.427 bayt**. Bütün girişler okundu; arşiv SHA-256 yan dosyayla ve arşivdeki DLL dağıtım DLL'iyle eşleşti (`artifacts/package-0.13-verification.json`). 59 kayıtlık indirme kanıtı, güncel kullanım ve ayrıntılı 0.13 hazırlama kılavuzu dahil. Yayımlama başarılı (`artifacts/publish-0.13.log`).
- Yerel geliştirme penceresinde 900 mantıksal pikselde Hazırla / Durumu kontrol et ve gerçek salt okunur plan/tarama gözlendi. Son yayımlanan pencere bulundu; ek son görüntü alma, küçültülmüş pencere ve kullanıcı etkileşimi yüzünden tamamlanamadı. Taze pencere seçimiyle kurtarma denemesi de `user input was detected in this window; call get_window_state before continuing` döndürdü; UI otomasyonu durduruldu. Son paket için ek görsel yakalama başarılı diye işaretlenmedi (`artifacts/preparation-ui-0.13-verification.json`).
- Kabul sınırı: Gerçek sessiz kurucu davranışları, Windows/OEM kurulumu, farklı cihazlar ve reboot/logon devamı temiz Windows 10/11 x64 cihazlarında ayrıca denenmelidir. Kaynak/indirme doğrulaması bu kurulum kabul testinin yerine geçmez. Donanımların veya 600 uygulamanın tamamı için otomatik kurulum iddiası yoktur.


## 0.10.0 — sade yerleşim ve geniş katalog

- Son canlı kontrol: **194 başarılı kontrol** (`artifacts/checks-0.10-live.log`): 187 temel, 3 katalog/crosshair/sıralama, 3 yeni OEM indirmesi ve 1 tüm OEM indirmelerinin geçme koşulu. Gerçek sistem, ekran veya oyun ayarları değiştirilmedi.
- Katalog **300 → 600** ürün/sürüm kaydıdır: 30 doğrudan üretici ve **570** topluluk manifesti. Son kaynak kontrolünde **579 canlı doğrulama / 21 erişim sınırı uyarısı** oluştu; 21'i GitHub üretici API kaynaklarıdır. Bu satırlar eski kaydıyla “Kayıt kontrolü gerekli” olarak korunur, güncel gibi gösterilmez ve otomatik seçilmez (`artifacts/catalog-600-verification.json`, `artifacts/catalog-600-summary.json`). Topluluk tarafındaki 570 kaydın tamamı son taramada ayrıştırıldı. İlk adaylardaki sayısal olmayan sekiz sürüm başka uygun kayıtlarla değiştirildi.
- Sürücü edinme araçları **3 → 6**, üretici destek bağlantıları **8 → 16**. Dell Command Update 5.7.1, HP Image Assistant 5.3.7 ve Lenovo System Update 5.08.04.85 gerçek dosya indirmesi / yerel Windows imzası / önbelleği yeniden doğrulama kontrollerinden geçti; Dell ve Lenovo SHA-256 değerleri üreticinin kaydıyla karşılaştırıldı (`artifacts/oem-tool-verification.json`). Dell sertifikasının gerçek adı “Dell Technologies Inc.” doğrulandı; hash veya imza koşulları gevşetilmedi. Üç kurucu da çalıştırılmadı. Başarılı indirme kanıtı **50 kayıt** içerir.
- Marka bağlantıları bir sürücü dosyası taraması değildir. Windows'un bu bilgisayara sunduğu gerçek sürücü sayısı değiştirilmedi veya katalog sayısına katılmadı. Lenovo'nun duyurduğu bakım sonu 31 Ekim 2026 ve Commercial Vantage geçiş bilgisi arayüz ve kılavuzda belirtildi.
- Crosshair açıklama banner'ları, ProSettings referans satırları ve kaynak düğmeleri kaldırıldı; sekme “FACEIT ilk 10” olarak kısaltıldı. Görüntü ve kaynak verileri/telif dosyaları korunur. Ortak metin kutusu iç boşluğu, satır boşluğu, yazı tipi eşiği ve düğme yan boşlukları düzenlendi. Güncelleme ayarları, oyun oturumu/kütüphanesi/FPS laboratuvarı, performans seçenekleri/ekran ayarları, kapsül ayrıntıları ve kurulum araçları katlanır bölümlere alındı.
- Gerçek pencerede geniş crosshair ve oyun ekranı; dar pencerede güncelleme, Wi-Fi, performans, temizlik ve format kiti incelendi. Dar genişlikte sekmeler alt satıra, kartlar tek sütuna geçti; metin ve düğmeler gözlenen alanlarda yatay taşmadı. Yeni genel yazı tipi eşiği sayesinde oyun başlığı Counter-Strike 2 olarak daha okunur görünüyor. Veri silme, FPS profili uygulama veya sürücü kurma işlemi yapılmadı.
- Önceki başarısız kaynak/imza denemeleri `artifacts/checks-0.10-first.log` ve ilgili önceki kanıtlarda tutuldu; son başarılı çalışma ayrı kayıttır.
- Son paket **0.10.0.0 / 409 giriş / 66.103.858 bayt**. ZIP'in bütün girişleri okundu ve SHA-256 yan dosyayla eşleşti; 50 başarılı indirme kaydı ve gereken lisans/atıf dosyaları içeridedir (`artifacts/package-0.10-verification.json`). Son uygulama açıldı; bağımsız uygulama taraması **600 kayıt / 21 kaynak uyarısı** döndürdü ve indirme başlatılmadı. Son paketlemede derleme hatası oluşmadı (`artifacts/publish-0.10.log`).

## 0.9.1 doğrulama geçmişi

## 0.9.1 — gerçek Dust2 ve FACEIT crosshair kodları

- **196 kontrol başarılı** (`artifacts/checks-0.9.1-live.log`): önceki 177 kontrol, 9 yeni FACEIT veri/ayrıştırıcı/yönlendirme kontrolü ve 10 canlı hesap kodu yenileme kontrolü. Kodlar checksum ve kanonik kod çözme/üretme kontrolünden geçti (`artifacts/faceit-crosshair-verification.json`). Gerçek oyun dosyaları veya PC ayarları değiştirilmedi.
- On hesabın tamamı için crosshair.club oyuncu sayfasının öne çıkan CS kodu alındı: donk666 → donk; CEMEN_BAKIN → kyousuke; dem0nnnnnnnn → dem0n; bluewh1te → bluewh1te; b1st- → b1st; xKacpersky → xKacpersky; z1Nny → z1Nny; -SYPH0 → SYPH0; nipl → npl; abiraju → abiraju. Eşleşmeler ve gözlem/kontrol tarihleri gömülü `Assets/faceit-crosshairs.json` içindedir. Kaynak yanıtları araştırma kanıtı olarak `artifacts/club-*.html` dosyalarında tutuldu.
- Ayrıştırıcı yalnız `pl-x-code` öne çıkan kodunu okur; tarihçe kodunu seçmez. Kaynak oyuncu adı, kaynak adresi, tarih, kod biçimi/checksum kontrol edilir. Başka hesap/kaynak, eksik şema veya eski CSGO kodu reddedilir; önceki kayıt korunur. Doğrudan FACEIT API'sinden canlı crosshair okuma veya oyun içinde import testi yapıldığı iddia edilmez.
- Dust2 piksel çizimi kaldırıldı. Valve'ın resmi CS2 sayfasının kullandığı `cs2/maps/dust2_double_doors_s2.jpg` orijinal JPEG dosyası gömüldü. Görüntü görsel olarak incelendi; uygulama yalnız en boy oranını koruyarak kırpar. Kaynak, hash ve dosya boyutu `artifacts/dust2-source-verification.json`; atıf `Assets/Dust2-SOURCE.txt` içindedir.
- Gerçek WPF penceresinde katalog ana sayfa olarak açıldı. Pro ve FACEIT sekmeleri, on hesaba ait Dust2 önizlemeleri ve donk666'nın aynı sayfada büyük önizlemesi kontrol edildi. Katalog/büyütme sırasında `list_windows` yalnız bir FromZero ana penceresi döndürdü. Crosshair sayfasında `CreateDialog` veya `ShowDialog` kalmadı. Yeni görünümde kod, ELO, oyuncu eşlemesi, gözlem tarihi ve kontrol tarihi ayrı gösterilir.
- Release derleme: 0 hata / 0 uyarı (`artifacts/build-0.9.1.log`).
- Son ZIP doğrulandı: **0.9.1.0 / 409 giriş / 66.064.444 bayt**. Bütün girişler okundu; SHA-256 yan dosyayla eşleşti. Dust2 atıf dosyası ve kullanım kılavuzu pakette bulundu (`artifacts/package-0.9.1-verification.json`). Paketlenmiş uygulama yeniden açıldı; donk666 kartından kod kopyalandı, “Kendi kodun” sekmesine Ctrl+V ile yapıştırıldı ve aynı kodun gerçek Dust2 üzerinde aynı siyah crosshair'i çizdiği görüldü. İşlem boyunca ayrı katalog/önizleme penceresi oluşmadı.

## 0.9.0 doğrulama geçmişi

## 0.9.0 — geniş katalog ve global ELO

- Temel kontroller: **177 başarılı** (`artifacts/checks-0.9-core.log`). Canlı kaynak çalışması: **180 başarılı kontrol** (`artifacts/checks-0.9-live.log`); 177 temel + uygulama kapsamı + crosshair yenileme + beş bölge sıralaması. Örnek indirmelerin sonucu bu sayıya dahil değildir.
- Canlı uygulama taraması **300 kayıt / 0 kaynak uyarısı** döndürdü (`artifacts/catalog-300-verification.json`). Mevcut 30 üretici hattına 270 Scoop Extras topluluk manifesti eklenmiştir. CLI veya manifest betikleri çalıştırılmadı. HTTPS, x64/ortak tek EXE/MSI, hash, alan adı ve GitHub depo yolu kontrolleri vardır. Kaynak hatası halinde gömülü kayıt “kontrol gerekli” olarak kalır; otomatik önerilmez.
- Yeni indirme yolunda **Anki MSI ve Geany EXE** gerçek indirme, SHA-256 ve Windows güven zinciri denetiminden geçti. **Alacritty** imzasız olduğu için 0x800B0100 ile reddedildi; istisna eklenmedi ve çalıştırılmadı (`artifacts/community-download-verification.json`). Tüm 270 paket indirilmiş gibi sunulmaz. Birleşik başarılı indirme kanıtı artık **47 kayıt** içerir.
- 50 pro kaydının tamamı kod/önizleme alanlarına kayıpsız çözülüyor. Eksik nipl/npl tablosu yerine tam alanları bulunan senzu kaydı kullanıldı. Kaynakta bulunan yeni “Dynamic Cross (Legacy/Shot Feedback)” etiketi stil 2 olarak tanındı. Kaynak kontrol tarihi 27 Ekim 2026'dır; oyun içi import/FPS ölçümü yapılmadı.
- FACEIT için EU, NA, SA, SEA, OCE bölge ilk 10 tabloları canlı alındı ve ELO ile birleştirildi (`artifacts/faceit-5-region-verification.json`). Bu tarihli üçüncü taraf Faceit Tracker kaydıdır, resmi anlık global sıralama iddiası yoktur. Elde edilen global ilk 10'un tümünün EU olması diğer bölgelerin atlandığı anlamına gelmez. Herhangi bir bölge kaynağı başarısızsa önceki bütün kayıt korunur. Doğrulanamayan hesaplara pro kodu tahminle atanmaz.
- Yeni pencere üzerinde 50 oyunculuk katalog, üç sütunlu kartlar, varsayılan Dust2 piksel çizimi ve FACEIT global sıralama görünümü açılıp incelendi. Dust2 uygulama içinde çizilen bir yorumdur; oyun ekran görüntüsü kullanılmadı.
- Kendi kodunu önizleme penceresine referans CS kodu girildi; geçerli kod durumu ve Dust2 üzerinde kırmızı crosshair çizimi görüldü. Son paketin “Yalnız uygulamaları tara” işlemi 300 kayıt / 0 kaynak uyarısı döndürdü. 1/15 ve 2/15 sayfaları, sonra 15 kurulu uygulamayı gösteren filtre gerçek pencerede kontrol edildi. Dokuz güncelleme kuyruğa önerildi; indirme başlatılmadı.
- Paket ZIP'i uygulama açıkken dosya kilidi yüzünden ilk denemede oluşturulamadı. Paketleme betiği artık geçici ZIP başarılı olunca ana arşivi değiştirir; başarısız işlem mevcut arşivi ezmez. Uygulama kapatılarak yeniden paketlendi; son yayın komutu başarılıdır (`artifacts/publish-0.9-final.log`).
- Son ZIP: **0.9.0.0 / 408 giriş / 63.584.644 bayt**. Bütün girişler sonuna kadar okundu; SHA-256 yan dosyayla eşleşti. Yeni topluluk lisansı, crosshair codec lisansı, kullanım kılavuzu ve 47 başarılı indirme kaydı içeride doğrulandı (`artifacts/package-0.9-verification.json`). Uygulama yeniden açıldı.
- İlk temel test tekrarında çalışan canlı testin dosya kilidi derlemeyi engelledi; canlı çalışma bittikten sonraki tekrar 177 kontrolle geçti. Son başarılı kayıtlar yukarıdaki loglardır. Gerçek PC, ekran, GPU veya oyun ayarları değiştirilmedi.

## 0.8.0 doğrulama geçmişi

## Pro crosshair kataloğu ve genişletilmiş güncellemeler

- 18 oyuncunun ProSettings kaynak tabloları 27 Ekim 2026'da kontrol edildi. Kaynak URL'si, sayfanın güncellenme tarihi, kontrol tarihi ve sayısal alanlar gömülü `Assets/crosshairs.json` içindedir. Eski alan şemasında kalan iki aday kataloğa alınmadı. Yeni kodlar bu kayıtlardan üretilir; oyuncunun doğrudan paylaştığı kod veya maç tarihi gibi sunulmaz.
- CS2 v1 codec, MIT lisanslı akiver/csgo-sharecode referans vektörünü aynı 46 karakterle üretip çözüyor. İşaretli gap, scope/outline/flag alanları, checksum, sürüm, taşma ve değer aralığı kontrolleri geçti. 18 katalog girdisinin kodu birebir önizleme parametrelerine çözülür. Eksik kaynak/bozuk şema eski kaydı değiştiremez. Oyun içinde import kabulü veya dinamik render eşitliği test edilmedi; UI durağan şematik önizleme olarak etiketlidir.
- Gerçek 0.8 penceresinde katalog açıldı: üç sütunlu kartlar, XANTARES araması, kod kopyalama durumu, canlı kaynak yenileme ve büyütme görünümü incelendi. Crosshair için oyun/Steam ayarı değişmedi. Kaynak yenileme yalnız `%LOCALAPPDATA%\FromZero\crosshair-xantares.json` kaydını oluşturdu.
- Uygulama kataloğu **30 ürün/sürüm hattı / 0 kaynak uyarısı** döndürdü. Node LTS/Current ayrı hattan sayılır. .NET kaynağı **8 hat / 104 paket / 0 uyarı** döndürdü; SDK'nın her feature band'ında son kararlı yama ayrı sunulur. Kurulu SDK farklı banda otomatik geçirilmez; eski bantlar elle seçilir. `artifacts/application-source-verification.json`, `artifacts/runtime-source-verification.json`.
- **20 yeni gerçek indirme tarifinin tamamı doğrulandı:** Thunderbird, Node LTS, Node Current, Vivaldi, Brave, PowerShell, ShareX, Audacity, HandBrake, Rufus, WinMerge, KeePassXC, MPC-HC, RustDesk, LocalSend, ScreenToGif, DBeaver, HeidiSQL, Joplin, Obsidian. Kurucular çalıştırılmadı; test klasörleri sonradan temizlendi. 18'i Windows yayıncı imzasıyla; ShareX/MPC-HC yalnız resmi üretici GitHub varlığının SHA-256'sı ile doğrulandı. Önceki 7-Zip izni de korunur. Birleşik kanıt **45 paket** içerir. `artifacts/application-expanded-final-verification.json`, `artifacts/download-verification.json`.
- İlk denemelerde farklı sertifika yayıncı adı veya imzasızlık nedeniyle reddedilen paketler kayıtlandı. Thunderbird/Mozilla, KeePassXC/DroidMonkey Apps, LocalSend/Tien Do Nam, HandBrake/Open Source Developer Scott Rae, ScreenToGif/N-Tech ve Joplin/Joplin eşleşmeleri gerçek imzaya göre düzeltildi. ShareX/MPC-HC için hash-only izin uygulama kimliği + tam üretici depo yolu + zorunlu 64 haneli hash ile sınırlıdır; imzasızlık gizlenmez. İlk reddedilen dosyalar çalıştırılmadı. Önceki loglar hata geçmişi olarak korunur.
- Son kaynak/4 tekrar indirme çalışmasında **174 kontrol başarılı**; bunun 167'si temel kontrol, 2'si kaynak, 4'ü indirme ve 1'i tüm istenen indirmelerin geçmesi koşuludur (`artifacts/checks-0.8-retry.log`). Önceki tam 20 paket denemesinde 16 indirme geçti, dört yayıncı/unsigned düzeltmesi son çalışmada ayrı ayrı geçti. Tek çalışmada 20'si birlikte geçilmiş gibi sayılmadı.
- 15 dakikalık resmi metadata önbelleği kaynak kontrol zamanını korur ve eski/hatalı kayıt güncel veri gibi kullanılmaz. Test tekrarı bir önceki gerçek HTTP cevaplarının hâlâ geçerli önbelleğini kullandı; `artifacts/verified-feed-cache` bu kayıtları içerir. Yeni test koşulu istenen indirmelerden biri bile başarısızsa tüm indirme grubunu başarısız sayar.
- OEM kapsamına 8 resmi destek sayfası ve Microsoft Store güncelleme ekranı eklendi. Bunlar otomatik taranan paket sayısına katılmadı; BIOS/firmware akışı ve Windows/kurum ilkeleri değiştirilmedi. WUA tüm taraması bu tur tekrar çalıştırılmadı; mevcut kapsam korundu.
- Son dağıtım yeniden oluşturuldu: sürüm 0.8.0, ZIP'in 407 girişi okunabilir, SHA-256 yan dosyayla eşleşir. CrosshairCodec MIT lisansı, güncel kullanım kılavuzu ve 45 gerçek kurucu indirme kanıtı pakette doğrulandı.

## 0.7.0 doğrulama geçmişi

## NVIDIA FPS profili, ekran Hz ve CS2 kopyalama

- Release/publish başarılı; derleme 0 hata / 0 uyarı. Son kontrol çalışması **147 başarılı kontrol** (`artifacts/checks-0.7.log`). Gerçek sistem ayarları değiştirilmedi.
- NVIDIA NVAPI üzerinden güç, doku filtreleme, yenileme tercihi ve VSync gerçek sürücüden salt okunur okundu. Değerler sırasıyla 5, 0, 0, 0x60925292; yeni penceredeki “NVIDIA ayarlarını oku” diyaloğunda anlamlı adlarıyla kontrol edildi. Gerçek NVIDIA yazma yapılmadı; genişletilmiş uygulama/geri alma ve dış değişikliği koruma test backend'iyle geçti.
- Windows ekran API yapıları gerçek x64 sistemde doğrulandı; AMD üzerinden bağlı dahili ekran 1920×1080 / 144 Hz ve aynı çözünürlükte 60,144 Hz modları döndü. Ekran kartı arayüzde incelendi. Gerçek ekran modu değiştirilmedi.
- Ekran test backend'iyle sürücü testi + önceden yedekleme, geçici uygulama, onayla kalıcılaştırma, eski Hz'ye dönme, bağımsız zaman aşımı, sonraki dış değişikliği koruma, desteklenmeyen/çözünürlük değiştiren seçim reddi, uygulama hatası ve onaysız kapatma geri alma doğrulandı. Kopan ekran/geri alma hatasında kurtarma durumu korunur. Canlı ekranın kararıp geri gelmesi geliştirme sırasında denenmedi.
- CS2 üretici yalnız `+fps_max` ve `+fps_max_ui` oluşturur. Negatif hedef reddi ve eski motor/öncelik flag'i eklememe kontrolleri geçti. UI'da seçenek paneli açıldı, `+fps_max 0 +fps_max_ui 120` görüldü; kopyalama düğmesi “Kopyalandı” durumuna geçti. Steam veya oyun dosyası düzenlenmedi. Canlı oyun başlatma/komut kabulü ve FPS kazanımı ölçülmedi.
- UI: Performans sayfası, seçimler, NVIDIA okuma diyaloğu, ekran kartı ve CS2 kopyalama alanı gözle incelendi. Modal kapandıktan sonra sistem menüsü odağı Escape ile temizlenince PageDown kaydırması çalıştı; alttaki içerikler erişilebilir. FPS profili ve gerçek Hz uygulama düğmeleri tıklanmadı.
- ZIP içindeki 406 giriş okunabilir; EXE sürümü 0.7.0, SHA-256 yan dosyayla eşleşiyor. Önceki 25 gerçek kurucu indirme kanıtı korunuyor; bu tur indirmeler tekrarlanmadı.
- Dayanaklar: NVIDIA resmi `NvApiDriverSettings.h` (güç/kalite/refresh/VSync kimlik ve değerleri), NVIDIA CS2 Reflex rehberi, Microsoft ChangeDisplaySettingsExW, Steam başlatma seçenekleri ve Valve 25 Haziran 2024 görüntü/FPS sınırı sürüm notları. VSync kapatma isteğe bağlı; G-Sync/VRR ve Reflex + Boost tercihi kullanıcıya açıklanır.

## 0.6.0 doğrulama geçmişi

## Tek arka plan, uygulama güncellemeleri ve oyun görüntü profilleri

- Üstteki ikinci dünya/mahalle şeridi kaldırıldı; ana pencere tek PixelWorld kullanır. Gerçek pencere ve Oyunlar ekranında kontrol edildi.
- Kapsam: canlı kaynak testinde 10 uygulama / 0 uyarı; .NET ve SDK için 8 hat / 64 paket. Önceki Windows teklif taraması, 40 oyun bileşeni kontrolü, 12 VC++ kurucusu korunur. XNA MSI ve DirectX çevrimdışı kurucu eklendi.
- Beş ek gerçek indirme başarılı: Microsoft XNA Framework 4.0 MSI, Google Chrome, VLC, 7-Zip ve .NET SDK 10.0.403 x64. XNA/Chrome/VLC/SDK Windows yayıncı imzasıyla; 7-Zip üreticinin GitHub yayın varlığı SHA-256 kaydıyla doğrulandı. Kurucular çalıştırılmadı. `artifacts/application-download-verification.json`; birleşik kanıt 25 paket.
- İlk denemede VLC'nin resmi HTTPS aynasına yönlendirmesi ve 7-Zip'in imzasız olması nedeniyle indirmeler reddedildi. VLC için üreticinin yönlendirdiği HTTPS yansısı kullanıldı, VideoLAN imza şartı korundu. 7-Zip için yalnız resmi ip7z/7zip yolunda, 64 haneli üretici hash'i varsa çalışan dar bir doğrulama yolu eklendi; imzasızlık kullanıcıya gösterilir. Diğer kurucuların imza kontrolü korunur.
- 12 oyun görüntü kontrolü: CS2 CRLF/BOM şeması, salt önizleme, gerçek test dosyasına uygulama, tam özgün baytlarla geri alma, sonradan değişmiş değeri koruma, eskimiş planı reddetme, çalışan oyun engeli, Valorant bölüm bazlı INI değişimi/geri alma, yinelenen anahtar ve salt okunur dosya reddi. Silme/yazma yalnız test klasörlerinde yapıldı.
- Canlı CS2 görüntü dosyası salt okunur olarak incelendi: 9 değişiklik / 0 tanınmayan hedef anahtar. Gerçek dosya değiştirilmedi. Bu bilgisayarda Valorant ayar dosyası yok; uygulama ve geri alma geçici INI üzerinde doğrulandı. Oyunların değerleri fiilen kabul etmesi veya gerçek FPS kazanımı test edilmedi.
- Son yayımlanan 0.6.0 penceresinde CS2 Önizle diyaloğu açıldı: aynı 9 değişiklik / 0 eksik anahtar görüldü ve yazma yapılmadan kapatıldı. Genel bakış, Oyunlar ve Güncellemeler ekranlarının tek arka planı incelendi. Güncelleme merkezi canlı taraması 3 Windows/sürücü teklifi, 10 uygulama, 64 .NET/SDK paketi ve 40 oyun bileşeniyle tamamlandı; kaynak uyarısı gösterilmedi. Otomatik indirme kapalıydı; kurulum/indirme başlatılmadı. Uygulama aramasında Chrome yazılınca yalnız Chrome kartı kaldığı erişilebilirlik ağacından doğrulandı.
- Son temel çalışma yeniden 132 kontrolle geçti (`artifacts/checks-0.6-core.log`). Dağıtım EXE sürümü 0.6.0; ZIP içindeki 406 giriş okunabildi, SHA-256 yan dosyayla eşleşti, paket 25 gerçek indirme doğrulama kaydı içeriyor.
- Alt kart seçim sayacının tıklama/kaydırma doğrulaması masaüstü otomasyonunun koordinat dönüşümü nedeniyle tamamlanamadı: `point (1763, 934) is over explorer.exe ... not target window FromZero.exe`. Pencere yeniden etkinleştirilip taze görüntü alındıktan sonra da araç sınırı tekrarlandı; ek UI girdisi gönderilmedi. Bu, uygulamada doğrulanmış bir kaydırma hatası olarak değerlendirilmedi.
- Tam canlı kaynak/indirme test çalışması: **139 kontrol başarılı**. Güncel temel test sayısı 132; iki canlı kaynak ve beş indirme kontrolü toplamı 139'dur. `artifacts/checks-0.6.log`.
- Araştırma kaynakları: Riot'un 7 Ekim 2026 FPS/grafik rehberi, Epic Scalability Reference ve NVIDIA CS2 Reflex rehberi. Sayısal dosya eşlemesi, yalnız mevcut tanınan anahtarlara uygulanır; yeni komut/anahtar eklenmez. Çözünürlük, yenileme, çözünürlük ölçeği ve giriş ayarları değiştirilmez. VSync ayrıca seçilir. FPS garantisi verilmez.

## 0.5.0 doğrulama geçmişi

## Mahalle ve güncelleme kataloğu

- Son temel test çalışmasında 115 kontrol başarılı. Önceki canlı çalışmada iki katalog ve 9 ek indirme/imza kontrolü de geçti; toplam 126 ayrı kontrol. Release derlemesi 0 hata / 0 uyarı.
- 10 ayrı modül durağı; temizlik/arcade/atölye tabela ve nesneleri, karakterin ileri/geri yürümesi ve kapıda durması uygulama penceresinde incelendi. Aynı sayfa yeniden çizildiğinde tekrar yürüyüş başlatılmaz. Animasyon zamanlayıcısı varışta durur; piksel boyaları tekrar kullanılır.
- Temizlik ekranı ve performans bağlantılarında üçüncü taraf marka/kaynak metinleri kaldırıldı. Temizlik sayfası erişilebilirlik ağacında eski marka adı bulunmadığı doğrulandı. Gerekli MIT metni anonim isimli THIRD-PARTY-NOTICES.txt dosyasında korunur; kaynak köken kaydı geliştirme ağacında kalır.
- Canlı Microsoft .NET sürüm indeksi: 8 kararlı hat, x86/x64 için toplam 48 Runtime/Desktop/ASP.NET Core paketi; kaynak uyarısı yok. `artifacts/runtime-source-verification.json`.
- Canlı geniş Windows Update taraması: üç teklif (NVIDIA ekran sürücüsü, HP firmware, Windows yazılım teklifi), uyarı yok. Firmware/BIOS, gizli ve superseded paketlerin indirmeye seçilmemesi kontrol edildi. `artifacts/windows-catalog-verification.json`.
- VC++ 2005/2008/2010/2012 x86/x64 ve .NET Runtime 10.0.14 x64 resmi kaynaklardan gerçekten indirildi. Windows Authenticode yayıncı kontrolü geçti; .NET dosyası resmi SHA-512 ile eşleşti. Kurucular başlatılmadı. `artifacts/expanded-download-verification.json`. Önceki 11 testle birleşik kanıt 20 paket içerir.
- Toplu Windows indirme betiği yalnızca Download kullanır; Install çağırmadığı, kimlik/revizyon doğrulaması ve firmware reddi kontrol edildi. Canlı Windows indirme ve sürücü kurulumu uygulanmadı. Otomatik indirme, kullanıcı açtıktan ve kapsamlı tarama başlattıktan sonra çalışır; zamanlanmış arka plan taraması yoktur.
- .NET metadata adresi/yayıncı alanı/SHA-512 izin listesi, bütün kurulu yamaların korunması ve aynı sürüm hattında karşılaştırma testleri geçti. Önizleme veya destek dışı sürümler otomatik seçilmez. İnternetteki bütün tarihsel sürücü sürümleri için tam kapsam iddiası yoktur.

## 0.4.0 doğrulama geçmişi

## ReClaim entegrasyonu ve yerleşim

- Kaynak depo `Gercekkriptocu/reclaim-cleaner`, sürüm `c154b018c715c38b1362da0ad8c3f016cf2c6b5d`. Gömülü `targets.json`, kaynaktaki 233 kuralla bayt bayt aynı. SHA-256: `8328e8b1294b2f8b4f1277ffbc6d8e1e8c3c79a849d3ae90ad87c68bc0829bf1`. MIT lisansı ve uyarlama kapsamı dağıtım paketine eklenir.
- **99 kontrol başarılı.** 24 yeni ReClaim kontrolü; kural doğrulama, korunan kökler, yaş/istisna filtreleri, salt tarama, önizleme sonrası yeni/değişmiş dosyaları koruma, açık uygulama engeli, kilitli dosya, dosya filtre modu, yönetici sınırı, rapor komutlarını çalıştırmama, hard link ve dizin bağlantısı koruması, iptal ve kullanılmış planın reddini kapsar. Silme testleri yalnızca oluşturulan test verileriyle yapılır.
- `ReclaimFileDelete` gerçek Windows dosya tutamacıyla silme testinden geçti. Tarama sonrası metadata ve gerçek yol değişikliği kontrol edilir; hard link sayısı birden büyükse silinmez.
- Gerçek Windows arayüzü yaklaşık 1307×828 ve 897×623 boyutlarında incelendi. Hero düğmesi kesilmiyor, özet kartları dar pencerede iki sütuna geçiyor. Kısa pencerede sol menü kaydırılarak Temizlik'e erişiliyor. Durum satırı ayrı sütunla çakışmadan gösteriliyor.
- Canlı ilk tarama, `files` modundaki Windows kökü korumasında kesildi; bu işlem dosya silmedi. Tarama, sadece doğrudan eşleşen dosyaları inceleyecek ve koruma hatalarını hedef bazında atlayacak şekilde düzeltildi; bu hata için regresyon kontrolü eklendi.
- Düzeltilmiş paketle canlı tarama tamamlandı: 233 kural işlendi, 41 hedefte 8,61 GB mantıksal dosya boyutu bulundu. Varsayılan uygun seçim 15 hedef / 1,95 GB oldu. Bazı hedefler erişim nedeniyle kısmi; bu rakamlar tamamı silinebilir veya fiziksel olarak açılacak alan garantisi değildir. Kullanıcı TEMP listesindeki 2076 dosya, ayrı ve kaydırılabilir inceleme penceresinde açıldı. Hiçbir gerçek dosya silinmedi.
- Temizlik onayı kaydırılabilir hedef listesi ve sabit eylem alanıyla açılır; kalıcı silme için kullanıcının seçim onayı gerekir. Gerçek kullanıcı önbellekleri geliştirme sırasında silinmedi.
- Canlı 15 hedef / 3054 dosyalık silme önizlemesi açılıp incelendi; onay kutusu boşken silme düğmesi kapalı. Escape ile vazgeçildi ve dosya silinmedi. Son görsel düzeltmelerde arama alanına görünür etiket/yükseklik eklendi; birincil düğmenin üzerine gelindiğinde yazı kontrastını bozan renk değişimi kaldırıldı.

## 0.3.0 doğrulama geçmişi

Yeni modüller: hedefli ağ onarımı, geri alınabilir TCP otomatik ayarı, Windows/NVIDIA performans profili, 40 oyun bileşeni/mimari kontrolü ve sade ana sayfalar.

Son kontrol programı: **75 kontrol başarılı**. Son Release derlemesi: **0 hata, 0 uyarı**. Yeni beş indirme testi ayrıca başarılı tamamlandı; birleşik indirme kanıtında 11 resmi paket vardır.

- Gerçek NVIDIA DRS okuması başarılı: güç yönetimi `5`, doku filtreleme `0` okundu. NVIDIA sürücü ayarı değiştirilmedi.
- Sahte performans platformunda Windows/NVIDIA ayar uygulama, geri alma ve kısmi hata kurtarma kontrolleri geçti.
- Statik IP'yi koruma, sağlıklı ağa gereksiz işlem önermeme, adaptör kimliği ve işlem izin listesi kontrolleri geçti.
- Gerçek Windows penceresinde WLAN hizmeti/fiziksel adaptör/DHCP kontrolü ve Microsoft DNS/TCP bağlantısı başarıyla test edildi. Bağlantı sağlıklı raporlandı. Adaptör yeniden başlatma, DHCP yenileme ve Winsock sıfırlama bu bilgisayarda uygulanmadı.
- Beş ek paket indirildi ve imzaları doğrulandı: DirectX ek kütüphane kurucusu, VC++ v14 x64/x86, VC++ 2013 x64/x86. `artifacts/download-components-verification.json` ve birleşik `artifacts/download-verification.json` kanıtları vardır. Kurucular çalıştırılmadı.
- Bileşen durumu `artifacts/component-verification.json` dosyasına kaydedilir. Dosya/kurulum kaydı varlığı tam oyun uyumluluğu kanıtı değildir.
- Sol menü hizalaması, kapalı teknik ayrıntılar ve sade performans/ağ ekranları gerçek Windows penceresinde incelendi.

Driver Booster ile veritabanı kapsamı eşdeğerliği ve gerçek FPS kazanımı doğrulanmadı. NVIDIA yazma/geri alma yolu resmi API üzerinden uygulanmıştır; canlı sürücü ayarını değiştiren test yapılmadı.

## 0.2.0 doğrulama geçmişi

26 Ekim 2026 — Windows 11 x64, .NET 10.

Yeni sürümde uygulama adı, EXE, çözüm/proje adları, veri klasörü, kapsül metinleri, başlıklar ve yayın paketi **FromZero** olarak değiştirildi. Önceki ön izleme ayarları uyumluluk katmanıyla korunur.

Yeni doğrulamalar:

- Yerel kontroller: gerçek uygulama envanteri, PCI GPU tespiti, aygıt/sürücü eşleştirme, INF dosyasının varlığı, kaldırılmış cihazı ayırma, güvenli HTTPS yönlendirmeleri, resmi sayfadan kurucu çözümleme, imzasız dosyayı reddetme, gerçek sürücü kataloğu ve kurulu AMD Software dosyasının Windows güven doğrulaması dahil. Firmware kurulumunun yetki/yedekleme adımından önce reddedilmesi de kontrol edilir.
- **Canlı Windows Update Agent taraması başarılı**: NVIDIA görüntü sürücüsü **32.0.16.1091** ve HP System Firmware **15.27.0.0** sunuldu. Hiçbir paket kurulmadı. Firmware/BIOS paketleri bu uygulamadan kurulamaz. Kanıt: `artifacts/driver-source-verification.json`.
- **Altı tam indirme başarılı**: Firefox, Visual Studio Code, Steam, NVIDIA App, AMD Auto-Detect / Adrenalin kurucusu, Intel Driver & Support Assistant. Üretici imzası ve yayıncı doğrulandı, SHA-256 hesaplandı, önbellekteki dosya tekrar doğrulandı. Kurucular çalıştırılmadı.
- İndirme kanıtı: `artifacts/download-verification.json`. Kaynak URL, tarih, bayt sayısı, SHA-256 ve yayıncı içerir. Üretici paketi sonradan değişebilir; uygulama çalışma sırasında yeniden doğrular.
- Canlı yerel envanter: **185 cihaz, 186 sürücü, 472 masaüstü / Store kaydı**.
- NVIDIA GeForce RTX 3060 Laptop GPU: sürücü **32.0.15.9282**, Windows INF dosyası mevcut; yardımcı NVIDIA App bu bilgisayarda tespit edilmedi.
- AMD Radeon Graphics: sürücü **31.0.21924.61**, Windows INF dosyası mevcut; AMD Software kurulum kaydı ve fiziksel EXE bulundu, imzası doğrulandı.
- Intel GPU/DSA bu bilgisayarda tespit edilmedi; resmi DSA paketi yine de indirme testinden geçti.
- Kişisel envanter kanıtı yerelde `artifacts/inventory-verification.json` içinde tutulur; dağıtım ZIP'ine eklenmez.
- FromZero'nun gerçek Windows penceresinde genel bakış, oyun istasyonu, format ve kurulum ekranları kontrol edildi. Tek piksel dünya arka planı, yarı saydam paneller ve sayfa değiştirmede ortak haritada kamera geçişi gözlendi.
- İmza denetimi Windows `WinVerifyTrust` ve doğrulanmış imza zincirindeki sertifika üzerinden çalışır. PowerShell imza modülünün kullanılamadığı bu bilgisayarda gerçek indirme testleri bu yolla başarıyla tamamlandı.

Gerçek sürücü **kurulumu**, sürücü düşürme, GPU uygulaması kurulumu, FPS kazanımı ve farklı üretici cihazlarındaki uyumluluk bu teslimatta test edilmedi. İndirme veya kurucu açılışı başarılı kurulum olarak işaretlenmez. Güncellik ve donanım uyumluluğunun son kararı üretici kurucusundadır.

## Önceki sürümün doğrulama geçmişi

26 Ekim 2026 — Windows 11 x64, .NET SDK 10.0.401.

- Release çözüm derlemesi: **0 hata, 0 uyarı**.
- Bağımsız kontrol programı: **38 kontrol başarılı**.
- Canlı envanter: **185 cihaz, 186 sürücü** okunabildi.
- Taşınabilir dağıtım: kendi .NET/WPF çalışma ortamı dahil.
- ZIP bütünlük/CRC kontrolü: başarılı; 403 dosya; yaklaşık 62.4 MiB.
- Piksel arayüz: gerçek Windows penceresinde açılış, sürücü ve oyun sayfaları görsel olarak kontrol edildi.
- QR kurtarma: gerçek fiziksel Ethernet ve Wi-Fi adaptörleri için yerel QR görüntüleri üretildi ve pencerede görüntülendi. Telefonla tarama/gerçek paket indirme testi yapılmadı.

Kontrol programında doğrulanan başlıca durumlar:

1. PowerShell dosya yolu tırnaklaması.
2. Kapsülde üst klasöre çıkma, mutlak yol ve benzer önekli kardeş klasör reddi.
3. INF donanım kimliği, yorum satırları, UTF-16 ve mimari katalog seçimi.
4. Katalog yolunun paket dışına çıkmasını reddetme.
5. Kapsülde tam içerik, değişmiş/eksik dosya ve yinelenen giriş kontrolü.
6. TEMP planında eski/yeni dosya ayrımı, silme öncesi değişiklik ve dış klasör yolu kontrolü.
7. CSV alıntılama, FPS matematiği, süreç/swapchain ayrımı, stutter ve geçersiz örnekler, P99.
8. Oyun argümanları ve geçersiz argümanların reddi.
9. Sahte platformda oyun ayarlarını uygulama ve geri alma.
10. Oturum dışında değiştirilmiş değerleri koruma.
11. Kesintiye uğramış oturumu kalıcı kayıttan kurtarma.
12. Kısmi uygulama başarısızlığında geri alma.
13. Eksik güç planını veya kayıt değerini değişiklik yapmadan reddetme.
14. Geri alma başarısızlığında kurtarma kaydını tutma ve tekrar deneyebilme.
15. Gerçek Windows donanım/sürücü envanteri ve bellek/güç planı okuma.

Gerçek sürücü kurulumu/güncellemesi, UAC onaylı sistem değişiklikleri, kullanıcı TEMP klasörü temizliği ve güç/kayıt tercihi değişiklikleri bu bilgisayarda otomatik denenmedi. Donanım uyumluluğu, oyun başlatıcıları/anti-cheat ve FPS kazanımı ayrı cihazlarda doğrulanmalıdır. Deneysel kayıt tercihleri için destek her Windows derlemesinde garanti edilmez.

Bu kayıt, kaynak testleri ve sınırlı gerçek pencere kontrollerini belgeler; bütün donanımlarda üretim sertifikasyonu anlamına gelmez.











