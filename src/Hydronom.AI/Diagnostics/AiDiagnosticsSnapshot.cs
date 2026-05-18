using System;
using Hydronom.AI.Safety;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Diagnostics;

public sealed record AiDiagnosticsSnapshot(
    DateTime TimestampUtc,
    bool Enabled,
    string Provider,
    string Mode,
    string? LastPlanId,
    string? LastGoal,
    int LastStepCount,
    bool LastPlanAllowed,
    bool LastPlanRequiresApproval,
    string? LastSafetyReason,
    double LastLatencyMs,
    string? LastError
)
{
    public static AiDiagnosticsSnapshot Disabled(string provider = "none")
        => new(
            TimestampUtc: DateTime.UtcNow,
            Enabled: false,
            Provider: provider,
            Mode: "disabled",
            LastPlanId: null,
            LastGoal: null,
            LastStepCount: 0,
            LastPlanAllowed: false,
            LastPlanRequiresApproval: true,
            LastSafetyReason: null,
            LastLatencyMs: 0,
            LastError: null
        );

    public static AiDiagnosticsSnapshot FromPlan(
        string provider,
        string mode,
        MissionPlan plan,
        AiSafetyDecision decision,
        double latencyMs)
        => new(
            TimestampUtc: DateTime.UtcNow,
            Enabled: true,
            Provider: provider,
            Mode: mode,
            LastPlanId: plan.Id,
            LastGoal: plan.Goal,
            LastStepCount: plan.Steps.Count,
            LastPlanAllowed: decision.Allowed,
            LastPlanRequiresApproval: decision.RequiresHumanApproval,
            LastSafetyReason: decision.Reason,
            LastLatencyMs: latencyMs,
            LastError: null
        );

    public static AiDiagnosticsSnapshot FromError(
        string provider,
        string mode,
        Exception ex,
        double latencyMs)
        => new(
            TimestampUtc: DateTime.UtcNow,
            Enabled: true,
            Provider: provider,
            Mode: mode,
            LastPlanId: null,
            LastGoal: null,
            LastStepCount: 0,
            LastPlanAllowed: false,
            LastPlanRequiresApproval: true,
            LastSafetyReason: "AI hata verdi.",
            LastLatencyMs: latencyMs,
            LastError: ex.Message
        );
}
