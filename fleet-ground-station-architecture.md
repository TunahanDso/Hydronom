**Hydronom Fleet & Ground Station Architecture**

**(Hydronom Filo ve Yer İstasyonu Mimarisi)**

**1. Ana felsefe**

Bu sistemin temel ilkesi şu olmalı:

**Her araç bağımsız çalışabilir, yer istasyonu tüm araçları görebilir,
gerektiğinde koordinasyon kurabilir; haberleşme yöntemi değişse bile üst
seviye Hydronom mimarisi değişmez.**

Yani Wi-Fi kullanınca başka, LoRa kullanınca başka, RF modem kullanınca
başka kod yazmayacağız.

Doğru felsefe:

Hydronom mesaj üretir.\
Transport katmanı mesajı uygun kanaldan taşır.\
Karşı taraf mesajı alır.\
Üst seviye sistem mesajın Wi-Fi ile mi LoRa ile mi geldiğini bilmek
zorunda kalmaz.

**2. Yeni büyük mimari görünüm**

![](./resimler/media/image1.png){width="6.3in"
height="5.86264435695538in"}

Burada üç ilişki var:

Araç ↔ Yer istasyonu\
Araç ↔ Araç\
Yer istasyonu ↔ Tüm filo

**3. Yer istasyonu ne yapacak?**

Hydronom Ops artık sadece "görüntüleme paneli" olmamalı. Yeni haliyle
yer istasyonu, Hydronom'un **operasyon beyni** gibi davranmalı.

Ama dikkat: Bu, araçların beynini tamamen devralmak anlamına gelmemeli.

Doğru ayrım şu:

  -----------------------------------------------------------------------
  **Katman**              **Sorumluluk**
  ----------------------- -----------------------------------------------
  **Araç üzerindeki       Gerçek zamanlı kontrol, güvenlik, motor komutu,
  Hydronom Runtime**      yerel karar

  **Yer İstasyonu /       Filo görünürlüğü, görev planlama, koordinasyon,
  Ground Station**        uzun telemetry analizi, operatör kontrolü

  **Fleet Layer**         Araçlar arası protokol, görev paylaşımı, rol,
                          durum, liderlik

  **Communication         Haberleşme teknolojilerinin soyutlanması
  Fabric**                
  -----------------------------------------------------------------------

En kritik kural:

**Yer istasyonu güçlü analiz ve koordinasyon yapabilir, ama araç
üzerindeki Safety katmanını asla ezemez.**

Yani operatör veya yer istasyonu "ilerle" dese bile araç kendi
sensörleriyle risk görüyorsa durmalı.

**4. Ground Station'ın yeni modülleri**

Yer istasyonu tarafına şu ana modüller gelmeli:

Hydronom.GroundStation\
├── FleetRegistry\
├── VehicleMonitor\
├── FleetCoordinator\
├── MissionPlanner\
├── MissionAllocator\
├── TelemetryFusionEngine\
├── GroundAnalysisEngine\
├── CommunicationRouter\
├── OperatorCommandCenter\
├── FleetSafetySupervisor\
├── ReplayRecorder\
├── MapWorldModel\
└── GroundAIOrchestrator

**4.1. FleetRegistry**

Tüm araçların kayıt defteri.

Şunu tutar:

Araç ID\
Araç adı\
Araç tipi\
Konum\
Bağlantı durumu\
Batarya\
Sağlık durumu\
Kabiliyetler\
Aktif görev\
Aktif rol\
Son görüldüğü zaman\
Kullanılabilir haberleşme kanalları

Örnek:

{\
\"vehicleId\": \"HYDRO-ALPHA-001\",\
\"name\": \"Alpha\",\
\"type\": \"SurfaceVessel\",\
\"role\": \"Leader\",\
\"health\": \"OK\",\
\"battery\": 82,\
\"links\": \[\"wifi\", \"rf\", \"lora\"\],\
\"capabilities\": \[\"navigation\", \"lidar\", \"camera\",
\"mapping\"\]\
}

**4.2. VehicleMonitor**

Her aracın canlı durumunu izler.

Alpha nerede?\
Hızı kaç?\
Batarya kaç?\
Son mesaj ne zaman geldi?\
Sensörleri sağlıklı mı?\
Motorlar cevap veriyor mu?\
Bağlantı kalitesi nasıl?

Bu Hydronom Ops'ta kartlar hâlinde görünür.

**4.3. TelemetryFusionEngine**

Araçların gönderdiği uzun telemetry mesajları yer istasyonunda
birleştirilebilir.

Örneğin:

Araç A bir engel gördü.\
Araç B aynı bölgede başka veri gördü.\
Araç C kamera ile hedef tespit etti.\
Yer istasyonu bunları ortak dünya modelinde birleştirdi.

Bu motorun görevi:

Multi-vehicle telemetry fusion\
Shared obstacle map\
Shared target map\
Shared mission progress\
Shared health map\
Shared environmental state

Yani araçların gördükleri dünyalar yer istasyonunda birleşir.

Bu şuna dönüşür:

**Ground Station Shared World Model**

**(Yer İstasyonu Ortak Dünya Modeli)**

**4.4. GroundAnalysisEngine**

Araçların tek tek yapamadığı daha uzun ve ağır analizleri yapar.

Mesela:

Bütün filo neden yavaşlıyor?\
Hangi araç daha çok enerji tüketiyor?\
Hangi bölgede engel yoğunluğu arttı?\
Görev dağılımı verimli mi?\
Bir araç sensörlerinde sapma mı yaşıyor?\
Hangi araç lider olmaya daha uygun?

Araç üstü analiz gerçek zamanlı ve kısa döngülü olur.

Yer istasyonu analizi ise daha geniş bakar:

Taktik analiz\
Filo performans analizi\
Görev verimliliği\
Enerji analizi\
Risk analizi\
Harita analizi

**4.5. FleetCoordinator**

Filo koordinasyonunun merkezi.

Şunu yapar:

Rolleri belirler.\
Araçları görevlere atar.\
Formasyon önerir.\
Çakışmaları çözer.\
Kaybolan araç görevlerini yeniden dağıtır.\
Görev önceliklerini günceller.

Ama yine kural:

FleetCoordinator komut verir, araç kendi Safety katmanından geçirir.

**4.6. CommunicationRouter**

Bu da yeni mimarinin kalbi.

Araçların farklı haberleşme teknolojileri kullanabilmesi için mesajları
doğru kanala yönlendirir.

Örnek:

Alpha Wi-Fi ile bağlı.\
Beta LoRa ile bağlı.\
Theta RF modem ile bağlı.\
Sigma hem Wi-Fi hem LoRa ile bağlı.

Ground Station şunu yapabilmeli:

Alpha\'ya yüksek bant genişlikli telemetry isteğini Wi-Fi'dan gönder.\
Beta'ya sadece düşük boyutlu görev komutu gönder, çünkü LoRa
kullanıyor.\
Sigma'yı relay olarak kullan.\
Theta'nın RF link kalitesi düştüyse alternatif link dene.

**5. Communication Fabric mantığı**

Şu an Hydronom'da TCP/NDJSON tarafı zaten var. Bu yeni güncellemede bunu
genellemeliyiz.

Ana fikir:

ITransport

Yani her haberleşme teknolojisi aynı arayüzden konuşmalı.

Örnek yapı:

public interface ITransport\
{\
string Name { get; }\
TransportKind Kind { get; }\
bool IsConnected { get; }\
\
Task ConnectAsync(CancellationToken ct);\
Task SendAsync(HydronomEnvelope envelope, CancellationToken ct);\
IAsyncEnumerable\<HydronomEnvelope\> ReceiveAsync(CancellationToken
ct);\
}

Sonra her teknoloji bunun implementasyonu olur:

TcpTransport\
UdpTransport\
WebSocketTransport\
SerialTransport\
LoRaTransport\
RfModemTransport\
MqttTransport\
CellularTransport\
MeshTransport\
FileReplayTransport

Bu sayede üst sistem şöyle düşünür:

Ben mesaj göndereceğim.\
Nasıl gittiği transport işidir.

Bu plug-and-play'in özü.

**6. Transport seçimi nasıl yapılmalı?**

Her mesaj tipi aynı kanaldan gitmemeli.

Mesela:

  -------------------------------------
  **Mesaj tipi**  **En uygun kanal**
  --------------- ---------------------
  Video stream    Wi-Fi / Ethernet / 4G

  Uzun telemetry  Wi-Fi / Ethernet / 4G

  Kısa durum      LoRa / RF / Wi-Fi
  mesajı          

  Acil durdurma   Mümkün olan tüm
                  kanallar

  Görev komutu    RF / Wi-Fi / LoRa

  Heartbeat       Düşük bant genişlikli
                  kanal

  Harita verisi   Wi-Fi / Ethernet

  Mini konum      LoRa / RF
  paketi          
  -------------------------------------

Bu yüzden CommunicationRouter'da bir **routing policy** olmalı.

Örnek:

{\
\"messageType\": \"EmergencyStop\",\
\"delivery\": \"BroadcastAllAvailableLinks\",\
\"priority\": \"Critical\",\
\"requiresAck\": true\
}

Başka örnek:

{\
\"messageType\": \"LongTelemetry\",\
\"delivery\": \"BestBandwidthLink\",\
\"priority\": \"Normal\",\
\"requiresAck\": false\
}

**7. Mesaj zarfı standardı**

Tüm Hydronom mesajları ortak bir zarfla taşınmalı.

{\
\"schema\": \"hydronom.envelope.v1\",\
\"messageId\": \"MSG-2026-000001\",\
\"sourceNodeId\": \"VEHICLE-ALPHA\",\
\"targetNodeId\": \"GROUND-001\",\
\"messageType\": \"FleetStatus\",\
\"priority\": \"Normal\",\
\"timestampUtc\": \"2026-05-01T12:00:00Z\",\
\"transportHints\": {\
\"preferred\": \[\"wifi\", \"rf\"\],\
\"fallback\": \[\"lora\"\],\
\"requiresAck\": true\
},\
\"payload\": {}\
}

Böylece ister TCP ile gelsin ister LoRa ile gelsin, üst sistem aynı
mesajı okur.

**8. Araç tarafındaki yeni yapı**

Her araçta şunlar olmalı:

Vehicle Node\
├── Local Runtime\
├── Local Safety\
├── Local Decision\
├── Fleet Agent\
├── Transport Manager\
├── Capability Announcer\
├── Ground Link Client\
└── Peer Link Client

**Fleet Agent ne yapar?**

Aracı filoya tanıtır.\
Diğer araçları tanır.\
Yer istasyonuna status yollar.\
Yer istasyonundan görev alır.\
Araçlar arası mesajları işler.\
FleetContext üretir.

**Transport Manager ne yapar?**

Hangi haberleşme modülü takılı?\
Wi-Fi var mı?\
RF modem var mı?\
LoRa var mı?\
Serial link var mı?\
Hangisi bağlı?\
Hangisinin sinyali iyi?\
Hangi mesaj hangi linkten gitmeli?

Bu tam senin plug-and-play felsefen.

**9. Yer istasyonunun araçları kontrol etmesi**

Yer istasyonu araçlara üç seviyede müdahale edebilmeli:

**Seviye 1 --- İzleme**

Sadece telemetry alır.\
Araçlara komut vermez.

**Seviye 2 --- Görev atama**

Araçlara görev verir.\
Araçlar görevi kendi local decision/safety katmanından geçirir.

**Seviye 3 --- Operatör kontrolü**

Manuel veya yarı manuel kontrol.\
Motor/heading/speed/target komutları gönderilir.

Ama burada da güvenlik kuralı aynı:

OperatorCommand → Vehicle Safety Gate → Actuation

Yani komut direkt motora gitmemeli.

**10. Yeni komut akışı**

Örnek:

Operator Hydronom Ops'ta Alpha'ya hedef seçer.\
Ground Station MissionPlanner komutu üretir.\
CommunicationRouter uygun linki seçer.\
Vehicle Fleet Agent komutu alır.\
Vehicle Decision bunu yerel göreve çevirir.\
Vehicle Safety kontrol eder.\
ActuatorManager motorlara uygular.\
Vehicle status geri yollar.\
Ground Station sonucu izler.

**11. Plug-and-play haberleşme felsefesi**

Yeni sistemde her haberleşme cihazı kendini şöyle tanıtmalı:

{\
\"type\": \"COMM_CAPABILITY\",\
\"deviceId\": \"LORA-001\",\
\"transportKind\": \"LoRa\",\
\"maxPayloadBytes\": 240,\
\"estimatedRangeMeters\": 2000,\
\"bandwidthClass\": \"Low\",\
\"latencyClass\": \"High\",\
\"supportsAck\": true,\
\"supportsBroadcast\": true,\
\"status\": \"Ready\"\
}

Wi-Fi için:

{\
\"type\": \"COMM_CAPABILITY\",\
\"deviceId\": \"WIFI-001\",\
\"transportKind\": \"WiFi\",\
\"maxPayloadBytes\": 65535,\
\"bandwidthClass\": \"High\",\
\"latencyClass\": \"Low\",\
\"supportsVideo\": true,\
\"status\": \"Ready\"\
}

Böylece sistem haberleşme cihazlarını sensör gibi keşfeder.

**Hydronom'da haberleşme modülleri de sensör/aktüatör gibi tak-çalıştır
bileşen olur.**

**12. Araçlar birbiriyle doğrudan mı konuşmalı, yer istasyonu üzerinden
mi?**

Bence ikisi de olmalı.!!!

**Direct Peer Mode**

Araçlar birbirine doğrudan mesaj atar.

Vehicle A ↔ Vehicle B

Avantaj:

Düşük gecikme\
Yer istasyonu gitse bile araçlar konuşur\
Formasyon için iyi

**Ground-Mediated Mode**

Araçlar yer istasyonu üzerinden konuşur.

Vehicle A ↔ Ground Station ↔ Vehicle B

Avantaj:

Merkezi kayıt\
Daha iyi analiz\
Operatör kontrolü\
Görev koordinasyonu

**Hybrid Mode**

En iyi yaklaşım:

Kritik yakın çevre bilgisi → araçlar arası doğrudan\
Stratejik koordinasyon → yer istasyonu üzerinden

Örnek:

Çarpışma uyarısı → Vehicle A direkt Vehicle B'ye atar.\
Görev yeniden dağıtımı → Ground Station yapar.

**13. Uzun telemetry meselesi**

Üç telemetry profili olmalı:

**Light Telemetry**

LoRa/RF için:

vehicleId\
position\
heading\
speed\
battery\
health\
mission state

**Normal Telemetry**

Wi-Fi/RF iyi bağlantı için:

Light telemetry +\
sensor summary\
obstacle summary\
target summary\
local analysis\
actuator summary

**Full Telemetry**

Wi-Fi/Ethernet/4G için:

Normal telemetry +\
raw-ish fused data\
map tiles\
obstacle clouds\
diagnostic logs\
long analysis traces\
AI reasoning summaries

Bu profiller transport'a göre otomatik seçilmeli.

Örnek:

Wi-Fi varsa Full Telemetry.\
RF varsa Normal Telemetry.\
LoRa varsa Light Telemetry.\
Bağlantı kötüleşirse otomatik Light'a düş.

Buna:

**Adaptive Telemetry Profile** diyebiliriz.

**14. Yer istasyonu füzyonu nasıl olmalı?**

Ground Station'da ayrı bir veri modeli olmalı:

GroundWorldModel\
├── Vehicles\
├── Obstacles\
├── Targets\
├── NoGoZones\
├── MissionAreas\
├── MapLayers\
├── LinkQualityMap\
├── FleetHealth\
└── EventTimeline

Araçlardan gelen bilgiler burada birleşir.

Örnek:

Alpha engel gördü.\
Beta aynı engeli başka açıdan gördü.\
Ground Station bu engeli tek ortak obstacle olarak kaydetti.\
Theta'nın yolu buna göre güncellendi.

Bu artık gerçek koordinasyon.

**15. Güvenlik ve yetki modeli**

Bu büyümede en kritik şeylerden biri yetki.

Her komutun seviyesi olmalı:

Info\
Suggestion\
MissionCommand\
ControlCommand\
CriticalCommand\
EmergencyCommand

Araç tarafında her gelen komut şu kapıdan geçmeli:

CommandValidator\
↓\
AuthorityManager\
↓\
SafetyGate\
↓\
Decision/Actuation

Yani:

Her mesaj geçerli mi?\
Kim gönderdi?\
Yetkisi var mı?\
Mesaj eski mi?\
Replay attack olabilir mi?\
Bu komut güvenli mi?

İlk aşamada basit token/vehicle key yeterli olabilir. İleri aşamada
imzalı mesaj sistemi eklenir.

**16. Yeni proje/solution yapısı önerisi**

İleride şöyle genişleyebilir:

Hydronom.Core\
├── Domain\
├── Fleet\
├── Communication\
├── GroundStation\
└── AI.Contracts\
\
Hydronom.Runtime\
├── Fleet\
├── Communication\
├── Safety\
└── VehicleRuntime\
\
Hydronom.GroundStation\
├── FleetRegistry\
├── Coordination\
├── TelemetryFusion\
├── MissionPlanning\
├── Analysis\
└── Routing\
\
Hydronom.Ops.Gateway\
├── WebSocket\
├── Snapshot\
├── FleetApi\
└── ControlApi\
\
Hydronom.Ops.Frontend\
├── Fleet Dashboard\
├── Mission Control\
├── Vehicle Detail\
├── Communication Links\
└── Ground Analysis

Burada önemli karar:

Ground Station mantığı sadece frontend değildir.\
Ayrı bir backend/engine katmanı olmalıdır.

Hydronom Ops sadece ekran değil; arkasında ciddi bir koordinasyon motoru
olmalı.

**17. Minimum gerçekçi başlangıç**

.İlk gerçekçi sürüm şöyle olmalı:

**Fleet & Ground v1**

1\. HydronomEnvelope standardı\
2. ITransport arayüzü\
3. TcpTransport / WebSocketTransport başlangıcı\
4. VehicleNode identity/capability/status modeli\
5. GroundStation FleetRegistry\
6. Araçların yer istasyonuna kayıt olması\
7. Heartbeat ve status akışı\
8. Hydronom Ops'ta çoklu araç listesi\
9. Basit command gönderme\
10. SafetyGate üzerinden komut kabul/reddetme

Bu sürümün amacı:

Yer istasyonu birden fazla Hydronom aracını görsün.\
Her aracın durumunu bilsin.\
Basit görev/komut gönderebilsin.\
Haberleşme katmanı plug-and-play'e hazırlansın.

**18. V2'de ne gelir?**

1\. Adaptive telemetry profiles\
2. Multiple transport support\
3. Link quality tracking\
4. Ground telemetry fusion\
5. Shared obstacle map\
6. Mission allocation\
7. Role assignment\
8. Direct vehicle-to-vehicle handshake

**19. V3'te ne gelir?**

1\. Formation control\
2. Dynamic leader election\
3. Multi-vehicle path planning\
4. Relay vehicle mode\
5. Swarm search patterns\
6. Ground AI-assisted coordination\
7. Fleet replay and after-action analysis

**20. Mimari karar**

4 temel taşı üzerine kurulmalı:

**1. HydronomEnvelope**

Tüm mesajların ortak zarfı.

**2. Communication Fabric**

Tüm haberleşme teknolojilerini plug-and-play soyutlayan katman.

**3. Fleet Layer**

Araçların birbirini tanıması, rol alması, görev paylaşması.

**4. Ground Station Engine**

Yer istasyonunun tüm araçları görmesi, analiz etmesi, koordine etmesi ve
gerektiğinde kontrol etmesi.

Bu dört parça birleşince Hydronom artık şuna dönüşür:

**Tekil otonom araç sistemi değil; çoklu araç operasyon mimarisi.**

**21. Final cümlesi**

Hydronom'un yeni Fleet & Ground Station mimarisi, her aracı bağımsız bir
otonom düğüm olarak korurken; yer istasyonu ve araçlar arası haberleşme
katmanları sayesinde bu düğümleri ortak görev yapabilen, veri
paylaşabilen, birbirini tamamlayabilen ve farklı haberleşme
teknolojilerine uyum sağlayabilen bir filo zekâsına dönüştürür.

**Hydronom artık yalnızca aracı yönetmez; araçların birlikte
çalışabildiği otonom operasyon ekosistemini yönetir.**

***Stars of Hydro Otonom Ekip Lideri***

*Tunahan Delisalihoğlu*
