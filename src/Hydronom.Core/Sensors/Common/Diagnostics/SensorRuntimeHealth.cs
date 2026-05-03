using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Diagnostics
{
    /// <summary>
    /// TÃ¼m sensÃ¶r runtime'Ä±n saÄŸlÄ±k Ã¶zeti.
    /// </summary>
    public readonly record struct SensorRuntimeHealth(
        DateTime TimestampUtc,
        SensorRuntimeMode RuntimeMode,
        IReadOnlyList<SensorHealthSnapshot> Sensors,
        int SensorCount,
        int HealthyCount,
        int DegradedCount,
        int StaleCount,
        int OfflineCount,
        bool HasCriticalIssue,
        string Summary
    )
    {
        public static SensorRuntimeHealth FromSensors(
            SensorRuntimeMode mode,
            IReadOnlyList<SensorHealthSnapshot>? sensors
        )
        {
            sensors ??= Array.Empty<SensorHealthSnapshot>();

            var safeSensors = sensors.Select(s => s.Sanitized()).ToArray();

            var healthy = safeSensors.Count(s => s.State == SensorHealthState.Healthy || s.State == SensorHealthState.Simulated);
            var degraded = safeSensors.Count(s => s.State == SensorHealthState.Degraded);
            var stale = safeSensors.Count(s => s.State == SensorHealthState.Stale);
            var offline = safeSensors.Count(s => s.State == SensorHealthState.Offline || s.State == SensorHealthState.Failing);

            return new SensorRuntimeHealth(
                TimestampUtc: DateTime.UtcNow,
                RuntimeMode: mode,
                Sensors: safeSensors,
                SensorCount: safeSensors.Length,
                HealthyCount: healthy,
                DegradedCount: degraded,
                StaleCount: stale,
                OfflineCount: offline,
                HasCriticalIssue: offline > 0,
                Summary: $"Sensors={safeSensors.Length}, healthy={healthy}, degraded={degraded}, stale={stale}, offline/failing={offline}"
            );
        }
    }
}

