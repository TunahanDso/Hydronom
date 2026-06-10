namespace Hydronom.Runtime.Sensors.Diagnostics;

/// <summary>
/// Runtime'ın görev/decision katmanına sunacağı tek capability durumu.
/// Sensör adı yerine capability adı merkez alınır.
/// </summary>
public readonly record struct RuntimeSensorCapabilityEntry(
    string Name,
    RuntimeSensorCapabilityStatus Status,
    double Confidence,
    string Provider,
    string FrameId,
    double TargetRateHz,
    bool RequiredCalibration,
    bool CalibrationValid,
    string Summary
)
{
    public RuntimeSensorCapabilityEntry Sanitized()
    {
        var name = Normalize(Name, "capability");
        var status = Status == RuntimeSensorCapabilityStatus.Unknown
            ? ResolveStatus(Confidence)
            : Status;

        var confidence = Clamp01(Confidence);

        return new RuntimeSensorCapabilityEntry(
            Name: name,
            Status: status,
            Confidence: confidence,
            Provider: Normalize(Provider, "missing"),
            FrameId: Normalize(FrameId, "base_link"),
            TargetRateHz: SafeNonNegative(TargetRateHz),
            RequiredCalibration: RequiredCalibration,
            CalibrationValid: CalibrationValid,
            Summary: string.IsNullOrWhiteSpace(Summary)
                ? BuildSummary(name, status, confidence, Provider)
                : Summary.Trim()
        );
    }

    public static RuntimeSensorCapabilityEntry Missing(string name)
    {
        var safeName = Normalize(name, "capability");

        return new RuntimeSensorCapabilityEntry(
            Name: safeName,
            Status: RuntimeSensorCapabilityStatus.Missing,
            Confidence: 0.0,
            Provider: "missing",
            FrameId: "unknown",
            TargetRateHz: 0.0,
            RequiredCalibration: false,
            CalibrationValid: false,
            Summary: $"{safeName}=missing"
        );
    }

    public static RuntimeSensorCapabilityStatus ResolveStatus(double confidence)
    {
        var c = Clamp01(confidence);

        if (c <= 0.0)
            return RuntimeSensorCapabilityStatus.Missing;

        if (c < 0.50)
            return RuntimeSensorCapabilityStatus.Degraded;

        return RuntimeSensorCapabilityStatus.Available;
    }

    private static string BuildSummary(
        string name,
        RuntimeSensorCapabilityStatus status,
        double confidence,
        string provider)
    {
        return $"{name}={status.ToString().ToLowerInvariant()} confidence={confidence:F2} provider={Normalize(provider, "missing")}";
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static double SafeNonNegative(double value)
    {
        if (!double.IsFinite(value))
            return 0.0;

        return value < 0.0 ? 0.0 : value;
    }

    private static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
            return 0.0;

        if (value < 0.0)
            return 0.0;

        if (value > 1.0)
            return 1.0;

        return value;
    }
}