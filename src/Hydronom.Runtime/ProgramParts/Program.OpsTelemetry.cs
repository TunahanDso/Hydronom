using Hydronom.Core.Domain;
using Hydronom.Runtime.Actuators;
using Hydronom.Runtime.Scenarios.Runtime;

partial class Program
{
    private const int OpsTelemetryEveryTicks = 5;
    private const string DefaultOpsVehicleId = "hydronom-main";

    private static bool ShouldPublishOpsTelemetry(long tickIndex)
    {
        if (tickIndex < 0)
            return false;

        return tickIndex % OpsTelemetryEveryTicks == 0;
    }

    private static async Task TryPublishOpsTelemetryFramesAsync(
        TcpJsonFrameSource? tcpFrameSource,
        RuntimeScenarioController runtimeScenarioController,
        ActuatorManager actuatorManager,
        VehicleState state,
        long tickIndex,
        CancellationToken cancellationToken)
    {
        if (tcpFrameSource is null)
            return;

        if (!ShouldPublishOpsTelemetry(tickIndex))
            return;

        try
        {
            var now = DateTime.UtcNow;

            var scenarioSnapshot = runtimeScenarioController.GetSnapshot();

            /*
            * Authoritative vehicle id:
            * RuntimeScenarioController artık snapshot.VehicleId alanını runtime'ın operasyonel
            * vehicle identity değeri olarak üretir.
            *
            * Bu yüzden OpsTelemetry burada ayrıca config okumaz.
            * Telemetry, mission, actuator ve world frame'leri aynı snapshot vehicle id ile yayınlanır.
            */
            var runtimeVehicleId = NormalizeVehicleId(scenarioSnapshot.VehicleId);

            var telemetryFrame = BuildRuntimeTelemetryFrame(
                state,
                now,
                runtimeVehicleId);

            await tcpFrameSource.Server
                .BroadcastAsync(telemetryFrame)
                .ConfigureAwait(false);

            var missionFrame = BuildRuntimeMissionStateFrame(
                scenarioSnapshot,
                state,
                now,
                runtimeVehicleId);

            await tcpFrameSource.Server
                .BroadcastAsync(missionFrame)
                .ConfigureAwait(false);

            var actuatorFrame = BuildRuntimeActuatorStateFrame(
                actuatorManager,
                now,
                runtimeVehicleId);

            await tcpFrameSource.Server
                .BroadcastAsync(actuatorFrame)
                .ConfigureAwait(false);

            var worldFrame = BuildRuntimeWorldObjectsFrame(
                scenarioSnapshot,
                now,
                runtimeVehicleId);

            await tcpFrameSource.Server
                .BroadcastAsync(worldFrame)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OPS-TEL] publish error: {ex.Message}");
        }
    }


    private static object BuildRuntimeTelemetryFrame(
        VehicleState state,
        DateTime now,
        string vehicleId)
    {
        var safeVehicleId = NormalizeVehicleId(vehicleId);

        var vx = state.LinearVelocity.X;
        var vy = state.LinearVelocity.Y;
        var vz = state.LinearVelocity.Z;

        var speed = Math.Sqrt(vx * vx + vy * vy + vz * vz);

        return new
        {
            type = "RuntimeTelemetry",
            timestampUtc = now,
            vehicleId = safeVehicleId,

            x = state.Position.X,
            y = state.Position.Y,
            z = state.Position.Z,

            yawDeg = state.Orientation.YawDeg,
            headingDeg = state.Orientation.YawDeg,

            vx,
            vy,
            vz,

            yawRateDeg = state.AngularVelocity.Z,
            rollRateDeg = state.AngularVelocity.X,
            pitchRateDeg = state.AngularVelocity.Y,

            speed
        };
    }

    private static object BuildRuntimeMissionStateFrame(
        RuntimeScenarioSnapshot snapshot,
        VehicleState state,
        DateTime now,
        string vehicleId)
    {
        var safeVehicleId = NormalizeVehicleId(vehicleId);

        var status = ResolveMissionStatus(snapshot);
        var currentStepIndex = snapshot.TotalObjectiveCount <= 0
            ? 0
            : Math.Clamp(snapshot.CompletedObjectiveCount + (snapshot.IsRunning ? 1 : 0), 0, snapshot.TotalObjectiveCount);

        return new
        {
            type = "RuntimeMissionState",
            timestampUtc = now,
            vehicleId = safeVehicleId,

            scenarioVehicleId = snapshot.VehicleId,

            missionId = snapshot.ScenarioId,
            missionName = snapshot.ScenarioName,
            status,

            currentStepIndex,
            totalStepCount = snapshot.TotalObjectiveCount,
            currentStepTitle = snapshot.CurrentObjectiveId,
            nextObjective = snapshot.CurrentObjectiveId,

            remainingDistanceMeters = snapshot.LastDistanceToTargetMeters,

            runId = snapshot.RunId,
            hasActiveScenario = snapshot.HasActiveScenario,
            isRunning = snapshot.IsRunning,
            scenarioState = snapshot.State,
            completedObjectiveCount = snapshot.CompletedObjectiveCount,
            totalObjectiveCount = snapshot.TotalObjectiveCount,
            lastCompletedObjectiveId = snapshot.LastCompletedObjectiveId,
            lastDistance3DToTargetMeters = snapshot.LastDistance3DToTargetMeters,
            lastTickSummary = snapshot.LastTickSummary,
            sessionSummary = snapshot.SessionSummary,

            activeObjectiveTarget = snapshot.ActiveObjectiveTargetX.HasValue &&
                                    snapshot.ActiveObjectiveTargetY.HasValue
                ? new
                {
                    x = snapshot.ActiveObjectiveTargetX.Value,
                    y = snapshot.ActiveObjectiveTargetY.Value,
                    z = snapshot.ActiveObjectiveTargetZ ?? 0.0,
                    toleranceMeters = snapshot.ActiveObjectiveToleranceMeters
                }
                : null,

            vehicleX = state.Position.X,
            vehicleY = state.Position.Y,
            vehicleZ = state.Position.Z,
            vehicleYawDeg = state.Orientation.YawDeg
        };
    }

    private static object BuildRuntimeActuatorStateFrame(
        ActuatorManager actuatorManager,
        DateTime now,
        string vehicleId)
    {
        var safeVehicleId = NormalizeVehicleId(vehicleId);

        var report = actuatorManager.LastAllocationReport;
        var force = actuatorManager.LastForceBody;
        var torque = actuatorManager.LastTorqueBody;

        var thrusters = actuatorManager.Thrusters
            .OrderBy(x => x.Channel)
            .Select(x => new
            {
                id = x.Id,
                channel = x.Channel,
                active = Math.Abs(x.Current) > 1e-6,
                normalizedCommand = x.Current,
                rpm = x.RpmFeedback,
                currentMa = x.CurrentSenseMilliAmp,
                healthy = x.IsHealthy,
                canReverse = x.CanReverse,
                reversed = x.Reversed,
                healthFlags = x.HealthFlags.ToString(),
                position = new
                {
                    x = x.Position.X,
                    y = x.Position.Y,
                    z = x.Position.Z
                },
                forceDir = new
                {
                    x = x.ForceDir.X,
                    y = x.ForceDir.Y,
                    z = x.ForceDir.Z
                }
            })
            .ToArray();

        var averageCommand = thrusters.Length == 0
            ? 0.0
            : thrusters.Average(x => Math.Abs(x.normalizedCommand));

        return new
        {
            type = "RuntimeActuatorState",
            timestampUtc = now,
            vehicleId = safeVehicleId,

            actuatorName = "thruster-array",
            actuatorType = "thruster-group",
            isEnabled = true,
            isHealthy = report.Success && !report.HadUnhealthyThruster,
            command = averageCommand,

            forceBody = new
            {
                x = force.X,
                y = force.Y,
                z = force.Z
            },
            torqueBody = new
            {
                x = torque.X,
                y = torque.Y,
                z = torque.Z
            },

            allocation = new
            {
                success = report.Success,
                reason = report.Reason,
                normalizedError = report.NormalizedError,
                saturationRatio = report.SaturationRatio,
                activeThrusterCount = report.ActiveThrusterCount,
                healthyThrusterCount = report.HealthyThrusterCount,
                hadSaturation = report.HadSaturation,
                hadUnhealthyThruster = report.HadUnhealthyThruster,
                authorityLimited = report.AuthorityLimited,
                reverseClampCount = report.ReverseClampCount,

                requestedForceBody = new
                {
                    x = report.RequestedForceBody.X,
                    y = report.RequestedForceBody.Y,
                    z = report.RequestedForceBody.Z
                },
                requestedTorqueBody = new
                {
                    x = report.RequestedTorqueBody.X,
                    y = report.RequestedTorqueBody.Y,
                    z = report.RequestedTorqueBody.Z
                },
                achievedForceBody = new
                {
                    x = report.AchievedForceBody.X,
                    y = report.AchievedForceBody.Y,
                    z = report.AchievedForceBody.Z
                },
                achievedTorqueBody = new
                {
                    x = report.AchievedTorqueBody.X,
                    y = report.AchievedTorqueBody.Y,
                    z = report.AchievedTorqueBody.Z
                }
            },

            thrusters
        };
    }

    private static object BuildRuntimeWorldObjectsFrame(
        RuntimeScenarioSnapshot snapshot,
        DateTime now,
        string vehicleId)
    {
        var safeVehicleId = NormalizeVehicleId(vehicleId);

        var route = (snapshot.RoutePoints ?? Array.Empty<RuntimeScenarioRoutePoint>())
            .OrderBy(x => x.Index)
            .Select(x => new
            {
                id = x.Id,
                label = x.Label,
                objectiveId = x.ObjectiveId,
                index = x.Index,
                type = "route-point",
                x = x.X,
                y = x.Y,
                z = x.Z,
                toleranceMeters = x.ToleranceMeters,
                active = x.IsActive,
                completed = x.IsCompleted
            })
            .ToArray();

        var objects = (snapshot.WorldObjects ?? Array.Empty<RuntimeScenarioWorldObject>())
            .Select(x => new
            {
                id = x.Id,
                type = x.Type,
                kind = x.Type,
                label = x.Label,
                objectiveId = x.ObjectiveId,
                side = x.Side,
                x = x.X,
                y = x.Y,
                z = x.Z,
                radius = x.Radius,
                color = x.Color,
                active = x.IsActive,
                completed = x.IsCompleted,
                isBlocking = x.IsBlocking,
                isDetectable = x.IsDetectable
            })
            .ToArray();

        return new
        {
            type = "RuntimeWorldObjects",
            timestampUtc = now,
            vehicleId = safeVehicleId,

            scenarioVehicleId = snapshot.VehicleId,

            scenarioId = snapshot.ScenarioId,
            scenarioName = snapshot.ScenarioName,
            runId = snapshot.RunId,
            source = "runtime-scenario-controller",

            currentObjectiveId = snapshot.CurrentObjectiveId,
            activeObjectiveTarget = snapshot.ActiveObjectiveTargetX.HasValue &&
                                    snapshot.ActiveObjectiveTargetY.HasValue
                ? new
                {
                    x = snapshot.ActiveObjectiveTargetX.Value,
                    y = snapshot.ActiveObjectiveTargetY.Value,
                    z = snapshot.ActiveObjectiveTargetZ ?? 0.0,
                    toleranceMeters = snapshot.ActiveObjectiveToleranceMeters
                }
                : null,

            route,
            objects
        };
    }

    private static string ResolveMissionStatus(RuntimeScenarioSnapshot snapshot)
    {
        if (snapshot.IsRunning)
            return "running";

        if (!snapshot.HasActiveScenario)
            return "idle";

        var state = snapshot.State?.Trim();

        if (string.IsNullOrWhiteSpace(state))
            return "idle";

        return state.ToLowerInvariant();
    }

    private static string NormalizeVehicleId(string? vehicleId)
    {
        return string.IsNullOrWhiteSpace(vehicleId)
            ? DefaultOpsVehicleId
            : vehicleId.Trim();
    }
}