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
using Hydronom.GroundStation.Security;
using Hydronom.GroundStation.Telemetry;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.Transports;
using Hydronom.GroundStation.Transports.Receive;
using Hydronom.GroundStation.WorldModel;
using FleetRegistryStore = Hydronom.GroundStation.FleetRegistry.FleetRegistry;

/// <summary>
/// Hydronom Ground Station tarafÄ±nÄ±n ana giriÅŸ sÄ±nÄ±fÄ±dÄ±r.
/// 
/// Bu sÄ±nÄ±f, yer istasyonunun ana koordinasyon kabuÄŸudur.
/// AmacÄ±:
/// - FleetRegistry'yi tek merkezden yÃ¶netmek,
/// - CommandTracker ile gÃ¶nderilen komutlarÄ± ve sonuÃ§larÄ±nÄ± takip etmek,
/// - CommandAckCorrelator ile gÃ¶nderilen command ile gerÃ§ek FleetCommandResult cevabÄ±nÄ± eÅŸleÅŸtirmek,
/// - GroundWorldModel ile ortak operasyon dÃ¼nyasÄ±nÄ± tutmak,
/// - MissionAllocator ile gÃ¶rev iÃ§in uygun araÃ§ seÃ§imini baÅŸlatmak,
/// - FleetCoordinator ile gÃ¶rev isteÄŸinden komut Ã¼retmek,
/// - GroundCommandSafetyGate ile gÃ¶nderilecek komutlarÄ± security/authority/safety Ã¶n filtresinden geÃ§irmek,
/// - CommunicationRouter ile gÃ¶nderilecek mesajlarÄ±n route kararÄ±nÄ± Ã¼retmek,
/// - TelemetryRoutePlanner ile route sonucuna gÃ¶re telemetry yoÄŸunluÄŸu planlamak,
/// - LinkHealthTracker ile araÃ§/transport bazlÄ± baÄŸlantÄ± saÄŸlÄ±ÄŸÄ±nÄ± takip etmek,
/// - GroundTransportExecutionTracker ile route/gÃ¶nderim sonuÃ§larÄ±nÄ± takip etmek,
/// - GroundTransportManager ile route kararÄ±nÄ± gerÃ§ek ITransport.SendAsync zincirine baÄŸlamak,
/// - GroundTransportReceiver ile transportlardan gelen envelope'larÄ± otomatik dinlemek,
/// - GroundDiagnosticsEngine ile tek Ã§aÄŸrÄ±da operasyon snapshot'Ä± Ã¼retmek,
/// - Gelen HydronomEnvelope mesajlarÄ±nÄ± dispatcher Ã¼zerinden yorumlamak,
/// - Yer istasyonu tarafÄ±nda bÃ¼yÃ¼yecek modÃ¼ller iÃ§in ana koordinasyon noktasÄ± olmaktÄ±r.
/// </summary>
public sealed class GroundStationEngine
{
    /// <summary>
    /// Yer istasyonunun araÃ§/node kayÄ±t defteri.
    /// </summary>
    public FleetRegistryStore FleetRegistry { get; } = new();

    /// <summary>
    /// Yer istasyonu tarafÄ±ndan gÃ¶nderilen komutlarÄ± ve araÃ§lardan dÃ¶nen sonuÃ§larÄ± takip eder.
    /// </summary>
    public CommandTracker CommandTracker { get; } = new();

    /// <summary>
    /// Yer istasyonu tarafÄ±ndan gÃ¶nderilen komutlar ile araÃ§tan gelen gerÃ§ek FleetCommandResult cevaplarÄ±nÄ± eÅŸleÅŸtirir.
    /// 
    /// Bu yapÄ±, SendAsync baÅŸarÄ±lÄ± oldu diye ACK varsaymak yerine,
    /// gerÃ§ek command result geldiÄŸinde ilgili route execution kaydÄ±nÄ± gÃ¼nceller.
    /// </summary>
    public CommandAckCorrelator CommandAckCorrelator { get; } = new();

    /// <summary>
    /// Yer istasyonunun ortak dÃ¼nya modelidir.
    /// </summary>
    public GroundWorldModel WorldModel { get; } = new();

    /// <summary>
    /// GÃ¶rev isteklerini filo iÃ§indeki uygun araca atamaya Ã§alÄ±ÅŸan ilk gÃ¶rev daÄŸÄ±tÄ±m modÃ¼lÃ¼dÃ¼r.
    /// </summary>
    public MissionAllocator MissionAllocator { get; } = new();

    /// <summary>
    /// GÃ¶rev isteÄŸini alÄ±p uygun aracÄ± seÃ§en ve araca gÃ¶nderilecek FleetCommand envelope Ã¼reten koordinasyon modÃ¼lÃ¼dÃ¼r.
    /// </summary>
    public FleetCoordinator FleetCoordinator { get; }

    /// <summary>
    /// Ground Station seviyesinde komutun yapÄ±sal, yetki ve hedef araÃ§ baÄŸlamÄ±na gÃ¶re gÃ¶nderilebilirliÄŸini kontrol eder.
    /// 
    /// Bu gate araÃ§ Ã¼zerindeki local SafetyGate'in yerine geÃ§mez.
    /// Sadece yer istasyonu tarafÄ±nda Ã¶n gÃ¼venlik filtresi saÄŸlar.
    /// </summary>
    public GroundCommandSafetyGate CommandSafetyGate { get; }

    /// <summary>
    /// En son deÄŸerlendirilen komutun safety/security sonucu.
    /// 
    /// Smoke test, diagnostics veya ileride Hydronom Ops tarafÄ±nda son reddetme sebebini gÃ¶stermek iÃ§in kullanÄ±labilir.
    /// </summary>
    public CommandValidationResult? LastCommandSafetyResult { get; private set; }
    /// <summary>
    /// En son yapÄ±lan mission allocation / gÃ¶rev atama sonucu.
    /// 
    /// Hydronom Ops, diagnostics ve Gateway tarafÄ±nda son gÃ¶rev atama kararÄ±nÄ±n
    /// neden baÅŸarÄ±lÄ± veya baÅŸarÄ±sÄ±z olduÄŸunu gÃ¶stermek iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public MissionAllocationResult? LastMissionAllocationResult { get; private set; }

    /// <summary>
    /// GÃ¶nderilecek HydronomEnvelope mesajlarÄ± iÃ§in route sonucu Ã¼reten iletiÅŸim yÃ¶nlendiricisidir.
    /// </summary>
    public CommunicationRouter CommunicationRouter { get; } = new();

    /// <summary>
    /// CommunicationRouter tarafÄ±ndan Ã¼retilen route sonucuna gÃ¶re telemetry profil planÄ± Ã¼retir.
    /// </summary>
    public TelemetryRoutePlanner TelemetryRoutePlanner { get; } = new();

    /// <summary>
    /// Yer istasyonu seviyesinde araÃ§larÄ±n haberleÅŸme baÄŸlantÄ± kalitesini takip eder.
    /// </summary>
    public LinkHealthTracker LinkHealthTracker { get; } = new();

    /// <summary>
    /// Route execution / transport gÃ¶nderim sonucu takip motorudur.
    /// </summary>
    public GroundTransportExecutionTracker TransportExecutionTracker { get; }

    /// <summary>
    /// Ground Station tarafÄ±ndaki gerÃ§ek/mock transport instance'larÄ±nÄ± tutan registry.
    /// </summary>
    public GroundTransportRegistry TransportRegistry { get; } = new();

    /// <summary>
    /// Route kararÄ±nÄ± gerÃ§ek ITransport.SendAsync zincirine baÄŸlayan transport manager.
    /// </summary>
    public GroundTransportManager TransportManager { get; }

    /// <summary>
    /// Transportlardan gelen HydronomEnvelope mesajlarÄ±nÄ± dinleyen receive pipeline'dÄ±r.
    /// </summary>
    public GroundTransportReceiver TransportReceiver { get; }

    /// <summary>
    /// Ground Station'Ä±n genel durumunu tek bir operasyon snapshot'Ä±na dÃ¶nÃ¼ÅŸtÃ¼ren diagnostics motorudur.
    /// </summary>
    public GroundDiagnosticsEngine DiagnosticsEngine { get; } = new();

    /// <summary>
    /// Ground Station tarafÄ±nda gelen mesajlarÄ± MessageType deÄŸerine gÃ¶re ilgili handler'a yÃ¶nlendiren dispatcher.
    /// </summary>
    public GroundMessageDispatcher Dispatcher { get; }

    /// <summary>
    /// Yer istasyonunun kendi node kimliÄŸi.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new()
    {
        NodeId = "GROUND-001",
        DisplayName = "Hydronom Ground Station",
        NodeType = "GroundStation",
        Role = "Coordinator"
    };

    /// <summary>
    /// GroundStationEngine oluÅŸturur.
    /// </summary>
    public GroundStationEngine()
    {
        FleetCoordinator = new FleetCoordinator(MissionAllocator);
        CommandSafetyGate = new GroundCommandSafetyGate();

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
    /// Gelen HydronomEnvelope mesajÄ±nÄ± iÅŸler.
    /// </summary>
    public bool HandleEnvelope(HydronomEnvelope envelope)
    {
        return Dispatcher.Dispatch(envelope);
    }

    /// <summary>
    /// Komutu Ground Station seviyesinde security/authority/safety Ã¶n filtresinden geÃ§irir.
    /// 
    /// Bu kontrol araÃ§ Ã¼zerindeki local SafetyGate'in yerine geÃ§mez.
    /// AraÃ§ runtime tarafÄ± yine kendi local safety kararÄ±nÄ± vermeye devam etmelidir.
    /// </summary>
    public CommandValidationResult EvaluateCommandSafety(
        FleetCommand? command,
        DateTimeOffset? nowUtc = null)
    {
        LastCommandSafetyResult = CommandSafetyGate.Evaluate(
            command,
            FleetRegistry.GetSnapshot(),
            nowUtc);

        return LastCommandSafetyResult;
    }

    /// <summary>
    /// Yer istasyonu tarafÄ±ndan Ã¼retilecek/gÃ¶nderilecek bir komutu safety/security kontrolÃ¼nden geÃ§irir,
    /// kayÄ±t altÄ±na alÄ±r ve aynÄ± komutu HydronomEnvelope iÃ§ine sararak dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public HydronomEnvelope? CreateTrackedCommandEnvelope(FleetCommand command)
    {
        if (command is null || !command.IsValid)
            return null;

        var safetyResult = EvaluateCommandSafety(command);

        if (!safetyResult.IsAllowed)
            return null;

        var tracked = CommandTracker.TrackCommand(command);

        if (!tracked)
            return null;

        return HydronomEnvelopeFactory.CreateCommand(command);
    }

    /// <summary>
    /// Verilen gÃ¶rev isteÄŸi iÃ§in mevcut fleet snapshot Ã¼zerinden en uygun aracÄ± seÃ§er.
    /// </summary>
    /// <summary>
    /// Verilen gÃ¶rev isteÄŸi iÃ§in mevcut fleet snapshot Ã¼zerinden en uygun aracÄ± seÃ§er.
    /// 
    /// Son allocation sonucu LastMissionAllocationResult iÃ§inde saklanÄ±r.
    /// </summary>
    public MissionAllocationResult AllocateMission(MissionRequest request)
    {
        LastMissionAllocationResult = MissionAllocator.Allocate(
            request,
            FleetRegistry.GetSnapshot());

        return LastMissionAllocationResult;
    }

    /// <summary>
    /// Verilen gÃ¶rev isteÄŸini filo koordinasyon sonucuna Ã§evirir.
    /// </summary>
    public FleetCoordinationResult CoordinateMission(MissionRequest request)
    {
        var coordination = FleetCoordinator.CoordinateMission(
            request,
            FleetRegistry.GetSnapshot(),
            Identity.NodeId,
            isOperatorIssued: true);

        LastMissionAllocationResult = coordination.Allocation;

        if (!coordination.Success || coordination.Command is null)
            return coordination;

        var trackedEnvelope = CreateTrackedCommandEnvelope(coordination.Command);

        if (trackedEnvelope is null)
        {
            var safetyReason = LastCommandSafetyResult is not null && LastCommandSafetyResult.IsRejected
                ? LastCommandSafetyResult.Reason
                : "Mission command was generated but could not be tracked by CommandTracker.";

            return FleetCoordinationResult.Failed(
                request,
                coordination.Allocation,
                safetyReason);
        }

        return coordination with
        {
            Envelope = trackedEnvelope,
            Reason = $"{coordination.Reason} Command accepted by GroundCommandSafetyGate and tracked by GroundStationEngine."
        };
    }

    /// <summary>
    /// Verilen envelope iÃ§in mevcut fleet snapshot Ã¼zerinden route sonucu Ã¼retir.
    /// </summary>
    public CommunicationRouteResult RouteEnvelope(HydronomEnvelope envelope)
    {
        return CommunicationRouter.Route(
            envelope,
            FleetRegistry.GetSnapshot());
    }

    /// <summary>
    /// Verilen envelope iÃ§in mevcut fleet snapshot ve LinkHealthTracker Ã¼zerinden link-aware route sonucu Ã¼retir.
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
    /// Transport registry iÃ§ine yeni transport ekler.
    /// </summary>
    public bool RegisterTransport(ITransport transport)
    {
        return TransportRegistry.Add(transport);
    }

    /// <summary>
    /// Transport registry iÃ§inden transport kaldÄ±rÄ±r.
    /// </summary>
    public bool RemoveTransportByName(string name)
    {
        return TransportRegistry.RemoveByName(name);
    }

    /// <summary>
    /// KayÄ±tlÄ± tÃ¼m transport baÄŸlantÄ±larÄ±nÄ± baÅŸlatÄ±r.
    /// </summary>
    public Task ConnectAllTransportsAsync(CancellationToken cancellationToken = default)
    {
        return TransportRegistry.ConnectAllAsync(cancellationToken);
    }

    /// <summary>
    /// KayÄ±tlÄ± tÃ¼m transport baÄŸlantÄ±larÄ±nÄ± kapatÄ±r.
    /// </summary>
    public Task DisconnectAllTransportsAsync(CancellationToken cancellationToken = default)
    {
        return TransportRegistry.DisconnectAllAsync(cancellationToken);
    }

    /// <summary>
    /// KayÄ±tlÄ± ve baÄŸlÄ± tÃ¼m transport'lar iÃ§in receive pipeline Ã§alÄ±ÅŸtÄ±rÄ±r.
    /// </summary>
    public Task RunTransportReceiversAsync(CancellationToken cancellationToken = default)
    {
        return TransportReceiver.RunAllAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir transport iÃ§in receive pipeline Ã§alÄ±ÅŸtÄ±rÄ±r.
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
    /// Transport receive event geÃ§miÅŸinin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundTransportReceiveEvent> GetTransportReceiveSnapshot()
    {
        return TransportReceiver.GetSnapshot();
    }

    /// <summary>
    /// KayÄ±tlÄ± receive event sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public int TransportReceiveEventCount => TransportReceiver.EventCount;

    /// <summary>
    /// Verilen envelope iÃ§in route Ã¼retir ve transport manager Ã¼zerinden gerÃ§ek/mock SendAsync zincirini Ã§alÄ±ÅŸtÄ±rÄ±r.
    /// 
    /// Bu metot genel envelope gÃ¶nderimi iÃ§indir.
    /// FleetCommand correlation gerekiyorsa SendTrackedCommandAsync kullanÄ±lmalÄ±dÄ±r.
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
    /// FleetCommand Ã¼retir, GroundCommandSafetyGate kontrolÃ¼nden geÃ§irir, CommandTracker'a kaydeder,
    /// envelope'a sarar, transport manager Ã¼zerinden gÃ¶nderir ve ACK correlation kaydÄ± aÃ§ar.
    /// 
    /// treatSuccessfulSendAsAckWhenRequired false verilirse gerÃ§ek ACK/result gelene kadar
    /// execution yalnÄ±zca Sent olarak kalabilir. FleetCommandResult geldiÄŸinde HandleCommandResult
    /// Ã¼zerinden gerÃ§ek ACK correlation yapÄ±lÄ±r.
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
    /// GÃ¶rev isteÄŸini koordine eder, command envelope Ã¼retir, command'Ä± safety/security filtresinden geÃ§irir,
    /// command'Ä± takip eder, transport manager ile gÃ¶nderir ve ACK correlation kaydÄ± aÃ§ar.
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
    /// Verilen envelope iÃ§in route sonucu Ã¼retir ve route execution kaydÄ± baÅŸlatÄ±r.
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
    /// Verilen envelope iÃ§in link health destekli route sonucu Ã¼retir ve route execution kaydÄ± baÅŸlatÄ±r.
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
    /// Verilen route sonucu ve envelope ile manuel execution kaydÄ± baÅŸlatÄ±r.
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
    /// Belirli execution iÃ§in transport gÃ¶nderim denemesi baÅŸladÄ±ÄŸÄ±nÄ± kaydeder.
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
    /// Belirli execution iÃ§in baÅŸarÄ±lÄ± gÃ¶nderim sonucunu kaydeder.
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
    /// Belirli execution iÃ§in ACK alÄ±nmÄ±ÅŸ gÃ¶nderim sonucunu kaydeder.
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
    /// Belirli execution iÃ§in timeout sonucunu kaydeder.
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
    /// Belirli execution iÃ§in baÅŸarÄ±sÄ±z gÃ¶nderim sonucunu kaydeder.
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
    /// TÃ¼m route execution kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetRouteExecutionSnapshot()
    {
        return TransportExecutionTracker.GetSnapshot();
    }

    /// <summary>
    /// Pending route execution kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetPendingRouteExecutionSnapshot()
    {
        return TransportExecutionTracker.GetPendingSnapshot();
    }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z route execution kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetFailedRouteExecutionSnapshot()
    {
        return TransportExecutionTracker.GetFailedSnapshot();
    }

    /// <summary>
    /// TÃ¼m command ACK correlation kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetCommandAckCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetSnapshot();
    }

    /// <summary>
    /// ACK almÄ±ÅŸ command correlation kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetAckedCommandCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetAckedSnapshot();
    }

    /// <summary>
    /// HenÃ¼z gerÃ§ek ACK/result almamÄ±ÅŸ command correlation kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetPendingCommandCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetPendingSnapshot();
    }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z command correlation kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetFailedCommandCorrelationSnapshot()
    {
        return CommandAckCorrelator.GetFailedSnapshot();
    }

    /// <summary>
    /// Belirli sÃ¼reden uzun sÃ¼redir gerÃ§ek ACK/result almamÄ±ÅŸ command correlation sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public int CountExpiredPendingCommandCorrelations(
        TimeSpan timeout,
        DateTimeOffset? nowUtc = null)
    {
        return CommandAckCorrelator.CountExpiredPending(timeout, nowUtc);
    }

    /// <summary>
    /// Verilen route sonucuna gÃ¶re telemetry profil planÄ± Ã¼retir.
    /// </summary>
    public TelemetryRoutePlan PlanTelemetryForRoute(CommunicationRouteResult route)
    {
        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen envelope iÃ§in Ã¶nce route sonucu, sonra telemetry route planÄ± Ã¼retir.
    /// </summary>
    public TelemetryRoutePlan PlanTelemetryForEnvelope(HydronomEnvelope envelope)
    {
        var route = RouteEnvelope(envelope);
        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen envelope iÃ§in link health destekli route sonucu, sonra telemetry route planÄ± Ã¼retir.
    /// </summary>
    public TelemetryRoutePlan PlanTelemetryForEnvelopeWithLinkHealth(HydronomEnvelope envelope)
    {
        var route = RouteEnvelopeWithLinkHealth(envelope);
        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen gÃ¶rev isteÄŸini koordine eder ve ortaya Ã§Ä±kan envelope iÃ§in route sonucu Ã¼retir.
    /// </summary>
    public CommunicationRouteResult? CoordinateMissionAndRoute(MissionRequest request)
    {
        var coordination = CoordinateMission(request);

        if (!coordination.Success || coordination.Envelope is null)
            return null;

        return RouteEnvelope(coordination.Envelope);
    }

    /// <summary>
    /// Verilen gÃ¶rev isteÄŸini koordine eder ve ortaya Ã§Ä±kan envelope iÃ§in link health destekli route sonucu Ã¼retir.
    /// </summary>
    public CommunicationRouteResult? CoordinateMissionAndRouteWithLinkHealth(MissionRequest request)
    {
        var coordination = CoordinateMission(request);

        if (!coordination.Success || coordination.Envelope is null)
            return null;

        return RouteEnvelopeWithLinkHealth(coordination.Envelope);
    }

    /// <summary>
    /// Verilen gÃ¶rev isteÄŸini koordine eder, route eder ve telemetry planÄ±nÄ± Ã¼retir.
    /// </summary>
    public TelemetryRoutePlan? CoordinateMissionRouteAndPlanTelemetry(MissionRequest request)
    {
        var route = CoordinateMissionAndRoute(request);

        if (route is null)
            return null;

        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Verilen gÃ¶rev isteÄŸini koordine eder, link health destekli route eder ve telemetry planÄ±nÄ± Ã¼retir.
    /// </summary>
    public TelemetryRoutePlan? CoordinateMissionRouteAndPlanTelemetryWithLinkHealth(MissionRequest request)
    {
        var route = CoordinateMissionAndRouteWithLinkHealth(request);

        if (route is null)
            return null;

        return TelemetryRoutePlanner.Plan(route);
    }

    /// <summary>
    /// Ground Station'Ä±n mevcut operasyon durumundan tek bakÄ±ÅŸlÄ±k snapshot Ã¼retir.
    /// </summary>
    public GroundOperationSnapshot CreateOperationSnapshot()
    {
        return DiagnosticsEngine.CreateSnapshot(
            FleetRegistry.GetSnapshot(),
            CommandTracker.GetSnapshot(),
            WorldModel,
            LinkHealthTracker.GetSnapshot(DateTime.UtcNow),
            TransportExecutionTracker.GetSnapshot(),
            CommandAckCorrelator.GetSnapshot(),
            TransportReceiver.GetSnapshot(),
            LastCommandSafetyResult,
            LastMissionAllocationResult);
    }

    /// <summary>
    /// Belirli bir aracÄ±n belirli bir transport Ã¼zerinden gÃ¶rÃ¼ldÃ¼ÄŸÃ¼nÃ¼ LinkHealthTracker'a bildirir.
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
    /// Bir araca belirli transport Ã¼zerinden mesaj gÃ¶nderme denemesini kayÄ±t altÄ±na alÄ±r.
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
    /// Bir route/gÃ¶nderim denemesinin baÅŸarÄ±lÄ± olduÄŸunu baÄŸlantÄ± saÄŸlÄ±k metriÄŸine iÅŸler.
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
    /// Bir route/gÃ¶nderim denemesinin baÅŸarÄ±sÄ±z olduÄŸunu baÄŸlantÄ± saÄŸlÄ±k metriÄŸine iÅŸler.
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
    /// Belirli bir transport Ã¼zerinden ACK alÄ±ndÄ±ÄŸÄ±nÄ± baÄŸlantÄ± saÄŸlÄ±k metriÄŸine iÅŸler.
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
    /// Belirli bir transport Ã¼zerinden timeout yaÅŸandÄ±ÄŸÄ±nÄ± baÄŸlantÄ± saÄŸlÄ±k metriÄŸine iÅŸler.
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
    /// Tahmini paket kaybÄ± bilgisini baÄŸlantÄ± saÄŸlÄ±k metriÄŸine iÅŸler.
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
    /// TÃ¼m araÃ§larÄ±n baÄŸlantÄ± saÄŸlÄ±k snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<VehicleLinkHealthSnapshot> GetLinkHealthSnapshot(DateTime? nowUtc = null)
    {
        return LinkHealthTracker.GetSnapshot(nowUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Belirli bir araÃ§ iÃ§in en iyi kullanÄ±labilir baÄŸlantÄ±yÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public TransportLinkMetrics? GetBestAvailableLink(string vehicleId)
    {
        return LinkHealthTracker.GetBestAvailableLink(vehicleId);
    }

    /// <summary>
    /// Belirli bir araÃ§ iÃ§in kullanÄ±labilir baÄŸlantÄ± listesini kalite skoruna gÃ¶re dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<TransportLinkMetrics> GetAvailableLinks(string vehicleId)
    {
        return LinkHealthTracker.GetAvailableLinks(vehicleId);
    }

    /// <summary>
    /// GroundWorldModel iÃ§ine yeni bir dÃ¼nya nesnesi ekler veya mevcut nesneyi gÃ¼nceller.
    /// </summary>
    public bool UpsertWorldObject(GroundWorldObject worldObject)
    {
        return WorldModel.Upsert(worldObject);
    }

    /// <summary>
    /// GroundWorldModel iÃ§indeki tÃ¼m dÃ¼nya nesnelerinin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetWorldSnapshot()
    {
        return WorldModel.GetSnapshot();
    }

    /// <summary>
    /// GroundWorldModel iÃ§indeki aktif dÃ¼nya nesnelerinin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveWorldSnapshot()
    {
        return WorldModel.GetActiveSnapshot();
    }

    /// <summary>
    /// Aktif engellerin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveObstacles()
    {
        return WorldModel.GetActiveObstacles();
    }

    /// <summary>
    /// Aktif hedeflerin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveTargets()
    {
        return WorldModel.GetActiveTargets();
    }

    /// <summary>
    /// FleetCommandResult mesajlarÄ±nÄ± iÅŸler.
    /// 
    /// Bu metot artÄ±k sadece CommandTracker'a result uygulamaz.
    /// AynÄ± zamanda CommandAckCorrelator Ã¼zerinden gerÃ§ek ACK/result eÅŸleÅŸtirmesi yapar
    /// ve ilgili RouteExecutionRecord kaydÄ±nÄ± Acked veya Failed olarak gÃ¼nceller.
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
    /// Registry iÃ§indeki tÃ¼m araÃ§/node durumlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetFleetSnapshot()
    {
        return FleetRegistry.GetSnapshot();
    }

    /// <summary>
    /// Online kabul edilen araÃ§/node durumlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetOnlineFleetSnapshot()
    {
        return FleetRegistry.GetOnlineNodes();
    }

    /// <summary>
    /// KayÄ±tlÄ± tÃ¼m komut geÃ§miÅŸinin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetCommandHistorySnapshot()
    {
        return CommandTracker.GetSnapshot();
    }

    /// <summary>
    /// HenÃ¼z sonuÃ§ bekleyen komutlarÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetPendingCommandSnapshot()
    {
        return CommandTracker.GetPendingCommands();
    }

    /// <summary>
    /// Belirli sÃ¼re heartbeat gÃ¶ndermeyen araÃ§larÄ± offline olarak iÅŸaretler.
    /// </summary>
    public int MarkStaleNodesOffline(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        return FleetRegistry.MarkStaleNodesOffline(timeout, nowUtc);
    }

    /// <summary>
    /// Belirli sÃ¼re boyunca sonuÃ§ dÃ¶nmeyen pending komutlarÄ± expired olarak iÅŸaretler.
    /// </summary>
    public int MarkExpiredCommands(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        return CommandTracker.MarkExpiredCommands(timeout, nowUtc);
    }

    /// <summary>
    /// Belirli sÃ¼reden uzun sÃ¼redir gÃ¼ncellenmeyen aktif dÃ¼nya nesnelerini pasif hale getirir.
    /// </summary>
    public int DeactivateStaleWorldObjects(TimeSpan maxAge, DateTimeOffset? nowUtc = null)
    {
        return WorldModel.DeactivateStaleObjects(maxAge, nowUtc);
    }

    /// <summary>
    /// GÃ¶nderilen FleetCommand ile oluÅŸan route execution kaydÄ± arasÄ±nda ACK correlation kaydÄ± aÃ§ar.
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
    /// Gelen FleetCommandResult mesajÄ±nÄ± daha Ã¶nce track edilen command execution kaydÄ± ile eÅŸleÅŸtirir.
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
