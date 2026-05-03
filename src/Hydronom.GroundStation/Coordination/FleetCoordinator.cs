namespace Hydronom.GroundStation.Coordination;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafÄ±nda gÃ¶rev isteÄŸini filo koordinasyon sonucuna Ã§eviren ana koordinasyon sÄ±nÄ±fÄ±dÄ±r.
/// 
/// FleetCoordinator ÅŸu zinciri kurar:
/// - MissionRequest alÄ±r,
/// - MissionAllocator ile en uygun aracÄ± seÃ§er,
/// - SeÃ§ilen araÃ§ iÃ§in FleetCommand Ã¼retir,
/// - CommandTracker'a kaydedilecek HydronomEnvelope Ã¼retir.
/// 
/// Bu sÄ±nÄ±f PDF'deki FleetCoordinator mantÄ±ÄŸÄ±nÄ±n ilk Ã§ekirdeÄŸidir.
/// Åimdilik gerÃ§ek transport gÃ¶nderimi yapmaz.
/// Sadece koordinasyon kararÄ±nÄ± ve gÃ¶nderilmeye hazÄ±r komut envelope'unu Ã¼retir.
/// </summary>
public sealed class FleetCoordinator
{
    /// <summary>
    /// GÃ¶reve uygun araÃ§ seÃ§imini yapan allocator.
    /// </summary>
    private readonly MissionAllocator _missionAllocator;

    /// <summary>
    /// FleetCoordinator oluÅŸturur.
    /// </summary>
    public FleetCoordinator(MissionAllocator? missionAllocator = null)
    {
        _missionAllocator = missionAllocator ?? new MissionAllocator();
    }

    /// <summary>
    /// GÃ¶rev isteÄŸini deÄŸerlendirir, uygun aracÄ± seÃ§er ve gÃ¶rev komutu Ã¼retir.
    /// 
    /// Bu metot komutu fiziksel olarak gÃ¶ndermez.
    /// DÃ¶nen Envelope, ileride CommunicationRouter / TransportManager tarafÄ±ndan gÃ¶nderilecektir.
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
    /// MissionRequest + MissionAllocationResult bilgisinden AssignMission komutu Ã¼retir.
    /// 
    /// Bu komut araÃ§ tarafÄ±nda:
    /// CommandValidator â†’ AuthorityManager â†’ SafetyGate â†’ TaskManager
    /// zincirinden geÃ§melidir.
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
