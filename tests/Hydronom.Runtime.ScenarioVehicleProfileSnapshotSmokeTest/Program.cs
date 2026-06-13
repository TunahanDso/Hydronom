using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Scenarios.Runtime;
using Hydronom.Runtime.Vehicles;
using Hydronom.Runtime.World.Runtime;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);

if (repoRoot is null)
{
    Fail("Repo root bulunamadı.");
    return;
}

var profilesRoot = Path.Combine(
    repoRoot,
    "src",
    "Hydronom.Runtime",
    "Vehicles",
    "Profiles");

Console.WriteLine("=== Hydronom Runtime Scenario Vehicle Profile Snapshot Smoke Test ===");
Console.WriteLine($"Repo root     : {repoRoot}");
Console.WriteLine($"Profiles root : {profilesRoot}");
Console.WriteLine();

Assert(Directory.Exists(profilesRoot), $"Profiles root bulunamadı: {profilesRoot}");

RunMainUuvSnapshotTest(profilesRoot);
RunMiniRovSnapshotTest(profilesRoot);
RunSurfaceSnapshotTest(profilesRoot);
RunNoProfileSnapshotTest();

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("RUNTIME_SCENARIO_VEHICLE_PROFILE_SNAPSHOT_SMOKE_TEST_OK");
Console.ResetColor();

static void RunMainUuvSnapshotTest(string profilesRoot)
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["VehicleProfile:Enabled"] = "true",
        ["VehicleProfile:ProfilesRoot"] = profilesRoot,
        ["VehicleProfile:ProfileId"] = "hydronom_uuv_main_2026",
        ["ScenarioRuntime:VehicleProfileId"] = "hydronom_uuv_main_2026",
        ["ScenarioRuntime:VehicleId"] = "hydronom-uuv-main",
        ["Runtime:TelemetrySummary:VehicleId"] = "hydronom-uuv-main"
    });

    var binding = VehicleProfileRuntimeBootstrapper.Bootstrap(config, AppContext.BaseDirectory);

    var controller = new RuntimeScenarioController(
        config,
        new FakeTaskManager(),
        new RuntimeWorldModel(),
        binding.ActiveContext);

    var snapshot = controller.GetSnapshot("main-uuv-test");

    Console.WriteLine(BuildSnapshotLine(snapshot));

    Assert(snapshot.VehicleProfileActive, "Main UUV snapshot VehicleProfileActive=false geldi.");
    Assert(snapshot.VehicleProfileId == "hydronom_uuv_main_2026", "Main UUV snapshot profile id yanlış.");
    Assert(snapshot.VehicleId == "hydronom-uuv-main", "Main UUV runtime vehicle id yanlış.");
    Assert(snapshot.VehiclePlatformKind == "UnderwaterVehicle", "Main UUV platform yanlış.");
    Assert(snapshot.VehicleDisplayName == "Hydronom UUV Main 2026", "Main UUV display name yanlış.");
    Assert(snapshot.VehicleIsUnderwater, "Main UUV underwater=false geldi.");
    Assert(!snapshot.VehicleIsMiniRov, "Main UUV miniRov=true gelmemeliydi.");
    Assert(snapshot.VehicleHasThrusters, "Main UUV thruster=false geldi.");
    Assert(snapshot.VehicleHasReverseAuthority, "Main UUV reverse=false geldi.");
    Assert(snapshot.VehicleCanGenerateLateralForce, "Main UUV lateral=false geldi.");
    Assert(snapshot.VehicleCanGenerateYawMoment, "Main UUV yaw=false geldi.");
    Assert(!string.IsNullOrWhiteSpace(snapshot.VehicleCapabilitySummary), "Main UUV capability summary boş geldi.");
}

static void RunMiniRovSnapshotTest(string profilesRoot)
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["VehicleProfile:Enabled"] = "true",
        ["VehicleProfile:ProfilesRoot"] = profilesRoot,
        ["VehicleProfile:ProfileId"] = "hydronom_mini_rov_2026",
        ["ScenarioRuntime:VehicleId"] = "hydronom-mini-rov",
        ["Runtime:TelemetrySummary:VehicleId"] = "hydronom-mini-rov"
    });

    var binding = VehicleProfileRuntimeBootstrapper.Bootstrap(config, AppContext.BaseDirectory);

    var controller = new RuntimeScenarioController(
        config,
        new FakeTaskManager(),
        new RuntimeWorldModel(),
        binding.ActiveContext);

    var snapshot = controller.GetSnapshot("mini-rov-test");

    Console.WriteLine(BuildSnapshotLine(snapshot));

    Assert(snapshot.VehicleProfileActive, "Mini ROV snapshot VehicleProfileActive=false geldi.");
    Assert(snapshot.VehicleProfileId == "hydronom_mini_rov_2026", "Mini ROV profile id yanlış.");
    Assert(snapshot.VehicleId == "hydronom-mini-rov", "Mini ROV runtime vehicle id yanlış.");
    Assert(snapshot.VehiclePlatformKind == "MiniRov", "Mini ROV platform yanlış.");
    Assert(snapshot.VehicleIsUnderwater, "Mini ROV underwater=false geldi.");
    Assert(snapshot.VehicleIsMiniRov, "Mini ROV IsMiniRov=false geldi.");
    Assert(snapshot.VehicleHasThrusters, "Mini ROV thruster=false geldi.");
    Assert(snapshot.VehicleHasReverseAuthority, "Mini ROV reverse=false geldi.");
    Assert(!snapshot.VehicleCanGenerateLateralForce, "Mini ROV lateral=true gelmemeliydi.");
    Assert(snapshot.VehicleCanGenerateYawMoment, "Mini ROV yaw=false geldi.");
}

static void RunSurfaceSnapshotTest(string profilesRoot)
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["VehicleProfile:Enabled"] = "true",
        ["VehicleProfile:ProfilesRoot"] = profilesRoot,
        ["VehicleProfile:ProfileId"] = "hydronom_surface_mk1",
        ["ScenarioRuntime:VehicleId"] = "hydronom-surface-mk1",
        ["Runtime:TelemetrySummary:VehicleId"] = "hydronom-surface-mk1"
    });

    var binding = VehicleProfileRuntimeBootstrapper.Bootstrap(config, AppContext.BaseDirectory);

    var controller = new RuntimeScenarioController(
        config,
        new FakeTaskManager(),
        new RuntimeWorldModel(),
        binding.ActiveContext);

    var snapshot = controller.GetSnapshot("surface-test");

    Console.WriteLine(BuildSnapshotLine(snapshot));

    Assert(snapshot.VehicleProfileActive, "Surface snapshot VehicleProfileActive=false geldi.");
    Assert(snapshot.VehicleProfileId == "hydronom_surface_mk1", "Surface profile id yanlış.");
    Assert(snapshot.VehicleId == "hydronom-surface-mk1", "Surface runtime vehicle id yanlış.");
    Assert(snapshot.VehiclePlatformKind == "SurfaceVessel", "Surface platform yanlış.");
    Assert(!snapshot.VehicleIsUnderwater, "Surface underwater=true gelmemeliydi.");
    Assert(!snapshot.VehicleIsMiniRov, "Surface MiniRov=true gelmemeliydi.");
    Assert(snapshot.VehicleHasThrusters, "Surface thruster=false geldi.");
    Assert(!snapshot.VehicleHasReverseAuthority, "Surface reverse=true gelmemeliydi.");
    Assert(!snapshot.VehicleCanGenerateLateralForce, "Surface lateral=true gelmemeliydi.");
    Assert(snapshot.VehicleCanGenerateYawMoment, "Surface yaw=false geldi.");
}

static void RunNoProfileSnapshotTest()
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["Runtime:TelemetrySummary:VehicleId"] = "hydronom-test"
    });

    var controller = new RuntimeScenarioController(
        config,
        new FakeTaskManager(),
        new RuntimeWorldModel(),
        activeVehicleContext: null);

    var snapshot = controller.GetSnapshot("no-profile-test");

    Console.WriteLine(BuildSnapshotLine(snapshot));

    Assert(!snapshot.VehicleProfileActive, "No-profile snapshot active=true gelmemeliydi.");
    Assert(snapshot.VehicleProfileId is null, "No-profile profile id null olmalıydı.");
    Assert(snapshot.VehicleId == "hydronom-test", "No-profile runtime vehicle id yanlış.");
    Assert(!snapshot.VehicleHasThrusters, "No-profile thruster=true gelmemeliydi.");
    Assert(!snapshot.VehicleHasReverseAuthority, "No-profile reverse=true gelmemeliydi.");
    Assert(!snapshot.VehicleCanGenerateLateralForce, "No-profile lateral=true gelmemeliydi.");
    Assert(!snapshot.VehicleCanGenerateYawMoment, "No-profile yaw=true gelmemeliydi.");
}

static string BuildSnapshotLine(RuntimeScenarioSnapshot snapshot)
{
    return
        $"snapshot msg={snapshot.Message} " +
        $"vehicle={snapshot.VehicleId ?? "none"} " +
        $"profile={snapshot.VehicleProfileId ?? "none"} " +
        $"platform={snapshot.VehiclePlatformKind ?? "none"} " +
        $"active={snapshot.VehicleProfileActive} " +
        $"underwater={snapshot.VehicleIsUnderwater} " +
        $"mini={snapshot.VehicleIsMiniRov} " +
        $"thrusters={snapshot.VehicleHasThrusters} " +
        $"reverse={snapshot.VehicleHasReverseAuthority} " +
        $"lateral={snapshot.VehicleCanGenerateLateralForce} " +
        $"yaw={snapshot.VehicleCanGenerateYawMoment}";
}

static IConfigurationRoot BuildConfig(Dictionary<string, string?> values)
{
    return new ConfigurationBuilder()
        .AddInMemoryCollection(values)
        .Build();
}

static string? FindRepoRoot(string startDirectory)
{
    var dir = new DirectoryInfo(startDirectory);

    while (dir is not null)
    {
        var slnPath = Path.Combine(dir.FullName, "Hydronom.sln");

        if (File.Exists(slnPath))
            return dir.FullName;

        dir = dir.Parent;
    }

    return null;
}

static void Assert(bool condition, string message)
{
    if (condition)
        return;

    Fail(message);
    Environment.ExitCode = 1;
    throw new InvalidOperationException(message);
}

static void Fail(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();
}

sealed class FakeTaskManager : ITaskManager
{
    public TaskDefinition? CurrentTask { get; private set; }

    public TaskPhase Phase { get; private set; } = TaskPhase.None;

    public void SetTask(TaskDefinition task)
    {
        CurrentTask = task;
        Phase = TaskPhase.Active;
    }

    public void Update(Insights insights, VehicleState? state = null)
    {
        if (CurrentTask is null && Phase != TaskPhase.Aborted)
            Phase = TaskPhase.None;
    }

    public void ClearTask()
    {
        CurrentTask = null;

        if (Phase != TaskPhase.Aborted)
            Phase = TaskPhase.None;
    }
}