using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.State.Authority;

namespace Hydronom.Runtime.FusionRuntime;

/// <summary>
/// StateEstimator çağrılarını runtime loop içinde yöneten uygulama katmanı.
/// Runtime doğrudan estimator detaylarını bilmek zorunda kalmaz.
/// </summary>
public sealed class StateEstimatorRunner
{
    private readonly StateEstimator _estimator;

    public StateEstimatorRunner(StateEstimator estimator)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
    }

    public string EstimatorName => _estimator.Name;

    public FusionDiagnostics LastDiagnostics => _estimator.LastDiagnostics;

    public StateUpdateCandidate? Run(
        IReadOnlyList<SensorSample> samples,
        FusionContext context)
    {
        var candidate = _estimator.Estimate(samples, context);
        return candidate?.Sanitized();
    }
}