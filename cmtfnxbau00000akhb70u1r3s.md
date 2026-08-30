---
title: "LoadFlux: Lojistikte İlan Değil, Karar Üreten Bir Ağ Kuruyoruz"
datePublished: 2026-08-30T10:24:36.014Z
cuid: cmtfnxbau00000akhb70u1r3s
slug: loadflux-lojistikte-i-lan-de-il-karar-reten-bir-a-kuruyoruz

---

Lojistik sektöründe çok temel ama pahalı bir çelişki var.

Bir tarafta yükünü taşıtacak araç arayan şirketler ve insanlar var.

Diğer tarafta yük arayan lojistik şirketleri, boş dönen kamyonlar, kapasitesinin yalnızca bir bölümünü kullanan araçlar ve bir sonraki işini bulmaya çalışan filolar var.

Yani aslında çoğu zaman problem **kapasitenin olmaması değil**.

Problem, doğru yükün doğru araçla, doğru yerde, doğru zamanda ve ekonomik olarak doğru koşullarda buluşamaması.

Eurostat'ın Avrupa taşımacılığı verilerinde, 2023 yılında AB'de kayıtlı ağır yük araçlarının araç-kilometrelerinin yaklaşık **%21,8'i boş yolculuklardan** oluşuyordu.

Üstelik karayolu taşımacılığının problemi yalnızca boş kilometre değil. IRU'nun 2025 araştırmasına göre Avrupa'da yaklaşık **502 bin doldurulamamış kamyon şoförü pozisyonu** bulunuyor. Bu, mevcut insan ve araç kapasitesinin daha verimli kullanılmasını giderek daha önemli hale getiriyor.

Biz de tam bu problemin ortasına **LoadFlux** ile giriyoruz.

Ama en başta bir şeyi netleştirelim:

> **LoadFlux bir yük ilan sitesi olmayacak.**

Bir dijital nakliye komisyoncusu da olmayacak.

Sadece müşteriyi lojistik şirketiyle buluşturan klasik bir marketplace olarak da kalmayacak.

LoadFlux'un hedefi çok daha büyük:

> **Fiziksel lojistik ağındaki yükleri, araçları, rotaları, zamanı ve kullanılabilir kapasiteyi sürekli analiz ederek her aracın yapabileceği en mantıklı ticari hareketleri üretmek.**

Başka bir ifadeyle:

**LoadFlux, lojistik sektörünün fırsat ve karar motoru olacak.**

* * *

# Önce en basit problemden başlayalım

Bir üretici düşünelim.

İstanbul Tuzla'da 4 palet makine parçası var.

Toplam ağırlık yaklaşık 2,3 ton.

Ürün salı günü öğleden sonra hazır olacak ve en geç perşembe Samsun'a ulaşması gerekiyor.

Bugünkü süreçte şirket çeşitli nakliyecileri arayabilir, WhatsApp gruplarına yazabilir, tanıdığı taşıyıcılardan fiyat isteyebilir veya farklı platformlarda yük ilanı oluşturabilir.

LoadFlux'ta ise müşteri tek bir taşıma talebi oluşturacak.

Örneğin:

**Çıkış:** Tuzla / İstanbul **Varış:** Tekkeköy / Samsun **Yük:** Makine parçaları **Ağırlık:** 2.300 kg **Palet:** 4 **Hazır olma zamanı:** Salı 14:00 **Teslim limiti:** Perşembe 18:00 **Araç gereksinimi:** Tenteli / uygun parsiyel taşıma **Bütçe:** 28.000 TL

Fakat LoadFlux için bu bilgiler yalnızca ekranda gösterilecek birkaç metin alanı değildir.

Arka planda bunların tamamı makine tarafından işlenebilir bir **Shipment Request** haline gelir.

Yani sistem artık şunu bilir:

> “Belirli koordinatlar arasında, belirli bir zaman penceresinde, belirli fiziksel özelliklere sahip bir yükün taşınması gerekiyor.”

Ve asıl işlem bundan sonra başlar.

* * *

# LoadFlux'un diğer tarafında araçlar var

Lojistik şirketleri sisteme yalnızca şirket profillerini değil, filolarını da tanımlayacak.

Örneğin:

### 34 LF 061

*   Tenteli TIR
    
*   24 ton maksimum taşıma kapasitesi
    
*   90 m³ kullanılabilir hacim
    
*   Mevcut yük: 9,4 ton
    
*   Kalan kapasite: 14,6 ton
    
*   Başlangıç: İstanbul
    
*   Hedef: Samsun
    
*   Hareket: 3 Eylül, 07:30
    
*   Planlanan rota: İstanbul → Bolu → Ankara → Çorum → Samsun
    

Burada LoadFlux'un bakış açısı değişiyor.

Klasik bir sistem şöyle düşünebilir:

> “Bu araç İstanbul'dan Samsun'a gidiyor. İstanbul-Samsun yüklerini gösterelim.”

Biz böyle yapmayacağız.

LoadFlux şunu soracak:

> **Bu aracın mevcut rotası, zamanı, kapasitesi ve maliyet yapısı düşünüldüğünde hangi yük veya yük kombinasyonlarını alması ekonomik olarak mantıklı?**

İki soru birbirinden tamamen farklı.

Ve LoadFlux'un bütün teknolojik yapısı ikinci sorunun üzerine kurulacak.

* * *

# Opportunity Engine: LoadFlux'un kalbi

Sistemin merkezinde **LoadFlux Opportunity Engine** bulunacak.

Her araç ve her potansiyel taşıma işi arasında bir uyumluluk hesaplanacak.

Basitleştirirsek:

```text
Araç
×
Rota
×
Yük
×
Kapasite
×
Zaman
×
Maliyet
×
Risk
×
Beklenen Gelir

↓

Transport Opportunity
```

Burada yalnızca iki şehrin aynı olup olmadığına bakmayacağız.

Sistem;

*   rota uyumunu,
    
*   aracın kalan kapasitesini,
    
*   yükün ağırlığını,
    
*   yükün hacmini,
    
*   palet/geometri uygunluğunu,
    
*   araç tipini,
    
*   yükleme zamanını,
    
*   teslim zamanını,
    
*   ana rotadan yapılması gereken sapmayı,
    
*   bekleme süresini,
    
*   sürüş süresini,
    
*   tahmini yakıt maliyetini,
    
*   ücretli yolları,
    
*   yükleme ve boşaltma süresini,
    
*   operasyonel riski,
    
*   müşterinin bütçesini,
    
*   tahmini taşıma gelirini
    

birlikte değerlendirecek.

Sonuçta firmaya 300 ilan göstermek yerine mümkün olduğunca şöyle diyeceğiz:

> **“Bu sefer için ekonomik olarak anlamlı 4 fırsat bulduk.”**

Bu fark LoadFlux'un temel ürün felsefesi olacak.

* * *

# Bir yük rotanın üzerinde olmak zorunda değil

Burada önemli bir detay var.

İstanbul'dan Samsun'a giden aracın alabileceği yükün İstanbul-Samsun arasında birebir bulunması gerekmiyor.

Örneğin aracın ana güzergâhından 18 kilometre saparak alınabilecek bir yük varsa ve bu yük 14.000 TL ek gelir sağlayacaksa o sapma son derece mantıklı olabilir.

Başka bir yük 70 kilometrelik ek rota yaratıp yalnızca 5.000 TL kazandırıyorsa anlamsız olabilir.

Bu nedenle LoadFlux sadece mesafeyi değil, **sapmanın ekonomisini** hesaplayacak.

Kabaca:

```text
Ek taşımanın değeri

=

Taşıma geliri

-

Ek yakıt
-

Ek sürücü zamanı
-

Yol ücretleri
-

Yükleme/boşaltma maliyeti
-

Bekleme maliyeti
-

Operasyonel risk
```

Böylece “rota üzerinde” kavramı geometrik olmaktan çıkıp ekonomik bir kavrama dönüşüyor.

* * *

# Dynamic Capacity Engine: Bir araç ya boş ya dolu değildir

Lojistikte kapasiteyi yalnızca “boş araç” üzerinden düşünmek de ciddi bir kayıp.

24 ton kapasiteli bir araç 9 ton yük taşıyorsa hâlâ kullanılabilecek 15 tona yakın kapasitesi olabilir.

LoadFlux bu kapasiteyi görünür hale getirecek.

Fakat yine yalnızca ton hesabı yapılmayacak.

Sistem;

*   ağırlık,
    
*   hacim,
    
*   palet sayısı,
    
*   fiziksel boyutlar,
    
*   yükleme sırası,
    
*   boşaltma sırası,
    
*   yük uyumluluğu,
    
*   kasa tipi,
    
*   özel taşıma şartları
    

gibi kısıtları birlikte değerlendirecek.

Çünkü 8 ton boş kapasitenin bulunması, her 8 tonluk yükün o araca konabileceği anlamına gelmiyor.

LoadFlux'un amacı yalnızca yük bulmak değil, **gerçek kullanılabilir kapasiteyi hesaplamak**.

* * *

# Ve işin en sevdiğimiz kısmı: Opportunity Packages

LoadFlux'un en önemli özgün özelliklerinden biri burada ortaya çıkıyor.

Sistem yalnızca tek tek işleri analiz etmeyecek.

**İş kombinasyonları oluşturacak.**

Diyelim ki bir TIR İstanbul'dan Samsun'a gidiyor ve yaklaşık 15 ton kapasitesi boş.

Sistem üç farklı taşıma buldu:

### İş A

İstanbul → Bolu 4 ton 14.000 TL

### İş B

Düzce → Çorum 5 ton 18.500 TL

### İş C

Çorum → Samsun 3 ton 15.300 TL

Tek başına üç ayrı ilan.

Ama LoadFlux için asıl soru şu:

> **Bu üç yük aynı sefer içerisinde birlikte alınabilir mi?**

Sistem rotayı yeniden hesaplayacak.

Kapasiteyi her pickup ve delivery noktasından sonra yeniden değerlendirecek.

Zaman pencerelerini kontrol edecek.

Yükleme ve boşaltma sıralarını kontrol edecek.

Ardından firmaya şöyle bir çıktı verebilecek:

* * *

## LoadFlux Opportunity Package

**Araç:** 34 LF 061 **Ana rota:** İstanbul → Samsun

**Önerilen taşıma:** 3 ek iş **Toplam ek yük:** 12 ton **Ana rota:** 731 km **Optimize rota:** 772 km **Ek mesafe:** +41 km **Ek operasyon süresi:** +1 saat 35 dakika

**Tahmini ek ciro:** 47.800 TL **Tahmini ek operasyon maliyeti:** 6.450 TL **Tahmini katkı:** +41.350 TL

**Kapasite kullanımı:**

%39 → **%91**

* * *

İşte LoadFlux'un kırılma noktalarından biri bu.

Çünkü artık:

> “Sana yük ilanları gösteriyorum.”

demiyoruz.

Şunu söylüyoruz:

> **“Aracının bu seferini daha kârlı hale getirecek operasyon planını buldum.”**

Bu problem teknik tarafta Vehicle Routing Problem, Pickup and Delivery, Time Windows, çok boyutlu kapasite optimizasyonu ve kombinatoryal optimizasyon gibi problemlerin birleşimine dönüşüyor.

Ve LoadFlux'un en ciddi teknoloji alanlarından biri tam olarak burada oluşacak.

* * *

# Araç yoldayken sistem durmayacak

Planlanan seferler önemli.

Ama gerçek lojistik statik değil.

Bir araç yükünü erken boşaltabilir.

Bir müşteri işi iptal edebilir.

Yeni bir taşıma ilanı ortaya çıkabilir.

Trafik değişebilir.

Araç beklenenden farklı bir saatte müsait olabilir.

Bu nedenle LoadFlux uzun vadede yalnızca sefer oluşturulduğu anda hesaplama yapan bir sistem olmayacak.

### Dynamic Dispatch Engine

Araçların canlı konumu sisteme aktarılabildiğinde LoadFlux sürekli yeniden değerlendirme yapabilecek.

Örneğin araç Ankara'daki yükünü boşalttı.

Normalde Samsun'a boş dönecek.

LoadFlux aracın:

*   anlık konumunu,
    
*   hedefini,
    
*   kalan sürüş süresini,
    
*   boş kapasitesini,
    
*   çevredeki aktif yükleri,
    
*   tahmini pickup sürelerini
    

analiz edecek.

Ve telefona şu bildirim gelebilecek:

> **Aracınızın dönüş rotasına uygun 2 yeni fırsat bulundu.**

Bir dokunuşla detay açılacak.

Bu noktada LoadFlux artık yalnızca marketplace değil, **hareket halindeki lojistik kapasitenin gerçek zamanlı karar katmanı** haline geliyor.

* * *

# LoadFlux Price Intelligence

Fiyat tarafını da yalnızca “müşteri bütçe yazsın, nakliyeci teklif versin” seviyesinde bırakmak istemiyoruz.

Sistem yeterli veri ürettikçe kendi fiyat zekâsını oluşturacak.

Model;

*   kilometre,
    
*   rota,
    
*   araç tipi,
    
*   yük türü,
    
*   ağırlık,
    
*   hacim,
    
*   yakıt,
    
*   ücretli yollar,
    
*   mevsimsellik,
    
*   bölgesel araç arzı,
    
*   yük talebi,
    
*   geçmiş teklifler,
    
*   kabul edilen fiyatlar,
    
*   reddedilen teklifler,
    
*   gün,
    
*   saat
    

gibi parametrelerden beslenecek.

Müşteri tarafında:

> **Tahmini piyasa aralığı: 31.000 – 35.500 TL**

görülebilir.

Firma tarafında ise:

> **Bu iş için rekabetçi teklif aralığı: 32.800 – 34.100 TL**

gibi bir karar desteği oluşabilir.

Daha da önemlisi sistem zamanla yalnızca “fiyat nedir?” sorusunu değil:

> **“Bu fiyattan teklif verirsem işi alma olasılığım nedir?”**

sorusunu da cevaplayabilir.

* * *

# Demand Heatmap: Yük henüz oluşmadan önce

LoadFlux yeterli veri biriktirdiğinde geçmişi görmek tek başına yeterli olmayacak.

Geleceği tahmin etmeye çalışacağız.

Hangi bölgelerde önümüzdeki saatlerde veya günlerde daha fazla taşıma talebi oluşma ihtimali var?

Hangi şehirden hangi şehre yük akışı artıyor?

Hangi günler belirli sanayi bölgelerinde taşıma yoğunluğu oluşuyor?

Bunların sonucunda filo yöneticisi haritada bir **Demand Heatmap** görebilecek.

Örneğin:

> Önümüzdeki 24 saat içerisinde Gebze–Bursa koridorunda yüksek taşıma talebi bekleniyor.

Buradan sonra filo planlaması yalnızca eldeki yüklerden ibaret olmaktan çıkıyor.

Araçlar **gelecekteki tahmini talebe göre konumlandırılmaya** başlanıyor.

* * *

# Empty Return Prediction

Lojistikte en pahalı sorulardan biri:

> “Oraya yük götürürsem dönüşte yük bulabilecek miyim?”

LoadFlux bunu da ölçülebilir bir probleme çevirecek.

Sistem geçmiş operasyonları, bölgesel yük üretimini, araç tipini, günü, saati ve talep desenlerini analiz ederek:

**Empty Return Probability**

üretebilecek.

Örneğin:

> Bu teslimat sonrası aracın boş dönüş riski: **%73**

ve ardından:

> Teslimattan sonra 38 km güneydeki bölgeye yönlenmeniz halinde dönüş yükü bulma olasılığı belirgin şekilde yükseliyor.

Bu tek başına büyük bir filo optimizasyon özelliği.

* * *

# Next Best City

Ve LoadFlux'un uzun vadede en güçlü sorularından birine geliyoruz:

> **Bu aracı bir sonraki hangi şehre göndermeliyim?**

Diyelim araç Ankara'da boşaldı.

Samsun'a mı dönmeli?

Konya'ya mı gitmeli?

Bursa tarafına mı yönelmeli?

Beklemeli mi?

LoadFlux bunu yalnızca mevcut ilanlara göre değerlendirmeyecek.

Hesaba:

*   aktif yükleri,
    
*   tahmini yük talebini,
    
*   spot fiyatları,
    
*   dönüş yükü ihtimalini,
    
*   kilometre maliyetini,
    
*   sürücünün çalışma süresini,
    
*   aracın özelliklerini,
    
*   geçmiş operasyon sonuçlarını
    

katacak.

Sonuç olarak:

### Seçenek A — Samsun

Beklenen gelir: X Boş kilometre riski: yüksek Operasyon riski: düşük

### Seçenek B — Konya

Beklenen gelir: Y Boş kilometre riski: orta Talep görünümü: güçlü

### Seçenek C — Bursa

Beklenen gelir: Z Ek yol: daha yüksek 48 saatlik tahmini toplam katkı: en yüksek

LoadFlux burada bir navigasyon uygulaması olmaktan da çıkıyor.

**Ekonomik navigasyon** yapmaya başlıyor.

* * *

# Fleet Profitability Intelligence

Bir filo yöneticisinin “araçlarım çalışıyor” demesi yeterli değil.

Önemli olan nasıl çalıştıkları.

LoadFlux her araç için zaman içerisinde:

*   toplam kilometre,
    
*   yüklü kilometre,
    
*   boş kilometre,
    
*   ortalama kapasite kullanımı,
    
*   kilometre başına gelir,
    
*   kilometre başına maliyet,
    
*   bekleme süresi,
    
*   rota bazlı kârlılık,
    
*   reddedilen fırsatlar,
    
*   kabul edilen fırsatlar,
    
*   LoadFlux sayesinde oluşturulan ek gelir
    

gibi metrikleri hesaplayabilecek.

Ve yalnızca grafik göstermek istemiyoruz.

Sistem veriyi yorumlayacak.

Örneğin:

> **34 LF 061 son 30 günde filonuzun ortalamasından %17 daha fazla boş kilometre yaptı.**

veya:

> **İstanbul → Ankara operasyonlarınız yüksek gelir üretmesine rağmen Ankara sonrası boş dönüş nedeniyle toplam sefer kârlılığı düşük.**

veya:

> **Bu ay LoadFlux tarafından oluşturulan fırsatlar filonuz için tahmini 184.000 TL ek katkı oluşturdu.**

Yani analitik ekranı geçmişi gösteren bir dashboard değil, **karar üreten bir sistem** olacak.

* * *

# LoadFlux AI Logistics Copilot

Burada “uygulamamıza bir chatbot koyduk, artık AI ürünüyüz” gibi bir şeyden bahsetmiyoruz.

LoadFlux AI sistemin gerçek operasyon verilerine bağlanacak.

Filo yöneticisi şunu yazabilecek:

> “Yarın İstanbul'dan çıkacak araçlarımı mümkün olan en kârlı şekilde planla.”

LoadFlux:

1.  müsait araçları bulacak,
    
2.  kapasitelerini analiz edecek,
    
3.  aktif taşıma işlerini değerlendirecek,
    
4.  tahmini talebi inceleyecek,
    
5.  rota kombinasyonları oluşturacak,
    
6.  maliyetleri hesaplayacak,
    
7.  boş dönüş riskini değerlendirecek,
    
8.  alternatif senaryolar üretecek.
    

Ve örneğin üç plan döndürecek:

### Plan A

En düşük operasyon riski Tahmini katkı: 118.000 TL

### Plan B

Daha agresif rota kombinasyonu Tahmini katkı: 136.000 TL

### Plan C

En düşük boş kilometre oranı Tahmini katkı: 124.000 TL

Son karar hâlâ insanda olacak.

Ama insan artık yüzlerce değişkeni kendi kafasında hesaplamak zorunda kalmayacak.

* * *

# Müşteri tarafını da unutmadık

Bütün bu optimizasyon sisteminin diğer tarafında taşıma yaptırmak isteyen kullanıcı var.

Müşteri:

*   yük ilanı oluşturabilecek,
    
*   fotoğraf yükleyebilecek,
    
*   konumları haritadan seçebilecek,
    
*   tarih ve zaman penceresi girebilecek,
    
*   yük özelliklerini tanımlayabilecek,
    
*   bütçe belirleyebilecek,
    
*   gelen lojistik tekliflerini karşılaştırabilecek,
    
*   firma profilini inceleyebilecek,
    
*   mesajlaşabilecek,
    
*   anlaşabilecek,
    
*   taşıma durumunu takip edebilecek,
    
*   tamamlanan operasyonu değerlendirebilecek.
    

Daha ileri aşamada doğal dil ile ilan oluşturulabilecek.

Kullanıcı yalnızca:

> “Yarın öğleden sonra Avcılar'dan Bursa'ya 6 palet yaklaşık 3 ton tekstil ürünü gidecek.”

yazacak.

LoadFlux bunu analiz ederek gerekli taşıma parametrelerini otomatik oluşturacak.

* * *

# Teklif sistemi

Eşleşme, anlaşma değildir.

LoadFlux doğru tarafları bir araya getirdikten sonra görüşme başlayacak.

Lojistik firması:

*   fiyat,
    
*   araç,
    
*   tahmini pickup,
    
*   teslimat zamanı,
    
*   taşıma koşulları
    

ile teklif verebilecek.

Müşteri farklı firmaları karşılaştırabilecek.

Karşı teklif yapılabilecek.

Mesajlaşma ve dosya paylaşımı gerçekleşebilecek.

Teklif değişiklikleri kayıt altında tutulabilecek.

Taraflar anlaşınca taşıma işi oluşturulacak.

İlerleyen aşamalarda elektronik sözleşme, ödeme altyapısı, sigorta ve finansal hizmetler de aynı işlem katmanına bağlanabilir.

* * *

# Trust Score

Böyle bir sistemde algoritmanın iyi olması yetmez.

Güven gerekiyor.

LoadFlux içerisinde firmalar ve operasyonlar için çok katmanlı bir doğrulama yapısı tasarlıyoruz.

Bunlar zaman içerisinde;

*   şirket doğrulama,
    
*   yetki belgeleri,
    
*   araç belgeleri,
    
*   sürücü bilgileri,
    
*   tamamlanan taşıma sayısı,
    
*   zamanında teslimat oranı,
    
*   iptal oranı,
    
*   müşteri değerlendirmeleri,
    
*   anlaşmazlıklar,
    
*   teklif davranışları
    

gibi verilerden beslenecek.

Bunların sonucunda dinamik bir **LoadFlux Trust Score** oluşturulabilecek.

Amaç yalnızca beş yıldız vermek değil.

Platformun kendi operasyon verisiyle gerçekten güvenilir taşıma ağı yaratmak.

* * *

# iOS, Android ve Web: Üç uygulama değil, tek ağ

LoadFlux aynı sistemin:

*   iOS,
    
*   Android,
    
*   Web
    

istemcilerinde çalışacak.

Mobil uygulama özellikle hızlı ilan, saha operasyonu, araç ve bildirim deneyiminde güçlü olacak.

Web tarafında ise filo yöneticileri ve kurumsal müşteriler için daha yoğun bilgi içeren:

*   filo ekranları,
    
*   operasyon haritaları,
    
*   analitikler,
    
*   fırsat paketleri,
    
*   rota planları
    

sunulacak.

Ama arka tarafta hepsi aynı lojistik ağına bağlı olacak.

* * *

# Teknik tarafta nasıl yapacağız?

İlk günden yüzlerce mikroservis açıp mimari diyagramlarla kendimizi kandırmak istemiyoruz.

Önce güçlü ve yönetilebilir bir çekirdek oluşturacağız.

Mobil ve çoklu platform tarafında **Flutter** önemli adaylarımızdan biri.

Backend tarafında:

**ASP.NET Core / .NET**

Ana veritabanı:

**PostgreSQL**

Coğrafi işlemler:

**PostGIS**

Cache:

**Redis**

Gerçek zamanlı veri:

**SignalR / WebSocket**

Optimizasyon ve matematiksel modeller:

**Python + OR-Tools ve gerektiğinde özel optimizasyon modelleri**

kullanılabilecek.

İlk backend yaklaşımımız **modüler monolith** olabilir.

Çünkü ilk günden dağıtık sistem karmaşıklığı üretmek yerine ürün mantığını doğru kurmak daha önemli.

Ana domain'ler kabaca:

```text
Identity

Customer

Company

Fleet

Vehicle

Driver

Shipment

Trip

Route

Matching

Opportunity

OpportunityPackage

Offer

Conversation

TransportOrder

Trust

Telemetry

Analytics

Forecast
```

şeklinde ayrılacak.

Sistem büyüdükçe özellikle:

*   Matching Engine,
    
*   Routing Engine,
    
*   Optimization Engine,
    
*   Telemetry,
    
*   Notification,
    
*   Forecasting
    

bağımsız servisler haline getirilebilir.

* * *

# Neden PostGIS?

Çünkü LoadFlux'un dünyası yalnızca:

```text
İstanbul
Ankara
Samsun
```

gibi stringlerden ibaret değil.

Bizim dünyamız koordinatlardan, yolların geometrisinden, pickup noktalarından, rota koridorlarından ve sapmalardan oluşuyor.

Örneğin sistemin:

> “Bu pickup noktası aracın planlanan rotasına kaç kilometre gerçek sapma oluşturuyor?”

sorusunu doğru şekilde cevaplayabilmesi gerekiyor.

Coğrafi veri LoadFlux'un temel veri tiplerinden biri olacak.

* * *

# Optimizasyon problemi sandığımızdan çok daha büyük

Bir araç ve bir yük için eşleşme yapmak nispeten kolaydır.

Ama:

20 araç,

500 açık yük,

farklı zaman pencereleri,

farklı pickup ve delivery noktaları,

farklı kapasiteler,

farklı araç gereksinimleri

olduğunda problem hızla büyür.

Opportunity Packages bunun üzerine bir de kombinasyon ekliyor.

Dolayısıyla LoadFlux'ta her şeyi tek bir “AI modeli” çözmeyecek.

Farklı problem sınıfları için farklı araçlar kullanılacak.

Bazı yerlerde:

**deterministik kurallar,**

bazı yerlerde:

**constraint optimization,**

bazı yerlerde:

**operations research,**

bazı yerlerde:

**machine learning,**

bazı yerlerde ise:

**LLM tabanlı doğal dil ve karar arayüzleri**

kullanılacak.

Bize göre gerçek mühendislik de tam olarak burada başlıyor.

Teknolojiyi probleme uydurmak.

Problemi moda olan teknolojiye değil.

* * *

# LoadFlux Data Flywheel

LoadFlux büyüdükçe en değerli varlık yalnızca uygulamanın kaynak kodu olmayacak.

Veri olacak.

Her operasyon bize şunları öğretecek:

*   hangi yük nereden nereye gidiyor,
    
*   hangi araçlar hangi bölgelerde çalışıyor,
    
*   hangi fiyat teklif ediliyor,
    
*   hangi fiyat kabul ediliyor,
    
*   hangi fiyat reddediliyor,
    
*   hangi rota tercih ediliyor,
    
*   hangi sapmalar kabul ediliyor,
    
*   hangi araç tipine hangi bölgede talep var,
    
*   hangi bölgede boş araç oluşuyor,
    
*   hangi saatlerde yük yoğunlaşıyor,
    
*   hangi rotalarda dönüş yükü bulunamıyor.
    

Bu da zaman içerisinde şu döngüyü oluşturacak:

```text
Daha fazla operasyon
        ↓
Daha fazla veri
        ↓
Daha iyi tahmin
        ↓
Daha iyi eşleşme
        ↓
Daha yüksek ekonomik değer
        ↓
Daha fazla kullanıcı
        ↓
Daha fazla operasyon
```

Bir arayüz kopyalanabilir.

Bir özellik kopyalanabilir.

Ama yıllar içerisinde oluşmuş lojistik karar verisi çok daha zor kopyalanır.

LoadFlux'un uzun vadeli teknoloji hendeğini burada görüyoruz.

* * *

# Avrupa'daki dönüşüm de önemli

Lojistik verisinin dijitalleşmesi yalnızca özel şirketlerin yönelimi değil.

Avrupa Birliği'nin eFTI düzenlemesi **9 Temmuz 2027'den itibaren tam olarak uygulanacak** ve üye devlet makamlarının sertifikalı eFTI platformları üzerinden elektronik olarak paylaşılan yük taşımacılığı bilgilerini kabul etmesi gerekecek.

Bu nedenle LoadFlux'un veri yapısını en başından:

*   makine tarafından okunabilir,
    
*   yapılandırılmış,
    
*   entegrasyona açık,
    
*   API-first düşünceye yakın
    

oluşturmak istiyoruz.

Gelecekte eFTI, e-CMR, ERP, TMS, telematik ve farklı taşıma ekosistemleriyle entegrasyon bu yüzden önemli olacak.

* * *

# Peki ilk sürümde ne olacak?

Burada önemli bir ayrım yapıyoruz.

LoadFlux'un ürün vizyonundaki bütün özellikler ana ürünün parçalarıdır.

Ama hepsini aynı gün geliştirmeye çalışmak mühendislik değil, plansızlık olur.

İlk çalışan uçtan uca zincir:

```text
Müşteri
   ↓
Taşıma Talebi
   ↓
Lojistik Firmaları
   ↓
Araç
   ↓
Trip / Rota
   ↓
Matching
   ↓
Opportunity
   ↓
Teklif
   ↓
Görüşme
   ↓
Anlaşma
   ↓
Taşıma
```

olacak.

Ardından sırasıyla:

### Phase 2

Route Compatibility Capacity Intelligence Advanced Matching

### Phase 3

Opportunity Packages Multi-load Optimization Profit Optimization

### Phase 4

Live GPS Dynamic Dispatch Real-Time Opportunities

### Phase 5

Price Intelligence Demand Heatmap Empty Return Prediction Next Best City

### Phase 6

AI Logistics Copilot Fleet-wide Optimization Predictive Dispatch

derinleşecek.

Yani ileri özellikler ayrı bir uygulama olmayacak.

**Hepsi LoadFlux olacak.**

* * *

# LoadFlux'un başarı metriği kullanıcı sayısı olmayacak

Bir milyon indirme güzel görünebilir.

Ama bizim için asıl soru:

> LoadFlux fiziksel lojistik sistemini gerçekten daha verimli hale getirdi mi?

Bu nedenle takip etmek istediğimiz esas metrikler arasında:

**Empty Kilometer Reduction**

Araçların boş kilometresini ne kadar azalttık?

**Average Capacity Utilization**

Araç kapasitesini ne kadar yükselttik?

**Opportunity Acceptance Rate**

Ürettiğimiz fırsatların ne kadarı gerçekten ekonomik değer yaratıyor?

**Time to First Offer**

Müşteri ilk kaliteli teklifini ne kadar hızlı alıyor?

**Match-to-Booking Conversion**

Ürettiğimiz eşleşmelerin kaçı gerçek taşıma operasyonuna dönüşüyor?

ve belki de en önemlisi:

# Opportunity Value Generated

> **LoadFlux müşterileri için ne kadar yeni ekonomik değer oluşturdu?**

olacak.

* * *

# Nihai hedefimiz

Bugün bir nakliyeci şu soruyu soruyor:

> “Aracım için yük var mı?”

LoadFlux'un ilk cevabı:

> “Evet. Sana en uygun yükleri buldum.”

Sonra soru değişecek:

> “Hangisini almalıyım?”

LoadFlux:

> “Şu ikisini birlikte alırsan daha kârlı.”

Bir süre sonra:

> “Bu teslimattan sonra nereye gitmeliyim?”

LoadFlux:

> “Konya'ya yönelmek önümüzdeki 24 saat için daha avantajlı görünüyor.”

Ve en sonunda filo yöneticisi şunu soracak:

> **“Önümüzdeki 48 saat için filomu nasıl çalıştırmalıyım?”**

LoadFlux bütün ağı değerlendirecek.

Araçları.

Yükleri.

Rotaları.

Sürücüleri.

Zamanı.

Talebi.

Kapasiteyi.

Maliyeti.

Riski.

Ve seçenekleri önüne koyacak.

İşte bizim LoadFlux ile ulaşmak istediğimiz yer tam olarak burası.

* * *

# Bir ilan platformu kurmuyoruz

Bunu özellikle tekrar etmek istiyoruz.

**LoadFlux'un meselesi ilan göstermek değil.**

Yük sahibinin daha hızlı taşıyıcı bulması önemli.

Nakliyecinin daha fazla iş bulması önemli.

Ama bunlar başlangıç.

Asıl hedef:

> **Zaten var olan fiziksel lojistik kapasitesinden daha fazla ekonomik değer üretmek.**

Yeni bir kamyon üretmeden.

Yeni bir yol yapmadan.

Yeni bir depo inşa etmeden.

Var olan araçları, var olan yükleri ve var olan hareketleri daha akıllı eşleştirerek.

Bugün lojistik ağı büyük ölçüde fiziksel.

Biz onun üzerine bir **karar katmanı** inşa etmek istiyoruz.

# LoadFlux

**Move Less Empty. Move More Value.**