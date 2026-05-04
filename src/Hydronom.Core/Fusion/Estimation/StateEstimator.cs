using Hydronom.Core.Fusion.Abstractions;
using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.State.Authority;

namespace Hydronom.Core.Fusion.Estimation;

/// <summary>
/// SensorSample verilerinden StateUpdateCandidate üreten estimator katmanı.
/// </summary>
public abstract class StateEstimator : IFusionEngine
{
    protected StateEstimator(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "state_estimator" : name.Trim();
        LastDiagnostics = FusionDiagnostics.Empty(Name);
    }

    public string Name { get; }

    public FusionDiagnostics LastDiagnostics { get; protected set; }

    public StateUpdateCandidate? Estimate(
        IReadOnlyList<SensorSample> samples,
        FusionContext context)
    {
        var fused = Fuse(samples, context);

        if (fused is null || !fused.Value.IsValid)
        {
            return null;
        }

        return fused.Value.ToCandidate(
            sourceKind: VehicleStateSourceKind.CSharpFusion,
            reason: $"{Name}: fusion estimate"
        );
    }

    public abstract FusedState? Fuse(
        IReadOnlyList<SensorSample> samples,
        FusionContext context);
}