namespace Hydronom.Runtime.Sensors.Diagnostics;

/// <summary>
/// Runtime seviyesinde aktif sensör capability görünümü.
/// Mission/Decision katmanı ileride sensör adına değil bu capability snapshot'a bakmalıdır.
/// </summary>
public readonly record struct RuntimeSensorCapabilitySnapshot(
    DateTime TimestampUtc,
    IReadOnlyList<RuntimeSensorCapabilityEntry> Capabilities,
    int CapabilityCount,
    int AvailableCount,
    int DegradedCount,
    int MissingCount,
    bool HasGlobalPosition,
    bool HasLocalPosition,
    bool HasAttitude,
    bool HasDepth,
    bool HasObstacleDetection,
    string Summary
)
{
    public static RuntimeSensorCapabilitySnapshot Empty => new(
        TimestampUtc: DateTime.UtcNow,
        Capabilities: Array.Empty<RuntimeSensorCapabilityEntry>(),
        CapabilityCount: 0,
        AvailableCount: 0,
        DegradedCount: 0,
        MissingCount: 0,
        HasGlobalPosition: false,
        HasLocalPosition: false,
        HasAttitude: false,
        HasDepth: false,
        HasObstacleDetection: false,
        Summary: "No runtime sensor capabilities."
    );

    public RuntimeSensorCapabilitySnapshot Sanitized()
    {
        var capabilities = (Capabilities ?? Array.Empty<RuntimeSensorCapabilityEntry>())
            .Select(x => x.Sanitized())
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Confidence).First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var available = capabilities.Count(x => x.Status == RuntimeSensorCapabilityStatus.Available);
        var degraded = capabilities.Count(x => x.Status == RuntimeSensorCapabilityStatus.Degraded);
        var missing = capabilities.Count(x => x.Status == RuntimeSensorCapabilityStatus.Missing);

        return new RuntimeSensorCapabilitySnapshot(
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            Capabilities: capabilities,
            CapabilityCount: capabilities.Length,
            AvailableCount: available,
            DegradedCount: degraded,
            MissingCount: missing,
            HasGlobalPosition: HasAvailable(capabilities, "global_position"),
            HasLocalPosition: HasAvailable(capabilities, "local_position"),
            HasAttitude: HasAvailable(capabilities, "attitude_estimation"),
            HasDepth: HasAvailable(capabilities, "depth"),
            HasObstacleDetection: HasAvailable(capabilities, "obstacle_detection"),
            Summary: string.IsNullOrWhiteSpace(Summary)
                ? BuildSummary(capabilities, available, degraded, missing)
                : Summary.Trim()
        );
    }

    public bool Has(string capabilityName, double minConfidence = 0.0)
    {
        if (string.IsNullOrWhiteSpace(capabilityName))
            return false;

        var safeName = capabilityName.Trim();

        return Sanitized().Capabilities.Any(x =>
            string.Equals(x.Name, safeName, StringComparison.OrdinalIgnoreCase) &&
            x.Status != RuntimeSensorCapabilityStatus.Missing &&
            x.Confidence >= minConfidence
        );
    }

    public RuntimeSensorCapabilityEntry Get(string capabilityName)
    {
        if (string.IsNullOrWhiteSpace(capabilityName))
            return RuntimeSensorCapabilityEntry.Missing("capability");

        var safeName = capabilityName.Trim();

        return Sanitized().Capabilities
            .Where(x => string.Equals(x.Name, safeName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Confidence)
            .FirstOrDefault(RuntimeSensorCapabilityEntry.Missing(safeName));
    }

    private static bool HasAvailable(
        IReadOnlyList<RuntimeSensorCapabilityEntry> capabilities,
        string name)
    {
        return capabilities.Any(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            x.Status == RuntimeSensorCapabilityStatus.Available);
    }

    private static string BuildSummary(
        IReadOnlyList<RuntimeSensorCapabilityEntry> capabilities,
        int available,
        int degraded,
        int missing)
    {
        if (capabilities.Count == 0)
            return "capabilities=0";

        var compact = string.Join(
            ", ",
            capabilities.Select(x => $"{x.Name}:{x.Status.ToString().ToLowerInvariant()}@{x.Confidence:F2}")
        );

        return $"capabilities={capabilities.Count}, available={available}, degraded={degraded}, missing={missing}; {compact}";
    }
}