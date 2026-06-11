using Hydronom.Core.Domain;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.State.Authority;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Operations.Snapshots;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.Backends.Sim;
using Hydronom.Runtime.Sensors.Runtime;
using Hydronom.Runtime.Simulation.Physics;
using Hydronom.Runtime.StateRuntime;
using Hydronom.Runtime.Telemetry;
using Hydronom.Runtime.World.Runtime;
using Hydronom.Runtime.Vehicles;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Runtime ana dÃƒÂ¶ngÃƒÂ¼sÃƒÂ¼nde kullanÃ„Â±lacak C# Primary telemetry pipeline ÃƒÂ§alÃ„Â±Ã…Å¸ma zamanÃ„Â± tutucusu.
    ///
    /// Bu yapÃ„Â± sensÃƒÂ¶r verisini TCP'den almaz.
    /// C# Primary akÃ„Â±Ã…Å¸ta sensÃƒÂ¶rler runtime iÃƒÂ§inde okunur; TCP yalnÃ„Â±zca Gateway/Ops telemetry yayÃ„Â±nÃ„Â± iÃƒÂ§in kullanÃ„Â±lÃ„Â±r.
    /// </summary>
    private sealed class RuntimeTelemetryRuntime : IAsyncDisposable
    {
        private readonly PhysicsTruthProvider _truthProvider;

        public RuntimeTelemetryRuntime(
            RuntimeTelemetryPipeline pipeline,
            PhysicsTruthProvider truthProvider,
            string vehicleId,
            int everyTicks,
            int logEveryTicks)
        {
            Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _truthProvider = truthProvider ?? throw new ArgumentNullException(nameof(truthProvider));

            VehicleId = string.IsNullOrWhiteSpace(vehicleId)
                ? "hydronom-main"
                : vehicleId.Trim();

            EveryTicks = everyTicks <= 0 ? 1 : everyTicks;
            LogEveryTicks = logEveryTicks <= 0 ? 50 : logEveryTicks;
        }

        public RuntimeTelemetryPipeline Pipeline { get; }

        public string VehicleId { get; }

        public int EveryTicks { get; }

        public int LogEveryTicks { get; }

        public void PublishTruthFromVehicleState(VehicleState state, long tickIndex)
        {
            var truth = new PhysicsTruthState(
                VehicleId: VehicleId,
                TimestampUtc: DateTime.UtcNow,
                Position: state.Position,
                Velocity: state.LinearVelocity,
                Acceleration: Vec3.Zero,
                Orientation: state.Orientation,
                AngularVelocityDegSec: state.AngularVelocity,
                AngularAccelerationDegSec: Vec3.Zero,
                LastAppliedLoads: PhysicsLoads.Zero,
                EnvironmentSummary: "RUNTIME_MAIN_LOOP",
                FrameId: "map",
                TraceId: $"runtime-main-loop-{tickIndex}"
            );

            _truthProvider.Publish(truth);
        }

        public async ValueTask DisposeAsync()
        {
            await Pipeline.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// RuntimeTelemetryPipeline kurulmalÃ„Â± mÃ„Â±?
    /// </summary>
    private static bool IsRuntimeTelemetryEnabled(IConfiguration config)
    {
        return ReadBool(config, "Runtime:TelemetrySummary:Enabled", true);
    }

    /// <summary>
    /// C# Primary RuntimeTelemetryPipeline oluÃ…Å¸turur.
    ///
    /// Not:
    /// - Bu pipeline runtime iÃƒÂ§indeki C# sensÃƒÂ¶r/fusion/state hattÃ„Â±nÃ„Â± kullanÃ„Â±r.
    /// - TcpJsonServer yalnÃ„Â±zca RuntimeTelemetrySummary yayÃ„Â±nlamak iÃƒÂ§in kullanÃ„Â±lÃ„Â±r.
    /// - Python/TcpJsonFrameSource fallback hattÃ„Â± bundan baÃ„Å¸Ã„Â±msÃ„Â±z kalÃ„Â±r.
    /// - Sim LiDAR aynÃ„Â± RuntimeWorldModel instance'Ã„Â±nÃ„Â± okuyarak senaryo dubalarÃ„Â±nÃ„Â±/engellerini raycast eder.
    /// </summary>
    private static RuntimeTelemetryRuntime CreateRuntimeTelemetryRuntime(
        IConfiguration config,
        TcpJsonServer tcpJsonServer,
        VehicleState initialState,
        RuntimeWorldModel? runtimeWorldModel = null,
        ActiveVehicleContext? activeVehicleContext = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(tcpJsonServer);

        var runtimeId = ReadString(config, "Runtime:TelemetrySummary:RuntimeId", "hydronom_runtime");
                var configuredVehicleId = ReadString(config, "Runtime:TelemetrySummary:VehicleId", "hydronom-main");

        var vehicleId =
            activeVehicleContext?.HasProfile == true &&
            !string.IsNullOrWhiteSpace(activeVehicleContext.VehicleId)
                ? activeVehicleContext.VehicleId
                : configuredVehicleId;
        var frameId = ReadString(config, "Runtime:TelemetrySummary:FrameId", "map");

        var everyTicks = ReadInt(config, "Runtime:TelemetrySummary:Every", 10);
        if (everyTicks <= 0)
        {
            everyTicks = 1;
        }

        var logEveryTicks = ReadInt(config, "Runtime:TelemetrySummary:LogEvery", 50);
        if (logEveryTicks <= 0)
        {
            logEveryTicks = 50;
        }

        var maxSampleAgeMs = ReadDouble(config, "Runtime:TelemetrySummary:MaxSampleAgeMs", 2_000.0);
        if (!double.IsFinite(maxSampleAgeMs) || maxSampleAgeMs <= 0.0)
        {
            maxSampleAgeMs = 2_000.0;
        }

        var truthProvider = new PhysicsTruthProvider("RuntimeTelemetryTruthProvider");

        var sensorOptions = SensorRuntimeOptions.Default();
        sensorOptions.Mode = SensorRuntimeMode.CSharpPrimary;
        sensorOptions.EnableDefaultSimSensors = ReadBool(config, "Runtime:TelemetrySummary:EnableDefaultSimSensors", true);
        sensorOptions.EnableImu = ReadBool(config, "Runtime:TelemetrySummary:EnableImu", true);
        sensorOptions.EnableGps = ReadBool(config, "Runtime:TelemetrySummary:EnableGps", true);

        /*
        * Depth sensÃƒÂ¶rÃƒÂ¼ yÃƒÂ¼zey aracÃ„Â± iÃƒÂ§in varsayÃ„Â±lan zorunlu deÃ„Å¸ildir.
        * SualtÃ„Â± / degraded GPS kaybÃ„Â± testlerinde Runtime:TelemetrySummary:EnableDepth=true ile aÃƒÂ§Ã„Â±lÃ„Â±r.
        */
        sensorOptions.EnableDepth = ReadBool(config, "Runtime:TelemetrySummary:EnableDepth", false);

        /*
         * Parkur-2 engel algÃ„Â±lama iÃƒÂ§in LiDAR varsayÃ„Â±lan olarak aÃƒÂ§Ã„Â±k olmalÃ„Â±.
         * KonfigÃƒÂ¼rasyonda Runtime:TelemetrySummary:EnableLidar=false verilirse kapatÃ„Â±labilir.
         */
        sensorOptions.EnableLidar = ReadBool(config, "Runtime:TelemetrySummary:EnableLidar", true);
        sensorOptions.EnableCamera = ReadBool(config, "Runtime:TelemetrySummary:EnableCamera", false);

        var lidarOptions = SimLidarBackendOptions.Default();
        lidarOptions.SensorId = ReadString(config, "Runtime:TelemetrySummary:Lidar:SensorId", "lidar0");
        lidarOptions.Source = ReadString(config, "Runtime:TelemetrySummary:Lidar:Source", "sim_lidar");
        lidarOptions.FrameId = ReadString(config, "Runtime:TelemetrySummary:Lidar:FrameId", "lidar_link");
        lidarOptions.RateHz = ReadDouble(config, "Runtime:TelemetrySummary:Lidar:RateHz", 10.0);
        lidarOptions.BeamCount = ReadInt(config, "Runtime:TelemetrySummary:Lidar:BeamCount", 181);
        lidarOptions.FovDeg = ReadDouble(config, "Runtime:TelemetrySummary:Lidar:FovDeg", 120.0);
        lidarOptions.RangeMinMeters = ReadDouble(config, "Runtime:TelemetrySummary:Lidar:RangeMinMeters", 0.05);
        lidarOptions.RangeMaxMeters = ReadDouble(config, "Runtime:TelemetrySummary:Lidar:RangeMaxMeters", 30.0);
        lidarOptions.NoiseMeters = ReadDouble(config, "Runtime:TelemetrySummary:Lidar:NoiseMeters", 0.01);
        lidarOptions.CalibrationId = ReadString(config, "Runtime:TelemetrySummary:Lidar:CalibrationId", "sim-lidar-default");

        lidarOptions = lidarOptions.Sanitized();

        var registry = new SensorBackendRegistry()
            .Register(
                key: "sim_imu",
                factory: _ => new Hydronom.Runtime.Sensors.Backends.Sim.SimImuSensor(
                    truthProvider: truthProvider)
            )
            .Register(
                key: "sim_gps",
                factory: _ => new Hydronom.Runtime.Sensors.Backends.Sim.SimGpsSensor(
                    truthProvider: truthProvider)
            )
            .Register(
                key: "sim_depth",
                factory: _ => new Hydronom.Runtime.Sensors.Backends.Sim.SimDepthSensor(
                    truthProvider: truthProvider)
            )
            .Register(
                key: "sim_lidar",
                factory: _ => new SimLidarBackend(
                    options: lidarOptions,
                    truthProvider: truthProvider,
                    worldModel: runtimeWorldModel)
            );

        var sensorRuntime = new SensorRuntimeBuilder(registry).Build(sensorOptions);

        var policy = StateAuthorityPolicy.CSharpPrimary with
        {
            MaxStateAgeMs = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxStateAgeMs", 2_000.0),

            /*
             * CDSE degraded modda GPS yokken IMU + Depth ile yaklaÃ…Å¸Ã„Â±k 0.48 confidence ÃƒÂ¼retebilir.
             * 0.45 eÃ…Å¸iÃ„Å¸i bu durumu kabul eder; depth-only gibi ÃƒÂ§ok dÃƒÂ¼Ã…Å¸ÃƒÂ¼k gÃƒÂ¼venli ÃƒÂ§Ã„Â±ktÃ„Â±lar ise reddedilmeye devam eder.
             */
            MinConfidence = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MinConfidence", 0.45),

            MaxTeleportDistanceMeters = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxTeleportDistanceMeters", 50.0),
            MaxPlausibleSpeedMps = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxPlausibleSpeedMps", 50.0),
            MaxPlausibleYawRateDegSec = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxPlausibleYawRateDegSec", 360.0),
            RequireFrameMatch = ReadBool(config, "Runtime:TelemetrySummary:StateAuthority:RequireFrameMatch", true)
        };

        var authority = new StateAuthorityManager(policy);
        var stateStore = new VehicleStateStore(vehicleId, StateAuthorityMode.CSharpPrimary);
        var statePipeline = new StateUpdatePipeline(authority, stateStore);
        var stateTelemetryBridge = new StateTelemetryBridge();

        /*
         * CDSE artÃ„Â±k runtime telemetry hattÃ„Â±nÃ„Â±n primary estimator'Ã„Â±dÃ„Â±r.
         *
         * GpsImuStateEstimator yalnÃ„Â±zca GPS+IMU ÃƒÂ¶zel senaryosunda ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±rken,
         * CapabilityDrivenStateEstimator GPS, IMU, Depth ve ileride eklenecek diÃ„Å¸er capability'lere gÃƒÂ¶re
         * eksik sensÃƒÂ¶r durumlarÃ„Â±nda da en iyi state tahminini ÃƒÂ¼retmeye ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±r.
         */
        var estimator = new CapabilityDrivenStateEstimator();
        var runner = new StateEstimatorRunner(estimator);

        var fusionHost = new FusionRuntimeHost(
            estimatorRunner: runner,
            statePipeline: statePipeline,
            stateStore: stateStore,
            stateTelemetryBridge: stateTelemetryBridge
        );

        var telemetryHost = new RuntimeTelemetryHost(
            snapshotBuilder: new RuntimeOperationSnapshotBuilder(runtimeId),
            telemetryBridge: new TelemetryBridge(),
            publisher: new TcpRuntimeTelemetryPublisher(tcpJsonServer)
        );

        var pipeline = new RuntimeTelemetryPipeline(
            sensorRuntime: sensorRuntime,
            fusionHost: fusionHost,
            stateStore: stateStore,
            telemetryHost: telemetryHost,
            vehicleId: vehicleId,
            frameId: frameId,
            maxSampleAgeMs: maxSampleAgeMs
        );

        var runtime = new RuntimeTelemetryRuntime(
            pipeline: pipeline,
            truthProvider: truthProvider,
            vehicleId: vehicleId,
            everyTicks: everyTicks,
            logEveryTicks: logEveryTicks
        );

        runtime.PublishTruthFromVehicleState(initialState, tickIndex: 0);

        Console.WriteLine(
            "[RT-TEL] Enabled Ã¢â€ â€™ " +
            $"runtimeId={runtimeId} vehicleId={vehicleId} vehicleSource={(activeVehicleContext?.HasProfile == true ? "activeVehicleProfile" : "config")} every={everyTicks} ticks " +
            $"estimator={estimator.Name} " +
            $"sensors=imu:{sensorOptions.EnableImu},gps:{sensorOptions.EnableGps},depth:{sensorOptions.EnableDepth},lidar:{sensorOptions.EnableLidar},camera:{sensorOptions.EnableCamera} " +
            $"worldModel={(runtimeWorldModel is null ? "none" : "shared")}"
        );

        return runtime;
    }

    /// <summary>
    /// Belirli tick'te runtime telemetry pipeline ÃƒÂ§alÃ„Â±Ã…Å¸tÃ„Â±rÃ„Â±lmalÃ„Â± mÃ„Â±?
    /// </summary>
    private static bool ShouldTickRuntimeTelemetry(
        RuntimeTelemetryRuntime? telemetryRuntime,
        long tickIndex)
    {
        if (telemetryRuntime is null)
        {
            return false;
        }

        if (tickIndex < 0)
        {
            return false;
        }

        return tickIndex % telemetryRuntime.EveryTicks == 0;
    }

    /// <summary>
    /// Runtime telemetry pipeline'Ã„Â± gÃƒÂ¼venli Ã…Å¸ekilde ÃƒÂ§alÃ„Â±Ã…Å¸tÃ„Â±rÃ„Â±r.
    ///
    /// Telemetry yayÃ„Â±n hatasÃ„Â± ana runtime kontrol dÃƒÂ¶ngÃƒÂ¼sÃƒÂ¼nÃƒÂ¼ dÃƒÂ¼Ã…Å¸ÃƒÂ¼rmemelidir;
    /// bu yÃƒÂ¼zden exception yakalanÃ„Â±r ve yalnÃ„Â±zca loglanÃ„Â±r.
    /// </summary>
    private static async Task TryTickRuntimeTelemetryPipelineAsync(
        RuntimeTelemetryRuntime? telemetryRuntime,
        RuntimeOptions runtime,
        VehicleState state,
        long tickIndex,
        CancellationToken cancellationToken)
    {
        if (!ShouldTickRuntimeTelemetry(telemetryRuntime, tickIndex))
        {
            return;
        }

        if (telemetryRuntime is null)
        {
            return;
        }

        try
        {
            telemetryRuntime.PublishTruthFromVehicleState(state, tickIndex);

            var result = await telemetryRuntime.Pipeline
                .TickAsync(cancellationToken)
                .ConfigureAwait(false);

            if (runtime.LogVerbose || tickIndex % telemetryRuntime.LogEveryTicks == 0)
            {
                Console.WriteLine(
                    "[RT-TEL] " +
                    $"tick={tickIndex} executed={result.Executed} " +
                    $"samples={result.SampleCount} sensors={result.HealthySensorCount}/{result.SensorCount} " +
                    $"candidate={result.CandidateProduced} accepted={result.StateAccepted} " +
                    $"published={result.Published} health={result.OverallHealth} reason={result.Reason}"
                );
            }
        }
        catch (OperationCanceledException)
        {
            // Normal kapanÃ„Â±Ã…Å¸.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RT-TEL] publish error: {ex.Message}");
        }
    }
}