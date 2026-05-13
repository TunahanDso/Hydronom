using System.Text.Json;
using HydronomOps.Gateway.Contracts.Sensors;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private void ProcessTwinImu(JsonElement root)
    {
        var timestamp = ReadTimestamp(root);

        var rollDeg = TryReadDouble(root, "rollDeg", "RollDeg", "roll_deg", "roll", "Roll") ?? _lastRollDeg;
        var pitchDeg = TryReadDouble(root, "pitchDeg", "PitchDeg", "pitch_deg", "pitch", "Pitch") ?? _lastPitchDeg;
        var yawDeg = TryReadDouble(root, "yawDeg", "YawDeg", "yaw_deg", "yaw", "Yaw") ?? _lastYawDeg;
        var headingDeg = TryReadDouble(root, "headingDeg", "HeadingDeg", "heading_deg", "heading", "Heading") ?? yawDeg;

        var rollRateDeg = TryReadDouble(root, "rollRateDeg", "RollRateDeg", "roll_rate_deg", "gxDeg", "GxDeg", "gx") ?? _lastRollRateDeg;
        var pitchRateDeg = TryReadDouble(root, "pitchRateDeg", "PitchRateDeg", "pitch_rate_deg", "gyDeg", "GyDeg", "gy") ?? _lastPitchRateDeg;
        var yawRateDeg = TryReadDouble(root, "yawRateDeg", "YawRateDeg", "yaw_rate_deg", "gzDeg", "GzDeg", "gz") ?? _lastYawRateDeg;

        lock (_twinGate)
        {
            _lastImuTimestampUtc = timestamp;
            _lastRollDeg = rollDeg;
            _lastPitchDeg = pitchDeg;
            _lastYawDeg = yawDeg;
            _lastHeadingDeg = headingDeg;
            _lastRollRateDeg = rollRateDeg;
            _lastPitchRateDeg = pitchRateDeg;
            _lastYawRateDeg = yawRateDeg;

            _stateStore.SetDebugSensorState(new SensorStateDto
            {
                TimestampUtc = timestamp,
                VehicleId = "hydronom-main",
                SensorName = "TwinImu",
                SensorType = "imu",
                Source = GetString(root, "source") ?? "runtime",
                Backend = "twin",
                IsSimulated = true,
                IsEnabled = true,
                IsHealthy = true,
                LastSampleUtc = timestamp
            });
        }

        _stateStore.TouchRuntimeMessage("TwinImu");
    }

    private void ProcessTwinGps(JsonElement root)
    {
        var timestamp = ReadTimestamp(root);

        var lat = TryReadDouble(root, "lat", "latitude", "Latitude");
        var lon = TryReadDouble(root, "lon", "lng", "longitude", "Longitude");
        var z = TryReadDouble(root, "z", "Z", "alt", "Alt") ?? _lastZ;

        var vx = TryReadDouble(root, "vx", "Vx", "velX", "VelX", "speedX", "SpeedX");
        var vy = TryReadDouble(root, "vy", "Vy", "velY", "VelY", "speedY", "SpeedY");
        var vz = TryReadDouble(root, "vz", "Vz", "velZ", "VelZ", "speedZ", "SpeedZ");

        var headingDeg = TryReadDouble(root, "headingDeg", "HeadingDeg", "heading_deg", "heading", "Heading");
        var yawDeg = TryReadDouble(root, "yawDeg", "YawDeg", "yaw_deg", "yaw", "Yaw");

        lock (_twinGate)
        {
            double x = _lastX;
            double y = _lastY;

            if (lat.HasValue && lon.HasValue)
            {
                if (!_gpsOriginInitialized)
                {
                    _originLatDeg = lat.Value;
                    _originLonDeg = lon.Value;
                    _gpsOriginInitialized = true;
                }

                (x, y) = ConvertLatLonToLocalMeters(lat.Value, lon.Value, _originLatDeg, _originLonDeg);
            }

            double dt = 0.0;

            if (_hasLastGpsSample)
            {
                dt = Math.Max((timestamp - _lastGpsTimestampUtc).TotalSeconds, 0.001);
            }

            if (vx is null)
            {
                vx = _hasLastGpsSample ? (x - _lastX) / dt : 0.0;
            }

            if (vy is null)
            {
                vy = _hasLastGpsSample ? (y - _lastY) / dt : 0.0;
            }

            if (vz is null)
            {
                vz = _hasLastGpsSample ? (z - _lastZ) / dt : 0.0;
            }

            _lastGpsTimestampUtc = timestamp;
            _hasLastGpsSample = true;
            _lastX = x;
            _lastY = y;
            _lastZ = z;

            if (headingDeg.HasValue)
            {
                _lastHeadingDeg = headingDeg.Value;
            }

            if (yawDeg.HasValue)
            {
                _lastYawDeg = yawDeg.Value;
            }

            _stateStore.SetDebugSensorState(new SensorStateDto
            {
                TimestampUtc = timestamp,
                VehicleId = "hydronom-main",
                SensorName = "TwinGps",
                SensorType = "gps",
                Source = GetString(root, "source") ?? "runtime",
                Backend = "twin",
                IsSimulated = true,
                IsEnabled = true,
                IsHealthy = true,
                LastSampleUtc = timestamp
            });
        }

        _stateStore.TouchRuntimeMessage("TwinGps");
    }

    private static (double x, double y) ConvertLatLonToLocalMeters(
        double latDeg,
        double lonDeg,
        double originLatDeg,
        double originLonDeg)
    {
        const double EarthRadiusM = 6378137.0;

        var latRad = DegreesToRadians(latDeg);
        var originLatRad = DegreesToRadians(originLatDeg);
        var deltaLatRad = DegreesToRadians(latDeg - originLatDeg);
        var deltaLonRad = DegreesToRadians(lonDeg - originLonDeg);

        var x = deltaLonRad * EarthRadiusM * Math.Cos((latRad + originLatRad) * 0.5);
        var y = deltaLatRad * EarthRadiusM;

        return (x, y);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}