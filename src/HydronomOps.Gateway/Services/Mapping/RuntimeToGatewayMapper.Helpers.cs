using System.Globalization;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Services.Mapping;

public sealed partial class RuntimeToGatewayMapper
{
    private FreshnessDto BuildFreshness(DateTime timestampUtc, string source)
    {
        var now = _clock.UtcNow;
        var ageMs = Math.Max(0, (long)(now - timestampUtc).TotalMilliseconds);
        const int thresholdMs = 5000;

        return new FreshnessDto
        {
            TimestampUtc = timestampUtc,
            AgeMs = ageMs,
            IsFresh = ageMs <= thresholdMs,
            ThresholdMs = thresholdMs,
            Source = source
        };
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out element))
            {
                return true;
            }
        }

        element = default;
        return false;
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.String)
            {
                return p.GetString();
            }

            if (p.ValueKind == JsonValueKind.Number ||
                p.ValueKind == JsonValueKind.True ||
                p.ValueKind == JsonValueKind.False)
            {
                return p.ToString();
            }
        }

        return null;
    }

    private static double GetDouble(JsonElement root, params string[] names)
    {
        return GetNullableDouble(root, names) ?? 0.0;
    }

    private static double? GetNullableDouble(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String &&
                double.TryParse(
                    p.GetString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int GetInt(JsonElement root, int fallback = 0, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String &&
                int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static long GetLong(JsonElement root, long fallback = 0, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String &&
                long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static bool GetBool(JsonElement root, params string[] names)
    {
        return GetBool(root, false, names);
    }

    private static bool GetBool(JsonElement root, bool fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
            {
                return p.GetBoolean();
            }

            if (p.ValueKind == JsonValueKind.String &&
                bool.TryParse(p.GetString(), out var parsed))
            {
                return parsed;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var intValue))
            {
                return intValue != 0;
            }
        }

        return fallback;
    }

    private static DateTime? GetNullableDateTime(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(
                    p.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                return dt;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var epochSeconds))
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds((long)(epochSeconds * 1000)).UtcDateTime;
                }
                catch
                {
                    // Geçersiz epoch değeri güvenli şekilde yok sayılır.
                }
            }
        }

        return null;
    }

    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double NormalizeAngleDeg(double deg)
    {
        while (deg > 180.0)
        {
            deg -= 360.0;
        }

        while (deg < -180.0)
        {
            deg += 360.0;
        }

        return deg;
    }
}