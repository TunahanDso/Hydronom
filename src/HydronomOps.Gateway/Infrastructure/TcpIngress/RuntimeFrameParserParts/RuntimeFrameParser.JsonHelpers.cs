using System.Globalization;
using System.Text.Json;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private static DateTime ReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestampUtc", out var ts1) &&
            ts1.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(
                ts1.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed1))
        {
            return parsed1;
        }

        if (root.TryGetProperty("TimestampUtc", out var ts2) &&
            ts2.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(
                ts2.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed2))
        {
            return parsed2;
        }

        if (root.TryGetProperty("t_imu", out var imuTs) &&
            imuTs.ValueKind == JsonValueKind.Number &&
            imuTs.TryGetDouble(out var imuEpoch))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(imuEpoch * 1000)).UtcDateTime;
        }

        if (root.TryGetProperty("t_gps", out var gpsTs) &&
            gpsTs.ValueKind == JsonValueKind.Number &&
            gpsTs.TryGetDouble(out var gpsEpoch))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(gpsEpoch * 1000)).UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    private static double? TryReadDouble(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String &&
                double.TryParse(
                    element.GetString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool? TryReadBool(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.String &&
                bool.TryParse(element.GetString(), out var parsed))
            {
                return parsed;
            }

            if (element.ValueKind == JsonValueKind.Number &&
                element.TryGetInt32(out var number))
            {
                return number != 0;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }
        }

        return null;
    }

    private static string? ReadType(JsonElement root)
    {
        if (root.TryGetProperty("type", out var typeElement) &&
            typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString();
        }

        if (root.TryGetProperty("Type", out var typeElementPascal) &&
            typeElementPascal.ValueKind == JsonValueKind.String)
        {
            return typeElementPascal.GetString();
        }

        return null;
    }
}