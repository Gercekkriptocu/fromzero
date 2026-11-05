# FromZero 0.19.0

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
- [MAS 3.12 kaynak kodu ve GPL-3.0 lisansı](https://github.com/massgravel/Microsoft-Activation-Scripts/tree/6401d2f36975dc07bcb6d22ea823a8498a75ec01)
