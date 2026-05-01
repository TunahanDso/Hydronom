namespace Hydronom.GroundStation.Diagnostics;

using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Ack;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.Transports.Receive;
using Hydronom.GroundStation.WorldModel;

/// <summary>
/// Ground Station tarafındaki farklı modüllerden gelen bilgileri okuyup
/// tek bir operasyon snapshot'ına dönüştüren diagnostics motorudur.
/// 
/// Bu sınıfın amacı:
/// - FleetRegistry snapshot'ını yorumlamak,
/// - CommandTracker geçmişini yorumlamak,
/// - GroundWorldModel durumunu yorumlamak,
/// - LinkHealthTracker bağlantı sağlığını yorumlamak,
/// - GroundTransportExecutionTracker route/gönderim durumunu yorumlamak,
/// - CommandAckCorrelator gerçek ACK/result korelasyon durumunu yorumlamak,
/// - GroundTransportReceiver inbound/gelen mesaj durumunu yorumlamak,
/// - genel health ve kısa açıklama üretmektir.
/// 
/// Böylece Hydronom Ops veya ilerideki Gateway katmanı tek çağrıyla
/// yer istasyonunun genel durumunu okuyabilir.
/// </summary>
public sealed class GroundDiagnosticsEngine
{
    /// <summary>
    /// Filo, komut, dünya modeli, bağlantı sağlığı, route execution, ACK correlation
    /// ve receive event verilerinden operasyon snapshot'ı üretir.
    /// 
    /// linkHealthSnapshot, routeExecutionSnapshot, ackCorrelationSnapshot ve receiveEventSnapshot opsiyoneldir.
    /// Böylece eski çağrılar bozulmadan çalışmaya devam eder.
    /// </summary>
    public GroundOperationSnapshot CreateSnapshot(
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        IReadOnlyList<CommandRecord> commandSnapshot,
        GroundWorldModel worldModel,
        IReadOnlyList<VehicleLinkHealthSnapshot>? linkHealthSnapshot = null,
        IReadOnlyList<RouteExecutionSnapshot>? routeExecutionSnapshot = null,
        IReadOnlyList<CommandAckCorrelationSnapshot>? ackCorrelationSnapshot = null,
        IReadOnlyList<GroundTransportReceiveEvent>? receiveEventSnapshot = null)
    {
        fleetSnapshot ??= Array.Empty<VehicleNodeStatus>();
        commandSnapshot ??= Array.Empty<CommandRecord>();
        linkHealthSnapshot ??= Array.Empty<VehicleLinkHealthSnapshot>();
        routeExecutionSnapshot ??= Array.Empty<RouteExecutionSnapshot>();
        ackCorrelationSnapshot ??= Array.Empty<CommandAckCorrelationSnapshot>();
        receiveEventSnapshot ??= Array.Empty<GroundTransportReceiveEvent>();

        var totalNodes = fleetSnapshot.Count;
        var onlineNodes = fleetSnapshot.Count(x => x.IsOnline);
        var offlineNodes = totalNodes - onlineNodes;

        var healthyNodes = fleetSnapshot.Count(x =>
            string.Equals(x.Health, "OK", StringComparison.OrdinalIgnoreCase));

        var warningNodes = fleetSnapshot.Count(x =>
            string.Equals(x.Health, "Warning", StringComparison.OrdinalIgnoreCase));

        var criticalNodes = fleetSnapshot.Count(x =>
            string.Equals(x.Health, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Health, "Fault", StringComparison.OrdinalIgnoreCase));

        var batteries = fleetSnapshot
            .Where(x => x.BatteryPercent is not null)
            .Select(x => x.BatteryPercent!.Value)
            .ToArray();

        double? averageBattery = batteries.Length == 0
            ? null
            : Math.Round(batteries.Average(), 2);

        var totalCommands = commandSnapshot.Count;
        var pendingCommands = commandSnapshot.Count(x => x.IsPending);
        var completedCommands = commandSnapshot.Count(x => x.IsCompleted);
        var successfulCommands = commandSnapshot.Count(x => x.IsSuccessful);
        var failedCommands = commandSnapshot.Count(IsFailedCommand);

        var totalWorldObjects = worldModel?.Count ?? 0;
        var activeWorldObjects = worldModel?.ActiveCount ?? 0;
        var activeObstacles = worldModel?.GetActiveObstacles().Count ?? 0;
        var activeTargets = worldModel?.GetActiveTargets().Count ?? 0;
        var activeNoGoZones = worldModel?.GetActiveNoGoZones().Count ?? 0;

        var linkVehicles = linkHealthSnapshot.ToArray();

        var transportLinks = linkVehicles
            .SelectMany(x => x.Links ?? Array.Empty<LinkHealthSnapshot>())
            .ToArray();

        var linkVehicleCount = linkVehicles.Length;
        var totalLinks = transportLinks.Length;

        var goodLinks = transportLinks.Count(x => x.Status == LinkHealthStatus.Good);
        var degradedLinks = transportLinks.Count(x => x.Status == LinkHealthStatus.Degraded);
        var criticalLinks = transportLinks.Count(x => x.Status == LinkHealthStatus.Critical);
        var lostLinks = transportLinks.Count(x => x.Status == LinkHealthStatus.Lost);
        var unknownLinks = transportLinks.Count(x => x.Status == LinkHealthStatus.Unknown);

        double? averageVehicleLinkQualityScore = linkVehicles.Length == 0
            ? null
            : Math.Round(linkVehicles.Average(x => x.OverallQualityScore), 2);

        double? averageTransportLinkQualityScore = transportLinks.Length == 0
            ? null
            : Math.Round(transportLinks.Average(x => x.QualityScore), 2);

        double? worstVehicleLinkQualityScore = linkVehicles.Length == 0
            ? null
            : Math.Round(linkVehicles.Min(x => x.OverallQualityScore), 2);

        double? worstTransportLinkQualityScore = transportLinks.Length == 0
            ? null
            : Math.Round(transportLinks.Min(x => x.QualityScore), 2);

        var linkHealthSummary = BuildLinkHealthSummary(
            linkVehicleCount,
            totalLinks,
            goodLinks,
            degradedLinks,
            criticalLinks,
            lostLinks,
            averageTransportLinkQualityScore,
            worstTransportLinkQualityScore);

        var routeExecutions = routeExecutionSnapshot.ToArray();

        var totalRouteExecutions = routeExecutions.Length;
        var pendingRouteExecutions = routeExecutions.Count(x => !x.IsCompleted);
        var completedRouteExecutions = routeExecutions.Count(x => x.IsCompleted);
        var successfulRouteExecutions = routeExecutions.Count(x => x.HasSuccess);
        var ackedRouteExecutions = routeExecutions.Count(x => x.HasAck);
        var timeoutRouteExecutions = routeExecutions.Count(x => x.HasTimeout);
        var failedRouteExecutions = routeExecutions.Count(x => x.HasFailure);
        var routeUnavailableExecutions = routeExecutions.Count(x => !x.CanRoute);

        var transportSendResults = routeExecutions
            .SelectMany(x => x.SendResults ?? Array.Empty<TransportSendResult>())
            .ToArray();

        var totalTransportSendResults = transportSendResults.Length;
        var successfulTransportSendResults = transportSendResults.Count(x => x.Success);
        var ackedTransportSendResults = transportSendResults.Count(x => x.HasAck);
        var timeoutTransportSendResults = transportSendResults.Count(x => x.IsTimeout);
        var failedTransportSendResults = transportSendResults.Count(x => x.IsFailure);

        var routeLatencies = routeExecutions
            .Where(x => x.BestLatencyMs.HasValue)
            .Select(x => x.BestLatencyMs!.Value)
            .ToArray();

        double? averageRouteExecutionLatencyMs = routeLatencies.Length == 0
            ? null
            : Math.Round(routeLatencies.Average(), 2);

        double? bestRouteExecutionLatencyMs = routeLatencies.Length == 0
            ? null
            : Math.Round(routeLatencies.Min(), 2);

        double? worstRouteExecutionLatencyMs = routeLatencies.Length == 0
            ? null
            : Math.Round(routeLatencies.Max(), 2);

        var routeExecutionSummary = BuildRouteExecutionSummary(
            totalRouteExecutions,
            successfulRouteExecutions,
            ackedRouteExecutions,
            timeoutRouteExecutions,
            failedRouteExecutions,
            routeUnavailableExecutions,
            averageRouteExecutionLatencyMs,
            worstRouteExecutionLatencyMs);

        var ackCorrelations = ackCorrelationSnapshot.ToArray();

        var totalAckCorrelations = ackCorrelations.Length;
        var pendingAckCorrelations = ackCorrelations.Count(x => !x.IsAcked);
        var ackedCorrelations = ackCorrelations.Count(x => x.IsAcked);
        var completedAckCorrelations = ackCorrelations.Count(x => x.IsCompleted);
        var successfulAckCorrelations = ackCorrelations.Count(x => x.IsSuccessful);
        var failedAckCorrelations = ackCorrelations.Count(x => x.IsFailed);

        // İlk faz için varsayılan gerçek ACK timeout eşiği.
        // İleride bu değer config üzerinden alınabilir.
        var ackTimeout = TimeSpan.FromSeconds(5);
        var nowUtc = DateTimeOffset.UtcNow;

        var expiredPendingAckCorrelations = ackCorrelations.Count(x =>
            !x.IsAcked &&
            nowUtc - x.CreatedUtc >= ackTimeout);

        var ackLatencies = ackCorrelations
            .Where(x => x.AckLatencyMs.HasValue)
            .Select(x => x.AckLatencyMs!.Value)
            .ToArray();

        double? averageAckCorrelationLatencyMs = ackLatencies.Length == 0
            ? null
            : Math.Round(ackLatencies.Average(), 2);

        double? bestAckCorrelationLatencyMs = ackLatencies.Length == 0
            ? null
            : Math.Round(ackLatencies.Min(), 2);

        double? worstAckCorrelationLatencyMs = ackLatencies.Length == 0
            ? null
            : Math.Round(ackLatencies.Max(), 2);

        var ackCorrelationSummary = BuildAckCorrelationSummary(
            totalAckCorrelations,
            pendingAckCorrelations,
            ackedCorrelations,
            successfulAckCorrelations,
            failedAckCorrelations,
            expiredPendingAckCorrelations,
            averageAckCorrelationLatencyMs,
            worstAckCorrelationLatencyMs);

        var receiveEvents = receiveEventSnapshot
            .OrderByDescending(x => x.ReceivedUtc)
            .ToArray();

        var totalReceiveEvents = receiveEvents.Length;
        var handledReceiveEvents = receiveEvents.Count(x => x.Handled && !x.HasError);
        var failedReceiveEvents = receiveEvents.Count(x => x.HasError);
        var unhandledReceiveEvents = receiveEvents.Count(x => !x.Handled && !x.HasError);

        DateTimeOffset? lastReceiveUtc = receiveEvents.Length == 0
            ? null
            : receiveEvents.Max(x => x.ReceivedUtc);

        var inboundFleetHeartbeatCount = receiveEvents.Count(x =>
            IsMessageType(x, "FleetHeartbeat"));

        var inboundFleetCommandResultCount = receiveEvents.Count(x =>
            IsMessageType(x, "FleetCommandResult"));

        var inboundFleetCommandCount = receiveEvents.Count(x =>
            IsMessageType(x, "FleetCommand"));

        var inboundVehicleNodeStatusCount = receiveEvents.Count(x =>
            IsMessageType(x, "VehicleNodeStatus"));

        var inboundUnknownMessageCount = receiveEvents.Count(x =>
            x.Envelope is null ||
            string.IsNullOrWhiteSpace(x.Envelope.MessageType) ||
            IsMessageType(x, "Unknown"));

        var receiveHealthSummary = BuildReceiveHealthSummary(
            totalReceiveEvents,
            handledReceiveEvents,
            failedReceiveEvents,
            unhandledReceiveEvents,
            inboundFleetHeartbeatCount,
            inboundFleetCommandResultCount,
            inboundFleetCommandCount,
            inboundVehicleNodeStatusCount,
            inboundUnknownMessageCount,
            lastReceiveUtc);

        var overallHealth = EvaluateOverallHealth(
            totalNodes,
            onlineNodes,
            criticalNodes,
            warningNodes,
            pendingCommands,
            failedCommands,
            totalLinks,
            goodLinks,
            degradedLinks,
            criticalLinks,
            lostLinks,
            totalRouteExecutions,
            pendingRouteExecutions,
            timeoutRouteExecutions,
            failedRouteExecutions,
            routeUnavailableExecutions,
            totalAckCorrelations,
            pendingAckCorrelations,
            failedAckCorrelations,
            expiredPendingAckCorrelations,
            totalReceiveEvents,
            failedReceiveEvents,
            unhandledReceiveEvents);

        var summary = BuildSummary(
            overallHealth,
            totalNodes,
            onlineNodes,
            pendingCommands,
            failedCommands,
            activeObstacles,
            activeTargets,
            linkHealthSummary,
            routeExecutionSummary,
            ackCorrelationSummary,
            receiveHealthSummary);

        return new GroundOperationSnapshot
        {
            TotalNodeCount = totalNodes,
            OnlineNodeCount = onlineNodes,
            OfflineNodeCount = offlineNodes,
            HealthyNodeCount = healthyNodes,
            WarningNodeCount = warningNodes,
            CriticalNodeCount = criticalNodes,
            AverageBatteryPercent = averageBattery,

            TotalCommandCount = totalCommands,
            PendingCommandCount = pendingCommands,
            CompletedCommandCount = completedCommands,
            SuccessfulCommandCount = successfulCommands,
            FailedCommandCount = failedCommands,

            TotalWorldObjectCount = totalWorldObjects,
            ActiveWorldObjectCount = activeWorldObjects,
            ActiveObstacleCount = activeObstacles,
            ActiveTargetCount = activeTargets,
            ActiveNoGoZoneCount = activeNoGoZones,

            LinkVehicleCount = linkVehicleCount,
            TotalLinkCount = totalLinks,
            GoodLinkCount = goodLinks,
            DegradedLinkCount = degradedLinks,
            CriticalLinkCount = criticalLinks,
            LostLinkCount = lostLinks,
            UnknownLinkCount = unknownLinks,
            AverageVehicleLinkQualityScore = averageVehicleLinkQualityScore,
            AverageTransportLinkQualityScore = averageTransportLinkQualityScore,
            WorstVehicleLinkQualityScore = worstVehicleLinkQualityScore,
            WorstTransportLinkQualityScore = worstTransportLinkQualityScore,
            LinkHealthSummary = linkHealthSummary,
            LinkHealth = linkVehicles,

            TotalRouteExecutionCount = totalRouteExecutions,
            PendingRouteExecutionCount = pendingRouteExecutions,
            CompletedRouteExecutionCount = completedRouteExecutions,
            SuccessfulRouteExecutionCount = successfulRouteExecutions,
            AckedRouteExecutionCount = ackedRouteExecutions,
            TimeoutRouteExecutionCount = timeoutRouteExecutions,
            FailedRouteExecutionCount = failedRouteExecutions,
            RouteUnavailableExecutionCount = routeUnavailableExecutions,
            TotalTransportSendResultCount = totalTransportSendResults,
            SuccessfulTransportSendResultCount = successfulTransportSendResults,
            AckedTransportSendResultCount = ackedTransportSendResults,
            TimeoutTransportSendResultCount = timeoutTransportSendResults,
            FailedTransportSendResultCount = failedTransportSendResults,
            AverageRouteExecutionLatencyMs = averageRouteExecutionLatencyMs,
            BestRouteExecutionLatencyMs = bestRouteExecutionLatencyMs,
            WorstRouteExecutionLatencyMs = worstRouteExecutionLatencyMs,
            RouteExecutionSummary = routeExecutionSummary,
            RouteExecutions = routeExecutions,

            TotalAckCorrelationCount = totalAckCorrelations,
            PendingAckCorrelationCount = pendingAckCorrelations,
            AckedCorrelationCount = ackedCorrelations,
            CompletedAckCorrelationCount = completedAckCorrelations,
            SuccessfulAckCorrelationCount = successfulAckCorrelations,
            FailedAckCorrelationCount = failedAckCorrelations,
            ExpiredPendingAckCorrelationCount = expiredPendingAckCorrelations,
            AverageAckCorrelationLatencyMs = averageAckCorrelationLatencyMs,
            BestAckCorrelationLatencyMs = bestAckCorrelationLatencyMs,
            WorstAckCorrelationLatencyMs = worstAckCorrelationLatencyMs,
            AckCorrelationSummary = ackCorrelationSummary,
            AckCorrelations = ackCorrelations,

            TotalReceiveEventCount = totalReceiveEvents,
            HandledReceiveEventCount = handledReceiveEvents,
            FailedReceiveEventCount = failedReceiveEvents,
            UnhandledReceiveEventCount = unhandledReceiveEvents,
            LastReceiveUtc = lastReceiveUtc,
            ReceiveHealthSummary = receiveHealthSummary,
            InboundFleetHeartbeatCount = inboundFleetHeartbeatCount,
            InboundFleetCommandResultCount = inboundFleetCommandResultCount,
            InboundFleetCommandCount = inboundFleetCommandCount,
            InboundVehicleNodeStatusCount = inboundVehicleNodeStatusCount,
            InboundUnknownMessageCount = inboundUnknownMessageCount,
            ReceiveEvents = receiveEvents,

            OverallHealth = overallHealth,
            Summary = summary
        };
    }

    /// <summary>
    /// Komut kaydının başarısız/expired sayılıp sayılmayacağını belirler.
    /// 
    /// CommandRecord içinde IsFailed alanı olmadığı için bunu LastResult üzerinden çıkarıyoruz.
    /// </summary>
    private static bool IsFailedCommand(CommandRecord record)
    {
        if (record is null)
            return false;

        var result = record.LastResult;

        if (result is null)
            return false;

        if (!result.Success)
            return true;

        return string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(result.Status, "Rejected", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(result.Status, "Expired", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(result.Status, "Timeout", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Receive event mesaj tipini güvenli şekilde kontrol eder.
    /// </summary>
    private static bool IsMessageType(GroundTransportReceiveEvent receiveEvent, string messageType)
    {
        if (receiveEvent?.Envelope is null)
            return false;

        return string.Equals(
            receiveEvent.Envelope.MessageType,
            messageType,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ground Station genel sağlık durumunu değerlendirir.
    /// 
    /// İlk fazda basit kural tabanlı değerlendirme kullanıyoruz.
    /// Link health, route execution, gerçek ACK correlation ve receive diagnostics verisi varsa
    /// bunlar da genel değerlendirmeye katılır.
    /// </summary>
    private static string EvaluateOverallHealth(
        int totalNodes,
        int onlineNodes,
        int criticalNodes,
        int warningNodes,
        int pendingCommands,
        int failedCommands,
        int totalLinks,
        int goodLinks,
        int degradedLinks,
        int criticalLinks,
        int lostLinks,
        int totalRouteExecutions,
        int pendingRouteExecutions,
        int timeoutRouteExecutions,
        int failedRouteExecutions,
        int routeUnavailableExecutions,
        int totalAckCorrelations,
        int pendingAckCorrelations,
        int failedAckCorrelations,
        int expiredPendingAckCorrelations,
        int totalReceiveEvents,
        int failedReceiveEvents,
        int unhandledReceiveEvents)
    {
        if (totalNodes == 0)
            return "Critical";

        if (onlineNodes == 0)
            return "Critical";

        if (criticalNodes > 0)
            return "Critical";

        if (failedCommands >= 3)
            return "Critical";

        // Link verisi varsa ve kullanılabilir hiçbir link yoksa operasyonel olarak kritik kabul edilir.
        if (totalLinks > 0 && goodLinks == 0 && degradedLinks == 0)
            return "Critical";

        // Route execution verisi varsa ve route/gönderim başarısızlıkları çok baskınsa kritik kabul edilir.
        if (totalRouteExecutions > 0 &&
            failedRouteExecutions + timeoutRouteExecutions + routeUnavailableExecutions >= Math.Max(3, totalRouteExecutions))
        {
            return "Critical";
        }

        // Gerçek ACK/result korelasyonunda süresi dolmuş pending kayıtlar varsa kritik kabul edilir.
        if (expiredPendingAckCorrelations > 0)
            return "Critical";

        // ACK correlation verisi varsa ve başarısız correlation sayısı baskınsa kritik kabul edilir.
        if (totalAckCorrelations > 0 &&
            failedAckCorrelations >= Math.Max(3, totalAckCorrelations))
        {
            return "Critical";
        }

        // Receive pipeline içinde hata baskınsa inbound haberleşme kritik kabul edilir.
        if (totalReceiveEvents > 0 &&
            failedReceiveEvents >= Math.Max(3, totalReceiveEvents))
        {
            return "Critical";
        }

        if (warningNodes > 0)
            return "Warning";

        if (pendingCommands >= 5)
            return "Warning";

        if (failedCommands > 0)
            return "Warning";

        if (criticalLinks > 0 || lostLinks > 0)
            return "Warning";

        if (degradedLinks > 0)
            return "Warning";

        if (pendingRouteExecutions >= 5)
            return "Warning";

        if (timeoutRouteExecutions > 0 || failedRouteExecutions > 0 || routeUnavailableExecutions > 0)
            return "Warning";

        if (pendingAckCorrelations > 0 || failedAckCorrelations > 0)
            return "Warning";

        if (failedReceiveEvents > 0 || unhandledReceiveEvents > 0)
            return "Warning";

        return "OK";
    }

    /// <summary>
    /// Link health için kısa özet cümlesi üretir.
    /// </summary>
    private static string BuildLinkHealthSummary(
        int linkVehicleCount,
        int totalLinks,
        int goodLinks,
        int degradedLinks,
        int criticalLinks,
        int lostLinks,
        double? averageTransportLinkQualityScore,
        double? worstTransportLinkQualityScore)
    {
        if (linkVehicleCount == 0 || totalLinks == 0)
            return "No link health data.";

        var avgText = averageTransportLinkQualityScore.HasValue
            ? averageTransportLinkQualityScore.Value.ToString("0.##")
            : "n/a";

        var worstText = worstTransportLinkQualityScore.HasValue
            ? worstTransportLinkQualityScore.Value.ToString("0.##")
            : "n/a";

        if (criticalLinks > 0 || lostLinks > 0)
        {
            return $"Link warning: {goodLinks}/{totalLinks} good links, {criticalLinks} critical, {lostLinks} lost, avg quality {avgText}, worst {worstText}.";
        }

        if (degradedLinks > 0)
        {
            return $"Link degraded: {goodLinks}/{totalLinks} good links, {degradedLinks} degraded, avg quality {avgText}, worst {worstText}.";
        }

        return $"Links OK: {goodLinks}/{totalLinks} good links, avg quality {avgText}, worst {worstText}.";
    }

    /// <summary>
    /// Route execution / transport send için kısa özet cümlesi üretir.
    /// </summary>
    private static string BuildRouteExecutionSummary(
        int totalRouteExecutions,
        int successfulRouteExecutions,
        int ackedRouteExecutions,
        int timeoutRouteExecutions,
        int failedRouteExecutions,
        int routeUnavailableExecutions,
        double? averageRouteExecutionLatencyMs,
        double? worstRouteExecutionLatencyMs)
    {
        if (totalRouteExecutions == 0)
            return "No route execution data.";

        var avgText = averageRouteExecutionLatencyMs.HasValue
            ? averageRouteExecutionLatencyMs.Value.ToString("0.##")
            : "n/a";

        var worstText = worstRouteExecutionLatencyMs.HasValue
            ? worstRouteExecutionLatencyMs.Value.ToString("0.##")
            : "n/a";

        if (timeoutRouteExecutions > 0 || failedRouteExecutions > 0 || routeUnavailableExecutions > 0)
        {
            return $"Route execution warning: {successfulRouteExecutions}/{totalRouteExecutions} successful, {ackedRouteExecutions} acked, {timeoutRouteExecutions} timeout, {failedRouteExecutions} failed, {routeUnavailableExecutions} unroutable, avg latency {avgText} ms, worst {worstText} ms.";
        }

        return $"Route executions OK: {successfulRouteExecutions}/{totalRouteExecutions} successful, {ackedRouteExecutions} acked, avg latency {avgText} ms, worst {worstText} ms.";
    }

    /// <summary>
    /// Gerçek ACK/result korelasyonu için kısa özet cümlesi üretir.
    /// </summary>
    private static string BuildAckCorrelationSummary(
        int totalAckCorrelations,
        int pendingAckCorrelations,
        int ackedCorrelations,
        int successfulAckCorrelations,
        int failedAckCorrelations,
        int expiredPendingAckCorrelations,
        double? averageAckCorrelationLatencyMs,
        double? worstAckCorrelationLatencyMs)
    {
        if (totalAckCorrelations == 0)
            return "No ACK correlation data.";

        var avgText = averageAckCorrelationLatencyMs.HasValue
            ? averageAckCorrelationLatencyMs.Value.ToString("0.##")
            : "n/a";

        var worstText = worstAckCorrelationLatencyMs.HasValue
            ? worstAckCorrelationLatencyMs.Value.ToString("0.##")
            : "n/a";

        if (expiredPendingAckCorrelations > 0)
        {
            return $"ACK correlation critical: {expiredPendingAckCorrelations} expired pending, {ackedCorrelations}/{totalAckCorrelations} acked, {failedAckCorrelations} failed, avg ACK {avgText} ms, worst {worstText} ms.";
        }

        if (pendingAckCorrelations > 0 || failedAckCorrelations > 0)
        {
            return $"ACK correlation warning: {ackedCorrelations}/{totalAckCorrelations} acked, {pendingAckCorrelations} pending, {failedAckCorrelations} failed, avg ACK {avgText} ms, worst {worstText} ms.";
        }

        return $"ACK correlations OK: {ackedCorrelations}/{totalAckCorrelations} acked, {successfulAckCorrelations} successful, avg ACK {avgText} ms, worst {worstText} ms.";
    }

    /// <summary>
    /// Receive diagnostics için kısa özet cümlesi üretir.
    /// </summary>
    private static string BuildReceiveHealthSummary(
        int totalReceiveEvents,
        int handledReceiveEvents,
        int failedReceiveEvents,
        int unhandledReceiveEvents,
        int inboundFleetHeartbeatCount,
        int inboundFleetCommandResultCount,
        int inboundFleetCommandCount,
        int inboundVehicleNodeStatusCount,
        int inboundUnknownMessageCount,
        DateTimeOffset? lastReceiveUtc)
    {
        if (totalReceiveEvents == 0)
            return "No receive data.";

        var lastText = lastReceiveUtc.HasValue
            ? lastReceiveUtc.Value.ToString("O")
            : "n/a";

        var messageBreakdown =
            $"heartbeat={inboundFleetHeartbeatCount}, commandResult={inboundFleetCommandResultCount}, command={inboundFleetCommandCount}, nodeStatus={inboundVehicleNodeStatusCount}, unknown={inboundUnknownMessageCount}";

        if (failedReceiveEvents > 0)
        {
            return $"Receive warning: {handledReceiveEvents}/{totalReceiveEvents} handled, {failedReceiveEvents} failed, {unhandledReceiveEvents} unhandled, {messageBreakdown}, last={lastText}.";
        }

        if (unhandledReceiveEvents > 0 || inboundUnknownMessageCount > 0)
        {
            return $"Receive degraded: {handledReceiveEvents}/{totalReceiveEvents} handled, {unhandledReceiveEvents} unhandled, {messageBreakdown}, last={lastText}.";
        }

        return $"Receive OK: {handledReceiveEvents}/{totalReceiveEvents} handled, {messageBreakdown}, last={lastText}.";
    }

    /// <summary>
    /// Snapshot için kısa özet cümlesi üretir.
    /// </summary>
    private static string BuildSummary(
        string overallHealth,
        int totalNodes,
        int onlineNodes,
        int pendingCommands,
        int failedCommands,
        int activeObstacles,
        int activeTargets,
        string linkHealthSummary,
        string routeExecutionSummary,
        string ackCorrelationSummary,
        string receiveHealthSummary)
    {
        if (string.Equals(overallHealth, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return $"Critical ground status: {onlineNodes}/{totalNodes} nodes online, {failedCommands} failed commands, {activeObstacles} active obstacles. {linkHealthSummary} {routeExecutionSummary} {ackCorrelationSummary} {receiveHealthSummary}";
        }

        if (string.Equals(overallHealth, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return $"Warning ground status: {onlineNodes}/{totalNodes} nodes online, {pendingCommands} pending commands, {failedCommands} failed commands. {linkHealthSummary} {routeExecutionSummary} {ackCorrelationSummary} {receiveHealthSummary}";
        }

        return $"Ground station OK: {onlineNodes}/{totalNodes} nodes online, {activeObstacles} active obstacles, {activeTargets} active targets. {linkHealthSummary} {routeExecutionSummary} {ackCorrelationSummary} {receiveHealthSummary}";
    }
}