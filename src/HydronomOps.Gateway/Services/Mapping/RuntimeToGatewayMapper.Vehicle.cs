using System.Globalization;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Services.Mapping;

public sealed partial class RuntimeToGatewayMapper
{
    public VehicleTelemetryDto MapVehicleTelemetry(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc =
            GetNullableDateTime(root, "timestampUtc", "TimestampUtc", "t") ??
            GetNullableDateTime(root, "t_imu", "t_gps") ??
            now;

        var vehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main";

        // ---------- top-level defaults ----------
        var x = GetDouble(root, "x", "X");
        var y = GetDouble(root, "y", "Y");
        var z = GetDouble(root, "z", "Z");

        var rollDeg = GetDouble(root, "rollDeg", "RollDeg", "roll_deg");
        var pitchDeg = GetDouble(root, "pitchDeg", "PitchDeg", "pitch_deg");
        var yawDeg = GetDouble(root, "yawDeg", "YawDeg", "yaw_deg");
        var headingDeg = GetDouble(root, "headingDeg", "HeadingDeg", "heading_deg", "yawDeg", "YawDeg", "yaw_deg");

        var vx = GetDouble(root, "vx", "Vx");
        var vy = GetDouble(root, "vy", "Vy");
        var vz = GetDouble(root, "vz", "Vz");

        var rollRateDeg = GetDouble(root, "rollRateDeg", "RollRateDeg", "roll_rate_deg");
        var pitchRateDeg = GetDouble(root, "pitchRateDeg", "PitchRateDeg", "pitch_rate_deg");
        var yawRateDeg = GetDouble(root, "yawRateDeg", "YawRateDeg", "yaw_rate_deg");

        var targetX = GetNullableDouble(root, "targetX", "TargetX");
        var targetY = GetNullableDouble(root, "targetY", "TargetY");

        var distanceToGoalM = GetNullableDouble(root, "distanceToGoalM", "DistanceToGoalM");
        var headingErrorDeg = GetNullableDouble(root, "headingErrorDeg", "HeadingErrorDeg");

        var obstacleAhead = GetBool(root, "obstacleAhead", "ObstacleAhead");
        var obstacleCount = GetInt(root, 0, "obstacleCount", "ObstacleCount");

        // ---------- FusedState.pose ----------
        if (TryGetProperty(root, out var pose, "pose", "Pose"))
        {
            x = GetDouble(pose, "x", "X");
            y = GetDouble(pose, "y", "Y");
            z = GetDouble(pose, "z", "Z");

            rollDeg = GetDouble(pose, "roll", "Roll", "rollDeg", "RollDeg", "roll_deg");
            pitchDeg = GetDouble(pose, "pitch", "Pitch", "pitchDeg", "PitchDeg", "pitch_deg");

            var poseYaw = GetNullableDouble(pose, "yaw", "Yaw", "yawDeg", "YawDeg", "yaw_deg");
            if (poseYaw.HasValue)
            {
                yawDeg = poseYaw.Value;
                headingDeg = poseYaw.Value;
            }
        }

        // ---------- FusedState.twist ----------
        if (TryGetProperty(root, out var twist, "twist", "Twist"))
        {
            vx = GetDouble(twist, "vx", "Vx");
            vy = GetDouble(twist, "vy", "Vy");
            vz = GetDouble(twist, "vz", "Vz");

            rollRateDeg = GetDouble(twist, "roll_rate", "Roll_Rate", "rollRateDeg", "RollRateDeg");
            pitchRateDeg = GetDouble(twist, "pitch_rate", "Pitch_Rate", "pitchRateDeg", "PitchRateDeg");

            var twistYawRate = GetNullableDouble(
                twist,
                "yaw_rate", "Yaw_Rate",
                "yawRate", "YawRate",
                "yawRateDeg", "YawRateDeg");

            if (twistYawRate.HasValue)
            {
                yawRateDeg = twistYawRate.Value;
            }
        }

        // ---------- target / goal ----------
        if (TryGetProperty(root, out var target, "target", "Target"))
        {
            targetX = GetNullableDouble(target, "x", "X");
            targetY = GetNullableDouble(target, "y", "Y");
        }
        else if (TryGetProperty(root, out var goal, "goal", "Goal"))
        {
            targetX = GetNullableDouble(goal, "x", "X");
            targetY = GetNullableDouble(goal, "y", "Y");
        }

        if (targetX.HasValue && targetY.HasValue)
        {
            var dx = targetX.Value - x;
            var dy = targetY.Value - y;
            distanceToGoalM = Math.Sqrt(dx * dx + dy * dy);

            var targetHeadingDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            headingErrorDeg = NormalizeAngleDeg(targetHeadingDeg - headingDeg);
        }

        // ---------- geometry extraction ----------
        var obstacles = ExtractRuntimeObstacles(root);
        var landmarks = ExtractLandmarks(root);

        // ---------- obstacle fallback from inputs.runtime_obstacles ----------
        if ((!obstacleAhead || obstacleCount == 0) &&
            TryGetRuntimeObstacleStats(root, x, y, headingDeg, out var computedAhead, out var computedCount))
        {
            obstacleAhead = computedAhead;
            obstacleCount = computedCount;
        }

        if (obstacleCount == 0 && obstacles.Count > 0)
        {
            obstacleCount = obstacles.Count;
        }

        // ---------- flattened telemetry/debug inputs ----------
        var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ExtractInputTelemetry(root, metrics, fields);

        // ---------- occupancy / mapping normalize ----------
        NormalizeOccupancyTelemetry(metrics, fields, landmarks);

        // Kullanışlı bazı üst seviye alanları da ayrıca ekleyelim.
        fields["mapper.vehicleId"] = vehicleId;
        fields["mapper.timestampUtc"] = timestampUtc.ToString("O", CultureInfo.InvariantCulture);

        metrics["vehicle.x"] = x;
        metrics["vehicle.y"] = y;
        metrics["vehicle.z"] = z;
        metrics["vehicle.headingDeg"] = headingDeg;
        metrics["vehicle.vx"] = vx;
        metrics["vehicle.vy"] = vy;
        metrics["vehicle.vz"] = vz;
        metrics["vehicle.obstacleCount"] = obstacleCount;

        if (distanceToGoalM.HasValue)
        {
            metrics["mission.distanceToGoalM"] = distanceToGoalM.Value;
        }

        if (headingErrorDeg.HasValue)
        {
            metrics["mission.headingErrorDeg"] = headingErrorDeg.Value;
        }

        return new VehicleTelemetryDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = vehicleId,
            X = x,
            Y = y,
            Z = z,
            RollDeg = rollDeg,
            PitchDeg = pitchDeg,
            YawDeg = yawDeg,
            HeadingDeg = headingDeg,
            Vx = vx,
            Vy = vy,
            Vz = vz,
            RollRateDeg = rollRateDeg,
            PitchRateDeg = pitchRateDeg,
            YawRateDeg = yawRateDeg,
            TargetX = targetX,
            TargetY = targetY,
            DistanceToGoalM = distanceToGoalM,
            HeadingErrorDeg = headingErrorDeg,
            ObstacleAhead = obstacleAhead,
            ObstacleCount = obstacleCount,
            Obstacles = obstacles,
            Landmarks = landmarks,
            Metrics = metrics,
            Fields = fields,
            Freshness = BuildFreshness(timestampUtc, "runtime")
        };
    }

    public VehicleTelemetryDto MapVehicleTelemetryFromExternalState(JsonElement root)
    {
        var dto = MapVehicleTelemetry(root);

        dto.Fields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        dto.Fields["origin"] = "external-state";

        return dto;
    }
}