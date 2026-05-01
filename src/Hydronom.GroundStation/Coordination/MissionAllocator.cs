namespace Hydronom.GroundStation.Coordination;

using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafında bir görevi filo içindeki en uygun araca atamaya çalışan basit görev dağıtıcıdır.
/// 
/// Bu sınıf PDF'deki MissionAllocator mantığının ilk çekirdeğidir.
/// Şimdilik karmaşık rota, enerji, mesafe veya risk hesabı yapmaz.
/// İlk hedef:
/// - Online araçları filtrelemek,
/// - Araç tipi uygun mu bakmak,
/// - Zorunlu kabiliyetler var mı kontrol etmek,
/// - Tercih edilen kabiliyetlere göre skor vermek,
/// - Batarya ve health durumunu basitçe hesaba katmak,
/// - En yüksek skorlu aracı seçmektir.
/// 
/// İleride bu sınıf:
/// - GroundWorldModel,
/// - Link quality,
/// - Energy model,
/// - Distance to target,
/// - Mission priority,
/// - FleetCoordinator role assignment
/// ile genişletilecektir.
/// </summary>
public sealed class MissionAllocator
{
    /// <summary>
    /// Verilen görev isteği için en uygun aracı seçer.
    /// </summary>
    public MissionAllocationResult Allocate(
        MissionRequest request,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot)
    {
        if (request is null || !request.IsValid)
        {
            return MissionAllocationResult.Failed(
                request!,
                "Invalid mission request.");
        }

        if (fleetSnapshot is null || fleetSnapshot.Count == 0)
        {
            return MissionAllocationResult.Failed(
                request,
                "No vehicles available in fleet snapshot.");
        }

        var rejected = new Dictionary<string, string>();
        var candidates = new List<(VehicleNodeStatus Status, double Score)>();

        foreach (var status in fleetSnapshot)
        {
            var nodeId = status.Identity.NodeId;

            if (!status.IsValid)
            {
                rejected[nodeId] = "Invalid vehicle status.";
                continue;
            }

            if (!status.IsOnline)
            {
                rejected[nodeId] = "Vehicle is offline.";
                continue;
            }

            if (!IsVehicleTypeAllowed(request, status))
            {
                rejected[nodeId] = "Vehicle type is not allowed for this mission.";
                continue;
            }

            if (!HasRequiredCapabilities(request, status, out var missingCapability))
            {
                rejected[nodeId] = $"Missing required capability: {missingCapability}";
                continue;
            }

            var score = CalculateScore(request, status);
            candidates.Add((status, score));
        }

        if (candidates.Count == 0)
        {
            return MissionAllocationResult.Failed(
                request,
                "No online vehicle satisfies mission requirements.",
                rejected);
        }

        var selected = candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Status.Identity.DisplayName)
            .First();

        return new MissionAllocationResult
        {
            MissionId = request.MissionId,
            Success = true,
            SelectedNodeId = selected.Status.Identity.NodeId,
            SelectedDisplayName = selected.Status.Identity.DisplayName,
            Score = selected.Score,
            CandidateNodeIds = candidates
                .Select(x => x.Status.Identity.NodeId)
                .ToArray(),
            RejectedNodeReasons = rejected,
            Reason = $"Selected {selected.Status.Identity.DisplayName} because it satisfies required capabilities and has the best score."
        };
    }

    /// <summary>
    /// Görev isteğindeki araç tipi kısıtına göre aracın uygun olup olmadığını kontrol eder.
    /// 
    /// AllowedVehicleTypes boşsa her araç tipi kabul edilir.
    /// </summary>
    private static bool IsVehicleTypeAllowed(
        MissionRequest request,
        VehicleNodeStatus status)
    {
        if (request.AllowedVehicleTypes.Count == 0)
            return true;

        return request.AllowedVehicleTypes
            .Any(x => string.Equals(
                x,
                status.Identity.VehicleType,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Aracın görevin zorunlu kabiliyetlerini taşıyıp taşımadığını kontrol eder.
    /// </summary>
    private static bool HasRequiredCapabilities(
        MissionRequest request,
        VehicleNodeStatus status,
        out string missingCapability)
    {
        missingCapability = string.Empty;

        var capabilityNames = status.Capabilities
            .Where(x => x.IsEnabled)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var required in request.RequiredCapabilities)
        {
            if (capabilityNames.Contains(required))
                continue;

            missingCapability = required;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Basit görev uygunluk skoru hesaplar.
    /// 
    /// İlk faz skor mantığı:
    /// - Temel uygunluk: 100
    /// - Her preferred capability: +10
    /// - Health OK ise +20
    /// - Health Warning ise -15
    /// - Health Critical/Fault ise -50
    /// - Batarya yüzdesi / 5 kadar ek skor
    /// - Leader/Scout/Mapper gibi rol eşleşmeleri küçük bonus alabilir
    /// </summary>
    private static double CalculateScore(
        MissionRequest request,
        VehicleNodeStatus status)
    {
        var score = 100.0;

        var capabilityNames = status.Capabilities
            .Where(x => x.IsEnabled)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var preferred in request.PreferredCapabilities)
        {
            if (capabilityNames.Contains(preferred))
                score += 10.0;
        }

        score += CalculateHealthScore(status.Health);
        score += CalculateBatteryScore(status.BatteryPercent);
        score += CalculateRoleBonus(request, status);

        return score;
    }

    /// <summary>
    /// Health durumuna göre skor katkısı hesaplar.
    /// </summary>
    private static double CalculateHealthScore(string health)
    {
        if (string.Equals(health, "OK", StringComparison.OrdinalIgnoreCase))
            return 20.0;

        if (string.Equals(health, "Warning", StringComparison.OrdinalIgnoreCase))
            return -15.0;

        if (string.Equals(health, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(health, "Fault", StringComparison.OrdinalIgnoreCase))
            return -50.0;

        return 0.0;
    }

    /// <summary>
    /// Batarya yüzdesine göre skor katkısı hesaplar.
    /// 
    /// Örnek:
    /// 80% batarya → +16 skor
    /// </summary>
    private static double CalculateBatteryScore(double? batteryPercent)
    {
        if (batteryPercent is null)
            return 0.0;

        var clamped = Math.Clamp(batteryPercent.Value, 0.0, 100.0);
        return clamped / 5.0;
    }

    /// <summary>
    /// Görev tipi ile araç rolü arasındaki basit eşleşmeye göre bonus verir.
    /// 
    /// İlk faz için sade tutulmuştur.
    /// </summary>
    private static double CalculateRoleBonus(
        MissionRequest request,
        VehicleNodeStatus status)
    {
        var role = status.Identity.Role;
        var missionType = request.MissionType;

        if (string.Equals(missionType, "Mapping", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(role, "Mapper", StringComparison.OrdinalIgnoreCase))
        {
            return 15.0;
        }

        if (string.Equals(missionType, "Search", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(role, "Scout", StringComparison.OrdinalIgnoreCase))
        {
            return 15.0;
        }

        if (string.Equals(missionType, "Relay", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(role, "Relay", StringComparison.OrdinalIgnoreCase))
        {
            return 15.0;
        }

        if (string.Equals(role, "Leader", StringComparison.OrdinalIgnoreCase))
            return 5.0;

        return 0.0;
    }
}