# 🌊 Hydronom

> **Platform bağımsız, güvenli, modüler ve yaşayan bir otonom araç sistemi**  
> Algılama • Füzyon • Karar • Görev • 6DOF Kontrol • Haberleşme • Güvenlik • Telemetri • Simülasyon • Yer İstasyonu

**Hydronom**, başlangıçta otonom deniz araçları için geliştirilen; zamanla platform bağımsız bir otonom sistem çekirdeğine dönüşen modüler, genişleyebilir ve gerçek dünyaya uyarlanabilir bir kontrol, algılama, görev yönetimi, haberleşme ve operasyon altyapısıdır.

Bu proje yalnızca bir yazılım yığını değildir.  
Hydronom; sensörlerden gelen veriyi anlamlandıran, görev durumunu takip eden, karar veren, aktüatörleri güvenli şekilde yöneten, kendi iç durumunu telemetriyle dışarı açan, simülasyon ve gerçek donanım arasında köprü kurabilen, güvenli haberleşme katmanı üzerinden komut alıp ACK/NACK döndürebilen çok katmanlı bir otonom runtime mimarisidir.

Hydronom’un amacı yalnızca bir tekneyi hareket ettirmek değildir.  
Amaç; **algılayan, yorumlayan, görev yürüten, kendi durumunu izleyen, güvenlik sınırlarını uygulayan, gerektiğinde kendini sınırlayan ve farklı araç tiplerine uyarlanabilen bir otonom sistem omurgası** oluşturmaktır.

Bugün Hydronom;

- 🚤 su üstü araçları,
- 🌊 su altı araçları,
- ⛵ yelkenli / uzun menzil görev platformları,
- 🤖 kara aracı benzeri robotik platformlar,
- 🚁 gelecekte VTOL / hava aracı benzeri sistemler,
- 🚀 deneysel roket / özel görev platformları

için ortak bir otonomi çekirdeği olabilecek şekilde evrilmektedir.

Bu repo, o yapının yaşayan mühendislik çekirdeğini temsil eder. ⚓

---

## 📊 Repository Scale

Hydronom artık tek bir araç prototipi değil; runtime kontrol, sensör mimarisi, füzyon, görev yönetimi, güvenlik, secure binary haberleşme, telemetri, AI destekli planlama, gateway, yer istasyonu, frontend görselleştirme, simülasyon ve gömülü firmware katmanlarını içeren çok katmanlı bir otonom sistem platformudur.

Güncel depo, build çıktıları ve bağımlılık klasörleri hariç tutulduğunda **152.000+ toplam satır** ve **102.000+ kaynak kod satırı** içermektedir.

| Area | Files | Total Lines | Code Lines | Comments | Blanks |
|---|---:|---:|---:|---:|---:|
| C# Core / Runtime / Gateway / Ground Station / AI | 776 | 95,141 | 62,632 | 18,738 | 13,771 |
| Python legacy sensor and tooling stack | 89 | 13,293 | 10,068 | 1,168 | 2,057 |
| Rust embedded firmware | 36 | 2,393 | 1,762 | 286 | 345 |
| TypeScript / TSX Ops frontend | 64 | 11,753 | 10,241 | 128 | 1,384 |
| JSON configuration and scenario assets | 31 | 9,557 | 9,473 | 0 | 84 |
| JavaScript / CSS / HTML support layers | 9 | 3,003 | 2,364 | 257 | 382 |
| C / C Header low-level experiments | 2 | 511 | 298 | 122 | 91 |
| MSBuild / Solution / YAML / TOML | 36 | 1,046 | 908 | 28 | 110 |
| Documentation / plain text / markdown | 8 | 9,810 | 0 | 8,124 | 1,686 |
| Other generated or auxiliary counted files | 340 | 5,957 | 5,004 | 0 | 953 |
| **Total** | **1,391** | **152,464** | **102,750** | **28,851** | **20,863** |

> Bu ölçüm `tokei` ile alınmıştır. `node_modules`, `bin`, `obj`, `.git`, `dist`, `build`, `.vs`, `__pycache__`, `venv` ve `.venv` gibi çıktı, bağımlılık ve ortam klasörleri hariç tutulmuştur.

Bu ölçek, Hydronom’un artık yalnızca “çalışan bir tekne kodu” olmadığını; haberleşmeden güvenliğe, sensörlerden simülasyona, gömülü yazılımdan yer istasyonuna kadar büyüyen gerçek bir otonom sistem ekosistemi olduğunu göstermektedir.

---

## 📚 İçindekiler

- [🌌 Projenin Hikâyesi](#-projenin-hikâyesi)
- [🎯 Amaç](#-amaç)
- [🧭 Temel Yaklaşım](#-temel-yaklaşım)
- [🌍 Platform Bağımsız Otonomi Vizyonu](#-platform-bağımsız-otonomi-vizyonu)
- [🏗️ Mimari Genel Bakış](#️-mimari-genel-bakış)
- [🧩 Temel Bileşenler](#-temel-bileşenler)
- [📡 Sensör ve Füzyon Mimarisi](#-sensör-ve-füzyon-mimarisi)
- [🛡️ Secure Binary Communication Pipeline](#️-secure-binary-communication-pipeline)
- [📤 Telemetri, ACK/NACK ve Gateway Akışı](#-telemetri-acknack-ve-gateway-akışı)
- [🤖 AI Destekli Planlama ve Yorumlama](#-ai-destekli-planlama-ve-yorumlama)
- [🖥️ Ground Station ve Ops Ekosistemi](#️-ground-station-ve-ops-ekosistemi)
- [🔌 Gömülü Sistem ve Pico 2W Firmware](#-gömülü-sistem-ve-pico-2w-firmware)
- [🧪 Simülasyon, Senaryo ve Test Altyapısı](#-simülasyon-senaryo-ve-test-altyapısı)
- [✨ Öne Çıkan Yetenekler](#-öne-çıkan-yetenekler)
- [🔄 Sistem Akışı](#-sistem-akışı)
- [🚤 Hedeflenen Kullanım Alanları](#-hedeflenen-kullanım-alanları)
- [🔮 Gelecek Vizyonu](#-gelecek-vizyonu)
- [🌐 Hydronom Filo ve Yer İstasyonu Mimarisi](#-hydronom-filo-ve-yer-istasyonu-mimarisi)
- [💙 Neden Özel?](#-neden-özel)
- [🛠️ Kurulum Mantığı](#️-kurulum-mantığı)
- [📐 Geliştirme Felsefesi](#-geliştirme-felsefesi)
- [✅ Doğrulama ve Smoke Test Durumu](#-doğrulama-ve-smoke-test-durumu)
- [📍 Durum](#-durum)
- [🤝 Teşekkür ve Kapanış](#-teşekkür-ve-kapanış)
- [📜 Lisans ve Kullanım Hakları](#-lisans-ve-kullanım-hakları)

---

## 🌌 Projenin Hikâyesi

Bazı projeler bir ihtiyaçtan doğar.  
Bazıları bir yarışma için yazılır.  
Bazıları ise insanın içinde bir türlü susmayan bir cümleden çıkar:

> **“Ben bunu gerçekten sıfırdan anlayarak kurmak istiyorum.”**

Hydronom biraz böyle başladı.

İlk bakışta mesele basit görünebilir:  
Bir aracı hareket ettirmek, bir motoru döndürmek, bir sensörden veri okumak, birkaç değeri ekrana yazdırmak...

Ama gerçek otonomi hiçbir zaman yalnızca bunların toplamı değildir.

Bir araç suyun üzerinde, suyun altında ya da gelecekte farklı bir ortamda hareket ederken yalnızca komut alıp motor süren bir makine olmamalıdır.  
Kendi durumunu takip etmeli, çevresini anlamlandırmalı, hedefini bilmeli, görevin hangi aşamasında olduğunu yorumlamalı, komutların güvenli olup olmadığını değerlendirmeli, haberleşme koparsa ne yapacağını bilmeli ve yaptığı her şeyi dışarıdan izlenebilir hale getirmelidir.

Hydronom, tam olarak bu düşüncenin içinden büyüdü.

Bu repo’nun içinde yalnızca kod yok.  
Burada geceleri süren denemeler, çalışmayan bağlantılar, bozulan paketler, anlamsız sensör çıktıları, sıfır gelen veriler, yanlış çalışan simülasyonlar, tekrar tekrar değişen mimari kararlar ve her seferinde sistemi biraz daha doğru kurma çabası var.

Başlangıçta bir deniz aracı runtime’ı olarak düşünülen Hydronom, zamanla çok daha büyük bir yapıya dönüştü:

- C# Primary runtime,
- platform bağımsız 6DOF durum modeli,
- modüler sensör mimarisi,
- simülasyon ve gerçek sistem ayrımı,
- secure binary haberleşme,
- HMAC, anti-replay ve authority validation,
- compact / delta telemetry,
- TCP transport,
- runtime command bridge,
- ACK/NACK yaşam döngüsü,
- Gateway ve Ops katmanları,
- yapay zekâ destekli görev planlama,
- Pico 2W tabanlı gömülü motor kontrolü,
- yer istasyonu ve operasyon altyapısı.

Hydronom bu yüzden yalnızca “bir tekne yazılımı” değildir.  
O, zamanla büyüyen ve kendi karakterini kazanan bir otonom sistem omurgasıdır.

Bu proje benim için sıradan bir repo değil.  
Çünkü burada amaç sadece bir şeyi çalıştırmak değildi.  
Amaç, bir gün çok daha büyük sistemlerin üzerine kurulabileceği sağlam bir temel oluşturmaktı.

Hydronom bugün hâlâ eksikleri olan, değişen, gelişen ve öğrenen bir sistemdir.  
Ama artık ortada yalnızca bir fikir yoktur.

Artık bir omurga vardır.  
Artık bir mimari vardır.  
Artık büyüyebilen bir sistem vardır. 🌱

---

## 🎯 Amaç

Hydronom’un temel amacı şudur:

> **Farklı araç tiplerine uyarlanabilen, güvenli, gözlemlenebilir, modüler ve platform bağımsız bir otonom sistem altyapısı oluşturmak.**

Bu amaç doğrultusunda Hydronom yalnızca bir aracı hareket ettirmeye odaklanmaz.  
Sistem; algılama, durum kestirimi, görev yönetimi, karar verme, güvenlik, haberleşme, telemetri, simülasyon ve operatör etkileşimini aynı mimari bütün içinde ele alır.

Hydronom’un çözmeye çalıştığı temel problemler şunlardır:

- 📡 sensörlerden ve dış kaynaklardan veri toplamak,
- 🧼 verileri zaman, kalite, birim ve eksen açısından normalize etmek,
- 🧠 çoklu kaynaktan anlamlı bir araç durumu üretmek,
- 🧭 3 eksenli konum ve 6DOF hareket mantığını sistemin geneline yaymak,
- 📋 görev ve senaryo akışını yönetmek,
- ⚖️ karar modülünü analiz, görev ve güvenlik çıktılarıyla beslemek,
- ⚙️ motor, thruster, servo ve diğer aktüatörleri kontrollü şekilde sürmek,
- 🛡️ güvenlik sınırlarını, authority policy kurallarını ve fail-safe davranışları uygulamak,
- 🔐 komutları güvenli haberleşme zinciri üzerinden almak ve doğrulamak,
- 📤 compact / delta telemetry ile sistem durumunu dış dünyaya aktarmak,
- 🧾 komutlar için ACK/NACK yaşam döngüsü oluşturmak,
- 🖥️ gateway, yer istasyonu ve web tabanlı operasyon arayüzleriyle bütünleşmek,
- 🤖 yapay zekâ destekli planlama, yorumlama ve yeniden planlama katmanlarına temel sağlamak,
- 🧪 gerçek donanım, simülasyon ve hibrit çalışma modlarını aynı mimaride desteklemek.

Hydronom’un hedefi yalnızca “çalışan bir demo” üretmek değildir.  
Hedef; gerçek yarış, saha testi, havuz testi, laboratuvar deneyi ve uzun vadeli Ar-Ge çalışmalarında büyüyebilecek bir omurga oluşturmaktır.

Bu yüzden Hydronom’da mimari kararlar alınırken yalnızca bugünkü tekne düşünülmez.  
Aynı çekirdeğin gelecekte şu platformlara da uyarlanabilmesi hedeflenir:

- 🚤 su üstü otonom araçlar,
- 🌊 su altı araçları,
- ⛵ yelkenli veya uzun menzil görev platformları,
- 🤖 kara aracı / paletli robot benzeri sistemler,
- 🚁 VTOL veya hava aracı benzeri platformlar,
- 🚀 özel görev / deneysel hareket sistemleri.

Hydronom’un nihai amacı, tek bir aracın kod tabanı olmaktan çıkıp;  
**farklı platformların aynı otonom runtime felsefesiyle çalışabildiği ortak bir mühendislik çekirdeği** haline gelmektir.

---

## 🧭 Temel Yaklaşım

Hydronom tasarlanırken amaç yalnızca çalışan bir prototip üretmek değildi.  
Asıl hedef; büyüyebilen, test edilebilen, farklı platformlara uyarlanabilen ve gerçek dünyada hataları görünür hale getirebilen bir otonom sistem mimarisi kurmaktı.

Bu yüzden Hydronom’un temel yaklaşımı aşağıdaki prensipler üzerine kuruludur.

### 1. 🧩 Modülerlik

Hydronom’da sistem tek bir büyük dosya, tek bir döngü veya birbirine gömülmüş sınıflar üzerine kurulmaz.

Sensör, füzyon, analiz, karar, görev, aktüasyon, haberleşme, güvenlik, telemetri, gateway, AI ve yer istasyonu katmanları mümkün olduğunca ayrık tutulur.  
Her katman kendi sorumluluğunu taşır ve diğer katmanlarla açık modeller / sözleşmeler üzerinden konuşur.

Bu yaklaşım sayesinde:

- yeni sensör backend’leri eklenebilir,
- farklı haberleşme kanalları denenebilir,
- simülasyon ve gerçek donanım aynı mimaride çalışabilir,
- runtime davranışı parça parça test edilebilir,
- tek bir modül değiştiğinde tüm sistemin kırılması engellenir.

### 2. 🌍 Platform Bağımsızlık

Hydronom başlangıçta deniz araçları için doğmuş olsa da artık yalnızca deniz üstü bir tekne runtime’ı olarak düşünülmez.

Sistemin çekirdeği; farklı araç tiplerinin ortak ihtiyaçlarını taşıyacak şekilde tasarlanır:

- durum takibi,
- görev yönetimi,
- karar üretimi,
- güvenli komut işleme,
- telemetri,
- aktüasyon dağıtımı,
- sensör entegrasyonu,
- sağlık ve risk takibi.

Bu nedenle platforma özel fiziksel etkiler, mümkün olduğunca çekirdek mantığa gömülmez.  
Su direnci, hava sürüklemesi, zemin sürtünmesi, kaldırma kuvveti, batarya davranışı veya araç geometrisi gibi bilgiler ayrı profil / model / provider katmanlarıyla temsil edilmelidir.

Çekirdek sistemin görevi belirli bir aracın fiziğini ezberlemek değil; farklı platformların verisini ortak bir otonomi diliyle işleyebilmektir.

### 3. 🧭 6DOF ve 3 Eksenli Sistem Düşüncesi

Gerçek araçlar yalnızca düzlemde hareket etmez.  
Özellikle su altı, hava aracı, roket, VTOL veya karma platformlar düşünüldüğünde 2D varsayımlar sistemi uzun vadede sınırlar.

Bu yüzden Hydronom’un güncel yönü; konum, hız, yönelim, açısal hız, kuvvet ve tork gibi değerleri 3 eksenli / 6DOF mantığında ele almaktır.

Bu yaklaşım:

- su üstü araçlar için daha doğru kuvvet / tork hesabı,
- su altı araçlar için derinlik ve pitch/roll kontrolü,
- hava araçları için tam uzaysal durum temsili,
- simülasyon için daha gerçekçi hareket modeli,
- platform bağımsız görev planlama için daha sağlam temel

oluşturur.

### 4. 👁️ Gözlemlenebilirlik

Bir otonom sistem ne yaptığını göstermiyorsa güvenilir değildir.

Hydronom’da telemetri, log, health, diagnostics, runtime snapshot, ACK/NACK, safety result ve gateway state gibi çıktılar sistemin sonradan eklenen yan ürünleri değildir.  
Bunlar sistemin anlaşılabilir, test edilebilir ve sahada izlenebilir olması için temel gerekliliklerdir.

Sistem yalnızca karar vermemeli; neden o kararı verdiğini, hangi veriye dayandığını, hangi komutu reddettiğini, hangi paketi kabul ettiğini ve hangi riskleri gördüğünü de dışarıya aktarabilmelidir.

### 5. 🛡️ Güvenlik ve Yetki Kontrolü

Her komut uygulanabilir olduğu için uygulanmamalıdır.

Hydronom’da güvenlik yaklaşımı birkaç katmandan oluşur:

- komutun kaynağı doğrulanır,
- authority / role kontrolü yapılır,
- safety-critical komutlar ayrı değerlendirilir,
- HMAC ile mesaj bütünlüğü korunur,
- anti-replay ile eski paketlerin tekrar kullanılması engellenir,
- komutlar ACK/NACK yaşam döngüsüyle takip edilir,
- runtime tarafında safety gate ve limiter mantığı uygulanır.

Bu yaklaşım özellikle yarış, saha testi, çok araçlı operasyon ve uzaktan kontrol senaryoları için kritik önemdedir.

### 6. 🔐 Ana Haberleşmede Binary Secure Pipeline

Hydronom’un güncel haberleşme yönü JSON tabanlı ana komut akışından uzaklaşmıştır.

JSON hâlâ debug, fallback veya insan tarafından okunabilir yardımcı kanallar için kullanılabilir.  
Ancak ana haberleşme omurgasında hedef:

> **Command / Telemetry / ACK → compact veya binary payload → HydronomEnvelope → HMAC → Binary codec → Transport**

şeklinde güvenli, doğrulanabilir ve daha verimli bir pipeline oluşturmaktır.

Bu sayede sistem:

- daha küçük paketlerle çalışır,
- bozuk paketleri CRC/HMAC tarafında yakalar,
- replay saldırılarını reddeder,
- command ve ACK payload’larını binary taşır,
- telemetry verisini compact / delta formatta gönderebilir,
- TCP ve ileride farklı transport katmanlarıyla genişleyebilir.

### 7. 🧪 Simülasyon ve Gerçek Donanım Ayrımı

Sadece simülasyonda çalışan bir sistem yeterli değildir.  
Sadece gerçek donanıma bağlı bir sistem de geliştirme hızını düşürür.

Hydronom bu yüzden simülasyon, gerçek donanım ve hibrit çalışma modlarını aynı mimaride destekleyecek şekilde gelişmektedir.

Amaç; sensör yokken de sistemi test edebilmek, gerçek sensör geldiğinde aynı akışı bozmadan kullanabilmek, simülasyon dünyasında görevleri deneyebilmek ve saha testinde aynı runtime omurgasını çalıştırabilmektir.

### 8. 🚀 Yavaş Ama Sağlam Gelişim

Hydronom’un geliştirme felsefesi hızlıca çalışan ama kırılgan bir demo üretmek değildir.

Bu sistemde bazen bir modül defalarca yeniden düşünülür.  
Bazen çalışan kod bile daha doğru mimari için değiştirilir.  
Bazen kısa vadeli kolay çözüm yerine uzun vadede taşınabilecek yapı tercih edilir.

Çünkü hedef yalnızca bugünkü testi geçmek değil; gelecekte daha büyük sistemlerin üzerine kurulabileceği sağlam bir temel oluşturmaktır.

---

## 🌍 Platform Bağımsız Otonomi Vizyonu

Hydronom’un en önemli yönlerinden biri, yalnızca tek bir araç tipine sıkışmayan bir otonomi çekirdeği kurma hedefidir.

Başlangıç noktası otonom deniz araçları olsa da, sistemin güncel mimari yönü çok daha geniştir. Hydronom artık yalnızca bir tekne yazılımı olarak değil; farklı fiziksel platformların aynı temel runtime felsefesiyle yönetilebildiği ortak bir otonom sistem altyapısı olarak düşünülmektedir.

Bu vizyonda araç tipi değişebilir:

- su üstü teknesi,
- su altı aracı,
- yelkenli platform,
- paletli veya tekerlekli kara aracı,
- VTOL / hava aracı,
- deneysel su altı roketi,
- özel görev robotu.

Fakat sistemin temel soruları büyük ölçüde aynıdır:

- Araç nerede?
- Hangi yönde hareket ediyor?
- Hangi görevi yürütüyor?
- Çevresinde ne var?
- Hangi komutlar güvenli?
- Hangi sensörlere güvenilebilir?
- Hangi aktüatörler kullanılabilir?
- Hangi durumda yavaşlamalı, durmalı veya fail-safe davranmalı?
- Operatöre hangi telemetri gönderilmeli?
- Gelen komut gerçekten yetkili ve güncel mi?

Hydronom bu soruları belirli bir platformun içine gömülü şekilde değil, ortak bir otonomi diliyle cevaplamaya çalışır.

### 🧠 Ortak Çekirdek, Platforma Özel Profil

Hydronom’un yaklaşımı şu ayrımı korumaktır:

> **Çekirdek sistem genel kalır; platforma özel bilgi profil, model veya provider katmanlarından gelir.**

Bu nedenle ana sistemin içine “bu araç kesin teknedir”, “bu araç sadece 2D hareket eder”, “bu sistem sadece su direnciyle çalışır” gibi varsayımlar gömülmemelidir.

Bunun yerine her araç kendi profilini sağlayabilir:

- araç tipi,
- kütle ve atalet bilgileri,
- 3D geometri,
- aktüatör yerleşimi,
- sensör yerleşimi,
- performans zarfı,
- güç / batarya limitleri,
- sürükleme veya direnç modeli,
- güvenli çalışma sınırları,
- kalibrasyon değerleri,
- platforma özel operasyon kısıtları.

Bu profil, Hydronom runtime için başlangıç bilgisi sağlar.  
Runtime ise bu bilgiyi genel kontrol, görev, güvenlik, telemetri ve karar mimarisi içinde işler.

### 🧭 2D Değil, 3D / 6DOF Temelli Düşünce

Birçok basit otonom sistem yalnızca X-Y düzleminde hareket eden bir araç varsayar.  
Bu yaklaşım kısa vadede yeterli görünse de, su altı, hava, roket veya karma platformlar düşünüldüğünde sınırlayıcı hale gelir.

Hydronom’un güncel yönü; sistemin tamamında 3 eksenli uzaysal durum ve 6DOF hareket mantığını temel almaktır.

Bu yaklaşım şu değerleri daha doğal temsil etmeyi sağlar:

- X / Y / Z konum,
- roll / pitch / yaw yönelim,
- lineer hız,
- açısal hız,
- gövde ekseninde kuvvet,
- gövde ekseninde tork,
- dünya ekseni ile gövde ekseni dönüşümleri,
- platforma göre farklı hareket kabiliyetleri.

Bu sayede aynı mimari hem yüzeyde ilerleyen bir aracı hem derinlik kontrolü yapan bir su altı aracını hem de gelecekte daha karmaşık uzaysal hareket yapan platformları taşıyabilecek hale gelir.

### 🔌 Sensörlü veya Sensörsüz Çalışabilme

Gerçek dünyada her sensör her zaman hazır olmayabilir.  
GPS kapalı olabilir, IMU geçici olarak kopabilir, LiDAR takılı olmayabilir, kamera devre dışı kalabilir veya bazı testlerde gerçek sensör yerine simülasyon verisi kullanılabilir.

Hydronom bu yüzden sensör mimarisini tak-çıkar ve backend değiştirilebilir şekilde ele alır.

Bir platform;

- gerçek sensörlerle,
- simülasyon sensörleriyle,
- replay kayıtlarıyla,
- dış pose kaynaklarıyla,
- eksik sensör setiyle,
- hibrit veri kaynaklarıyla

çalışabilir hale gelmelidir.

Bu yaklaşım, yarış ve saha testleri kadar laboratuvar geliştirmeleri için de önemlidir.

### 🛡️ Platformdan Bağımsız Güvenlik

Güvenlik yalnızca belirli bir araç için yazılmış birkaç koşuldan ibaret olmamalıdır.

Hydronom’da güvenlik şu genel sorular üzerinden düşünülür:

- Bu komut yetkili bir kaynaktan mı geldi?
- Bu komut tekrar oynatılmış eski bir paket olabilir mi?
- Bu hareket mevcut araç durumuna göre güvenli mi?
- Bu görev mevcut platform kabiliyetleriyle uyumlu mu?
- Araç bu komutu uygulayabilecek aktüatörlere sahip mi?
- Sensör verisi yeterince taze ve güvenilir mi?
- Araç fail-safe moda geçmeli mi?

Böylece güvenlik yalnızca “tekne çok hızlı gitmesin” seviyesinde kalmaz.  
Farklı platformların farklı riskleri olsa bile, çekirdek sistem güvenlik kararlarını ortak bir mimari üzerinden yönetebilir.

### 🌐 Uzun Vadeli Hedef

Hydronom’un uzun vadeli hedefi, her araç için baştan ayrı bir otonom sistem yazmak değildir.

Hedef; farklı araçların kendi profil, sensör, aktüatör ve fizik modelleriyle aynı Hydronom çekirdeğine bağlanabildiği bir yapı kurmaktır.

Bu vizyon gerçekleştiğinde Hydronom yalnızca bir yarışma kod tabanı değil; farklı araçların aynı operasyon ekosistemi içinde yönetilebildiği, test edilebildiği ve geliştirilebildiği bir otonom sistem platformu haline gelecektir.

> **Araç değişebilir. Ortam değişebilir. Görev değişebilir.  
> Ama otonomi çekirdeği aynı kalabilir.**

---

## 🏗️ Mimari Genel Bakış

Hydronom katmanlı, modüler ve genişletilebilir bir otonom sistem mimarisi olarak tasarlanır.

Bu mimaride amaç, bütün davranışı tek bir büyük döngüye veya tek bir araç tipine gömmek değildir.  
Bunun yerine sistem; çekirdek domain modelleri, runtime çalışma akışı, sensör/füzyon katmanı, görev ve karar mekanizması, güvenlik, haberleşme, telemetri, gateway, yer istasyonu, AI ve gömülü firmware gibi katmanlara ayrılır.

Genel bakışla Hydronom şu ana katmanlardan oluşur:

```text
┌──────────────────────────────────────────────────────────────┐
│                    Ground Station / Ops UI                   │
│     Web arayüzü, görev izleme, araç durumu, dünya modeli      │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│                         Gateway Layer                         │
│       Runtime frame parsing, snapshot API, WebSocket, DTO      │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│                   Secure Communication Layer                  │
│ Binary envelope, HMAC, anti-replay, authority, ACK/NACK, TCP   │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│                         Runtime Layer                         │
│   Sensor runtime, fusion, analysis, decision, task, control    │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│                         Core Layer                            │
│ Domain models, contracts, state, telemetry, safety, profiles   │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│              Hardware / Simulation / Embedded Layer           │
│   Pico 2W, sensors, ESC/motor control, sim world, replay data  │
└──────────────────────────────────────────────────────────────┘
```

Bu yapı sayesinde Hydronom hem gerçek donanım üzerinde çalışabilecek hem de simülasyon, replay, test ve geliştirme ortamlarında aynı mimari prensipleri koruyabilecek şekilde büyür.

### 🧠 Core Layer

Core katmanı sistemin ortak dilini ve temel modellerini içerir.

Bu katmanda;

- araç durum modelleri,
- 3D / 6DOF temsil yapıları,
- sensör sözleşmeleri,
- telemetri modelleri,
- haberleşme envelope yapıları,
- güvenlik ve authority modelleri,
- command / ACK / NACK modelleri,
- görev ve runtime bridge tipleri,
- platformdan bağımsız domain yapıları

yer alır.

Core katmanı doğrudan belirli bir donanıma veya UI’a bağlı olmamalıdır.  
Bu katman Hydronom’un “ne olduğunu” tanımlar.

### ⚙️ Runtime Layer

Runtime katmanı sistemin canlı çalışma akışını taşır.

Bu katmanda;

- sensör backend’leri açılır,
- sensör örnekleri okunur,
- state estimation / fusion akışı beslenir,
- görev ve senaryo durumu takip edilir,
- analiz ve karar modülleri çalışır,
- güvenlik sınırları uygulanır,
- aktüatör komutları üretilir,
- runtime telemetri frame’leri yayınlanır.

Runtime katmanı Hydronom’un “nasıl çalıştığını” belirleyen ana yürütme alanıdır.

Güncel mimari yönünde Hydronom, **C# Primary runtime** yaklaşımına ilerlemektedir.  
Bu yaklaşımda Python artık ana otorite değil; gerektiğinde legacy backup, yardımcı tooling veya geçici sensör köprüsü olarak konumlanır.

### 📡 Sensor / Fusion Layer

Sensör mimarisi artık tek bir düz klasör veya tek bir kaynak üzerinden düşünülmez.

Hydronom sensör tarafında;

- ortak sensör modelleri,
- backend değiştirilebilir kaynaklar,
- simülasyon sensörleri,
- gerçek donanım sensörleri,
- replay kaynakları,
- kalite / timing / health bilgisi,
- discovery / plug-and-play yaklaşımı

üzerine kurulu bir yapı hedefler.

Bu katmanın amacı yalnızca veri okumak değildir.  
Amaç; gelen verinin zamanını, kalitesini, kaynağını, güvenilirliğini ve sisteme katkısını anlamlı hale getirmektir.

### 🧭 State / Simulation / World Layer

Hydronom’da araç durumu yalnızca birkaç sayıdan ibaret değildir.

Sistem; araç pozisyonu, yönelimi, hızı, açısal hızı, kuvvet/tork etkileri, hedefler, engeller, görev nesneleri ve operasyon dünyasını birlikte ele alacak şekilde gelişmektedir.

Bu kapsamda mimari şu kavramları destekleyecek yönde büyür:

- physics truth,
- simulation truth,
- operational vehicle state,
- runtime world model,
- observed world,
- fused world,
- route points,
- world objects,
- mission targets,
- no-go / safety zones.

Uzun vadede amaç; aracın yalnızca önceden verilmiş kusursuz senaryo bilgisiyle değil, sensörlerden gözlemlediği dünya üzerinden operasyonel bir dünya modeli oluşturabilmesidir.

### ⚖️ Analysis / Decision / Task Layer

Hydronom’un davranışı tek bir “git” komutundan ibaret değildir.

Analysis katmanı; risk, engel, hedef, sapma, sağlık, görev durumu ve çevre bilgisini yorumlar.  
Decision katmanı; bu analiz çıktıları ve görev hedeflerine göre davranış seçer.  
Task / Mission katmanı ise daha yüksek seviyeli görev akışını yönetir.

Bu ayrım sayesinde sistem:

- görev durumunu takip edebilir,
- hedefe yaklaşma / durma / bekleme fazlarını ayırabilir,
- güvenli olmayan komutları sınırlayabilir,
- manuel / otonom / fail-safe durumlarını yönetebilir,
- gelecekte daha gelişmiş planner ve AI önerilerini sisteme dahil edebilir.

  ### 🛡️ Safety / Authority Layer

Hydronom’da güvenlik yalnızca motor çıkışını sınırlamak değildir.

Güncel mimaride güvenlik şu alanlara yayılır:

- command authority validation,
- trusted source kontrolü,
- operator / observer / runtime / emergency role ayrımı,
- safety-critical reason kontrolü,
- HMAC doğrulama,
- anti-replay kontrolü,
- ACK/NACK sonucu,
- runtime safety gate,
- actuator limiter,
- görev ve platform uyumluluk değerlendirmesi.

Bu sayede sistem gelen her komutu doğrudan uygulamak yerine, komutun kimden geldiğini, ne istediğini, güvenli olup olmadığını ve uygulanabilirliğini değerlendirir.

### 🔐 Secure Communication Layer

Hydronom’un güncel haberleşme mimarisi binary ve secure pipeline yönüne taşınmıştır.

Ana haberleşme zinciri şu mantığa dayanır:

```text
Command / Telemetry / ACK
        ↓
Compact veya Binary Payload
        ↓
HydronomEnvelope
        ↓
HMAC-SHA256
        ↓
BinaryHydronomCodec + CRC
        ↓
Transport Layer
        ↓
Receiver / Validator / Runtime Bridge
```

Bu katman;

- binary envelope codec,
- CRC32 integrity check,
- HMAC-SHA256 signing / verification,
- anti-replay window,
- compact telemetry,
- delta telemetry,
- command binary codec,
- ACK binary codec,
- priority queue,
- adaptive bandwidth policy,
- TCP packet transport,
- InMemory transport

gibi bileşenleri içerir.

JSON hâlâ debug veya fallback amaçlı kullanılabilir.  
Ancak ana command / ACK payload hattı artık binary payload üzerine kuruludur.

### 📤 Gateway Layer

Gateway katmanı Runtime’dan gelen bilgiyi dış sistemlerin kullanabileceği hale getirir.

Bu katmanda;

- runtime frame parsing,
- telemetry summary,
- mission state,
- actuator state,
- world state,
- sensor/debug diagnostics,
- snapshot endpoint,
- WebSocket yayınları,
- DTO modelleri

bulunur.

Gateway, runtime’ın iç dünyasını Ground Station / Ops arayüzüne taşımak için kritik bir köprü görevi görür.

### 🖥️ Ground Station / Ops Layer

Ground Station ve Ops katmanı, Hydronom’un operatör tarafındaki yüzüdür.

Bu katmanın hedefi yalnızca birkaç telemetri değeri göstermek değildir.  
Amaç; araç durumunu, görev akışını, dünya modelini, hedefleri, rotayı, riskleri, sensör durumunu ve sistem sağlığını anlaşılır şekilde operatöre sunmaktır.

Uzun vadede bu katman;

- çoklu araç görünürlüğü,
- harita / 3D dünya modeli,
- görev planlama,
- runtime diagnostics,
- command gönderimi,
- secure command feedback,
- AI destekli görev önerileri

gibi kabiliyetlerle büyüyecektir.

### 🤖 AI Layer

Hydronom AI katmanı doğrudan kontrol otoritesi olarak değil, destekleyici bir planlama ve yorumlama katmanı olarak düşünülür.

Bu katman;

- görev bağlamını yorumlayabilir,
- runtime durumunu özetleyebilir,
- görev planı önerebilir,
- riskli planları safety gate üzerinden sınırlandırabilir,
- operatöre açıklanabilir öneriler sunabilir.

Son söz her zaman güvenlik ve runtime otoritesinde kalmalıdır.  
AI, Hydronom’da kontrolü ele alan bağımsız bir aktör değil; güvenlik sınırları içinde çalışan yardımcı bir karar destek katmanıdır.

### 🔌 Embedded / Hardware Layer

Hydronom’un gerçek donanım tarafında gömülü sistemler önemli bir rol oynar.

Güncel yönde Raspberry Pi Pico 2W gibi MCU’lar;

- düşük seviye motor / ESC kontrolü,
- PWM üretimi,
- manuel kontrol entegrasyonu,
- sensör node yapıları,
- USB-UART üzerinden veri aktarımı

için kullanılabilir.

Bu yaklaşımda yüksek seviye otonomi C# runtime tarafında kalırken, düşük seviye zaman hassas donanım işleri MCU tarafına devredilir.

Gerçek sistem yönünde hedeflenen ayrım şudur:

- C# runtime; görev, karar, füzyon, güvenlik, telemetri ve üst seviye kontrol mantığını yürütür.
- Pico / MCU tarafı; PWM, ESC, motor sürme ve sensör node davranışları gibi donanıma yakın işleri üstlenir.
- USB-UART veya benzeri seri bağlantılar üzerinden Hydronom ile düşük seviye donanım arasında güvenilir veri alışverişi kurulur.
- Kamera gibi yüksek bant genişlikli kaynaklar doğrudan yüksek seviye bilgisayar tarafında ele alınabilir.

Bu yapı, Hydronom’un hem gerçek donanımla çalışmasını hem de sensör / motor tarafında tak-çıkar, değiştirilebilir ve modüler bir saha mimarisine yaklaşmasını sağlar.

### 🧪 Test / Smoke Test Layer

Hydronom’un büyüyen mimarisi, yalnızca manuel denemelerle güvenilir hale getirilemez.

Bu yüzden projede farklı smoke test katmanları bulunur:

- communication smoke test,
- secure command smoke test,
- command authority smoke test,
- runtime command bridge + ACK smoke test,
- transport smoke test,
- TCP transport smoke test,
- compact telemetry smoke test,
- telemetry delta smoke test,
- telemetry envelope smoke test,
- runtime pipeline smoke test,
- diagnostics smoke test,
- scenario smoke test,
- gateway ingress / ops smoke test,
- AI / Ground Station smoke test.

Bu testler, Hydronom’un yalnızca build almasını değil; ana veri akışlarının gerçekten çalıştığını da doğrulamaya yardımcı olur.

---

## 🧩 Temel Bileşenler

Hydronom birçok alt sistemden oluşan katmanlı bir otonom araç platformudur.

Bu bileşenler tek başına bağımsız parçalar gibi görünse de, asıl güçleri birlikte çalıştıklarında ortaya çıkar.  
Sensörlerden gelen veri, runtime içinde işlenir; görev ve karar katmanları bu veriyi kullanır; güvenlik katmanı komutları sınırlar; haberleşme katmanı dış dünya ile güvenli veri alışverişi sağlar; gateway ve yer istasyonu ise sistemin iç durumunu görünür hale getirir.

Hydronom’un temel bileşenleri aşağıdaki ana gruplar etrafında şekillenir.

### 🧠 Core / Domain Models

Core katmanı Hydronom’un ortak dilini oluşturur.

Bu katmanda sistemin farklı modüllerinin üzerinde anlaşacağı temel modeller bulunur:

- araç durumu,
- konum ve yönelim,
- 3D / 6DOF temsil yapıları,
- kuvvet ve tork modelleri,
- sensör örnekleri,
- telemetri frame’leri,
- görev ve runtime bridge modelleri,
- haberleşme envelope yapıları,
- command / ACK / NACK modelleri,
- güvenlik ve authority modelleri.

Bu modeller, runtime, gateway, AI, ground station ve test katmanlarının ortak bir veri dili kullanmasını sağlar.

Core katmanının temel amacı; belirli bir araca, belirli bir UI’a veya belirli bir donanıma bağlı kalmadan Hydronom’un ortak mühendislik omurgasını tanımlamaktır.

### 📡 Sensor Runtime

Sensor Runtime katmanı Hydronom’un dış dünyadan veri aldığı bölümdür.

Bu katman yalnızca sensör okumakla kalmaz; sensörün kimliğini, bağlantı tipini, zaman bilgisini, kalite durumunu, health durumunu ve sisteme nasıl katkı vereceğini de temsil eder.

Hydronom sensör mimarisi şu hedeflerle gelişmektedir:

- plug-and-play / tak-çıkar sensör yaklaşımı,
- backend değiştirilebilir sensör kaynakları,
- gerçek donanım, simülasyon ve replay desteği,
- sensör timing / quality / health takibi,
- sensör discovery ve probe mantığı,
- eksik sensörlerle de çalışabilen runtime davranışı,
- kamera dışındaki birçok sensörün Pico / MCU node üzerinden veri gönderebilmesi.

Bu yapı sayesinde aynı sensör tipi farklı backend’lerle temsil edilebilir.  
Örneğin bir IMU verisi gerçek donanımdan, simülasyondan, replay dosyasından veya ileride bir Pico node üzerinden gelebilir.

Hydronom için önemli olan yalnızca verinin gelmesi değil; verinin nereden geldiğinin, ne kadar güncel olduğunun ve ne kadar güvenilir olduğunun bilinmesidir.

### 🧠 Fusion / State Estimation

Farklı sensörlerden gelen veriler doğrudan kullanılabilir durumda olmayabilir.

Gerçek sistemlerde sensör verileri;

- farklı zamanlarda gelir,
- farklı frekanslarda akar,
- gürültü içerir,
- drift oluşturabilir,
- eksen dönüşümü gerektirebilir,
- geçici olarak kopabilir,
- kalite olarak birbirinden farklı olabilir.

Fusion / State Estimation katmanının amacı, bu dağınık veri kaynaklarından aracın anlamlı ve kullanılabilir durumunu üretmektir.

Hydronom’un güncel yönünde bu katman yalnızca 2D konum üretmek için değil; 3 eksenli konum, yönelim, hız, açısal hız, kuvvet/tork etkileri ve sensör güvenilirliği gibi bilgileri birlikte ele alacak şekilde düşünülür.

Bu katman ileride;

- IMU,
- GPS,
- derinlik sensörü,
- kamera,
- LiDAR,
- sonar / stereo vision,
- dış pose kaynakları,
- simülasyon truth verisi,
- replay kayıtları

gibi farklı kaynakları ortak bir state estimation akışında değerlendirebilecek şekilde genişleyecektir.

Amaç, aracın yalnızca “nerede olduğunu” değil; ne kadar güvenilir bir durumda olduğunu, hangi veriye ne kadar güvenilebileceğini ve runtime’ın karar katmanına hangi state bilgisini sunması gerektiğini belirlemektir.

### 🔎 Analysis Layer

Analysis katmanı, aracın içinde bulunduğu durumu yorumlayan değerlendirme katmanıdır.

Bu katman ham sensör verisini veya sadece state bilgisini doğrudan kullanmak yerine, sistem için anlamlı risk ve durum yorumları üretir.

Örnek değerlendirmeler:

- hedefe olan uzaklık,
- rota sapması,
- engel yakınlığı,
- sensör tazeliği,
- sistem health durumu,
- batarya / güç riski,
- bağlantı kalitesi,
- görev uyumluluğu,
- çevresel risk,
- yaklaşma veya durma ihtiyacı.

Analysis katmanı, Decision ve Safety katmanlarının daha bilinçli davranmasına yardımcı olur.

Hydronom’da hedef, kararların yalnızca “hedef şu tarafta, o yöne git” basitliğinde kalmamasıdır.  
Araç; hızını, hedefe yaklaşma durumunu, çevre riskini, sensör kalitesini ve görev bağlamını birlikte değerlendirebilmelidir.

### ⚖️ Decision Layer

Decision katmanı, Analysis çıktıları, görev hedefleri, güvenlik kısıtları ve runtime durumuna göre davranış seçimi yapar.

Bu katman aracın o anda ne yapması gerektiğini belirleyen ana davranış merkezlerinden biridir.

Örnek davranışlar:

- hedefe ilerle,
- yavaşla,
- yaklaşma moduna geç,
- bekle,
- dur,
- obstacle avoidance uygula,
- manuel komuta öncelik ver,
- fail-safe moda geç,
- görev adımını tamamla,
- yeni hedefe yönel.

Decision katmanı doğrudan motor sürmez.  
Bunun yerine daha üst seviyeli bir davranış veya hareket isteği üretir.  
Bu istek daha sonra control, safety limiter, actuator manager ve mixer katmanlarından geçerek uygulanabilir aktüatör komutlarına dönüştürülür.

Bu ayrım, Hydronom’un büyümesi için kritiktir.  
Çünkü karar vermek ile motor sürmek aynı sorumluluk değildir.

Decision katmanı zamanla daha gelişmiş planner, local avoidance, AI önerisi, mission compatibility ve platform capability değerlendirmeleriyle beslenebilecek şekilde tasarlanır.

### 🗺️ Task / Mission Layer

Task / Mission katmanı, Hydronom’un yüksek seviyeli görev akışını yönetir.

Bu katman aracın yalnızca anlık olarak nereye gitmesi gerektiğini değil, görevin hangi aşamasında olduğunu da takip eder.

Örnek görev davranışları:

- waypoint takip etmek,
- senaryo başlatmak veya durdurmak,
- hedefe yaklaşmak,
- hedefte beklemek,
- görev adımını tamamlamak,
- sonraki hedefe geçmek,
- acil durumda görevi kesmek,
- manuel kontrol devralındığında otonom görevi askıya almak.

Hydronom’da görev mantığı, karar ve kontrol katmanlarından ayrı düşünülür.  
Çünkü görev sistemi “ne yapılmalı?” sorusunu yüksek seviyede cevaplar; karar ve kontrol katmanları ise bunun güvenli ve uygulanabilir şekilde nasıl davranışa dönüşeceğini belirler.

Bu ayrım sayesinde Hydronom ileride daha karmaşık görev motorlarına evrilebilir:

- FSM tabanlı görevler,
- Behavior Tree yaklaşımı,
- senaryo tabanlı testler,
- görev uyumluluk kontrolü,
- çoklu araç görev paylaşımı,
- AI destekli görev planlama,
- runtime sırasında yeniden planlama.

### ⚙️ Control / Actuation Layer

Control / Actuation katmanı, karar katmanından gelen hareket isteğini fiziksel aktüatör komutlarına dönüştüren bölümdür.

Bu katmanda hedef; doğrudan “motora şu değeri ver” mantığıyla çalışmak değil, hareket isteğini güvenlik, araç geometrisi, aktüatör kabiliyeti ve platform limitleri üzerinden işleyerek uygulanabilir komutlara çevirmektir.

Bu katman şu işleri üstlenir:

- hareket isteğini yorumlamak,
- throttle / rudder / thrust benzeri komutları üretmek,
- aktüatör limitlerini uygulamak,
- thruster veya motor geometrisine göre dağıtım yapmak,
- 6DOF kuvvet / tork etkilerini hesaba katmak,
- tek yönlü veya çift yönlü motor/ESC kabiliyetlerini dikkate almak,
- güvenli duruş ve fail-safe davranışlarını desteklemek,
- gerçek motor kontrolü veya simülasyon aktüasyonu arasında köprü kurmak.

Hydronom’da aktüasyon yalnızca yüzey aracı düşünülerek tasarlanmamalıdır.  
Aynı temel mantığın su altı aracı, farklı thruster dizilimleri, kara aracı, yelkenli platform veya gelecekte daha farklı hareket sistemleriyle de uyumlu olabilmesi hedeflenir.

Bu yüzden platforma özel fiziksel detaylar mümkün olduğunca araç profili, geometri modeli, actuator provider veya physics model katmanlarında tutulmalıdır.

### 🧮 Thruster / Motor Allocation

Hydronom’da motor veya thruster komutları yalnızca sırayla kanallara yazılan PWM değerleri olarak düşünülmez.

Özellikle çok motorlu sistemlerde hareket isteğinin doğru motorlara dağıtılması gerekir.  
Bu dağıtımda şu bilgiler önemlidir:

- motorun araç üzerindeki konumu,
- kuvvet yönü,
- ters bağlı olup olmadığı,
- negatif thrust destekleyip desteklemediği,
- kanal bilgisi,
- maksimum / minimum komut sınırları,
- platformun beklenen hareket eksenleri,
- güvenlik ve limiter kararları.

Bu yaklaşım sayesinde Hydronom, farklı araç geometrilerinde aynı üst seviye kontrol mantığını kullanabilir.

Örneğin bir yüzey teknesinde yatay itki ve yaw kontrolü ön plandayken, su altı aracında Z ekseni, pitch, roll ve derinlik kontrolü de önem kazanır.  
Hydronom’un uzun vadeli hedefi, bu farkları çekirdek kontrol mantığını bozmadan araç profilleri ve actuator modelleri üzerinden yönetebilmektir.

### 🛑 Safety Limiter / Fail-Safe

Control ve actuation hattının en kritik parçalarından biri safety limiter yaklaşımıdır.

Hydronom’da güvenli olmayan bir hareket isteği doğrudan aktüatöre gönderilmemelidir.  
Komutlar uygulanmadan önce sistemin mevcut durumu, görev bağlamı, sensör sağlığı ve platform limitleri dikkate alınmalıdır.

Örnek güvenlik davranışları:

- hız veya thrust sınırlandırma,
- ani komut değişimini yumuşatma,
- stale / bayat sensör verisinde güvenli moda geçme,
- haberleşme kopmasında fail-safe davranışı,
- emergency stop durumunda aktüatörleri güvenli kapatma,
- yanlış yetkiden gelen komutları uygulamama,
- platformun desteklemediği komutu reddetme.

Bu yaklaşım Hydronom’un “çalışan” bir sistem olmasının ötesinde, sahada daha güvenilir davranabilen bir sistem olmasını hedefler.

### 🔐 Communication / Security Layer

Communication / Security katmanı, Hydronom’un dış dünya ile güvenli ve doğrulanabilir şekilde konuşmasını sağlar.

Hydronom’un güncel haberleşme yönü, ana komut ve ACK/NACK hattında JSON yerine binary payload kullanımına geçmiştir.  
Bu yaklaşım daha küçük paketler, daha sağlam doğrulama ve daha kontrollü veri akışı sağlar.

Bu katmanda şu temel bileşenler bulunur:

- HydronomEnvelope,
- BinaryHydronomCodec,
- CRC32 integrity check,
- HMAC-SHA256 signing / verification,
- anti-replay window,
- command binary codec,
- runtime command ACK binary codec,
- secure command receiver,
- command authority validator,
- runtime command bridge,
- priority queue,
- adaptive bandwidth policy,
- InMemory transport,
- TCP packet transport.

Ana haberleşme zinciri şu şekilde düşünülür:

```text
Command / Telemetry / ACK
        ↓
Compact veya Binary Payload
        ↓
HydronomEnvelope
        ↓
HMAC-SHA256
        ↓
BinaryHydronomCodec + CRC
        ↓
Transport
        ↓
Receiver / Validator / Runtime Bridge
```

Bu yapı sayesinde Hydronom;

- bozuk paketleri yakalayabilir,
- yetkisiz komutları reddedebilir,
- tekrar oynatılmış eski paketleri engelleyebilir,
- komutların kimden geldiğini ve ne istediğini doğrulayabilir,
- ACK/NACK ile komut yaşam döngüsünü takip edebilir,
- telemetriyi compact ve delta formatta taşıyabilir,
- TCP veya ileride farklı transport katmanlarıyla genişleyebilir.

JSON tamamen yasaklanmış değildir.  
Ancak ana güvenli haberleşme hattında JSON payload yerine binary payload kullanılması hedeflenir.  
JSON daha çok debug, fallback, geliştirme kolaylığı veya insan tarafından okunabilir yardımcı kanallar için düşünülebilir.

### 📤 Telemetry / Feedback Layer

Telemetry / Feedback katmanı, Hydronom’un kendi iç durumunu dış dünyaya aktardığı bölümdür.

Bir otonom sistemin güvenilir olabilmesi için yalnızca çalışması yetmez; ne yaptığını, hangi kararı verdiğini, hangi komutu kabul ettiğini, hangi riski gördüğünü ve hangi durumda olduğunu da gösterebilmesi gerekir.

Hydronom telemetri tarafında şu bilgileri taşıyabilecek şekilde gelişmektedir:

- araç konumu,
- yönelim,
- hız,
- açısal hız,
- görev durumu,
- aktif hedef,
- hedefe uzaklık,
- karar çıktısı,
- risk durumu,
- sensör sağlık bilgisi,
- aktüatör durumu,
- kuvvet / tork etkileri,
- dünya nesneleri,
- route point bilgileri,
- runtime diagnostics,
- command ACK/NACK sonuçları.

Güncel iletişim mimarisinde telemetri yalnızca büyük JSON paketleriyle taşınmak zorunda değildir.  
Compact telemetry, field mask, quantization ve delta telemetry yaklaşımıyla yalnızca gerekli alanlar daha küçük paketlerle gönderilebilir.

Bu yaklaşım özellikle sınırlı bant genişliği, kablosuz bağlantı, uzun menzil görevleri ve çoklu araç senaryoları için önemlidir.

Telemetry / Feedback katmanının amacı, Hydronom’u dışarıdan anlaşılabilir hale getirmektir.  
Çünkü görülmeyen sistem yönetilemez; açıklanamayan karar güven vermez.

### 📤 Gateway / Snapshot Layer

Gateway katmanı, Runtime’dan gelen veri akışını dış sistemlerin anlayabileceği modellere dönüştüren köprü katmandır.

Runtime kendi içinde sensör, görev, karar, aktüasyon, dünya modeli ve telemetri bilgilerini üretir.  
Gateway ise bu bilgileri operasyon arayüzü, API istemcileri, WebSocket yayınları ve yer istasyonu bileşenleri için kullanılabilir hale getirir.

Bu katmanın temel görevleri şunlardır:

- runtime frame’lerini parse etmek,
- telemetry summary üretmek,
- araç aggregate state bilgisini tutmak,
- mission state bilgisini dışarı açmak,
- actuator state bilgisini göstermek,
- world state / route point / world object bilgisini taşımak,
- sensor ve debug diagnostics verilerini snapshot’a dahil etmek,
- HTTP snapshot endpoint sağlamak,
- WebSocket üzerinden canlı durum güncellemeleri yayınlamak.

Gateway, Hydronom’un iç runtime dünyası ile operatör tarafındaki görünür sistem arasında kritik bir çeviri katmanıdır.

Bu sayede Ground Station veya Ops arayüzü, runtime’ın iç detaylarına doğrudan bağımlı olmadan araç durumunu, görev akışını, dünya modelini ve sistem sağlığını izleyebilir.

### 🖥️ Ops / Ground Station Layer

Ops ve Ground Station katmanı, Hydronom’un operatör tarafındaki yüzüdür.

Bu katmanın amacı yalnızca birkaç sayı göstermek değildir.  
Amaç; aracın ne yaptığını, hangi görevi yürüttüğünü, hangi hedefe gittiğini, hangi nesneleri gördüğünü, hangi risklerle karşılaştığını ve sistemin ne kadar sağlıklı çalıştığını anlaşılır şekilde sunmaktır.

Bu katman zamanla şu yetenekleri taşıyacak şekilde gelişmektedir:

- araç durum paneli,
- görev / senaryo izleme,
- aktif hedef ve rota görünürlüğü,
- route point ve world object gösterimi,
- runtime diagnostics,
- sensör health görünürlüğü,
- actuator state görünürlüğü,
- command gönderimi,
- secure command feedback,
- ACK/NACK sonucu gösterimi,
- harita veya 3D operasyon görünümü,
- çoklu araç / filo operasyon hazırlığı.

Ground Station ve Ops katmanı, Hydronom’un yalnızca “çalışan bir runtime” değil, izlenebilir ve yönetilebilir bir operasyon sistemi olmasını sağlar.

### 🤖 AI Assistance Layer

Hydronom AI katmanı, doğrudan motor süren veya güvenlik otoritesini ele alan bir yapı olarak düşünülmez.

AI katmanı daha çok karar destek, görev yorumlama, plan önerisi, runtime bağlam özetleme ve operatör yardımcısı rolünde konumlanır.

Bu katman şu işleri destekleyebilir:

- görev bağlamını analiz etmek,
- runtime durumunu özetlemek,
- operatöre açıklanabilir öneriler sunmak,
- görev planı üretmek veya iyileştirmek,
- riskli planları işaretlemek,
- mission prompt oluşturmak,
- runtime context üzerinden sistemin durumunu yorumlamak,
- Ground Station tarafında görev asistanı olarak çalışmak.

Hydronom’da AI katmanı güvenlik sınırlarının üstünde konumlanmaz.  
AI tarafından önerilen her plan veya yorum, runtime güvenliği, authority policy, mission compatibility ve safety gate gibi katmanlardan geçmelidir.

Bu yaklaşım sayesinde AI, sistemi kontrolsüz hale getiren bir otorite değil; güvenlik sınırları içinde çalışan yardımcı bir mühendislik katmanı olur.

### 🔌 Embedded / Pico Firmware Layer

Hydronom’un gerçek donanım tarafında gömülü sistemler önemli bir rol oynar.

Yüksek seviye otonomi, görev, karar, güvenlik ve telemetri mantığı C# runtime tarafında kalırken; zaman hassas ve donanıma yakın işler MCU tarafında yürütülebilir.

Güncel yönde Raspberry Pi Pico 2W gibi kartlar şu işler için kullanılabilir:

- ESC / motor kontrolü,
- PWM sinyali üretimi,
- manuel kontrol entegrasyonu,
- düşük seviye actuator gateway davranışı,
- sensör node yapıları,
- USB-UART üzerinden veri aktarımı,
- gerçek donanım ile Hydronom runtime arasında köprü kurma.

Bu ayrım sayesinde Hydronom’da üst seviye karar ve kontrol mantığı donanımdan kopmadan ama donanıma da gömülmeden çalışabilir.

Örneğin runtime bir motor veya thruster komutu üretir.  
Bu komut güvenlik ve allocation katmanlarından geçtikten sonra Pico / MCU tarafına gönderilebilir.  
MCU ise bu komutu gerçek PWM / ESC sinyaline dönüştürür.

Bu yaklaşım, Hydronom’un gerçek araç üzerinde daha temiz ve sürdürülebilir bir donanım mimarisiyle çalışmasını hedefler.

### 🧪 Simulation / Scenario Layer

Hydronom yalnızca gerçek donanım takılıyken çalışabilecek bir sistem olarak düşünülmez.

Simülasyon ve senaryo katmanı, gerçek testlere çıkmadan önce runtime davranışını, görev akışını, karar mantığını, aktüasyon zincirini ve telemetri üretimini doğrulamaya yardımcı olur.

Bu katmanda şu kavramlar önemlidir:

- senaryo tanımları,
- görev hedefleri,
- waypoint / route point yapıları,
- dünya nesneleri,
- engeller,
- güvenlik bölgeleri,
- physics truth,
- simulation truth,
- runtime world model,
- sim sensör backend’leri,
- replay verisi,
- smoke test senaryoları.

Hydronom için simülasyon yalnızca ekranda araç hareket ettirmek değildir.  
Amaç; runtime’ın gerçek sistemde çalışacak akışını mümkün olduğunca aynı mimariyle test edebilmektir.

Uzun vadede Hydronom simülasyon tarafında “bilinen dünya” ve “keşfedilen dünya” ayrımını da desteklemelidir.  
Yani senaryo dosyasında bütün nesneler tanımlı olsa bile araç, isterse göreve başlarken bu nesneleri bilmiyormuş gibi davranabilir ve sensör gözlemleriyle kendi operasyonel dünya modelini oluşturabilir.

### ✅ Smoke Test / Verification Layer

Hydronom büyüdükçe yalnızca “build alıyor” demek yeterli değildir.

Bu yüzden sistemde farklı katmanları doğrulayan smoke test projeleri bulunur.  
Bu testler, ana veri akışlarının gerçekten çalıştığını ve kritik pipeline’ların bozulmadığını hızlıca görmek için kullanılır.

Öne çıkan doğrulama alanları:

- communication core,
- binary envelope codec,
- CRC / HMAC doğrulama,
- anti-replay kontrolü,
- compact telemetry,
- telemetry delta,
- telemetry envelope,
- secure command pipeline,
- command authority validation,
- secure command receiver,
- runtime command bridge,
- ACK / NACK pipeline,
- InMemory transport,
- TCP transport,
- runtime pipeline,
- scenario pipeline,
- diagnostics,
- gateway ingress,
- ground station,
- AI / mission assistant.

Bu test katmanı, Hydronom’un çok sayıda modülünü birlikte geliştirirken güvenli ilerlemek için kritik önemdedir.

Özellikle secure communication tarafında son doğrulanan ana hat şudur:

- command payload binary taşınır,
- ACK / NACK payload binary taşınır,
- telemetry compact / delta formatta taşınabilir,
- envelope binary codec ile paketlenir,
- HMAC ile imzalanır,
- CRC ile bozulma yakalanır,
- anti-replay ile tekrar oynatma engellenir,
- TCP transport üzerinden gerçek localhost roundtrip yapılabilir.

Bu doğrulamalar Hydronom’un haberleşme ve güvenlik omurgasının yalnızca teorik olarak değil, çalışan smoke testlerle de desteklendiğini gösterir.

### 🧱 Bileşenlerin Birlikte Çalışması

Hydronom’un gücü, bu bileşenlerin ayrı ayrı var olmasından değil; birbirleriyle kontrollü, gözlemlenebilir ve güvenli şekilde konuşabilmesinden gelir.

Genel akışta;

- sensör katmanı veriyi toplar,
- fusion / state estimation katmanı anlamlı durum üretir,
- analysis katmanı bu durumu yorumlar,
- task / mission katmanı hedefi ve görev bağlamını sağlar,
- decision katmanı davranış seçer,
- control / actuation katmanı uygulanabilir komut üretir,
- safety katmanı komutları sınırlar,
- communication katmanı dış dünya ile güvenli veri alışverişi kurar,
- gateway ve ops katmanları sistemi görünür hale getirir,
- AI katmanı ise güvenlik sınırları içinde planlama ve yorumlama desteği sunar.

Bu bütünlük sayesinde Hydronom yalnızca tek tek modüllerden oluşan bir kod tabanı değil; büyüyebilen, test edilebilen ve farklı platformlara uyarlanabilen bir otonom sistem mimarisi haline gelir.

---

## 📡 Sensör ve Füzyon Mimarisi

Hydronom’da sensör mimarisi yalnızca “veri okuyan sınıflar” olarak düşünülmez.

Gerçek bir otonom sistemde sensör verisi; zaman, kalite, kaynak, bağlantı tipi, kalibrasyon, health durumu ve güvenilirlik bilgisiyle birlikte anlam kazanır.  
Bu yüzden Hydronom’un sensör katmanı, ham veriyi doğrudan karar mekanizmasına vermek yerine, veriyi sistemin anlayabileceği ortak bir modele dönüştürmeyi hedefler.

Sensör tarafındaki temel hedefler şunlardır:

- gerçek donanım sensörlerini desteklemek,
- simülasyon sensörleriyle çalışabilmek,
- replay / kayıt verilerinden test yapabilmek,
- backend değiştirilebilir yapı kurmak,
- sensörleri plug-and-play / tak-çıkar mantığıyla keşfedebilmek,
- sensör timing / quality / health bilgisini takip etmek,
- eksik sensörlerle de güvenli runtime davranışı üretebilmek,
- sensör verisini platform bağımsız şekilde ortak state estimation hattına taşımak.

Hydronom’un güncel yönünde sensörler tek bir sabit kaynağa bağlı değildir.  
Aynı sensör tipi farklı kaynaklardan gelebilir:

- gerçek fiziksel sensör,
- Pico / MCU üzerinden gelen seri veri,
- doğrudan bilgisayara bağlı USB / UART / I2C / SPI cihazı,
- simülasyon backend’i,
- replay dosyası,
- dış pose sağlayıcı,
- gateway veya başka runtime kaynağı.

Bu yaklaşım sayesinde Hydronom, hem laboratuvar ortamında sensörsüz test edilebilir hem de gerçek araç üzerinde sensörler takıldıkça aynı mimari bozulmadan genişleyebilir.

### 🔌 Pico / MCU Tabanlı Sensör Node Yaklaşımı

Hydronom’un gerçek donanım mimarisinde sensörlerin önemli bir kısmı doğrudan yüksek seviye bilgisayara bağlı olmak zorunda değildir.

Güncel hedeflerden biri; birçok sensörü Raspberry Pi Pico / Pico 2W veya benzeri MCU node’ları üzerinden toplamak ve Hydronom runtime’a USB-UART ya da benzeri seri bağlantılarla aktarmaktır.

Bu yaklaşımda her sensör veya sensör grubu kendi küçük node davranışına sahip olabilir:

- IMU node,
- GPS node,
- derinlik sensörü node,
- güç / batarya ölçüm node’u,
- çevresel sensör node’u,
- özel görev sensörü node’u,
- actuator feedback node’u.

Pico / MCU node tarafı ham veya yarı işlenmiş veriyi toplar.  
Hydronom runtime ise bu veriyi yüksek seviye sensör modeli olarak yorumlar.

Bu ayrımın temel avantajları şunlardır:

- sensörler tak-çıkar mantığıyla yönetilebilir,
- her sensör kendi küçük donanım arayüzüne sahip olabilir,
- yüksek seviye bilgisayarın pin / bus karmaşası azalır,
- USB-UART üzerinden daha temiz ve modüler veri akışı kurulabilir,
- arızalı veya eksik bir sensör node’u tüm sistemi bozmak yerine izole edilebilir,
- gelecekte farklı araçlarda aynı sensör node mimarisi tekrar kullanılabilir.

Hydronom için önemli olan, verinin hangi fiziksel bağlantıdan geldiğinden çok; runtime’a geldiğinde standart bir sensör örneği, zaman bilgisi, kalite bilgisi ve kaynak kimliğiyle temsil edilebilmesidir.

### 📷 Kamera İstisnası

Kamera verisi diğer birçok sensörden farklıdır.

IMU, GPS, derinlik, güç veya basit çevresel sensörler düşük veri hacmiyle Pico / MCU üzerinden taşınabilirken; kamera yüksek bant genişliği, görüntü işleme ihtiyacı ve düşük gecikme gereksinimi nedeniyle doğrudan yüksek seviye bilgisayar tarafında ele alınmalıdır.

Bu nedenle Hydronom mimarisinde kamera için hedef yaklaşım şudur:

- kamera doğrudan Raspberry Pi / mini PC / Jetson gibi yüksek seviye bilgisayara bağlanır,
- görüntü verisi doğrudan Hydronom runtime veya ilgili vision modülü tarafından okunur,
- kamera verisi Pico üzerinden geçirilmez,
- vision çıktıları runtime’a algılama / perception sonucu olarak aktarılır,
- ham görüntü ile karar katmanı arasında doğrudan değil, anlamlandırılmış perception çıktıları üzerinden bağ kurulur.

Bu ayrım, hem veri yolu karmaşasını azaltır hem de kamera gibi ağır veri kaynaklarının daha doğru yerde işlenmesini sağlar.

Hydronom’da kamera, “basit seri sensör” gibi değil; perception ve vision katmanını besleyen yüksek bant genişlikli bir algılama kaynağı olarak düşünülmelidir.

### 🔁 Backend Değiştirilebilir Sensör Mimarisi

Hydronom’da bir sensör tipi tek bir donanım veya tek bir sürücüye bağlı düşünülmemelidir.

Aynı sensör mantığı farklı backend’lerle çalışabilir:

- gerçek donanım backend’i,
- simülasyon backend’i,
- replay backend’i,
- Pico / MCU seri backend’i,
- USB / UART backend’i,
- network üzerinden gelen backend,
- geçici mock / test backend’i.

Örneğin bir IMU için sistemin üst katmanları yalnızca “IMU sample” görmek ister.  
Bu sample’ın gerçek bir BNO085’ten mi, simülasyon backend’inden mi, replay dosyasından mı, yoksa Pico üzerinden gelen seri paketten mi üretildiği üst seviye karar katmanını doğrudan ilgilendirmemelidir.

Bu yaklaşım Hydronom’a şu avantajları sağlar:

- donanım değiştiğinde üst seviye mimari bozulmaz,
- sensör yokken simülasyon backend’iyle test yapılabilir,
- farklı marka/model sensörler aynı sözleşmeye bağlanabilir,
- yarış öncesi hızlı donanım değişimleri daha yönetilebilir olur,
- her sensör kaynağı ayrı health ve quality bilgisiyle izlenebilir,
- runtime hangi sensöre ne kadar güveneceğini daha bilinçli değerlendirebilir.

Backend değiştirilebilir yapı, Hydronom’un uzun vadeli platform bağımsızlık hedefi için kritik bir parçadır.

### 🔎 Discovery / Plug-and-Play Yaklaşımı

Hydronom’un sensör tarafındaki hedeflerden biri, sensörlerin sisteme mümkün olduğunca tak-çıkar mantığıyla dahil edilebilmesidir.

Bu yaklaşımda runtime, bağlı sensör kaynaklarını keşfedebilir, aday cihazları değerlendirebilir ve uygun backend üzerinden sisteme dahil edebilir.

Discovery mantığı şu bilgileri kullanabilir:

- bağlantı tipi,
- port / cihaz yolu,
- sensör kimliği,
- üretici / model bilgisi,
- desteklenen veri tipi,
- örnekleme frekansı,
- health durumu,
- protokol sürümü,
- kalibrasyon bilgisi,
- backend uyumluluğu.

Bu yapı sayesinde sistem, her sensör değişiminde çekirdek kodun elle yeniden düzenlenmesine ihtiyaç duymadan genişleyebilir.

Örneğin aynı araçta bir testte sim IMU kullanılabilirken, başka bir testte gerçek IMU node’u kullanılabilir.  
Veya LiDAR tarafında bir modelden başka bir modele geçildiğinde, üst seviye runtime aynı “range / obstacle / scan” mantığıyla çalışmaya devam edebilir.

Plug-and-play yaklaşımı yalnızca kullanım kolaylığı için değildir.  
Aynı zamanda saha testlerinde arızalı sensörü hızlıca devreden çıkarmak, yedek sensöre geçmek veya eksik sensör setiyle güvenli modda çalışmak için de önemlidir.

### 🧾 Sensor Sample / Timing / Quality / Health

Hydronom’da sensör verisi yalnızca ham değerlerden ibaret değildir.

Bir sensör örneğinin gerçekten kullanılabilir olabilmesi için yanında bazı kritik bilgiler de taşınmalıdır:

- verinin hangi sensörden geldiği,
- hangi backend tarafından üretildiği,
- ne zaman ölçüldüğü,
- runtime’a ne zaman ulaştığı,
- gecikme / yaş bilgisi,
- kalite durumu,
- health durumu,
- sensörün güvenilirlik seviyesi,
- bağlantı kaynağı,
- veri tipinin ne olduğu.

Bu bilgiler olmadan fusion ve decision katmanları sağlıklı karar veremez.

Örneğin aynı anda gelen iki GPS verisinden biri daha yeni, biri daha eski olabilir.  
Bir IMU yüksek frekansta veri üretse bile kalibrasyonu bozuk olabilir.  
Bir derinlik sensörü doğru çalışıyor gibi görünebilir ama bağlantısı aralıklı kopuyor olabilir.

Hydronom bu nedenle sensör verisini yalnızca “değer” olarak değil; zaman, kalite ve kaynak bilgisiyle birlikte ele almayı hedefler.

### ⏱️ Timing

Otonom sistemlerde zaman bilgisi kritik önemdedir.

Sensör verisinin yalnızca geldiği an değil, gerçekten ölçüldüğü an da önemlidir.  
Bu nedenle Hydronom sensör tarafında şu ayrımları önemser:

- capture time,
- receive time,
- processing time,
- sample age,
- sensor update rate,
- stale data durumu.

Bu yaklaşım sayesinde runtime, eski veya bayat veriyi yeniymiş gibi kullanmaz.

Özellikle yüksek frekanslı kontrol, state estimation, obstacle avoidance ve gerçek zamanlı telemetri için timing bilgisi temel gerekliliktir.

### ✅ Quality

Her sensör verisi aynı güvenilirlikte değildir.

Hydronom’da sensör sample’ları ileride kalite bilgisiyle birlikte değerlendirilebilir:

- good,
- degraded,
- invalid,
- stale,
- estimated,
- simulated,
- replayed,
- unknown.

Bu kalite bilgisi fusion katmanının hangi veriye ne kadar güveneceğini belirlemesine yardımcı olur.

Örneğin GPS kısa süreli bozulduğunda sistem tamamen çökmez; IMU, son bilinen durum, simülasyon veya başka kaynaklarla geçici tahmin yürütebilir.  
Benzer şekilde simülasyon verisi ile gerçek donanım verisi aynı gibi davranmamalı; kaynak tipi açıkça bilinebilmelidir.

### ❤️ Health

Sensör health durumu, sensörün genel çalışma sağlığını ifade eder.

Health bilgisi yalnızca “veri geliyor mu?” sorusundan ibaret değildir.  
Aşağıdaki durumlar da izlenebilir:

- sensör açık mı,
- veri frekansı beklenen aralıkta mı,
- son veri ne kadar eski,
- hata sayısı artıyor mu,
- bağlantı kopması var mı,
- kalibrasyon gerekli mi,
- veri geçersizleşiyor mu,
- backend hata üretiyor mu.

Bu bilgiler runtime diagnostics, gateway snapshot, ground station ve safety katmanları için önemlidir.

Sensör health verisi sayesinde operatör yalnızca “araç çalışıyor” bilgisini değil, aracın hangi sensörlere gerçekten güvenerek çalıştığını da görebilir.

### 🧠 Fusion’a Veri Hazırlama

Sensor Runtime’ın en önemli görevlerinden biri, dağınık veri kaynaklarını fusion katmanı için anlamlı hale getirmektir.

Fusion katmanı mümkün olduğunca standartlaştırılmış ve açıklamalı veri almalıdır:

- sample identity,
- source identity,
- sensor type,
- timestamp,
- quality,
- health,
- typed data payload,
- coordinate frame bilgisi,
- measurement uncertainty,
- confidence bilgisi.

Bu yapı sayesinde state estimation katmanı, farklı sensörlerden gelen verileri tek bir ortak akış içinde değerlendirebilir.

Hydronom’un uzun vadeli hedefi, sensör verisini doğrudan karar katmanına vermek yerine; önce güvenilir, zaman uyumlu ve kalite kontrollü bir state estimation hattından geçirmektir.

Bu sayede karar katmanı ham sensör karmaşasıyla değil, anlamlı ve güvenilir araç durumu ile çalışır.

### 🧠 Fusion / State Estimation Hedefi

Hydronom’da fusion katmanının hedefi, farklı kaynaklardan gelen verileri tek bir anlamlı araç durumuna dönüştürmektir.

Bu katman yalnızca GPS ve IMU verisini birleştiren basit bir yapı olarak düşünülmemelidir.  
Uzun vadede fusion / state estimation hattı, aracın gerçek operasyonel durumunu temsil eden daha geniş bir bilgi modeli üretmelidir.

Bu model şu alanları kapsayabilir:

- 3 eksenli konum,
- roll / pitch / yaw yönelim,
- lineer hız,
- açısal hız,
- ivme,
- derinlik,
- heading,
- hedefe göre relatif konum,
- sensör güvenilirliği,
- state confidence,
- tahmin edilen hata payı,
- son güncelleme zamanı.

Bu sayede runtime, karar verirken yalnızca tek bir sensör çıktısına bağlı kalmaz.  
Bunun yerine farklı kaynaklardan gelen verilerin güvenilirlik durumunu dikkate alan daha sağlam bir araç durumu kullanır.

### 🌗 Sensörlü, Sensörsüz ve Hibrit Çalışma

Hydronom’un önemli hedeflerinden biri, yalnızca tam sensör seti takılıyken çalışabilen kırılgan bir sistem olmamaktır.

Gerçek testlerde her zaman tüm sensörler hazır olmayabilir:

- GPS kapalı olabilir,
- IMU geçici olarak bozulabilir,
- LiDAR takılı olmayabilir,
- kamera devre dışı bırakılabilir,
- derinlik sensörü henüz eklenmemiş olabilir,
- bazı sensörler yalnızca simülasyonda bulunabilir.

Bu nedenle Hydronom’un runtime davranışı farklı veri durumlarına uyum sağlayabilmelidir.

Sistem şu modlarda çalışabilecek şekilde düşünülür:

- gerçek sensörlerle çalışma,
- yalnızca simülasyon sensörleriyle çalışma,
- eksik sensör setiyle güvenli çalışma,
- replay verisiyle test,
- dış pose sağlayıcıyla çalışma,
- sensörsüz veya minimum sensörlü fail-safe / diagnostic çalışma,
- gerçek + simülasyon hibrit çalışma.

Bu yaklaşım, geliştirme sürecinde büyük avantaj sağlar.  
Donanım eksikken sistem tamamen durmak zorunda kalmaz; gerçek sensör geldiğinde de mimari baştan yazılmadan genişleyebilir.

### 🧪 Simülasyon ve Replay ile Füzyon Testi

Fusion katmanı yalnızca gerçek araç üzerinde test edilirse geliştirme yavaşlar ve hatalar geç fark edilir.

Bu yüzden Hydronom’da simülasyon ve replay kaynakları, fusion geliştirme sürecinin önemli parçalarıdır.

Simülasyon sayesinde:

- farklı sensör kombinasyonları denenebilir,
- GPS kaybı simüle edilebilir,
- IMU drift etkisi test edilebilir,
- gürültülü veriyle karar davranışı gözlemlenebilir,
- obstacle / world model etkileri denenebilir,
- state estimation çıktısı gerçek truth verisiyle karşılaştırılabilir.

Replay yaklaşımı ise daha önce kaydedilmiş saha veya test verilerinin tekrar çalıştırılmasını sağlar.  
Bu sayede aynı veri üzerinde farklı fusion algoritmaları, filtreler veya parametreler karşılaştırılabilir.

Hydronom’un uzun vadeli hedefi, gerçek sistemden gelen veriyi yalnızca anlık tüketmek değil; gerektiğinde kaydetmek, yeniden oynatmak ve sistem davranışını sürekli iyileştirmek için kullanmaktır.

### 🧭 Operasyonel State ve Truth Ayrımı

Simülasyonda aracın “gerçek” durumu bilinebilir.  
Fakat gerçek dünyada araç yalnızca sensörlerden ve tahminlerden elde edilen operasyonel durumu bilir.

Bu yüzden Hydronom’da şu ayrım önemlidir:

- **Physics / Simulation Truth:** Simülasyonun bildiği gerçek araç durumu.
- **Observed State:** Sensörlerin ölçtüğü ham veya yarı işlenmiş durum.
- **Fused State:** Fusion katmanının ürettiği tahmini durum.
- **Operational Vehicle State:** Runtime’ın karar, görev ve kontrol katmanlarında kullandığı güvenilir araç durumu.

Bu ayrım, simülasyonun gerçekçi kalması için kritiktir.

Araç simülasyon içinde bile her şeyi doğrudan bilmemelidir.  
Gerçek araç gibi sensörleri üzerinden gözlem yapmalı, eksik veya gürültülü verilerle çalışmalı ve kendi operasyonel durumunu bu gözlemler üzerinden kurmalıdır.

Bu yaklaşım Hydronom’u yalnızca “senaryo dosyasındaki bilgileri bilen” bir simülasyon sistemi olmaktan çıkarır; gerçek otonomi davranışına daha yakın bir mimariye taşır.

---

## 🛡️ Secure Binary Communication Pipeline

Hydronom’un güncel haberleşme mimarisi, ana komut ve ACK/NACK hattında JSON tabanlı payload yaklaşımından binary ve güvenli bir pipeline yapısına taşınmıştır.

Bu dönüşümün temel amacı; yarış, saha testi, uzaktan kontrol ve çoklu araç senaryolarında daha güvenli, daha küçük, daha doğrulanabilir ve daha kontrollü bir haberleşme omurgası oluşturmaktır.

Hydronom’da JSON hâlâ debug, fallback veya insan tarafından okunabilir yardımcı kanallar için kullanılabilir.  
Ancak ana haberleşme hedefi şu yöndedir:

> **Ana command, telemetry ve ACK/NACK akışı binary/compact payload üzerinden taşınmalıdır.**

Genel secure communication zinciri şu şekilde düşünülür:

```text
Command / Telemetry / ACK-NACK
        ↓
Compact veya Binary Payload
        ↓
HydronomEnvelope
        ↓
HMAC-SHA256 Signature
        ↓
BinaryHydronomCodec
        ↓
CRC32 Integrity Check
        ↓
Transport Layer
        ↓
Receiver / Validator / Runtime Bridge
```

Bu yapı sayesinde Hydronom;

- komut payload’larını binary formatta taşıyabilir,
- ACK/NACK payload’larını binary formatta taşıyabilir,
- telemetriyi compact ve delta formatta gönderebilir,
- mesajları HMAC-SHA256 ile imzalayabilir,
- bozuk paketleri CRC32 ile yakalayabilir,
- replay saldırılarını anti-replay window ile reddedebilir,
- command authority validation ile yetkisiz komutları engelleyebilir,
- runtime command bridge ile güvenli komutları runtime intent’e çevirebilir,
- ACK/NACK yaşam döngüsüyle komut sonucunu takip edebilir,
- TCP veya ileride farklı transport katmanları üzerinden aynı envelope mantığını kullanabilir.

Bu haberleşme katmanı, Hydronom’un yalnızca veri gönderen bir sistem değil; aldığı ve gönderdiği mesajları doğrulayan, sınıflandıran, önceliklendiren ve güvenlik süzgecinden geçiren bir operasyon altyapısı olmasını sağlar.

### 📦 HydronomEnvelope

Hydronom’da güvenli haberleşme yalnızca ham byte göndermekten ibaret değildir.

Her mesaj önce ortak bir envelope yapısı içine alınır.  
Bu envelope, mesajın ne olduğunu, kimden geldiğini, kime gittiğini, hangi araca ait olduğunu, hangi sırada üretildiğini, önceliğini, flags bilgisini ve payload tipini taşır.

Envelope içinde temsil edilebilecek temel bilgiler şunlardır:

- message type,
- priority,
- source id,
- target id,
- vehicle id,
- sequence,
- timestamp,
- content type,
- flags,
- session id,
- correlation id,
- payload bytes.

Bu yaklaşım sayesinde command, telemetry, ACK/NACK veya farklı mesaj türleri aynı güvenli haberleşme omurgasından geçebilir.

HydronomEnvelope, üst seviye mesajı transport katmanından bağımsız hale getirir.  
Yani mesajın TCP, InMemory, ileride UDP, serial, LoRa veya başka bir kanaldan taşınması envelope mantığını değiştirmek zorunda değildir.

### 🧬 BinaryHydronomCodec

HydronomEnvelope, ana haberleşme hattında binary codec ile paketlenir.

Binary codec’in amacı; envelope verisini daha küçük, daha hızlı işlenebilir ve bütünlük kontrolüne uygun bir byte dizisine dönüştürmektir.

Bu katman sayesinde:

- mesaj yapısı standart hale gelir,
- payload ham byte olarak korunur,
- content type bilgisi payload’ın nasıl çözüleceğini belirtir,
- envelope metadata’sı binary paket içine alınır,
- transport katmanı yalnızca byte taşır,
- receiver tarafı paketi tekrar envelope’a dönüştürebilir.

Bu yapı, ana haberleşme hattında JSON tabanlı büyük ve gevşek payload taşımak yerine daha kontrollü bir protokol oluşturur.

### 🧮 CRC32 Integrity Check

Binary paketler transport üzerinden taşınırken bozulabilir, eksik gelebilir veya hatalı parse edilebilir.

Bu nedenle Hydronom binary paketlerinde CRC32 bütünlük kontrolü kullanılır.

CRC32’nin amacı güvenlik sağlamak değildir.  
Asıl görevi, paketin byte seviyesinde bozulup bozulmadığını hızlıca yakalamaktır.

CRC32 şu durumlarda faydalıdır:

- eksik paket,
- bozulmuş payload,
- yanlış frame,
- transport kaynaklı byte hatası,
- hatalı decode girişimi.

Güvenlik doğrulaması HMAC tarafında yapılırken, CRC32 daha düşük seviyeli paket bütünlüğü kontrolü sağlar.

### 🔏 HMAC-SHA256 Signature

CRC32 paketin bozulup bozulmadığını yakalar; ancak mesajın gerçekten güvenilir bir kaynaktan geldiğini kanıtlamaz.

Bu nedenle Hydronom secure communication hattında HMAC-SHA256 imzalama ve doğrulama yaklaşımı kullanılır.

HMAC ile amaç:

- mesajın yetkili sistem tarafından üretildiğini doğrulamak,
- payload veya envelope metadata’sı değiştirildiyse bunu yakalamak,
- dışarıdan pakete müdahale edilmesini engellemek,
- bozulmuş veya sahte mesajları runtime’a ulaşmadan reddetmek.

Receiver tarafında HMAC doğrulaması başarısız olursa mesaj güvenilir kabul edilmez ve işlenmez.

Bu yaklaşım özellikle uzaktan komut, emergency stop, arm/disarm, görev başlatma, manuel kontrol ve çoklu araç haberleşmesi gibi senaryolarda kritik önemdedir.

### 🔁 Anti-Replay Window

Geçerli bir mesajın daha sonra tekrar gönderilmesi de tehlikeli olabilir.

Örneğin daha önce geçerli olan bir Arm veya MissionCommand paketi, biri tarafından tekrar oynatılırsa sistem bunu yeni komut sanmamalıdır.

Bu nedenle Hydronom’da anti-replay mantığı kullanılır.

Anti-replay window şu amaçlarla çalışır:

- aynı sequence değerine sahip paketi tekrar kabul etmemek,
- eski mesajların yeniden oynatılmasını engellemek,
- komutların zaman ve sıra mantığını korumak,
- güvenli haberleşme hattında tekrar saldırılarını azaltmak.

ACK/NACK tarafında önemli bir tasarım kararı vardır:

> Bir komut için birden fazla ACK üretilebileceğinden, ACK envelope sequence değeri komutun orijinal sequence değeri değil, ayrı `AckSequence` değeri olmalıdır.

Bu sayede aynı command için `Accepted`, `QueuedForSafetyGate`, `Applied` gibi birden fazla ACK üretildiğinde anti-replay mekanizması bu ACK’leri yanlışlıkla tekrar paket olarak reddetmez.

### 🎮 Command Binary Codec

Hydronom’da ana komut payload hattı JSON yerine binary formata taşınmıştır.

Command Binary Codec, `HydronomCommandFrame` verisini binary payload olarak encode/decode eder.  
Bu payload daha sonra `HydronomEnvelope` içine alınır, HMAC ile imzalanır ve binary envelope codec üzerinden transport katmanına verilir.

Binary command payload içinde temel olarak şu bilgiler taşınır:

- command id,
- command kind,
- authority,
- source id,
- target id,
- vehicle id,
- operator id,
- sequence,
- timestamp,
- requires ACK bilgisi,
- safety critical bilgisi,
- reason,
- sorted parameter key/value çiftleri.

Bu yaklaşımın temel amacı, ana command hattında insan tarafından okunabilir ama büyük ve gevşek JSON payload yerine daha kontrollü, daha küçük ve protokol mantığına daha uygun bir binary payload kullanmaktır.

Örnek command türleri:

- Arm,
- Disarm,
- EmergencyStop,
- ManualControl,
- MissionCommand,
- ScenarioCommand,
- AuthorityClaim,
- AuthorityRelease,
- SetMode,
- SetTarget,
- RequestStatus,
- RequestSnapshot.

Komut payload’ının binary olması, komutun güvenlikten muaf olduğu anlamına gelmez.  
Tam tersine, binary command payload şu zincirden geçerek güvenli hale gelir:

```text
HydronomCommandFrame
        ↓
HydronomCommandBinaryCodec
        ↓
HydronomEnvelope
        ↓
HMAC-SHA256
        ↓
BinaryHydronomCodec + CRC
        ↓
Transport
        ↓
SecureCommandReceiver
```

Receiver tarafında komut yalnızca decode edilmez; aynı zamanda source, target, vehicle id, sequence, message type, content type, HMAC, anti-replay ve authority policy kontrollerinden geçirilir.

### 🧾 ACK / NACK Binary Codec

Hydronom’da command sonucunun takip edilebilmesi için ACK/NACK yaşam döngüsü kullanılır.

Bir komut runtime’a ulaştığında sistem yalnızca “paket geldi” demekle yetinmez.  
Komutun decode edilip edilmediği, güvenlikten geçip geçmediği, authority tarafından kabul edilip edilmediği, runtime intent’e dönüşüp dönüşmediği ve uygulanıp uygulanmadığı ayrı durumlarla takip edilebilir.

ACK/NACK binary payload içinde temel olarak şu bilgiler taşınır:

- ack id,
- ack sequence,
- command id,
- intent id,
- command kind,
- intent kind,
- status,
- reason,
- source id,
- target id,
- vehicle id,
- operator id,
- original command sequence,
- command timestamp,
- ack timestamp,
- issues,
- metadata.

ACK tarafındaki kritik tasarım kararı şudur:

> `Sequence`, komutun orijinal sequence değeridir.  
> `AckSequence` ise ACK paketinin kendi haberleşme sequence değeridir.

Bu ayrım çok önemlidir.  
Çünkü tek bir komut için birden fazla ACK üretilebilir:

- Received,
- Accepted,
- QueuedForSafetyGate,
- QueuedForExecution,
- Applied,
- Rejected,
- Failed,
- Timeout.

Eğer bütün ACK’ler command sequence ile gönderilseydi, anti-replay mekanizması ikinci ACK’i tekrar paket sanıp reddedebilirdi.  
Bu yüzden ACK envelope sequence değeri olarak `AckSequence` kullanılır; command sequence ise payload içinde referans olarak korunur.

ACK/NACK hattı genel olarak şu şekilde çalışır:

```text
HydronomRuntimeCommandAck
        ↓
HydronomRuntimeCommandAckBinaryCodec
        ↓
HydronomEnvelope
        ↓
HMAC-SHA256
        ↓
BinaryHydronomCodec + CRC
        ↓
Transport
        ↓
SecureRuntimeCommandAckPipeline
```

Bu yapı sayesinde Hydronom’da komutlar “ateşle ve unut” mantığıyla değil; izlenebilir, doğrulanabilir ve açıklanabilir bir yaşam döngüsüyle yönetilir.

### 🚦 Command Authority Validation

Güvenli haberleşmede yalnızca paketin bozulmamış olması yeterli değildir.

Bir komut teknik olarak doğru encode edilmiş, HMAC doğrulamasından geçmiş ve replay kontrolünden geçmiş olabilir.  
Fakat yine de o komutu gönderen kaynak, o komutu vermeye yetkili olmayabilir.

Bu nedenle Hydronom’da command authority validation katmanı bulunur.

Bu katman şu soruları değerlendirir:

- Komut güvenilir bir source id’den mi geliyor?
- Source id beklenen authority ile eşleşiyor mu?
- Bu authority bu command kind için yetkili mi?
- Safety-critical komut için reason verilmiş mi?
- Operator command için operator id var mı?
- Observer yalnızca izin verilen status/snapshot komutlarını mı istiyor?
- Emergency console yalnızca emergency/status gibi yetkili komutları mı veriyor?
- Autonomous runtime kendi yetkisini aşan komut üretmeye çalışıyor mu?

Bu sayede Hydronom, yalnızca “imzalı paket geldi” diye her komutu uygulamaz.  
Komut önce güvenlik ve yetki mantığından geçer; uygun değilse ACK/NACK hattı üzerinden reddedilebilir.

### 🌉 Runtime Command Bridge

Secure command pipeline’dan geçen komutlar doğrudan runtime davranışı olmak zorunda değildir.

Önce `HydronomCommandFrame`, runtime tarafının anlayacağı daha operasyonel bir `HydronomRuntimeCommandIntent` modeline çevrilir.

Runtime Command Bridge şu dönüşümleri yapabilir:

- Arm → Arm intent,
- Disarm → Disarm intent,
- EmergencyStop → EmergencyStop intent,
- MissionCommand → StartMission / StopMission / StartScenario gibi intent’ler,
- ScenarioCommand → senaryo odaklı runtime intent,
- SetMode → runtime mode intent,
- SetTarget → hedef güncelleme intent’i,
- RequestStatus → status request intent,
- RequestSnapshot → snapshot request intent.

Bu ayrım, haberleşme protokolü ile runtime davranışını birbirinden ayırır.

Command frame dış dünyadan gelen güvenli mesajı temsil eder.  
Runtime intent ise Hydronom runtime’ın gerçekten işleyebileceği operasyonel isteği temsil eder.

Bu sayede ileride command protokolü genişlese bile runtime tarafında daha temiz ve kontrollü bir köprü katmanı korunabilir.

### 📡 Compact Telemetry

Hydronom’da telemetri verisi yalnızca büyük JSON mesajlarıyla taşınmak zorunda değildir.

Özellikle kablosuz bağlantı, uzun menzil görevler, sınırlı bant genişliği ve çoklu araç senaryoları düşünüldüğünde telemetri paketlerinin küçük, seçilebilir ve verimli olması gerekir.

Bu nedenle Hydronom’da compact telemetry yaklaşımı kullanılır.

Compact telemetry şu mantığa dayanır:

- hangi alanların gönderileceği field mask ile belirtilir,
- konum / hız / yönelim gibi değerler quantized formatta taşınır,
- yalnızca gerekli telemetry alanları pakete dahil edilir,
- snapshot ve delta frame ayrımı yapılabilir,
- bandwidth durumuna göre daha az veri gönderilebilir.

Compact telemetry içinde taşınabilecek örnek alanlar:

- position,
- orientation,
- velocity,
- angular velocity,
- control state,
- power state,
- mission state,
- risk state,
- wrench / force / torque bilgisi.

Bu yaklaşım sayesinde Hydronom, operatöre ve gateway katmanına ihtiyaç duyulan bilgiyi daha küçük paketlerle aktarabilir.

### 🔄 Delta Telemetry

Her telemetry frame’de bütün sistem durumunu tekrar tekrar göndermek verimsizdir.

Delta telemetry yaklaşımı, önceki frame ile mevcut frame arasındaki farkı değerlendirir ve yalnızca anlamlı değişiklikleri gönderir.

Bu yapı şu avantajları sağlar:

- değişmeyen alanlar gönderilmez,
- paket boyutu küçülür,
- bandwidth daha verimli kullanılır,
- düşük bant genişliği durumlarında sistem çalışmaya devam edebilir,
- önemli alanlar zorla gönderilebilir,
- küçük gürültüler eşik altında bastırılabilir.

Delta telemetry için farklı hassasiyet profilleri kullanılabilir:

- default profile,
- sensitive profile,
- low-bandwidth profile.

Örneğin düşük bant genişliği modunda çok küçük konum, açı, hız veya risk değişimleri gönderilmeyebilir.  
Buna karşılık görev durumu, güvenlik durumu veya kritik telemetry alanları gerektiğinde zorunlu olarak gönderilebilir.

Bu yaklaşım, Hydronom’un telemetry hattını yalnızca “veriyi bas” mantığından çıkarıp daha akıllı ve bağlantı koşullarına uyum sağlayan bir yapıya taşır.

### 🚦 Priority Queue

Her mesaj aynı öneme sahip değildir.

Bir EmergencyStop komutu ile düşük öncelikli debug telemetry mesajı aynı sırada beklememelidir.  
Bu nedenle Hydronom’da priority queue mantığı kullanılır.

Mesajlar önem seviyelerine göre sınıflandırılabilir:

- Emergency,
- Critical,
- High,
- Normal,
- Low,
- Bulk.

Bu yapı sayesinde acil ve güvenlik açısından kritik mesajlar düşük öncelikli mesajların arkasında beklemez.

Örneğin bağlantı zayıfladığında sistem düşük öncelikli veya bulk mesajları düşürebilir; ancak emergency ve critical mesajların iletilmesine öncelik verir.

Priority queue yaklaşımı, özellikle saha ve yarış koşullarında önemlidir.  
Çünkü gerçek bağlantılar her zaman stabil değildir ve sistem hangi mesajın daha önemli olduğunu bilmelidir.

### 📶 Adaptive Bandwidth Policy

Hydronom’un haberleşme sistemi farklı bağlantı kalitelerine uyum sağlayabilmelidir.

Bağlantı güçlü olduğunda daha geniş telemetry gönderilebilir.  
Bağlantı zayıfladığında ise sistem gereksiz mesajları azaltmalı, daha küçük paketler göndermeli ve kritik mesajlara öncelik vermelidir.

Adaptive bandwidth policy şu durumları yönetebilir:

- strong link,
- normal link,
- weak link,
- critical link,
- lost link.

Bu politika sayesinde sistem;

- düşük öncelikli mesajları düşürebilir,
- bulk veriyi erteleyebilir,
- sadece kritik telemetry gönderebilir,
- bağlantı kaybolduğunda gönderimi durdurabilir,
- bağlantı iyileştiğinde normal akışa dönebilir.

Bu yaklaşım, Hydronom’un haberleşme hattını gerçek dünya koşullarına daha uygun hale getirir.

---

### 🚚 Transport Layer

Transport katmanı, güvenli haberleşme pipeline’ının ürettiği byte paketlerini fiziksel veya mantıksal taşıma kanalı üzerinden ileten bölümdür.

Hydronom’da transport katmanı, üst seviye command / telemetry / ACK mantığından ayrıdır.  
Yani üst seviye sistem mesajın nasıl paketlendiğini ve güvenli hale getirildiğini bilir; fakat o mesajın TCP, InMemory, ileride UDP, serial, LoRa veya başka bir kanal üzerinden taşınması ayrı bir sorumluluktur.

Bu ayrım sayesinde aynı secure envelope ve binary payload mantığı farklı haberleşme teknolojileriyle kullanılabilir.

Transport katmanı genel olarak şu işleri üstlenir:

- paketi göndermek,
- gelen paketi almak,
- kanal bilgisini korumak,
- gönderilen / alınan byte sayısını izlemek,
- drop / hata sayısını takip etmek,
- transport health ve istatistik üretmek,
- farklı taşıma teknolojileri için ortak arayüz sağlamak.

### 🧪 InMemory Transport

InMemory transport, gerçek network kullanmadan iki endpoint arasında paket alışverişi test etmeyi sağlar.

Bu yapı özellikle pipeline doğrulaması için kullanışlıdır.  
Gerçek TCP, RF veya serial bağlantıya ihtiyaç olmadan command, telemetry ve ACK/NACK paketlerinin doğru encode, sign, send, receive, verify ve decode edildiği test edilebilir.

InMemory transport sayesinde şu akışlar hızlıca doğrulanabilir:

- secure command roundtrip,
- replay rejection,
- telemetry transfer,
- ACK/NACK transfer,
- queue ve stats davranışı,
- transport stop / drop senaryoları.

Bu test yaklaşımı, gerçek haberleşme kanalına geçmeden önce core pipeline’ın sağlam olduğunu göstermek için önemlidir.

### 🌐 TCP Packet Transport

TCP transport, Hydronom’un secure binary packet yapısını gerçek localhost / network akışı üzerinde taşımak için kullanılan transport katmanıdır.

TCP stream doğası gereği mesaj sınırlarını otomatik korumaz.  
Bu yüzden Hydronom TCP transport içinde ayrıca packet framing mantığı kullanılır.

TCP transport şu amaçlarla kullanılır:

- secure command paketlerini runtime’a taşımak,
- telemetry paketlerini geri göndermek,
- ACK/NACK paketlerini ground side’a iletmek,
- gerçek socket üzerinde binary packet roundtrip doğrulamak,
- ileride runtime secure command host entegrasyonuna temel oluşturmak.

Bu katman sayesinde Hydronom haberleşme omurgası yalnızca memory içi testlerde değil, gerçek TCP server/client yapısında da doğrulanabilir hale gelir.

### ✅ Doğrulanan Haberleşme Testleri

Secure communication tarafında şu ana kadar farklı seviyelerde smoke testler doğrulanmıştır:

- CommunicationSmokeTest,
- CommunicationQueueSmokeTest,
- CompactTelemetrySmokeTest,
- TelemetryDeltaSmokeTest,
- TelemetryEnvelopeSmokeTest,
- CommunicationPipelineSmokeTest,
- SecureCommandSmokeTest,
- CommandAuthoritySmokeTest,
- SecureCommandReceiverSmokeTest,
- TransportSmokeTest,
- TcpTransportSmokeTest,
- RuntimeCommandBridgeAckSmokeTest.

Son doğrulanan ana akışlarda şu sonuçlar elde edilmiştir:

- binary command payload başarıyla encode/decode edilmiştir,
- binary ACK/NACK payload başarıyla encode/decode edilmiştir,
- HMAC doğrulaması çalışmıştır,
- bozuk paketler CRC/HMAC tarafında reddedilmiştir,
- replay paketleri anti-replay tarafından reddedilmiştir,
- authority policy yetkisiz komutları engellemiştir,
- secure command runtime intent’e çevrilmiştir,
- ACK/NACK packet roundtrip doğrulanmıştır,
- TCP localhost üzerinde secure command ve telemetry akışı başarıyla test edilmiştir.

Bu testler, Hydronom’un haberleşme mimarisinin yalnızca tasarım olarak değil, çalışan doğrulama senaryolarıyla da desteklendiğini gösterir.

### 📉 Binary Payload Sonrası Paket Boyutları

Command ve ACK/NACK payload’larının binary formata taşınmasıyla paket boyutları daha kontrollü hale gelmiştir.

Örnek doğrulama çıktılarında:

- Arm command payload yaklaşık 150 byte,
- Arm command packet yaklaşık 351 byte,
- Mission command payload yaklaşık 219 byte,
- Mission command packet yaklaşık 424 byte,
- Arm accepted ACK packet yaklaşık 494 byte,
- Observer NACK packet yaklaşık 504 byte,
- Scenario ACK packet yaklaşık 485 byte

seviyelerinde ölçülmüştür.

Bu değerler, JSON tabanlı command / ACK payload yaklaşımına göre daha verimli bir ana haberleşme hattına geçildiğini göstermektedir.

---

## 📤 Telemetri, ACK/NACK ve Gateway Akışı

Hydronom’da dış dünya ile veri alışverişi yalnızca “telemetri gönderme” mantığından ibaret değildir.

Sistem; araç durumunu, görev akışını, sensör sağlığını, dünya modelini, aktüatör durumunu, güvenlik kararlarını ve komut sonuçlarını dış sistemlere düzenli ve anlaşılır şekilde aktarabilmelidir.

Bu nedenle Hydronom’da telemetri, ACK/NACK ve Gateway akışı birlikte düşünülür.

Genel veri akışı şu şekilde özetlenebilir:

```text
Runtime
  ↓
Telemetry / Mission / Actuator / World / Sensor / Diagnostics Frames
  ↓
Gateway Parser
  ↓
Vehicle Aggregate State
  ↓
Snapshot API / WebSocket
  ↓
Ground Station / Ops UI
```

Komut tarafında ise akış şu şekildedir:

```text
Ground Station / Operator
  ↓
Secure Binary Command Packet
  ↓
Transport Layer
  ↓
Secure Command Receiver
  ↓
Authority Validation
  ↓
Runtime Command Bridge
  ↓
Runtime Intent
  ↓
ACK / NACK Response
  ↓
Ground Station / Operator Feedback
```

Bu iki yönlü yapı sayesinde Hydronom yalnızca veri yayınlayan bir runtime değil; komut alan, komutu doğrulayan, sonucu açıklayan ve durumunu sürekli görünür kılan bir operasyon sistemine dönüşür.

---

### 📡 Telemetry Frame Akışı

Hydronom runtime, aracın iç durumunu dış dünyaya taşımak için farklı telemetry frame türleri üretebilir.

Bu frame’ler yalnızca konum veya hız bilgisinden ibaret değildir.  
Runtime’ın ne yaptığını, hangi görevde olduğunu, hangi hedefe yöneldiğini, hangi sensörlerin aktif olduğunu ve sistemin ne kadar sağlıklı çalıştığını temsil eder.

Telemetry akışında taşınabilecek bilgiler:

- araç konumu,
- yönelim,
- lineer hız,
- açısal hız,
- görev durumu,
- aktif hedef,
- hedefe uzaklık,
- karar modu,
- risk seviyesi,
- sensör durumu,
- aktüatör durumu,
- runtime health,
- bağlantı durumu,
- dünya nesneleri,
- route point bilgileri,
- diagnostics çıktıları.

Bu veriler Gateway katmanına ulaştığında dış sistemlerin kullanabileceği modellere dönüştürülür.

### 🧩 Compact Telemetry

Hydronom’da telemetry verisi her zaman büyük ve tam JSON frame’leriyle gönderilmek zorunda değildir.

Compact telemetry yaklaşımı, özellikle bant genişliği kısıtlı veya bağlantı kalitesi değişken ortamlarda daha verimli veri aktarımı sağlar.

Bu yaklaşımda:

- hangi alanların gönderileceği field mask ile belirlenir,
- sayısal değerler quantized formatta taşınır,
- gereksiz alanlar pakete eklenmez,
- snapshot ve delta ayrımı yapılabilir,
- kritik alanlar gerektiğinde zorunlu gönderilebilir.

Örneğin yalnızca konum, yönelim ve hız değiştiyse bütün görev, güç, wrench veya debug bilgisini tekrar göndermek gerekmez.

Bu sayede Hydronom daha küçük telemetry paketleriyle çalışabilir ve kablosuz iletişimde daha dayanıklı hale gelir.

### 🔄 Delta Telemetry

Delta telemetry, önceki telemetry frame ile mevcut telemetry frame arasındaki farkı değerlendirir.

Amaç, her seferinde tüm sistem durumunu göndermek yerine yalnızca anlamlı değişiklikleri iletmektir.

Delta telemetry şu avantajları sağlar:

- paket boyutunu küçültür,
- bandwidth kullanımını azaltır,
- değişmeyen alanları tekrar göndermeyi engeller,
- küçük gürültüleri eşik altında bastırır,
- düşük bant genişliği durumunda iletişimi daha sürdürülebilir hale getirir.

Örneğin aracın batarya yüzdesi, risk seviyesi veya hedefe uzaklığı anlamlı şekilde değişmediyse bu alanlar gönderilmeyebilir.  
Ancak görev durumu değiştiyse veya risk arttıysa bu bilgi delta frame’e dahil edilir.

### 📌 Snapshot ve Delta Ayrımı

Hydronom telemetry hattında iki temel yaklaşım birlikte kullanılabilir:

- **Snapshot:** Sistemin seçilen tüm durumunu belirli bir anda temsil eder.
- **Delta:** Önceki duruma göre yalnızca değişen veya önemli hale gelen alanları taşır.

Snapshot frame’ler yeni bağlanan istemciler, bağlantı toparlama veya periyodik tam durum yenileme için önemlidir.  
Delta frame’ler ise normal akışta daha küçük ve verimli güncellemeler sağlar.

Bu ayrım, Hydronom’un hem güvenilir hem de bant genişliği açısından daha verimli bir telemetry hattı kurmasına yardımcı olur.

### 🧾 ACK / NACK Yaşam Döngüsü

Hydronom’da komut gönderimi “paketi yolla ve unut” mantığıyla ele alınmaz.

Bir komut runtime’a gönderildiğinde, sistem bu komutun hangi aşamada olduğunu takip edebilmelidir:

- paket alındı mı,
- decode edildi mi,
- güvenlik doğrulamasından geçti mi,
- authority policy tarafından kabul edildi mi,
- runtime intent’e çevrildi mi,
- safety gate’e alındı mı,
- execution queue’ya girdi mi,
- uygulandı mı,
- reddedildi mi,
- hata veya timeout oluştu mu?

Bu nedenle Hydronom’da ACK/NACK yaşam döngüsü kullanılır.

ACK/NACK durumları şunları temsil edebilir:

- Received,
- Accepted,
- QueuedForSafetyGate,
- QueuedForExecution,
- Applied,
- Rejected,
- RejectedByDecode,
- RejectedBySecurity,
- RejectedByAuthority,
- RejectedByRuntimeBridge,
- RejectedBySafetyGate,
- Failed,
- Timeout.

Bu yapı sayesinde operatör veya Ground Station yalnızca “komut gönderildi” bilgisini değil, komutun sistem içinde hangi aşamaya kadar ilerlediğini de görebilir.

### ✅ ACK

ACK, komutun sistem tarafından olumlu bir aşamaya taşındığını belirtir.

Örneğin bir komut:

- başarıyla alınmış olabilir,
- güvenlik doğrulamasından geçmiş olabilir,
- authority policy tarafından kabul edilmiş olabilir,
- runtime intent’e çevrilmiş olabilir,
- safety gate kuyruğuna alınmış olabilir,
- uygulanmış olabilir.

Bu durumların hepsi aynı anlama gelmez.  
Bu yüzden ACK yalnızca tek bir “OK” cevabı değildir; komutun yaşam döngüsündeki pozisyonunu açıklayan daha detaylı bir geri bildirimdir.

### ❌ NACK

NACK, komutun bir sebeple reddedildiğini veya başarısız olduğunu belirtir.

NACK şu durumlarda üretilebilir:

- paket decode edilemediğinde,
- HMAC doğrulaması başarısız olduğunda,
- replay kontrolü paketi reddettiğinde,
- content type beklenen formatta olmadığında,
- command envelope ile payload uyuşmadığında,
- source / target / vehicle id uyuşmadığında,
- authority policy komutu reddettiğinde,
- runtime bridge komutu intent’e çeviremediğinde,
- safety gate komutu güvenli bulmadığında,
- execution sırasında hata oluştuğunda,
- timeout meydana geldiğinde.

Bu sayede reddedilen komutlar sessizce kaybolmaz.  
Sistem, komutun neden reddedildiğini açıklayabilir.

### 🔢 AckSequence ve Command Sequence Ayrımı

Hydronom ACK/NACK mimarisinde önemli bir tasarım kararı vardır.

Bir komutun kendi `Sequence` değeri vardır.  
Fakat o komut için birden fazla ACK/NACK üretilebilir.

Örneğin aynı komut için şu sıra oluşabilir:

```text
Command Sequence: 42

ACK 1 → Received
ACK 2 → Accepted
ACK 3 → QueuedForSafetyGate
ACK 4 → Applied
```

Eğer bütün ACK paketleri command sequence değeri olan `42` ile gönderilirse, anti-replay mekanizması ikinci ACK paketini eski paket sanıp reddedebilir.

Bu nedenle Hydronom’da ACK paketlerinin kendi `AckSequence` değeri bulunur.

- `Sequence`: Komutun orijinal sequence değeridir.
- `AckSequence`: ACK/NACK paketinin kendi haberleşme sequence değeridir.

Envelope sequence alanında ACK için `AckSequence` kullanılır.  
Komutun orijinal sequence değeri ise ACK payload içinde referans olarak korunur.

Bu ayrım, çok aşamalı komut geri bildiriminin anti-replay mekanizmasıyla çakışmadan çalışmasını sağlar.

### 📤 Gateway Snapshot Akışı

Gateway katmanı, Runtime’dan gelen farklı frame türlerini tek bir dış görünür sistem durumuna dönüştürür.

Runtime içinde üretilen bilgiler doğrudan UI veya dış istemciler tarafından kullanılmayabilir.  
Bu nedenle Gateway, bu verileri parse eder, normalize eder ve snapshot modeli içinde birleştirir.

Gateway snapshot içinde şu bilgiler bulunabilir:

- araç kimliği,
- runtime bağlantı durumu,
- son telemetry zamanı,
- araç pozisyonu,
- yönelim ve hız bilgileri,
- görev / mission durumu,
- aktif hedef,
- route point listesi,
- world object listesi,
- actuator state,
- sensor state,
- debug sensor state,
- diagnostics bilgileri,
- runtime health,
- AI / Ground Station bağlam bilgileri.

Bu yapı sayesinde dış sistemler runtime’ın iç detaylarını bilmeden tek bir snapshot endpoint üzerinden güncel sistem durumunu okuyabilir.

### 🌐 WebSocket Yayını

Snapshot endpoint, istemcilerin güncel durumu periyodik olarak çekmesini sağlar.  
Ancak operasyon arayüzlerinde yalnızca polling yeterli olmayabilir.

Bu nedenle Gateway katmanı WebSocket yayınlarıyla canlı durum güncellemeleri sağlayabilir.

WebSocket akışı sayesinde:

- araç durumu anlık güncellenebilir,
- görev ilerleyişi canlı izlenebilir,
- route ve hedef değişimleri hızlı yansıtılabilir,
- actuator / sensor / diagnostics bilgileri operatöre daha hızlı ulaşabilir,
- Ground Station veya Ops arayüzü runtime olaylarına daha yakın çalışabilir.

Bu yapı, Hydronom’un gerçek operasyon ortamında daha akıcı ve izlenebilir hale gelmesini sağlar.

### 🖥️ Ops Görünürlüğü

Hydronom’un iç sisteminin güçlü olması tek başına yeterli değildir.  
Operatör sistemin ne yaptığını göremiyorsa, sistem güven vermez.

Ops / Ground Station tarafında hedeflenen görünürlük şu alanları kapsar:

- araç nerede,
- hangi hedefe gidiyor,
- görev hangi aşamada,
- rota nasıl ilerliyor,
- dünya modelinde hangi nesneler var,
- sensörler sağlıklı mı,
- aktüatörler ne durumda,
- risk seviyesi nedir,
- son komut kabul edildi mi,
- komut neden reddedildi,
- runtime hangi karar modunda çalışıyor,
- bağlantı ve telemetry durumu nasıl.

Bu görünürlük, özellikle yarış ve saha testlerinde kritik önemdedir.  
Çünkü operatör yalnızca aracı değil, aracın arkasındaki karar ve güvenlik sistemini de anlayabilmelidir.

### 🧭 Runtime → Gateway → Ops Dünya Modeli

Hydronom’da dünya modeli yalnızca simülasyon tarafında kalan bir veri değildir.

Runtime; görev hedefleri, route point’ler, world object’ler, engeller, checkpoint’ler, buoy’ler, no-go bölgeleri veya görev nesneleri gibi bilgileri Gateway’e aktarabilir.  
Gateway bu bilgileri snapshot ve WebSocket üzerinden Ops arayüzüne taşır.

Bu akış şu şekilde düşünülebilir:

```text
Runtime World / Mission State
        ↓
RuntimeWorldObjects Frame
        ↓
Gateway RuntimeFrameParser
        ↓
WorldStateDto
        ↓
Snapshot API / WebSocket
        ↓
Ops / Ground Station Visualization
```

Bu sayede operatör yalnızca aracın konumunu değil, aracın içinde bulunduğu operasyon dünyasını da görebilir.

Uzun vadede bu yapı; çoklu araç operasyonları, görev planlama, 3D dünya görünümü, engel farkındalığı ve AI destekli görev değerlendirme gibi kabiliyetlerin temelini oluşturur.

---

## 🤖 AI Destekli Planlama ve Yorumlama

Hydronom’da yapay zekâ katmanı, doğrudan motor süren veya güvenlik otoritesini ele alan bağımsız bir kontrol sistemi olarak düşünülmez.

AI katmanının temel rolü; runtime, görev, operasyon ve sistem sağlığı bağlamını yorumlayan; operatöre açıklanabilir öneriler sunan; görev planlama ve yeniden planlama süreçlerine destek veren yardımcı bir mühendislik katmanı olmaktır.

Bu yaklaşımda AI;

- aracın mevcut durumunu yorumlayabilir,
- görev bağlamını analiz edebilir,
- runtime telemetry bilgisini özetleyebilir,
- görev planı önerebilir,
- riskli durumları işaretleyebilir,
- operatöre anlaşılır açıklamalar üretebilir,
- Ground Station tarafında görev asistanı gibi çalışabilir,
- mission planning prompt’ları oluşturabilir,
- runtime context üzerinden sistem davranışını değerlendirebilir.

Ancak AI katmanı Hydronom’da nihai otorite değildir.

AI tarafından önerilen her plan, yorum veya görev değişikliği; runtime güvenliği, authority policy, mission compatibility, safety gate ve operatör kontrolü gibi katmanlardan geçmelidir.

Bu nedenle Hydronom’da AI şu şekilde konumlanır:

> **AI karar destek sağlar; güvenlik ve uygulama otoritesi runtime mimarisinde kalır.**

Bu yaklaşım, yapay zekâyı kontrolsüz bir karar vericiye dönüştürmeden, Hydronom’un daha anlaşılır, daha planlı ve daha operasyonel hale gelmesine yardımcı olur.

### 🧠 AI Runtime Context

AI katmanının anlamlı öneriler üretebilmesi için yalnızca statik görev metnine bakması yeterli değildir.

Hydronom’da AI, runtime context üzerinden sistemin güncel durumunu yorumlayabilecek şekilde düşünülür.

Runtime context içinde şu bilgiler yer alabilir:

- araç kimliği,
- görev durumu,
- aktif hedef,
- araç konumu,
- hız ve yönelim bilgisi,
- risk seviyesi,
- sensör health durumu,
- actuator state,
- world state,
- route point bilgisi,
- diagnostics çıktıları,
- son command / ACK durumu,
- runtime mode,
- bağlantı ve telemetry durumu.

Bu bilgiler AI katmanına verildiğinde, AI yalnızca genel bir metin üretmez; sistemin gerçekten içinde bulunduğu operasyonel bağlamı dikkate alarak yorum yapabilir.

Örneğin AI şu sorulara destek olabilir:

- Araç neden yavaşlamış olabilir?
- Görev hangi aşamada görünüyor?
- Risk seviyesi neden yükselmiş olabilir?
- Sensörlerden biri güvenilir görünmüyor mu?
- Operatör hangi bilgiyi önce kontrol etmeli?
- Görev planı mevcut araç kabiliyetleriyle uyumlu mu?

Bu yaklaşım, AI katmanını Hydronom’un gözlemlenebilirlik ve operasyon desteği tarafında değerli hale getirir.

### 🗺️ Mission Planning Assistance

Hydronom AI katmanı görev planlama süreçlerinde yardımcı bir katman olarak kullanılabilir.

AI burada doğrudan görevi başlatan veya araca komut veren bir otorite olmak zorunda değildir.  
Bunun yerine görev hedeflerini, araç durumunu, çevresel bilgileri ve operasyon kısıtlarını yorumlayarak öneriler üretebilir.

AI destekli görev planlama şu alanlarda kullanılabilir:

- görev açıklamasını yapılandırmak,
- hedef sırası önermek,
- riskli görev adımlarını işaretlemek,
- eksik görev parametrelerini tespit etmek,
- operatöre anlaşılır görev özeti sunmak,
- görev başarımı için dikkat edilmesi gerekenleri listelemek,
- runtime context’e göre yeniden planlama önerisi üretmek.

Örneğin bir senaryoda araç belirli waypoint’leri takip ederken risk artarsa, AI operatöre alternatif görev akışı veya dikkat edilmesi gereken noktalar hakkında öneri sunabilir.

Ancak bu öneriler doğrudan uygulanmadan önce Hydronom’un güvenlik ve yetki katmanlarından geçmelidir.

### 🛡️ AI Safety Gate

AI tarafından üretilen her öneri doğru, güvenli veya uygulanabilir kabul edilmemelidir.

Bu nedenle Hydronom’da AI çıktılarının safety gate ve authority mantığıyla birlikte değerlendirilmesi gerekir.

AI Safety yaklaşımı şu soruları sorabilir:

- Önerilen görev araç kabiliyetleriyle uyumlu mu?
- Öneri mevcut güvenlik sınırlarını ihlal ediyor mu?
- Komut safety-critical bir etki doğuruyor mu?
- Operatör onayı gerekiyor mu?
- AI önerisi sadece bilgi amaçlı mı, yoksa runtime davranışını değiştirecek mi?
- Araç bu öneriyi uygulayacak sensör ve aktüatörlere sahip mi?
- Bağlantı, sensör health veya görev durumu bu öneri için yeterli mi?

Bu yaklaşım sayesinde AI katmanı Hydronom’da kontrolsüz bir karar vericiye dönüşmez.

AI; öneri, açıklama ve planlama desteği sağlar.  
Runtime, safety gate, authority policy ve operatör kontrolü ise uygulanabilirlik ve güvenlik kararlarını verir.

### 🧑‍✈️ Ground Station AI Assistant

Hydronom’un Ground Station tarafında AI, operatör için görev asistanı gibi çalışabilir.

Bu asistan;

- mevcut görevi özetleyebilir,
- araç durumunu açıklayabilir,
- telemetry bilgisini yorumlayabilir,
- riskleri anlaşılır hale getirebilir,
- görev planı önerileri sunabilir,
- hata durumlarında kontrol edilmesi gereken noktaları listeleyebilir,
- sistemin neden belirli bir davranışa geçtiğini açıklamaya yardımcı olabilir.

Bu, özellikle karmaşık runtime durumlarında operatörün sistemi daha hızlı anlamasını sağlar.

Hydronom’un büyüyen yapısında AI’ın en değerli rolü, sistemi “daha otonom” yapmaktan önce, sistemi **daha açıklanabilir, daha yönetilebilir ve daha anlaşılır** hale getirmektir.

### ⚖️ AI Katmanının Sınırları

Hydronom’da AI katmanı güçlü bir yardımcı olabilir; fakat sistem güvenliği açısından sınırları net olmalıdır.

AI’ın yapmaması gerekenler:

- doğrudan motor / thruster komutu üretmek,
- safety gate’i atlayarak komut uygulatmak,
- authority policy dışında hareket etmek,
- operatör onayı gereken durumlarda tek başına karar vermek,
- sensör health ve runtime risklerini yok saymak,
- araç kabiliyetleriyle uyumsuz görev önermek,
- güvenlik açısından kritik komutları kontrolsüz şekilde tetiklemek.

Bu nedenle AI çıktıları Hydronom içinde doğrudan “gerçek” kabul edilmez.  
AI çıktısı, diğer sistem çıktıları gibi doğrulanması, sınırlandırılması ve bağlama göre değerlendirilmesi gereken bir öneri veya yorum katmanı olarak ele alınır.

Bu yaklaşım, AI’ın faydasını korurken sistemin güvenliğini runtime, safety ve authority katmanlarında tutar.

### 🔮 Gelecek AI Hedefleri

Hydronom’un AI tarafı uzun vadede yalnızca metin üreten bir yardımcı olmaktan daha ileriye taşınabilir.

Gelecekte hedeflenen AI destekli kabiliyetler şunlardır:

- runtime context üzerinden görev başarımı analizi,
- telemetry trend yorumlama,
- sensör health anomali açıklama,
- görev planı kalite değerlendirmesi,
- mission compatibility önerileri,
- güvenli alternatif rota önerileri,
- operatör için olay özeti üretme,
- test sonrası log analizi,
- simülasyon sonuçlarını yorumlama,
- saha testi raporu hazırlama,
- çoklu araç görev koordinasyon önerileri,
- doğal dil ile görev taslağı oluşturma.

Bu hedeflerde bile temel ilke değişmez:

> **AI önerir, açıklar ve destekler.  
> Runtime, safety ve operatör kararı uygular.**

Hydronom’da AI’ın değeri, sistemi kontrolsüz hale getirmesinde değil; karmaşık runtime davranışlarını daha anlaşılır, daha planlanabilir ve daha yönetilebilir hale getirmesindedir.

---

## 🖥️ Ground Station ve Ops Ekosistemi

Hydronom yalnızca araç üzerinde çalışan bir runtime’dan ibaret değildir.

Gerçek bir otonom sistemde aracın ne yaptığını görmek, görev akışını izlemek, riskleri anlamak, sensör ve aktüatör durumunu takip etmek, gerektiğinde güvenli komut göndermek ve sistemin genel sağlığını değerlendirmek gerekir.

Bu nedenle Hydronom’da Ground Station ve Ops ekosistemi önemli bir katmandır.

Ground Station / Ops tarafının temel amacı şudur:

> **Hydronom runtime’ın iç dünyasını operatör için anlaşılır, izlenebilir ve yönetilebilir hale getirmek.**

Bu katman şu sorulara cevap verebilmelidir:

- Araç şu anda bağlı mı?
- Runtime çalışıyor mu?
- Araç nerede?
- Hangi hedefe gidiyor?
- Görev hangi aşamada?
- Sensörler sağlıklı mı?
- Aktüatörler aktif mi?
- Risk seviyesi nedir?
- Dünya modelinde hangi nesneler var?
- Route point’ler ve hedefler doğru görünüyor mu?
- Son komut kabul edildi mi?
- Komut reddedildiyse nedeni ne?
- Sistem güvenli modda mı?
- AI veya görev asistanı hangi önerileri sunuyor?

Bu görünürlük, özellikle yarış, saha testi, havuz testi, laboratuvar deneyi ve çoklu araç operasyonları için kritik önemdedir.

Hydronom’un hedefi, operatöre yalnızca birkaç sayı göstermek değildir.  
Amaç; runtime, görev, dünya modeli, güvenlik, haberleşme ve AI katmanlarını tek bir operasyon bakışında anlamlı hale getirmektir.

### 🌉 Gateway → Snapshot → WebSocket → Ops UI Akışı

Hydronom’da Runtime’ın ürettiği veriler doğrudan kullanıcı arayüzüne ham haliyle verilmez.

Runtime; telemetry, mission, actuator, world, sensor ve diagnostics gibi farklı frame’ler üretir.  
Gateway katmanı bu frame’leri parse eder, anlamlı DTO modellerine dönüştürür ve dış sistemlerin okuyabileceği tekil bir snapshot durumunda birleştirir.

Genel akış şu şekildedir:

```text
Hydronom Runtime
        ↓
Runtime Telemetry / Mission / Actuator / World / Sensor Frames
        ↓
HydronomOps Gateway
        ↓
Runtime Frame Parser
        ↓
Vehicle Aggregate State
        ↓
Snapshot API + WebSocket
        ↓
Ops / Ground Station UI
```

Bu yapı sayesinde Ops arayüzü, runtime’ın içindeki tüm karmaşık sınıflara ve düşük seviye veri akışına doğrudan bağımlı olmak zorunda kalmaz.

Gateway şu görevleri üstlenir:

- runtime’dan gelen frame’leri ayrıştırmak,
- araç durumunu aggregate state içinde toplamak,
- son telemetry zamanlarını izlemek,
- görev durumunu güncellemek,
- actuator state bilgisini saklamak,
- world state bilgisini taşımak,
- sensör ve debug diagnostics bilgilerini snapshot’a eklemek,
- HTTP snapshot endpoint sağlamak,
- WebSocket üzerinden canlı güncelleme göndermek.

Bu ayrım, Hydronom’un frontend tarafını daha esnek hale getirir.  
Runtime gelişse bile Gateway dışarıya kararlı ve daha okunabilir bir operasyon modeli sunabilir.

### 📸 Snapshot API

Snapshot API, Hydronom’un o anki sistem durumunu dışarıya tek bir okunabilir model olarak sunar.

Bu snapshot içinde şu bilgiler bulunabilir:

- runtime bağlantı durumu,
- araç kimliği,
- son telemetry zamanı,
- pozisyon / yönelim / hız bilgileri,
- görev ve mission durumu,
- aktif hedef,
- route point listesi,
- world object listesi,
- actuator state,
- sensor state,
- diagnostics,
- AI / Ground Station context bilgileri.

Snapshot API özellikle dashboard, test aracı, debug script’i veya frontend uygulaması için kullanışlıdır.

Operatör veya geliştirici tek tek runtime frame’lerini takip etmek yerine, sistemin güncel özet durumunu snapshot üzerinden okuyabilir.

### 🔴 WebSocket Canlı Yayını

Operasyon arayüzlerinde yalnızca snapshot’ı belirli aralıklarla çekmek yeterli olmayabilir.

Bu yüzden HydronomOps Gateway, WebSocket üzerinden canlı durum güncellemeleri yayınlayabilecek şekilde düşünülür.

WebSocket akışı şu avantajları sağlar:

- araç durumu daha akıcı güncellenir,
- görev ilerleyişi canlı takip edilir,
- yeni world object veya route bilgisi hızlı yansır,
- actuator / sensor / diagnostics değişimleri gecikmeden görülebilir,
- frontend sürekli polling yapmak zorunda kalmaz,
- çoklu araç operasyonlarında daha dinamik bir izleme zemini oluşur.

Bu yapı, Hydronom’un operasyon tarafını yalnızca statik bir panel olmaktan çıkarıp gerçek zamanlı bir izleme sistemine yaklaştırır.

### 🗺️ Ops UI ve Operasyon Görünürlüğü

Ops UI, Hydronom’un operatör tarafındaki ana görünürlük katmanıdır.

Bu arayüzün amacı yalnızca konum, hız veya görev adı göstermek değildir.  
Amaç; runtime’ın içeride ne yaptığını, aracın hangi hedefe yöneldiğini, hangi dünya nesnelerini bildiğini, hangi sensörlere güvendiğini ve hangi güvenlik durumunda olduğunu anlaşılır hale getirmektir.

Ops UI içinde görünür olması hedeflenen temel bilgiler şunlardır:

- araç pozisyonu,
- yönelim ve hız bilgileri,
- aktif görev,
- aktif hedef,
- route point listesi,
- world object listesi,
- sensör health durumu,
- actuator state,
- runtime diagnostics,
- risk seviyesi,
- son command / ACK sonucu,
- bağlantı ve telemetry durumu.

Bu görünürlük özellikle saha testlerinde çok önemlidir.  
Çünkü operatör yalnızca aracın hareketini değil, aracın o hareketi hangi iç durumla yaptığını da görebilmelidir.

### 🌐 3D Tactical View

Hydronom’un uzun vadeli Ops vizyonunda 3D tactical view önemli bir yer tutar.

3D görünüm, aracın yalnızca düzlemdeki konumunu değil; operasyon dünyasını, hedefleri, engelleri, route point’leri ve görev nesnelerini daha anlaşılır şekilde temsil etmeyi hedefler.

Bu görünüm şu amaçlarla kullanılabilir:

- aracın dünya içindeki konumunu göstermek,
- görev rotasını görselleştirmek,
- checkpoint / buoy / obstacle gibi nesneleri göstermek,
- aktif hedefi belirtmek,
- yaklaşma ve görev ilerleyişini izlemek,
- su üstü / su altı / farklı platform senaryolarında 3 eksenli farkındalık sağlamak.

3D tactical view yalnızca görsel bir süs değildir.  
Hydronom’un dünya modeli, görev mantığı ve runtime state bilgisini operatörün anlayabileceği bir operasyon haritasına dönüştürür.

### 🧭 World Model Görünürlüğü

Hydronom’da dünya modeli yalnızca runtime içinde kalan bir veri yapısı olmamalıdır.

Runtime’ın bildiği veya gözlemlediği dünya bilgisi Gateway üzerinden Ops arayüzüne taşınabilmelidir.

Bu dünya modeli şunları içerebilir:

- görev hedefleri,
- route point’ler,
- checkpoint’ler,
- buoy’ler,
- engeller,
- no-go bölgeleri,
- safety zone’lar,
- başlangıç / bitiş noktaları,
- görev nesneleri,
- platforma özel operasyon alanları.

Bu bilgilerin görünür olması, operatörün aracın kararlarını daha iyi anlamasını sağlar.

Örneğin araç bir hedefe doğrudan gitmek yerine farklı bir rota izliyorsa, operatör bunun sebebini dünya modeli üzerinden görebilmelidir:  
engel, güvenlik bölgesi, görev sırası, rota noktası veya runtime karar modu.

Bu yaklaşım Hydronom’u “siyah kutu” olmaktan uzaklaştırır.  
Araç yalnızca karar vermez; karar verdiği dünyayı da dışarı gösterir.

### 🔐 Secure Command Feedback

Ground Station yalnızca aracı izleyen pasif bir ekran değildir.  
Uzun vadede operatörün güvenli şekilde komut gönderebildiği ve bu komutların sonucunu takip edebildiği bir operasyon merkezi olmalıdır.

Hydronom’un secure command feedback yaklaşımı şu mantığa dayanır:

- operatör komut gönderir,
- komut binary secure pipeline üzerinden paketlenir,
- HMAC ile doğrulanabilir hale gelir,
- runtime tarafında authority validation’dan geçer,
- runtime command bridge ile intent’e çevrilir,
- safety gate ve execution aşamalarına alınır,
- sonuç ACK/NACK olarak Ground Station’a döner.

Bu sayede operatör yalnızca “komutu gönderdim” bilgisini değil, komutun sistem içinde ne olduğunu da görebilir:

- kabul edildi mi,
- reddedildi mi,
- güvenlikten geçemedi mi,
- yetki problemi mi var,
- safety gate’e mi takıldı,
- uygulandı mı,
- timeout mu oldu?

Bu geri bildirim, özellikle arm/disarm, emergency stop, mission start, scenario start, manual control ve mode change gibi kritik komutlarda çok önemlidir.

### 🤖 Ground Station AI Assistant

Ground Station tarafında AI katmanı, operatöre destek olan bir görev ve sistem yorumlama asistanı olarak konumlanabilir.

Bu asistanın görevi aracı doğrudan kontrol etmek değildir.  
Asıl hedef; karmaşık runtime durumlarını, görev akışını, telemetry verisini ve diagnostics bilgilerini operatör için daha anlaşılır hale getirmektir.

Ground Station AI Assistant şu konularda yardımcı olabilir:

- mevcut görevi özetlemek,
- aracın neden belirli bir moda geçtiğini yorumlamak,
- sensor health problemlerini açıklamak,
- telemetry trendlerini yorumlamak,
- görev planı için öneriler sunmak,
- riskli durumları operatöre anlaşılır şekilde anlatmak,
- test sonrası kısa operasyon raporu üretmek,
- runtime context üzerinden “şu an ne oluyor?” sorusuna cevap vermek.

AI asistanı, sistemin üstünde kontrolsüz bir otorite değildir.  
Önerileri safety, authority ve operatör onayıyla sınırlandırılmış bir karar destek katmanı olarak düşünülmelidir.

### 🤝 Çoklu Araç ve Filo Hazırlığı

Hydronom’un Ops / Ground Station ekosistemi uzun vadede tek araçla sınırlı kalmamalıdır.

Aynı operasyon merkezinden birden fazla Hydronom aracının izlenebilmesi, görev paylaşımı yapılabilmesi ve araç durumlarının karşılaştırılabilmesi hedeflenebilir.

Bu vizyon için Ground Station tarafında şu hazırlıklar önemlidir:

- vehicle id bazlı snapshot ayrımı,
- çoklu araç bağlantı durumu,
- araç bazlı telemetry ve health izleme,
- ortak world model görünürlüğü,
- görev paylaşımı veya görev atama altyapısı,
- araç capability bilgisinin görünür olması,
- her araç için ayrı command / ACK/NACK takibi,
- filo düzeyinde risk ve görev durumu özeti.

Bu yaklaşım sayesinde Hydronom yalnızca tek bir aracın runtime’ı olmaktan çıkıp, birden fazla otonom aracın birlikte izlenebildiği ve yönetilebildiği bir operasyon ekosistemine doğru büyüyebilir.

### 🧭 Operasyon Ekosistemi Olarak Hydronom

Ground Station ve Ops katmanı, Hydronom’un gerçek dünyaya açılan yüzüdür.

Runtime içeride ne kadar güçlü olursa olsun, operatör onu anlayamıyorsa sistemin güvenilirliği eksik kalır.  
Bu yüzden Hydronom’da operasyon görünürlüğü, telemetry, world model, command feedback, diagnostics ve AI destekli yorumlama birlikte düşünülür.

Uzun vadede hedef şudur:

> **Hydronom yalnızca otonom çalışan bir araç sistemi değil; araçların izlenebildiği, anlaşılabildiği, güvenli şekilde komutlandırılabildiği ve birlikte yönetilebildiği bir operasyon ekosistemi olmalıdır.**

---

## 🔌 Gömülü Sistem ve Pico 2W Firmware

Hydronom’un gerçek donanım tarafında gömülü sistemler önemli bir rol oynar.

Yüksek seviye otonomi, görev yönetimi, karar verme, güvenlik, telemetri, AI ve gateway mantığı C# runtime tarafında yürütülürken; zaman hassas, düşük seviye ve donanıma yakın işler MCU tarafına devredilebilir.

Bu ayrım Hydronom’un daha temiz bir mimariyle gerçek araca bağlanmasını sağlar.

Gömülü katmanın temel görevi, yüksek seviye runtime ile fiziksel dünya arasında güvenilir bir köprü kurmaktır.

Bu katmanda özellikle Raspberry Pi Pico 2W gibi mikrodenetleyiciler şu amaçlarla kullanılabilir:

- ESC / motor kontrolü,
- PWM sinyali üretimi,
- manuel kontrol entegrasyonu,
- actuator gateway davranışı,
- sensör node mimarisi,
- USB-UART üzerinden veri aktarımı,
- düşük seviye donanım sağlık takibi,
- gerçek araç ile Hydronom runtime arasında sade ve güvenilir bağlantı kurma.

Bu yaklaşımda Pico / MCU tarafı otonominin ana karar vericisi değildir.

Ana karar, görev, güvenlik ve kontrol stratejisi Hydronom runtime tarafında kalır.  
Pico / MCU ise runtime’dan gelen güvenli ve sınırlandırılmış komutları donanım seviyesinde uygulanabilir sinyallere dönüştürür.

Bu ayrım sayesinde sistem hem daha modüler olur hem de donanım tarafındaki değişiklikler yüksek seviye otonomi mimarisini bozmaz.

### ⚙️ Pico 2W Motor / ESC Kontrol Rolü

Hydronom’da motor ve ESC kontrolü, yüksek seviye runtime ile düşük seviye donanım arasında dikkatli bir ayrım gerektirir.

C# runtime tarafı;

- görev durumunu,
- karar mantığını,
- güvenlik sınırlarını,
- actuator allocation sonucunu,
- motor / thruster komutlarını,
- emergency stop veya fail-safe kararlarını

üretir.

Pico / MCU tarafı ise bu komutları gerçek donanım sinyallerine dönüştürür.

Bu yaklaşımda Pico 2W şu görevleri üstlenebilir:

- PWM sinyali üretmek,
- ESC sinyal aralığını uygulamak,
- motor kanallarını sürmek,
- gelen komutları parse etmek,
- geçersiz komutları reddetmek,
- bağlantı koparsa güvenli moda geçmek,
- emergency stop durumunda motorları durdurmak,
- manuel kontrol girdilerini düşük seviyede okuyabilmek,
- runtime ile seri haberleşme üzerinden komut alışverişi yapmak.

Bu sayede yüksek seviye Hydronom runtime doğrudan pin seviyesine gömülmez.  
Runtime; güvenli, anlamlı ve sınırlandırılmış actuator komutları üretir.  
Pico ise bu komutları gerçek PWM / ESC dünyasına taşır.

### 🦀 Rust Firmware Yaklaşımı

Pico 2W firmware tarafında Rust kullanımı, Hydronom’un düşük seviye kontrol katmanını daha güvenli ve düzenli hale getirmek için önemli bir adımdır.

Rust tarafındaki hedef yalnızca motor döndürmek değildir.  
Hedef; okunabilir, genişletilebilir, hata ihtimali azaltılmış ve gelecekte manuel kontrol / sensör node / actuator feedback gibi özelliklerle büyüyebilecek bir gömülü firmware altyapısı oluşturmaktır.

Rust firmware yaklaşımı şu avantajları sağlar:

- daha güvenli bellek yönetimi,
- tip güvenliği,
- daha temiz modüler yapı,
- düşük seviye donanım kontrolünde daha kontrollü akış,
- embedded geliştirme için güçlü paket ekosistemi,
- ileride farklı Pico node’ları için ortak mimari kurabilme.

Hydronom tarafında Pico firmware’in rolü, runtime’ın yerine geçmek değildir.  
Firmware, yüksek seviye otonomi kararlarını uygulayan düşük seviye güvenilir yürütücü katman olarak konumlanır.

### 🔄 Runtime → Pico Komut Akışı

Motor kontrol akışı genel olarak şu şekilde düşünülebilir:

```text
Hydronom Runtime
        ↓
Decision / Control / Safety Limiter
        ↓
Actuator Allocation
        ↓
Motor / Thruster Command Frame
        ↓
Serial / USB-UART Link
        ↓
Pico 2W Firmware
        ↓
PWM / ESC Output
        ↓
Motor / Thruster
```

Bu akışta runtime tarafı karar ve güvenlikten sorumludur.  
Pico tarafı ise kendisine gelen komutları gerçek donanıma uygular.

Böylece sistemde sorumluluklar netleşir:

- Runtime ne yapılacağını belirler.
- Safety katmanı komutun güvenli olup olmadığını sınırlar.
- Actuator allocation hangi motorun ne yapacağını hesaplar.
- Pico firmware komutu PWM / ESC sinyaline dönüştürür.
- Motor / thruster fiziksel hareketi üretir.

Bu ayrım Hydronom’un hem daha güvenli hem de daha sürdürülebilir bir gerçek donanım mimarisiyle büyümesini sağlar.

### 🎮 Manuel Kontrol Entegrasyonu

Hydronom’da manuel kontrol, otonom sistemin dışında rastgele bağlanan ayrı bir yapı olarak düşünülmemelidir.

Manuel kontrol de güvenlik, authority, runtime mode, actuator limiter ve emergency stop mantığıyla uyumlu şekilde ele alınmalıdır.

Bu yaklaşımda manuel kontrol girdileri;

- joystick,
- switch,
- buton,
- emergency stop,
- potansiyometre,
- rotary encoder,
- ground station komutu,
- RF / kumanda alıcısı

gibi kaynaklardan gelebilir.

Pico / MCU tarafı bazı manuel girişleri düşük seviyede okuyabilir.  
Ancak bu girişlerin aracı doğrudan ve kontrolsüz şekilde sürmesi yerine, Hydronom runtime tarafından bilinen, izlenen ve güvenlikten geçirilen bir kontrol akışına dahil olması hedeflenir.

Manuel kontrol entegrasyonunda önemli noktalar:

- manuel kontrol aktif mi,
- operatör yetkili mi,
- emergency stop önceliği var mı,
- otonom görev askıya alınmalı mı,
- komut güvenli sınırlar içinde mi,
- bağlantı koparsa motorlar ne yapmalı,
- manuel komut telemetry ve diagnostics içinde görünmeli mi?

Bu sayede manuel kontrol, otonom sistemle yarışan ayrı bir devre değil; Hydronom’un güvenlik ve operasyon mimarisi içinde tanımlı bir çalışma modu haline gelir.

### 📡 Pico / MCU Sensör Node Mimarisi

Hydronom’un gerçek donanım hedeflerinden biri, birçok sensörü Pico / MCU tabanlı küçük node’lar üzerinden sisteme bağlayabilmektir.

Bu yaklaşımda her sensör veya sensör grubu kendi node davranışına sahip olabilir:

- IMU node,
- GPS node,
- derinlik sensörü node,
- güç / batarya ölçüm node’u,
- çevresel sensör node’u,
- actuator feedback node’u,
- özel görev sensörü node’u.

Pico / MCU node;

- sensörü okur,
- gerekirse basit doğrulama yapar,
- zaman bilgisi ekler,
- bağlantı durumunu takip eder,
- veriyi seri protokol üzerinden Hydronom runtime’a gönderir.

Hydronom runtime ise bu veriyi doğrudan ham byte olarak değil, standart sensör sample modeline dönüştürerek kullanır.

Bu mimari şu avantajları sağlar:

- yüksek seviye bilgisayardaki pin / bus karmaşası azalır,
- sensörler daha modüler hale gelir,
- arızalı sensör node’u izole edilebilir,
- araçlar arasında sensör node’ları tekrar kullanılabilir,
- sensör backend’leri daha kolay değiştirilebilir,
- plug-and-play / discovery mimarisi için zemin oluşur.

### 🔌 USB-UART Protokol Yaklaşımı

Pico / MCU ile Hydronom runtime arasındaki temel haberleşme yolu USB-UART veya benzeri seri bağlantılar olabilir.

Bu bağlantı üzerinden hem actuator komutları hem de sensör verileri taşınabilir.  
Ancak bunun güvenilir olabilmesi için seri haberleşme yalnızca “satır satır rastgele metin gönderme” seviyesinde kalmamalıdır.

Uzun vadede seri protokol şu özellikleri taşımalıdır:

- belirli frame başlangıç bilgisi,
- protokol versiyonu,
- mesaj tipi,
- kaynak node kimliği,
- sequence bilgisi,
- timestamp,
- payload uzunluğu,
- checksum / CRC,
- hata durumları,
- heartbeat / health mesajı,
- emergency stop önceliği,
- command ACK cevabı.

Bu sayede Hydronom runtime, Pico’dan gelen verinin hangi node’a ait olduğunu, ne zaman üretildiğini, bozulup bozulmadığını ve hangi sensör tipini temsil ettiğini anlayabilir.

### 🧱 Yüksek Seviye ve Düşük Seviye Sorumluluk Ayrımı

Hydronom’da yüksek seviye runtime ile düşük seviye firmware arasındaki sınır net olmalıdır.

Yüksek seviye C# runtime şunlardan sorumludur:

- görev yönetimi,
- karar verme,
- güvenlik mantığı,
- sensor fusion,
- telemetry,
- command authority,
- actuator allocation,
- runtime diagnostics,
- AI ve Ground Station entegrasyonu.

Pico / MCU firmware tarafı ise şunlardan sorumludur:

- PWM üretimi,
- ESC / motor sinyali,
- düşük seviye giriş okuma,
- sensör node davranışı,
- seri veri aktarımı,
- basit bağlantı health takibi,
- emergency stop gibi düşük seviye güvenli durumlara hızlı tepki.

Bu ayrım sayesinde Hydronom hem yüksek seviyede güçlü bir otonomi mimarisi kurabilir hem de gerçek donanımın zaman hassas ihtiyaçlarını daha doğru yerde çözebilir.

---

## 🧪 Simülasyon, Senaryo ve Test Altyapısı

Hydronom yalnızca gerçek donanım takılıyken çalışabilen bir sistem olarak düşünülmez.

Gerçek araç testleri değerlidir; fakat her geliştirme adımını doğrudan gerçek donanım üzerinde denemek hem yavaş hem de risklidir.  
Bu nedenle Hydronom’da simülasyon, senaryo ve smoke test altyapısı sistemin temel parçalarından biri olarak ele alınır.

Simülasyon tarafındaki amaç yalnızca ekranda hareket eden basit bir araç göstermek değildir.  
Asıl hedef, runtime’ın gerçek sistemde çalışacak karar, görev, sensör, kontrol, aktüasyon, telemetri ve güvenlik akışlarını mümkün olduğunca aynı mimari üzerinden test edebilmektir.

Hydronom simülasyon yaklaşımı şu prensiplere dayanır:

- gerçek runtime akışını bozmadan test yapabilmek,
- sensör yokken de sistem davranışını doğrulayabilmek,
- görev ve senaryo akışlarını tekrar tekrar çalıştırabilmek,
- world object, route point, hedef ve engel mantığını test edebilmek,
- actuator ve physics etkilerini gözlemleyebilmek,
- telemetry ve Gateway akışını doğrulayabilmek,
- güvenlik ve fail-safe durumlarını kontrollü şekilde deneyebilmek,
- gerçek donanım gelmeden önce yazılımın büyük kısmını olgunlaştırmak.

Bu yaklaşım sayesinde Hydronom, yalnızca “sahada deneyip gören” bir sistem olmaktan çıkar.  
Geliştirme, test, doğrulama ve iyileştirme süreçleri daha kontrollü hale gelir.

### 🌍 Simülasyon Dünyası

Hydronom’da simülasyon dünyası yalnızca aracın pozisyonunu tutan basit bir yapı olmamalıdır.

Uzun vadede simülasyon dünyası şu kavramları taşıyabilecek şekilde düşünülür:

- araç başlangıç durumu,
- görev hedefleri,
- route point’ler,
- checkpoint’ler,
- buoy’ler,
- engeller,
- no-go bölgeleri,
- safety zone’lar,
- görev nesneleri,
- çevresel etkiler,
- sensörlerin görebildiği veya göremediği nesneler,
- platforma özel hareket / direnç / performans etkileri.

Bu dünya modeli sayesinde runtime, yalnızca bir koordinata gitmek yerine görev bağlamı olan bir operasyon alanında çalışabilir.

Simülasyon dünyası, Gateway ve Ops katmanına da aktarılabilir.  
Böylece operatör simülasyon sırasında yalnızca aracın konumunu değil, aracın içinde bulunduğu görev ortamını da görebilir.

### 🗺️ Senaryo Sistemi

Hydronom’da senaryo sistemi, runtime davranışını kontrollü görev ortamlarında test etmek için kullanılır.

Bir senaryo yalnızca “araç şuradan başlasın, şuraya gitsin” bilgisinden ibaret değildir.  
Senaryo; görev hedeflerini, dünya nesnelerini, rota noktalarını, engelleri, güvenlik bölgelerini ve test koşullarını tanımlayan operasyonel bir yapı olarak düşünülür.

Senaryo sistemi şu amaçlarla kullanılabilir:

- waypoint takip testleri,
- obstacle avoidance testleri,
- görev başlangıç / bitiş akışı,
- aktif hedef değişimi,
- route point doğrulaması,
- world object görünürlüğü,
- mission state üretimi,
- actuator ve control davranışı,
- telemetry ve Gateway akışı,
- safety / fail-safe senaryoları.

Bu yapı sayesinde aynı görev tekrar tekrar çalıştırılabilir ve runtime değişikliklerinin davranışı nasıl etkilediği daha net gözlemlenebilir.

### 🧭 Bilinen Dünya ve Keşfedilen Dünya Ayrımı

Simülasyonda senaryo dosyası bütün dünya nesnelerini tanımlayabilir.  
Fakat gerçek araç, dünyadaki her şeyi doğrudan bilmez. Gerçek araç çevresini sensörleriyle gözlemler, eksik bilgiyle karar verir ve zaman içinde kendi operasyonel dünya modelini oluşturur.

Bu yüzden Hydronom’da uzun vadeli hedeflerden biri şu ayrımı desteklemektir:

- **Known World:** Senaryo dosyasında veya sistem konfigürasyonunda önceden bilinen dünya.
- **Observed World:** Sensörlerin gerçekten algıladığı dünya.
- **Fused World:** Birden fazla gözlem ve kaynak üzerinden oluşturulan işlenmiş dünya modeli.
- **Operational World:** Runtime’ın karar, görev ve güvenlik katmanlarında kullandığı dünya temsili.

Bu ayrım özellikle simülasyonun gerçekçi olması için önemlidir.

Araç simülasyonda bile isterse tüm senaryo nesnelerini doğrudan bilmiyormuş gibi başlatılabilir.  
Bu durumda sensör backend’leri, aracın görüş alanı, menzili, veri kalitesi ve algılama limitlerine göre nesneleri runtime’a bildirir.

Böylece araç, gerçek dünyadaki gibi çevresini zamanla keşfeder.

### 🔎 Discovery Mode / Unknown World Yaklaşımı

Discovery mode, aracın göreve tüm dünyayı bilerek değil, sınırlı bilgiyle başlamasını sağlar.

Bu modda senaryo dosyasında bütün nesneler tanımlı olsa bile runtime’a yalnızca görev için gerekli başlangıç bilgileri verilebilir.  
Diğer nesneler, simülasyon sensörleri tarafından algılandıkça Observed World veya Fused World içine girer.

Bu yaklaşım şu testleri mümkün kılar:

- sensör menzili etkisi,
- görüş alanı kısıtı,
- engel geç fark edilirse karar davranışı,
- bilinmeyen nesne keşfi,
- görev sırasında dünya modelinin güncellenmesi,
- perception / fusion / decision entegrasyonu,
- local planner davranışı,
- güvenli yavaşlama veya kaçınma tepkileri.

Bu sayede Hydronom simülasyonu yalnızca “mükemmel bilgiyle çalışan” bir demo olmaktan çıkar.  
Araç, gerçek otonomiye daha yakın şekilde eksik, gecikmeli ve gürültülü bilgiyle çalışabilir.

### 🧭 Physics Truth ve Operational State Ayrımı

Simülasyon ortamında aracın gerçek fiziksel durumu sistem tarafından bilinebilir.  
Fakat gerçek dünyada araç, kendi durumunu yalnızca sensörler, tahminler ve dış kaynaklardan gelen bilgilerle anlayabilir.

Bu nedenle Hydronom’da simülasyon tarafında şu ayrım önemlidir:

- **Physics Truth:** Simülasyon motorunun bildiği gerçek fiziksel durumdur.
- **Simulation Truth:** Senaryo ve simülasyon ortamının sahip olduğu tam bilgiye yakın durumdur.
- **Observed State:** Sensörlerin ölçtüğü veya algıladığı durumdur.
- **Fused State:** Birden fazla sensör ve kaynak üzerinden üretilen tahmini durumdur.
- **Operational State:** Runtime’ın görev, karar, güvenlik ve kontrol katmanlarında kullandığı güvenilir araç durumudur.

Bu ayrım sayesinde simülasyon daha gerçekçi hale gelir.

Araç, simülasyon içinde bile doğrudan physics truth bilgisini kullanmak zorunda değildir.  
Bunun yerine simüle edilmiş sensörler üzerinden gözlem yapabilir, bu gözlemler fusion katmanına girebilir ve runtime kendi operational state bilgisini bu verilerden üretebilir.

Bu yaklaşım, gerçek araç davranışına daha yakın testler yapılmasını sağlar.

### 📡 Simüle Sensörler

Hydronom’da simülasyon sensörleri yalnızca “sahte veri üretici” olarak düşünülmemelidir.

Simüle sensörlerin amacı, gerçek sensörlerin davranışına yakın veri kaynakları oluşturmaktır.

Bu sensörler şu özellikleri modelleyebilir:

- ölçüm frekansı,
- gecikme,
- gürültü,
- drift,
- görüş alanı,
- menzil,
- algılama limiti,
- veri kaybı,
- stale sample durumu,
- kalite düşüşü,
- bağlantı kopması,
- simülasyon / gerçek kaynak ayrımı.

Örneğin simülasyonda bir LiDAR veya kamera backend’i, senaryo dosyasındaki bütün nesneleri doğrudan runtime’a vermek yerine yalnızca aracın görüş alanına ve sensör menziline giren nesneleri algılayabilir.

Bu sayede runtime, kusursuz bilgiye sahip gibi değil; gerçek sensörlerden gelen sınırlı ve bazen hatalı verilerle çalışan bir sistem gibi davranır.

### 🌊 Platforma Özel Fizik ve Direnç Modelleri

Hydronom platform bağımsız bir çekirdek hedeflediği için, fiziksel etkiler doğrudan tek bir araç tipine sabitlenmemelidir.

Farklı platformlar farklı direnç ve hareket etkilerine sahiptir:

- su üstü araçlarında hidrodinamik direnç,
- su altı araçlarında sürükleme ve kaldırma / batma etkileri,
- hava araçlarında aerodinamik sürükleme,
- kara araçlarında zemin sürtünmesi,
- yelkenli platformlarda rüzgâr ve yelken kuvvetleri,
- roket benzeri sistemlerde farklı itki ve ortam etkileri.

Hydronom çekirdeği bu fiziksel etkileri doğrudan ezberlememelidir.  
Bunun yerine araç profili, physics model, performance model veya provider katmanları üzerinden genel runtime’a sonuç üretmelidir.

Çekirdek sistem için önemli olan şudur:

- araca etki eden kuvvetler,
- torklar,
- hız sınırları,
- güç ihtiyacı,
- direnç / opposing force,
- performans zarfı,
- güvenli çalışma limitleri.

Bu yaklaşım, Hydronom’un yalnızca tekneye özel bir fizik sistemi olmaktan çıkıp farklı araç tiplerine uyarlanabilir bir otonom runtime olmasını destekler.

### 🧪 Simülasyonun Test Değeri

Simülasyonun değeri yalnızca aracın hareketini görmek değildir.

Hydronom simülasyonu şu soruları test etmek için kullanılabilir:

- Görev doğru başlıyor mu?
- Araç hedefe yaklaşırken doğru yavaşlıyor mu?
- Hedefe varınca görev adımı tamamlanıyor mu?
- Engel algılandığında karar değişiyor mu?
- Sensör verisi bayatladığında sistem güvenli moda geçiyor mu?
- Telemetry Gateway’e doğru ulaşıyor mu?
- Ops arayüzünde dünya modeli doğru görünüyor mu?
- Actuator allocation beklenen çıktıyı üretiyor mu?
- Safety limiter komutu doğru sınırlıyor mu?
- Secure command geldiğinde runtime intent doğru oluşuyor mu?
- ACK/NACK sonucu beklenen şekilde dönüyor mu?

Bu nedenle Hydronom’da simülasyon, yalnızca görsel bir demo değil; runtime davranışını doğrulayan mühendislik test ortamıdır.

### ✅ Smoke Test / Verification Altyapısı

Hydronom büyüdükçe yalnızca `dotnet build` sonucunun başarılı olması yeterli değildir.

Build almak, kodun derlenebildiğini gösterir.  
Fakat otonom sistemlerde asıl kritik olan şey; haberleşme, güvenlik, runtime, telemetry, gateway, scenario, AI ve ground station gibi ana akışların gerçekten birlikte çalışıp çalışmadığını doğrulamaktır.

Bu yüzden Hydronom’da farklı katmanlara özel smoke test projeleri kullanılır.

Smoke test yaklaşımının amacı şudur:

- kritik veri akışlarının bozulmadığını hızlıca görmek,
- yeni mimari değişikliklerden sonra ana pipeline’ları doğrulamak,
- runtime ve communication zincirlerini gerçekçi senaryolarla test etmek,
- güvenlik kontrollerinin gerçekten çalıştığını görmek,
- Gateway / Ops tarafına veri akışının ulaştığını doğrulamak,
- büyük refactorlardan sonra sistemin hâlâ ayakta olduğunu kanıtlamak.

### 🧪 Öne Çıkan Smoke Test Alanları

Hydronom’da doğrulama yapılan ana alanlar şunlardır:

- communication core,
- binary envelope codec,
- CRC32 integrity check,
- HMAC-SHA256 doğrulama,
- anti-replay mekanizması,
- priority queue,
- adaptive bandwidth policy,
- compact telemetry,
- telemetry delta builder,
- telemetry envelope adapter,
- secure command pipeline,
- command authority validation,
- secure command receiver,
- runtime command bridge,
- ACK / NACK pipeline,
- InMemory transport,
- TCP packet transport,
- runtime telemetry pipeline,
- runtime scenario pipeline,
- diagnostics pipeline,
- Gateway runtime ingress,
- Ground Station smoke test,
- AI / Ground Station assistant smoke test.

Bu testler, Hydronom’un yalnızca tek bir modülünün değil; modüller arası ana bağlantıların da sağlıklı kaldığını gösterir.

### 🔐 Communication Smoke Testleri

Secure communication tarafında yapılan testler özellikle kritiktir.

Çünkü Hydronom’da uzaktan komut, telemetry, ACK/NACK ve runtime intent akışları güvenli haberleşme zincirinden geçer.

Bu testlerde doğrulanan başlıca davranışlar:

- binary envelope encode/decode,
- CRC ile bozuk paket yakalama,
- HMAC ile payload / metadata değişikliğini yakalama,
- replay packet reddi,
- wrong vehicle id reddi,
- wrong content type reddi,
- command payload decode,
- ACK/NACK payload decode,
- authority policy reddi,
- emergency / critical / high priority ayrımı,
- TCP üzerinden gerçek localhost roundtrip.

Bu doğrulamalar sayesinde haberleşme katmanı yalnızca teorik olarak değil, çalışan testlerle de güvence altına alınır.

### 📡 Runtime / Scenario / Gateway Smoke Testleri

Runtime tarafındaki testler, sistemin görev ve çalışma akışını doğrulamak için kullanılır.

Bu testlerde şu davranışlar kontrol edilebilir:

- runtime pipeline ayağa kalkıyor mu,
- sensör sample akışı geliyor mu,
- scenario başlatılabiliyor mu,
- görev hedefleri işleniyor mu,
- telemetry frame üretiliyor mu,
- diagnostics bilgisi oluşuyor mu,
- runtime world object bilgisi Gateway’e taşınıyor mu,
- snapshot endpoint beklenen alanları içeriyor mu,
- Ops tarafının ihtiyaç duyduğu DTO modelleri doluyor mu.

Bu testler özellikle Hydronom’un büyüyen Gateway / Ops ekosistemi için önemlidir.

Çünkü runtime’ın doğru çalışması kadar, bu durumun dışarıdan doğru gözlemlenebilmesi de gereklidir.

### 🧭 Verification Kültürü

Hydronom’da smoke testler yalnızca “test yazmış olmak” için bulunmaz.

Bu testler, geliştirme sürecinde güvenli ilerlemenin bir parçasıdır.

Büyük bir mimari değişiklikten sonra şu sorular hızlıca cevaplanabilmelidir:

- Sistem hâlâ build alıyor mu?
- Secure command hattı çalışıyor mu?
- ACK/NACK bozuldu mu?
- TCP transport hâlâ packet taşıyor mu?
- Compact telemetry decode ediliyor mu?
- Runtime scenario akışı sağlam mı?
- Gateway snapshot doğru doluyor mu?
- Ground Station tarafı temel context’i okuyabiliyor mu?

Bu yaklaşım, Hydronom’un büyürken kontrolsüz şekilde kırılmasını engellemeye yardımcı olur.

---

## ✨ Öne Çıkan Yetenekler

Hydronom’un güncel mimarisi, yalnızca tek bir aracı hareket ettirmeye değil; güvenli, gözlemlenebilir, genişleyebilir ve platform bağımsız bir otonom sistem omurgası oluşturmaya odaklanır.

Öne çıkan yeteneklerden bazıları şunlardır:

- 🧩 **Modüler C# Primary Runtime**  
  Sensör, füzyon, görev, karar, kontrol, aktüasyon, güvenlik, telemetri ve haberleşme katmanlarının ayrıştırıldığı genişleyebilir runtime yapısı.

- 🌍 **Platform Bağımsız Otonomi Yaklaşımı**  
  Su üstü, su altı, yelkenli, kara aracı, VTOL veya farklı deneysel platformlara uyarlanabilecek ortak otonomi çekirdeği.

- 🧭 **3D / 6DOF Durum Modeli**  
  Konum, yönelim, hız, açısal hız, kuvvet ve tork etkilerini daha genel bir uzaysal hareket mantığıyla ele alma hedefi.

- 📡 **Backend Değiştirilebilir Sensör Mimarisi**  
  Gerçek donanım, simülasyon, replay, Pico / MCU node, USB-UART veya network kaynaklı sensör verilerini aynı üst seviye modele bağlayabilme yaklaşımı.

- 🧠 **Fusion / State Estimation Altyapısı**  
  Farklı kaynaklardan gelen veriyi zaman, kalite, health ve güvenilirlik bilgisiyle birlikte anlamlı araç durumuna dönüştürme hedefi.

- 🛡️ **Secure Binary Communication Pipeline**  
  Binary command payload, binary ACK/NACK payload, compact telemetry, HMAC-SHA256, CRC32, anti-replay ve authority validation ile güvenli haberleşme omurgası.

- 📉 **Compact / Delta Telemetry**  
  Field mask, quantization ve delta mantığıyla daha küçük, seçilebilir ve bant genişliğine duyarlı telemetri akışı.

- 🧾 **ACK / NACK Yaşam Döngüsü**  
  Komutların yalnızca gönderilmesini değil; decode, security, authority, runtime bridge, safety gate ve execution aşamalarının takip edilebilmesini sağlayan geri bildirim modeli.

- 🚦 **Priority Queue ve Adaptive Bandwidth**  
  Emergency, Critical, High, Normal, Low ve Bulk mesaj öncelikleriyle bağlantı kalitesine göre haberleşme davranışını uyarlama yaklaşımı.

- 🌐 **TCP ve InMemory Transport Altyapısı**  
  Secure packet akışını hem test ortamında hem de gerçek localhost TCP server/client yapısında doğrulayabilen transport katmanı.

- 🧪 **Simülasyon ve Senaryo Desteği**  
  Görev hedefleri, route point’ler, world object’ler, engeller, physics truth, operational state ve keşfedilen dünya yaklaşımına temel oluşturan simülasyon mimarisi.

- 🖥️ **Gateway / Snapshot / WebSocket Ekosistemi**  
  Runtime’dan gelen telemetry, mission, actuator, world, sensor ve diagnostics frame’lerini dış sistemlere okunabilir snapshot ve canlı yayın olarak sunabilme yaklaşımı.

- 🗺️ **Ops / Ground Station Görünürlüğü**  
  Araç durumu, görev akışı, dünya modeli, sensör health, actuator state, diagnostics, risk ve command feedback bilgisini operatöre anlaşılır şekilde sunma hedefi.

- 🤖 **AI Destekli Planlama ve Yorumlama**  
  Runtime context, görev durumu, telemetry ve diagnostics verilerini yorumlayarak operatöre görev planlama ve açıklanabilir karar desteği sunabilecek AI katmanı.

- 🔌 **Pico 2W / MCU Firmware Entegrasyonu**  
  Motor / ESC kontrolü, PWM üretimi, manuel kontrol, sensör node mimarisi ve USB-UART veri aktarımı için düşük seviye gömülü sistem yaklaşımı.

- ✅ **Geniş Smoke Test Kültürü**  
  Communication, secure command, authority, ACK/NACK, TCP transport, telemetry, runtime, scenario, diagnostics, gateway, ground station ve AI katmanlarını doğrulayan test altyapısı.

Bu yetenekler Hydronom’u yalnızca çalışan bir prototip olmaktan çıkarıp; gerçek donanım, simülasyon, güvenli haberleşme, operasyon arayüzü ve uzun vadeli platform bağımsızlık hedeflerini birlikte taşıyan bir otonom sistem platformuna dönüştürür.

---

## 🔄 Sistem Akışı

Hydronom’un çalışma akışı, yalnızca sensör verisi alıp motor komutu üretmekten ibaret değildir.

Sistem; sensörlerden gelen veriyi işler, araç durumunu üretir, görev bağlamını değerlendirir, karar verir, güvenlik sınırlarını uygular, aktüatör komutları oluşturur, dış dünyaya telemetri yayınlar ve gelen komutları güvenli haberleşme hattından geçirerek runtime intent’e dönüştürür.

Genel runtime akışı şu şekilde düşünülebilir:

```text
Sensors / Simulation / Replay / Pico Nodes
        ↓
Sensor Runtime
        ↓
Timing / Quality / Health Validation
        ↓
Fusion / State Estimation
        ↓
Operational Vehicle State
        ↓
Analysis Layer
        ↓
Task / Mission Layer
        ↓
Decision Layer
        ↓
Control / Actuation Layer
        ↓
Safety Limiter / Allocation
        ↓
Motor / Thruster / Servo / Pico Firmware
        ↓
Telemetry / Diagnostics / Gateway / Ops
```

### 1. 📥 Veri Kaynakları Sisteme Girer

Hydronom farklı veri kaynaklarından beslenebilir:

- gerçek sensörler,
- simülasyon backend’leri,
- replay kayıtları,
- Pico / MCU sensör node’ları,
- dış pose sağlayıcılar,
- runtime world model,
- Gateway veya başka sistemlerden gelen yardımcı veriler.

Bu veri kaynakları doğrudan karar katmanına verilmez.  
Önce Sensor Runtime tarafından kimlik, zaman, kalite, health ve kaynak bilgisiyle birlikte anlamlı hale getirilir.

### 2. 🧾 Sensör Verisi Doğrulanır

Gelen sensör verisi yalnızca “değer” olarak ele alınmaz.

Sistem şu bilgileri değerlendirir:

- veri ne zaman ölçüldü,
- runtime’a ne zaman ulaştı,
- veri bayat mı,
- sensör sağlıklı mı,
- backend güvenilir mi,
- sample kalitesi yeterli mi,
- sensör simülasyon mu gerçek mi,
- bağlantı kopması var mı?

Bu aşama, fusion ve decision katmanlarının daha güvenilir bilgiyle çalışmasına yardımcı olur.

### 3. 🧠 Fusion / State Estimation Araç Durumu Üretir

Farklı sensörlerden gelen veri fusion / state estimation katmanında birleştirilir.

Amaç yalnızca tek bir konum değeri üretmek değildir.  
Hydronom’un hedeflediği state modeli; konum, yönelim, hız, açısal hız, derinlik, confidence, sensor quality ve zaman bilgisini birlikte ele alabilecek şekilde düşünülür.

Bu aşamanın çıktısı, runtime’ın karar ve görev katmanlarında kullanacağı operasyonel araç durumudur.

### 4. 🔎 Analysis Katmanı Durumu Yorumlar

Analysis katmanı, aracın ve çevrenin durumunu yorumlar.

Bu katman şu sorulara cevap arar:

- hedefe ne kadar yakınız,
- rota sapması var mı,
- risk seviyesi yükseliyor mu,
- sensörler güvenilir mi,
- engel veya görev nesnesi var mı,
- sistem health durumu nasıl,
- bağlantı veya telemetry problemi var mı,
- araç yavaşlamalı mı,
- fail-safe ihtiyacı var mı?

Bu yorumlar, karar ve güvenlik katmanlarının daha bilinçli davranmasını sağlar.

### 5. 🗺️ Task / Mission Katmanı Görev Bağlamını Sağlar

Task / Mission katmanı, aracın hangi görevi yürüttüğünü ve görevin hangi aşamasında olduğunu takip eder.

Bu katman;

- aktif hedefi,
- görev adımını,
- route point sırasını,
- scenario durumunu,
- görev tamamlanma koşullarını,
- pause / resume / abort durumlarını,
- manuel devralma etkisini

yönetebilir.

Görev katmanı ne yapılması gerektiğini belirler; karar ve kontrol katmanları bunun nasıl davranışa dönüşeceğini belirler.

### 6. ⚖️ Decision Katmanı Davranış Seçer

Decision katmanı, analysis çıktısı ve görev bağlamına göre davranış üretir.

Örneğin sistem;

- hedefe ilerleyebilir,
- yavaşlayabilir,
- yaklaşma moduna geçebilir,
- bekleyebilir,
- obstacle avoidance uygulayabilir,
- manuel kontrolü önceliklendirebilir,
- fail-safe moda geçebilir,
- görev adımını tamamlayabilir.

Decision katmanı doğrudan motor sürmez.  
Daha üst seviye bir davranış veya hareket isteği üretir.

### 7. ⚙️ Control / Actuation Komut Üretir

Control / Actuation katmanı, decision çıktısını uygulanabilir aktüatör komutlarına dönüştürür.

Bu aşamada;

- hareket isteği yorumlanır,
- araç geometrisi dikkate alınır,
- motor / thruster kabiliyetleri değerlendirilir,
- tek yönlü veya çift yönlü ESC desteği göz önüne alınır,
- 6DOF kuvvet / tork etkileri hesaba katılır,
- gerçek veya simülasyon aktüasyonu hedeflenir.

Bu katman, davranış kararını fiziksel dünyaya uygulanabilir komuta yaklaştırır.

### 8. 🛡️ Safety Limiter ve Allocation Uygulanır

Motor veya thruster komutları doğrudan donanıma gönderilmez.

Önce güvenlik ve sınırlandırma katmanlarından geçer:

- ani komut değişimleri sınırlanır,
- maksimum thrust / throttle sınırları uygulanır,
- emergency stop kontrol edilir,
- sensör bayatlığı veya bağlantı kaybı değerlendirilir,
- platform kabiliyetleri dikkate alınır,
- güvenli duruş davranışları uygulanır.

Bu aşama, Hydronom’un sahada daha kontrollü ve güvenli çalışması için kritiktir.

### 9. 🔌 Donanım veya Simülasyon Aktüasyonu Gerçekleşir

Üretilen güvenli komutlar hedef ortama göre uygulanır:

- gerçek motor / ESC / servo,
- Pico 2W veya başka MCU firmware,
- serial / USB-UART actuator gateway,
- mock motor controller,
- simülasyon physics modeli,
- virtual actuator backend.

Bu ayrım sayesinde aynı üst seviye runtime hem gerçek donanımda hem simülasyonda benzer mimariyle çalışabilir.

### 10. 📤 Telemetry, Diagnostics ve Gateway Akışı Yayınlanır

Runtime kendi iç durumunu dış dünyaya aktarır.

Bu aşamada;

- telemetry frame’leri,
- mission state,
- actuator state,
- world state,
- sensor health,
- diagnostics,
- risk ve karar bilgisi,
- command ACK/NACK sonucu

Gateway ve Ops katmanına taşınabilir.

Bu sayede operatör yalnızca aracın hareketini değil, sistemin neden o şekilde davrandığını da gözlemleyebilir.

### 11. 🔐 Secure Command Akışı Runtime’a Girer

Dış dünyadan gelen komutlar doğrudan uygulanmaz.

Komut akışı şu kontrollerden geçer:

- binary packet decode,
- CRC kontrolü,
- HMAC doğrulaması,
- anti-replay kontrolü,
- envelope / payload validation,
- command authority validation,
- runtime command bridge,
- safety gate,
- ACK/NACK üretimi.

Bu yapı sayesinde Hydronom, dış komutları güvenli, izlenebilir ve açıklanabilir bir süreçten geçirerek işler.

---

## 🚤 Hedeflenen Kullanım Alanları

Hydronom’un hedefi yalnızca tek bir yarış aracı veya tek bir görev senaryosu için çalışan dar kapsamlı bir yazılım olmak değildir.

Sistem; modüler runtime, sensör mimarisi, güvenli haberleşme, telemetri, simülasyon, yer istasyonu ve AI destekli yorumlama katmanlarıyla farklı otonom araç ve operasyon senaryolarında kullanılabilecek bir temel oluşturmayı hedefler.

Hydronom’un hedeflenen kullanım alanları şunlardır:

### 🚤 Otonom Su Üstü Araçları

Hydronom’un ilk ve en doğal kullanım alanlarından biri otonom su üstü araçlarıdır.

Bu kapsamda sistem;

- waypoint takibi,
- görev rotası izleme,
- hedefe yaklaşma,
- durma / bekleme davranışı,
- engel farkındalığı,
- telemetri aktarımı,
- yer istasyonu izleme,
- güvenli komut alma,
- motor / thruster kontrolü

gibi görevleri destekleyecek şekilde gelişmektedir.

Su üstü araçları, Hydronom’un hem yarışma hem de saha testi tarafındaki temel uygulama alanlarından biridir.

### 🌊 Su Altı Araçları

Hydronom’un uzun vadeli platform bağımsızlık hedefi açısından su altı araçları önemli bir test alanıdır.

Su altında 2D varsayımlar yeterli değildir.  
Derinlik, pitch, roll, Z ekseni hareketi, sınırlı haberleşme, sensör kısıtları ve görüş problemleri gibi konular daha ciddi hale gelir.

Bu nedenle Hydronom’un 3D / 6DOF state yaklaşımı, sensör health takibi, eksik sensörle çalışma ve simülasyon altyapısı su altı araçları için kritik önemdedir.

Su altı kullanımında hedeflenen konular:

- derinlik kontrolü,
- IMU ve depth sensor entegrasyonu,
- kamera / stereo vision tabanlı algılama,
- su altı görev senaryoları,
- sınırlı görüş ve sensör güvenilirliği,
- güvenli fail-safe davranışları,
- su altı thruster allocation mantığı.

### ⛵ Yelkenli ve Uzun Menzil Platformları

Hydronom’un haberleşme, telemetri ve enerji farkındalığı uzun vadede yelkenli veya uzun menzil görev platformları için de temel olabilir.

Bu tip sistemlerde;

- düşük güç tüketimi,
- bağlantı kopmalarına dayanıklılık,
- compact / delta telemetry,
- görev sürekliliği,
- rüzgâr ve çevresel etki modelleme,
- uzun süreli otonom karar davranışı,
- güvenilir health monitoring

gibi konular önem kazanır.

Hydronom’un adaptive bandwidth ve compact telemetry yaklaşımı, uzun menzil görevlerde özellikle değerli hale gelebilir.

### 🤖 Kara Aracı ve Paletli Robot Platformları

Hydronom’un çekirdeği yalnızca deniz ortamına kilitlenmemelidir.

Kara aracı, paletli robot veya tarımsal otonom platformlarda da benzer temel ihtiyaçlar vardır:

- araç durumu takibi,
- görev hedefleri,
- sensör entegrasyonu,
- aktüatör kontrolü,
- güvenli komut işleme,
- telemetri,
- yer istasyonu,
- simülasyon ve test.

Bu nedenle Hydronom’un platform bağımsız çekirdeği, ileride kara aracı benzeri robotik sistemler için de uyarlanabilir bir temel sağlayabilir.

### 🚁 VTOL / Hava Aracı Benzeri Platformlar

Hydronom’un 6DOF ve platform bağımsız state yaklaşımı, uzun vadede hava aracı benzeri platformlar için de düşünsel bir temel oluşturabilir.

Bu alan, çok daha yüksek güvenlik ve kontrol hassasiyeti gerektirir.  
Bu yüzden Hydronom’un mevcut hali doğrudan uçuş kontrol sistemi olarak konumlanmamalıdır.

Ancak uzun vadeli mimari açısından;

- 3D konum ve yönelim,
- görev planlama,
- telemetri,
- yer istasyonu,
- AI destekli görev yorumu,
- güvenli command pipeline,
- simülasyon ve diagnostics

gibi katmanlar hava aracı ekosistemlerinde de anlamlıdır.

### 🚀 Deneysel ve Özel Görev Platformları

Hydronom’un en önemli hedeflerinden biri, belirli bir araç tipine sıkışmadan özel görev platformlarına da temel oluşturabilmesidir.

Bu kapsamda sistem ileride;

- deneysel su altı roketi,
- özel görev robotları,
- araştırma platformları,
- modüler test araçları,
- hibrit hareket sistemleri,
- yarışma odaklı özel araçlar

için kullanılabilecek ortak bir runtime yaklaşımı sunabilir.

Bu tür platformlarda Hydronom’un en değerli yönü; görev, güvenlik, telemetri, haberleşme ve operasyon görünürlüğünü aynı sistem felsefesi içinde ele almasıdır.

### 🎓 Eğitim, Ar-Ge ve Takım Geliştirme

Hydronom yalnızca araç çalıştırmak için değil, aynı zamanda mühendislik öğrenimi ve takım içi Ar-Ge kültürü için de kullanılabilecek bir yapıdır.

Bu repo;

- otonom sistem mimarisi,
- C# runtime geliştirme,
- sensör entegrasyonu,
- embedded firmware,
- güvenli haberleşme,
- simülasyon,
- telemetry,
- gateway,
- ground station,
- AI destekli görev planlama

gibi birçok alanı aynı proje içinde birleştirir.

Bu nedenle Hydronom; öğrenci takımları, mühendislik eğitimi, yarışma hazırlığı ve uzun vadeli Ar-Ge çalışmaları için güçlü bir öğrenme ve geliştirme zemini olabilir.

### 🌐 Çoklu Araç ve Filo Operasyonları

Hydronom’un uzun vadeli vizyonlarından biri, tek araç runtime’ından çoklu araç operasyon ekosistemine doğru büyümektir.

Bu vizyonda birden fazla araç;

- kendi runtime’ına sahip olabilir,
- kendi telemetry akışını gönderebilir,
- ortak Ground Station üzerinden izlenebilir,
- görev durumlarını paylaşabilir,
- world model bilgisini operasyon merkezine aktarabilir,
- secure command pipeline üzerinden ayrı ayrı yönetilebilir.

Bu yaklaşım, Hydronom’un gelecekte filo tabanlı otonom operasyonlara temel oluşturabilecek bir platforma dönüşmesini sağlar.

---

## 🔮 Gelecek Vizyonu

Hydronom şu anki haliyle bir son ürün değil; büyümeye devam eden bir otonom sistem platformudur.

Bugünkü yapı; runtime, sensör mimarisi, güvenli haberleşme, telemetri, Gateway, Ground Station, AI, simülasyon ve gömülü firmware katmanlarını bir araya getiren güçlü bir temel oluşturmuştur.  
Ancak Hydronom’un asıl hedefi, bu temeli zamanla daha gerçekçi, daha güvenli, daha akıllı ve daha platform bağımsız hale getirmektir.

Hydronom’un gelecek vizyonu birkaç ana yönde ilerlemektedir.

### 🧠 Daha Güçlü State Estimation ve Fusion

Gelecekte Hydronom’un state estimation katmanı daha gelişmiş hale getirilecektir.

Hedeflenen geliştirmeler:

- IMU / GPS / depth sensor / camera / LiDAR / sonar benzeri kaynakların daha sağlam birleştirilmesi,
- sensör güvenilirliğine göre ağırlıklandırma,
- gürültü ve drift etkilerinin azaltılması,
- bayat veri ve kopan sensörlere karşı daha dayanıklı davranış,
- 3D / 6DOF state modelinin runtime genelinde daha güçlü kullanılması,
- simulation truth ile operational state arasındaki farkın daha net test edilmesi.

Bu geliştirmeler, Hydronom’un yalnızca veri alan değil; verinin güvenilirliğini anlayan bir sisteme dönüşmesini sağlayacaktır.

### 🌍 Gelişmiş Dünya Modeli ve Keşif Mantığı

Hydronom’un uzun vadeli hedeflerinden biri, aracın operasyon dünyasını daha bilinçli şekilde temsil edebilmesidir.

Bu kapsamda hedeflenenler:

- observed world,
- fused world,
- operational world,
- world object tracking,
- görev nesnesi farkındalığı,
- no-go / safety zone yönetimi,
- bilinmeyen dünya / discovery mode,
- sensör menzili ve görüş alanına göre nesne algılama,
- Ops arayüzünde daha zengin dünya görünürlüğü.

Böylece araç yalnızca kendisine verilen hedefleri takip etmekle kalmaz; çevresini gözlemleyerek kendi operasyonel dünya modelini oluşturabilir.

### 🗺️ Daha Gelişmiş Planning ve Motion Behavior

Hydronom’un karar ve görev katmanı zamanla daha gelişmiş planner yapılarıyla desteklenebilir.

Gelecekte hedeflenen konular:

- global planner,
- local planner,
- obstacle-aware path following,
- hedefe yaklaşma / yavaşlama / tutunma fazları,
- hız ve stopping distance farkındalığı,
- görev durumuna göre davranış geçişleri,
- platform kabiliyetine göre hareket planlama,
- AI destekli yeniden planlama önerileri.

Amaç, aracın yalnızca hedefe yönelen basit bir sistem olmaktan çıkıp, görev ve çevre bağlamını birlikte değerlendiren daha olgun bir otonom davranış üretmesidir.

### 🛡️ Daha Gelişmiş Güvenlik ve Yetki Mimarisi

Hydronom’da güvenlik zaten temel bir mimari parça olarak ele alınmaktadır.  
Gelecekte bu katman daha da genişletilebilir.

Hedeflenen geliştirmeler:

- runtime safety gate’in daha derin entegrasyonu,
- mission compatibility kontrolünün güçlendirilmesi,
- vehicle capability policy’nin daha aktif kullanılması,
- command retry / resend / timeout yönetimi,
- heartbeat ve link health takibi,
- key management ve güvenli konfigürasyon,
- role-based command policy’nin genişletilmesi,
- çoklu araç senaryolarında güvenli komut ayrımı.

Bu geliştirmeler, Hydronom’un yarış ve saha koşullarında daha güvenilir bir operasyon sistemi haline gelmesine katkı sağlayacaktır.

### 🔐 Haberleşme Altyapısının Genişletilmesi

Hydronom’un ana command ve ACK/NACK hattı binary secure pipeline yönüne taşınmıştır.

Gelecekte haberleşme tarafında hedeflenenler:

- Runtime secure command host entegrasyonu,
- eski JSON CommandServer’ın debug / fallback seviyesine indirilmesi,
- Gateway ve Ops tarafının secure telemetry kanalına daha sıkı bağlanması,
- ACK resend / retry mekanizması,
- heartbeat ve link health ölçümü,
- TCP transport header yapısının daha binary hale getirilmesi,
- serial / UDP / LoRa / RF transport seçenekleri,
- çoklu araç için kanal ve vehicle id ayrımı,
- düşük bant genişliği profillerinin sahada test edilmesi.

Uzun vadede hedef, Hydronom mesajlarının farklı taşıma teknolojileri üzerinden aynı güvenlik ve envelope mantığıyla taşınabilmesidir.

### 🖥️ Daha Güçlü Ground Station ve Ops Deneyimi

Hydronom’un operasyon tarafı, sistemin sahada anlaşılabilir ve yönetilebilir olması için kritik önemdedir.

Gelecekte hedeflenen Ops / Ground Station kabiliyetleri:

- daha gelişmiş 3D tactical view,
- araç durum panelleri,
- world model görselleştirme,
- route / hedef / görev ilerleme görünürlüğü,
- sensor health ve actuator health panelleri,
- command ACK/NACK timeline,
- runtime diagnostics ekranları,
- AI destekli görev asistanı,
- çoklu araç görünürlüğü,
- filo düzeyinde görev ve risk özeti.

Bu hedefler, Hydronom’u yalnızca çalışan bir runtime değil; gerçek bir operasyon platformu haline getirmeyi amaçlar.

### 🔌 Gerçek Donanım ve Embedded Entegrasyonu

Hydronom’un gerçek araç tarafında Pico / MCU mimarisi daha da güçlendirilecektir.

Hedeflenen geliştirmeler:

- Pico 2W firmware’in motor / ESC kontrolünde olgunlaştırılması,
- manuel kontrol entegrasyonu,
- sensör node protokolünün netleşmesi,
- USB-UART framing ve checksum yapısının geliştirilmesi,
- actuator feedback akışının eklenmesi,
- emergency stop davranışının düşük seviye güvenceye alınması,
- gerçek sensörlerin C# Primary runtime’a profesyonel backend’lerle bağlanması.

Bu sayede Hydronom, gerçek donanım üzerinde daha sürdürülebilir ve modüler bir yapıya kavuşacaktır.

### 🧪 Daha Gerçekçi Simülasyon ve Test Ekosistemi

Simülasyon tarafında hedef, yalnızca aracın hareket ettiğini görmek değildir.

Gelecekte hedeflenenler:

- daha gerçekçi physics model,
- platforma özel direnç / performans modelleri,
- CFD veya saha testinden gelen drag / thrust verilerinin profile aktarılması,
- unknown world / discovery mode,
- simüle sensör menzili ve görüş alanı,
- sensör gürültüsü / drift / kopma modelleme,
- replay tabanlı regression test,
- test sonrası otomatik raporlama,
- scenario judge ve skor değerlendirmesi.

Bu yapı, gerçek araç testlerine çıkmadan önce Hydronom’un daha güvenli ve kontrollü şekilde olgunlaşmasını sağlar.

### 🤖 AI Destekli Operasyonun Genişletilmesi

AI katmanı gelecekte daha güçlü bir operasyon yardımcısına dönüşebilir.

Hedeflenen AI kabiliyetleri:

- görev planı kalite analizi,
- runtime context yorumlama,
- telemetry trend açıklama,
- sensor health anomali yorumu,
- test sonrası log özeti,
- simülasyon sonucu raporlama,
- operatöre görev önerisi sunma,
- doğal dil ile görev taslağı oluşturma,
- çoklu araç operasyonlarında karar destek.

Bu vizyonda AI, kontrol otoritesini ele alan bir katman değil; güvenlik sınırları içinde çalışan açıklayıcı ve destekleyici bir mühendislik aracıdır.

### 🌐 Uzun Vadeli Platform Hedefi

Hydronom’un uzun vadeli hedefi, her araç için ayrı ayrı sıfırdan otonom sistem yazmak değildir.

Hedef; farklı platformların kendi profil, sensör, aktüatör, fizik ve görev modelleriyle aynı Hydronom çekirdeğine bağlanabilmesidir.

Bu vizyon gerçekleştiğinde Hydronom;

- su üstü araçları,
- su altı araçları,
- yelkenli platformlar,
- kara araçları,
- VTOL / hava aracı benzeri sistemler,
- özel görev robotları,
- deneysel platformlar

için ortak bir otonom runtime felsefesi sunabilir.

Kısacası Hydronom’un geleceği yalnızca “bir tekne yazılımı” olmak değildir.

> **Hydronom’un geleceği; farklı araçların, farklı görevlerin ve farklı ortamların aynı güvenli, gözlemlenebilir ve modüler otonomi çekirdeği üzerinde birleşebildiği bir sistem platformu olmaktır.**

---

## 🌐 Hydronom Filo ve Yer İstasyonu Mimarisi

Hydronom’un uzun vadeli vizyonlarından biri, tek araç runtime’ından çoklu araç operasyon ekosistemine doğru genişlemektir.

Bu yaklaşımda her araç kendi Hydronom runtime’ına sahip bağımsız bir otonom düğüm olarak çalışabilir.  
Yer istasyonu ise bu araçları izleyen, görev durumlarını takip eden, telemetriyi toplayan, riskleri görünür hale getiren ve gerektiğinde güvenli komut gönderen üst seviye operasyon merkezi olarak konumlanır.

Bu mimari şu temel fikre dayanır:

> **Her araç kendi karar ve güvenlik sorumluluğunu taşır; yer istasyonu ise operasyon görünürlüğü, görev koordinasyonu ve güvenli komut yönetimi sağlar.**

### 🚤 Araç Düğümleri

Her Hydronom aracı kendi başına çalışabilen bir runtime düğümü olarak düşünülür.

Bir araç düğümü şunlara sahip olabilir:

- kendi runtime döngüsü,
- kendi sensör backend’leri,
- kendi fusion / state estimation hattı,
- kendi görev ve karar sistemi,
- kendi actuator / motor kontrol hattı,
- kendi safety limiter mantığı,
- kendi telemetry üretimi,
- kendi secure command receiver yapısı,
- kendi vehicle id ve capability bilgisi.

Bu yapı sayesinde araçlar yer istasyonuna bağımlı olmadan da temel otonom davranışlarını sürdürebilir.

Yer istasyonu bağlantısı koptuğunda araç güvenli moda geçebilir, görevi duraklatabilir, son güvenli davranışı sürdürebilir veya platforma göre belirlenen fail-safe davranışı uygulayabilir.

### 🖥️ Yer İstasyonu

Yer istasyonu, Hydronom ekosisteminin operasyon merkezidir.

Bu merkez şu görevleri üstlenebilir:

- araç bağlantı durumlarını izlemek,
- her aracın telemetry bilgisini göstermek,
- görev ve senaryo durumunu takip etmek,
- world model ve route bilgilerini görselleştirmek,
- sensor / actuator / diagnostics durumunu sunmak,
- AI destekli görev yorumu sağlamak,
- operatör komutlarını secure pipeline üzerinden göndermek,
- ACK/NACK sonuçlarını göstermek,
- çoklu araç görev görünürlüğü sağlamak.

Yer istasyonu, araçların iç runtime mantığını ezberlemek zorunda değildir.  
Gateway ve snapshot modelleri üzerinden her aracın dışarı açtığı operasyon durumunu okuyabilir.

### 📡 Haberleşme Katmanı

Filo mimarisinde haberleşme katmanı kritik önemdedir.

Hydronom’un hedefi, üst seviye mesaj mantığını belirli bir fiziksel bağlantı teknolojisine kilitlememektir.

Aynı Hydronom mesajları farklı taşıma katmanları üzerinden taşınabilir:

- TCP,
- Ethernet,
- Wi-Fi,
- RF modem,
- LoRa,
- serial / USB-UART,
- mesh ağ yapıları,
- ileride eklenecek özel transport katmanları.

Önemli olan, transport teknolojisinin üst seviye command / telemetry / ACK modelini bozmak zorunda kalmamasıdır.

Bu nedenle Hydronom’da mesajlar önce ortak envelope, güvenlik, content type, sequence, priority ve payload mantığıyla temsil edilir.  
Transport katmanı ise bu güvenli paketi taşır.

### 🔐 Filo İçin Secure Command Yaklaşımı

Çoklu araç sistemlerinde güvenli komut yönetimi daha da önemli hale gelir.

Yer istasyonundan gelen bir komutun;

- hangi araç için üretildiği,
- hangi source tarafından gönderildiği,
- hangi authority ile geldiği,
- replay olup olmadığı,
- HMAC doğrulamasından geçip geçmediği,
- safety-critical olup olmadığı,
- ilgili aracın bu komutu uygulama kabiliyeti olup olmadığı

kontrol edilmelidir.

Bu nedenle Hydronom filo mimarisinde secure command pipeline önemli bir temel sağlar.

Her araç, kendisine gelen komutu şu süreçten geçirebilir:

```text
Secure Command Packet
        ↓
Binary Decode + CRC
        ↓
HMAC Verification
        ↓
Anti-Replay Check
        ↓
Envelope / Payload Validation
        ↓
Authority Validation
        ↓
Runtime Command Bridge
        ↓
Safety Gate
        ↓
ACK / NACK Response
```

Bu akış, yer istasyonunun araçlara komut göndermesini daha güvenli ve izlenebilir hale getirir.

### 📤 Filo Telemetri ve Snapshot Mantığı

Filo mimarisinde her araç kendi telemetry akışını üretir.

Yer istasyonu veya Gateway katmanı bu telemetry akışlarını araç kimliğine göre ayırmalı ve her araç için ayrı aggregate state tutabilmelidir.

Filo telemetry görünürlüğünde şu bilgiler önemlidir:

- vehicle id,
- araç bağlantı durumu,
- son telemetry zamanı,
- araç pozisyonu,
- görev durumu,
- aktif hedef,
- risk seviyesi,
- sensor health,
- actuator state,
- diagnostics,
- ACK/NACK geçmişi,
- world model katkısı.

Bu yapı sayesinde operatör tek araç yerine birden fazla aracı aynı operasyon ekranında izleyebilir.

### 🗺️ Ortak Dünya Modeli

Filo sistemlerinde araçların ayrı ayrı gördüğü dünya bilgisi daha büyük bir operasyon resmine dönüşebilir.

Her araç kendi sensörleriyle farklı nesneleri gözlemleyebilir.  
Bu gözlemler yer istasyonunda veya merkezi bir Gateway katmanında ortak dünya modeline katkı sağlayabilir.

Ortak dünya modeli şu bilgileri içerebilir:

- görev hedefleri,
- rota noktaları,
- engeller,
- no-go bölgeleri,
- güvenlik alanları,
- araç konumları,
- keşfedilen nesneler,
- görev nesneleri,
- platforma özel operasyon bölgeleri.

Bu yaklaşım özellikle çoklu araç koordinasyonu, alan tarama, görev paylaşımı ve operasyon güvenliği için önemlidir.

### 🤝 Görev Koordinasyonu

Hydronom’un filo vizyonunda araçlar yalnızca yan yana çalışan bağımsız sistemler olarak kalmayabilir.

Uzun vadede yer istasyonu veya üst seviye görev yöneticisi şu kabiliyetleri sağlayabilir:

- araçlara görev atama,
- görevleri araç kabiliyetine göre dağıtma,
- araç health durumuna göre görev değiştirme,
- bir aracın görevini başka araca devretme,
- çoklu araç route planlama,
- ortak risk ve görev durumu takibi,
- AI destekli görev koordinasyon önerileri.

Bu koordinasyon doğrudan kontrol anlamına gelmek zorunda değildir.  
Her araç yine kendi safety ve runtime otoritesini korur.  
Yer istasyonu ise görev düzeyinde yönlendirme ve görünürlük sağlar.

### 🧭 Filo Mimarisinin Hedefi

Hydronom filo ve yer istasyonu mimarisinin hedefi, tek bir aracı uzaktan izlemekten daha geniştir.

Hedef; birden fazla otonom aracın aynı operasyon ekosistemi içinde:

- güvenli şekilde haberleşebildiği,
- görev durumlarını paylaşabildiği,
- yer istasyonu üzerinden izlenebildiği,
- operator komutlarını güvenli şekilde alabildiği,
- ACK/NACK ile komut sonucunu bildirebildiği,
- ortak dünya modeline katkı sağlayabildiği,
- gerektiğinde AI destekli görev koordinasyonu alabileceği

bir yapı kurmaktır.

> **Hydronom’un filo vizyonu; her aracı bağımsız bir otonom düğüm, yer istasyonunu ise bu düğümlerin güvenli ve anlaşılır operasyon merkezi haline getirmektir.**

---

## 💙 Neden Özel?

Hydronom’u özel yapan şey yalnızca sahip olduğu dosya sayısı, kod satırı veya teknik modüller değildir.

Bu proje hazır bir framework’ün üzerine hızlıca kurulan yüzeysel bir demo değildir.  
Hydronom; defalarca değişen ihtiyaçların, gerçek donanım sorunlarının, yarış baskısının, mimari arayışların, simülasyon denemelerinin, sensör problemlerinin, haberleşme kırılmalarının ve yeniden kurulan sistem parçalarının içinden büyümüştür.

Bu yüzden Hydronom’un özel tarafı yalnızca “çalışması” değildir.

Özel olan şey, sistemin arkasındaki niyettir:

> **Bir şeyi sadece çalıştırmak için değil, gerçekten doğru bir temel kurmak için geliştirmek.**

Hydronom’da birçok karar kısa vadeli kolay çözüm yerine uzun vadeli mimari sağlamlık düşünülerek alınır.

Bu bazen daha yavaş ilerlemek anlamına gelir.  
Bazen çalışan kodu yeniden yazmak gerekir.  
Bazen tek bir problem için saatlerce mimari düşünmek gerekir.  
Bazen bir sensörün, bir komutun, bir telemetry frame’inin veya bir ACK paketinin sistemde nereye oturacağını tekrar tekrar sorgulamak gerekir.

Ama bu yaklaşım Hydronom’a karakter kazandırır.

Hydronom’un özel taraflarından bazıları şunlardır:

- sıfırdan kurulan otonom runtime yaklaşımı,
- C# Primary mimariye doğru bilinçli geçiş,
- Python’un legacy / backup seviyesine indirgenmesi,
- platform bağımsızlık hedefinin korunması,
- 3D / 6DOF düşüncenin sisteme yayılması,
- sensör mimarisinin backend değiştirilebilir tasarlanması,
- gerçek donanım ve simülasyonun birlikte düşünülmesi,
- secure binary haberleşme hattının adım adım kurulması,
- HMAC, anti-replay, authority validation ve ACK/NACK gibi güvenlik yapılarına önem verilmesi,
- Gateway, Ops, Ground Station ve AI katmanlarının sistemin parçası olarak düşünülmesi,
- Pico / MCU firmware tarafının yüksek seviye runtime’dan ayrıştırılması,
- smoke test kültürüyle büyük değişikliklerin doğrulanması.

Hydronom bugün hâlâ tamamlanmış değildir.  
Bazı modüller gelişmektedir, bazı parçalar yeniden yazılacaktır, bazı fikirler daha sonra olgunlaşacaktır.

Ama bu eksiklikler Hydronom’un değerini azaltmaz.  
Tam tersine, onun yaşayan bir mühendislik sistemi olduğunu gösterir.

Bu proje yalnızca bir yarışma aracını hareket ettirmek için değil;  
bir gün çok daha büyük, daha güvenli ve daha akıllı sistemlerin üzerine kurulabileceği bir temel oluşturmak için vardır.

Hydronom bu yüzden özeldir.

Çünkü burada yalnızca kod yoktur.  
Burada bir sistem kurma isteği, bir mühendislik inadını sürdürme iradesi ve “daha doğrusunu yapabilir miyiz?” sorusunu bırakmayan bir yaklaşım vardır.

---

## 🛠️ Kurulum Mantığı

Hydronom tek bir küçük uygulamadan oluşmadığı için kurulum mantığı da tek adımlı bir “çalıştır ve bitir” yaklaşımıyla düşünülmemelidir.

Repo içinde C# runtime, Core kütüphanesi, Gateway, Ground Station, AI katmanı, smoke test projeleri, frontend / Ops bileşenleri, Python legacy araçları ve Pico / embedded firmware gibi farklı katmanlar bulunabilir.

Bu yüzden Hydronom’u çalıştırırken önce hangi katmanın hedeflendiği belirlenmelidir:

- sadece build almak,
- smoke test çalıştırmak,
- runtime başlatmak,
- Gateway / Ops tarafını çalıştırmak,
- senaryo testi almak,
- gerçek donanım ile actuator testi yapmak,
- Pico firmware tarafını geliştirmek,
- AI / Ground Station entegrasyonunu denemek.

### 📦 Temel Gereksinimler

Hydronom’un farklı parçaları için aşağıdaki araçlardan bazıları gerekebilir:

- .NET SDK 8.0,
- Git,
- PowerShell,
- Python,
- Node.js / npm,
- Rust toolchain,
- Pico / embedded geliştirme araçları,
- seri port erişimi,
- sensör / ESC / motor donanımları,
- Gateway ve Ops için gerekli frontend bağımlılıkları.

Ana C# çözümü için temel gereksinim `.NET 8.0` ortamıdır.

### 🧱 Genel Build

Hydronom’un ana çözümü şu komutla derlenebilir:

```powershell
dotnet build .\Hydronom.sln
```

Bu komut Core, Runtime, Gateway, Ground Station, AI ve test projelerinin derlenebilir durumda olup olmadığını doğrulamak için ilk kontrol adımıdır.

Başarılı bir build, sistemin derlendiğini gösterir.  
Ancak Hydronom gibi çok katmanlı bir sistemde yalnızca build almak yeterli değildir; kritik veri akışları ayrıca smoke testlerle doğrulanmalıdır.

### 🧪 Smoke Test Mantığı

Hydronom’da farklı alt sistemleri doğrulamak için ayrı smoke test projeleri bulunur.

Örnek smoke test alanları:

- communication core,
- secure command,
- command authority,
- runtime command bridge + ACK,
- TCP transport,
- compact telemetry,
- telemetry delta,
- telemetry envelope,
- runtime pipeline,
- scenario pipeline,
- diagnostics,
- Gateway ingress,
- Ground Station,
- AI integration.

Genel yaklaşım şudur:

```powershell
dotnet run --project .\tests\<SmokeTestProject>\<SmokeTestProject>.csproj
```

Örneğin secure command pipeline testlerinden biri şu şekilde çalıştırılabilir:

```powershell
dotnet run --project .\tests\Hydronom.Core.SecureCommandSmokeTest\Hydronom.Core.SecureCommandSmokeTest.csproj
```

TCP transport doğrulaması için:

```powershell
dotnet run --project .\tests\Hydronom.Core.TcpTransportSmokeTest\Hydronom.Core.TcpTransportSmokeTest.csproj
```

Runtime command bridge ve ACK/NACK hattı için:

```powershell
dotnet run --project .\tests\Hydronom.Core.RuntimeCommandBridgeAckSmokeTest\Hydronom.Core.RuntimeCommandBridgeAckSmokeTest.csproj
```

### ⚙️ Runtime Çalıştırma Mantığı

Runtime katmanı Hydronom’un canlı çalışma tarafıdır.

Runtime başlatılmadan önce genellikle şu sorular netleştirilmelidir:

- simülasyon mu çalışacak,
- gerçek donanım mı kullanılacak,
- sensörler gerçek mi sim mi,
- actuator tarafı mock mu serial mı,
- scenario başlatılacak mı,
- Gateway’e telemetry gönderilecek mi,
- secure command host aktif mi,
- legacy JSON command server debug/fallback olarak mı kullanılacak?

Runtime genel olarak şu mantıkla çalıştırılır:

```powershell
dotnet run --project .\src\Hydronom.Runtime\Hydronom.Runtime.csproj
```

Ancak gerçek çalışma modu `appsettings.json`, ortam değişkenleri, scenario dosyaları ve donanım bağlantılarına göre değişebilir.

### 🌉 Gateway / Ops Çalıştırma Mantığı

Gateway katmanı, Runtime’dan gelen frame’leri parse eder ve snapshot / WebSocket üzerinden dış sistemlere sunar.

Gateway tarafı çalıştırıldığında şu bilgiler dışarı açılabilir:

- runtime connection state,
- vehicle aggregate state,
- telemetry summary,
- mission state,
- actuator state,
- world state,
- sensor / debug diagnostics,
- snapshot endpoint,
- WebSocket updates.

Gateway genel olarak şu tarz bir komutla çalıştırılabilir:

```powershell
dotnet run --project .\src\HydronomOps.Gateway\HydronomOps.Gateway.csproj
```

Ops frontend tarafı ayrı bir Node / frontend çalışma akışına sahip olabilir.  
Bu taraf, Gateway snapshot ve WebSocket verilerini kullanarak operasyon arayüzünü oluşturur.

### 🔌 Pico / Embedded Firmware Mantığı

Pico / embedded tarafı, Hydronom’un düşük seviye donanım katmanıdır.

Bu bölümde amaç:

- motor / ESC kontrolü,
- PWM üretimi,
- manuel kontrol girdileri,
- sensör node davranışları,
- USB-UART üzerinden runtime ile haberleşme

gibi donanıma yakın işleri yürütmektir.

Pico firmware geliştirme akışı, kullanılan embedded proje yapısına ve Rust toolchain ayarlarına göre değişebilir.  
Bu katman yüksek seviye Hydronom runtime’ın yerine geçmez; runtime tarafından üretilen güvenli ve sınırlandırılmış komutları fiziksel sinyale dönüştüren düşük seviye yürütücü olarak konumlanır.

### 🧑‍💻 Geliştirici İçin Not

Hydronom tek dosyada anlaşılacak bir proje değildir.

Projeyi anlamak için en sağlıklı yaklaşım şudur:

1. Önce Core modellerini ve domain yapılarını incele.
2. Sonra Runtime veri akışını takip et.
3. Ardından Sensor / Fusion / Decision / Actuation ilişkisini anlamaya çalış.
4. Communication ve Security katmanını ayrı bir protokol omurgası olarak değerlendir.
5. Gateway ve Ops tarafını runtime’ın dış dünyaya açılan görünür yüzü olarak oku.
6. Smoke testleri, sistemin hangi ana akışları doğruladığını görmek için kullan.
7. Gerçek donanım ve simülasyon ayrımını mimarinin temel parçası olarak düşün.

Hydronom’u anlamanın yolu, tek bir dosyayı ezberlemek değil; katmanlar arasındaki sorumlulukları ve veri akışını kavramaktır.

---

## 📐 Geliştirme Felsefesi

Hydronom geliştirilirken temel hedef yalnızca kısa vadede çalışan bir demo üretmek değildir.

Bu proje; gerçek donanım, simülasyon, güvenlik, haberleşme, sensör füzyonu, görev yönetimi, yer istasyonu ve uzun vadeli platform bağımsızlık hedefleri birlikte düşünülerek geliştirilmektedir.

Hydronom’un geliştirme felsefesi birkaç temel ilkeye dayanır.

### 🧱 Hızlı Hack Yerine Sürdürülebilir Mimari

Hydronom’da kısa vadede çalışan ama uzun vadede taşınamayan çözümler mümkün olduğunca tercih edilmez.

Bir modül çalışıyor olsa bile, eğer mimari olarak yanlış yerdeyse veya ileride sistemi kilitleyecekse yeniden düşünülmelidir.

Bu yaklaşım bazen geliştirmeyi yavaşlatır.  
Fakat Hydronom’un hedefi yalnızca bugünkü testi geçmek değil; gelecekte daha büyük sistemlerin üzerine kurulabileceği sağlam bir temel oluşturmaktır.

### 🧩 Katmanlı ve Ayrıştırılmış Tasarım

Hydronom’da her katmanın sorumluluğu net olmalıdır.

Sensör okumak, state estimation yapmak, görev yönetmek, karar vermek, motor komutu üretmek, güvenlik uygulamak, haberleşme sağlamak ve telemetry yayınlamak aynı sorumluluk değildir.

Bu yüzden sistem şu ayrımı korumaya çalışır:

- Core modeller sistemi tanımlar.
- Runtime canlı çalışma akışını yürütür.
- Sensor katmanı veri kaynaklarını yönetir.
- Fusion katmanı anlamlı state üretir.
- Analysis ve Decision davranış seçimine yardımcı olur.
- Control ve Actuation fiziksel komuta yaklaşır.
- Safety katmanı komutları sınırlar.
- Communication katmanı güvenli veri alışverişi sağlar.
- Gateway ve Ops sistemi görünür hale getirir.
- AI katmanı yorumlama ve planlama desteği sunar.
- Embedded firmware düşük seviye donanım işlerini yürütür.

Bu ayrım, sistemin büyürken anlaşılabilir kalmasını sağlar.

### 🌍 Platform Bağımsız Düşünmek

Hydronom’un önemli felsefelerinden biri, çekirdeği tek bir araç tipine kilitlememektir.

Bugün bir su üstü aracı üzerinde çalışan bir yapı, yarın su altı aracı, yelkenli platform, kara aracı veya farklı deneysel platformlar için de temel oluşturabilmelidir.

Bu nedenle platforma özel detaylar çekirdek sisteme gömülmemelidir.

Örneğin;

- su direnci,
- hava sürüklemesi,
- zemin sürtünmesi,
- kaldırma / batma etkileri,
- araç geometrisi,
- aktüatör yerleşimi,
- sensör konfigürasyonu,
- performans limiti

gibi bilgiler araç profili, model veya provider katmanları üzerinden sisteme verilmelidir.

Çekirdek sistem, belirli bir fiziksel platformu ezberlemek yerine farklı platformlardan gelen bilgiyi ortak bir otonomi diliyle işleyebilmelidir.

### 👁️ Gözlemlenebilirlik Bir Lüks Değildir

Hydronom’da telemetry, diagnostics, health, snapshot, ACK/NACK ve log çıktıları sonradan eklenmiş yardımcı detaylar olarak görülmez.

Bir sistem ne yaptığını göstermiyorsa, sahada ona güvenmek zordur.

Bu yüzden Hydronom’da sistemin iç durumu görünür olmalıdır:

- hangi karar verildi,
- hangi görev adımı aktif,
- hangi sensör sağlıklı,
- hangi komut kabul edildi,
- hangi komut reddedildi,
- hangi risk oluştu,
- hangi world object görüldü,
- hangi actuator komutu üretildi,
- hangi telemetry frame’i yayınlandı.

Bu görünürlük hem geliştirici hem operatör hem de takım için kritik önemdedir.

### 🛡️ Güvenlik Sonradan Eklenen Bir Parça Değildir

Hydronom’da güvenlik, sistemin en sonuna eklenen küçük bir kontrol olarak düşünülmez.

Güvenlik; command pipeline’dan actuator output’a kadar birçok katmanda bulunmalıdır.

Bu kapsamda:

- komut kaynağı doğrulanır,
- authority policy uygulanır,
- HMAC doğrulaması yapılır,
- anti-replay kontrolü uygulanır,
- safety-critical komutlar ayrı ele alınır,
- runtime safety gate devreye girer,
- actuator limiter komutu sınırlar,
- emergency stop her zaman öncelikli düşünülür,
- ACK/NACK ile komut sonucu görünür hale getirilir.

Gerçek otonom sistemlerde güvenlik, yalnızca hata olunca devreye giren bir mekanizma değil; normal çalışma akışının doğal parçası olmalıdır.

### 🧪 Test Edilebilirlik ve Doğrulama Kültürü

Hydronom büyüdükçe manuel deneme yapmak tek başına yeterli olmaz.

Bu yüzden smoke testler, build doğrulamaları, scenario testleri, communication testleri ve gateway doğrulamaları geliştirme sürecinin bir parçasıdır.

Her büyük değişiklikten sonra şu sorular sorulmalıdır:

- Sistem hâlâ build alıyor mu?
- Secure command hattı çalışıyor mu?
- ACK/NACK decode ediliyor mu?
- Telemetry pipeline sağlam mı?
- TCP transport roundtrip geçiyor mu?
- Runtime scenario akışı bozuldu mu?
- Gateway snapshot hâlâ doluyor mu?
- Ops tarafının ihtiyaç duyduğu veri geliyor mu?

Bu kültür, Hydronom’un büyürken kontrolsüz kırılmasını engellemeye yardımcı olur.

### 🧠 AI Destekli Ama Güvenlik Kontrollü Yaklaşım

Hydronom’da AI katmanı değerli bir yardımcıdır; ancak sistemin nihai güvenlik otoritesi değildir.

AI;

- yorum yapabilir,
- görev planı önerebilir,
- telemetry özetleyebilir,
- riskleri açıklayabilir,
- operatöre destek olabilir.

Fakat AI çıktıları doğrudan uygulanmamalıdır.  
Her öneri runtime, safety, authority ve operatör kontrolüyle birlikte değerlendirilmelidir.

Bu yaklaşım, AI’ın faydasını alırken sistemi kontrolsüz hale getirmemeyi amaçlar.

### 🔌 Gerçek Donanımı Unutmayan Yazılım

Hydronom yalnızca masaüstünde çalışan bir simülasyon projesi değildir.

Gerçek araçta;

- kablo kopabilir,
- sensör bozulabilir,
- ESC farklı davranabilir,
- GPS gelmeyebilir,
- haberleşme zayıflayabilir,
- güç sistemi sorun çıkarabilir,
- motor beklenenden farklı tepki verebilir.

Bu yüzden yazılım mimarisi, gerçek dünyanın belirsizliklerini dikkate almalıdır.

Hydronom’da simülasyon önemlidir; fakat nihai hedef gerçek donanımda güvenilir çalışabilecek bir sistem kurmaktır.

### 🌱 Yaşayan Sistem Yaklaşımı

Hydronom donmuş bir ürün değildir.

Bu proje sürekli değişen, öğrenen, büyüyen ve yeniden şekillenen bir mühendislik organizmasıdır.

Bazı parçalar bugün yeterlidir ama yarın yeniden yazılabilir.  
Bazı modüller geçici olabilir.  
Bazı tasarımlar saha testlerinden sonra değişebilir.

Bu normaldir.

Önemli olan, sistemin büyürken yönünü kaybetmemesidir:

> **Modülerlik, güvenlik, gözlemlenebilirlik, platform bağımsızlık ve gerçek dünyaya uygunluk.**

Hydronom’un geliştirme felsefesi bu eksenler üzerinde ilerler.

---

## ✅ Doğrulama ve Smoke Test Durumu

Hydronom’un büyüyen mimarisi yalnızca başarılı build çıktısına dayanmaz.

Build almak, kodun derlenebilir olduğunu gösterir.  
Ancak Hydronom gibi çok katmanlı bir otonom sistemde asıl önemli olan; haberleşme, güvenlik, runtime, telemetry, scenario, Gateway, Ground Station ve AI katmanlarının birlikte çalışabildiğini doğrulamaktır.

Bu nedenle projede farklı smoke test projeleriyle ana veri akışları düzenli olarak kontrol edilir.

### 🧱 Genel Build Durumu

Ana çözüm şu komutla doğrulanabilir:

```powershell
dotnet build .\Hydronom.sln
```

Güncel doğrulama durumunda Core, Runtime, Gateway, Ground Station, AI ve çok sayıda smoke test projesi `.NET 8.0` altında başarılı şekilde build alabilmektedir.

Build’in başarılı olması, sistemin temel derleme bütünlüğünü gösterir.  
Ancak kritik pipeline’ların ayrıca smoke testlerle çalıştırılması gerekir.

### 🔐 Communication / Security Smoke Testleri

Hydronom’un secure communication hattı aşağıdaki testlerle doğrulanmaktadır:

- `Hydronom.Core.CommunicationSmokeTest`
- `Hydronom.Core.CommunicationQueueSmokeTest`
- `Hydronom.Core.CommunicationPipelineSmokeTest`
- `Hydronom.Core.SecureCommandSmokeTest`
- `Hydronom.Core.CommandAuthoritySmokeTest`
- `Hydronom.Core.SecureCommandReceiverSmokeTest`
- `Hydronom.Core.TransportSmokeTest`
- `Hydronom.Core.TcpTransportSmokeTest`
- `Hydronom.Core.RuntimeCommandBridgeAckSmokeTest`

Bu testlerde doğrulanan ana davranışlar:

- binary envelope encode / decode,
- CRC32 ile bozuk paket yakalama,
- HMAC-SHA256 doğrulama,
- payload bozulduğunda reddetme,
- anti-replay ile tekrar paketi reddetme,
- wrong vehicle id yakalama,
- wrong content type yakalama,
- command authority validation,
- secure command receiver davranışı,
- runtime command bridge dönüşümü,
- ACK / NACK binary payload doğrulaması,
- TCP localhost üzerinde secure command ve telemetry roundtrip.

### 📡 Telemetry Smoke Testleri

Telemetry tarafında Hydronom compact, delta ve envelope tabanlı akışları doğrulayabilir.

Öne çıkan testler:

- `Hydronom.Core.CompactTelemetrySmokeTest`
- `Hydronom.Core.TelemetryDeltaSmokeTest`
- `Hydronom.Core.TelemetryEnvelopeSmokeTest`

Bu testlerde doğrulanan başlıca davranışlar:

- compact telemetry encode / decode,
- field mask ile seçili alan gönderimi,
- quantization doğruluğu,
- snapshot / delta ayrımı,
- değişmeyen frame’in bastırılması,
- anlamlı değişikliklerin delta frame’e eklenmesi,
- telemetry frame’in envelope içine alınması,
- HMAC + binary codec zinciriyle birlikte çalışması.

Bu yaklaşım, Hydronom’un telemetry hattını büyük JSON mesajlarına bağımlı olmaktan çıkarıp daha verimli ve bant genişliğine duyarlı hale getirir.

### ⚙️ Runtime / Scenario Smoke Testleri

Runtime tarafında farklı çalışma akışlarını doğrulayan smoke test projeleri bulunur.

Öne çıkan alanlar:

- runtime pipeline,
- telemetry pipeline,
- diagnostics pipeline,
- scenario pipeline,
- telemetry host,
- task / mission flow,
- actuator state üretimi,
- runtime world object akışı.

Bu testler, Hydronom’un yalnızca core veri modellerinden ibaret olmadığını; gerçek runtime davranışının da doğrulanabilir olduğunu gösterir.

Örnek test alanları:

- `Hydronom.Runtime.RuntimePipelineSmokeTest`
- `Hydronom.Runtime.PipelineSmokeTest`
- `Hydronom.Runtime.DiagnosticsSmokeTest`
- `Hydronom.Runtime.ScenarioSmokeTest`
- `Hydronom.Runtime.TelemetryPipelineSmokeTest`
- `Hydronom.Runtime.TelemetryHostSmokeTest`

### 🌉 Gateway / Ground Station Smoke Testleri

Gateway ve Ground Station tarafında amaç, Runtime’dan gelen verilerin dış sistemlere doğru şekilde aktarılabildiğini doğrulamaktır.

Öne çıkan testler:

- `HydronomOps.Gateway.RuntimeSummaryIngressSmokeTest`
- `HydronomOps.Gateway.RuntimeOpsSmokeTest`
- `Hydronom.GroundStation.SmokeTest`
- `Hydronom.AI.GroundStationSmokeTest`

Bu testler şu davranışları doğrulamaya yardımcı olur:

- runtime frame parsing,
- telemetry summary ingest,
- snapshot modelinin dolması,
- mission / actuator / world state bilgisinin taşınması,
- Gateway ve Ops için gerekli DTO modellerinin oluşması,
- Ground Station tarafında AI / mission assistant bağlamının çalışması.

### 🤖 AI Smoke Testleri

Hydronom AI katmanı, doğrudan kontrol otoritesi olmak yerine görev yorumlama, planlama ve Ground Station desteği için konumlanır.

AI tarafındaki smoke testler şu konuları doğrulamaya yardımcı olur:

- AI runtime context oluşturma,
- mission assistant davranışı,
- Ground Station entegrasyonu,
- plan validasyonu,
- safety gate yaklaşımı,
- operatöre açıklanabilir çıktı üretme.

Bu doğrulamalar, AI katmanının Hydronom içinde kontrolsüz bir otorite değil; güvenlik sınırları içinde çalışan destekleyici bir mühendislik katmanı olarak kalmasına yardımcı olur.

### ✅ Son Doğrulanan Kritik Haberleşme Durumu

Son doğrulanan secure communication hattında aşağıdaki ana sonuçlar elde edilmiştir:

- command payload JSON yerine binary formatta taşınmaktadır,
- ACK / NACK payload JSON yerine binary formatta taşınmaktadır,
- compact telemetry ve delta telemetry çalışmaktadır,
- binary envelope codec ve CRC doğrulaması çalışmaktadır,
- HMAC-SHA256 imzalama / doğrulama çalışmaktadır,
- anti-replay tekrar paketleri reddetmektedir,
- command authority policy yetkisiz komutları engellemektedir,
- runtime command bridge secure command’ı runtime intent’e çevirmektedir,
- ACK/NACK yaşam döngüsü binary payload ile doğrulanmıştır,
- TCP transport üzerinde gerçek localhost secure command ve telemetry roundtrip başarılıdır.

Örnek doğrulama çıktılarında command ve ACK/NACK packet boyutları şu seviyelerde ölçülmüştür:

- Arm command payload yaklaşık `150 byte`,
- Arm command packet yaklaşık `351 byte`,
- Mission command payload yaklaşık `219 byte`,
- Mission command packet yaklaşık `424 byte`,
- Arm accepted ACK packet yaklaşık `494 byte`,
- Observer NACK packet yaklaşık `504 byte`,
- Scenario ACK packet yaklaşık `485 byte`.

Bu sonuçlar, Hydronom’un haberleşme ve güvenlik omurgasının yalnızca tasarım olarak değil, çalışan testlerle de doğrulandığını gösterir.

### 🧭 Doğrulama Felsefesi

Hydronom’da testlerin amacı yalnızca hata yakalamak değildir.

Asıl amaç, büyüyen sistemin ana damarlarının hâlâ çalıştığını hızlıca görebilmektir.

Her büyük değişiklikten sonra şu sorular cevaplanabilmelidir:

- Build sağlam mı?
- Secure command pipeline çalışıyor mu?
- ACK/NACK decode ediliyor mu?
- TCP transport roundtrip geçiyor mu?
- Compact telemetry doğru mu?
- Runtime scenario akışı bozuldu mu?
- Gateway snapshot doluyor mu?
- Ground Station ve AI tarafı temel context’i okuyabiliyor mu?

Bu doğrulama kültürü, Hydronom’un büyürken kontrolsüz kırılmasını engelleyen en önemli mühendislik alışkanlıklarından biridir.

---

## 📍 Durum

Hydronom aktif olarak gelişen, çok katmanlı ve yaşayan bir otonom sistem platformudur.

Proje artık yalnızca temel bir tekne kontrol yazılımı seviyesinde değildir.  
Güncel durumda Hydronom; Core, Runtime, Communication, Telemetry, Gateway, Ground Station, AI, Simulation, Sensor Architecture ve Pico / Embedded Firmware katmanlarını birlikte taşıyan büyük bir mühendislik ekosistemine dönüşmüştür.

### ✅ Mevcut Güçlü Temeller

Şu an Hydronom tarafında güçlü şekilde oluşmuş ana temeller şunlardır:

- C# Primary runtime yönelimi,
- platform bağımsız otonomi hedefi,
- 3D / 6DOF state yaklaşımı,
- modüler Core / Runtime ayrımı,
- sensör backend mimarisi,
- simülasyon / gerçek / hibrit çalışma yaklaşımı,
- secure binary communication pipeline,
- compact / delta telemetry,
- command authority validation,
- ACK / NACK yaşam döngüsü,
- TCP ve InMemory transport altyapısı,
- Gateway snapshot ve WebSocket yönelimi,
- Ops / Ground Station görünürlüğü,
- AI destekli görev yorumlama altyapısı,
- Pico 2W tabanlı embedded motor kontrol çalışmaları,
- smoke test ve doğrulama kültürü.

Bu temeller, Hydronom’un artık yalnızca fikir veya prototip değil; büyüyen ve test edilebilen bir sistem omurgası olduğunu gösterir.

### 🔐 Haberleşme ve Güvenlik Durumu

Haberleşme ve güvenlik tarafında önemli bir eşik geçilmiştir.

Güncel durumda;

- command payload binary formata taşınmıştır,
- ACK / NACK payload binary formata taşınmıştır,
- telemetry compact / delta formatta taşınabilir hale gelmiştir,
- HydronomEnvelope yapısı binary codec üzerinden taşınabilmektedir,
- HMAC-SHA256 ile mesaj imzalama / doğrulama yapılabilmektedir,
- CRC32 ile bozuk packet yakalanabilmektedir,
- anti-replay window tekrar paketleri reddedebilmektedir,
- command authority policy yetkisiz komutları engelleyebilmektedir,
- runtime command bridge secure command’ı runtime intent’e çevirebilmektedir,
- TCP localhost üzerinde secure command ve telemetry roundtrip doğrulanmıştır.

Bu, Hydronom’un ana haberleşme yolunun JSON ağırlıklı debug yaklaşımından çıkarak binary / secure communication omurgasına doğru ciddi şekilde ilerlediğini gösterir.

JSON hâlâ debug veya fallback amaçlı kullanılabilir; ancak ana command ve ACK/NACK payload hattı artık binary yaklaşımıyla temsil edilmektedir.

### 📡 Sensör ve Füzyon Durumu

Sensör mimarisi, tekil ve sabit kaynaklı bir yapıdan daha profesyonel ve modüler bir yaklaşıma doğru evrilmektedir.

Güncel hedef;

- gerçek sensör,
- simülasyon sensörü,
- replay kaynağı,
- Pico / MCU node,
- USB-UART backend,
- dış pose sağlayıcı,
- eksik sensör seti

gibi farklı veri kaynaklarını ortak sensör modeli altında ele alabilmektir.

Fusion / state estimation tarafı hâlâ gelişmeye açık ana alanlardan biridir.  
Hydronom’un uzun vadeli başarısı için sensör timing, quality, health, confidence ve 6DOF operational state üretimi daha da güçlendirilmelidir.

### 🧪 Simülasyon ve Runtime Durumu

Runtime ve scenario tarafında sistem artık görev akışlarını, telemetry üretimini, actuator davranışlarını, diagnostics bilgisini ve world object / route point verilerini test edebilecek bir noktaya ilerlemiştir.

Simülasyon tarafında hedef yalnızca aracı hareket ettirmek değildir.  
Amaç; gerçek runtime akışını, görev davranışını, world model görünürlüğünü, sensör benzetimini ve operational state üretimini birlikte test edebilmektir.

Gelecekte simülasyon tarafında en kritik gelişim alanlarından biri:

> **bilinen dünya ile keşfedilen dünya ayrımını güçlendirmek ve aracı sensörleriyle dünyayı keşfeden bir yapıya taşımaktır.**

### 🖥️ Gateway / Ops / Ground Station Durumu

Gateway ve Ops tarafı, Hydronom’un iç runtime bilgisini dış dünyaya açmak için büyümektedir.

Güncel yönde Gateway;

- runtime frame parsing,
- telemetry summary,
- mission state,
- actuator state,
- world state,
- sensor / debug diagnostics,
- snapshot endpoint,
- WebSocket yayını

gibi işlevleri üstlenmektedir.

Ground Station ve Ops tarafında hedef, operatörün yalnızca aracı izlemesi değil; aracın görev, risk, dünya modeli, sensör health, actuator state ve command feedback durumunu da anlayabilmesidir.

Bu katman, Hydronom’un gerçek operasyon sistemi haline gelmesi için kritik önemdedir.

### 🤖 AI Durumu

AI katmanı, Hydronom’da doğrudan kontrol otoritesi olarak değil; görev yorumlama, runtime context analizi, plan önerisi ve Ground Station desteği sunan yardımcı bir katman olarak konumlanmaktadır.

Güncel hedef, AI’ın:

- runtime durumunu özetlemesi,
- görev bağlamını yorumlaması,
- operatöre açıklanabilir öneriler sunması,
- mission planning süreçlerine destek olması,
- safety gate ve authority sınırları içinde kalmasıdır.

AI tarafı hâlâ gelişmeye açıktır; ancak Hydronom’un operasyonel anlaşılabilirliğini artırmak için önemli bir potansiyel taşımaktadır.

### 🔌 Embedded / Pico Durumu

Pico 2W ve embedded firmware tarafı, Hydronom’un gerçek donanım yolculuğunda önemli bir adımdır.

Güncel hedef;

- motor / ESC kontrolünü MCU tarafında yürütmek,
- PWM üretimini düşük seviyede güvenilir yapmak,
- manuel kontrol entegrasyonuna zemin hazırlamak,
- sensör node mimarisini büyütmek,
- USB-UART üzerinden Hydronom runtime ile veri alışverişi kurmaktır.

Bu yaklaşımda yüksek seviye otonomi C# runtime tarafında kalır.  
Pico / MCU tarafı ise runtime’ın ürettiği güvenli ve sınırlandırılmış komutları fiziksel sinyallere dönüştüren düşük seviye yürütücü olarak çalışır.

### 🔄 Devam Eden Ana Gelişim Alanları

Hydronom hâlâ tamamlanmış bir ürün değildir.

Aktif gelişim alanlarından bazıları şunlardır:

- gerçek sensör backend’lerinin C# Primary runtime’a taşınması,
- daha güçlü fusion / state estimation,
- gerçek donanımda Pico / MCU entegrasyonu,
- Runtime secure command host entegrasyonu,
- Gateway / Ops secure telemetry entegrasyonu,
- ACK retry / timeout / resend mekanizması,
- link health ve heartbeat sistemi,
- daha gerçekçi simülasyon physics modeli,
- unknown world / discovery mode,
- platform profile ve vehicle capability katmanlarının genişletilmesi,
- Ground Station command feedback görünürlüğü,
- AI destekli görev planlama ve test raporlama.

### 🧭 Güncel Özet

Hydronom’un güncel durumu şu şekilde özetlenebilir:

> **Hydronom artık yalnızca çalışan bir otonom tekne yazılımı değil; güvenli haberleşme, modüler runtime, sensör mimarisi, telemetri, simülasyon, yer istasyonu, AI ve embedded firmware katmanlarını birlikte taşıyan platform bağımsız bir otonom sistem omurgasıdır.**

Henüz yapılacak çok şey vardır.  
Fakat artık ortada yalnızca fikir yoktur.

Artık çalışan, test edilen, büyüyen ve gerçek sistemlere doğru ilerleyen bir mimari vardır.

---

## 🤝 Teşekkür ve Kapanış

Hydronom benim için yalnızca bir repo değil.

Bu proje; uzun süren denemelerin, defalarca değişen mimarilerin, çalışan ama yeterli görülmeyen çözümlerin, gerçek donanım sorunlarının, simülasyon testlerinin, sensör belirsizliklerinin, haberleşme problemlerinin ve yeniden yeniden ayağa kaldırılan sistem parçalarının toplamıdır.

Bazen bir motoru döndürmek bile yeterince zor oldu.  
Bazen sensör verisi gelmedi.  
Bazen gelen veri anlamsızdı.  
Bazen paket bozuldu, bağlantı koptu, sistem beklenmeyen şekilde davrandı.  
Bazen çalışan bir şeyi bile daha doğru mimari için tekrar değiştirmek gerekti.

Ama Hydronom’un ruhu da tam olarak burada oluştu.

Bu proje yalnızca “bir araç hareket etsin” diye yapılmadı.  
Hydronom; anlamaya, yeniden kurmaya, daha sağlamını aramaya ve zamanla daha büyük sistemlere temel olabilecek bir mühendislik omurgası oluşturmaya çalıştı.

Bugün Hydronom;

- C# Primary runtime yönelimi,
- platform bağımsız otonomi vizyonu,
- 3D / 6DOF sistem düşüncesi,
- modüler sensör mimarisi,
- secure binary communication pipeline,
- compact / delta telemetry,
- ACK/NACK yaşam döngüsü,
- Gateway / Ops / Ground Station ekosistemi,
- AI destekli yorumlama yaklaşımı,
- Pico 2W embedded firmware çalışmaları,
- simülasyon ve smoke test kültürü

gibi birçok katmanı aynı çatı altında taşımaya başlamıştır.

Bu hâliyle bile Hydronom bitmiş değildir.  
Belki de asıl değerli tarafı budur.

Çünkü Hydronom donmuş bir ürün değil; yaşayan, değişen, öğrenen ve büyüyen bir mühendislik sistemidir.

Bu repo’ya bakan biri yalnızca kod görmemelidir.  
Burada bir sistemi gerçekten anlamaya çalışma çabası, bir şeyleri yüzeysel değil temelden kurma isteği ve “daha iyisini yapabilir miyiz?” sorusunu bırakmayan bir yaklaşım vardır.

Hydronom’un bugünkü hali, gelecekte kurulabilecek daha büyük sistemlerin yalnızca başlangıcıdır.

Belki eksikleri var.  
Belki bazı parçaları daha çok değişecek.  
Belki daha uzun süre test edilecek, bozulacak, düzeltilecek ve yeniden şekillenecek.

Ama artık ortada güçlü bir yön var.

> **Hydronom daha bitmedi.  
> Belki de gerçekten şimdi başlıyor.** 🌊✨

---

## 📜 Lisans ve Kullanım Hakları

© 2026 Tunahan Delisalihoğlu. Tüm hakları saklıdır.

Bu projenin tüm fikri, sınai ve yazılımsal mülkiyet hakları Tunahan Delisalihoğlu’na aittir.

Hydronom; otonom araç runtime mimarisi, sensör ve füzyon altyapısı, güvenli haberleşme hattı, telemetri sistemi, görev yönetimi, yer istasyonu, AI destekli operasyon yaklaşımı, gömülü firmware çalışmaları ve ilgili tüm tasarım / mimari kararlarıyla birlikte koruma altındadır.

### 🔒 Kullanım İzni

Bu yazılımın kullanım, geliştirme, erişim ve yaygınlaştırma hakkı **münhasıran Sadece Tunahan Delisalihoğlu na ve Yıldız Teknik Üniversitesi Stars of Hydro takımı üyelerine aittir.

Bu kapsam dışındaki tüm kullanım biçimleri, geliştiricinin açık ve yazılı iznine tabidir.

Bu izin; projenin tamamını, kaynak kod dosyalarını, mimari dokümanlarını, haberleşme protokollerini, görev ve runtime yapılarını, gömülü firmware bileşenlerini, AI / Ground Station entegrasyonlarını ve Hydronom adı altında geliştirilen ilgili tüm alt sistemleri kapsar.

### ⚠️ Kısıtlamalar

Aşağıdaki eylemler, yazılı izin olmaksızın **kesin olarak yasaktır**:

- projenin tamamının veya herhangi bir bölümünün kopyalanması,
- kaynak kodun, mimari tasarımın veya protokol yapılarının paylaşılması,
- projenin veya türevlerinin dağıtılması,
- ticari amaçla kullanılması,
- yarışma amacıyla başka takım veya kişiler tarafından kullanılması,
- tersine mühendislik yapılması,
- yeniden paketlenmesi,
- başka projelere doğrudan veya dolaylı şekilde entegre edilmesi,
- üçüncü kişilerle kaynak kod, doküman, mimari veya firmware parçalarının paylaşılması,
- Hydronom ismi, mimarisi veya ayırt edici sistem yaklaşımı kullanılarak benzer bir sistemin izinsiz şekilde türetilmesi.

### 🧠 Türetilmiş Çalışmalar

Bu projeden türetilen her türlü çalışma:

- geliştiricinin önceden açık ve yazılı onayına tabidir,
- görünür şekilde kaynak atfı içermelidir,
- Hydronom’un özgün mimari ve sistem tasarımını izinsiz şekilde çoğaltamaz,
- Stars of Hydro dışındaki kişi, takım, kurum veya projelerde izinsiz kullanılamaz.

Hydronom’un herhangi bir modülünden, dosyasından, protokolünden, mimari kararından veya teknik yaklaşımından türetilen çalışmalar da bu kapsamda değerlendirilir.

### 🔐 Güvenlik ve Saha Kullanımı Uyarısı

Hydronom; gerçek motorlar, ESC’ler, sensörler, gömülü kartlar, bataryalar, haberleşme sistemleri ve otonom karar mekanizmalarıyla birlikte kullanılabilecek bir sistemdir.

Bu nedenle yazılımın kullanımı sırasında:

- donanım güvenliği,
- batarya güvenliği,
- motor / ESC güvenliği,
- su üstü veya su altı test güvenliği,
- haberleşme güvenliği,
- emergency stop hazırlığı,
- operatör kontrolü,
- çevre ve insan güvenliği

dikkate alınmalıdır.

Bu yazılımın herhangi bir gerçek araç üzerinde kullanılması, gerekli mühendislik kontrolleri ve güvenlik önlemleri alınmadan yapılmamalıdır.

### ⚙️ Sorumluluk Reddi

Bu yazılım **“olduğu gibi” (as-is)** sunulmaktadır.

Yazılımın kullanımı, değiştirilmesi, test edilmesi veya gerçek donanım üzerinde çalıştırılması sonucunda oluşabilecek:

- donanım hasarları,
- motor / ESC / batarya arızaları,
- veri kaybı,
- bağlantı problemleri,
- sistem hataları,
- yanlış görev davranışları,
- güvenlik riskleri,
- operasyonel kazalar,
- üçüncü kişilere veya çevreye verilebilecek zararlar

konusunda geliştirici hiçbir şekilde sorumlu tutulamaz.

Hydronom’un gerçek sistemlerde kullanımı, kullanan kişi veya ekibin kendi teknik sorumluluğundadır.

### 📩 İletişim

İzin talepleri, lisanslama, kullanım hakkı veya proje hakkında iletişim için:

**tdelisalihoglu@outlook.com.tr**

---

> ⚠️ Not: Bu depo, Hydronom sisteminin tamamını veya en güncel tüm özel modüllerini içermeyebilir.  
> Daha gelişmiş, deneysel veya ekip içi kullanıma ayrılmış bazı modüller yalnızca özel geliştirme ortamlarında tutulabilir.

---
