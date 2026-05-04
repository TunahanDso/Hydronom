using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Fusion.Quality;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Gps.Models;
using Hydronom.Core.Sensors.Imu.Models;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.Fusion.Estimation;

/// <summary>
/// Başlangıç seviyesi GPS + IMU tabanlı state estimator.
/// GPS lokal X/Y/Z ve hız/course bilgisini, IMU attitude/yaw-rate bilgisiyle birleştirir.
/// </summary>
public sealed class GpsImuStateEstimator : StateEstimator
{
    public GpsImuStateEstimator()
        : base("gps_imu_state_estimator")
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

        var gpsSample = usableSamples
            .Where(x => x.DataKind == SensorDataKind.Gps && x.Data is GpsSampleData)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefault();

        var imuSample = usableSamples
            .Where(x => x.DataKind == SensorDataKind.Imu && x.Data is ImuSampleData)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefault();

        if (gpsSample.Data is not GpsSampleData gps)
        {
            LastDiagnostics = BuildDiagnostics(
                inputSamples,
                used: Array.Empty<SensorSample>(),
                rejected: rejected,
                confidence: 0.0,
                produced: false,
                summary: "GPS sample bulunamadı."
            );

            return null;
        }

        if (!gps.HasLocalPosition)
        {
            LastDiagnostics = BuildDiagnostics(
                inputSamples,
                used: Array.Empty<SensorSample>(),
                rejected: rejected.Append(gpsSample.SampleId).ToArray(),
                confidence: 0.0,
                produced: false,
                summary: "GPS local X/Y position yok."
            );

            return null;
        }

        var hasImu = imuSample.Data is ImuSampleData;
        var imu = hasImu ? (ImuSampleData)imuSample.Data! : ImuSampleData.Zero;

        var timestamp = MaxTimestamp(gpsSample.TimestampUtc, hasImu ? imuSample.TimestampUtc : gpsSample.TimestampUtc);

        var yawDeg = imu.YawDeg ?? gps.CourseDeg ?? 0.0;
        var rollDeg = imu.RollDeg ?? 0.0;
        var pitchDeg = imu.PitchDeg ?? 0.0;

        var yawRateDegSec = hasImu
            ? RadToDeg(imu.GzRadSec)
            : 0.0;

        var speed = gps.SpeedMps ?? 0.0;
        var courseDeg = gps.CourseDeg ?? yawDeg;
        var courseRad = courseDeg * Math.PI / 180.0;

        var vx = speed * Math.Cos(courseRad);
        var vy = speed * Math.Sin(courseRad);
        var vz = 0.0;

        var pose = new VehiclePose(
            X: gps.X ?? 0.0,
            Y: gps.Y ?? 0.0,
            Z: gps.Z ?? gps.AltitudeMeters ?? 0.0,
            YawDeg: yawDeg,
            FrameId: safeContext.FrameId
        ).Sanitized();

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

        var usedSamples = hasImu
            ? new[] { gpsSample, imuSample }
            : new[] { gpsSample };

        var freshnessMs = usedSamples
            .Select(x => Math.Max(0.0, (safeContext.TimestampUtc - x.TimestampUtc).TotalMilliseconds))
            .DefaultIfEmpty(0.0)
            .Max();

        var confidence = ComputeConfidence(gps, hasImu);

        var quality = FusionQuality.FromInputs(
            confidence: confidence,
            freshnessMs: freshnessMs,
            usedSampleCount: usedSamples.Length,
            rejectedSampleCount: rejected.Length,
            summary: hasImu
                ? "GPS local position + IMU attitude fusion."
                : "GPS local position fusion without IMU."
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

    private static double ComputeConfidence(GpsSampleData gps, bool hasImu)
    {
        var gpsConfidence = 0.65;

        if (gps.Hdop.HasValue)
        {
            gpsConfidence = gps.Hdop.Value switch
            {
                <= 0.0 => 0.20,
                <= 0.8 => 0.95,
                <= 1.5 => 0.85,
                <= 2.5 => 0.70,
                <= 5.0 => 0.45,
                _ => 0.25
            };
        }

        if (gps.FixType <= 0)
        {
            gpsConfidence *= 0.35;
        }

        if (gps.Satellites < 4)
        {
            gpsConfidence *= 0.60;
        }

        if (hasImu)
        {
            gpsConfidence += 0.10;
        }

        return Clamp01(gpsConfidence);
    }

    private static DateTime MaxTimestamp(DateTime a, DateTime b)
    {
        if (a == default)
        {
            return b == default ? DateTime.UtcNow : b;
        }

        if (b == default)
        {
            return a;
        }

        return a > b ? a : b;
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