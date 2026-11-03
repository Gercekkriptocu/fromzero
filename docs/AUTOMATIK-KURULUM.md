# FromZero 0.13 hazırlama akışı

Güncel profiller, 21 otomatik uygulama, 6 oyun, çalışırlık ve kurtarma: [0.13 ayrıntılı kılavuzu](HAZIRLIK-0.13.md).

## 0.13.1 başlangıç düzeltmesi

App.xaml otomatik StartupUri içermez. Normal uygulama ana pencereyi açıkça oluşturur; worker ve salt okunur worker-check ayrı penceresiz başlangıç yollarıdır. WPF boş StartupUri atamasını reddettiği için önceki atamalar açılış çökmesi üretiyordu. Hata kaydına erken istisnanın asıl nedeni yazılır; her süreç hatası yönetici yetkisine bağlanmaz. Yükseltilmiş uygulama çocuğa mevcut yetkisini aktarır; standart uygulama runas kullanır. worker-check seçeneği kurulum motorunu, HKCU Run devam kaydını ve kurucuları çalıştırmaz.

Kaynak: [Microsoft StartupUri — ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.startupuri).

## Kurulum motoru

`PreparationPolicy` seçeneklerden sabit sıralı adımlar üretir. Kalıcı kayıt yalnız profil, bilinen paket kimlikleri, durum ve sonuç içerir; serbest yürütülebilir yol/argüman/PowerShell metni kabul edilmez. Yönetici çalışanı kullanıcı SID'sini, bilgisayarı, uygulama konumunu ve adım sırasını tekrar doğrular. Aynı kullanıcı için iki hazırlama çalışanı paralel kurulum yapamaz.

`PreparationEngine` yazma işlemlerini taklit eden arka uçla test edilebilir. Her geçiş atomik kaydedilir. Başarısız bağımsız işler devam eder; yedek başarısızlığı sürücü adımlarını kapatır. Kurulumlar çalışan süreç öldürülerek iptal edilmez. Yeniden başlatma durumunda boot zamanına göre aynı Windows oturumunda tekrar kurulum engellenir. Oturum açılışı HKCU Run girdisi yalnız FromZero'yu aynı plan kimliğiyle açar; UAC atlanmaz. Başarılı tamamlanmada girdi silinir.

## Paket güveni ve doğrulama

Mevcut HTTPS/izinli alan adı, Windows imza/yayıncı ve varsa üretici hash kontrolleri kullanılır. Kurucular çalıştırılmadan önce ProgramData içindeki yöneticiler/SYSTEM erişimli hazırlama klasörüne kopyalanır; hash ve imza tekrar doğrulanır, kurucu dosyası yazmaya kapalı tutulur. Genel topluluk manifestleri otomatik yönetici kurulumuna alınmaz. VLC hedef sürümü VideoLAN'dan, kurucusu sabit izinli Verinomi VideoLAN aynasından alınır; beklenen imza VideoLAN'dır. Rastgele seçilen CDN/mirror alan adları otomatik kabul edilmez.

Firefox, Chrome ve VS Code hedef sürümleri üretici kayıtlarından; VLC hedefi resmi sürüm metninden okunur. Kurulu sürüm aynı veya daha yeniyse korunur. Yeni kurulum sonrası ürün/sürüm kaydı yeniden taranır. Steam mevcutsa istemci kendi güncellemelerini yönetir. Açık uygulamaları zorla kapatmak yerine ilgili adımda kontrol istenir. Visual C++ v14 kurucusunun hedef sürümü dosyadan okunur. DirectX tam paketi korumalı klasöre çıkartılır; Microsoft imzalı DXSETUP /silent kullanılır. Son dosya/uygulama/cihaz taraması ayrıca saklanır.

## Kaynak önceliği ve sınırlar

USB'deki mevcut donanımla tam kimlik eşleşen katalog imzalı INF'ler önce ağ sınıfından başlanarak PnPUtil ile yüklenir. Windows sürücü seçimini ve katalog kapsamındaki dosyaları ayrıca doğrular; zorla sürüm düşürme yapılmaz. BIOS/firmware INF'leri hariçtir. Windows Update sürücüler ve yazılım için ayrı çalışır; firmware, önizleme, özellik yükseltmesi ve etkileşim isteyen paketler otomatik seçilmez. Kurulum kimlikleri tekrar sorgulanır ve kod/yeniden başlatma sonucu raporlanır. Her aşama en fazla üç tarama, tüm iş en fazla dört yeniden başlatma turudur.

Dell: resmi Command Update kurucusu ve gerekirse Microsoft .NET Desktop 10 ön koşulu; CLI /applyUpdates -updateType=driver -silent -reboot=disable. HP: yalnız bilinen iş modeli ailelerinde HPIA /Category:Drivers /Action:Install; diğer modeller Windows/USB kaynağına devam eder. OEM CLI kodları donanımda kabul testine ihtiyaç duyar; 3011, destek dışı OS/model ve indirme hataları başarı sayılmaz. Diğer markalar için otomatik genel kaynak Windows Update'tir. GPU yardımcı kurucuları etkileşimli olabilir ve uygulama varlığı sürücü güncelliği yerine geçmez.

## Doğrulama sınırı

Geliştirme kontrolleri sıra/durum, hata/iptal, yedek kapısı, kimlik/komut reddi, yeniden başlatma devamı, dosya yolu sınırları, gerçek indirme/imza ve üretici sürüm kayıtlarını kapsar. Windows PowerShell kurulum metinleri yerel ayrıştırıcıyla çalıştırılmadan kontrol edilir. Kullanıcının Windows/sürücü/uygulama kurulumları ve yeniden başlatması bu doğrulamada yapılmaz. Dağıtım kabulü için temiz Windows 10/11 x64, birden fazla GPU/OEM, internet kesilmesi ve en az bir gerçek reboot senaryosu ayrıca denenmelidir.

## Birincil teknik kaynaklar

- [Windows sürücü eşleşmesi ve sıralaması](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/how-windows-selects-a-driver-for-a-device)
- [Windows Update Agent API](https://learn.microsoft.com/en-us/windows/win32/api/_wua/)
- [PnPUtil seçenekleri](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax) ve [sonuç kodları](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-return-values)
- [MSI sessiz kurulum ve yeniden başlatma seçenekleri](https://learn.microsoft.com/en-us/windows/win32/msi/standard-installer-command-line-options)
- [Firefox tam kurucu seçenekleri](https://firefox-source-docs.mozilla.org/browser/installer/windows/installer/FullConfig.html)
- [VS Code Windows kurulum türleri](https://code.visualstudio.com/docs/setup/windows)
- [DirectX ek bileşenleri ve sessiz kurulum](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/directx-setup-for-game-developers)
- [Dell Command Update 5.x CLI](https://www.dell.com/support/manuals/en-us/command-update/dcu_rg/dell-command-update-cli-commands?guid=guid-92619086-5f7c-4a05-bce2-0d560c15e8ed&lang=en-us)
- [Dell 5.7.1 ve .NET ön koşulu](https://www.dell.com/support/home/en-gb/drivers/driversdetails?driverid=61r17)
- [HP HPIA kılavuzu](https://ftp.ext.hp.com/pub/caps-softpaq/cmit/whitepapers/HPIAUserGuide.pdf)
- [HP'nin yalnız sürücü yükleme örneği](https://jp.ext.hp.com/blog/security/product/microsoft-endpoint-manager-intune-hp-image-assistant-driver-update/)


