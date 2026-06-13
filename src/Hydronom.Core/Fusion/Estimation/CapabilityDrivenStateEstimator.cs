using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Fusion.Quality;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Depth.Models;
using Hydronom.Core.Sensors.Gps.Models;
using Hydronom.Core.Sensors.Imu.Models;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.Fusion.Estimation;

/// <summary>
/// Capability Driven State Estimation.
/// 
/// Bu estimator sensör isimlerine değil, eldeki kullanılabilir bilgiye ve capability fikrine göre
/// en iyi state tahminini üretmeye çalışır.
/// 
/// Temel prensip:
/// State sahibi sensör değildir; Hydronom'dur.
/// Sensörler yalnızca state üretimini besleyen kaynaklardır.
/// </summary>
public sealed class CapabilityDrivenStateEstimator : StateEstimator
{
    private FusedState? _lastEstimate;

    public CapabilityDrivenStateEstimator()
        : base("capability_driven_state_estimator")
    {
    }

    public override FusedState? Fuse(
        IReadOnlyList<SensorSample> samples,
        FusionContext context)
    {
        var safeContext = context.Sanitized();
        var inputSamples = samples ?? Array.Empty<SensorSample>();

        if (inputSamples.Count == 0)
        {
            LastDiagnostics = FusionDiagnostics.Empty(Name);
            return null;
        }

        var usableSamples = inputSamples
            .Where(x => x.IsValid && !x.IsStale(safeContext.MaxSampleAgeMs, safeContext.TimestampUtc))
            .Select(x => x.Sanitized())
            .ToArray();

        var rejected = inputSamples
            .Where(x => !usableSamples.Any(u => u.SampleId == x.SampleId))
            .Select(x => x.SampleId)
            .ToArray();

        var gpsSample = LatestSample<GpsSampleData>(usableSamples);
        var imuSample = LatestSample<ImuSampleData>(usableSamples);
        var depthSample = LatestSample<DepthSampleData>(usableSamples);

        var hasGps = gpsSample.Data is GpsSampleData gpsRaw && gpsRaw.Sanitized().HasLocalPosition;
        var hasImu = imuSample.Data is ImuSampleData;
        var hasDepth = depthSample.Data is DepthSampleData depthRaw && depthRaw.Sanitized().Valid;

        if (!hasGps && !hasImu && !hasDepth)
        {
            LastDiagnostics = BuildDiagnostics(
                inputSamples,
                used: Array.Empty<SensorSample>(),
                rejected: rejected,
                confidence: 0.0,
                produced: false,
                summary: "CDSE usable GPS/IMU/Depth sample bulamadı."
            );

            return null;
        }

        var usedSamples = new List<SensorSample>();

        if (hasGps)
        {
            usedSamples.Add(gpsSample);
        }

        if (hasImu)
        {
            usedSamples.Add(imuSample);
        }

        if (hasDepth)
        {
            usedSamples.Add(depthSample);
        }

        var gps = hasGps ? ((GpsSampleData)gpsSample.Data!).Sanitized() : GpsSampleData.Empty;
        var imu = hasImu ? ((ImuSampleData)imuSample.Data!).Sanitized() : ImuSampleData.Zero;
        var depth = hasDepth ? ((DepthSampleData)depthSample.Data!).Sanitized() : DepthSampleData.Empty;

        var last = _lastEstimate?.Sanitized();

        var timestamp = MaxTimestamp(usedSamples.Select(x => x.TimestampUtc));

        var yawDeg = FirstFinite(
            imu.YawDeg,
            gps.CourseDeg,
            last?.Pose.YawDeg,
            0.0);

        var rollDeg = FirstFinite(
            imu.RollDeg,
            last?.Attitude.RollDeg,
            0.0);

        var pitchDeg = FirstFinite(
            imu.PitchDeg,
            last?.Attitude.PitchDeg,
            0.0);

        var yawRateDegSec = hasImu
            ? RadToDeg(imu.GzRadSec)
            : last?.Twist.YawRateDegSec ?? 0.0;

        var z = hasDepth
            ? -depth.DepthMeters
            : FirstFinite(
                gps.Z,
                gps.AltitudeMeters,
                last?.Pose.Z,
                0.0);

        var x = hasGps
            ? gps.X ?? 0.0
            : last?.Pose.X ?? 0.0;

        var y = hasGps
            ? gps.Y ?? 0.0
            : last?.Pose.Y ?? 0.0;

        var pose = new VehiclePose(
            X: x,
            Y: y,
            Z: z,
            YawDeg: yawDeg,
            FrameId: safeContext.FrameId
        ).Sanitized();

        var speed = gps.SpeedMps ?? 0.0;
        var courseDeg = gps.CourseDeg ?? yawDeg;
        var courseRad = courseDeg * Math.PI / 180.0;

        var vx = hasGps && gps.SpeedMps.HasValue
            ? speed * Math.Cos(courseRad)
            : 0.0;

        var vy = hasGps && gps.SpeedMps.HasValue
            ? speed * Math.Sin(courseRad)
            : 0.0;

        var vz = EstimateVerticalSpeed(last, pose, timestamp);

        var twist = new VehicleTwist(
            Vx: vx,
            Vy: vy,
            Vz: vz,
            YawRateDegSec: yawRateDegSec
        ).Sanitized();

        var attitude = new VehicleAttitude(
            RollDeg: rollDeg,
            PitchDeg: pitchDeg,
            YawDeg: yawDeg,
            RollRateDegSec: hasImu ? RadToDeg(imu.GxRadSec) : 0.0,
            PitchRateDegSec: hasImu ? RadToDeg(imu.GyRadSec) : 0.0,
            YawRateDegSec: yawRateDegSec
        ).Sanitized();

        var freshnessMs = usedSamples
            .Select(x => Math.Max(0.0, (safeContext.TimestampUtc - x.TimestampUtc).TotalMilliseconds))
            .DefaultIfEmpty(0.0)
            .Max();

        var confidence = ComputeConfidence(
            gps,
            imuSample,
            depthSample,
            hasGps,
            hasImu,
            hasDepth,
            last.HasValue);

        var modeSummary = BuildModeSummary(hasGps, hasImu, hasDepth, last.HasValue);

        var quality = FusionQuality.FromInputs(
            confidence: confidence,
            freshnessMs: freshnessMs,
            usedSampleCount: usedSamples.Count,
            rejectedSampleCount: rejected.Length,
            summary: modeSummary
        );

        var fused = new FusedState(
            VehicleId: safeContext.VehicleId,
            TimestampUtc: timestamp,
            Pose: pose,
            Twist: twist,
            Attitude: attitude,
            Quality: quality,
            FrameId: safeContext.FrameId,
            InputSampleIds: usedSamples.Select(x => x.SampleId).ToArray(),
            TraceId: safeContext.TraceId
        ).Sanitized();

        _lastEstimate = fused;

        LastDiagnostics = BuildDiagnostics(
            inputSamples,
            usedSamples,
            rejected,
            confidence,
            produced: true,
            summary: quality.Summary
        );

        return fused;
    }

    private static SensorSample LatestSample<T>(IReadOnlyList<SensorSample> samples)
        where T : struct
    {
        return samples
            .Where(x => x.Data is T)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefault();
    }

    private static double ComputeConfidence(
        GpsSampleData gps,
        SensorSample imuSample,
        SensorSample depthSample,
        bool hasGps,
        bool hasImu,
        bool hasDepth,
        bool hasLastEstimate)
    {
        var confidence = 0.0;

        if (hasGps)
        {
            var gpsConfidence = 0.55;

            if (gps.Hdop.HasValue)
            {
                gpsConfidence = gps.Hdop.Value switch
                {
                    <= 0.0 => 0.12,
                    <= 0.8 => 0.62,
                    <= 1.5 => 0.56,
                    <= 2.5 => 0.46,
                    <= 5.0 => 0.30,
                    _ => 0.18
                };
            }

            if (gps.FixType <= 0)
            {
                gpsConfidence *= 0.35;
            }

            if (gps.Satellites > 0 && gps.Satellites < 4)
            {
                gpsConfidence *= 0.60;
            }

            confidence += gpsConfidence;
        }

        if (hasImu)
        {
            confidence += 0.24 * imuSample.Quality.Confidence;
        }

        if (hasDepth)
        {
            confidence += 0.18 * depthSample.Quality.Confidence;
        }

        if (!hasGps && hasLastEstimate)
        {
            confidence += 0.08;
        }

        if (!hasGps && hasImu && hasDepth)
        {
            confidence += 0.08;
        }

        if (!hasGps && (hasImu || hasDepth))
        {
            confidence = Math.Min(confidence, 0.48);
        }

        return Clamp01(confidence);
    }

    private static string BuildModeSummary(
        bool hasGps,
        bool hasImu,
        bool hasDepth,
        bool hasLastEstimate)
    {
        var parts = new List<string>
        {
            "CDSE"
        };

        parts.Add(hasGps ? "gps=ok" : "gps=missing");
        parts.Add(hasImu ? "imu=ok" : "imu=missing");
        parts.Add(hasDepth ? "depth=ok" : "depth=missing");

        if (!hasGps)
        {
            parts.Add(hasLastEstimate ? "xy=held_from_last_estimate" : "xy=low_confidence_origin");
            parts.Add("mode=degraded_estimated");
        }
        else
        {
            parts.Add("mode=measured_estimated");
        }

        return string.Join("; ", parts);
    }

    private FusionDiagnostics BuildDiagnostics(
        IReadOnlyList<SensorSample> input,
        IReadOnlyList<SensorSample> used,
        IReadOnlyList<string> rejected,
        double confidence,
        bool produced,
        string summary)
    {
        return new FusionDiagnostics(
            FusionEngineName: Name,
            TimestampUtc: DateTime.UtcNow,
            InputSampleCount: input.Count,
            UsedSampleCount: used.Count,
            RejectedSampleCount: rejected.Count,
            UsedSensorIds: used
                .Select(x => x.Sensor.SensorId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            RejectedSampleIds: rejected,
            Confidence: confidence,
            ProducedCandidate: produced,
            Summary: summary
        ).Sanitized();
    }

    private static DateTime MaxTimestamp(IEnumerable<DateTime> timestamps)
    {
        var values = timestamps
            .Where(x => x != default)
            .ToArray();

        if (values.Length == 0)
        {
            return DateTime.UtcNow;
        }

        return values.Max();
    }

    private static double EstimateVerticalSpeed(
        FusedState? last,
        VehiclePose pose,
        DateTime timestamp)
    {
        if (!last.HasValue)
        {
            return 0.0;
        }

        var previous = last.Value.Sanitized();
        var dt = (timestamp - previous.TimestampUtc).TotalSeconds;

        if (!double.IsFinite(dt) || dt <= 1e-6)
        {
            return 0.0;
        }

        return (pose.Z - previous.Pose.Z) / dt;
    }

    private static double FirstFinite(
        double? first,
        double? second,
        double? third,
        double fallback)
    {
        if (first.HasValue && double.IsFinite(first.Value))
        {
            return first.Value;
        }

        if (second.HasValue && double.IsFinite(second.Value))
        {
            return second.Value;
        }

        if (third.HasValue && double.IsFinite(third.Value))
        {
            return third.Value;
        }

        return double.IsFinite(fallback) ? fallback : 0.0;
    }

    private static double FirstFinite(
        double? first,
        double? second,
        double fallback)
    {
        if (first.HasValue && double.IsFinite(first.Value))
        {
            return first.Value;
        }

        if (second.HasValue && double.IsFinite(second.Value))
        {
            return second.Value;
        }

        return double.IsFinite(fallback) ? fallback : 0.0;
    }

    private static double RadToDeg(double rad)
    {
        return rad * 180.0 / Math.PI;
    }

    private static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}
