# FromZero 0.19.2

- Güncellemeler ekranı uygun eksikleri tek düğmeyle seçer. İndirilen Windows paketleri de kurulum kuyruğuna alınır.
- Seçilen Windows paketleri, sürücüler ve desteklenen kurucular indirilip kurulur. Sürücü kurulumu öncesi yedek alınır. Paket doğrulama, elle kurulması gereken paketler ve yeniden başlatma durumları sonuçta ayrı gösterilir.
- NVIDIA App'in yeni x64 indirme adresi desteklenir. Eski bağlantı biçimi de tanınır.
- FromZero açılışta yönetici yetkisi alır. PowerShell işlemleri uygulamanın yetkisini kullanır. Yeni kurulumun varsayılan yolu Program Files klasörüdür.

## 0.19.1

- Aktivasyon motoru bilgi bölümü kaldırıldı.
- Komutun sıfır çıkış kodu tek başına başarılı etkinleştirme sayılmaz. Windows ve yerel Office lisans durumu işlem öncesi ve sonrası karşılaştırılır; zaten etkin, doğrulanan, kısmen doğrulanan ve doğrulanamayan sonuçlar ayrılır. Windows eklenti lisansları ana sistem lisansı sayılmaz.
- Microsoft 365, Office LTSC 2024 / 2021, Office 2019 / 2016, Project ve Visio'nun x86 / x64 Click-to-Run, kayıtlı ve yerel kurulumları taranır. Word bulunmasa da Excel veya bağımsız Visio / Project algılanır. Kurulum sürümü ve mimarisi gösterilir.
- Microsoft 365 abonelik durumu, varsa Microsoft'un kurulu vNextDiag aracıyla salt okunur olarak alınır. Hesap e-postası ve kiracı kimliği özet kayıtlara eklenmez. Durum alınamıyorsa etkinleştirildi denmez.
- Ohook işlemi gerçek abonelik doğrulaması değildir; sonucu Office Hesap ekranında ayrıca kontrol edilmesi gereken işlem olarak gösterilir. Güncel Office algılaması tüm kurulumlarda etkinleştirme garantisi anlamına gelmez.

## 0.19.0

More tools menüsüne uygulama içi Activator ekranı eklendi. Windows, Office / Microsoft 365 ve Analiz sekmeleri uygulamanın İngilizce ve Türkçe dil tercihini kullanır.

- Windows ve Office lisans kayıtları, son beş anahtar karakteri, dakika cinsinden kalan lisans süresi ve seçili SPP yapılandırması okunur.
- Kurulu Windows lisansı ve Office toplu lisansı yerel Microsoft araçlarıyla yeniden etkinleştirilebilir.
- İsteğe bağlı HWID ve Ohook işlemleri MAS 3.12 motorunu belirli bir commit adresinden indirir, SHA-256 özetini doğrular ve çıktıyı uygulamada gösterir. Windows HWID işleminde sürüm değiştirme kapalıdır. Ohook kaldırma seçeneği bulunur.
- Simülasyon yalnızca işlem açıklamasını gösterir. Windows rearm ayrı onay isteyen gelişmiş bir bakım işlemidir.
- Komut tamamlandıktan sonra lisans durumu yeniden okunur. Office'in son durumu uygulamanın Dosya → Hesap ekranından da kontrol edilmelidir.

Kontroller gerçek lisans envanteri, komutların Windows PowerShell sözdizimi ve veri maskelemesini kapsar. Geliştirme bilgisayarında aktivasyon, Ohook kurulumu veya rearm işlemi çalıştırılmadı.

Microsoft ve MAS araçlarının yerel çıktıları işletim sisteminin veya aracın dilinde olabilir. Bu sürümün kurucusunda yayıncı Authenticode imzası bulunmaz.

## Kaynaklar

- [Microsoft Windows lisans komutları](https://learn.microsoft.com/en-us/windows-server/get-started/activation-slmgr-vbs-options)
- [Microsoft Office lisans araçları](https://learn.microsoft.com/en-us/office/volume-license-activation/tools-to-manage-volume-activation-of-office)
- [Microsoft 365 vNextDiag lisans tanılaması](https://learn.microsoft.com/en-us/microsoft-365-apps/licensing-activation/vnextdiag)
- [MAS 3.12 kaynak kodu ve GPL-3.0 lisansı](https://github.com/massgravel/Microsoft-Activation-Scripts/tree/6401d2f36975dc07bcb6d22ea823a8498a75ec01)
