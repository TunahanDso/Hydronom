namespace Hydronom.GroundStation.Diagnostics;

using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.WorldModel;

/// <summary>
/// Ground Station tarafındaki farklı modüllerden gelen bilgileri okuyup
/// tek bir operasyon snapshot'ına dönüştüren diagnostics motorudur.
/// 
/// Bu sınıfın amacı:
/// - FleetRegistry snapshot'ını yorumlamak,
/// - CommandTracker geçmişini yorumlamak,
/// - GroundWorldModel durumunu yorumlamak,
/// - genel health ve kısa açıklama üretmektir.
/// 
/// Böylece Hydronom Ops veya ilerideki Gateway katmanı tek çağrıyla
/// yer istasyonunun genel durumunu okuyabilir.
/// </summary>
public sealed class GroundDiagnosticsEngine
{
    /// <summary>
    /// Filo, komut ve dünya modeli verilerinden operasyon snapshot'ı üretir.
    /// </summary>
    public GroundOperationSnapshot CreateSnapshot(
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        IReadOnlyList<CommandRecord> commandSnapshot,
        GroundWorldModel worldModel)
    {
        fleetSnapshot ??= Array.Empty<VehicleNodeStatus>();
        commandSnapshot ??= Array.Empty<CommandRecord>();

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

        var overallHealth = EvaluateOverallHealth(
            totalNodes,
            onlineNodes,
            criticalNodes,
            warningNodes,
            pendingCommands,
            failedCommands);

        var summary = BuildSummary(
            overallHealth,
            totalNodes,
            onlineNodes,
            pendingCommands,
            failedCommands,
            activeObstacles,
            activeTargets);

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
    /// </summary>
    private static string EvaluateOverallHealth(
        int totalNodes,
        int onlineNodes,
        int criticalNodes,
        int warningNodes,
        int pendingCommands,
        int failedCommands)
    {
        if (totalNodes == 0)
            return "Critical";

        if (onlineNodes == 0)
            return "Critical";

        if (criticalNodes > 0)
            return "Critical";

        if (failedCommands >= 3)
            return "Critical";

        if (warningNodes > 0)
            return "Warning";

        if (pendingCommands >= 5)
            return "Warning";

        if (failedCommands > 0)
            return "Warning";

        return "OK";
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
        int activeTargets)
    {
        if (string.Equals(overallHealth, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return $"Critical ground status: {onlineNodes}/{totalNodes} nodes online, {failedCommands} failed commands, {activeObstacles} active obstacles.";
        }

        if (string.Equals(overallHealth, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return $"Warning ground status: {onlineNodes}/{totalNodes} nodes online, {pendingCommands} pending commands, {failedCommands} failed commands.";
        }

        return $"Ground station OK: {onlineNodes}/{totalNodes} nodes online, {activeObstacles} active obstacles, {activeTargets} active targets.";
    }
}