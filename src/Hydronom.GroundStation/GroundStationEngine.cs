namespace Hydronom.GroundStation;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Ack;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.Coordination;
using Hydronom.GroundStation.Diagnostics;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.Routing;
using Hydronom.GroundStation.Telemetry;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.Transports;
using Hydronom.GroundStation.Transports.Receive;
using Hydronom.GroundStation.WorldModel;
using FleetRegistryStore = Hydronom.GroundStation.FleetRegistry.FleetRegistry;

/// <summary>
/// Hydronom Ground Station tarafının ana giriş sınıfıdır.
/// 
/// Bu sınıf, yer istasyonunun ana koordinasyon kabuğudur.
/// Amacı:
/// - FleetRegistry'yi tek merkezden yönetmek,
/// - CommandTracker ile gönderilen komutları ve sonuçlarını takip etmek,
/// - CommandAckCorrelator ile gönderilen command ile gerçek FleetCommandResult cevabını eşleştirmek,
/// - GroundWorldModel ile ortak operasyon dünyasını tutmak,
/// - MissionAllocator ile görev için uygun araç seçimini başlatmak,
/// - FleetCoordinator ile görev isteğinden komut üretmek,
/// - CommunicationRouter ile gönderilecek mesajların route kararını üretmek,
/// - TelemetryRoutePlanner ile route sonucuna göre telemetry yoğunluğu planlamak,
/// - LinkHealthTracker ile araç/transport bazlı bağlantı sağlığını takip etmek,
/// - GroundTransportExecutionTracker ile route/gönderim sonuçlarını takip etmek,
/// - GroundTransportManager ile route kararını gerçek ITransport.SendAsync zincirine bağlamak,
/// - GroundTransportReceiver ile transportlardan gelen envelope'ları otomatik dinlemek,
/// - GroundDiagnosticsEngine ile tek çağrıda operasyon snapshot'ı üretmek,
/// - Gelen HydronomEnvelope mesajlarını dispatcher üzerinden yorumlamak,
/// - Yer istasyonu tarafında büyüyecek modüller için ana koordinasyon noktası olmaktır.
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
    /// Yer istasyonu tarafından gönderilen komutlar ile araçtan gelen gerçek FleetCommandResult cevaplarını eşleştirir.
    /// 
    /// Bu yapı, SendAsync başarılı oldu diye ACK varsaymak yerine,
    /// gerçek command result geldiğinde ilgili route execution kaydını günceller.
    /// </summary>
    public CommandAckCorrelator CommandAckCorrelator { get; } = new();

    /// <summary>
    /// Yer istasyonunun ortak dünya modelidir.
    /// </summary>
    public GroundWorldModel WorldModel { get; } = new();

    /// <summary>
    /// Görev isteklerini filo içindeki uygun araca atamaya çalışan ilk görev dağıtım modülüdür.
    /// </summary>
    public MissionAllocator MissionAllocator { get; } = new();

    /// <summary>
    /// Görev isteğini alıp uygun aracı seçen ve araca gönderilecek FleetCommand envelope üreten koordinasyon modülüdür.
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
    /// </summary>
    public GroundTransportExecutionTracker TransportExecutionTracker { get; }

    /// <summary>
    /// Ground Station tarafındaki gerçek/mock transport instance'larını tutan registry.
    /// </summary>
    public GroundTransportRegistry TransportRegistry { get; } = new();

    /// <summary>
    /// Route kararını gerçek ITransport.SendAsync zincirine bağlayan transport manager.
    /// </summary>
    public GroundTransportManager TransportManager { get; }

    /// <summary>
    /// Transportlardan gelen HydronomEnvelope mesajlarını dinleyen receive pipeline'dır.
    /// </summary>
    public GroundTransportReceiver TransportReceiver { get; }

    /// <summary>
    /// Ground Station'ın genel durumunu tek bir operasyon snapshot'ına dönüştüren diagnostics motorudur.
    /// </summary>
    public GroundDiagnosticsEngine DiagnosticsEngine { get; } = new();

    /// <summary>
    /// Ground Station tarafında gelen mesajları MessageType değerine göre ilgili handler'a yönlendiren dispatcher.
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
        TransportManager = new GroundTransportManager(TransportRegistry, TransportExecutionTracker);

        Dispatcher = new GroundMessageDispatcher(
            onHeartbeat: FleetRegistry.ApplyHeartbeat,
            onCommandResult: HandleCommandResult);

        TransportReceiver = new GroundTransportReceiver(
            TransportRegistry,
            LinkHealthTracker,
            HandleEnvelope);
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
    /// Transport registry içine yeni transport ekler.
    /// </summary>
    public bool RegisterTransport(ITransport transport)
    {
        return TransportRegistry.Add(transport);
    }

    /// <summary>
    /// Transport registry içinden transport kaldırır.
    /// </summary>
    public bool RemoveTransportByName(string name)
    {
        return TransportRegistry.RemoveByName(name);
    }

    /// <summary>
    /// Kayıtlı tüm transport bağlantılarını başlatır.
    /// </summary>
    public Task ConnectAllTransportsAsync(CancellationToken cancellationToken = default)
    {
        return TransportRegistry.ConnectAllAsync(cancellationToken);
    }

    /// <summary>
    /// Kayıtlı tüm transport bağlantılarını kapatır.
    /// </summary>
    public Task DisconnectAllTransportsAsync(CancellationToken cancellationToken = default)
    {
        return TransportRegistry.DisconnectAllAsync(cancellationToken);
    }

    /// <summary>
    /// Kayıtlı ve bağlı tüm transport'lar için receive pipeline çalıştırır.
    /// </summary>
    public Task RunTransportReceiversAsync(CancellationToken cancellationToken = default)
    {
        return TransportReceiver.RunAllAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir transport için receive pipeline çalıştırır.
    /// </summary>
    public Task RunTransportReceiverAsync(
        ITransport transport,
        CancellationToken cancellationToken = default)
    {
        return TransportReceiver.RunTransportAsync(
            transport,
            cancellationToken);
    }

    /// <summary>
    /// Transport receive event geçmişinin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundTransportReceiveEvent> GetTransportReceiveSnapshot()
    {
        return TransportReceiver.GetSnapshot();
    }

    /// <summary>
    /// Kayıtlı receive event sayısını döndürür.
    /// </summary>
    public int TransportReceiveEventCount => TransportReceiver.EventCount;

    /// <summary>
    /// Verilen envelope için route üretir ve transport manager üzerinden gerçek/mock SendAsync zincirini çalıştırır.
    /// 
    /// Bu metot genel envelope gönderimi içindir.
    /// FleetCommand correlation gerekiyorsa SendTrackedCommandAsync kullanılmalıdır.
    /// </summary>
    public async Task<RouteExecutionRecord> SendEnvelopeAsync(
        HydronomEnvelope envelope,
        bool useLinkHealthRouting = true,
        bool treatSuccessfulSendAsAckWhenRequired = true,
        TimeSpan? sendTimeout = null,
        bool tryFallbacks = true,
        bool sendToAllForBroadcast = true,
        CancellationToken cancellationToken = default)
    {
        var route = useLinkHealthRouting
            ? RouteEnvelopeWithLinkHealth(envelope)
            : RouteEnvelope(envelope);

        var request = new GroundTransportSendRequest
        {
            Envelope = envelope,
            UseLinkHealthRouting = useLinkHealthRouting,
            TreatSuccessfulSendAsAckWhenRequired = treatSuccessfulSendAsAckWhenRequired,
            SendTimeout = sendTimeout ?? TimeSpan.FromSeconds(2),
            TryFallbacks = tryFallbacks,
            SendToAllForBroadcast = sendToAllForBroadcast,
            Reason = "GroundStationEngine SendEnvelopeAsync request."
        };

        return await TransportManager.SendAsync(
            request,
            route,
            cancellationToken);
    }

    /// <summary>
    /// FleetCommand üretir, CommandTracker'a kaydeder, envelope'a sarar,
    /// transport manager üzerinden gönderir ve ACK correlation kaydı açar.
    /// 
    /// treatSuccessfulSendAsAckWhenRequired false verilirse gerçek ACK/result gelene kadar
    /// execution yalnızca Sent olarak kalabilir. FleetCommandResult geldiğinde HandleCommandResult
    /// üzerinden gerçek ACK correlation yapılır.
    /// </summary>
    public async Task<RouteExecutionRecord?> SendTrackedCommandAsync(
        FleetCommand command,
        bool useLinkHealthRouting = true,
        bool treatSuccessfulSendAsAckWhenRequired = true,
        TimeSpan? sendTimeout = null,
        bool tryFallbacks = true,
        CancellationToken cancellationToken = default)
    {
        var envelope = CreateTrackedCommandEnvelope(command);

        if (envelope is null)
            return null;

        var execution = await SendEnvelopeAsync(
            envelope,
            useLinkHealthRouting,
            treatSuccessfulSendAsAckWhenRequired,
            sendTimeout,
            tryFallbacks,
            sendToAllForBroadcast: true,
            cancellationToken);

        TrackCommandAckCorrelation(
            command,
            envelope,
            execution);

        return execution;
    }

    /// <summary>
    /// Görev isteğini koordine eder, command envelope üretir, command'ı takip eder,
    /// transport manager ile gönderir ve ACK correlation kaydı açar.
    /// </summary>
    public async Task<RouteExecutionRecord?> CoordinateMissionAndSendAsync(
        MissionRequest request,
        bool useLinkHealthRouting = true,
        bool treatSuccessfulSendAsAckWhenRequired = true,
        TimeSpan? sendTimeout = null,
        bool tryFallbacks = true,
        CancellationToken cancellationToken = default)
    {
        var coordination = CoordinateMission(request);

        if (!coordination.Success || coordination.Envelope is null || coordination.Command is null)
            return null;

        var execution = await SendEnvelopeAsync(
            coordination.Envelope,
            useLinkHealthRouting,
            treatSuccessfulSendAsAckWhenRequired,
            sendTimeout,
            tryFallbacks,
            sendToAllForBroadcast: true,
            cancellationToken);

        TrackCommandAckCorrelation(
            coordination.Command,
            coordination.Envelope,
            execution);

        return execution;
    }

    /// <summary>
    /// Verilen envelope için route sonucu üretir ve route execution kaydı başlatır.
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
    /// Tüm command ACK correlation kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetCommandAckCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetSnapshot();
    }

    /// <summary>
    /// ACK almış command correlation kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetAckedCommandCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetAckedSnapshot();
    }

    /// <summary>
    /// Henüz gerçek ACK/result almamış command correlation kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetPendingCommandCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetPendingSnapshot();
    }

    /// <summary>
    /// Başarısız command correlation kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetFailedCommandCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetFailedSnapshot();
    }

    /// <summary>
    /// Belirli süreden uzun süredir gerçek ACK/result almamış command correlation sayısını döndürür.
    /// </summary>
    public int CountExpiredPendingCommandCorrelations(
        TimeSpan timeout,
        DateTimeOffset? nowUtc = null)
    {
        return CommandAckCorrelator.CountExpiredPending(timeout, nowUtc);
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
            TransportExecutionTracker.GetSnapshot(),
            CommandAckCorrelator.GetSnapshot());
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
    /// 
    /// Bu metot artık sadece CommandTracker'a result uygulamaz.
    /// Aynı zamanda CommandAckCorrelator üzerinden gerçek ACK/result eşleştirmesi yapar
    /// ve ilgili RouteExecutionRecord kaydını Acked veya Failed olarak günceller.
    /// </summary>
    private bool HandleCommandResult(FleetCommandResult result)
    {
        if (result is null || !result.IsValid)
            return false;

        var appliedToCommandTracker = CommandTracker.ApplyResult(result);

        if (!appliedToCommandTracker)
            return false;

        ApplyCommandAckCorrelation(result);

        return true;
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

    /// <summary>
    /// Gönderilen FleetCommand ile oluşan route execution kaydı arasında ACK correlation kaydı açar.
    /// </summary>
    private CommandAckCorrelationRecord? TrackCommandAckCorrelation(
        FleetCommand command,
        HydronomEnvelope envelope,
        RouteExecutionRecord execution)
    {
        var selectedTransport =
            execution.SendResults.FirstOrDefault(x => x.Success || x.HasAck)?.TransportKind ??
            execution.CandidateTransports.FirstOrDefault();

        return CommandAckCorrelator.Track(
            command,
            envelope,
            execution,
            selectedTransport);
    }

    /// <summary>
    /// Gelen FleetCommandResult mesajını daha önce track edilen command execution kaydı ile eşleştirir.
    /// </summary>
    private void ApplyCommandAckCorrelation(FleetCommandResult result)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        var correlation = CommandAckCorrelator.ApplyResult(
            result,
            nowUtc);

        if (correlation is null)
            return;

        if (correlation.IsFailed)
        {
            TransportExecutionTracker.RecordFailure(
                correlation.ExecutionId,
                correlation.TransportKind,
                TransportSendStatus.Failed,
                correlation.CreatedUtc,
                correlation.LastResultUtc ?? nowUtc,
                $"Command result correlated as failure: {result.Status}",
                result.Message);

            return;
        }

        TransportExecutionTracker.RecordAcked(
            correlation.ExecutionId,
            correlation.TransportKind,
            correlation.CreatedUtc,
            correlation.LastResultUtc ?? nowUtc,
            correlation.LastResultLatencyMs,
            $"Command result correlated as ACK/result: {result.Status}");
    }
}