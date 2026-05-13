using HydronomOps.Gateway.Contracts.Diagnostics;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private void LogRawSampleIfNeeded(string type, string line)
    {
        if (!ShouldDebugType(type))
        {
            return;
        }

        var count = _debugLogCounts.AddOrUpdate(type, 1, (_, current) => current + 1);
        if (count > MaxDebugLogsPerType)
        {
            return;
        }

        _stateStore.AddLog(new GatewayLogDto
        {
            Level = "debug",
            Category = "parser-sample",
            Message = $"Ham runtime örneği alındı. Type={type}, Index={count}",
            Detail = TrimForLog(line),
            TimestampUtc = DateTime.UtcNow
        });
    }

    private static bool ShouldDebugType(string type)
    {
        return string.Equals(type, "FusedState", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Sample", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "ExternalState", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "RuntimeTelemetrySummary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "RuntimeSummary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "RuntimeMissionState", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "RuntimeActuatorState", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "RuntimeWorldObjects", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "TwinImu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "TwinGps", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimForLog(string value, int maxLength = 4000)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + " ...[truncated]";
    }
}