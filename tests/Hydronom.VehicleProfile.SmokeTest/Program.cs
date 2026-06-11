using Hydronom.Core.Vehicles;
using Hydronom.Core.Vehicles.Actuation;
using Hydronom.Runtime.Vehicles;
using Hydronom.Runtime.Vehicles.Loading;
using Hydronom.Runtime.Vehicles.Registry;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);

if (repoRoot is null)
{
    Fail("Repo root bulunamadı. Hydronom.sln aranırken üst klasörlere çıkıldı ama bulunamadı.");
    return;
}

var profilesRoot = Path.Combine(
    repoRoot,
    "src",
    "Hydronom.Runtime",
    "Vehicles",
    "Profiles");

Console.WriteLine("=== Hydronom Vehicle Profile Smoke Test ===");
Console.WriteLine($"Repo root     : {repoRoot}");
Console.WriteLine($"Profiles root : {profilesRoot}");
Console.WriteLine();

if (!Directory.Exists(profilesRoot))
{
    Fail($"Profiles root bulunamadı: {profilesRoot}");
    return;
}

var loader = new VehicleProfilePackageLoader();
var registry = new VehicleProfileRegistry();

var profiles = loader.LoadProfilesFromRoot(profilesRoot);

Console.WriteLine($"Loaded profile count: {profiles.Count}");

foreach (var profile in profiles)
{
    var validation = profile.Validate();

    Console.WriteLine();
    Console.WriteLine($"[{profile.ProfileId}]");
    Console.WriteLine($"  VehicleId     : {profile.VehicleId}");
    Console.WriteLine($"  DisplayName   : {profile.DisplayName}");
    Console.WriteLine($"  Platform      : {profile.PlatformKind}");
    Console.WriteLine($"  Underwater    : {profile.IsUnderwater}");
    Console.WriteLine($"  MiniROV       : {profile.IsMiniRov}");
    Console.WriteLine($"  Thrusters     : {profile.Actuation.ActiveThrusters.Count}");
    Console.WriteLine($"  HasCamera     : {profile.HasCamera}");
    Console.WriteLine($"  HasDepth      : {profile.HasDepthSensor}");
    Console.WriteLine($"  FleetRole     : {profile.FleetRole?.Role ?? "none"}");
    Console.WriteLine($"  IsFleetChild  : {profile.IsFleetChild}");
    Console.WriteLine($"  Valid         : {validation.IsValid}");

    foreach (var warning in validation.Warnings)
        Console.WriteLine($"  WARNING       : {warning}");

    foreach (var error in validation.Errors)
        Console.WriteLine($"  ERROR         : {error}");

    var capability = VehicleCapabilityProfileFactory.FromVehicleProfile(profile);

    Console.WriteLine($"  Capability    : {capability.Summary}");

    if (!registry.Register(profile))
        Fail($"Registry profile kaydedemedi: {profile.ProfileId}");
}

Console.WriteLine();
Console.WriteLine($"Registry count: {registry.Count}");

Assert(registry.Count >= 3, "En az 3 profile bekleniyordu: surface, main UUV, mini ROV.");

Assert(
    registry.TryGetByProfileId("hydronom_surface_mk1", out var surface) && surface is not null,
    "hydronom_surface_mk1 bulunamadı.");

Assert(
    registry.TryGetByProfileId("hydronom_uuv_main_2026", out var mainUuv) && mainUuv is not null,
    "hydronom_uuv_main_2026 bulunamadı.");

Assert(
    registry.TryGetByProfileId("hydronom_mini_rov_2026", out var miniRov) && miniRov is not null,
    "hydronom_mini_rov_2026 bulunamadı.");

Assert(surface!.IsSurface, "Surface profile IsSurface=false geldi.");
Assert(mainUuv!.IsUnderwater, "Main UUV IsUnderwater=false geldi.");
Assert(miniRov!.IsMiniRov, "Mini ROV IsMiniRov=false geldi.");

Assert(mainUuv.CanCarryChildVehicle, "Main UUV CanCarryChildVehicle=false geldi.");
Assert(miniRov.IsFleetChild, "Mini ROV IsFleetChild=false geldi.");

Assert(mainUuv.HasDepthSensor, "Main UUV depth sensor taşımıyor görünüyor.");
Assert(miniRov.HasCamera, "Mini ROV camera taşımıyor görünüyor.");

var active = new ActiveVehicleContext();
active.SetProfile(mainUuv);

Console.WriteLine();
Console.WriteLine(active.BuildSummary());

Assert(active.HasProfile, "ActiveVehicleContext profile alamadı.");
Assert(active.IsUnderwater, "ActiveVehicleContext underwater=false geldi.");
Assert(active.CapabilityProfile.HasAnyThruster, "ActiveVehicleContext capability thruster yok dedi.");
Assert(active.CapabilityProfile.CanGenerateYawMoment, "Main UUV yaw moment üretemiyor görünüyor.");

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Vehicle Profile Smoke Test başarılı.");
Console.ResetColor();

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