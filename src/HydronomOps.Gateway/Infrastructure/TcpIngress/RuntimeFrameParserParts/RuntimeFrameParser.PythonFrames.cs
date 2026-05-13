using System.Text.Json;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
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

        if (mapped.TargetX is not null)
        {
            existing.TargetX = mapped.TargetX;
        }

        if (mapped.TargetY is not null)
        {
            existing.TargetY = mapped.TargetY;
        }

        if (mapped.DistanceToGoalM is not null)
        {
            existing.DistanceToGoalM = mapped.DistanceToGoalM;
        }

        if (mapped.HeadingErrorDeg is not null)
        {
            existing.HeadingErrorDeg = mapped.HeadingErrorDeg;
        }

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
}