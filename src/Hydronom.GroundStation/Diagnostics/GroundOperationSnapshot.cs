namespace Hydronom.GroundStation.Diagnostics;

using Hydronom.GroundStation.Ack;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.Security;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.Transports.Receive;

/// <summary>
/// Ground Station tarafÄ±nÄ±n tek bakÄ±ÅŸta okunabilir operasyon Ã¶zetini temsil eder.
/// 
/// Bu model, Hydronom Ops veya diagnostics ekranÄ± iÃ§in tek Ã§aÄŸrÄ±da genel durum bilgisi saÄŸlar.
/// AmaÃ§, farklÄ± modÃ¼llerden gelen bilgileri sade bir snapshot halinde toplamaktÄ±r.
/// 
/// Ã–rnek olarak ÅŸunlarÄ± Ã¶zetler:
/// - Filo durumu,
/// - Komut geÃ§miÅŸi,
/// - Ortak dÃ¼nya modeli,
/// - BaÄŸlantÄ±/link saÄŸlÄ±ÄŸÄ±,
/// - Route execution / transport gÃ¶nderim durumu,
/// - GerÃ§ek command ACK/result korelasyon durumu,
/// - Inbound receive / gelen mesaj trafiÄŸi durumu,
/// - Command safety/security deÄŸerlendirme durumu,
/// - Mission allocation / gÃ¶rev atama karar durumu,
/// - Genel health deÄŸerlendirmesi,
/// - KÄ±sa aÃ§Ä±klama.
/// </summary>
public sealed record GroundOperationSnapshot
{
    /// <summary>
    /// Snapshot'Ä±n Ã¼retildiÄŸi UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Toplam kayÄ±tlÄ± araÃ§/node sayÄ±sÄ±.
    /// </summary>
    public int TotalNodeCount { get; init; }

    /// <summary>
    /// Online durumdaki araÃ§/node sayÄ±sÄ±.
    /// </summary>
    public int OnlineNodeCount { get; init; }

    /// <summary>
    /// Offline durumdaki araÃ§/node sayÄ±sÄ±.
    /// </summary>
    public int OfflineNodeCount { get; init; }

    /// <summary>
    /// Health deÄŸeri OK olan araÃ§/node sayÄ±sÄ±.
    /// </summary>
    public int HealthyNodeCount { get; init; }

    /// <summary>
    /// Health deÄŸeri Warning olan araÃ§/node sayÄ±sÄ±.
    /// </summary>
    public int WarningNodeCount { get; init; }

    /// <summary>
    /// Health deÄŸeri Critical veya Fault olan araÃ§/node sayÄ±sÄ±.
    /// </summary>
    public int CriticalNodeCount { get; init; }

    /// <summary>
    /// Ortalama batarya yÃ¼zdesi.
    /// 
    /// Batarya bilgisi olmayan araÃ§lar ortalamaya dahil edilmez.
    /// HiÃ§ batarya bilgisi yoksa null kalÄ±r.
    /// </summary>
    public double? AverageBatteryPercent { get; init; }

    /// <summary>
    /// KayÄ±tlÄ± toplam komut sayÄ±sÄ±.
    /// </summary>
    public int TotalCommandCount { get; init; }

    /// <summary>
    /// HenÃ¼z sonuÃ§ bekleyen komut sayÄ±sÄ±.
    /// </summary>
    public int PendingCommandCount { get; init; }

    /// <summary>
    /// TamamlanmÄ±ÅŸ komut sayÄ±sÄ±.
    /// </summary>
    public int CompletedCommandCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±lÄ± komut sayÄ±sÄ±.
    /// </summary>
    public int SuccessfulCommandCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z veya expired komut sayÄ±sÄ±.
    /// </summary>
    public int FailedCommandCount { get; init; }

    /// <summary>
    /// GroundWorldModel iÃ§indeki toplam dÃ¼nya nesnesi sayÄ±sÄ±.
    /// </summary>
    public int TotalWorldObjectCount { get; init; }

    /// <summary>
    /// GroundWorldModel iÃ§indeki aktif dÃ¼nya nesnesi sayÄ±sÄ±.
    /// </summary>
    public int ActiveWorldObjectCount { get; init; }

    /// <summary>
    /// Aktif obstacle sayÄ±sÄ±.
    /// </summary>
    public int ActiveObstacleCount { get; init; }

    /// <summary>
    /// Aktif target sayÄ±sÄ±.
    /// </summary>
    public int ActiveTargetCount { get; init; }

    /// <summary>
    /// Aktif no-go zone sayÄ±sÄ±.
    /// </summary>
    public int ActiveNoGoZoneCount { get; init; }

    /// <summary>
    /// LinkHealthTracker tarafÄ±ndan takip edilen toplam araÃ§ baÄŸlantÄ± grubu sayÄ±sÄ±.
    /// 
    /// Bu deÄŸer araÃ§ bazlÄ±dÄ±r.
    /// Ã–rneÄŸin Alpha iÃ§in WiFi + LoRa takip ediliyorsa bu alan yine 1 olur.
    /// </summary>
    public int LinkVehicleCount { get; init; }

    /// <summary>
    /// Takip edilen toplam transport link sayÄ±sÄ±.
    /// 
    /// Ã–rneÄŸin Alpha/WiFi, Alpha/LoRa, Beta/RF toplam 3 link sayÄ±lÄ±r.
    /// </summary>
    public int TotalLinkCount { get; init; }

    /// <summary>
    /// Good durumundaki link sayÄ±sÄ±.
    /// </summary>
    public int GoodLinkCount { get; init; }

    /// <summary>
    /// Degraded durumundaki link sayÄ±sÄ±.
    /// </summary>
    public int DegradedLinkCount { get; init; }

    /// <summary>
    /// Critical durumundaki link sayÄ±sÄ±.
    /// </summary>
    public int CriticalLinkCount { get; init; }

    /// <summary>
    /// Lost durumundaki link sayÄ±sÄ±.
    /// </summary>
    public int LostLinkCount { get; init; }

    /// <summary>
    /// Unknown durumundaki link sayÄ±sÄ±.
    /// </summary>
    public int UnknownLinkCount { get; init; }

    /// <summary>
    /// AraÃ§lar arasÄ±ndaki en iyi linklerin ortalama kalite skoru.
    /// 
    /// AraÃ§ baÅŸÄ±na OverallQualityScore deÄŸerlerinin ortalamasÄ±dÄ±r.
    /// Link verisi yoksa null kalÄ±r.
    /// </summary>
    public double? AverageVehicleLinkQualityScore { get; init; }

    /// <summary>
    /// TÃ¼m transport linklerinin ortalama kalite skoru.
    /// 
    /// Link verisi yoksa null kalÄ±r.
    /// </summary>
    public double? AverageTransportLinkQualityScore { get; init; }

    /// <summary>
    /// En dÃ¼ÅŸÃ¼k araÃ§ baÄŸlantÄ± kalite skoru.
    /// 
    /// Filodaki zayÄ±f halkayÄ± hÄ±zlÄ± gÃ¶rmek iÃ§in kullanÄ±lÄ±r.
    /// Link verisi yoksa null kalÄ±r.
    /// </summary>
    public double? WorstVehicleLinkQualityScore { get; init; }

    /// <summary>
    /// En dÃ¼ÅŸÃ¼k transport baÄŸlantÄ± kalite skoru.
    /// 
    /// Tekil transport seviyesindeki en kÃ¶tÃ¼ linki gÃ¶sterir.
    /// Link verisi yoksa null kalÄ±r.
    /// </summary>
    public double? WorstTransportLinkQualityScore { get; init; }

    /// <summary>
    /// Link health durumunun kÄ±sa insan-okunabilir aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops Communication Links panelinde veya diagnostics Ã¶zetinde gÃ¶sterilebilir.
    /// </summary>
    public string LinkHealthSummary { get; init; } = "No link health data.";

    /// <summary>
    /// AraÃ§ bazlÄ± link health snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafÄ±nda araÃ§ kartlarÄ±, link health paneli,
    /// communication diagnostics ekranÄ± ve ileride route karar izleme iÃ§in kullanÄ±labilir.
    /// </summary>
    public IReadOnlyList<VehicleLinkHealthSnapshot> LinkHealth { get; init; } =
        Array.Empty<VehicleLinkHealthSnapshot>();

    /// <summary>
    /// Toplam route execution kaydÄ± sayÄ±sÄ±.
    /// 
    /// Bu deÄŸer GroundTransportExecutionTracker iÃ§inde baÅŸlatÄ±lmÄ±ÅŸ route/gÃ¶nderim takip kayÄ±tlarÄ±nÄ± gÃ¶sterir.
    /// </summary>
    public int TotalRouteExecutionCount { get; init; }

    /// <summary>
    /// HenÃ¼z tamamlanmamÄ±ÅŸ route execution sayÄ±sÄ±.
    /// </summary>
    public int PendingRouteExecutionCount { get; init; }

    /// <summary>
    /// TamamlanmÄ±ÅŸ route execution sayÄ±sÄ±.
    /// </summary>
    public int CompletedRouteExecutionCount { get; init; }

    /// <summary>
    /// En az bir baÅŸarÄ±lÄ± gÃ¶nderim sonucu olan route execution sayÄ±sÄ±.
    /// </summary>
    public int SuccessfulRouteExecutionCount { get; init; }

    /// <summary>
    /// ACK alÄ±nmÄ±ÅŸ route execution sayÄ±sÄ±.
    /// </summary>
    public int AckedRouteExecutionCount { get; init; }

    /// <summary>
    /// Timeout yaÅŸamÄ±ÅŸ route execution sayÄ±sÄ±.
    /// </summary>
    public int TimeoutRouteExecutionCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z route execution sayÄ±sÄ±.
    /// </summary>
    public int FailedRouteExecutionCount { get; init; }

    /// <summary>
    /// Route edilemeyen execution sayÄ±sÄ±.
    /// 
    /// CanRoute false olan route kayÄ±tlarÄ±nÄ± sayar.
    /// </summary>
    public int RouteUnavailableExecutionCount { get; init; }

    /// <summary>
    /// Transport gÃ¶nderim sonuÃ§larÄ±nÄ±n toplam sayÄ±sÄ±.
    /// 
    /// Bir route execution iÃ§inde birden fazla transport denemesi olabilir.
    /// </summary>
    public int TotalTransportSendResultCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±lÄ± transport send sonucu sayÄ±sÄ±.
    /// Sent veya Acked durumlarÄ± baÅŸarÄ±lÄ± kabul edilir.
    /// </summary>
    public int SuccessfulTransportSendResultCount { get; init; }

    /// <summary>
    /// ACK alÄ±nmÄ±ÅŸ transport send sonucu sayÄ±sÄ±.
    /// </summary>
    public int AckedTransportSendResultCount { get; init; }

    /// <summary>
    /// Timeout transport send sonucu sayÄ±sÄ±.
    /// </summary>
    public int TimeoutTransportSendResultCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z transport send sonucu sayÄ±sÄ±.
    /// </summary>
    public int FailedTransportSendResultCount { get; init; }

    /// <summary>
    /// Route execution kayÄ±tlarÄ±nda Ã¶lÃ§Ã¼len ortalama en iyi latency.
    /// 
    /// Her execution iÃ§in BestLatencyMs deÄŸerlerinin ortalamasÄ±dÄ±r.
    /// Veri yoksa null kalÄ±r.
    /// </summary>
    public double? AverageRouteExecutionLatencyMs { get; init; }

    /// <summary>
    /// Route execution kayÄ±tlarÄ±nda gÃ¶rÃ¼len en iyi latency deÄŸeri.
    /// Veri yoksa null kalÄ±r.
    /// </summary>
    public double? BestRouteExecutionLatencyMs { get; init; }

    /// <summary>
    /// Route execution kayÄ±tlarÄ±nda gÃ¶rÃ¼len en kÃ¶tÃ¼ latency deÄŸeri.
    /// Veri yoksa null kalÄ±r.
    /// </summary>
    public double? WorstRouteExecutionLatencyMs { get; init; }

    /// <summary>
    /// Route execution / transport send durumunun kÄ±sa insan-okunabilir aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops diagnostics panelinde, command route ekranÄ±nda veya iletiÅŸim geÃ§miÅŸinde gÃ¶sterilebilir.
    /// </summary>
    public string RouteExecutionSummary { get; init; } = "No route execution data.";

    /// <summary>
    /// Route execution snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafÄ±nda:
    /// - route history,
    /// - command delivery trace,
    /// - transport send result history,
    /// - ACK / timeout izleme
    /// ekranlarÄ±nÄ± besleyebilir.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> RouteExecutions { get; init; } =
        Array.Empty<RouteExecutionSnapshot>();

    /// <summary>
    /// Toplam command ACK correlation kaydÄ± sayÄ±sÄ±.
    /// 
    /// Bu deÄŸer CommandAckCorrelator iÃ§inde takip edilen CommandId â†’ RouteExecution baÄŸlantÄ±larÄ±nÄ± gÃ¶sterir.
    /// </summary>
    public int TotalAckCorrelationCount { get; init; }

    /// <summary>
    /// HenÃ¼z gerÃ§ek FleetCommandResult almamÄ±ÅŸ correlation sayÄ±sÄ±.
    /// </summary>
    public int PendingAckCorrelationCount { get; init; }

    /// <summary>
    /// GerÃ§ek FleetCommandResult ile ACK/result almÄ±ÅŸ correlation sayÄ±sÄ±.
    /// </summary>
    public int AckedCorrelationCount { get; init; }

    /// <summary>
    /// TamamlanmÄ±ÅŸ correlation sayÄ±sÄ±.
    /// 
    /// Applied, Completed, Failed, Rejected, Timeout gibi nihai durumlar burada sayÄ±lÄ±r.
    /// </summary>
    public int CompletedAckCorrelationCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±lÄ± correlation sayÄ±sÄ±.
    /// 
    /// Accepted, Applied veya Completed durumlarÄ± baÅŸarÄ±lÄ± kabul edilir.
    /// </summary>
    public int SuccessfulAckCorrelationCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z correlation sayÄ±sÄ±.
    /// 
    /// Rejected, Failed, Expired veya Timeout durumlarÄ± baÅŸarÄ±sÄ±z kabul edilir.
    /// </summary>
    public int FailedAckCorrelationCount { get; init; }

    /// <summary>
    /// Belirli bir sÃ¼re iÃ§inde gerÃ§ek ACK/result almamÄ±ÅŸ pending correlation sayÄ±sÄ±.
    /// 
    /// Diagnostics motoru ilk fazda varsayÄ±lan timeout eÅŸiÄŸi ile hesaplar.
    /// </summary>
    public int ExpiredPendingAckCorrelationCount { get; init; }

    /// <summary>
    /// ACK/result alÄ±nan correlation kayÄ±tlarÄ± iÃ§in ortalama ACK gecikmesi.
    /// 
    /// Veri yoksa null kalÄ±r.
    /// </summary>
    public double? AverageAckCorrelationLatencyMs { get; init; }

    /// <summary>
    /// ACK/result alÄ±nan correlation kayÄ±tlarÄ± iÃ§inde en iyi gecikme.
    /// 
    /// Veri yoksa null kalÄ±r.
    /// </summary>
    public double? BestAckCorrelationLatencyMs { get; init; }

    /// <summary>
    /// ACK/result alÄ±nan correlation kayÄ±tlarÄ± iÃ§inde en kÃ¶tÃ¼ gecikme.
    /// 
    /// Veri yoksa null kalÄ±r.
    /// </summary>
    public double? WorstAckCorrelationLatencyMs { get; init; }

    /// <summary>
    /// Command ACK correlation durumunun kÄ±sa insan-okunabilir aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops command delivery / ACK diagnostics panelinde gÃ¶sterilebilir.
    /// </summary>
    public string AckCorrelationSummary { get; init; } = "No ACK correlation data.";

    /// <summary>
    /// Command ACK correlation snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafÄ±nda:
    /// - gerÃ§ek ACK izleme,
    /// - command delivery trace,
    /// - CommandId â†’ ExecutionId eÅŸleÅŸmesi,
    /// - ACK latency analizi
    /// ekranlarÄ±nÄ± besleyebilir.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> AckCorrelations { get; init; } =
        Array.Empty<CommandAckCorrelationSnapshot>();

    /// <summary>
    /// Toplam receive event sayÄ±sÄ±.
    /// 
    /// Bu deÄŸer GroundTransportReceiver Ã¼zerinden alÄ±nan tÃ¼m inbound mesaj olaylarÄ±nÄ± gÃ¶sterir.
    /// </summary>
    public int TotalReceiveEventCount { get; init; }

    /// <summary>
    /// BaÅŸarÄ±yla iÅŸlenmiÅŸ receive event sayÄ±sÄ±.
    /// 
    /// HandleEnvelope tarafÄ±ndan kabul edilip ilgili Ground Station modÃ¼llerine aktarÄ±lmÄ±ÅŸ mesajlarÄ± sayar.
    /// </summary>
    public int HandledReceiveEventCount { get; init; }

    /// <summary>
    /// Ä°ÅŸlenirken hata oluÅŸmuÅŸ receive event sayÄ±sÄ±.
    /// 
    /// Deserialize, dispatch, payload restore veya HandleEnvelope aÅŸamasÄ±nda hata alan inbound mesajlar burada sayÄ±lÄ±r.
    /// </summary>
    public int FailedReceiveEventCount { get; init; }

    /// <summary>
    /// AlÄ±nmÄ±ÅŸ fakat anlamlÄ± ÅŸekilde iÅŸlenememiÅŸ receive event sayÄ±sÄ±.
    /// 
    /// Mesaj geldiÄŸi halde bilinmeyen tip, eksik payload veya handler eksikliÄŸi nedeniyle kullanÄ±lamayan olaylar iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public int UnhandledReceiveEventCount { get; init; }

    /// <summary>
    /// Son inbound mesajÄ±n alÄ±ndÄ±ÄŸÄ± UTC zaman.
    /// 
    /// HiÃ§ receive event yoksa null kalÄ±r.
    /// </summary>
    public DateTimeOffset? LastReceiveUtc { get; init; }

    /// <summary>
    /// Inbound receive durumunun kÄ±sa insan-okunabilir aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops inbound communication panelinde veya diagnostics Ã¶zetinde gÃ¶sterilebilir.
    /// </summary>
    public string ReceiveHealthSummary { get; init; } = "No receive data.";

    /// <summary>
    /// Gelen FleetHeartbeat mesajÄ± sayÄ±sÄ±.
    /// </summary>
    public int InboundFleetHeartbeatCount { get; init; }

    /// <summary>
    /// Gelen FleetCommandResult mesajÄ± sayÄ±sÄ±.
    /// </summary>
    public int InboundFleetCommandResultCount { get; init; }

    /// <summary>
    /// Gelen FleetCommand mesajÄ± sayÄ±sÄ±.
    /// 
    /// Ground Station tarafÄ±nda genelde araÃ§tan komut gelmesi beklenmez;
    /// ama peer/relay veya test senaryolarÄ±nda anlamlÄ± olabilir.
    /// </summary>
    public int InboundFleetCommandCount { get; init; }

    /// <summary>
    /// Gelen VehicleNodeStatus mesajÄ± sayÄ±sÄ±.
    /// </summary>
    public int InboundVehicleNodeStatusCount { get; init; }

    /// <summary>
    /// Bilinmeyen veya sÄ±nÄ±flandÄ±rÄ±lamayan inbound mesaj sayÄ±sÄ±.
    /// </summary>
    public int InboundUnknownMessageCount { get; init; }

    /// <summary>
    /// Receive event snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafÄ±nda:
    /// - inbound message history,
    /// - heartbeat geÃ§miÅŸi,
    /// - command result geÃ§miÅŸi,
    /// - receive hata geÃ§miÅŸi,
    /// - transport bazlÄ± gelen trafik ekranlarÄ±nÄ± besleyebilir.
    /// </summary>
    public IReadOnlyList<GroundTransportReceiveEvent> ReceiveEvents { get; init; } =
        Array.Empty<GroundTransportReceiveEvent>();

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde komut izinli miydi?
    /// 
    /// HiÃ§ komut safety deÄŸerlendirmesi yapÄ±lmadÄ±ysa null kalÄ±r.
    /// </summary>
    public bool? LastCommandSafetyAllowed { get; init; }

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde komut reddedildi mi?
    /// 
    /// HiÃ§ komut safety deÄŸerlendirmesi yapÄ±lmadÄ±ysa null kalÄ±r.
    /// </summary>
    public bool? LastCommandSafetyRejected { get; init; }

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinin kÄ±sa aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops Command Safety panelinde gÃ¶sterilebilir.
    /// </summary>
    public string LastCommandSafetyReason { get; init; } = "No command safety evaluation.";

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde tespit edilen toplam issue sayÄ±sÄ±.
    /// </summary>
    public int LastCommandSafetyIssueCount { get; init; }

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde tespit edilen blocking issue sayÄ±sÄ±.
    /// </summary>
    public int LastCommandSafetyBlockingIssueCount { get; init; }

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde tespit edilen warning issue sayÄ±sÄ±.
    /// </summary>
    public int LastCommandSafetyWarningIssueCount { get; init; }

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde Ã¼retilen issue kodlarÄ±.
    /// 
    /// Ã–rnek:
    /// - TARGET_UNKNOWN
    /// - TARGET_OFFLINE
    /// - DUPLICATE_COMMAND_ID
    /// - EMERGENCY_PRIORITY_REQUIRED
    /// </summary>
    public IReadOnlyList<string> LastCommandSafetyIssueCodes { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Son command safety/security deÄŸerlendirmesinde Ã¼retilen issue detaylarÄ±.
    /// 
    /// Bu alan Hydronom Ops tarafÄ±nda command safety detay ekranÄ±nÄ± besleyebilir.
    /// </summary>
    public IReadOnlyList<CommandValidationIssue> LastCommandSafetyIssues { get; init; } =
        Array.Empty<CommandValidationIssue>();

    /// <summary>
    /// Son gÃ¶rev atama kararÄ±nÄ±n MissionId deÄŸeri.
    /// 
    /// HiÃ§ gÃ¶rev atamasÄ± yapÄ±lmadÄ±ysa boÅŸ kalÄ±r.
    /// </summary>
    public string LastMissionAllocationMissionId { get; init; } = string.Empty;

    /// <summary>
    /// Son gÃ¶rev atama baÅŸarÄ±lÄ± mÄ±?
    /// 
    /// HiÃ§ gÃ¶rev atamasÄ± yapÄ±lmadÄ±ysa null kalÄ±r.
    /// </summary>
    public bool? LastMissionAllocationSuccess { get; init; }

    /// <summary>
    /// Son gÃ¶rev atamada seÃ§ilen node id.
    /// 
    /// Atama baÅŸarÄ±sÄ±zsa veya hiÃ§ atama yapÄ±lmadÄ±ysa boÅŸ kalÄ±r.
    /// </summary>
    public string LastMissionAllocationSelectedNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Son gÃ¶rev atamada seÃ§ilen node display name.
    /// 
    /// Atama baÅŸarÄ±sÄ±zsa veya hiÃ§ atama yapÄ±lmadÄ±ysa boÅŸ kalÄ±r.
    /// </summary>
    public string LastMissionAllocationSelectedDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Son gÃ¶rev atama aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops Mission Allocation panelinde gÃ¶sterilebilir.
    /// </summary>
    public string LastMissionAllocationReason { get; init; } = "No mission allocation.";

    /// <summary>
    /// Son gÃ¶rev atamada seÃ§ilen adayÄ±n final skoru.
    /// 
    /// Atama yapÄ±lmadÄ±ysa null kalÄ±r.
    /// </summary>
    public double? LastMissionAllocationScore { get; init; }

    /// <summary>
    /// Son gÃ¶rev atamadaki aday node id listesi.
    /// </summary>
    public IReadOnlyList<string> LastMissionAllocationCandidateNodeIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Son gÃ¶rev atamada reddedilen node id â†’ sebep eÅŸleÅŸmeleri.
    /// </summary>
    public IReadOnlyDictionary<string, string> LastMissionAllocationRejectedNodeReasons { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Ground Station genel health deÄŸerlendirmesi.
    /// 
    /// Ã–rnek:
    /// - OK
    /// - Warning
    /// - Critical
    /// </summary>
    public string OverallHealth { get; init; } = "Unknown";

    /// <summary>
    /// Genel durumun kÄ±sa insan-okunabilir aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops Ã¼st panelinde veya log ekranÄ±nda gÃ¶sterilebilir.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Snapshot Ã¼zerinde kritik durum olup olmadÄ±ÄŸÄ±nÄ± hÄ±zlÄ±ca dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public bool HasCriticalIssues =>
        string.Equals(OverallHealth, "Critical", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Snapshot Ã¼zerinde uyarÄ± durumu olup olmadÄ±ÄŸÄ±nÄ± hÄ±zlÄ±ca dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public bool HasWarnings =>
        string.Equals(OverallHealth, "Warning", StringComparison.OrdinalIgnoreCase);
}
