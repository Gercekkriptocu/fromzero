# FromZero 0.13 — bilgisayarını kullanıma hazırla

## Ekran ve kullanım

**Bilgisayarı hazırla** üç bölüm içerir: **Hazırla**, **Durumu kontrol et**, **USB ve kurtarma**. Bir kullanım profili seç; uygulama, oyun ve isteğe bağlı güç/görüntü tercihlerini açılır bölümlerden düzenle. **Planı incele** yalnız envanter okur ve yerel bir plan kaydeder. **Bilgisayarımı hazırla** incelenebilir adımları gösterir; kabulünden sonra aynı Windows kullanıcısı için yönetici çalışanı açar. Otomatik kurulum Windows 10 2004+ / Windows 11 x64 kapsamındadır.

| Profil | Varsayılan uygulamalar | Ek kontrol |
|---|---|---|
| Günlük kullanım | Firefox, VLC | Temel bağlantı, grafik ve ses |
| Rekabetçi oyun | Firefox, VLC, Steam | DirectX ek kütüphaneleri, eski Visual C++; seçilen oyunlar |
| Yayın ve içerik | Firefox, OBS Studio, Audacity, VLC | Etkin mikrofon; OBS kayıt/kodlayıcı için kullanıcı testi |
| Yazılım geliştirme | VS Code, Git, Node.js LTS, PowerShell, Notepad++ | Çalıştırılabilir dosya ve PATH görünürlüğü |
| Dizüstü kullanım | Firefox, VLC | Pil, kamera, işaret aygıtı; işlev tuşları kullanıcı testi |

Profiller güç veya görüntü ayarını kendiliğinden değiştirmez. Güç tercihi mevcut planlar arasından dengeli, yüksek performans veya güç tasarrufu olabilir. Yüksek performans için adaptör aranır. Modern Standby veya üreticinin özel planları nedeniyle bulunmayan bir plan üretilmez. Mevcut plan önce yedeklenir, yeni plan yeniden okunur. Geri alırken sonradan yaptığın bir dış değişiklik korunur.

Seçilen **CS2 / VALORANT** için dengeli veya rekabetçi FPS görüntü tercihi ayrıca açılabilir. Oyun kapalı olmalı, desteklenen ayar dosyası daha önce oluşmuş olmalı. Mevcut oyun görüntü motoru dosyayı yedekler ve yalnız tanıdığı anahtarları değiştirir. GPU/ekran yenileme seçeneklerine ve CS2 başlatma satırına mevcut performans/oyun merkezinden ulaşılır. Ölçülmüş FPS artışı olmadan kazanç sayısı verilmez.

## Otomatik uygulama kapsamı: 21

Firefox, Google Chrome, VLC, Steam, Visual Studio Code, Git, Notepad++, OBS Studio, PowerShell 7, Audacity, WinMerge, KeePassXC, LocalSend, ScreenToGif, DBeaver Community, HeidiSQL, Thunderbird, Node.js LTS, Microsoft Edge, Brave, Microsoft PowerToys.

Her uygulama için derlenmiş sabit kurucu argümanları, ürün adı eşlemesi ve açık süreç koruması vardır. Kaynak sürümü üreticinin canlı metadata'sından okunur; uygun ürün/sürüm kurulumdan sonra yeniden aranır. Node.js LTS kendi ana sürüm hattıyla karşılaştırılır. Steam mevcutsa kendi güncelleyicisine bırakılır. Desteklenen uygulama sayısı, genel güncelleme kataloğunun 600 kaydıyla aynı şey değildir.

Yeni tarifler yalnız üreticinin imzalı EXE/MSI paketlerini kullanır. GitHub API erişimi kullanılamadığında aynı üreticinin halka açık kararlı sürüm sayfası ve sabit x64 varlık deseni okunabilir. Başka repo, önizleme paketi veya belirsiz kurucu seçilmez. Genel topluluk manifestlerindeki komutlar yönetici olarak çalıştırılmaz; WinGet/Scoop gerekmez. Güncel üretici metadata'sı alınamazsa çevrimiçi güncellik iddia edilmez.

USB kitine yeni uygulamaların üretici tarifi ve kurucusu da eklenir. İçe aktarım USB'nin yayıncı/alan adı kurallarına güvenmez: derlenmiş üretici kurallarıyla tekrar eşleştirir; dosya SHA-256 ve Windows imzasını doğrular. USB sürümü en güncel çevrimiçi sürüm olarak sunulmaz. Pakette olmayan kurucular ve Windows güncellemeleri internet gerektirir.

## Donanıma göre tarif

Model, kasa türü ve mevcut PCI/USB donanım kimlikleri tarifte gösterilir. Sıra: önce durum kaydı ve sürücü yedeği; varsa donanımla tam eşleşen imzalı USB sürücüleri; Windows'un uygun sürücü teklifleri; desteklenen üretici aracı; Windows ve bileşenler; uygulamalar; seçilen tercihler; son kontrol. Marka eşleşmesi tek başına sürücü uyumluluğu kanıtı sayılmaz.

Dell Command Update yalnız sürücü kategorisinde çalışır. HP Image Assistant yalnız belirlenen iş modeli ailelerinde denenir; HP Victus gibi tüketici modelleri markası nedeniyle bu akışa sokulmaz. Diğer markalarda Windows Update ve model özel resmi destek dizini kullanılır. BIOS/firmware, güvenlik kapatma ve zorla sürüm düşürme bu akışa dahil değildir.

## Ölçülen ve kullanıcıya bırakılan kontroller

- Ağ geçidi, DNS çözümleme ve Microsoft HTTPS metin yanıtı ve ayrı HTTP giriş sayfası testi ayrı sonuçlardır. Tek hedefe erişememe bütün internetin bozuk olduğunu kanıtlamaz.
- Grafik aygıtı sorun kodu, etkin sürücü sağlayıcısı/sürümü/imza kaydı ve gerçek ekran modları okunur. Görülen Hz, FPS ölçümü değildir.
- Core Audio üzerinden etkin çıkış/giriş uçları okunur. **Test sesini çal**, **Sesi duydum / Ses gelmedi** ile işitme testi kullanıcı tarafından kaydedilir. Mikrofon kaydı yapılmaz. OBS kodlayıcısı çalışıyor diye yalnız GPU markasından karar verilmez.
- Bluetooth donanımı ve aygıt sorunları okunur; eşleşme ve gerçek kullanım ayrıca denenmelidir.
- Uygulama kaydı/sürümü ve bulunabilen gerçek EXE kontrol edilir. EXE bulunamazsa uygulama otomatik başarılı sayılmaz; çalıştırma testi kullanıcıya bırakılır.
- CS2, VALORANT, Fortnite, GTA V, Minecraft ve Apex için kayıtlı Steam kütüphaneleri, Epic manifestleri, Riot kayıtları ve Minecraft klasörü/Store kaydı incelenir. Başka diskteki kayıtlı kütüphaneler dahildir. Oyunlar indirilmez, hesaplara giriş yapılmaz. VALORANT güvenlik bilgileri yalnız okunur; TPM sürümü/oyun kabulü ayrıca doğrulanır.

## Önce / sonra ve kurtarma

Başlangıç kaydı ile sürücü, Windows, uygulama, GPU aracı ve tercih adımlarının çevresindeki durumlar yerel olarak saklanır. Aygıt kaybı/yeni sorun kodu, bağlantı kaybı, ses çıkışı kaybı, sürücü ve uygulama sürüm değişimleri karşılaştırılır. Olası bir bozulma varsa kuyruk kontrol için durur; yeniden başlatma isteyen kurucu önce yeniden başlatma kontrol noktasına geçer. Bir adımın ardından görülmesi o adımın kesin neden olduğunu kanıtlamaz; örneğin USB çıkarılması da fark üretir.

**USB ve kurtarma** raporları, önceki hazırlamalar arasında seçim, önce/sonra farklarını, güç geri alma ve mevcut oyun görüntü geri alma ekranına geçişi içerir. Sürücüler için yalnız gerçekten dışa aktarılmış üçüncü taraf INF yedekleri veya Windows'ta mevcutsa Sürücüyü Geri Al kullanılabilir. Her sürücü/Windows güncellemesi için evrensel otomatik geri alma iddiası yoktur.

Eski 0.12 kayıtları yeni şemaya geçerken yeni işlemler için onay temizlenir. Tamamlanan uygulama/bileşen/yedek adımları korunur; eklenen adımlar yeniden inceleme gerektirir. Piksel karakter yalnız gerçek çalışan aşamaya gider; çalışma aracı, tamamlanma veya dikkat durumu gerçek iş kaydından beslenir. İlerleme, tamamlanan adım sayısıdır; indirme yüzdesi veya tahmini kalan süre değildir.

## Doğrulama sınırı ve birincil kaynaklar

Geliştirme bilgisayarında gerçek uygulama/sürücü/Windows kurulumu, güç/GPU değişikliği veya yeniden başlatma çalıştırılmadı. Sıra/durum, regresyonda durma, eski kayıt geçişi ve güç geri alma davranışları taklit arka uçlarla sınandı; kaynaklar ve seçili kurucular gerçek ağ/Windows imzasıyla kontrol edildi. Tam kurulum, son sürüm sessiz kurucu davranışı, farklı OEM'ler ve reboot devamı temiz Windows test cihazlarında ayrıca kabul testi gerektirir.

- [Windows sürücü seçimi](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process)
- [Windows NCSI HTTP testi](https://learn.microsoft.com/en-us/windows-server/networking/ncsi/ncsi-frequently-asked-questions) — HTTPS testi için ayrı Microsoft metin hedefi kullanılır.
- [Core Audio etkin uç taraması](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immdeviceenumerator)
- [MSI komutları](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/msiexec), [Inno Setup seçenekleri](https://jrsoftware.org/ishelp/topic_setupcmdline.htm), [NSIS seçenekleri](https://nsis.sourceforge.io/Docs/Chapter3.html)
- [OBS sistem gereksinimleri](https://obsproject.com/kb/system-requirements), [donanım kodlama](https://obsproject.com/kb/hardware-encoding)
- [VALORANT destek](https://support-valorant.riotgames.com/), [Riot Vanguard On-Demand](https://www.riotgames.com/en/news/vanguard-on-demand) — bu isteğe bağlı modun gereksinimleri bütün kullanıcılara genel zorunluluk olarak uygulanmaz.

