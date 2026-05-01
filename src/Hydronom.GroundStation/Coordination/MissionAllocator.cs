namespace Hydronom.GroundStation.Coordination;

using Hydronom.Core.Fleet;
using Hydronom.GroundStation.MissionCompatibility;

/// <summary>
/// Ground Station tarafında bir görevi filo içindeki en uygun araca atamaya çalışan görev dağıtıcıdır.
/// 
/// Bu sınıf PDF'deki MissionAllocator mantığının ilk çekirdeğidir.
/// 
/// Bu sürümde görev ataması artık sadece online/offline kontrolüne bakmaz.
/// MissionCompatibilityEvaluator üzerinden:
/// - Araç tipi uygunluğu,
/// - Zorunlu capability uygunluğu,
/// - Tercih edilen capability durumu,
/// - Capability enabled/health durumu,
/// - Simülasyon/gerçek araç politikası
/// değerlendirmesi yapılır.
/// 
/// Sonrasında allocator:
/// - Mission compatibility skorunu,
/// - Health katkısını,
/// - Batarya katkısını,
/// - Role/mission bonusunu
/// birleştirerek en uygun aracı seçer.
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
    private readonly MissionCompatibilityEvaluator _compatibilityEvaluator;

    public MissionAllocator(
        MissionCompatibilityEvaluator? compatibilityEvaluator = null)
    {
        _compatibilityEvaluator = compatibilityEvaluator ?? new MissionCompatibilityEvaluator();
    }

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

        var rejected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<(VehicleNodeStatus Status, MissionCompatibilityResult Compatibility, double Score)>();

        var capabilityRequirements = BuildCapabilityRequirements(request);

        var allowedVehicleTypes = request.AllowedVehicleTypes?.ToArray()
            ?? Array.Empty<string>();

        foreach (var status in fleetSnapshot)
        {
            var nodeId = status.Identity.NodeId;

            if (!status.IsValid)
            {
                rejected[nodeId] = "Invalid vehicle status.";
                continue;
            }

            var compatibility = _compatibilityEvaluator.Evaluate(
                status,
                request.MissionType,
                allowedVehicleTypes,
                capabilityRequirements,
                requireOnline: true,
                allowSimulation: true);

            if (!compatibility.IsCompatible)
            {
                rejected[nodeId] = BuildRejectionReason(compatibility);
                continue;
            }

            var score = CalculateScore(
                request,
                status,
                compatibility);

            candidates.Add((status, compatibility, score));
        }

        if (candidates.Count == 0)
        {
            return MissionAllocationResult.Failed(
                request,
                "No vehicle satisfies mission compatibility requirements.",
                rejected);
        }

        var selected = candidates
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Compatibility.Score)
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
            Reason =
                $"Selected {selected.Status.Identity.DisplayName} because it passed mission compatibility evaluation " +
                $"and has the best allocation score. CompatibilityScore={selected.Compatibility.Score:0.##}, FinalScore={selected.Score:0.##}."
        };
    }

    /// <summary>
    /// MissionRequest içindeki required/preferred capability listelerini
    /// MissionCompatibilityEvaluator tarafından anlaşılacak requirement listesine çevirir.
    /// </summary>
    private static IReadOnlyList<MissionCapabilityRequirement> BuildCapabilityRequirements(
        MissionRequest request)
    {
        var requirements = new List<MissionCapabilityRequirement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var required in request.RequiredCapabilities ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(required))
                continue;

            if (!seen.Add(required))
                continue;

            requirements.Add(new MissionCapabilityRequirement
            {
                Name = required,
                Required = true,
                RequireEnabled = true,
                RequireHealthy = true,
                Weight = 2.0
            });
        }

        foreach (var preferred in request.PreferredCapabilities ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(preferred))
                continue;

            if (!seen.Add(preferred))
                continue;

            requirements.Add(new MissionCapabilityRequirement
            {
                Name = preferred,
                Required = false,
                RequireEnabled = true,
                RequireHealthy = true,
                Weight = 0.5
            });
        }

        return requirements;
    }

    /// <summary>
    /// Mission compatibility reddini kısa ve okunabilir allocator gerekçesine çevirir.
    /// </summary>
    private static string BuildRejectionReason(
        MissionCompatibilityResult compatibility)
    {
        if (compatibility.Issues.Count == 0)
            return compatibility.Reason;

        var blockingIssues = compatibility.Issues
            .Where(x => x.IsBlocking)
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (blockingIssues.Length == 0)
            return compatibility.Reason;

        return $"{compatibility.Reason} BlockingIssues={string.Join(", ", blockingIssues)}.";
    }

    /// <summary>
    /// Görev uygunluk skorunu hesaplar.
    /// 
    /// İlk faz skor mantığı:
    /// - MissionCompatibilityEvaluator skoru temel alınır.
    /// - Health OK ise +20
    /// - Health Warning ise -15
    /// - Health Critical/Fault ise -50
    /// - Batarya yüzdesi / 5 kadar ek skor
    /// - Leader/Scout/Mapper gibi rol eşleşmeleri küçük bonus alabilir
    /// </summary>
    private static double CalculateScore(
        MissionRequest request,
        VehicleNodeStatus status,
        MissionCompatibilityResult compatibility)
    {
        var score = compatibility.Score;

        score += CalculateHealthScore(status.Health);
        score += CalculateBatteryScore(status.BatteryPercent);
        score += CalculateRoleBonus(request, status);

        return Math.Round(score, 2);
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