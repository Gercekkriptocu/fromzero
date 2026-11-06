# FromZero 0.20.1

- Güncelleme satırları cihazın veya uygulamanın adını gösterir. Kategori adı ikinci satırdadır. Ekran kartı ve ses sürücüsünün hangi cihaza ait olduğu görülebilir.
- Kullanıcı girdisi isteyebilir işaretli sürücüler artık atlanmaz: Windows Update sessiz kurulumla indirme ve kurulumu dener. Başarısız sürücüler için aynı bilgisayara, aynı güncelleme kimliğiyle indirilen WUA önbelleği kullanılır; donanım, tam sürüm, mimari ve imzalı INF/katalog kontrolünden sonra PnPUtil kurulumu denenir. Windows'un etkinleştirdiği gerçek sürüm doğrulanır.
- SWC ses işleme bileşeni kimlikleri desteklenir. Büyük ekran kartı CAB paketleri için boyut ve disk alanı kontrolleri genişletildi.
- Visual Studio Code, Vivaldi, Zed, Zoom ve doğrulanmış MSI paketlerinin sessiz kurulum desteği eklendi. Kurulum kapsamı korunur.
- OBS kod 6 sonucu kullanılan dosyalar olarak açıklanır. Sonuçlar büyük bir açılır pencere yerine güncellemeler ekranında gösterilir.

## 0.20.0

- Donanıma özel resmi Microsoft Update Catalog araması eklendi. Donanım kimliği, mimari, sürüm ve paket kaynağı gösterilir.
- Katalog paketleri resmi SHA-256 kaydıyla indirilir. CAB arşivi ve çıkarılan INF/katalog dosyaları incelenir. İncelenen uygun sürücüler mevcut indirme/kurulum kuyruğuna eklenir.
- Kurulumdan önce yedek alınır ve cihaz/paket yeniden kontrol edilir. Etkin sürüm, Windows'un koruduğu sürücü ve yeniden başlatma sonuçları ayrılır.
- Güncelleme taraması ortada FromZero renkleriyle hareket eden özgün piksel radar gösterir. Tarama mesajı ve durdurma düğmesi aynı ekrandadır.
- Güncelleme sonuçları tek listede kurulu/bulunan sürüm, eski/eksik/güncel ayrımı, seçim kutuları, arama ve kategori filtreleriyle gösterilir.
- Türkçe Windows kültüründe küçük harfli donanım kimliklerinin eşleşmesini engelleyen bir karşılaştırma hatası düzeltildi.

Driver Booster incelemesinin kanıtları, kapsamı ve çözülemeyen sunucu ayrıntıları [REA raporunda](https://github.com/Gercekkriptocu/fromzero/blob/main/docs/DRIVER-BOOSTER-REA.md) bulunur. Bu sürüm IObit'in tüm sürücü kataloğunun birebir kopyasını içermez.
