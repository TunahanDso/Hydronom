using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Services.State;

/// <summary>
/// Gateway'in tuttuğu birleşik araç durumunu thread-safe şekilde yönetir.
/// </summary>
public sealed class GatewayStateStore : IGatewayStateStore
{
    private readonly object _lock = new();

    private readonly VehicleAggregateState _current = new()
    {
        VehicleId = "hydronom-main",
        StartedUtc = DateTime.UtcNow
    };

    public VehicleAggregateState GetCurrent()
    {
        lock (_lock)
        {
            return Clone(_current);
        }
    }

    public VehicleTelemetryDto? GetVehicleTelemetry()
    {
        lock (_lock)
        {
            if (_current.VehicleTelemetry is null)
            {
                return null;
            }

            return CloneVehicleTelemetry(_current.VehicleTelemetry);
        }
    }

    public void SetVehicleTelemetry(VehicleTelemetryDto telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        lock (_lock)
        {
            _current.VehicleTelemetry = CloneVehicleTelemetry(telemetry);
            _current.LastVehicleTelemetryUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetMissionState(MissionStateDto missionState)
    {
        ArgumentNullException.ThrowIfNull(missionState);

        lock (_lock)
        {
            _current.MissionState = CloneMissionState(missionState);
            _current.LastMissionStateUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetSensorState(SensorStateDto sensorState)
    {
        ArgumentNullException.ThrowIfNull(sensorState);

        lock (_lock)
        {
            _current.SensorState = CloneSensorState(sensorState);
            _current.LastSensorStateSource = sensorState.Source;
            _current.LastSensorStateUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetRuntimeSensorState(SensorStateDto sensorState)
    {
        ArgumentNullException.ThrowIfNull(sensorState);

        lock (_lock)
        {
            var cloned = CloneSensorState(sensorState);

            _current.RuntimeSensorState = cloned;

            // Eski endpoint ve websocket akışı bozulmasın diye genel SensorState
            // ana runtime sensör özetiyle beslenir.
            // Twin/debug sensörler bu alanı ezmemelidir.
            _current.SensorState = CloneSensorState(cloned);

            _current.LastSensorStateSource = "runtime-telemetry-summary";
            _current.LastSensorStateUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetDebugSensorState(SensorStateDto sensorState)
    {
        ArgumentNullException.ThrowIfNull(sensorState);

        lock (_lock)
        {
            var cloned = CloneSensorState(sensorState);

            _current.DebugSensorState = cloned;
            _current.LastDebugSensorName = cloned.SensorName;
            _current.LastDebugSensorStateUtc = DateTime.UtcNow;

            // Bilerek _current.SensorState güncellenmiyor.
            // TwinImu/TwinGps gibi debug sensörler ana runtime sensor özetini ezmemeli.
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetActuatorState(ActuatorStateDto actuatorState)
    {
        ArgumentNullException.ThrowIfNull(actuatorState);

        lock (_lock)
        {
            _current.ActuatorState = CloneActuatorState(actuatorState);
            _current.LastActuatorStateUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetDiagnosticsState(DiagnosticsStateDto diagnosticsState)
    {
        ArgumentNullException.ThrowIfNull(diagnosticsState);

        lock (_lock)
        {
            _current.DiagnosticsState = CloneDiagnosticsState(diagnosticsState);
            _current.LastDiagnosticsStateUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void AddLog(GatewayLogDto log)
    {
        ArgumentNullException.ThrowIfNull(log);

        lock (_lock)
        {
            _current.Logs.Add(CloneGatewayLog(log));

            if (_current.Logs.Count > 200)
            {
                _current.Logs.RemoveAt(0);
            }

            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void TouchRuntimeMessage(string? rawLine = null)
    {
        lock (_lock)
        {
            _current.LastRuntimeIngressUtc = DateTime.UtcNow;
            _current.LastRawRuntimeLine = rawLine;
            _current.TotalMessagesReceived++;
            _current.RuntimeConnected = true;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetRuntimeConnected(bool isConnected)
    {
        lock (_lock)
        {
            _current.RuntimeConnected = isConnected;

            // Runtime hattı düştüyse Python akışı da bağlı kabul edilmemeli.
            if (!isConnected)
            {
                _current.PythonConnected = false;
            }

            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetPythonConnected(bool isConnected)
    {
        lock (_lock)
        {
            _current.PythonConnected = isConnected;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetWebSocketClientCount(int count)
    {
        lock (_lock)
        {
            _current.WebSocketClientCount = count;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void IncrementBroadcastCount()
    {
        lock (_lock)
        {
            _current.TotalMessagesBroadcast++;
            _current.LastGatewayBroadcastUtc = DateTime.UtcNow;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public void SetLastError(string? error)
    {
        lock (_lock)
        {
            _current.LastError = error;
            _current.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    private static VehicleAggregateState Clone(VehicleAggregateState source)
    {
        return new VehicleAggregateState
        {
            VehicleId = source.VehicleId,
            StartedUtc = source.StartedUtc,
            LastUpdatedUtc = source.LastUpdatedUtc,
            LastRuntimeIngressUtc = source.LastRuntimeIngressUtc,
            LastVehicleTelemetryUtc = source.LastVehicleTelemetryUtc,
            LastMissionStateUtc = source.LastMissionStateUtc,
            LastSensorStateUtc = source.LastSensorStateUtc,
            LastDebugSensorStateUtc = source.LastDebugSensorStateUtc,
            LastActuatorStateUtc = source.LastActuatorStateUtc,
            LastDiagnosticsStateUtc = source.LastDiagnosticsStateUtc,
            LastGatewayBroadcastUtc = source.LastGatewayBroadcastUtc,
            LastRawRuntimeLine = source.LastRawRuntimeLine,
            LastError = source.LastError,
            RuntimeConnected = source.RuntimeConnected,
            PythonConnected = source.PythonConnected,
            WebSocketClientCount = source.WebSocketClientCount,
            TotalMessagesReceived = source.TotalMessagesReceived,
            TotalMessagesBroadcast = source.TotalMessagesBroadcast,
            VehicleTelemetry = source.VehicleTelemetry is null ? null : CloneVehicleTelemetry(source.VehicleTelemetry),
            MissionState = source.MissionState is null ? null : CloneMissionState(source.MissionState),
            SensorState = source.SensorState is null ? null : CloneSensorState(source.SensorState),
            RuntimeSensorState = source.RuntimeSensorState is null ? null : CloneSensorState(source.RuntimeSensorState),
            DebugSensorState = source.DebugSensorState is null ? null : CloneSensorState(source.DebugSensorState),
            LastSensorStateSource = source.LastSensorStateSource,
            LastDebugSensorName = source.LastDebugSensorName,
            ActuatorState = source.ActuatorState is null ? null : CloneActuatorState(source.ActuatorState),
            DiagnosticsState = source.DiagnosticsState is null ? null : CloneDiagnosticsState(source.DiagnosticsState),
            Logs = source.Logs.Select(CloneGatewayLog).ToList()
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

            Obstacles = source.Obstacles is null
                ? new List<ObstacleDto>()
                : source.Obstacles.Select(CloneObstacle).ToList(),

            Landmarks = source.Landmarks is null
                ? new List<LandmarkDto>()
                : source.Landmarks.Select(CloneLandmark).ToList(),

            Metrics = source.Metrics is null
                ? new Dictionary<string, double>()
                : new Dictionary<string, double>(source.Metrics),

            Fields = source.Fields is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(source.Fields),

            Freshness = source.Freshness
        };
    }

    private static ObstacleDto CloneObstacle(ObstacleDto source)
    {
        return new ObstacleDto
        {
            X = source.X,
            Y = source.Y,
            R = source.R
        };
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
                : source.Points.Select(CloneObstaclePoint).ToList(),
            Style = source.Style,
            Metrics = source.Metrics is null
                ? new Dictionary<string, double>()
                : new Dictionary<string, double>(source.Metrics),
            Fields = source.Fields is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(source.Fields)
        };
    }

    private static ObstaclePointDto CloneObstaclePoint(ObstaclePointDto source)
    {
        return new ObstaclePointDto
        {
            X = source.X,
            Y = source.Y
        };
    }

    private static MissionStateDto CloneMissionState(MissionStateDto source)
    {
        return new MissionStateDto
        {
            TimestampUtc = source.TimestampUtc,
            VehicleId = source.VehicleId,
            MissionId = source.MissionId,
            MissionName = source.MissionName,
            Status = source.Status,
            CurrentStepIndex = source.CurrentStepIndex,
            TotalStepCount = source.TotalStepCount,
            CurrentStepTitle = source.CurrentStepTitle,
            NextObjective = source.NextObjective,
            RemainingDistanceMeters = source.RemainingDistanceMeters,
            StartedAtUtc = source.StartedAtUtc,
            FinishedAtUtc = source.FinishedAtUtc,
            Freshness = source.Freshness
        };
    }

    private static SensorStateDto CloneSensorState(SensorStateDto source)
    {
        return new SensorStateDto
        {
            TimestampUtc = source.TimestampUtc,
            VehicleId = source.VehicleId,
            SensorName = source.SensorName,
            SensorType = source.SensorType,
            Source = source.Source,
            Backend = source.Backend,
            IsSimulated = source.IsSimulated,
            IsEnabled = source.IsEnabled,
            IsHealthy = source.IsHealthy,
            ConfiguredRateHz = source.ConfiguredRateHz,
            EffectiveRateHz = source.EffectiveRateHz,
            LastSampleUtc = source.LastSampleUtc,
            LastError = source.LastError,
            Metrics = source.Metrics is null
                ? new Dictionary<string, double>()
                : new Dictionary<string, double>(source.Metrics),
            Fields = source.Fields is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(source.Fields),
            Freshness = source.Freshness
        };
    }

    private static ActuatorStateDto CloneActuatorState(ActuatorStateDto source)
    {
        return new ActuatorStateDto
        {
            TimestampUtc = source.TimestampUtc,
            VehicleId = source.VehicleId,
            Freshness = source.Freshness
        };
    }

    private static DiagnosticsStateDto CloneDiagnosticsState(DiagnosticsStateDto source)
    {
        return new DiagnosticsStateDto
        {
            TimestampUtc = source.TimestampUtc,
            GatewayStatus = source.GatewayStatus,
            RuntimeConnected = source.RuntimeConnected,
            HasWebSocketClients = source.HasWebSocketClients,
            ConnectedWebSocketClients = source.ConnectedWebSocketClients,
            LastRuntimeMessageUtc = source.LastRuntimeMessageUtc,
            RuntimeFreshness = source.RuntimeFreshness,
            LastError = source.LastError,
            LastErrorUtc = source.LastErrorUtc,
            IngressMessageCount = source.IngressMessageCount,
            BroadcastMessageCount = source.BroadcastMessageCount
        };
    }

    private static GatewayLogDto CloneGatewayLog(GatewayLogDto source)
    {
        return new GatewayLogDto
        {
            TimestampUtc = source.TimestampUtc,
            Level = source.Level,
            Category = source.Category,
            Message = source.Message,
            Detail = source.Detail
        };
    }
}