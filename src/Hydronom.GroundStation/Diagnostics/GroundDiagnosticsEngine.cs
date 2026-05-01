namespace Hydronom.GroundStation.Diagnostics;

using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.TransportExecution;
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
/// - genel health ve kısa açıklama üretmektir.
/// 
/// Böylece Hydronom Ops veya ilerideki Gateway katmanı tek çağrıyla
/// yer istasyonunun genel durumunu okuyabilir.
/// </summary>
public sealed class GroundDiagnosticsEngine
{
    /// <summary>
    /// Filo, komut, dünya modeli, bağlantı sağlığı ve route execution verilerinden operasyon snapshot'ı üretir.
    /// 
    /// linkHealthSnapshot ve routeExecutionSnapshot opsiyoneldir.
    /// Böylece eski çağrılar bozulmadan çalışmaya devam eder.
    /// </summary>
    public GroundOperationSnapshot CreateSnapshot(
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        IReadOnlyList<CommandRecord> commandSnapshot,
        GroundWorldModel worldModel,
        IReadOnlyList<VehicleLinkHealthSnapshot>? linkHealthSnapshot = null,
        IReadOnlyList<RouteExecutionSnapshot>? routeExecutionSnapshot = null)
    {
        fleetSnapshot ??= Array.Empty<VehicleNodeStatus>();
        commandSnapshot ??= Array.Empty<CommandRecord>();
        linkHealthSnapshot ??= Array.Empty<VehicleLinkHealthSnapshot>();
        routeExecutionSnapshot ??= Array.Empty<RouteExecutionSnapshot>();

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
            routeUnavailableExecutions);

        var summary = BuildSummary(
            overallHealth,
            totalNodes,
            onlineNodes,
            pendingCommands,
            failedCommands,
            activeObstacles,
            activeTargets,
            linkHealthSummary,
            routeExecutionSummary);

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
    /// Ground Station genel sağlık durumunu değerlendirir.
    /// 
    /// İlk fazda basit kural tabanlı değerlendirme kullanıyoruz.
    /// Link health ve route execution verisi varsa bunlar da genel değerlendirmeye katılır.
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
        int routeUnavailableExecutions)
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
        string routeExecutionSummary)
    {
        if (string.Equals(overallHealth, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return $"Critical ground status: {onlineNodes}/{totalNodes} nodes online, {failedCommands} failed commands, {activeObstacles} active obstacles. {linkHealthSummary} {routeExecutionSummary}";
        }

        if (string.Equals(overallHealth, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return $"Warning ground status: {onlineNodes}/{totalNodes} nodes online, {pendingCommands} pending commands, {failedCommands} failed commands. {linkHealthSummary} {routeExecutionSummary}";
        }

        return $"Ground station OK: {onlineNodes}/{totalNodes} nodes online, {activeObstacles} active obstacles, {activeTargets} active targets. {linkHealthSummary} {routeExecutionSummary}";
    }
}