using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.State.Authority;

namespace Hydronom.Core.Fusion.Abstractions;

/// <summary>
/// SensorSample listesinden fusion/state estimation çıktısı üreten ortak Core sözleşmesi.
/// Fusion katmanı doğrudan authoritative state yazmaz; StateUpdateCandidate üretir.
/// </summary>
public interface IFusionEngine
{
    string Name { get; }

    FusionDiagnostics LastDiagnostics { get; }

    StateUpdateCandidate? Estimate(
        IReadOnlyList<SensorSample> samples,
        FusionContext context);

    FusedState? Fuse(
        IReadOnlyList<SensorSample> samples,
        FusionContext context);
}