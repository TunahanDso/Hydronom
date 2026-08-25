---
title: "Koltuk Sevdası"
datePublished: 2026-08-25T08:11:08.565Z
cuid: cmt8dyfel000009i793koda14
slug: koltuk-sevdas

---

Bazı takımlar yarışma kaybettikleri için dağılmaz.

Bazıları bütçesiz oldukları için de dağılmaz.

Eksik parça bulunur.  
Bozulan sensör değiştirilir.  
Yetişmeyen kartın etrafından dolaşılır.  
Olmayan donanım simüle edilir.  
Eksik insanın işi bir başkası tarafından öğrenilir.

Mühendislik zaten biraz da budur.

Elinizdekilerle, elinizde olmayanların arasındaki boşluğu kapatma sanatı.

Ama bir mühendislik takımının başına gelebilecek öyle bir problem vardır ki lehimle düzelmez, kodla patch’lenmez, torna tezgâhında yeniden üretilemez:

**Koltuk sevdası.**

Çünkü bir noktadan sonra projenin nasıl daha iyi olacağı değil, kimin yöneteceği konuşulmaya başlanır.

Teknik doğruların yerini kişisel otorite alır.

Üreten insanların rahatsız edici bulunmaya başladığı, sorgulamayan insanların ise “uyumlu” kabul edildiği bir düzen doğar.

Sonra herkes hâlâ bir takım olduğunu düşünür.

Formalar vardır.

Logolar vardır.

Kaptan vardır.

Toplantılar vardır.

Belki takım odasının kapısında aynı isim bile yazıyordur.

Ama içeride mühendislik kalmamıştır.

Benim anlatacağım hikâye biraz bunun hikâyesi.

İsim vermeyeceğim.

Çünkü gerek yok.

İnsanların isimleri birkaç yıl sonra unutulur.

**Ortaya çıkardıkları — ya da çıkaramadıkları — şeyler kalır.**

**Bir tekne yapmıyorduk**

Hydronom dışarıdan bakıldığında bir otonom deniz aracıydı.

Bir gövde.

Motorlar.

Sensörler.

Bilgisayar.

Elektronik.

Ve bunları taşıyan mekanik bir platform.

Ama benim için hiçbir zaman bundan ibaret olmadı.

Çünkü bir otonom sistemin asıl ürünü gövdesi değildir.

Asıl ürün, o gövdenin **ne yapacağına kendi başına karar verebilmesini sağlayan zekâdır.**

Zamanla Hydronom’un içerisinde navigasyon, görev yönetimi, arrival ve hold davranışları, güvenlik katmanları, aktüatör yönetimi, araç durum yönetimi, telemetri, haberleşme, sensör işleme ve karar mekanizmaları birbirinden ayrılmaya başladı.

Sistem büyüdü.

Bir yarışma teknesinin yazılımı olmaktan çıkıp gerçek bir otonom sistem mimarisine yaklaşmaya başladı.

Bu dönüşüm tesadüfen olmadı.

Çünkü önümüze sürekli problem çıkıyordu.

Ve çoğu zaman önümüzde iki seçenek vardı:

**“Olmuyor.” demek.**

Ya da:

**“Başka nasıl yaparız?” diye sormak.**

Biz yıllarca ikinci soruyu sorduk.

**Olmayan şeylerle çalışan bir sistem**

Bir noktada GPS yoktu.

Peki sistem tamamen kör mü kalacaktı?

Hayır.

IMU ve derinlik bilgisiyle daha düşük kabiliyetli bir çalışma modu tasarlandı.

Sistem bazı yeteneklerini kaybettiğinde tamamen çökmek yerine sahip olduğu sensörlere göre davranış değiştirebilecek hâle getirildi.

Bir başka yerde haberleşme güvenliği problemi vardı.

Telemetri yalnızca “paketi gönder, karşı taraf alsın” mantığında bırakılmadı.

Mesaj doğrulama ve bütünlük kontrolü düşünüldü.

Başka bir problemde sensör verisinin kendisi güvenilir değildi.

Bir sensöre körü körüne güvenmek yerine sistemin mevcut kabiliyetini değerlendiren yapılar geliştirildi.

Bir modül yetişmediğinde bütün sistemin onunla birlikte ölmemesi gerekiyordu.

Bu nedenle bileşenleri birbirinden ayırmaya başladık.

Decision başka bir şeydi.

Task başka bir şeydi.

Safety başka bir şeydi.

Actuator yönetimi başka bir şeydi.

Araç durumunun otoritesi bile ayrı düşünülüyordu.

Neden?

Çünkü mühendislikte çok erken öğrendiğim bir gerçek vardı:

**Bir sistem ancak parçalarından biri hata verdiğinde hayatta kalabiliyorsa gerçekten sistemdir.**

Kâğıt üzerinde güzel görünen mimariler yapmak kolaydır.

Asıl mesele cumartesi gecesi sensörünüz çalışmadığında ne yaptığınızdır.

Bizim hikâyemizin önemli bir bölümü de buydu.

Problem çıkıyordu.

Bakıyorduk.

Düşünüyorduk.

Bazen oldukça tuhaf bir çözüm buluyorduk.

Sonra kervanı yeniden yürütüyorduk.

Bir şey eksikse simüle ediyorduk.

Bir şey güvenilmezse çevresine koruma koyuyorduk.

Bir komponent yoksa sistem mimarisini onsuz yaşayabilecek hâle getiriyorduk.

Bazen mühendislik açısından ideal olmayan ama sistemi ayakta tutabilecek geçici çözümler geliştiriyorduk.

Çünkü laboratuvar ortamında “ideal sistem” tasarlamak başka bir şeydir.

**Gerçek dünyada çalışan sistem yapmak başka bir şey.**

Ve Hydronom’un asıl değeri biraz burada oluştu.

**Sonra Hydrocard geldi**

Bir süre sonra başka bir problem açıkça görülmeye başladı.

Otonom bir aracın içerisinde sürekli yeni kartlar, dönüştürücüler, bağlantılar, kablolar ve geçici elektronik çözümler birikiyordu.

Her yeni bağlantı başka bir hata noktasıydı.

Her konnektör başka bir risk.

Her ek kart başka bir entegrasyon problemi.

Bunun daha iyi bir yolu olmalıydı.

İşte Hydrocard burada doğdu.

Fikir aslında oldukça basitti:

**Aracın beynini ve sinir sistemini dağınık kartlardan kurtarıp mümkün olduğunca bütünleşik bir platform hâline getirmek.**

Hydrocard’ın görevleri düşünüldü.

Mimarisi oluşturuldu.

Kart baştan sona tasarlandı.

İş yalnızca “şöyle bir kart yapsak güzel olur” seviyesinde kalmadı.

Gerçek bir mühendislik ürünü olarak tasarlandı.

Sonra duvara çarptık.

Elektronik kartı tasarlamak yetmiyordu.

Onu hayata geçirmek gerekiyordu.

Ve daha da önemlisi:

**O kartın içerisinde yaşayacak gömülü yazılımı yazabilecek bir ekip gerekiyordu.**

İşte aylar boyunca çözülemeyen problemlerden biri buydu.

Kart vardı.

Fikir vardı.

Mimari vardı.

İhtiyaç vardı.

Ama kartın mikrodenetleyicisini gerçekten ayağa kaldırabilecek, çevre birimlerini sürebilecek, haberleşme katmanlarını güvenilir biçimde yazabilecek ve donanımı sistemin geri kalanıyla bütünleştirebilecek sürdürülebilir bir gömülü yazılım ekibi bir türlü oluşmadı.

Bu, dışarıdan bakıldığında küçük bir personel eksikliği gibi görülebilir.

Değildi.

Çünkü elektronik kart üzerinde birkaç LED yakmak gömülü sistem geliştirmek değildir.

UART çalıştırmak da değildir.

Bir yarışma demosunda motor döndürmek hiç değildir.

Gerçek gömülü sistem geliştirme;

watchdog’dur,

fault handling’dir,

zamanlamadır,

deterministik davranıştır,

memory yönetimidir,

sensör sürücüleridir,

haberleşme protokolleridir,

donanım-yazılım entegrasyonudur,

ve en önemlisi sistemin haftalar sonra da aynı güvenilirlikle çalışmasını sağlayabilmektir.

Hydrocard’ın önündeki büyük duvarlardan biri buydu.

**Kartın zekâsını yazabilecek insan zinciri kurulamadı.**

Aylar geçti.

Fakat ihtiyaç ortadan kalkmadı.

**Ve daha büyük bir problem vardı: mekanik**

Otonom araç projelerinde çok sevilen bir yanılgı vardır.

Ortaya güzel bir gövde çıkınca aracın büyük bölümünün tamamlandığı düşünülür.

CAD modeli güzeldir.

Gövde heybetlidir.

Metal parlar.

Motor yatakları yapılmıştır.

Montaj tamamlanmıştır.

Fotoğraf çekilir.

Ve ortaya fiziksel olarak “araç” diyebileceğiniz bir şey çıkar.

Ama çok basit bir soru sormak gerekir:

**Sonra?**

O gövde ne biliyor?

Ne görüyor?

Ne anlıyor?

Ne karar verebiliyor?

Beklenmeyen bir durumda ne yapıyor?

Rotasından çıktığında nasıl geri dönüyor?

Sensörü yanlış veri verdiğinde bunu anlayabiliyor mu?

Haberleşme kesildiğinde ne yapıyor?

Motorlardan biri beklenen tepkiyi vermediğinde bunu fark ediyor mu?

Eğer bunların cevabı yoksa karşınızda otonom araç yoktur.

Karşınızda yalnızca mekanik bir platform vardır.

Ve burada yıllarca canımı sıkan temel fikir ayrılıklarından biri buydu.

**Mekanik, sistemin kendisi değildir.**

Mekanik sistem, zekâyı dünyaya bağlayan bedendir.

Elbette çok değerlidir.

İyi mekanik tasarım olmadan kontrolcü ne kadar iyi olursa olsun araç başarısız olur.

Yanlış ağırlık merkezi bütün hesabınızı bozar.

Kötü tahrik yerleşimi kontrol edilebilirliği mahveder.

Titreşim sensör verisini kirletir.

Su izolasyonu kötü yapılırsa dünyanın en iyi yazılımı bir damla suya yenilebilir.

Ama bunun tersi de aynı derecede doğrudur.

Üzerinde karar sistemi bulunmayan mükemmel bir gövdenin otonom sistem açısından değeri sınırlıdır.

Çünkü:

**Akıl taşımayan bir iskelet, ne kadar güzel imal edilirse edilsin yine iskelettir.**

Hydronom’un gücü gövdesinin ne kadar güzel göründüğü değildi.

Onu diğer araçlardan ayırması gereken şey;

düşünmesi,

karar vermesi,

hata toleransı,

yazılım mimarisi,

sensörlerini anlamlandırması,

ve gerçek dünyada kendi davranışını üretebilmesiydi.

Bunu anlatmak ise her zaman kolay olmadı.

**Çünkü fotoğrafta kod görünmez**

Mekanik parçanın çok büyük bir avantajı vardır.

Fotoğrafını çekebilirsiniz.

Bir masanın üzerine koyabilirsiniz.

İnsanlara gösterebilirsiniz.

“Bunu biz yaptık.” dersiniz.

Yazılımda ise altı ay uğraştığınız bir safety architecture bazen ekranda yalnızca aracın saçma bir hareket yapmaması şeklinde görünür.

İki hafta uğraştığınız fault recovery sistemi yarışma boyunca hiç devreye girmezse kimse onun varlığını bile fark etmez.

İyi bir state management sistemi alkış almaz.

İyi bir embedded driver fotoğrafta güzel görünmez.

Doğru tasarlanmış bir haberleşme protokolünü elinize alıp Instagram’a koyamazsınız.

Ve öğrenci takımlarında bu yüzden tehlikeli bir durum oluşabilir:

**Görünen mühendislik, görünmeyen mühendisliğin önüne geçer.**

Oysa gerçek otonom sistemlerde çoğu zaman değer tam ters taraftadır.

Gövdeyi yeniden üretirsiniz.

Bir motoru değiştirirsiniz.

Yeni bir sensör satın alırsınız.

Ama yıllar içinde oluşmuş sistem bilgisini Amazon’dan sipariş veremezsiniz.

**İşte kavga aslında burada başladı**

Zamanla mesele teknik olmaktan çıktı.

Kim hangi işi yapıyor?

Kim hangi kararı veriyor?

Kim takımın başında?

Kim kime hesap verecek?

Kim hangi unvanı taşıyacak?

Bunlar giderek daha fazla önem kazanmaya başladı.

Ve benim açımdan en absürt taraf şuydu:

Biz bir yandan sistemdeki gerçek mühendislik açıklarını kapatmaya uğraşıyorduk.

Embedded ekip oluşmuyor.

Elektronik üretim yetişmiyor.

Mekanik tarafta entegrasyon problemleri çıkıyor.

Sensör sıkıntısı çıkıyor.

Haberleşme sıkıntısı çıkıyor.

Kaynak sıkıntısı çıkıyor.

Biz yine bir yol bulmaya çalışıyoruz.

Ama bütün bunların ortasında bazı insanlar için esas problem hâlâ organizasyon şemasındaki kutular olabiliyordu.

Bunu anlamakta zorlandım.

Hâlâ da zorlanıyorum.

Çünkü önünüzde çalışmayan bir araç varken kaptanlık makamının ne önemi vardır?

Araç çalışmıyorsa kaptan kimin kaptanıdır?

**Koltuk korunurken makine kaybedildi**

Sonunda ben ayrıldım.

Fakat ayrılık yalnızca bir insanın takımdan çıkması değildi.

Bunu o gün anlatmak zordu.

Bugün sonuçlara bakınca çok daha kolay.

Çünkü Hydronom benimle birlikte o yapıdan ayrıldı.

Hydrocard da ayrıldı.

Ve onlarla beraber;

yıllar içerisinde oluşan sistem mimarisi,

otonomi yaklaşımı,

yazılım altyapısı,

karar mekanizmaları,

geliştirme kültürü,

ve problemlere bakış biçiminin önemli bir bölümü de ayrıldı.

Bu cümle kibirli gelebilir.

O yüzden en sağlıklı ölçüyü kullanalım.

Sonrasına bakalım.

Çünkü mühendislikte iddiaların en acımasız hakemi sonuçtur.

Takımın önündeki yarışma süreçleri ilerleyemedi.

Teknik süreklilik kayboldu.

Bir dönem sürekli büyüyen sistemin yerine yeniden “yarışmaya araç yetiştirme” telaşı hakim oldu.

Ve zaman geçtikçe çok rahatsız edici bir soru ortaya çıktı:

**Koltuk uğruna aslında ne korunmuştu?**

Hydronom yok.

Hydrocard yok.

Otonomi mimarisi yok.

Teknik süreklilik yok.

Yarışma başarısı yok.

Peki geriye ne kaldı?

Unvanlar mı?

**Fakat Hydronom ölmedi**

İşin bütün ironisi burada başlıyor.

Çünkü Hydronom takımından ayrıldıktan sonra ortadan kaybolmadı.

Tam tersine hayatı ilk kez yarışma sınırlarının dışına çıktı.

Teknolojik birikiminin bir bölümü özel sektör tarafında gerçek ticari karşılık bulmaya başladı.

Ve başka bir tarafta Hydronom’un taşıdığı düşünce yaşamaya devam etti.

Oradan **Tydronom** doğdu.

Tydronom yalnızca isim değişikliği değildi.

Bir yarışma aracının öğrendiklerinin gerçek bir ürün yaklaşımına taşınmasıydı.

Hydronom artık yalnızca kendi başına bir proje değildi.

Bir teknolojik soyun başlangıcı olmuştu.

Daha sonra bu soy **ATA** gibi daha büyük sistem vizyonlarının içerisine uzanmaya başladı.

Bu yüzden bazen şakayla karışık söylediğim ama aslında oldukça ciddi bir cümle var:

**Hydronom baba oldu.**

Bir çocuk yaptı.

O çocuk başka sistemlerin yolunu açmaya başladı.

Bir zamanlar birkaç şamandıra arasında doğru rotayı bulmaya çalışan öğrenci projesinin genleri bugün bambaşka makinelerin içerisinde yaşamaya hazırlanıyor.

Bir proje için bundan daha büyük başarı olabilir mi?

Kupa mı?

Plaket mi?

Sahnedeki fotoğraf mı?

Bence değil.

**Gerçek mühendislik mirası, projeniz artık sizin elinizde değilken bile yaşamaya devam ediyorsa oluşur.**

**Hydrocard’ın daha da acı ironisi**

Hydrocard’ın hikâyesi ise belki bundan bile daha sert.

Çünkü kart üretilemedi.

Aylar boyunca onu gerçek anlamda ayağa kaldırabilecek elektronik ve gömülü geliştirme kapasitesi oluşturulamadı.

Ama problem gerçekti.

Dağınık elektronik mimarinin yarattığı entegrasyon yükü gerçekti.

Tek kart üzerinde daha fazla hesaplama ve kontrol fonksiyonunun birleştirilmesi ihtiyacı gerçekti.

Ve yalnızca birkaç ay sonra benzer ihtiyaçlara cevap veren bütünleşik platformların çok daha büyük yapılarda ortaya çıkmaya başlamasını görmek benim için oldukça öğreticiydi.

Bu bana şunu gösterdi:

Bizim problem tanımımız yanlış değildi.

Hydrocard gereksiz değildi.

Hayal ürünü hiç değildi.

**Sadece doğru fikri ürüne dönüştürecek organizasyonel ve teknik zincir kurulamadı.**

Ve mühendislik tarihinde bunun örnekleri doludur.

İyi fikirler her zaman iyi ürünlere dönüşmez.

Çünkü fikir ile ürün arasında insan vardır.

Disiplin vardır.

Teknik yeterlilik vardır.

Sabır vardır.

Ve ekip kültürü vardır.

Bir tanesi eksik olduğunda kâğıt üzerindeki en güzel kart bile yalnızca PCB dosyası olarak kalabilir.

**En büyük yanılgımız neydi?**

Belki benim de en büyük hatalarımdan biri şuydu:

Teknik olarak doğru bir şeyin, yeterince iyi anlatılırsa mutlaka kabul göreceğini düşünmek.

Öyle olmuyor.

Mühendislik ekipleri yalnızca mühendislikten oluşmuyor.

Ego var.

Statü var.

Gruplaşma var.

İnsan ilişkileri var.

Kendini tehdit altında hissetme var.

Ve bazen bir insan sizin ürettiğiniz şeyi kötü bulduğu için değil, **sizin üretmeye devam etmenizin kendi pozisyonunu anlamsızlaştıracağından korktuğu için** size direnebiliyor.

İşte burada bir takımın karakteri ortaya çıkar.

İyi kurum yetenekli insanı çoğaltır.

Kötü kurum ise yetenekli insanı törpüler.

İyi lider kendisinden daha iyi mühendisleri etrafında görmek ister.

Kötü lider ise en rahat kendisinden daha zayıf insanların arasında hisseder.

Çünkü birincisi ürün ister.

İkincisi kontrol ister.

**Bir kaptanın gerçek testi**

Kaptanlık bana hiçbir zaman romantik gelmedi.

Gerçek liderlik takım fotoğrafının ortasında durmak değildir.

Gerçek liderlik bazen herkes gittikten sonra laboratuvarda kalan son kişi olmaktır.

Bazen bilmediğiniz bir teknolojiyi üç gecede öğrenmektir.

Bazen başka disiplinin açık verdiği yerde “bu benim alanım değil” demek yerine problemi çözmektir.

Bazen aylarca geliştirdiğiniz şeyi çöpe atıp daha doğru bir mimariye geçmektir.

Ve en önemlisi:

**Kendinizi gereksiz hâle getirecek kadar iyi bir sistem kurmaktır.**

Çünkü iyi liderin en büyük başarısı, kendisi gittikten sonra takımın onu aramamasıdır.

Takım daha iyi çalışmalıdır.

Yeni insanlar gelmelidir.

Eski projeler büyümelidir.

Yeni projeler doğmalıdır.

Bilgi aktarılmalıdır.

Sonraki nesil öncekinden daha iyi olmalıdır.

Eğer bir insan gittikten sonra bütün kritik sistemler de gidiyorsa ortada kurumsallaşma problemi vardır.

Bunda giden kişinin de sorumluluğu olabilir.

Kalan yönetimin de.

Ben bugün kendi payımı da görüyorum.

Daha fazla dokümantasyon yapılabilirdi.

Daha fazla insan yetiştirilebilirdi.

Bilgi daha fazla dağıtılabilirdi.

Bazı çatışmalar daha iyi yönetilebilirdi.

Bunları inkâr etmiyorum.

Ama bir şey yine de değişmiyor:

Bir organizasyon, elindeki üretim kapasitesini korumak yerine onunla savaşmayı seçiyorsa sonuçlarına da katlanır.

**Çünkü rozet makine yapmaz**

Bütün bu hikâyeden sonra genç mühendislerin aklında tek bir şey kalmasını isterim.

Bir takımın değerini;

kaç kişilik olduğu,

kaç yöneticisi bulunduğu,

kaç alt takıma ayrıldığı,

kaç sponsoru olduğu,

kaç güzel render hazırladığı,

kaç sosyal medya paylaşımı yaptığı

belirlemez.

Bir soru belirler:

**Çalışıyor mu?**

Kart çalışıyor mu?

Kod çalışıyor mu?

Araç çalışıyor mu?

Sistem hata aldığında yaşamaya devam ediyor mu?

Gerçek dünyaya çıktığında tasarladığınız davranışı gösterebiliyor mu?

Gösteremiyorsa organizasyon şemanızın ne kadar güzel olduğunun hiçbir önemi yoktur.

Rozet makine yapmaz.

Unvan algoritma yazmaz.

Kaptanlık mikrodenetleyici programlamaz.

Toplantı tutanağı tekneyi otonom yapmaz.

Ve hiçbir yönetmelik fizik kanunlarını ikna edemez.

**Sonunda zaman hakem oluyor**

Bugün geriye dönüp baktığımda yaşananların hiçbirine sevinmiyorum.

Takımın başarısız olması benim başarım değildir.

Keşke ben ayrıldıktan sonra daha iyi bir Hydronom yapsalardı.

Keşke Hydrocard’dan çok daha iyi bir kart geliştirseydiler.

Keşke yarışmalara gidip bizim dönemimizin bütün sonuçlarını geçselerdi.

Bundan gerçekten mutluluk duyardım.

Çünkü teknoloji böyle ilerler.

Sonraki nesil öncekini geçer.

Geçmelidir.

Ama olmadı.

Ve aynı zaman diliminde çok garip bir terslik yaşandı.

Bir tarafta Hydronom’un çıktığı yapı küçülürken,

diğer tarafta Hydronom’un fikri büyüdü.

Bir tarafta Hydrocard yapılamazken,

diğer tarafta onun çözmeye çalıştığı problemin ne kadar gerçek olduğu hızla ortaya çıktı.

Bir tarafta makamlar korunurken,

diğer tarafta teknoloji başka yerlere göç etti.

İşte bu yüzden bugün geçmişe bakarken hissettiğim temel duygu öfke değil.

**İroni.**

Çok büyük bir ironi.

Çünkü yıllar boyunca korunmak için bu kadar mücadele edilen koltukların gerçekten ne kadar değersiz olduğunu sonunda zaman gösterdi.

Bir gün üniversiteden mezun oluyorsunuz.

Takım kartınız iptal ediliyor.

Laboratuvar anahtarını teslim ediyorsunuz.

WhatsApp grubundan çıkıyorsunuz.

LinkedIn’deki “Team Captain” satırı geçmiş deneyimlerin arasına gömülüyor.

Ve geriye tek bir soru kalıyor:

**Ne ürettin?**

Çalışan bir sistem mi?

Bir teknoloji mi?

Bir mühendis mi yetiştirdin?

Yeni bir yöntem mi bıraktın?

Senden sonra yaşamaya devam eden bir mimari mi?

Yoksa yalnızca bir dönem boyunca bir sandalyede mi oturdun?

Ben Hydronom’a baktığımda cevabımı biliyorum.

Hydronom hâlâ yaşıyor.

Başka biçimlerde.

Başka makinelerde.

Başka isimlerle.

Çocuklarıyla.

Belki bir gün torunlarıyla.

Onu doğuran takım ise bugün başka bir hikâyenin içinde.

Ve galiba bütün yaşananları anlatabilecek en kısa cümle hâlâ aynı:

**Koltuk yerinde kaldı.**

**Gelecek ise kapıdan çıkıp gitti.**

[https://github.com/TunahanDso/Hydronom](https://github.com/TunahanDso/Hydronom)