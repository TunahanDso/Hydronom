# 🌊 Hydronom

> **Modüler, genişleyebilir ve yaşayan bir otonom deniz aracı sistemi**  
> Algılama • Analiz • Karar • Görev • Güvenlik • Telemetri

**Hydronom**, otonom deniz araçları için geliştirilen modüler, genişleyebilir ve gerçek dünyaya uyarlanabilir bir kontrol, algılama, analiz ve görev yönetim sistemidir.  
Bu proje yalnızca bir yazılım yığını değildir; aynı zamanda bir mühendislik hayalinin, uzun süren bir arayışın, defalarca bozup yeniden kurmanın, anlamaya çalışmanın, denemenin, hata yapmanın ve yeniden ayağa kalkmanın sonucudur.

Hydronom’un amacı; bir deniz aracını sadece hareket ettiren bir sistem kurmak değil, **algılayan, yorumlayan, karar veren, görev yürüten, kendi durumunu takip eden ve gerektiğinde kendini güvenli şekilde sınırlayabilen bir otonom yapı** oluşturmaktır.

Bu repo, o yapının yaşayan çekirdeğini temsil eder. ⚓


---

## 📊 Language Statistics

| Language | Files | Code | Comment | Blank | Total |
|---|---:|---:|---:|---:|---:|
| C# | 117 | 13,172 | 3,201 | 2,994 | 19,367 |
| Python | 89 | 8,541 | 2,499 | 2,120 | 13,160 |
| JSON | 29 | 5,970 | 0 | 31 | 6,001 |
| TypeScript JSX | 25 | 4,335 | 37 | 599 | 4,971 |
| TypeScript | 29 | 3,258 | 77 | 397 | 3,732 |
| JavaScript | 3 | 1,594 | 213 | 298 | 2,105 |
| HTML | 3 | 513 | 27 | 48 | 588 |
| PostCSS | 2 | 306 | 15 | 47 | 368 |
| C | 1 | 168 | 57 | 49 | 274 |
| C++ | 1 | 130 | 65 | 44 | 239 |
| XML | 6 | 67 | 7 | 22 | 96 |
| YAML | 1 | 54 | 21 | 11 | 86 |
| JSON with Comments | 1 | 23 | 0 | 0 | 23 |

> Toplamda Hydronom; **C#**, **Python**, **JSON**, **TypeScript** ve yardımcı teknolojilerden oluşan çok katmanlı bir mühendislik ekosistemidir. Bu dağılım, projenin yalnızca tek bir uygulama değil; runtime, AI, gateway, frontend, config ve düşük seviye deneysel bileşenleri kapsayan geniş bir sistem olduğunu gösterir.


---

## 📚 İçindekiler

- [🌌 Projenin Hikâyesi](#-projenin-hikâyesi)
- [🎯 Amaç](#-amaç)
- [🧭 Temel Yaklaşım](#-temel-yaklaşım)
- [🏗️ Mimari Genel Bakış](#️-mimari-genel-bakış)
- [🧩 Temel Bileşenler](#-temel-bileşenler)
- [✨ Öne Çıkan Yetenekler](#-öne-çıkan-yetenekler)
- [🔄 Sistem Akışı](#-sistem-akışı)
- [🚤 Hedeflenen Kullanım Alanları](#-hedeflenen-kullanım-alanları)
- [🔮 Gelecek Vizyonu](#-gelecek-vizyonu)
- [🌐 Yakında: Hydronom Filo ve Yer İstasyonu Mimarisi](#-yakında-hydronom-filo-ve-yer-istasyonu-mimarisi)
- [💙 Neden Özel?](#-neden-özel)
- [🛠️ Kurulum Mantığı](#️-kurulum-mantığı)
- [📐 Geliştirme Felsefesi](#-geliştirme-felsefesi)
- [📍 Durum](#-durum)
- [🤝 Teşekkür ve Kapanış](#-teşekkür-ve-kapanış)
- [📜 Lisans](#-lisans)

---

## 🌌 Projenin Hikâyesi

Bazı projeler bir ihtiyaçtan doğar.  
Bazıları bir yarışma için yazılır.  
Bazıları ise insanın içinde bir türlü susmayan bir cümleden çıkar:

> **“Ben bunu gerçekten sıfırdan anlayarak kurmak istiyorum.”**

Hydronom biraz böyle başladı.

Bir teknenin ileri gitmesi tek başına etkileyici değildir.  
Bir motoru döndürmek, bir ESC’yi sürmek, sensörden veri okumak, ekranda birkaç sayı göstermek... bunların her biri tek başına yapılabilir şeylerdir. Ama mesele bunları yan yana koymak değildir. Mesele, bunların arasındaki görünmeyen bağı kurmaktır.

Bir araç suyun üzerinde ya da altında hareket ederken;

- 📍 nerede olduğunu,
- ⚙️ ne yaptığını,
- 👀 neye yaklaştığını,
- 🎯 neyi hedeflediğini,
- ❤️ ne kadar sağlıklı çalıştığını,
- 🛑 ne zaman durması gerektiğini

anlayabilsin istendi.

İşte Hydronom, tam olarak bu ihtiyaçtan büyüdü.

Bu repo’nun içinde sadece kod yok.  
Burada geceleri süren denemeler, çalışmayan bağlantılar, saçma hatalar, bozuk paketler, beklenmedik sensör davranışları, sıfır çıkan veriler, anlamsız ofsetler, yeniden çizilen mimariler ve her şeye rağmen devam etme iradesi var.

Bu yüzden Hydronom benim için sıradan bir proje değil.  
Bu sistem, bir gün çok daha büyük şeyler kurabilecek bir aklın ilk ciddi iskeletlerinden biri. 🌱

---

## 🎯 Amaç

Hydronom’un temel amacı şudur:

> **Otonom deniz araçları için güvenilir, modüler, platformdan bağımsız ve geliştirilebilir bir çalışma altyapısı oluşturmak.**

Bu amaç doğrultusunda sistem şu problemleri çözmeye odaklanır:

- 📡 sensörlerden veri toplamak,
- 🧼 verileri normalize etmek,
- 🧠 çoklu kaynaktan anlamlı bir durum üretmek,
- 📋 görev mantığını işletmek,
- ⚖️ karar modülünü beslemek,
- ⚙️ aktüatörleri kontrollü şekilde sürmek,
- 🛡️ güvenlik sınırlarını uygulamak,
- 📤 telemetri ve sağlık durumunu dış sistemlere aktarmak,
- 🤖 yapay zekâ destekli planlama ve yorumlama katmanlarıyla bütünleşmek.

Hydronom yalnızca “tekne kodu” değildir.  
Doğru mimari ile bu yapı;

- 🚤 su üstü araçları,
- 🌊 su altı araçları,
- ⛵ yelkenli platformlar,
- 🔁 hibrit görev araçları,
- 🤖 hatta gelecekte farklı mobil robotik sistemler

için de temel olabilir.

---

## 🧭 Temel Yaklaşım

Hydronom tasarlanırken temel olarak şu prensiplere bağlı kalınmıştır:

### 1. 🧩 Modülerlik

Sistemin her parçası, mümkün olduğunca diğer parçaları minimum bağımlılıkla kullanacak şekilde ayrıştırılır.  
Sensör, analiz, görev, karar, telemetri, güvenlik ve aktüasyon katmanları birbirine gömülü değil; konuşabilen ama ayrılabilen yapıdadır.

### 2. 🌍 Gerçek Dünyaya Uygunluk

Simülasyonda çalışan ama sahada çöken bir sistem yeterli değildir.  
Hydronom’un hedefi yalnızca demo üretmek değil, **gerçek donanım ve gerçek belirsizliklerle başa çıkabilen bir sistem** olmaktır.

### 3. 👁️ Gözlemlenebilirlik

Bir sistem ne yaptığını göstermiyorsa ona güvenmek zordur.  
Bu yüzden log, telemetri, health bilgisi, olay akışı ve iç durum görünürlüğü sistemin asli parçalarıdır.

### 4. 🛡️ Güvenlik

Her hareket yapılabildiği için yapılmamalıdır.  
Hydronom’da emniyet sınırları, veri tazeliği, stall kontrolleri, limiter mantıkları ve güvenli duruş yaklaşımı yalnızca ek özellik değil, çekirdek gerekliliktir.

### 5. 🚀 Genişleyebilirlik

Bugün basit bir görev, yarın çok araçlı koordinasyon olabilir.  
Bugün kural tabanlı karar, yarın AI destekli görev planlama olabilir.  
Mimari, bu evrimi taşıyacak kadar esnek düşünülmüştür.

---

## 🏗️ Mimari Genel Bakış

Hydronom genel olarak katmanlı bir mimari mantığıyla ele alınır.

### 🧠 Çekirdek Mantık

Çekirdek katmanda;

- veri tipleri,
- domain modelleri,
- temel sözleşmeler,
- fiziksel durum temsil yapıları,
- kuvvet / tork mantıkları,
- görev ve karar arayüzleri

yer alır.

Bu katman sistemin **ne olduğu** ile ilgilenir.

### ⚙️ Runtime Katmanı

Runtime katmanı;

- sensör akışını alır,
- frame’leri işler,
- karar mantığını çalıştırır,
- görevleri yürütür,
- aktüatör komutları üretir,
- telemetri yayınlar

ve sistemin canlı çalışma döngüsünü taşır.

Bu katman sistemin **nasıl çalıştığı** ile ilgilenir.

### 🔌 I/O ve Haberleşme

Hydronom, dış dünyayla veri alışverişi yapabilmek için TCP/JSON, NDJSON, telemetri yayınları ve farklı giriş kaynakları ile haberleşebilir.  
Bu, sistemi hem lokal testte hem de dağıtık mimaride daha kullanılabilir hale getirir.

### 📊 Analiz ve Durum Değerlendirme

Araç sadece veri almakla kalmaz; bu veriyi yorumlayarak şu sorulara cevap üretecek bir analitik yaklaşımı hedefler:

- 🚧 engel var mı,
- 📏 hedefe uzaklık ne,
- 🧭 rota sapması var mı,
- ⚠️ risk seviyesi yükseliyor mu,
- ❤️ sistem sağlıklı mı?

### 🤖 Gelecekteki AI Katmanı

Hydronom’un vizyonunda yapay zekâ katmanı doğrudan kontrol otoritesi olarak değil, önce **öneri, planlama, yorumlama ve yeniden planlama** katmanı olarak düşünülür.  
Son söz her zaman güvenlik sınırları içindeki runtime otoritesinde kalır.

---

## 🧩 Temel Bileşenler

Hydronom birçok alt modülden oluşur. Repo yapısına göre adlar değişebilse de temel sistem mantığı aşağıdaki başlıklar etrafında döner.

### 📡 Sensör Katmanı

Araçtan veya dış sistemlerden gelen verilerin toplanmasıyla ilgilenir.

Örnek veri kaynakları:

- IMU
- GPS
- LiDAR
- dış pose sağlayıcılar
- twin / sim verileri
- health / power verileri
- özel telemetri akışları

Bu katmanın görevi yalnızca veri okumak değil; veriyi kullanılabilir forma getirmektir.

### 🧠 Fusion / State Estimation

Farklı sensörlerden gelen veriler her zaman doğrudan kullanılabilir olmaz.  
Zaman farkı, gürültü, ofset, kopma, drift ve uyumsuz eksenler gibi problemler vardır.

Bu yüzden sistem:

- sensörleri birleştirir,
- ortak zaman mantığı kurar,
- durum tahmini üretir,
- gerekirse external state ile kendi iç tahminini harmanlar.

### 🔎 Analysis

Analysis katmanı aracın etrafını ve iç durumunu değerlendiren yorum katmanıdır.

Örnek değerlendirmeler:

- ön tarafta engel var mı,
- görev hattından sapma var mı,
- sensör tazeliği düştü mü,
- riskli hareket oluşuyor mu,
- güç veya sağlık problemi başlıyor mu?

Bu bölüm gelecekte çok daha ileri düzey çevresel ve power / health analizleriyle büyüyecek şekilde düşünülmüştür.

### ⚖️ Decision

Decision katmanı, analiz çıktıları ve görev hedeflerine göre davranış seçimi yapar.

Örnek davranışlar:

- rota takip et,
- yavaşla,
- dur,
- kaçın,
- manuel komut önceliklendir,
- fail-safe moduna geç.

Bu katman tek başına “zekâ” değildir; ama sistem davranışının çekirdeğidir.

### 🗺️ Task / Mission

Görev sistemi, daha yüksek seviyeli akışı yönetir.

Örnek görevler:

- belirli waypoint’leri sırayla takip et,
- bir bölgeyi tara,
- hedefe git ve bekle,
- manuel devralmayı bekle,
- görev sırasında belirli bir koşul oluşursa yeni adıma geç.

Bu yapı, ileride daha karmaşık FSM / Behavior Tree hibrit görev sistemlerine evrilebilecek şekilde düşünülmüştür.

### ⚙️ Actuation

Karar katmanının ürettiği hareket istekleri doğrudan motora gitmez.  
Önce:

- limitlenir,
- geometriye göre dağıtılır,
- thrust / mixer mantığına göre çevrilir,
- güvenlik koşullarından geçirilir.

Böylece araç komutları daha kontrollü hale gelir.

### 📤 Telemetry / Feedback

Sistemin iç dünyasını dışarı aktarır.

Örnek bilgiler:

- 📍 konum
- 🧭 yönelim
- 💨 hız
- 🎯 hedef bilgisi
- 🚧 engeller
- ❤️ sensör sağlık bilgisi
- 🔩 kuvvet / tork verileri
- 📝 olay kayıtları

Bu, operatör arayüzleri ve hata ayıklama için kritik önemdedir.

---

## ✨ Öne Çıkan Yetenekler

Hydronom’un güçlü taraflarından bazıları şunlardır:

- 🧩 Modüler sensör entegrasyonu
- 🔁 Sim / gerçek / hibrit çalışma yaklaşımına uygun yapı
- 📡 Çok katmanlı telemetri ve durum görünürlüğü
- 🛡️ Güvenlik odaklı limiter ve fail-safe mantığı
- ⚙️ Geometri tabanlı thrust / mixer yaklaşımı
- 🧠 Görev ve karar katmanlarının ayrıştırılması
- 🔌 Dış sistemlerle veri alışverişine uygun haberleşme altyapısı
- 📈 Genişletilebilir analiz modülü
- 🤖 AI destekli planlama için hazırlanmış altyapı
- 🌐 Gelecekte çoklu araç ve daha büyük operasyon sistemlerine dönüşebilecek temel mimari

---

## 🔄 Sistem Akışı

Hydronom’un genel akışı kabaca şu şekildedir:

1. **📥 Sensör verisi gelir**  
   IMU, GPS, LiDAR veya dış kaynaklardan ham bilgi alınır.

2. **🧼 Veri normalize edilir**  
   Zaman, birim, eksen ve kalite bilgileri düzenlenir.

3. **🧠 Durum üretilir**  
   Araç için anlamlı bir fused state oluşturulur.

4. **🔎 Analiz yapılır**  
   Çevre, hedef, risk ve sağlık değerlendirilir.

5. **🗺️ Görev durumu okunur**  
   Araç o anda hangi görev adımındadır, neyi başarmaya çalışmaktadır?

6. **⚖️ Karar verilir**  
   Devam et, yönel, kaçın, yavaşla, dur, bekle gibi davranış tercihleri oluşur.

7. **🛡️ Komut sınırlandırılır**  
   Güvenli hareket limitleri uygulanır.

8. **⚙️ Aktüatör komutu üretilir**  
   Motor / thruster seviyesinde uygulanabilir komutlara dönüştürülür.

9. **📤 Telemetri ve olay kaydı yayınlanır**  
   Sistem kendi yaptığını görünür kılar.

Bu akış basit görünse de, gerçek mühendislik değeri bu adımlar arasındaki bağların sağlam kurulmasından gelir.

---

## 🚤 Hedeflenen Kullanım Alanları

Hydronom aşağıdaki alanlarda kullanılabilecek bir omurga olmayı hedefler:

- 🎓 öğrenci takımı otonom deniz araçları
- 🏁 yarışma araçları
- 🗺️ görev tabanlı yüzey araçları
- 🌊 su altı keşif / test platformları
- 📡 kıyı gözlem sistemleri
- 🔬 araştırma amaçlı robotik deniz platformları
- 🧪 modüler robotik deney ortamları
- 🤖 AI destekli otonom görev planlama araştırmaları

---

## 🔮 Gelecek Vizyonu

Hydronom şu anki haliyle bir son ürün değil.  
Aksine, daha büyük bir dünyanın temelidir.

Gelecekte hedeflenen bazı yönler:

- 🧠 daha güçlü state estimation
- 🗺️ gelişmiş mapping ve çevresel farkındalık
- 🔋 power / health analizi
- 📋 daha sofistike görev motoru
- 🤝 çoklu araç koordinasyonu
- ⏺️ kayıt / yeniden oynatma altyapısı
- 🧪 daha iyi simülasyon gerçekliği
- 🌐 web-first operasyon arayüzleri
- 🤖 yapay zekâ ile görev planlama ve yeniden planlama
- 🧭 platform bağımsız otonomi çekirdeği
- 🚀 deniz araçlarından öteye taşınabilen ortak otonom runtime mantığı

Kısacası Hydronom’un geleceği yalnızca “bir tekne yazılımı” olmak değil;  
**modüler otonominin yaşayan bir çekirdeği** haline gelmektir.

---

## 🌐 Yakında: Hydronom Filo ve Yer İstasyonu Mimarisi

Hydronom, tek bir otonom araç runtime’ı olmanın ötesine geçerek; birden fazla aracın birlikte çalışabildiği, haberleşebildiği, görev paylaşabildiği ve yer istasyonu üzerinden izlenip koordine edilebildiği daha büyük bir otonom operasyon mimarisine doğru genişlemektedir.

Planlanan **Filo ve Yer İstasyonu Mimarisi**, Hydronom’un mevcut modüler yapısını koruyarak sisteme şu yeni kabiliyetleri kazandırmayı hedefler:

- 🤝 çoklu araç farkındalığı ve koordinasyonu
- 📡 Hydronom sistemleri arası haberleşme
- 🔌 tak-çalıştır haberleşme teknolojileri
- 📊 adaptif telemetri profilleri
- 🖥️ yer istasyonu tabanlı filo izleme ve analiz
- 🧭 engeller, hedefler, görevler ve araç sağlığı için ortak dünya modeli
- 🛡️ yer istasyonu ile araçlar arasında güvenlik kapısından geçirilen komut ve kontrol yapısı

Bu mimaride her Hydronom aracı, kendi başına bağımsız otonom çalışabilen bir düğüm olarak kalır.  
Yer istasyonu ise tüm araçları görebilen, uzun telemetri mesajlarını işleyebilen, filo durumunu analiz edebilen, görev koordinasyonu sağlayabilen ve gerektiğinde operatör komutlarını güvenli şekilde araçlara aktarabilen üst seviye operasyon merkezi olarak konumlanır.

Haberleşme tarafında temel hedef, Wi-Fi, RF, LoRa, Ethernet, seri haberleşme, mesh veya gelecekte eklenecek farklı bağlantı teknolojilerinin üst seviye Hydronom mimarisini bozmadan sisteme dahil edilebilmesidir.  
Yani Hydronom mesajı üretir; uygun haberleşme kanalı mesajı taşır; üst seviye sistem ise mesajın hangi teknolojiyle geldiğini bilmek zorunda kalmadan çalışmaya devam eder.

Bu yaklaşım sayesinde Hydronom, yalnızca tek bir aracı kontrol eden bir sistem olmaktan çıkıp; birden fazla otonom aracın birlikte görev yapabildiği, veri paylaşabildiği ve ortak operasyon yürütebildiği bir filo mimarisine doğru evrilecektir.

> **Hydronom artık yalnızca bir aracı kontrol etmeyecek; otonom araçların birlikte çalışabildiği bir operasyon ekosistemini koordine edecektir.**

---

## 💙 Neden Özel?

Çünkü bu sistem hazır bir iskeletin üstüne alelacele yerleştirilmiş bir katman değil.

Bu proje:

- düşünülmüş,
- defalarca sorgulanmış,
- bazen baştan kurulmuş,
- bazen aynı problem günlerce taşınmış,
- bazen çok küçük bir ilerleme için saatler verilmiş,
- ama sonunda yavaş yavaş kendi karakterini kazanmış bir sistemdir.

Hydronom’un özel tarafı yalnızca teknik yapısı değil, **niyetidir**.

Buradaki niyet şudur:

> **“Bir şey çalışsın diye değil, gerçekten doğru bir temel oluşsun diye uğraşmak.”**

Bu bazen daha yavaş ilerletir.  
Ama uzun vadede gerçek sistemleri böyle kurarsın.

---

## 🛠️ Kurulum Mantığı

Bu repo’nun tam kurulum adımları proje yapısına göre değişebilir.  
Ama genel mantık şu şekildedir:

### 📦 Gereksinimler

Projeye göre aşağıdakilerden bazıları gerekir:

- .NET SDK
- Python
- ilgili Python paketleri
- seri port / sensör sürücüleri
- gerekirse simülasyon verileri
- donanım tarafında ESC, motor, IMU, LiDAR, GPS, MCU veya ilgili arayüzler

### ▶️ Genel Çalıştırma Mantığı

- önce bağımlılıklar kurulur,
- sensör / sim kaynakları hazırlanır,
- runtime başlatılır,
- fusion / telemetri akışı doğrulanır,
- gateway veya viewer bağlanır,
- sonra görev / komut akışı denenir.

### 🧑‍💻 Geliştirici İçin Not

Hydronom, tek dosyada anlaşılacak bir sistem değildir.  
Bu yüzden projeyi incelerken en sağlıklı yaklaşım:

- önce domain modellerini,
- sonra veri akışını,
- ardından runtime döngüsünü,
- en son görev / karar / aktüasyon ilişkisini

incelemektir.

Bu sistem “ezberlenerek” değil, katman katman sindirilerek anlaşılır.

---

## 📐 Geliştirme Felsefesi

Hydronom geliştirilirken bazı temel anlayışlar ön planda tutuldu:

- 🧱 hızlı hack yerine sürdürülebilir mimari
- 🚤 yalnızca demo değil, sahaya çıkabilecek mantık
- 👁️ görünmeyen hataları ortaya çıkaracak gözlemlenebilirlik
- 🌱 tek bir kullanım senaryosu yerine genişleyebilir tasarım
- 🛡️ emniyeti sonradan eklenen parça değil, baştan gelen ilke olarak görmek
- ⏳ gerekirse yavaş ama sağlam ilerlemek

Bu proje kusursuz değildir.  
Eksikleri vardır, gelişmeye açıktır, bazı kısımları hâlâ dönüşmektedir.  
Ama önemli olan tam da budur: Hydronom donmuş bir yapı değil, yaşayan bir mühendislik organizmasıdır.

---

## 📍 Durum

Hydronom aktif olarak gelişen bir projedir.

Şu an sistem;

- ✅ temel mimari iskeletini kurmuş,
- ✅ sensör ve veri akışı mantığını şekillendirmiş,
- ✅ karar / görev / telemetri / gateway düşüncesini yerleştirmiş,
- 🔄 gerçek donanım ve simülasyon arasında köprü kurabilecek bir noktaya yaklaşmış,
- 🚀 daha büyük operasyon araçlarının temelini atmış

durumdadır.

Henüz yapılacak çok şey vardır.  
Ama artık ortada sadece fikir yoktur.  
Artık bir omurga vardır.

---

## 🤝 Teşekkür ve Kapanış

Hydronom benim için yalnızca bir repo değil.

Bu proje bazen yorucuydu.  
Bazen aynı yerde dönüp duruyor gibi hissettirdi.  
Bazen küçücük bir veri akışının çalışması bile gereğinden fazla zor geldi.  
Bazen “neden bu kadar uğraşıyorum?” sorusu da geçti içimden.

Ama sonra şunu fark ettim:

> İnsan bazen yaptığı şeyin bugünkü haline değil, onda gördüğü geleceğe emek verir.

Hydronom da biraz öyle.

Belki şu an eksik.  
Belki daha yolun çok başında.  
Belki bazı parçaları hâlâ kırılgan.  
Ama içinde gerçek bir yön var.  
Gerçek bir karakter var.  
Ve en önemlisi, devam etme iradesi var.

Bu repo’yu açan herkes yalnızca koda değil,  
aynı zamanda bir sistem kurma isteğine,  
bir mühendislik inadına  
ve yarım bırakılmamış bir hayale de bakıyor.

**Eğer buradaysan, hoş geldin.**  
**Hydronom daha bitmedi.**  
**Belki aslında daha yeni başlıyor.** 🌊✨

---

## 📜 Lisans ve Kullanım Hakları

© 2026 Tunahan Delisalihoğlu. Tüm hakları saklıdır.

Bu projenin tüm fikri ve sınai mülkiyet hakları Tunahan Delisalihoğlu’na aittir.

### 🔒 Kullanım İzni
Bu yazılımın kullanım, geliştirme, erişim ve yaygınlaştırma hakkı **münhasıran Yıldız Teknik Üniversitesi Stars of Hydro takımı üyelerine** aittir.  
Bu kapsam dışındaki tüm kullanım biçimleri, geliştiricinin açık ve yazılı iznine tabidir.

### ⚠️ Kısıtlamalar
Aşağıdaki eylemler, yazılı izin olmaksızın **kesin olarak yasaktır**:

- Projenin tamamının veya herhangi bir bölümünün kopyalanması  
- Kaynak kodun veya türevlerinin paylaşılması veya dağıtılması  
- Ticari veya yarışma amaçlı kullanımı  
- Tersine mühendislik, yeniden paketleme veya başka projelere entegrasyon  
- Üçüncü kişilerle doğrudan veya dolaylı paylaşım  

### 🧠 Türetilmiş Çalışmalar
Bu projeden türetilen her türlü çalışma:
- Açık ve görünür şekilde kaynak atfı içermelidir  
- Geliştiricinin önceden yazılı onayına tabidir  

### ⚙️ Sorumluluk Reddi
Bu yazılım **“olduğu gibi” (as-is)** sunulmaktadır.  
Yazılımın kullanımı sonucunda oluşabilecek:

- Donanım hasarları  
- Sistem hataları  
- Veri kaybı  
- Operasyonel riskler  

konularında geliştirici hiçbir şekilde sorumlu tutulamaz.

### 📩 İletişim
İzin talepleri ve lisanslama konuları için:  
**tdelisalihoglu@outlook.com.tr**

---

> ⚠️ Not: Bu depo, Hydronom sisteminin tamamını içermeyebilir.  
> Daha gelişmiş ve güncel modüller yalnızca ekip içi kullanım için saklanmaktadır.
---
