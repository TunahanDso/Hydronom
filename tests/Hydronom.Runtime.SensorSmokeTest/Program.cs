using Hydronom.Core.Domain;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Gps.Models;
using Hydronom.Core.Sensors.Imu.Models;
using Hydronom.Core.Sensors.Lidar.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.Backends.Lidar;
using Hydronom.Runtime.Sensors.Backends.Sim;
using Hydronom.Runtime.Sensors.Runtime;
using Hydronom.Runtime.Simulation.Physics;
using Hydronom.Runtime.World.Runtime;

Console.WriteLine("=== Hydronom Runtime Sensor Smoke Test ===");
Console.WriteLine();

var defaultResult = await RunDefaultAutoWiringScenarioAsync();

Console.WriteLine();
Console.WriteLine("------------------------------------------------------------");
Console.WriteLine();

var truthFedResult = await RunTruthFedScenarioAsync();

Console.WriteLine();
Console.WriteLine("------------------------------------------------------------");
Console.WriteLine();

var lidarResult = await RunTruthFedLidarRaycastScenarioAsync();

Console.WriteLine();
Console.WriteLine("------------------------------------------------------------");
Console.WriteLine();

if (!defaultResult || !truthFedResult || !lidarResult)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("FAIL: Sensor smoke test başarısız.");
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: Tüm sensor smoke test senaryoları başarılı.");
Console.ResetColor();

return 0;

static async Task<bool> RunDefaultAutoWiringScenarioAsync()
{
    Console.WriteLine("[1] Default CSharpPrimary auto-wiring scenario");
    Console.WriteLine();

    var options = SensorRuntimeOptions.Default();

    options.Mode = SensorRuntimeMode.CSharpPrimary;
    options.EnableDefaultSimSensors = true;
    options.EnableImu = true;
    options.EnableGps = true;
    options.EnableLidar = false;
    options.EnableCamera = false;

    var selector = new SensorRuntimeSelector(options);
    var runtime = selector.CreateRuntime();

    Console.WriteLine($"Runtime type : {runtime.GetType().Name}");
    Console.WriteLine($"Runtime mode : {runtime.Mode}");
    Console.WriteLine($"Is running   : {runtime.IsRunning}");

    if (runtime is not CSharpSensorRuntime csharpRuntime)
        return Fail("Runtime CSharpSensorRuntime değil.");

    Console.WriteLine($"Backend count before start : {csharpRuntime.BackendCount}");
    Console.WriteLine($"Has backends               : {csharpRuntime.HasBackends}");

    if (csharpRuntime.BackendCount != 2)
        return Fail("Beklenen backend sayısı 2 olmalıydı. Beklenen: sim_imu + sim_gps.");

    await runtime.StartAsync();

    Console.WriteLine();
    Console.WriteLine("Runtime started.");
    Console.WriteLine($"Is running : {runtime.IsRunning}");

    var samples = await runtime.ReadBatchAsync();

    PrintSamples(samples);

    var health = runtime.GetHealth();
    PrintHealth(
        health.SensorCount,
        health.HealthyCount,
        health.DegradedCount,
        health.StaleCount,
        health.OfflineCount,
        health.HasCriticalIssue,
        health.Summary
    );

    await runtime.StopAsync();

    Console.WriteLine();
    Console.WriteLine("Runtime stopped.");
    Console.WriteLine($"Is running : {runtime.IsRunning}");

    if (!ValidateHasImuAndGps(samples))
        return false;

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine();
    Console.WriteLine("PASS: Default auto-wiring çalışıyor. sim_imu + sim_gps sample üretti.");
    Console.ResetColor();

    return true;
}

static async Task<bool> RunTruthFedScenarioAsync()
{
    Console.WriteLine("[2] Truth-fed CSharpPrimary sensor scenario");
    Console.WriteLine();

    var truthProvider = new PhysicsTruthProvider("SmokeTestTruthProvider");

    var truth = new PhysicsTruthState(
        VehicleId: "SMOKE-VEHICLE-001",
        TimestampUtc: DateTime.UtcNow,
        Position: new Vec3(12.5, -4.25, 1.2),
        Velocity: new Vec3(2.0, 1.0, 0.0),
        Acceleration: new Vec3(0.3, -0.1, 0.05),
        Orientation: new Orientation(3.0, -2.0, 47.0),
        AngularVelocityDegSec: new Vec3(1.5, -0.5, 12.0),
        AngularAccelerationDegSec: Vec3.Zero,
        LastAppliedLoads: PhysicsLoads.Zero,
        EnvironmentSummary: "SMOKE_TEST",
        FrameId: "map",
        TraceId: "truth-fed-smoke-test"
    );

    truthProvider.Publish(truth);

    var options = SensorRuntimeOptions.Default();

    options.Mode = SensorRuntimeMode.CSharpPrimary;
    options.EnableDefaultSimSensors = true;
    options.EnableImu = true;
    options.EnableGps = true;
    options.EnableLidar = false;
    options.EnableCamera = false;

    var registry = new SensorBackendRegistry()
        .Register(
            key: "sim_imu",
            factory: _ => new Hydronom.Runtime.Sensors.Imu.SimImuSensor(truthProvider: truthProvider)
        )
        .Register(
            key: "sim_gps",
            factory: _ => new Hydronom.Runtime.Sensors.Gps.SimGpsSensor(truthProvider: truthProvider)
        );

    var builder = new SensorRuntimeBuilder(registry);
    var runtime = builder.Build(options);

    Console.WriteLine($"Runtime type : {runtime.GetType().Name}");
    Console.WriteLine($"Runtime mode : {runtime.Mode}");
    Console.WriteLine($"Truth available : {truthProvider.IsAvailable}");
    Console.WriteLine($"Truth last UTC  : {truthProvider.LastTruthUtc:O}");

    if (runtime is not CSharpSensorRuntime csharpRuntime)
        return Fail("Truth-fed runtime CSharpSensorRuntime değil.");

    Console.WriteLine($"Backend count before start : {csharpRuntime.BackendCount}");
    Console.WriteLine($"Has backends               : {csharpRuntime.HasBackends}");

    if (csharpRuntime.BackendCount != 2)
        return Fail("Truth-fed senaryoda beklenen backend sayısı 2 olmalıydı.");

    await runtime.StartAsync();

    var samples = await runtime.ReadBatchAsync();

    PrintSamples(samples);

    await runtime.StopAsync();

    if (!ValidateHasImuAndGps(samples))
        return false;

    var gpsSample = samples.First(x => x.DataKind == SensorDataKind.Gps);
    var imuSample = samples.First(x => x.DataKind == SensorDataKind.Imu);

    if (gpsSample.Data is not GpsSampleData gps)
        return Fail("GPS sample data tipi GpsSampleData değil.");

    if (imuSample.Data is not ImuSampleData imu)
        return Fail("IMU sample data tipi ImuSampleData değil.");

    var gpsX = gps.X ?? 0.0;
    var gpsY = gps.Y ?? 0.0;
    var gpsZ = gps.Z ?? 0.0;

    var imuRollDeg = imu.RollDeg ?? 0.0;
    var imuPitchDeg = imu.PitchDeg ?? 0.0;
    var imuYawDeg = imu.YawDeg ?? 0.0;

    Console.WriteLine();
    Console.WriteLine("Truth-fed checks:");
    Console.WriteLine($"- GPS X/Y/Z          : {gpsX:F3}, {gpsY:F3}, {gpsZ:F3}");
    Console.WriteLine($"- Truth X/Y/Z        : {truth.Position.X:F3}, {truth.Position.Y:F3}, {truth.Position.Z:F3}");
    Console.WriteLine($"- IMU Roll/Pitch/Yaw : {imuRollDeg:F3}, {imuPitchDeg:F3}, {imuYawDeg:F3}");
    Console.WriteLine($"- Truth R/P/Y        : {truth.Orientation.RollDeg:F3}, {truth.Orientation.PitchDeg:F3}, {truth.Orientation.YawDeg:F3}");

    /*
     * GPS içinde noise olduğu için X/Y değerlerini birebir eşitlemiyoruz.
     * Noise scale options'a bağlıdır. Burada makul toleransla truth'a yakınlık kontrol edilir.
     */
    if (Math.Abs(gpsX - truth.Position.X) > 2.0)
        return Fail($"GPS X truth'a yakın değil. gps.X={gpsX:F3}, truth.X={truth.Position.X:F3}");

    if (Math.Abs(gpsY - truth.Position.Y) > 2.0)
        return Fail($"GPS Y truth'a yakın değil. gps.Y={gpsY:F3}, truth.Y={truth.Position.Y:F3}");

    if (Math.Abs(gpsZ - truth.Position.Z) > 0.001)
        return Fail($"GPS Z truth ile uyumlu değil. gps.Z={gpsZ:F3}, truth.Z={truth.Position.Z:F3}");

    if (Math.Abs(imuRollDeg - truth.Orientation.RollDeg) > 0.001)
        return Fail($"IMU roll truth ile uyumlu değil. imu.Roll={imuRollDeg:F3}, truth.Roll={truth.Orientation.RollDeg:F3}");

    if (Math.Abs(imuPitchDeg - truth.Orientation.PitchDeg) > 0.001)
        return Fail($"IMU pitch truth ile uyumlu değil. imu.Pitch={imuPitchDeg:F3}, truth.Pitch={truth.Orientation.PitchDeg:F3}");

    if (Math.Abs(imuYawDeg - truth.Orientation.YawDeg) > 0.001)
        return Fail($"IMU yaw truth ile uyumlu değil. imu.Yaw={imuYawDeg:F3}, truth.Yaw={truth.Orientation.YawDeg:F3}");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine();
    Console.WriteLine("PASS: Truth-fed sim IMU/GPS PhysicsTruthState üzerinden sample üretti.");
    Console.ResetColor();

    return true;
}

static async Task<bool> RunTruthFedLidarRaycastScenarioAsync()
{
    Console.WriteLine("[3] Truth-fed Sim LiDAR raycast scenario");
    Console.WriteLine();

    var truthProvider = new PhysicsTruthProvider("SmokeTestLidarTruthProvider");

    var truth = new PhysicsTruthState(
        VehicleId: "SMOKE-LIDAR-VEHICLE-001",
        TimestampUtc: DateTime.UtcNow,
        Position: Vec3.Zero,
        Velocity: Vec3.Zero,
        Acceleration: Vec3.Zero,
        Orientation: new Orientation(0.0, 0.0, 0.0),
        AngularVelocityDegSec: Vec3.Zero,
        AngularAccelerationDegSec: Vec3.Zero,
        LastAppliedLoads: PhysicsLoads.Zero,
        EnvironmentSummary: "LIDAR_RAYCAST_SMOKE_TEST",
        FrameId: "map",
        TraceId: "truth-fed-lidar-smoke-test"
    );

    truthProvider.Publish(truth);

    var worldModel = new RuntimeWorldModel();

    worldModel.Upsert(new HydronomWorldObject
    {
        Id = "obstacle_front_10m",
        Kind = "obstacle",
        Name = "Front Obstacle 10m",
        Layer = "obstacle",
        X = 10.0,
        Y = 0.0,
        Z = 0.0,
        Radius = 0.5,
        IsActive = true,
        IsBlocking = true
    });

    var options = SensorRuntimeOptions.Default();

    options.Mode = SensorRuntimeMode.CSharpPrimary;
    options.EnableDefaultSimSensors = true;
    options.EnableImu = false;
    options.EnableGps = false;
    options.EnableLidar = true;
    options.EnableCamera = false;

    var lidarOptions = new LidarBackendOptions
    {
        SensorId = "lidar0",
        Source = "sim_lidar",
        FrameId = "lidar_link",
        CalibrationId = "smoke_test_lidar",
        RateHz = 10.0,
        RangeMinMeters = 0.05,
        RangeMaxMeters = 30.0,
        FovDeg = 180.0,
        BeamCount = 181,
        NoiseMeters = 0.0,
        UseWorldModel = true
    };

    var registry = new SensorBackendRegistry()
        .Register(
            key: "sim_lidar",
            factory: _ => new SimLidarBackend(
                options: lidarOptions,
                truthProvider: truthProvider,
                worldModel: worldModel
            )
        );

    var builder = new SensorRuntimeBuilder(registry);
    var runtime = builder.Build(options);

    Console.WriteLine($"Runtime type : {runtime.GetType().Name}");
    Console.WriteLine($"Runtime mode : {runtime.Mode}");
    Console.WriteLine($"Truth available : {truthProvider.IsAvailable}");
    Console.WriteLine($"World object count : {worldModel.Count}");

    if (runtime is not CSharpSensorRuntime csharpRuntime)
        return Fail("LiDAR runtime CSharpSensorRuntime değil.");

    Console.WriteLine($"Backend count before start : {csharpRuntime.BackendCount}");
    Console.WriteLine($"Has backends               : {csharpRuntime.HasBackends}");

    if (csharpRuntime.BackendCount != 1)
        return Fail("LiDAR senaryoda beklenen backend sayısı 1 olmalıydı.");

    await runtime.StartAsync();

    var samples = await runtime.ReadBatchAsync();

    PrintSamples(samples);

    await runtime.StopAsync();

    if (samples.Count != 1)
        return Fail($"LiDAR senaryoda 1 sample bekleniyordu. Actual={samples.Count}");

    var lidarSample = samples.FirstOrDefault(x => x.DataKind == SensorDataKind.Lidar);

    if (lidarSample.Data is not LaserScan scan)
        return Fail("LiDAR sample data tipi LaserScan değil.");

    Console.WriteLine();
    Console.WriteLine("LiDAR checks:");
    Console.WriteLine($"- Beam count       : {scan.BeamCount}");
    Console.WriteLine($"- Has ranges       : {scan.HasRanges}");
    Console.WriteLine($"- Nearest range    : {scan.NearestRangeMeters?.ToString("F3") ?? "null"}");
    Console.WriteLine($"- Obstacle hints   : {scan.ObstacleHints.Count}");

    if (!scan.HasRanges)
        return Fail("LaserScan range üretmedi.");

    if (scan.BeamCount != 181)
        return Fail($"LaserScan beam count 181 olmalıydı. Actual={scan.BeamCount}");

    var nearest = scan.NearestRangeMeters;

    if (nearest is null)
        return Fail("LaserScan nearest range null geldi.");

    /*
     * Obstacle merkezi 10 m'de, radius 0.5 m.
     * Ray dairenin ön yüzeyine çarpacağı için beklenen mesafe yaklaşık 9.5 m'dir.
     */
    if (Math.Abs(nearest.Value - 9.5) > 0.25)
        return Fail($"LiDAR nearest range beklenen değere yakın değil. nearest={nearest.Value:F3}, expected≈9.5");

    var hasObstacleHint = scan.ObstacleHints.Any(x =>
        x.ObjectId.Equals("obstacle_front_10m", StringComparison.OrdinalIgnoreCase));

    if (!hasObstacleHint)
        return Fail("LaserScan obstacle_front_10m için obstacle hint üretmedi.");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine();
    Console.WriteLine("PASS: Truth-fed Sim LiDAR RuntimeWorldModel içindeki obstacle'ı raycast ile gördü.");
    Console.ResetColor();

    return true;
}

static bool ValidateHasImuAndGps(IReadOnlyList<Hydronom.Core.Sensors.Common.Models.SensorSample> samples)
{
    if (samples.Count < 2)
        return Fail("En az 2 sample bekleniyordu: IMU + GPS.");

    var hasImu = samples.Any(x => x.DataKind == SensorDataKind.Imu);
    var hasGps = samples.Any(x => x.DataKind == SensorDataKind.Gps);

    if (!hasImu || !hasGps)
        return Fail($"IMU/GPS sample eksik. hasImu={hasImu}, hasGps={hasGps}");

    return true;
}

static void PrintSamples(IReadOnlyList<Hydronom.Core.Sensors.Common.Models.SensorSample> samples)
{
    Console.WriteLine();
    Console.WriteLine($"Sample count : {samples.Count}");

    foreach (var sample in samples)
    {
        Console.WriteLine(
            $"- {sample.Sensor.SensorId} | source={sample.Sensor.SourceId} | kind={sample.DataKind} | valid={sample.IsValid} | seq={sample.Sequence}"
        );
    }
}

static void PrintHealth(
    int sensorCount,
    int healthyCount,
    int degradedCount,
    int staleCount,
    int offlineCount,
    bool hasCriticalIssue,
    string summary)
{
    Console.WriteLine();
    Console.WriteLine("Health:");
    Console.WriteLine($"- Sensor count       : {sensorCount}");
    Console.WriteLine($"- Healthy count      : {healthyCount}");
    Console.WriteLine($"- Degraded count     : {degradedCount}");
    Console.WriteLine($"- Stale count        : {staleCount}");
    Console.WriteLine($"- Offline count      : {offlineCount}");
    Console.WriteLine($"- Has critical issue : {hasCriticalIssue}");
    Console.WriteLine($"- Summary            : {summary}");
}

static bool Fail(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();
    return false;
}