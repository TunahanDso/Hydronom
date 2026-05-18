using System;
using System.Collections.Generic;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Policies;

public sealed record AiAuthorityPolicy(
    AiMode Mode,
    bool AllowRuntimeCommandEmission,
    bool RequireHumanApprovalForMissionStart,
    int MaxPlanSteps,
    IReadOnlySet<string> ForbiddenPhrases
)
{
    public static AiAuthorityPolicy SuggestOnly()
        => new(
            Mode: AiMode.Suggest,
            AllowRuntimeCommandEmission: false,
            RequireHumanApprovalForMissionStart: true,
            MaxPlanSteps: 12,
            ForbiddenPhrases: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "disable safety",
                "bypass safety",
                "ignore safety",
                "force actuator",
                "direct motor command",
                "override emergency",
                "emergency bypass",
                "güvenliği kapat",
                "emniyeti kapat",
                "güvenliği atla",
                "doğrudan motor",
                "motorları zorla",
                "acil durdurmayı atla"
            }
        );

    public static AiAuthorityPolicy AdvisoryAutopilot()
        => new(
            Mode: AiMode.Autopilot,
            AllowRuntimeCommandEmission: false,
            RequireHumanApprovalForMissionStart: true,
            MaxPlanSteps: 16,
            ForbiddenPhrases: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "disable safety",
                "bypass safety",
                "ignore safety",
                "override emergency",
                "emergency bypass",
                "güvenliği kapat",
                "emniyeti kapat",
                "acil durdurmayı atla"
            }
        );
}
