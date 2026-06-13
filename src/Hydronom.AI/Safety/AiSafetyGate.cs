using System;
using System.Linq;
using Hydronom.AI.Planning.Validation;
using Hydronom.AI.Policies;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Safety;

public sealed record AiSafetyDecision(
    bool Allowed,
    bool RequiresHumanApproval,
    string Reason,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> Warnings
);

public sealed class AiSafetyGate
{
    public AiSafetyDecision Evaluate(
        MissionPlan? plan,
        AiPlanValidationResult validation,
        SafetyPolicy? safetyPolicy = null,
        AiAuthorityPolicy? authorityPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(validation);

        safetyPolicy ??= SafetyPolicy.DefaultSuggest();
        authorityPolicy ??= AiAuthorityPolicy.SuggestOnly();

        var blocking = validation.BlockingIssues.Select(i => $"{i.Code}: {i.Message}").ToArray();
        var warnings = validation.Warnings.Select(i => $"{i.Code}: {i.Message}").ToArray();

        if (plan is null)
        {
            return new AiSafetyDecision(
                Allowed: false,
                RequiresHumanApproval: true,
                Reason: "Plan null olduğu için reddedildi.",
                BlockingIssues: blocking.Length == 0 ? new[] { "PLAN_NULL" } : blocking,
                Warnings: warnings
            );
        }

        if (!validation.IsValid)
        {
            return new AiSafetyDecision(
                Allowed: false,
                RequiresHumanApproval: true,
                Reason: "Plan blocking validation issue içerdiği için reddedildi.",
                BlockingIssues: blocking,
                Warnings: warnings
            );
        }

        if (safetyPolicy.Mode == AiMode.Suggest || authorityPolicy.RequireHumanApprovalForMissionStart)
        {
            return new AiSafetyDecision(
                Allowed: true,
                RequiresHumanApproval: true,
                Reason: "Plan güvenli öneri olarak kabul edildi; uygulama için operatör onayı gerekir.",
                BlockingIssues: blocking,
                Warnings: warnings
            );
        }

        return new AiSafetyDecision(
            Allowed: true,
            RequiresHumanApproval: false,
            Reason: "Plan safety gate tarafından kabul edildi.",
            BlockingIssues: blocking,
            Warnings: warnings
        );
    }
}
