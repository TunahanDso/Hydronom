using Hydronom.Core.Domain;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.State.Authority;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Operations.Snapshots;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.Runtime;
using Hydronom.Runtime.Simulation.Physics;
using Hydronom.Runtime.StateRuntime;
using Hydronom.Runtime.Telemetry;
using Microsoft.Extensions.Configuration;
using Hydronom.Core.Sensors.Common.Models;

partial class Program
{
    /// <summary>
    /// Runtime ana döngüsünde kullanılacak C# Primary telemetry pipeline çalışma zamanı tutucusu.
    ///
    /// Bu yapı sensör verisini TCP'den almaz.
    /// C# Primary akışta sensörler runtime içinde okunur; TCP yalnızca Gateway/Ops telemetry yayını için kullanılır.
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
    /// RuntimeTelemetryPipeline kurulmalı mı?
    /// </summary>
    private static bool IsRuntimeTelemetryEnabled(IConfiguration config)
    {
        return ReadBool(config, "Runtime:TelemetrySummary:Enabled", true);
    }

    /// <summary>
    /// C# Primary RuntimeTelemetryPipeline oluşturur.
    ///
    /// Not:
    /// - Bu pipeline runtime içindeki C# sensör/fusion/state hattını kullanır.
    /// - TcpJsonServer yalnızca RuntimeTelemetrySummary yayınlamak için kullanılır.
    /// - Python/TcpJsonFrameSource fallback hattı bundan bağımsız kalır.
    /// </summary>
    private static RuntimeTelemetryRuntime CreateRuntimeTelemetryRuntime(
        IConfiguration config,
        TcpJsonServer tcpJsonServer,
        VehicleState initialState)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(tcpJsonServer);

        var runtimeId = ReadString(config, "Runtime:TelemetrySummary:RuntimeId", "hydronom_runtime");
        var vehicleId = ReadString(config, "Runtime:TelemetrySummary:VehicleId", "hydronom-main");
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
        sensorOptions.EnableLidar = ReadBool(config, "Runtime:TelemetrySummary:EnableLidar", false);
        sensorOptions.EnableCamera = ReadBool(config, "Runtime:TelemetrySummary:EnableCamera", false);

        var registry = new SensorBackendRegistry()
            .Register(
                key: "sim_imu",
                factory: _ => new Hydronom.Runtime.Sensors.Imu.SimImuSensor(truthProvider: truthProvider)
            )
            .Register(
                key: "sim_gps",
                factory: _ => new Hydronom.Runtime.Sensors.Gps.SimGpsSensor(truthProvider: truthProvider)
            );

        var sensorRuntime = new SensorRuntimeBuilder(registry).Build(sensorOptions);

        var policy = StateAuthorityPolicy.CSharpPrimary with
        {
            MaxStateAgeMs = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxStateAgeMs", 2_000.0),
            MinConfidence = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MinConfidence", 0.50),
            MaxTeleportDistanceMeters = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxTeleportDistanceMeters", 50.0),
            MaxPlausibleSpeedMps = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxPlausibleSpeedMps", 50.0),
            MaxPlausibleYawRateDegSec = ReadDouble(config, "Runtime:TelemetrySummary:StateAuthority:MaxPlausibleYawRateDegSec", 360.0),
            RequireFrameMatch = ReadBool(config, "Runtime:TelemetrySummary:StateAuthority:RequireFrameMatch", true)
        };

        var authority = new StateAuthorityManager(policy);
        var stateStore = new VehicleStateStore(vehicleId, StateAuthorityMode.CSharpPrimary);
        var statePipeline = new StateUpdatePipeline(authority, stateStore);
        var stateTelemetryBridge = new StateTelemetryBridge();

        var estimator = new GpsImuStateEstimator();
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
            "[RT-TEL] Enabled → " +
            $"runtimeId={runtimeId} vehicleId={vehicleId} every={everyTicks} ticks " +
            $"sensors=imu:{sensorOptions.EnableImu},gps:{sensorOptions.EnableGps},lidar:{sensorOptions.EnableLidar},camera:{sensorOptions.EnableCamera}"
        );

        return runtime;
    }

    /// <summary>
    /// Belirli tick'te runtime telemetry pipeline çalıştırılmalı mı?
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
    /// Runtime telemetry pipeline'ı güvenli şekilde çalıştırır.
    ///
    /// Telemetry yayın hatası ana runtime kontrol döngüsünü düşürmemelidir;
    /// bu yüzden exception yakalanır ve yalnızca loglanır.
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
            // Normal kapanış.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RT-TEL] publish error: {ex.Message}");
        }
    }
}