namespace Hydronom.GroundStation.Diagnostics;

using Hydronom.GroundStation.Ack;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.Security;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.Transports.Receive;

/// <summary>
/// Ground Station tarafının tek bakışta okunabilir operasyon özetini temsil eder.
/// 
/// Bu model, Hydronom Ops veya diagnostics ekranı için tek çağrıda genel durum bilgisi sağlar.
/// Amaç, farklı modüllerden gelen bilgileri sade bir snapshot halinde toplamaktır.
/// 
/// Örnek olarak şunları özetler:
/// - Filo durumu,
/// - Komut geçmişi,
/// - Ortak dünya modeli,
/// - Bağlantı/link sağlığı,
/// - Route execution / transport gönderim durumu,
/// - Gerçek command ACK/result korelasyon durumu,
/// - Inbound receive / gelen mesaj trafiği durumu,
/// - Command safety/security değerlendirme durumu,
/// - Mission allocation / görev atama karar durumu,
/// - Genel health değerlendirmesi,
/// - Kısa açıklama.
/// </summary>
public sealed record GroundOperationSnapshot
{
    /// <summary>
    /// Snapshot'ın üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Toplam kayıtlı araç/node sayısı.
    /// </summary>
    public int TotalNodeCount { get; init; }

    /// <summary>
    /// Online durumdaki araç/node sayısı.
    /// </summary>
    public int OnlineNodeCount { get; init; }

    /// <summary>
    /// Offline durumdaki araç/node sayısı.
    /// </summary>
    public int OfflineNodeCount { get; init; }

    /// <summary>
    /// Health değeri OK olan araç/node sayısı.
    /// </summary>
    public int HealthyNodeCount { get; init; }

    /// <summary>
    /// Health değeri Warning olan araç/node sayısı.
    /// </summary>
    public int WarningNodeCount { get; init; }

    /// <summary>
    /// Health değeri Critical veya Fault olan araç/node sayısı.
    /// </summary>
    public int CriticalNodeCount { get; init; }

    /// <summary>
    /// Ortalama batarya yüzdesi.
    /// 
    /// Batarya bilgisi olmayan araçlar ortalamaya dahil edilmez.
    /// Hiç batarya bilgisi yoksa null kalır.
    /// </summary>
    public double? AverageBatteryPercent { get; init; }

    /// <summary>
    /// Kayıtlı toplam komut sayısı.
    /// </summary>
    public int TotalCommandCount { get; init; }

    /// <summary>
    /// Henüz sonuç bekleyen komut sayısı.
    /// </summary>
    public int PendingCommandCount { get; init; }

    /// <summary>
    /// Tamamlanmış komut sayısı.
    /// </summary>
    public int CompletedCommandCount { get; init; }

    /// <summary>
    /// Başarılı komut sayısı.
    /// </summary>
    public int SuccessfulCommandCount { get; init; }

    /// <summary>
    /// Başarısız veya expired komut sayısı.
    /// </summary>
    public int FailedCommandCount { get; init; }

    /// <summary>
    /// GroundWorldModel içindeki toplam dünya nesnesi sayısı.
    /// </summary>
    public int TotalWorldObjectCount { get; init; }

    /// <summary>
    /// GroundWorldModel içindeki aktif dünya nesnesi sayısı.
    /// </summary>
    public int ActiveWorldObjectCount { get; init; }

    /// <summary>
    /// Aktif obstacle sayısı.
    /// </summary>
    public int ActiveObstacleCount { get; init; }

    /// <summary>
    /// Aktif target sayısı.
    /// </summary>
    public int ActiveTargetCount { get; init; }

    /// <summary>
    /// Aktif no-go zone sayısı.
    /// </summary>
    public int ActiveNoGoZoneCount { get; init; }

    /// <summary>
    /// LinkHealthTracker tarafından takip edilen toplam araç bağlantı grubu sayısı.
    /// 
    /// Bu değer araç bazlıdır.
    /// Örneğin Alpha için WiFi + LoRa takip ediliyorsa bu alan yine 1 olur.
    /// </summary>
    public int LinkVehicleCount { get; init; }

    /// <summary>
    /// Takip edilen toplam transport link sayısı.
    /// 
    /// Örneğin Alpha/WiFi, Alpha/LoRa, Beta/RF toplam 3 link sayılır.
    /// </summary>
    public int TotalLinkCount { get; init; }

    /// <summary>
    /// Good durumundaki link sayısı.
    /// </summary>
    public int GoodLinkCount { get; init; }

    /// <summary>
    /// Degraded durumundaki link sayısı.
    /// </summary>
    public int DegradedLinkCount { get; init; }

    /// <summary>
    /// Critical durumundaki link sayısı.
    /// </summary>
    public int CriticalLinkCount { get; init; }

    /// <summary>
    /// Lost durumundaki link sayısı.
    /// </summary>
    public int LostLinkCount { get; init; }

    /// <summary>
    /// Unknown durumundaki link sayısı.
    /// </summary>
    public int UnknownLinkCount { get; init; }

    /// <summary>
    /// Araçlar arasındaki en iyi linklerin ortalama kalite skoru.
    /// 
    /// Araç başına OverallQualityScore değerlerinin ortalamasıdır.
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? AverageVehicleLinkQualityScore { get; init; }

    /// <summary>
    /// Tüm transport linklerinin ortalama kalite skoru.
    /// 
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? AverageTransportLinkQualityScore { get; init; }

    /// <summary>
    /// En düşük araç bağlantı kalite skoru.
    /// 
    /// Filodaki zayıf halkayı hızlı görmek için kullanılır.
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? WorstVehicleLinkQualityScore { get; init; }

    /// <summary>
    /// En düşük transport bağlantı kalite skoru.
    /// 
    /// Tekil transport seviyesindeki en kötü linki gösterir.
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? WorstTransportLinkQualityScore { get; init; }

    /// <summary>
    /// Link health durumunun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops Communication Links panelinde veya diagnostics özetinde gösterilebilir.
    /// </summary>
    public string LinkHealthSummary { get; init; } = "No link health data.";

    /// <summary>
    /// Araç bazlı link health snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafında araç kartları, link health paneli,
    /// communication diagnostics ekranı ve ileride route karar izleme için kullanılabilir.
    /// </summary>
    public IReadOnlyList<VehicleLinkHealthSnapshot> LinkHealth { get; init; } =
        Array.Empty<VehicleLinkHealthSnapshot>();

    /// <summary>
    /// Toplam route execution kaydı sayısı.
    /// 
    /// Bu değer GroundTransportExecutionTracker içinde başlatılmış route/gönderim takip kayıtlarını gösterir.
    /// </summary>
    public int TotalRouteExecutionCount { get; init; }

    /// <summary>
    /// Henüz tamamlanmamış route execution sayısı.
    /// </summary>
    public int PendingRouteExecutionCount { get; init; }

    /// <summary>
    /// Tamamlanmış route execution sayısı.
    /// </summary>
    public int CompletedRouteExecutionCount { get; init; }

    /// <summary>
    /// En az bir başarılı gönderim sonucu olan route execution sayısı.
    /// </summary>
    public int SuccessfulRouteExecutionCount { get; init; }

    /// <summary>
    /// ACK alınmış route execution sayısı.
    /// </summary>
    public int AckedRouteExecutionCount { get; init; }

    /// <summary>
    /// Timeout yaşamış route execution sayısı.
    /// </summary>
    public int TimeoutRouteExecutionCount { get; init; }

    /// <summary>
    /// Başarısız route execution sayısı.
    /// </summary>
    public int FailedRouteExecutionCount { get; init; }

    /// <summary>
    /// Route edilemeyen execution sayısı.
    /// 
    /// CanRoute false olan route kayıtlarını sayar.
    /// </summary>
    public int RouteUnavailableExecutionCount { get; init; }

    /// <summary>
    /// Transport gönderim sonuçlarının toplam sayısı.
    /// 
    /// Bir route execution içinde birden fazla transport denemesi olabilir.
    /// </summary>
    public int TotalTransportSendResultCount { get; init; }

    /// <summary>
    /// Başarılı transport send sonucu sayısı.
    /// Sent veya Acked durumları başarılı kabul edilir.
    /// </summary>
    public int SuccessfulTransportSendResultCount { get; init; }

    /// <summary>
    /// ACK alınmış transport send sonucu sayısı.
    /// </summary>
    public int AckedTransportSendResultCount { get; init; }

    /// <summary>
    /// Timeout transport send sonucu sayısı.
    /// </summary>
    public int TimeoutTransportSendResultCount { get; init; }

    /// <summary>
    /// Başarısız transport send sonucu sayısı.
    /// </summary>
    public int FailedTransportSendResultCount { get; init; }

    /// <summary>
    /// Route execution kayıtlarında ölçülen ortalama en iyi latency.
    /// 
    /// Her execution için BestLatencyMs değerlerinin ortalamasıdır.
    /// Veri yoksa null kalır.
    /// </summary>
    public double? AverageRouteExecutionLatencyMs { get; init; }

    /// <summary>
    /// Route execution kayıtlarında görülen en iyi latency değeri.
    /// Veri yoksa null kalır.
    /// </summary>
    public double? BestRouteExecutionLatencyMs { get; init; }

    /// <summary>
    /// Route execution kayıtlarında görülen en kötü latency değeri.
    /// Veri yoksa null kalır.
    /// </summary>
    public double? WorstRouteExecutionLatencyMs { get; init; }

    /// <summary>
    /// Route execution / transport send durumunun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops diagnostics panelinde, command route ekranında veya iletişim geçmişinde gösterilebilir.
    /// </summary>
    public string RouteExecutionSummary { get; init; } = "No route execution data.";

    /// <summary>
    /// Route execution snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafında:
    /// - route history,
    /// - command delivery trace,
    /// - transport send result history,
    /// - ACK / timeout izleme
    /// ekranlarını besleyebilir.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> RouteExecutions { get; init; } =
        Array.Empty<RouteExecutionSnapshot>();

    /// <summary>
    /// Toplam command ACK correlation kaydı sayısı.
    /// 
    /// Bu değer CommandAckCorrelator içinde takip edilen CommandId → RouteExecution bağlantılarını gösterir.
    /// </summary>
    public int TotalAckCorrelationCount { get; init; }

    /// <summary>
    /// Henüz gerçek FleetCommandResult almamış correlation sayısı.
    /// </summary>
    public int PendingAckCorrelationCount { get; init; }

    /// <summary>
    /// Gerçek FleetCommandResult ile ACK/result almış correlation sayısı.
    /// </summary>
    public int AckedCorrelationCount { get; init; }

    /// <summary>
    /// Tamamlanmış correlation sayısı.
    /// 
    /// Applied, Completed, Failed, Rejected, Timeout gibi nihai durumlar burada sayılır.
    /// </summary>
    public int CompletedAckCorrelationCount { get; init; }

    /// <summary>
    /// Başarılı correlation sayısı.
    /// 
    /// Accepted, Applied veya Completed durumları başarılı kabul edilir.
    /// </summary>
    public int SuccessfulAckCorrelationCount { get; init; }

    /// <summary>
    /// Başarısız correlation sayısı.
    /// 
    /// Rejected, Failed, Expired veya Timeout durumları başarısız kabul edilir.
    /// </summary>
    public int FailedAckCorrelationCount { get; init; }

    /// <summary>
    /// Belirli bir süre içinde gerçek ACK/result almamış pending correlation sayısı.
    /// 
    /// Diagnostics motoru ilk fazda varsayılan timeout eşiği ile hesaplar.
    /// </summary>
    public int ExpiredPendingAckCorrelationCount { get; init; }

    /// <summary>
    /// ACK/result alınan correlation kayıtları için ortalama ACK gecikmesi.
    /// 
    /// Veri yoksa null kalır.
    /// </summary>
    public double? AverageAckCorrelationLatencyMs { get; init; }

    /// <summary>
    /// ACK/result alınan correlation kayıtları içinde en iyi gecikme.
    /// 
    /// Veri yoksa null kalır.
    /// </summary>
    public double? BestAckCorrelationLatencyMs { get; init; }

    /// <summary>
    /// ACK/result alınan correlation kayıtları içinde en kötü gecikme.
    /// 
    /// Veri yoksa null kalır.
    /// </summary>
    public double? WorstAckCorrelationLatencyMs { get; init; }

    /// <summary>
    /// Command ACK correlation durumunun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops command delivery / ACK diagnostics panelinde gösterilebilir.
    /// </summary>
    public string AckCorrelationSummary { get; init; } = "No ACK correlation data.";

    /// <summary>
    /// Command ACK correlation snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafında:
    /// - gerçek ACK izleme,
    /// - command delivery trace,
    /// - CommandId → ExecutionId eşleşmesi,
    /// - ACK latency analizi
    /// ekranlarını besleyebilir.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> AckCorrelations { get; init; } =
        Array.Empty<CommandAckCorrelationSnapshot>();

    /// <summary>
    /// Toplam receive event sayısı.
    /// 
    /// Bu değer GroundTransportReceiver üzerinden alınan tüm inbound mesaj olaylarını gösterir.
    /// </summary>
    public int TotalReceiveEventCount { get; init; }

    /// <summary>
    /// Başarıyla işlenmiş receive event sayısı.
    /// 
    /// HandleEnvelope tarafından kabul edilip ilgili Ground Station modüllerine aktarılmış mesajları sayar.
    /// </summary>
    public int HandledReceiveEventCount { get; init; }

    /// <summary>
    /// İşlenirken hata oluşmuş receive event sayısı.
    /// 
    /// Deserialize, dispatch, payload restore veya HandleEnvelope aşamasında hata alan inbound mesajlar burada sayılır.
    /// </summary>
    public int FailedReceiveEventCount { get; init; }

    /// <summary>
    /// Alınmış fakat anlamlı şekilde işlenememiş receive event sayısı.
    /// 
    /// Mesaj geldiği halde bilinmeyen tip, eksik payload veya handler eksikliği nedeniyle kullanılamayan olaylar için kullanılır.
    /// </summary>
    public int UnhandledReceiveEventCount { get; init; }

    /// <summary>
    /// Son inbound mesajın alındığı UTC zaman.
    /// 
    /// Hiç receive event yoksa null kalır.
    /// </summary>
    public DateTimeOffset? LastReceiveUtc { get; init; }

    /// <summary>
    /// Inbound receive durumunun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops inbound communication panelinde veya diagnostics özetinde gösterilebilir.
    /// </summary>
    public string ReceiveHealthSummary { get; init; } = "No receive data.";

    /// <summary>
    /// Gelen FleetHeartbeat mesajı sayısı.
    /// </summary>
    public int InboundFleetHeartbeatCount { get; init; }

    /// <summary>
    /// Gelen FleetCommandResult mesajı sayısı.
    /// </summary>
    public int InboundFleetCommandResultCount { get; init; }

    /// <summary>
    /// Gelen FleetCommand mesajı sayısı.
    /// 
    /// Ground Station tarafında genelde araçtan komut gelmesi beklenmez;
    /// ama peer/relay veya test senaryolarında anlamlı olabilir.
    /// </summary>
    public int InboundFleetCommandCount { get; init; }

    /// <summary>
    /// Gelen VehicleNodeStatus mesajı sayısı.
    /// </summary>
    public int InboundVehicleNodeStatusCount { get; init; }

    /// <summary>
    /// Bilinmeyen veya sınıflandırılamayan inbound mesaj sayısı.
    /// </summary>
    public int InboundUnknownMessageCount { get; init; }

    /// <summary>
    /// Receive event snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafında:
    /// - inbound message history,
    /// - heartbeat geçmişi,
    /// - command result geçmişi,
    /// - receive hata geçmişi,
    /// - transport bazlı gelen trafik ekranlarını besleyebilir.
    /// </summary>
    public IReadOnlyList<GroundTransportReceiveEvent> ReceiveEvents { get; init; } =
        Array.Empty<GroundTransportReceiveEvent>();

    /// <summary>
    /// Son command safety/security değerlendirmesinde komut izinli miydi?
    /// 
    /// Hiç komut safety değerlendirmesi yapılmadıysa null kalır.
    /// </summary>
    public bool? LastCommandSafetyAllowed { get; init; }

    /// <summary>
    /// Son command safety/security değerlendirmesinde komut reddedildi mi?
    /// 
    /// Hiç komut safety değerlendirmesi yapılmadıysa null kalır.
    /// </summary>
    public bool? LastCommandSafetyRejected { get; init; }

    /// <summary>
    /// Son command safety/security değerlendirmesinin kısa açıklaması.
    /// 
    /// Hydronom Ops Command Safety panelinde gösterilebilir.
    /// </summary>
    public string LastCommandSafetyReason { get; init; } = "No command safety evaluation.";

    /// <summary>
    /// Son command safety/security değerlendirmesinde tespit edilen toplam issue sayısı.
    /// </summary>
    public int LastCommandSafetyIssueCount { get; init; }

    /// <summary>
    /// Son command safety/security değerlendirmesinde tespit edilen blocking issue sayısı.
    /// </summary>
    public int LastCommandSafetyBlockingIssueCount { get; init; }

    /// <summary>
    /// Son command safety/security değerlendirmesinde tespit edilen warning issue sayısı.
    /// </summary>
    public int LastCommandSafetyWarningIssueCount { get; init; }

    /// <summary>
    /// Son command safety/security değerlendirmesinde üretilen issue kodları.
    /// 
    /// Örnek:
    /// - TARGET_UNKNOWN
    /// - TARGET_OFFLINE
    /// - DUPLICATE_COMMAND_ID
    /// - EMERGENCY_PRIORITY_REQUIRED
    /// </summary>
    public IReadOnlyList<string> LastCommandSafetyIssueCodes { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Son command safety/security değerlendirmesinde üretilen issue detayları.
    /// 
    /// Bu alan Hydronom Ops tarafında command safety detay ekranını besleyebilir.
    /// </summary>
    public IReadOnlyList<CommandValidationIssue> LastCommandSafetyIssues { get; init; } =
        Array.Empty<CommandValidationIssue>();

    /// <summary>
    /// Son görev atama kararının MissionId değeri.
    /// 
    /// Hiç görev ataması yapılmadıysa boş kalır.
    /// </summary>
    public string LastMissionAllocationMissionId { get; init; } = string.Empty;

    /// <summary>
    /// Son görev atama başarılı mı?
    /// 
    /// Hiç görev ataması yapılmadıysa null kalır.
    /// </summary>
    public bool? LastMissionAllocationSuccess { get; init; }

    /// <summary>
    /// Son görev atamada seçilen node id.
    /// 
    /// Atama başarısızsa veya hiç atama yapılmadıysa boş kalır.
    /// </summary>
    public string LastMissionAllocationSelectedNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Son görev atamada seçilen node display name.
    /// 
    /// Atama başarısızsa veya hiç atama yapılmadıysa boş kalır.
    /// </summary>
    public string LastMissionAllocationSelectedDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Son görev atama açıklaması.
    /// 
    /// Hydronom Ops Mission Allocation panelinde gösterilebilir.
    /// </summary>
    public string LastMissionAllocationReason { get; init; } = "No mission allocation.";

    /// <summary>
    /// Son görev atamada seçilen adayın final skoru.
    /// 
    /// Atama yapılmadıysa null kalır.
    /// </summary>
    public double? LastMissionAllocationScore { get; init; }

    /// <summary>
    /// Son görev atamadaki aday node id listesi.
    /// </summary>
    public IReadOnlyList<string> LastMissionAllocationCandidateNodeIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Son görev atamada reddedilen node id → sebep eşleşmeleri.
    /// </summary>
    public IReadOnlyDictionary<string, string> LastMissionAllocationRejectedNodeReasons { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Ground Station genel health değerlendirmesi.
    /// 
    /// Örnek:
    /// - OK
    /// - Warning
    /// - Critical
    /// </summary>
    public string OverallHealth { get; init; } = "Unknown";

    /// <summary>
    /// Genel durumun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops üst panelinde veya log ekranında gösterilebilir.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Snapshot üzerinde kritik durum olup olmadığını hızlıca döndürür.
    /// </summary>
    public bool HasCriticalIssues =>
        string.Equals(OverallHealth, "Critical", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Snapshot üzerinde uyarı durumu olup olmadığını hızlıca döndürür.
    /// </summary>
    public bool HasWarnings =>
        string.Equals(OverallHealth, "Warning", StringComparison.OrdinalIgnoreCase);
}