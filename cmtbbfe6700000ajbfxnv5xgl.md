---
title: "Vira Bismillah: TUNIX Football Digital Twin"
datePublished: 2026-08-27T09:23:39.821Z
cuid: cmtbbfe6700000ajbfxnv5xgl
slug: vira-bismillah-tunix-football-digital-twin

---

## Futbolu tahmin etmeye değil, modellemeye başlıyoruz.

Bazen bir proje aylarca yapılan fizibilitelerden, onlarca toplantıdan ve yüzlerce sayfalık dokümandan doğar.

Bazen de insan bir futbol istatistiği paylaşımının altında birkaç satır okur ve şu soruyu sorar:

**“Peki bunu gerçekten doğru yapmak isteseydik nasıl yapardık?”**

TUNIX Football Digital Twin biraz böyle doğdu.

Bir takımın şampiyonluk ihtimalini yalnızca geçmiş maç skorlarından hesaplayan bir model üzerine düşünürken problem giderek büyüdü.

Çünkü futbol yalnızca geçmiş skorların toplamı değildi.

Bir futbol takımı yaşayan bir sistemdi.

Transfer yapıyor.

Oyuncu kaybediyor.

Teknik direktör değiştiriyor.

Sakatlanıyor.

Yoruluyor.

Taktik değiştiriyor.

Avrupa kupasına gidiyor.

Yoğun fikstüre giriyor.

Bir oyuncusu formunun zirvesine çıkıyor.

Başka bir oyuncusu aylardır sahaya dönemiyor.

Ekonomik olarak güçleniyor veya küçülüyor.

Rakibinin başına başka bir şey geliyor.

Ve bütün bunlar daha skor tabelasına yansımadan takımın gerçek durumunu değiştiriyor.

İşte bizim başlangıç sorumuz tam olarak burada değişti.

Artık mesele:

> “Bu takım şampiyon olur mu?”

değildi.

Asıl soru şuydu:

> **Bir futbol kulübünün belirli bir andaki gerçek durumunu dijital ortamda temsil edebilir miyiz?**

Ve daha önemlisi:

> **Bu dijital dünyayı geleceğe doğru binlerce, yüz binlerce kez çalıştırabilir miyiz?**

Cevabımız:

**Deneyeceğiz.**

Vira Bismillah.

* * *

# Bir tahmin sitesi yapmıyoruz

İlk günden bunun sınırını doğru çizmek gerekiyor.

TUNIX Football Digital Twin bir:

*   skor tahmin sitesi,
    
*   klasik futbol istatistik sitesi,
    
*   Elo tablosu,
    
*   transfer haber sitesi,
    
*   “AI bugün ne dedi?” uygulaması
    

olmayacak.

Hedefimiz daha büyük.

Futbol dünyasının mümkün olduğunca geniş bölümünü zamana bağlı bir **digital twin** içerisinde temsil etmek istiyoruz.

Bir başka ifadeyle sistemin herhangi bir (t) anındaki futbol dünyası için bir state'i olacak:

$$\mathcal{W}_t = FootballWorldState(t)$$

Bu state içerisinde:

*   kulüpler,
    
*   oyuncular,
    
*   teknik direktörler,
    
*   kadrolar,
    
*   sözleşmeler,
    
*   transferler,
    
*   sakatlıklar,
    
*   cezalar,
    
*   maçlar,
    
*   organizasyonlar,
    
*   fikstür,
    
*   performans,
    
*   form,
    
*   piyasa değerleri,
    
*   takım güçleri,
    
*   oyuncu güçleri,
    
*   belirsizlikler,
    
*   taktik yapılar,
    
*   hava koşulları,
    
*   seyahat,
    
*   dinlenme süreleri,
    
*   tarihsel olaylar
    

ve zamanla ekleyeceğimiz çok daha fazla değişken yer alacak.

Amacımız tek bir sayı hesaplamak değil.

Amacımız futbol dünyasının **hesaplanabilir bir temsilini oluşturmak.**

* * *

# Skoru sonuç olarak görüyoruz, sebep olarak değil

Bir takımın son beş maçta dört galibiyet alması önemli bir veridir.

Ama neden kazandı?

Yeni transfer mi geldi?

Rakipler mi zayıftı?

Forvet olağanüstü bitiricilik mi gösterdi?

Takımın gerçek üretimi mi arttı?

Kaleci normalin üzerinde mi kurtardı?

Teknik direktör sistem mi değiştirdi?

Rakipler kırmızı kart mı gördü?

Fikstür mü kolaydı?

Bunların tamamını yalnızca:

```text
2-0
1-0
3-1
0-1
2-1
```

üzerinden öğrenmeye çalışmak mümkündür.

Fakat bilgi kaybı büyüktür.

Biz skoru çöpe atmıyoruz.

Tam tersine skor, maç modelimizin önemli gözlemlerinden biri olacak.

Fakat sistemin dünyaya açılan tek penceresi olmayacak.

* * *

# Temel yaklaşım: Latent Football State

Bir takımın “gerçek gücü” doğrudan ölçülebilen bir büyüklük değil.

Bu nedenle takım durumunu bir **latent state** olarak ele almak istiyoruz.

Örneğin:

$$S_{team,t} = [ A_t, D_t, B_t, T_t, P_t, SP_t, DEP_t, FIT_t, FAT_t, CHEM_t, CONF_t, UNC_t ]$$

Burada örneğin:

*   (A\_t): hücum gücü
    
*   (D\_t): savunma gücü
    
*   (B\_t): build-up kapasitesi
    
*   (T\_t): transition gücü
    
*   (P\_t): pressing kapasitesi
    
*   (SP\_t): duran top gücü
    
*   (DEP\_t): kadro derinliği
    
*   (FIT\_t): oyuncu uygunluğu
    
*   (FAT\_t): yorgunluk
    
*   (CHEM\_t): takım uyumu
    
*   (CONF\_t): form/güven etkisi
    
*   (UNC\_t): model belirsizliği
    

gibi değişkenleri temsil edebilir.

Bunların nihai tanımları elbette araştırma, veri ve backtest sonucunda şekillenecek.

Buradaki kritik düşünce şu:

**Takım gücü tek sayı olmak zorunda değil.**

Ve daha da önemlisi:

**Takım gücü sabit değil.**

* * *

# Futbol bir state-space problemi olarak ele alınabilir

Sistemin matematiksel omurgasında değerlendirdiğimiz yapılardan biri state-space yaklaşımı.

Basitleştirirsek:

$$x_{t+1}=f(x_t,u_t)+w_t$$

ve

$$y_t=g(x_t)+v_t$$

Burada:

*   (x\_t): doğrudan göremediğimiz gerçek futbol state'i
    
*   (u\_t): dışarıdan gelen olaylar
    
*   (y\_t): gözlediğimiz maç ve performans verileri
    
*   (w\_t): sistem belirsizliği
    
*   (v\_t): ölçüm gürültüsü
    

(u\_t) dediğimiz şey ise tam olarak futbolun gerçek hayatı:

$$u_t = [ Transfer, Injury, Suspension, CoachChange, Fixture, Fatigue, SquadChange, ... ]$$

Bu ayrım bizim için çok önemli.

Çünkü bir takım dünya çapında bir futbolcu transfer ettiğinde gerçek takım state'i ilk maçın bitmesini beklemez.

**Gerçek dünya o anda değişmiştir.**

Dijital modelin de mümkün olduğunca o anda değişmesi gerekir.

* * *

# Salah problemi

Basit bir örnek.

Bir takımın dün itibarıyla hücum gücü:

$$A_t = 72$$

olsun.

Bugün dünya çapında bir hücum oyuncusu transfer edildiğini düşünelim.

Sadece geçmiş skorlara dayanan bir sistem bu transferden habersizdir.

Takım birkaç maç oynar.

Yeni oyuncu gol atar.

Takım kazanır.

Model sonuçları görür.

Takım gücünü yükseltir.

Bizim hedefimiz farklı.

Transfer gerçekleştiği anda:

$$TransferEvent \rightarrow SquadState \rightarrow PlayerImpact \rightarrow TeamState$$

zinciri çalışmalı.

Maç oynandıktan sonra ise sistem ikinci kez güncellenmeli:

$$ExpectedPerformance \leftrightarrow ObservedPerformance$$

Yani model hem **ön bilgiye** hem de **sahadaki kanıta** sahip olacak.

* * *

# Oyuncu yalnızca piyasa değerinden ibaret değil

Transfermarkt ve benzeri kaynaklar sistemimizin veri kaynaklarından bazıları olabilir.

Ancak:

$$MarketValue \neq FootballAbility$$

Bir futbolcunun piyasa değeri;

*   yaştan,
    
*   kontrat süresinden,
    
*   potansiyelden,
    
*   ticari değerden,
    
*   ligden,
    
*   kulüpten,
    
*   talep seviyesinden
    

etkilenebilir.

Biz oyuncuyu çok boyutlu bir state ile temsil etmek istiyoruz.

Örneğin:

$$P_i = [ Finishing, ShotGeneration, Passing, Creation, Progression, Dribbling, PressResistance, Pressing, Tackling, Interception, Aerial, Physical, Positioning, Availability, Form ]$$

Bunların tamamının ilk sürümde bulunması gerekmiyor.

Ama mimari, gelecekte bulunmalarına engel olmayacak şekilde kurulmalı.

* * *

# Oyuncu gücü bağlama göre değişmeli

Bir başka önemli düşüncemiz:

$$PlayerImpact \neq Constant$$

Bir oyuncunun katkısı sadece kendi yeteneğinin fonksiyonu değildir.

Daha doğru yaklaşım:

$$Impact = f( Player, Team, Coach, Role, League, Formation, Opposition )$$

Bir sağ kanat oyuncusunun zaten aynı bölgede elit oyuncuya sahip bir kadroya eklenmesi ile o pozisyonda büyük açık bulunan takıma transfer olması aynı marjinal etkiyi oluşturmayabilir.

İlk yaklaşımımız örneğin:

$$TransferImpact = Quality \times ExpectedMinutes \times TacticalFit \times PositionNeed \times LeagueAdjustment \times Adaptation$$

gibi modellenebilir.

Ama burada özellikle “formülü bulduk” demiyoruz.

**Formülü veriden öğreneceğiz.**

* * *

# İlk 11 yetmez

İki takım düşünelim.

İkisinin de ideal ilk 11 gücü 85.

Birinin rotasyonu 83.

Diğerinin rotasyonu 67.

Bu iki takım 38 maçlık ligde aynı değildir.

Hele birisi Avrupa kupalarında oynuyorsa hiç değildir.

Dolayısıyla:

$$SquadStrength = w_1 FirstXI+ w_2 Rotation+ w_3 Depth$$

gibi bir yapı gerekir.

Üstelik (w\_1,w\_2,w\_3) sabit olmak zorunda değildir.

Bir takım üç günde bir maç oynuyorsa kadro derinliğinin değeri artmalıdır.

* * *

# Yorgunluk da state'in parçası

Bir futbolcu pazar günü 90 dakika, çarşamba günü 120 dakika, cumartesi tekrar 90 dakika oynuyorsa matematiksel model açısından üç maçta da aynı oyuncu değildir.

Bu nedenle oyuncu bazında bir workload/fatigue state'i düşünüyoruz:

# $$ F\_{i,t+1}

## F\_{i,t} + Load\_{i,t}

Recovery\_{i,t} $$

Takım yorgunluğu ise oyuncu durumlarından üretilebilir.

Böylece:

*   Avrupa kupası,
    
*   kupa maçları,
    
*   seyahat,
    
*   uzatma,
    
*   rotasyon,
    
*   dinlenme günü
    

gibi faktörler maç modeline girebilir.

* * *

# Sakatlık: 0 veya 1 değil

Oyuncu durumunu:

```text
healthy
injured
```

şeklinde iki değerli tutmak istemiyoruz.

Mümkün olduğu ölçüde:

*   sakatlık türü,
    
*   başlangıç zamanı,
    
*   beklenen dönüş,
    
*   recurrence riski,
    
*   antrenman durumu,
    
*   dakika kısıtlaması,
    
*   maç kondisyonu
    

gibi bilgileri değerlendirebiliriz.

Dolayısıyla sistem:

$$P(Available_i,t)$$

veya

$$P(Start_i,t)$$

hesaplayabilir.

Bir futbolcu için gerçek dünya çoğu zaman %0 veya %100 değildir.

* * *

# Teknik direktör bir metadata değildir

Teknik direktör değişikliği de yalnızca:

```text
coach_id = 2819
```

güncellemesi olmayacak.

Uzun vadede teknik direktörleri de çok boyutlu modellerle temsil etmek istiyoruz.

Örneğin:

$$CoachVector = [ Attack, Defence, Press, Possession, Transition, Development, Rotation, GameManagement ]$$

ve:

$$CoachFit = f(Coach,Squad)$$

hesaplanabilir.

Çünkü aynı teknik direktör iki farklı kadroda aynı etkiyi göstermek zorunda değildir.

* * *

# Taktik eşleşmeleri

Futbolun güzel taraflarından biri:

$$A>B$$

ve

$$B>C$$

olmasının mutlaka

$$A>C$$

anlamına gelmemesidir.

Çünkü futbol yalnızca strength karşılaştırması değildir.

**Match-up problemidir.**

Uzun vadede takımları birer style vector ile temsil etmeyi hedefliyoruz:

$$Style = [ Possession, Tempo, Width, PressIntensity, Directness, CounterAttack, HighLine, CrossFrequency, BuildUpRisk ]$$

Sonrasında:

$$Matchup(A,B)$$

takımların stil etkileşimini modele ekleyebilir.

* * *

# Maç motoru

Dixon-Coles gibi akademik olarak bilinen modeller bizim için başlangıç noktası ve baseline olabilir.

Örneğin:

$$\lambda_H = f( Attack_H, Defence_A, HomeAdvantage, Lineup, Fatigue, Matchup, ... )$$

ve

$$\lambda_A = f( Attack_A, Defence_H, ... )$$

hesaplanabilir.

Buradan skor dağılımı:

$$P(X=x,Y=y)$$

elde edilir.

Ancak kendimizi tek modele kilitlemeyeceğiz.

Araştıracağımız adaylar arasında:

*   Poisson modelleri
    
*   Dixon-Coles
    
*   Bivariate Poisson
    
*   Bayesian hierarchical modeller
    
*   state-space modeller
    
*   Elo türevleri
    
*   gradient boosting
    
*   neural networks
    
*   ensemble modeller
    

olacak.

Kazananı isminden dolayı değil, **backtest sonucundan dolayı** seçeceğiz.

* * *

# Bize ait bir model

Burada önemli bir çizgimiz var.

PyTorch kullanabiliriz.

JAX kullanabiliriz.

NumPy kullanabiliriz.

PyMC kullanabiliriz.

Scikit-learn kullanabiliriz.

Hazır matematik kütüphanelerini yeniden yazmanın mühendislik açısından hiçbir anlamı yok.

Fakat:

*   Player Intelligence Model,
    
*   Team State Model,
    
*   Transfer Impact Model,
    
*   Match Engine,
    
*   World Simulation Engine
    

bizim problem tanımımızla ve bizim verimizle geliştirilecek.

Bir başka şirketin API'sinden:

```text
team_strength = 82
```

alıp üzerine arayüz yapmak istemiyoruz.

**State'in sahibi biz olmalıyız.**

* * *

# En değerli varlığımız zamanla kod olmayacak

Bu projenin uzun vadede en önemli bölümü:

## Temporal Football Knowledge Base

olacak.

Sistem yalnızca bugünü bilmeyecek.

Bir bilginin **ne zaman doğru olduğunu** da bilecek.

Örneğin:

```text
player: X
club: Y

valid_from:
valid_until:

observed_at:

source:
confidence:
version:
```

tutacağız.

Burada iki farklı zaman kavramı özellikle önemli.

### Valid time

O bilgi gerçek dünyada ne zaman geçerliydi?

### Observed time

Biz o bilgiyi ne zaman öğrendik?

Bu ayrım geçmişe dönük model testleri için hayati.

* * *

# 1 Ağustos 2026 saat 18.00'de ne biliyorduk?

Sistemin ileride şu soruyu cevaplayabilmesini istiyoruz:

> “1 Ağustos 2026 saat 18.00 itibarıyla veri tabanımız ne biliyordu?”

Bugünün bilgilerini alıp geçmişe götürmek kolaydır.

Ama bu **data leakage** yaratır.

Biz geçmiş modeli gerçekten geçmişte sahip olduğu bilgiyle çalıştırmak istiyoruz.

Bu nedenle altyapımızın merkezindeki fikirlerden biri:

# Event Sourcing

olacak.

* * *

# Her şey bir olay

Transfer:

```text
PLAYER_TRANSFERRED
```

Sakatlık:

```text
PLAYER_INJURED
```

İyileşme:

```text
PLAYER_RETURNED
```

Teknik direktör:

```text
COACH_CHANGED
```

Maç:

```text
MATCH_FINISHED
```

Kadrolar:

```text
LINEUP_CONFIRMED
```

Ceza:

```text
PLAYER_SUSPENDED
```

Her olay world state'i değiştirecek.

Kabaca:

```text
Event
   ↓
Football Event Bus
   ↓
State Engine
   ↓
Feature Engine
   ↓
Prediction Engine
   ↓
Simulation Engine
```

* * *

# Veri: Tek kaynağa bağlı olmayacağız

Transfermarkt önemli bir kaynak.

Ama tek kaynak olmayacak.

Hedefimiz farklı kategorilerde çok sayıda kaynağı bağlayabilecek bir collector mimarisi.

Örneğin:

```text
Federation Collectors
League Collectors
Club Collectors
Transfer Collectors
Statistics Collectors
News Collectors
Market Value Collectors
Odds Collectors
Weather Collectors
Historical Data Collectors
              ↓
           Raw Lake
              ↓
         Normalization
              ↓
      Entity Resolution
              ↓
        Validation
              ↓
        Canonical DB
```

Bazı kaynaklar API olabilir.

Bazıları lisanslı veri feed'i olabilir.

Bazıları izin verilen otomatik veri toplama yöntemleri olabilir.

Bazıları manuel doğrulama gerektirebilir.

Burada önemli bir prensibimiz daha olacak:

**Bir kaynağa ulaşabiliyor olmak o verinin ticari kullanım hakkına sahip olduğumuz anlamına gelmez.**

Bu nedenle kullanım koşulları, lisanslama, rate limit ve veri sahipliği projenin teknik mimarisi kadar ciddi ele alınacak.

* * *

# Raw veriyi kaybetmeyeceğiz

Collector bir kaynaktan veri çektiğinde yalnız normalize edilmiş sonucunu saklamak büyük hata olur.

Ham veri de tutulacak.

Örneğin:

```text
/raw
    /source-a
    /source-b
    /federation
    /clubs
    /matches
```

Neden?

Çünkü iki yıl sonra parser'ımızdaki bir hatayı fark edebiliriz.

Ham veri elimizdeyse:

**yeniden işleyebiliriz.**

Yoksa:

**tarih kaybolmuştur.**

* * *

# Entity Resolution

Bu proje yapılırken muhtemelen en az konuşulup en çok küfür ettirecek bölümlerden biri bu olacak.

Aynı oyuncu farklı kaynaklarda:

```text
Mohamed Salah
M. Salah
Mohamed S.
M Salah
```

olarak bulunabilir.

Kulüplerin isimleri değişebilir.

Liglerin sponsor isimleri değişebilir.

Oyuncular aynı isimde olabilir.

Transfer kayıtlarında yazım hatası olabilir.

Bu yüzden merkezi bir kimlik sistemi kuracağız.

Her gerçek dünya varlığının bizim sistemimizde canonical ID'si olacak.

```text
PlayerId
ClubId
CoachId
CompetitionId
MatchId
```

Dış kaynak ID'leri bunlara map edilecek.

Model doğrudan kaynak isimleriyle çalışmayacak.

* * *

# Kaynak güvenilirliği

Her veri aynı değerde değildir.

Resmi kulüp açıklaması ile sosyal medyada dolaşan transfer söylentisi aynı confidence ile modele giremez.

Bu nedenle kaynak katmanında:

$$R_s = SourceReliability$$

tutmayı planlıyoruz.

Örneğin olaylar:

```text
source_count
source_reliability
event_confidence
verification_state
```

taşıyabilir.

Çelişkili bilgiler conflict resolution sistemine girebilir.

* * *

# Söylenti bile ayrı bir evren olabilir

Bu bizi çok güzel bir özelliğe getiriyor.

Sistem yalnız gerçekleşen dünyayı simüle etmek zorunda değil.

Kullanıcı şunu sorabilmeli:

> “Bu transfer gerçekleşirse ne olur?”

Örneğin:

### Universe A

Mevcut dünya.

### Universe B

Belirli oyuncu transfer edildi.

Sonra iki state ayrı ayrı simüle edilir.

```text
Championship

Current: 18.4%
Scenario: 27.9%

Δ = +9.5 percentage points
```

Ama yalnızca şampiyonluk değil:

```text
Expected Points
Expected Goals
Top 2
Top 4
European Qualification
Cup Probability
Squad Depth
Attack Strength
Rotation Quality
```

gibi sonuçlar karşılaştırılabilir.

* * *

# Scenario Engine

Bu sistemin en heyecan verici parçalarından biri olacak.

Kullanıcı gelecekte gerçekleşebilecek olayları kendisi oluşturabilecek.

Örneğin:

> Bir futbolcu üç ay sakatlanırsa?

> Teknik direktör ayrılırsa?

> Takıma altı puan ceza verilirse?

> Yeni santrfor gelirse?

> Rakip Avrupa'da yarı finale kadar gider ve fikstürü sıkışırsa?

> Bir oyuncu sezon sonuna kadar oynayamazsa?

Current world-state'i değiştirmeyeceğiz.

Fork oluşturacağız.

$$World_A = Current$$

$$World_B = Current + Scenario$$

İki dünya ayrı ayrı simüle edilecek.

Bu noktada artık futbol tahmin sitesinden çıkıp gerçek bir:

# Football Scenario Laboratory

haline geliyoruz.

* * *

# World Simulation Engine

Bir şampiyonluk ihtimalinin tek sezon simülasyonundan anlamı yok.

Güncel world state'i aldıktan sonra geleceği:

$$N=10,000$$

$$N=100,000$$

ve gerektiğinde daha fazla kez simüle edeceğiz.

Her evrende:

*   maç sonuçları,
    
*   takım state belirsizliği,
    
*   oyuncu availability,
    
*   performans varyansı,
    
*   fikstür,
    
*   yorgunluk
    

örneklenebilir.

Sonrasında:

```text
Championship        27.42%
Top 2               53.61%
Top 4               78.04%
Europe              89.12%
Relegation           0.19%

Expected Points      72.8
Median Points        73
90% Interval         62–83
```

gibi dağılımlar üretilebilir.

* * *

# Tek sayı değil, belirsizlik

Bir başka temel prensibimiz:

> **Model kendinden ne kadar emin olmadığını da bilmeli.**

Dolayısıyla:

```text
Team Strength = 84
```

yerine mümkün olduğunca:

$$Strength = 84.1$$

$$90\%CI=[80.8,87.4]$$

gibi dağılımlarla çalışmak istiyoruz.

Yeni yükselmiş bir takım hakkında çok az veri varsa belirsizliği yüksek olmalı.

Yıllardır takip ettiğimiz bir takımda belirsizlik daha dar olabilir.

**Bilmiyorsak bilmiyoruz demeyi modelin matematiğine koymak istiyoruz.**

* * *

# Records Engine

Projeyi yalnız gelecekle sınırlamıyoruz.

Elimizde doğru temporal veri tabanı oluştuğunda futbol tarihini de sorgulanabilir hale getirmek istiyoruz.

Örneğin:

> Süper Lig tarihindeki en uzun galibiyet serisi nedir?

kolay soru.

Ama sistem zamanla şunları da cevaplayabilmeli:

> 21 yaşından küçük bir oyuncunun bir sezonda attığı en fazla deplasman golü?

> Trabzonspor'un Galatasaray karşısındaki en uzun yenilmezlik serisi?

> Son 20 yılda lige yükselen takımlar arasında ilk 10 haftada en fazla puan toplayan takım?

> Bir sezonda deplasmanda hiç kaybetmeden en uzun seri yapan teknik direktör?

Bunların her biri için önceden elle “rekor” tanımlamak istemiyoruz.

Bunun yerine bir:

# Football Query Engine

kurmak istiyoruz.

Örneğin:

```text
entity = player
competition = super_league
age < 21
metric = away_goals
scope = season
aggregation = sum
sort = descending
limit = 1
```

ve sorgu motoru rekoru kendi bulsun.

* * *

# Doğal dil katmanı

Burada büyük dil modellerinin yeri de olacak.

Ama çok net bir mimari sınırla.

LLM'ye:

> “Trabzonspor'un şampiyonluk ihtimali kaç?”

diye sorup kafasından sayı üretmesini istemiyoruz.

LLM'nin görevi:

$$NaturalLanguage \rightarrow StructuredFootballQuery$$

olacak.

Hesabı bizim sistemimiz yapacak.

Örneğin kullanıcı:

> “Salah gelirse Trabzonspor'un Şampiyonlar Ligi'nde son 16 ihtimali ne kadar değişir?”

diye soracak.

Dil katmanı bunu:

1.  entity resolution,
    
2.  scenario creation,
    
3.  competition query,
    
4.  simulation request
    

haline çevirecek.

Rakamı ise Football Digital Twin üretecek.

* * *

# Explainability

Model:

```text
Championship
26.2% → 34.8%
```

dedi.

İlk sorumuz:

> **Neden?**

olmalı.

Mümkün olduğunca contribution decomposition göstermek istiyoruz.

Örneğin:

```text
New Transfer                 +4.2 pp
Recent Team Performance      +1.8 pp
Rival Points Loss            +1.4 pp
Fixture Change               +0.9 pp
Squad Availability           +0.5 pp
Other                        -0.2 pp
```

Modeli kara kutu halinde kullanıcıya bırakmak istemiyoruz.

* * *

# Prediction History

Bugünkü tahmin de yarın unutulmayacak.

Her model snapshot'ı saklanacak.

Örneğin:

```text
Trabzonspor Championship Probability

01 Aug     11.2%
08 Aug     13.8%
15 Aug     17.4%
22 Aug     23.9%
29 Aug     29.7%
```

Her değişimin yanında sebepler bulunabilecek.

Böylece yalnız:

> “Bugün ne düşünüyoruz?”

değil:

> **“Neden fikrimiz değişti?”**

sorusunu da cevaplayabiliriz.

* * *

# Database Architecture

İlk canonical veri kaynağımız için tercihimiz:

## PostgreSQL

Çünkü futbol verisinin önemli bölümü doğal olarak ilişkisel.

```text
Player
↓
Contract
↓
Club
↓
Squad
↓
Match
↓
Competition
```

gibi ilişkiler SQL için son derece uygun.

Fakat her şeyi PostgreSQL'e zorlamayacağız.

* * *

# Analytics: ClickHouse

Milyonlarca event oluşmaya başladığında analitik sorgular için farklı ihtiyaç doğacak.

Örneğin:

> Son 15 yıldaki bütün şut eventlerini tara.

> 28 yaş üstü santrforların deplasman performanslarını karşılaştır.

> Bir ligdeki 20 milyon pas eventini aggregate et.

Bu tarz OLAP workloads için ileride:

## ClickHouse

kullanmayı planlıyoruz.

PostgreSQL:

**canonical truth**

ClickHouse:

**analytical speed**

* * *

# Object Storage

Ham veriler, büyük çıktılar, model artifact'ları ve historical dump'lar için:

```text
S3-compatible storage
```

kullanılabilir.

Geliştirme ortamında:

## MinIO

iyi adaylardan biri.

* * *

# Cache

Canlı kullanımda her sorguyu yeniden hesaplamak saçma olacaktır.

Bunun için:

## Redis

*   cache,
    
*   ephemeral state,
    
*   job information,
    
*   rate limiting
    

gibi görevlerde kullanılabilir.

* * *

# Event Stream

İlk gün Kafka kurup 47 mikroservis oluşturarak kendimize eziyet etmeyeceğiz.

Başlangıçta:

# Modular Monolith

yaklaşımı daha doğru.

Fakat event mimarisini baştan düşünerek ilerleyeceğiz.

Ölçek büyüdüğünde:

*   Redpanda
    
*   Kafka
    

gibi event streaming çözümlerine geçilebilir.

* * *

# Programlama dili

Bu proje için “tek dil kullanacağız” gibi ideolojik bir karar vermiyoruz.

Doğru işi doğru araca vereceğiz.

### Modelleme

## Python

Burada kararımız oldukça net.

Ekosistem:

*   NumPy
    
*   SciPy
    
*   Polars
    
*   PyTorch
    
*   JAX
    
*   PyMC
    
*   scikit-learn
    
*   XGBoost
    
*   LightGBM
    

gibi araçlarla model araştırması için muazzam.

* * *

# Backend

İlk sürümlerde:

## Python + FastAPI

mantıklı başlangıç.

Model ile backend arasında gereksiz dil bariyeri oluşturmaz.

Ürün büyüdüğünde servisleri gerektiği şekilde ayırabiliriz.

* * *

# Frontend

## TypeScript + Next.js

Modern, hızlı, SSR/SEO tarafı güçlü ve ürün geliştirme temposu yüksek.

* * *

# Performans kritik servisler

Her şeyi ilk günden Rust ile yazmanın anlamı yok.

Ama gerçekten ihtiyacımız olduğunda:

## Rust

özellikle:

*   yüksek hacimli collector,
    
*   parsing,
    
*   concurrency,
    
*   simulation'ın bazı kritik parçaları,
    
*   düşük latency servisler
    

için devreye girebilir.

* * *

# İlk stack

Bugünkü mimari düşüncemiz kabaca:

```text
MODEL / ML
Python

DATA PROCESSING
Python
Polars

COLLECTORS
Python
HTTPX
Playwright
Rust when necessary

BACKEND
FastAPI

FRONTEND
TypeScript
Next.js

CANONICAL DATABASE
PostgreSQL

ANALYTICS
ClickHouse

CACHE
Redis

OBJECT STORAGE
S3 / MinIO

EVENT STREAM
Redpanda / Kafka — when needed

ML EXPERIMENTS
MLflow

WORKFLOW ORCHESTRATION
Prefect / Dagster

CONTAINERS
Docker
```

Kubernetes?

Belki bir gün.

Ama daha ilk futbolcu kaydını oluşturmadan Kubernetes cluster kurup “ölçeklenebiliriz” diye kendimizi kandırmayacağız.

Önce çalışan sistem.

Sonra ölçek.

* * *

# Database düşündüğümüzden çok daha büyük olacak

Uzun vadeli şema içerisinde yalnızca birkaç örnek:

```text
players
player_aliases
player_states
clubs
club_states
squads
squad_memberships
contracts
transfers
transfer_rumours
market_values
injuries
suspensions
coaches
coach_tenures
competitions
competition_seasons
competition_rules
fixtures
matches
lineups
appearances
match_events
advanced_events
stadiums
referees
weather_events
travel_events
odds
rankings
sources
source_entities
source_reliability
raw_snapshots
football_events
model_versions
model_snapshots
simulations
simulation_runs
scenarios
scenario_events
records
record_queries
```

Ve bunun son liste olduğunu düşünmüyoruz.

* * *

# Modelimizin rakibi yine kendi modelimiz olacak

En önemli bilimsel prensiplerden biri:

**Yeni model eskisini yenemiyorsa production'a girmeyecek.**

Baseline'larımız olabilir:

```text
Naive Table
Elo
Poisson
Dixon-Coles
Market Consensus
Model v0.1
Model v0.2
Model v0.3
```

Her model historical backtest'e girecek.

* * *

# Ölçmeden “iyi model” demeyeceğiz

Model performansında kullanacağımız metriklerden bazıları:

*   Log Loss
    
*   Brier Score
    
*   Ranked Probability Score
    
*   Calibration Error
    
*   Goal likelihood
    
*   MAE
    
*   probability calibration
    
*   championship calibration
    
*   relegation calibration
    

Örneğin model tarih boyunca %30 dediği olayların gerçekten yaklaşık %30'unda başarılı mı?

Bu soru:

> “Geçen hafta üç maçı bildik.”

cümlesinden çok daha değerlidir.

* * *

# Ablation testleri

Bir başka kritik deney:

### Model A

Sadece skor.

### Model B

Skor + kadro.

### Model C

Skor + kadro + transfer.

### Model D

Skor + kadro + transfer + sakatlık.

### Model E

Hepsi + gelişmiş event data + taktik + fikstür.

Sonra bakacağız:

**Hangi veri gerçekten fayda sağlıyor?**

Çünkü modele 700 değişken koymak otomatik olarak modeli iyi yapmaz.

Bazı değişkenler yalnızca gürültüdür.

Bunu sezgiyle değil, deneyle ayıracağız.

* * *

# Subscription Product

Bütün bu altyapı yalnız laboratuvarda kalmayacak.

Son kullanıcıya web üzerinden abonelik modeliyle açmayı hedefliyoruz.

Ürün katmanları zamanla şu yapılara dönüşebilir:

## Match Center

Maç tahmini, dağılımlar, kadrolar, team state ve açıklamalar.

## League Intelligence

Şampiyonluk, Avrupa, küme düşme ve sıralama olasılıkları.

## Club Intelligence

Bir takımın yaşayan digital twin'i.

## Player Intelligence

Oyuncu state'i ve projeksiyonları.

## Transfer Lab

Transfer gerçekleşirse ne olur?

## Scenario Lab

Alternatif futbol evrenleri.

## Records Lab

Tarihsel futbol sorguları.

## Model Lab

Model metodolojisi, benchmark ve calibration.

## API

Profesyonel kullanım.

* * *

# B2C'den fazlası

Uzun vadede ürün yalnız futbolsevere hitap etmek zorunda değil.

Potansiyel kullanıcılar:

*   taraftarlar,
    
*   spor gazetecileri,
    
*   içerik üreticileri,
    
*   analistler,
    
*   scouting ekipleri,
    
*   kulüpler,
    
*   medya kuruluşları,
    
*   veri şirketleri
    

olabilir.

Dolayısıyla model:

$$B2C + B2B + API$$

olarak büyüyebilir.

Ama oraya daha çok yol var.

* * *

# İlk hedefimiz dünya değil

Dünyanın bütün liglerini ilk günden modellemeye çalışmak yapılabilecek en büyük hatalardan biri olur.

İlk hedefimiz çok daha net:

# Süper Lig'i mümkün olduğunca derin modellemek.

Bir ligi gerçekten iyi yapalım.

Veri pipeline'ı çalışsın.

Temporal database otursun.

Event sourcing çalışsın.

Entity resolution otursun.

Player Engine oluşsun.

Team State Engine oluşsun.

Match Engine kalibre olsun.

Simulation Engine doğrulansın.

Scenario Engine çalışsın.

Records Engine sorgu üretsin.

Sonra aynı mimari:

Premier League,

Bundesliga,

La Liga,

Serie A,

Ligue 1,

Avrupa kupaları

ve diğer organizasyonlara genişlesin.

* * *

# Üç temel motor

Bugün kafamızdaki sistemin özü şu:

$$\boxed{ Player Intelligence \rightarrow Team State \rightarrow Football World Simulation }$$

### 1\. Player Intelligence Engine

Oyuncuyu modelleyecek.

### 2\. Team State Engine

Oyunculardan, teknik ekipten ve çevresel değişkenlerden takımın yaşayan state'ini üretecek.

### 3\. Football World Simulator

Bu state'lerden geleceği simüle edecek.

Bunların üzerinde:

$$Scenario Engine$$

$$Records Engine$$

$$Query Engine$$

$$Explainability Engine$$

$$Natural Language Layer$$

bulunacak.

* * *

# Peki neden bu kadar geniş?

Çünkü problemi küçültürsek alacağımız cevap da küçük olur.

Bir takımın şampiyon olup olmayacağını gerçekten analiz etmek istiyorsak sadece puan tablosuna bakamayız.

Bir futbolcunun değerini anlamak istiyorsak sadece piyasa değerine bakamayız.

Bir transferin etkisini anlamak istiyorsak yalnızca transfer olduktan sonra skorların değişmesini bekleyemeyiz.

Bir modelin iyi olduğunu söylemek istiyorsak üç doğru tahmin ekran görüntüsü paylaşamayız.

Ve geçmişi analiz etmek istiyorsak bugünkü bilgilerimizi geçmişe taşıyamayız.

Futbol karmaşık.

Biz de problemi sırf çözmesi kolay olsun diye olduğundan basit göstermeyeceğiz.

* * *

# Koddan daha değerli bir şey inşa etmek

Bir noktadan sonra bu projenin asıl değeri yalnız algoritmalar olmayacak.

Yıllar içerisinde veri tabanımız şunları bilecek:

> O futbolcu hangi gün hangi takımdaydı?

> O gün piyasa değeri neydi?

> Sakat mıydı?

> Model onu nasıl değerlendiriyordu?

> Takımın state'i neydi?

> Piyasa ne düşünüyordu?

> Fikstür nasıldı?

> Hangi transfer söylentileri vardı?

> Sonra ne gerçekleşti?

> Model nerede yanıldı?

> Sonraki versiyon bunu düzeltti mi?

Bu noktada elimizde yalnız bir uygulama olmayacak.

Elimizde:

# yaşayan, versiyonlanmış, temporal bir futbol bilgi sistemi

olacak.

Bunun birkaç yılda oluşturacağı veri birikimi, sonradan yalnız kod yazarak kolayca kopyalanabilecek bir şey değil.

* * *

# Şimdi başlıyoruz

Önümüzde oldukça uzun bir yol var.

Muhtemelen yanlış modeller kuracağız.

Bazı hipotezlerimiz çöpe gidecek.

Bazı veri kaynakları düşündüğümüz kadar faydalı çıkmayacak.

Bazı mimari kararlarımızı yeniden yazacağız.

Bazı geceler bir oyuncunun aynı kişi olduğunu iki farklı veri kaynağına anlatmaya çalışarak geçecek.

Bazı modeller çok güzel görünüp backtest'te darmadağın olacak.

Ve muhtemelen bundan keyif alacağız.

Çünkü mühendislik biraz da budur.

Gerçeği basitleştirerek haklı çıkmaya çalışmak değil;

**kurduğun sistem yanlışsa onun yanlış olduğunu ölçebilecek kadar iyi bir sistem kurmak.**

TUNIX Football Digital Twin ile futbol için tam olarak bunu yapmak istiyoruz.

Bir tahmin tablosu üretmek değil.

Bir API'nin verdiği sayıları güzel grafiklere koymak değil.

Bir yapay zekâya skor sordurmak hiç değil.

## Kendi verimizi.

## Kendi state'imizi.

## Kendi modellerimizi.

## Kendi simülasyon motorumuzu.

## Kendi futbol dünyamızı.

sıfırdan kuracağız.

Ne kadarını başarabileceğimizi bugün bilmiyoruz.

Zaten heyecan verici olan tarafı da bu.

**Vira Bismillah.**

*TUNIX — İnsan İçin Teknoloji.*