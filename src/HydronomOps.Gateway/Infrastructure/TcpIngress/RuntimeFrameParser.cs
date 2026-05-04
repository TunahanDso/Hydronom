using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using HydronomOps.Gateway.Infrastructure.Serialization;
using HydronomOps.Gateway.Services.Mapping;
using HydronomOps.Gateway.Services.State;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

/// <summary>
/// Runtime'tan gelen ham NDJSON satırlarını parse eder ve gateway state store'a işler.
/// </summary>
public sealed class RuntimeFrameParser
{
    private readonly IGatewayStateStore _stateStore;
    private readonly RuntimeToGatewayMapper _mapper;

    private readonly object _twinGate = new();

    private readonly ConcurrentDictionary<string, int> _debugLogCounts = new();
    private const int MaxDebugLogsPerType = 5;

    private DateTime _lastImuTimestampUtc = DateTime.UtcNow;
    private DateTime _lastGpsTimestampUtc = DateTime.UtcNow;
    private bool _hasLastGpsSample;

    private double _lastX;
    private double _lastY;
    private double _lastZ;
    private double _lastRollDeg;
    private double _lastPitchDeg;
    private double _lastYawDeg;
    private double _lastHeadingDeg;
    private double _lastRollRateDeg;
    private double _lastPitchRateDeg;
    private double _lastYawRateDeg;

    // GPS -> yerel XY dönüşümü için referans origin
    private bool _gpsOriginInitialized;
    private double _originLatDeg;
    private double _originLonDeg;

    public RuntimeFrameParser(
        IGatewayStateStore stateStore,
        RuntimeToGatewayMapper mapper)
    {
        _stateStore = stateStore;
        _mapper = mapper;
    }

    public void ProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(line, JsonDefaults.DocumentOptions);
            var root = document.RootElement;

            var type = ReadType(root);
            if (string.IsNullOrWhiteSpace(type))
            {
                _stateStore.AddLog(new GatewayLogDto
                {
                    Level = "debug",
                    Category = "parser",
                    Message = "Type alanı olmayan runtime satırı alındı.",
                    Detail = TrimForLog(line),
                    TimestampUtc = DateTime.UtcNow
                });

                return;
            }

            LogRawSampleIfNeeded(type, line);

            switch (type)
            {
                case "FusedState":
                    ProcessFusedState(root);
                    break;

                case "ExternalState":
                    ProcessExternalState(root);
                    break;

                case "Sample":
                    ProcessSample(root);
                    break;

                case "Health":
                    ProcessHealth(root);
                    break;

                case "RuntimeTelemetrySummary":
                case "RuntimeSummary":
                    ProcessRuntimeTelemetrySummary(root);
                    break;

                case "Event":
                    ProcessEvent(root);
                    break;

                case "Capability":
                    ProcessCapability(root);
                    break;

                case "TwinImu":
                    ProcessTwinImu(root);
                    break;

                case "TwinGps":
                    ProcessTwinGps(root);
                    break;

                case "StreamSubscribe":
                    _stateStore.TouchRuntimeMessage("StreamSubscribe");
                    break;

                default:
                    _stateStore.AddLog(new GatewayLogDto
                    {
                        Level = "debug",
                        Category = "parser",
                        Message = $"Bilinmeyen mesaj tipi alındı: {type}",
                        Detail = TrimForLog(line),
                        TimestampUtc = DateTime.UtcNow
                    });

                    _stateStore.TouchRuntimeMessage(type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _stateStore.AddLog(new GatewayLogDto
            {
                Level = "error",
                Category = "parser",
                Message = $"Frame parse hatası: {ex.Message}",
                Detail = TrimForLog(line),
                TimestampUtc = DateTime.UtcNow
            });
        }
    }

    private void MarkPythonConnected()
    {
        _stateStore.SetPythonConnected(true);
    }

    private void ProcessFusedState(JsonElement root)
    {
        MarkPythonConnected();

        var vehicle = _mapper.MapVehicleTelemetry(root);
        var mission = _mapper.MapMissionState(root);
        var diagnostics = _mapper.MapDiagnosticsState(root);

        _stateStore.SetVehicleTelemetry(vehicle);
        _stateStore.SetMissionState(mission);
        _stateStore.SetDiagnosticsState(diagnostics);
        _stateStore.TouchRuntimeMessage("FusedState");
    }

    private void ProcessExternalState(JsonElement root)
    {
        MarkPythonConnected();

        var existing = GetOrCreateVehicleTelemetry();
        var mapped = _mapper.MapVehicleTelemetryFromExternalState(root);

        existing.TimestampUtc = mapped.TimestampUtc;
        existing.VehicleId = mapped.VehicleId;
        existing.X = mapped.X;
        existing.Y = mapped.Y;
        existing.Z = mapped.Z;
        existing.RollDeg = mapped.RollDeg;
        existing.PitchDeg = mapped.PitchDeg;
        existing.YawDeg = mapped.YawDeg;
        existing.HeadingDeg = mapped.HeadingDeg;
        existing.Vx = mapped.Vx;
        existing.Vy = mapped.Vy;
        existing.Vz = mapped.Vz;
        existing.RollRateDeg = mapped.RollRateDeg;
        existing.PitchRateDeg = mapped.PitchRateDeg;
        existing.YawRateDeg = mapped.YawRateDeg;

        if (mapped.TargetX is not null) existing.TargetX = mapped.TargetX;
        if (mapped.TargetY is not null) existing.TargetY = mapped.TargetY;
        if (mapped.DistanceToGoalM is not null) existing.DistanceToGoalM = mapped.DistanceToGoalM;
        if (mapped.HeadingErrorDeg is not null) existing.HeadingErrorDeg = mapped.HeadingErrorDeg;

        // ExternalState çoğu zaman pose/twist taşır.
        // Bu yüzden boş obstacle/landmark listeleri gelirse mevcut harita bilgisini ezmeyelim.
        if (mapped.Obstacles is not null && mapped.Obstacles.Count > 0)
        {
            existing.Obstacles = CloneObstacles(mapped.Obstacles);
            existing.ObstacleCount = mapped.ObstacleCount > 0
                ? mapped.ObstacleCount
                : mapped.Obstacles.Count;
            existing.ObstacleAhead = mapped.ObstacleAhead || mapped.Obstacles.Count > 0;
        }
        else
        {
            if (mapped.ObstacleCount > 0)
            {
                existing.ObstacleCount = mapped.ObstacleCount;
            }

            if (mapped.ObstacleAhead)
            {
                existing.ObstacleAhead = true;
            }
        }

        if (mapped.Landmarks is not null && mapped.Landmarks.Count > 0)
        {
            existing.Landmarks = CloneLandmarks(mapped.Landmarks);
        }

        if (mapped.Metrics is not null)
        {
            existing.Metrics = new Dictionary<string, double>(mapped.Metrics);
        }

        if (mapped.Fields is not null)
        {
            existing.Fields = new Dictionary<string, string>(mapped.Fields);
        }

        existing.Freshness = mapped.Freshness;

        _stateStore.SetVehicleTelemetry(existing);
        _stateStore.TouchRuntimeMessage("ExternalState");
    }

    private void ProcessSample(JsonElement root)
    {
        MarkPythonConnected();

        var sensorState = _mapper.MapSensorState(root);
        var actuatorState = _mapper.MapActuatorState(root);

        if (sensorState is not null)
        {
            _stateStore.SetSensorState(sensorState);
        }

        if (actuatorState is not null)
        {
            _stateStore.SetActuatorState(actuatorState);
        }

        _stateStore.TouchRuntimeMessage("Sample");
    }

    private void ProcessHealth(JsonElement root)
    {
        MarkPythonConnected();

        var diagnostics = _mapper.MapDiagnosticsStateFromHealth(root);
        _stateStore.SetDiagnosticsState(diagnostics);
        _stateStore.TouchRuntimeMessage("Health");
    }

    private void ProcessRuntimeTelemetrySummary(JsonElement root)
    {
        /*
         * Bu mesaj C# Primary runtime tarafından üretilen ürünleşmiş runtime özetidir.
         * Python kaynaklı sayılmamalıdır; bu yüzden MarkPythonConnected() çağırmıyoruz.
         *
         * Bu sayede Gateway tarafında:
         * - RuntimeConnected true olur.
         * - PythonConnected gereksiz yere true olmaz.
         * - VehicleTelemetry / DiagnosticsState / SensorState runtime summary'den beslenir.
         */
        var vehicle = _mapper.MapVehicleTelemetryFromRuntimeSummary(root);
        var diagnostics = _mapper.MapDiagnosticsStateFromRuntimeSummary(root);
        var sensor = _mapper.MapSensorStateFromRuntimeSummary(root);

        _stateStore.SetVehicleTelemetry(vehicle);
        _stateStore.SetDiagnosticsState(diagnostics);
        _stateStore.SetSensorState(sensor);
        _stateStore.TouchRuntimeMessage("RuntimeTelemetrySummary");
    }

    private void ProcessEvent(JsonElement root)
    {
        MarkPythonConnected();

        var log = _mapper.MapGatewayLogFromEvent(root);
        _stateStore.AddLog(log);
        _stateStore.TouchRuntimeMessage("Event");
    }

    private void ProcessCapability(JsonElement root)
    {
        MarkPythonConnected();

        var log = _mapper.MapGatewayLogFromCapability(root);
        _stateStore.AddLog(log);
        _stateStore.TouchRuntimeMessage("Capability");
    }

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

            _stateStore.SetSensorState(new SensorStateDto
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

            var telemetry = GetOrCreateVehicleTelemetry();
            telemetry.TimestampUtc = timestamp;
            telemetry.VehicleId = "hydronom-main";
            telemetry.RollDeg = _lastRollDeg;
            telemetry.PitchDeg = _lastPitchDeg;
            telemetry.YawDeg = _lastYawDeg;
            telemetry.HeadingDeg = _lastHeadingDeg;
            telemetry.RollRateDeg = _lastRollRateDeg;
            telemetry.PitchRateDeg = _lastPitchRateDeg;
            telemetry.YawRateDeg = _lastYawRateDeg;

            _stateStore.SetVehicleTelemetry(telemetry);
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

            _stateStore.SetSensorState(new SensorStateDto
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

            var telemetry = GetOrCreateVehicleTelemetry();
            telemetry.TimestampUtc = timestamp;
            telemetry.VehicleId = "hydronom-main";
            telemetry.X = _lastX;
            telemetry.Y = _lastY;
            telemetry.Z = _lastZ;
            telemetry.Vx = vx ?? telemetry.Vx;
            telemetry.Vy = vy ?? telemetry.Vy;
            telemetry.Vz = vz ?? telemetry.Vz;

            if (headingDeg.HasValue)
            {
                telemetry.HeadingDeg = headingDeg.Value;
            }

            if (yawDeg.HasValue)
            {
                telemetry.YawDeg = yawDeg.Value;
            }

            _stateStore.SetVehicleTelemetry(telemetry);
        }

        _stateStore.TouchRuntimeMessage("TwinGps");
    }

    private VehicleTelemetryDto GetOrCreateVehicleTelemetry()
    {
        var current = _stateStore.GetVehicleTelemetry();
        if (current is not null)
        {
            return CloneVehicleTelemetry(current);
        }

        return new VehicleTelemetryDto
        {
            TimestampUtc = DateTime.UtcNow,
            VehicleId = "hydronom-main",
            Obstacles = new List<ObstacleDto>(),
            Landmarks = new List<LandmarkDto>(),
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static VehicleTelemetryDto CloneVehicleTelemetry(VehicleTelemetryDto source)
    {
        return new VehicleTelemetryDto
        {
            TimestampUtc = source.TimestampUtc,
            VehicleId = source.VehicleId,
            X = source.X,
            Y = source.Y,
            Z = source.Z,
            RollDeg = source.RollDeg,
            PitchDeg = source.PitchDeg,
            YawDeg = source.YawDeg,
            HeadingDeg = source.HeadingDeg,
            Vx = source.Vx,
            Vy = source.Vy,
            Vz = source.Vz,
            RollRateDeg = source.RollRateDeg,
            PitchRateDeg = source.PitchRateDeg,
            YawRateDeg = source.YawRateDeg,
            TargetX = source.TargetX,
            TargetY = source.TargetY,
            DistanceToGoalM = source.DistanceToGoalM,
            HeadingErrorDeg = source.HeadingErrorDeg,
            ObstacleAhead = source.ObstacleAhead,
            ObstacleCount = source.ObstacleCount,
            Obstacles = CloneObstacles(source.Obstacles),
            Landmarks = CloneLandmarks(source.Landmarks),
            Metrics = source.Metrics is null ? null : new Dictionary<string, double>(source.Metrics),
            Fields = source.Fields is null ? null : new Dictionary<string, string>(source.Fields),
            Freshness = source.Freshness
        };
    }

    private static List<ObstacleDto> CloneObstacles(IReadOnlyCollection<ObstacleDto>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new List<ObstacleDto>();
        }

        return source
            .Select(o => new ObstacleDto
            {
                X = o.X,
                Y = o.Y,
                R = o.R
            })
            .ToList();
    }

    private static List<LandmarkDto> CloneLandmarks(IReadOnlyCollection<LandmarkDto>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new List<LandmarkDto>();
        }

        return source.Select(CloneLandmark).ToList();
    }

    private static LandmarkDto CloneLandmark(LandmarkDto source)
    {
        return new LandmarkDto
        {
            Id = source.Id,
            Type = source.Type,
            Shape = source.Shape,
            Points = source.Points is null
                ? new List<ObstaclePointDto>()
                : source.Points
                    .Select(p => new ObstaclePointDto
                    {
                        X = p.X,
                        Y = p.Y
                    })
                    .ToList(),
            Style = source.Style is null
                ? null
                : new LandmarkStyleDto
                {
                    Color = source.Style.Color,
                    Width = source.Style.Width,
                    Radius = source.Style.Radius,
                    Label = source.Style.Label,
                    Fields = source.Style.Fields is null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string>(source.Style.Fields)
                },
            Metrics = source.Metrics is null
                ? new Dictionary<string, double>()
                : new Dictionary<string, double>(source.Metrics),
            Fields = source.Fields is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(source.Fields)
        };
    }

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