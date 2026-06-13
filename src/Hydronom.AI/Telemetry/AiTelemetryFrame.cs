using System;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Telemetry;

public sealed record AiTelemetryFrame(
    DateTime TimestampUtc,
    string Source,
    string Provider,
    string Mode,
    string PlanId,
    string Goal,
    int StepCount,
    bool Allowed,
    bool RequiresHumanApproval,
    string Reason
)
{
    public static AiTelemetryFrame FromPlan(
        string source,
        string provider,
        string mode,
        MissionPlan plan,
        bool allowed,
        bool requiresHumanApproval,
        string reason)
        => new(
            TimestampUtc: DateTime.UtcNow,
            Source: source,
            Provider: provider,
            Mode: mode,
            PlanId: plan.Id,
            Goal: plan.Goal,
            StepCount: plan.Steps.Count,
            Allowed: allowed,
            RequiresHumanApproval: requiresHumanApproval,
            Reason: reason
        );
}
