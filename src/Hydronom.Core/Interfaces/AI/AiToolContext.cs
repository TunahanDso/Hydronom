using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hydronom.Core.Interfaces.AI;

// Tool'ların runtime'a erişmesi için güvenli bağlam.
// Burayı zamanla genişleteceğiz (TaskManager, CommandServer, Telemetry, vs.)
public sealed class AiToolContext
{
    public string RuntimeInstanceId { get; }
    public IReadOnlyDictionary<string, object?> Capabilities { get; }

    // Runtime sırasında güncellenebilir snapshot.
    public IReadOnlyDictionary<string, object?> TelemetrySnapshot { get; private set; }

    public AiToolContext(
        string runtimeInstanceId,
        IReadOnlyDictionary<string, object?> capabilities,
        IReadOnlyDictionary<string, object?> telemetrySnapshot)
    {
        RuntimeInstanceId = RequireText(runtimeInstanceId, nameof(runtimeInstanceId));
        Capabilities = NormalizeDictionary(capabilities);
        TelemetrySnapshot = NormalizeDictionary(telemetrySnapshot);
    }

    public void UpdateTelemetrySnapshot(IReadOnlyDictionary<string, object?> telemetrySnapshot)
    {
        TelemetrySnapshot = NormalizeDictionary(telemetrySnapshot);
    }

    private static string RequireText(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Alan boş olamaz.", paramName);

        return value.Trim();
    }

    private static IReadOnlyDictionary<string, object?> NormalizeDictionary(IReadOnlyDictionary<string, object?>? source)
    {
        if (source is null || source.Count == 0)
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            normalized[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(normalized);
    }
}