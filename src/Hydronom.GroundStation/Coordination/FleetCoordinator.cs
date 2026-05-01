namespace Hydronom.GroundStation.Coordination;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafında görev isteğini filo koordinasyon sonucuna çeviren ana koordinasyon sınıfıdır.
/// 
/// FleetCoordinator şu zinciri kurar:
/// - MissionRequest alır,
/// - MissionAllocator ile en uygun aracı seçer,
/// - Seçilen araç için FleetCommand üretir,
/// - CommandTracker'a kaydedilecek HydronomEnvelope üretir.
/// 
/// Bu sınıf PDF'deki FleetCoordinator mantığının ilk çekirdeğidir.
/// Şimdilik gerçek transport gönderimi yapmaz.
/// Sadece koordinasyon kararını ve gönderilmeye hazır komut envelope'unu üretir.
/// </summary>
public sealed class FleetCoordinator
{
    /// <summary>
    /// Göreve uygun araç seçimini yapan allocator.
    /// </summary>
    private readonly MissionAllocator _missionAllocator;

    /// <summary>
    /// FleetCoordinator oluşturur.
    /// </summary>
    public FleetCoordinator(MissionAllocator? missionAllocator = null)
    {
        _missionAllocator = missionAllocator ?? new MissionAllocator();
    }

    /// <summary>
    /// Görev isteğini değerlendirir, uygun aracı seçer ve görev komutu üretir.
    /// 
    /// Bu metot komutu fiziksel olarak göndermez.
    /// Dönen Envelope, ileride CommunicationRouter / TransportManager tarafından gönderilecektir.
    /// </summary>
    public FleetCoordinationResult CoordinateMission(
        MissionRequest request,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        string sourceNodeId = "GROUND-001",
        bool isOperatorIssued = true)
    {
        if (request is null || !request.IsValid)
        {
            return FleetCoordinationResult.Failed(
                request,
                null,
                "Invalid mission request.");
        }

        var allocation = _missionAllocator.Allocate(request, fleetSnapshot);

        if (!allocation.Success)
        {
            return FleetCoordinationResult.Failed(
                request,
                allocation,
                allocation.Reason);
        }

        var command = CreateAssignMissionCommand(
            request,
            allocation,
            sourceNodeId,
            isOperatorIssued);

        if (!command.IsValid)
        {
            return FleetCoordinationResult.Failed(
                request,
                allocation,
                "Generated fleet command is invalid.");
        }

        var envelope = HydronomEnvelopeFactory.CreateCommand(command);

        return FleetCoordinationResult.Succeeded(
            request,
            allocation,
            command,
            envelope,
            $"Mission {request.MissionId} coordinated for {allocation.SelectedNodeId}.");
    }

    /// <summary>
    /// MissionRequest + MissionAllocationResult bilgisinden AssignMission komutu üretir.
    /// 
    /// Bu komut araç tarafında:
    /// CommandValidator → AuthorityManager → SafetyGate → TaskManager
    /// zincirinden geçmelidir.
    /// </summary>
    private static FleetCommand CreateAssignMissionCommand(
        MissionRequest request,
        MissionAllocationResult allocation,
        string sourceNodeId,
        bool isOperatorIssued)
    {
        var args = new Dictionary<string, string>
        {
            ["missionId"] = request.MissionId,
            ["missionType"] = request.MissionType,
            ["missionName"] = request.Name,
            ["priority"] = request.Priority.ToString(),
            ["allocationScore"] = allocation.Score.ToString("F2")
        };

        if (!string.IsNullOrWhiteSpace(request.RelatedWorldObjectId))
            args["relatedWorldObjectId"] = request.RelatedWorldObjectId;

        if (request.TargetLatitude is not null)
            args["targetLat"] = request.TargetLatitude.Value.ToString("R");

        if (request.TargetLongitude is not null)
            args["targetLon"] = request.TargetLongitude.Value.ToString("R");

        if (request.TargetX is not null)
            args["targetX"] = request.TargetX.Value.ToString("R");

        if (request.TargetY is not null)
            args["targetY"] = request.TargetY.Value.ToString("R");

        foreach (var pair in request.Metadata)
        {
            args[$"meta.{pair.Key}"] = pair.Value;
        }

        return new FleetCommand
        {
            SourceNodeId = string.IsNullOrWhiteSpace(sourceNodeId)
                ? "GROUND-001"
                : sourceNodeId,
            TargetNodeId = allocation.SelectedNodeId,
            CommandType = "AssignMission",
            AuthorityLevel = "MissionCommand",
            Priority = MessagePriority.High,
            Args = args,
            IsOperatorIssued = isOperatorIssued,
            RequiresResult = true,
            Metadata = new Dictionary<string, string>
            {
                ["coordinator"] = "FleetCoordinator",
                ["selectedDisplayName"] = allocation.SelectedDisplayName,
                ["allocationReason"] = allocation.Reason
            }
        };
    }
}