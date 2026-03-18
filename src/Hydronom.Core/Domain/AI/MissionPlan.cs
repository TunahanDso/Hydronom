using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain.AI;

public sealed record MissionPlan(
    string Id,
    string Goal,
    IReadOnlyList<MissionStep> Steps,
    DateTime CreatedUtc
)
{
    public static MissionPlan Create(string goal, IReadOnlyList<MissionStep> steps)
        => new MissionPlan(Guid.NewGuid().ToString("N"), goal, steps, DateTime.UtcNow);
}

public sealed record MissionStep(
    int Index,
    string Title,
    string Description,
    IReadOnlyList<string> ExpectedTools
);