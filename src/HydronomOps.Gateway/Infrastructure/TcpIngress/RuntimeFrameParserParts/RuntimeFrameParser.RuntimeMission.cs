using System.Text.Json;
using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Contracts.Mission;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private void ProcessRuntimeMissionState(JsonElement root)
    {
        var timestamp = ReadTimestamp(root);

        var mission = new MissionStateDto
        {
            TimestampUtc = timestamp,
            VehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main",
            MissionId = GetString(root, "missionId", "MissionId", "scenarioId", "ScenarioId"),
            MissionName = GetString(root, "missionName", "MissionName", "scenarioName", "ScenarioName"),
            Status = GetString(root, "status", "Status", "scenarioState", "ScenarioState") ?? "idle",
            CurrentStepIndex = (int)(TryReadDouble(
                root,
                "currentStepIndex",
                "CurrentStepIndex",
                "completedObjectiveCount",
                "CompletedObjectiveCount") ?? 0.0),
            TotalStepCount = (int)(TryReadDouble(
                root,
                "totalStepCount",
                "TotalStepCount",
                "totalObjectiveCount",
                "TotalObjectiveCount") ?? 0.0),
            CurrentStepTitle = GetString(
                root,
                "currentStepTitle",
                "CurrentStepTitle",
                "currentObjectiveId",
                "CurrentObjectiveId"),
            NextObjective = GetString(
                root,
                "nextObjective",
                "NextObjective",
                "currentObjectiveId",
                "CurrentObjectiveId"),
            RemainingDistanceMeters = TryReadDouble(
                root,
                "remainingDistanceMeters",
                "RemainingDistanceMeters",
                "lastDistanceToTargetMeters",
                "LastDistanceToTargetMeters"),
            Freshness = new FreshnessDto
            {
                TimestampUtc = timestamp,
                AgeMs = 0,
                IsFresh = true,
                ThresholdMs = 5000,
                Source = "runtime-mission-state"
            }
        };

        var warnings = new List<string>();

        var runId = GetString(root, "runId", "RunId");
        if (!string.IsNullOrWhiteSpace(runId))
        {
            warnings.Add($"runId={runId}");
        }

        var lastCompleted = GetString(root, "lastCompletedObjectiveId", "LastCompletedObjectiveId");
        if (!string.IsNullOrWhiteSpace(lastCompleted))
        {
            warnings.Add($"lastCompleted={lastCompleted}");
        }

        var summary = GetString(
            root,
            "lastTickSummary",
            "LastTickSummary",
            "sessionSummary",
            "SessionSummary");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            warnings.Add(summary);
        }

        mission.Warnings = warnings;

        _stateStore.SetMissionState(mission);
        _stateStore.TouchRuntimeMessage("RuntimeMissionState");
    }
}