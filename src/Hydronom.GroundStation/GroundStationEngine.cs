namespace Hydronom.GroundStation;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.Coordination;
using Hydronom.GroundStation.Diagnostics;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.Routing;
using Hydronom.GroundStation.Telemetry;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.WorldModel;
using FleetRegistryStore = Hydronom.GroundStation.FleetRegistry.FleetRegistry;

/// <summary>
/// Hydronom Ground Station tarafının ana giriş sınıfıdır.
/// 
/// Bu sınıf, yer istasyonunun ana koordinasyon kabuğudur.
/// Amacı:
/// - FleetRegistry'yi tek merkezden yönetmek,
/// - CommandTracker ile gönderilen komutları ve sonuçlarını takip etmek,
/// - GroundWorldModel ile ortak operasyon dünyasını tutmak,
/// - MissionAllocator ile görev için uygun araç seçimini başlatmak,
/// - FleetCoordinator ile görev isteğinden komut üretmek,
/// - CommunicationRouter ile gönderilecek mesajların route kararını üretmek,
/// - TelemetryRoutePlanner ile route sonucuna göre telemetry yoğunluğu planlamak,
/// - LinkHealthTracker ile araç/transport bazlı bağlantı sağlığını takip etmek,
/// - GroundTransportExecutionTracker ile route/gönderim sonuçlarını takip etmek,
/// - GroundDiagnosticsEngine ile tek çağrıda operasyon snapshot'ı üretmek,
/// - Gelen HydronomEnvelope mesajlarını dispatcher üzerinden yorumlamak,
/// - Heartbeat mesajlarını registry'ye işlemek,
/// - Komut sonuçlarını izlenebilir hale getirmek,
/// - Yer istasyonu tarafında büyüyecek modüller için ana koordinasyon noktası olmaktır.
/// 
/// İleride bu sınıfın altına şunlar bağlanabilir:
/// - FleetCoordinator
/// - MissionPlanner
/// - MissionAllocator
/// - CommunicationRouter
/// - TelemetryFusionEngine
/// - GroundAnalysisEngine
/// - ReplayRecorder
/// - GroundWorldModel
/// - LinkHealthTracker / LinkQualityMap
/// - GroundTransportExecutionTracker
/// </summary>
public sealed class GroundStationEngine
{
    /// <summary>
    /// Yer istasyonunun araç/node kayıt defteri.
    /// </summary>
    public FleetRegistryStore FleetRegistry { get; } = new();

    /// <summary>
    /// Yer istasyonu tarafından gönderilen komutları ve araçlardan dönen sonuçları takip eder.
    /// </summary>
    public CommandTracker CommandTracker { get; } = new();

    /// <summary>
    /// Yer istasyonunun ortak dünya modelidir.
    /// </summary>
    public GroundWorldModel WorldModel { get; } = new();

    /// <summary>
    /// Görev isteklerini filo içindeki uygun araca atamaya çalışan ilk görev dağıtım modülüdür.
    /// </summary>
    public MissionAllocator MissionAllocator { get; } = new();

    /// <summary>
    /// Görev isteğini alıp uygun aracı seçen ve araca gönderilecek FleetCommand envelope üreten
    /// koordinasyon modülüdür.
    /// </summary>
    public FleetCoordinator FleetCoordinator { get; }

    /// <summary>
    /// Gönderilecek HydronomEnvelope mesajları için route sonucu üreten iletişim yönlendiricisidir.
    /// </summary>
    public CommunicationRouter CommunicationRouter { get; } = new();

    /// <summary>
    /// CommunicationRouter tarafından üretilen route sonucuna göre telemetry profil planı üretir.
    /// </summary>
    public TelemetryRoutePlanner TelemetryRoutePlanner { get; } = new();

    /// <summary>
    /// Yer istasyonu seviyesinde araçların haberleşme bağlantı kalitesini takip eder.
    /// </summary>
    public LinkHealthTracker LinkHealthTracker { get; } = new();

    /// <summary>
    /// Route execution / transport gönderim sonucu takip motorudur.
    /// 
    /// Bu yapı:
    /// - Route kararından execution kaydı başlatır,
    /// - Transport gönderim denemelerini kaydeder,
    /// - Sent / Acked / Timeout / Failed sonuçlarını takip eder,
    /// - LinkHealthTracker'ı otomatik günceller,
    /// - Diagnostics ve Hydronom Ops için execution snapshot üretir.
    /// 
    /// Gerçek transport katmanı geldiğinde router → transport send → result → link health
    /// zincirinin merkezi olacaktır.
    /// </summary>
    public GroundTransportExecutionTracker TransportExecutionTracker { get; }

    /// <summary>
    /// Ground Station'ın genel durumunu tek bir operasyon snapshot'ına dönüştüren diagnostics motorudur.
    /// </summary>
    public GroundDiagnosticsEngine DiagnosticsEngine { get; } = new();

    /// <summary>
    /// Ground Station tarafında gelen mesajları MessageType değerine göre
    /// ilgili handler'a yönlendiren dispatcher.
    /// </summary>
    public GroundMessageDispatcher Dispatcher { get; }

    /// <summary>
    /// Yer istasyonunun kendi node kimliği.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new()
    {
        NodeId = "GROUND-001",
        DisplayName = "Hydronom Ground Station",
        NodeType = "GroundStation",
        Role = "Coordinator"
    };

    /// <summary>
    /// GroundStationEngine oluşturur.
    /// </summary>
    public GroundStationEngine()
    {
        FleetCoordinator = new FleetCoordinator(MissionAllocator);
        TransportExecutionTracker = new GroundTransportExecutionTracker(LinkHealthTracker);

        Dispatcher = new GroundMessageDispatcher(
            onHeartbeat: FleetRegistry.ApplyHeartbeat,
            onCommandResult: HandleCommandResult);
    }

    /// <summary>
    /// Gelen HydronomEnvelope mesajını işler.
    /// </summary>
    public bool HandleEnvelope(HydronomEnvelope envelope)
    {
        return Dispatcher.Dispatch(envelope);
    }

    /// <summary>
    /// Yer istasyonu tarafından üretilecek/gönderilecek bir komutu kayıt altına alır
    /// ve aynı komutu HydronomEnvelope içine sararak döndürür.
    /// </summary>
    public HydronomEnvelope? CreateTrackedCommandEnvelope(FleetCommand command)
    {
        if (command is null || !command.IsValid)
            return null;

        var tracked = CommandTracker.TrackCommand(command);

        if (!tracked)
            return null;

        return HydronomEnvelopeFactory.CreateCommand(command);
    }

    /// <summary>
    /// Verilen görev isteği için mevcut fleet snapshot üzerinden en uygun aracı seçer.
    /// </summary>
    public MissionAllocationResult AllocateMission(MissionRequest request)
    {
        return MissionAllocator.Allocate(
            request,
            FleetRegistry.GetSnapshot());
    }

    /// <summary>
    /// Verilen görev isteğini filo koordinasyon sonucuna çevirir.
    /// </summary>
    public FleetCoordinationResult CoordinateMission(MissionRequest request)
    {
        var coordination = FleetCoordinator.CoordinateMission(
            request,
            FleetRegistry.GetSnapshot(),
            Identity.NodeId,
            isOperatorIssued: true);

        if (!coordination.Success || coordination.Command is null)
            return coordination;

        var trackedEnvelope = CreateTrackedCommandEnvelope(coordination.Command);

        if (trackedEnvelope is null)
        {
            return FleetCoordinationResult.Failed(
                request,
                coordination.Allocation,
                "Mission command was generated but could not be tracked by CommandTracker.");
        }

        return coordination with
        {
            Envelope = trackedEnvelope,
            Reason = $"{coordination.Reason} Command tracked by GroundStationEngine."
        };
    }

    /// <summary>
    /// Verilen envelope için mevcut fleet snapshot üzerinden route sonucu üretir.
    /// </summary>
    public CommunicationRouteResult RouteEnvelope(HydronomEnvelope envelope)
    {
        return CommunicationRouter.Route(
            envelope,
            FleetRegistry.GetSnapshot());
    }

    /// <summary>
    /// Verilen envelope için mevcut fleet snapshot ve LinkHealthTracker üzerinden link-aware route sonucu üretir.
    /// </summary>
    public CommunicationRouteResult RouteEnvelopeWithLinkHealth(HydronomEnvelope envelope)
    {
        return CommunicationRouter.Route(
            envelope,
            FleetRegistry.GetSnapshot(),
            linkAvailabilityFilter: (vehicleId, transportKind) =>
                LinkHealthTracker
                    .GetAvailableLinks(vehicleId)
                    .Any(link => link.TransportKind == transportKind));
    }

    /// <summary>
    /// Verilen envelope için route sonucu üretir ve route execution kaydı başlatır.
    /// 
    /// Bu metot gerçek gönderim yapmaz.
    /// Sadece gönderim denemesinin takip edilebilir kayıt nesnesini üretir.
    /// </summary>
    public RouteExecutionRecord BeginRouteExecution(
        HydronomEnvelope envelope,
        DateTimeOffset? nowUtc = null)
    {
        var route = RouteEnvelope(envelope);

        return TransportExecutionTracker.BeginExecution(
            envelope,
            route,
            nowUtc);
    }

    /// <summary>
    /// Verilen envelope için link health destekli route sonucu üretir ve route execution kaydı başlatır.
    /// 
    /// Bu metot ileride sağlıklı linklerden gönderim denemesi başlatmak için kullanılacaktır.
    /// </summary>
    public RouteExecutionRecord BeginRouteExecutionWithLinkHealth(
        HydronomEnvelope envelope,
        DateTimeOffset? nowUtc = null)
    {
        var route = RouteEnvelopeWithLinkHealth(envelope);

        return TransportExecutionTracker.BeginExecution(
            envelope,
            route,
            nowUtc);
    }

    /// <summary>
    /// Verilen route sonucu ve envelope ile manuel execution kaydı başlatır.
    /// 
    /// Testlerde veya dış modüllerde route önceden üretildiyse kullanılır.
    /// </summary>
    public RouteExecutionRecord BeginRouteExecution(
        HydronomEnvelope envelope,
        CommunicationRouteResult route,
        DateTimeOffset? nowUtc = null)
    {
        return TransportExecutionTracker.BeginExecution(
            envelope,
            route,
            nowUtc);
    }

    /// <summary>
    /// Belirli execution için transport gönderim denemesi başladığını kaydeder.
    /// 
    /// LinkHealthTracker üzerinde send sayacını da artırır.
    /// </summary>
    public bool RecordTransportSendAttempt(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset? nowUtc = null)
    {
        return TransportExecutionTracker.RecordSendAttempt(
            executionId,
            transportKind,
            nowUtc);
    }

    /// <summary>
    /// Belirli execution için başarılı gönderim sonucunu kaydeder.
    /// 
    /// LinkHealthTracker üzerinde route success metriğini de günceller.
    /// </summary>
    public bool RecordTransportSent(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        double? latencyMs = null,
        string? reason = null)
    {
        return TransportExecutionTracker.RecordSent(
            executionId,
            transportKind,
            startedUtc,
            completedUtc,
            latencyMs,
            reason);
    }

    /// <summary>
    /// Belirli execution için ACK alınmış gönderim sonucunu kaydeder.
    /// 
    /// LinkHealthTracker üzerinde hem route success hem ACK metriği güncellenir.
    /// </summary>
    public bool RecordTransportAcked(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        double? latencyMs = null,
        string? reason = null)
    {
        return TransportExecutionTracker.RecordAcked(
            executionId,
            transportKind,
            startedUtc,
            completedUtc,
            latencyMs,
            reason);
    }

    /// <summary>
    /// Belirli execution için timeout sonucunu kaydeder.
    /// 
    /// LinkHealthTracker üzerinde timeout metriğini de günceller.
    /// </summary>
    public bool RecordTransportTimeout(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string? reason = null)
    {
        return TransportExecutionTracker.RecordTimeout(
            executionId,
            transportKind,
            startedUtc,
            completedUtc,
            reason);
    }

    /// <summary>
    /// Belirli execution için başarısız gönderim sonucunu kaydeder.
    /// 
    /// LinkHealthTracker üzerinde route failure metriğini de günceller.
    /// </summary>
    public bool RecordTransportFailure(
        string executionId,
        TransportKind transportKind,
        TransportSendStatus status,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string reason,
        string? errorMessage = null)
    {
        return TransportExecutionTracker.RecordFailure(
            executionId,
            transportKind,
            status,
            startedUtc,
            completedUtc,
            reason,
            errorMessage);
    }

    /// <summary>
    /// Tüm route execution kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetRouteExecutionSnapshot()
    {
        return TransportExecutionTracker.GetSnapshot();
    }

    /// <summary>
    /// Pending route execution kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetPendingRouteExecutionSnapshot()
    {
        return TransportExecutionTracker.GetPendingSnapshot();
    }

    /// <summary>
    /// Başarısız route execution kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetFailedRouteExecutionSnapshot()
    {
        return TransportExecutionTracker.GetFailedSnapshot();
    }

    /// <summary>
    /// Verilen route sonucuna göre telemetry profil planı üretir.
    /// </summary>
    public TelemetryRoutePlan PlanTelemetryForRoute(CommunicationRouteResult route)
    {
        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen envelope için önce route sonucu, sonra telemetry route planı üretir.
    /// </summary>
    public TelemetryRoutePlan PlanTelemetryForEnvelope(HydronomEnvelope envelope)
    {
        var route = RouteEnvelope(envelope);
        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen envelope için link health destekli route sonucu, sonra telemetry route planı üretir.
    /// </summary>
    public TelemetryRoutePlan PlanTelemetryForEnvelopeWithLinkHealth(HydronomEnvelope envelope)
    {
        var route = RouteEnvelopeWithLinkHealth(envelope);
        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen görev isteğini koordine eder ve ortaya çıkan envelope için route sonucu üretir.
    /// </summary>
    public CommunicationRouteResult? CoordinateMissionAndRoute(MissionRequest request)
    {
        var coordination = CoordinateMission(request);

        if (!coordination.Success || coordination.Envelope is null)
            return null;

        return RouteEnvelope(coordination.Envelope);
    }

    /// <summary>
    /// Verilen görev isteğini koordine eder ve ortaya çıkan envelope için link health destekli route sonucu üretir.
    /// </summary>
    public CommunicationRouteResult? CoordinateMissionAndRouteWithLinkHealth(MissionRequest request)
    {
        var coordination = CoordinateMission(request);

        if (!coordination.Success || coordination.Envelope is null)
            return null;

        return RouteEnvelopeWithLinkHealth(coordination.Envelope);
    }

    /// <summary>
    /// Verilen görev isteğini koordine eder, route eder ve telemetry planını üretir.
    /// </summary>
    public TelemetryRoutePlan? CoordinateMissionRouteAndPlanTelemetry(MissionRequest request)
    {
        var route = CoordinateMissionAndRoute(request);

        if (route is null)
            return null;

        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen görev isteğini koordine eder, link health destekli route eder ve telemetry planını üretir.
    /// </summary>
    public TelemetryRoutePlan? CoordinateMissionRouteAndPlanTelemetryWithLinkHealth(MissionRequest request)
    {
        var route = CoordinateMissionAndRouteWithLinkHealth(request);

        if (route is null)
            return null;

        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Ground Station'ın mevcut operasyon durumundan tek bakışlık snapshot üretir.
    /// </summary>
   public GroundOperationSnapshot CreateOperationSnapshot()
{
    return DiagnosticsEngine.CreateSnapshot(
        FleetRegistry.GetSnapshot(),
        CommandTracker.GetSnapshot(),
        WorldModel,
        LinkHealthTracker.GetSnapshot(DateTime.UtcNow),
        TransportExecutionTracker.GetSnapshot());
}

    /// <summary>
    /// Belirli bir aracın belirli bir transport üzerinden görüldüğünü LinkHealthTracker'a bildirir.
    /// </summary>
    public void MarkLinkSeen(
        string vehicleId,
        TransportKind transportKind,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.MarkSeen(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Bir araca belirli transport üzerinden mesaj gönderme denemesini kayıt altına alır.
    /// </summary>
    public void RecordLinkSend(
        string vehicleId,
        TransportKind transportKind,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.RecordSend(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Bir route/gönderim denemesinin başarılı olduğunu bağlantı sağlık metriğine işler.
    /// </summary>
    public void RecordLinkRouteSuccess(
        string vehicleId,
        TransportKind transportKind,
        double? latencyMs = null,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.RecordRouteSuccess(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow,
            latencyMs);
    }

    /// <summary>
    /// Bir route/gönderim denemesinin başarısız olduğunu bağlantı sağlık metriğine işler.
    /// </summary>
    public void RecordLinkRouteFailure(
        string vehicleId,
        TransportKind transportKind,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.RecordRouteFailure(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Belirli bir transport üzerinden ACK alındığını bağlantı sağlık metriğine işler.
    /// </summary>
    public void RecordLinkAck(
        string vehicleId,
        TransportKind transportKind,
        double? latencyMs = null,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.RecordAck(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow,
            latencyMs);
    }

    /// <summary>
    /// Belirli bir transport üzerinden timeout yaşandığını bağlantı sağlık metriğine işler.
    /// </summary>
    public void RecordLinkTimeout(
        string vehicleId,
        TransportKind transportKind,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.RecordTimeout(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Tahmini paket kaybı bilgisini bağlantı sağlık metriğine işler.
    /// </summary>
    public void RecordEstimatedLinkPacketLoss(
        string vehicleId,
        TransportKind transportKind,
        int lostPacketCount = 1,
        DateTime? nowUtc = null)
    {
        LinkHealthTracker.RecordEstimatedPacketLoss(
            vehicleId,
            transportKind,
            nowUtc ?? DateTime.UtcNow,
            lostPacketCount);
    }

    /// <summary>
    /// Tüm araçların bağlantı sağlık snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<VehicleLinkHealthSnapshot> GetLinkHealthSnapshot(DateTime? nowUtc = null)
    {
        return LinkHealthTracker.GetSnapshot(nowUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Belirli bir araç için en iyi kullanılabilir bağlantıyı döndürür.
    /// </summary>
    public TransportLinkMetrics? GetBestAvailableLink(string vehicleId)
    {
        return LinkHealthTracker.GetBestAvailableLink(vehicleId);
    }

    /// <summary>
    /// Belirli bir araç için kullanılabilir bağlantı listesini kalite skoruna göre döndürür.
    /// </summary>
    public IReadOnlyList<TransportLinkMetrics> GetAvailableLinks(string vehicleId)
    {
        return LinkHealthTracker.GetAvailableLinks(vehicleId);
    }

    /// <summary>
    /// GroundWorldModel içine yeni bir dünya nesnesi ekler veya mevcut nesneyi günceller.
    /// </summary>
    public bool UpsertWorldObject(GroundWorldObject worldObject)
    {
        return WorldModel.Upsert(worldObject);
    }

    /// <summary>
    /// GroundWorldModel içindeki tüm dünya nesnelerinin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetWorldSnapshot()
    {
        return WorldModel.GetSnapshot();
    }

    /// <summary>
    /// GroundWorldModel içindeki aktif dünya nesnelerinin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveWorldSnapshot()
    {
        return WorldModel.GetActiveSnapshot();
    }

    /// <summary>
    /// Aktif engellerin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveObstacles()
    {
        return WorldModel.GetActiveObstacles();
    }

    /// <summary>
    /// Aktif hedeflerin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveTargets()
    {
        return WorldModel.GetActiveTargets();
    }

    /// <summary>
    /// FleetCommandResult mesajlarını işler.
    /// </summary>
    private bool HandleCommandResult(FleetCommandResult result)
    {
        if (result is null || !result.IsValid)
            return false;

        return CommandTracker.ApplyResult(result);
    }

    /// <summary>
    /// Registry içindeki tüm araç/node durumlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetFleetSnapshot()
    {
        return FleetRegistry.GetSnapshot();
    }

    /// <summary>
    /// Online kabul edilen araç/node durumlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetOnlineFleetSnapshot()
    {
        return FleetRegistry.GetOnlineNodes();
    }

    /// <summary>
    /// Kayıtlı tüm komut geçmişinin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetCommandHistorySnapshot()
    {
        return CommandTracker.GetSnapshot();
    }

    /// <summary>
    /// Henüz sonuç bekleyen komutların snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetPendingCommandSnapshot()
    {
        return CommandTracker.GetPendingCommands();
    }

    /// <summary>
    /// Belirli süre heartbeat göndermeyen araçları offline olarak işaretler.
    /// </summary>
    public int MarkStaleNodesOffline(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        return FleetRegistry.MarkStaleNodesOffline(timeout, nowUtc);
    }

    /// <summary>
    /// Belirli süre boyunca sonuç dönmeyen pending komutları expired olarak işaretler.
    /// </summary>
    public int MarkExpiredCommands(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        return CommandTracker.MarkExpiredCommands(timeout, nowUtc);
    }

    /// <summary>
    /// Belirli süreden uzun süredir güncellenmeyen aktif dünya nesnelerini pasif hale getirir.
    /// </summary>
    public int DeactivateStaleWorldObjects(TimeSpan maxAge, DateTimeOffset? nowUtc = null)
    {
        return WorldModel.DeactivateStaleObjects(maxAge, nowUtc);
    }
}