namespace Hydronom.Core.Fusion.Models;

/// <summary>
/// Fusion sürecinde kullanılan zaman, frame, sensor source ve runtime bağlamı.
/// </summary>
public readonly record struct FusionContext(
    string VehicleId,
    DateTime TimestampUtc,
    string FrameId,
    double MaxSampleAgeMs,
    string TraceId
)
{
    public static FusionContext Create(
        string vehicleId,
        string frameId = "map",
        double maxSampleAgeMs = 1000.0,
        string traceId = "")
    {
        return new FusionContext(
            VehicleId: string.IsNullOrWhiteSpace(vehicleId) ? "UNKNOWN" : vehicleId.Trim(),
            TimestampUtc: DateTime.UtcNow,
            FrameId: string.IsNullOrWhiteSpace(frameId) ? "map" : frameId.Trim(),
            MaxSampleAgeMs: maxSampleAgeMs <= 0.0 || !double.IsFinite(maxSampleAgeMs) ? 1000.0 : maxSampleAgeMs,
            TraceId: string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId.Trim()
        );
    }

    public FusionContext Sanitized()
    {
        return new FusionContext(
            VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            FrameId: string.IsNullOrWhiteSpace(FrameId) ? "map" : FrameId.Trim(),
            MaxSampleAgeMs: MaxSampleAgeMs <= 0.0 || !double.IsFinite(MaxSampleAgeMs) ? 1000.0 : MaxSampleAgeMs,
            TraceId: string.IsNullOrWhiteSpace(TraceId) ? Guid.NewGuid().ToString("N") : TraceId.Trim()
        );
    }
}