using Hydronom.Runtime.Vehicles;
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

Console.WriteLine("=== Hydronom Runtime Vehicle Profile Binding Smoke Test ===");
Console.WriteLine($"Repo root     : {repoRoot}");
Console.WriteLine($"Profiles root : {profilesRoot}");
Console.WriteLine();

Assert(Directory.Exists(profilesRoot), $"Profiles root bulunamadı: {profilesRoot}");

RunMainUuvBindingTest(profilesRoot);
RunMiniRovBindingTest(profilesRoot);
RunSurfaceFallbackTest(profilesRoot);
RunDisabledProfileTest(profilesRoot);

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("RUNTIME_VEHICLE_PROFILE_BINDING_SMOKE_TEST_OK");
Console.ResetColor();

static void RunMainUuvBindingTest(string profilesRoot)
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

    Console.WriteLine(binding.BuildSummary());

    Assert(binding.Enabled, "Main UUV binding disabled geldi.");
    Assert(binding.HasActiveProfile, "Main UUV active profile yok.");
    Assert(binding.ActiveContext.ProfileId == "hydronom_uuv_main_2026", "Main UUV profile id yanlış.");
    Assert(binding.ActiveContext.VehicleId == "hydronom-uuv-main", "Main UUV vehicle id yanlış.");
    Assert(binding.ActiveContext.IsUnderwater, "Main UUV underwater=false geldi.");
    Assert(binding.ActiveContext.CapabilityProfile.HasAnyThruster, "Main UUV thruster yok görünüyor.");
    Assert(binding.ActiveContext.CapabilityProfile.CanGenerateLateralForce, "Main UUV lateral authority yok görünüyor.");
    Assert(binding.ActiveContext.CapabilityProfile.CanGenerateYawMoment, "Main UUV yaw authority yok görünüyor.");
}

static void RunMiniRovBindingTest(string profilesRoot)
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["VehicleProfile:Enabled"] = "true",
        ["VehicleProfile:ProfilesRoot"] = profilesRoot,
        ["VehicleProfile:ProfileId"] = "hydronom_mini_rov_2026"
    });

    var binding = VehicleProfileRuntimeBootstrapper.Bootstrap(config, AppContext.BaseDirectory);

    Console.WriteLine(binding.BuildSummary());

    Assert(binding.HasActiveProfile, "Mini ROV active profile yok.");
    Assert(binding.ActiveContext.ProfileId == "hydronom_mini_rov_2026", "Mini ROV profile id yanlış.");
    Assert(binding.ActiveContext.VehicleId == "hydronom-mini-rov", "Mini ROV vehicle id yanlış.");
    Assert(binding.ActiveContext.IsUnderwater, "Mini ROV underwater=false geldi.");
    Assert(binding.ActiveContext.IsMiniRov, "Mini ROV IsMiniRov=false geldi.");
    Assert(binding.ActiveContext.CapabilityProfile.HasReverseAuthority, "Mini ROV reverse authority yok görünüyor.");
    Assert(!binding.ActiveContext.CapabilityProfile.CanGenerateLateralForce, "Mini ROV lateral authority beklenmiyordu.");
}

static void RunSurfaceFallbackTest(string profilesRoot)
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["VehicleProfile:Enabled"] = "true",
        ["VehicleProfile:ProfilesRoot"] = profilesRoot
    });

    var binding = VehicleProfileRuntimeBootstrapper.Bootstrap(config, AppContext.BaseDirectory);

    Console.WriteLine(binding.BuildSummary());

    Assert(binding.HasActiveProfile, "Surface fallback active profile yok.");
    Assert(binding.ActiveContext.ProfileId == "hydronom_surface_mk1", "Surface fallback profile id yanlış.");
    Assert(binding.ActiveContext.VehicleId == "hydronom-surface-mk1", "Surface fallback vehicle id yanlış.");
    Assert(!binding.ActiveContext.IsUnderwater, "Surface fallback underwater=true geldi.");
    Assert(!binding.ActiveContext.CapabilityProfile.HasReverseAuthority, "Surface fallback reverse authority beklenmiyordu.");
    Assert(!binding.ActiveContext.CapabilityProfile.CanGenerateLateralForce, "Surface fallback lateral authority beklenmiyordu.");
    Assert(binding.ActiveContext.CapabilityProfile.CanGenerateYawMoment, "Surface fallback yaw authority yok görünüyor.");
}

static void RunDisabledProfileTest(string profilesRoot)
{
    var config = BuildConfig(new Dictionary<string, string?>
    {
        ["VehicleProfile:Enabled"] = "false",
        ["VehicleProfile:ProfilesRoot"] = profilesRoot,
        ["VehicleProfile:ProfileId"] = "hydronom_uuv_main_2026"
    });

    var binding = VehicleProfileRuntimeBootstrapper.Bootstrap(config, AppContext.BaseDirectory);

    Console.WriteLine(binding.BuildSummary());

    Assert(!binding.Enabled, "Disabled profile binding enabled geldi.");
    Assert(!binding.HasActiveProfile, "Disabled profile active profile üretmemeliydi.");
    Assert(binding.LoadedProfiles.Count == 0, "Disabled profile loaded profiles boş olmalıydı.");
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