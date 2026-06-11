using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Scenarios.Models;
using Hydronom.Runtime.Scenarios;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// VP9A-4A:
    /// Aktif scenario/package metadata içindeki world.* tag'lerini okuyarak
    /// WorldOptions değerlerini scenario-aware hale getirir.
    ///
    /// Desteklenen tag örnekleri:
    /// - world.id
    /// - world.name
    /// - world.surfaceZ / world.surface_z / surfaceZ
    /// - world.floorZ   / world.floor_z   / floorZ
    /// - world.gravityMps2 / world.gravity_mps2 / gravityMps2
    /// </summary>
    private static async Task<WorldOptions> ReadScenarioAwareWorldOptionsAsync(
        IConfiguration config,
        WorldOptions fallback,
        CancellationToken cancellationToken = default)
    {
        if (!ReadBool(config, "ScenarioRuntime:Enabled", false))
            return fallback;

        var path = ResolveScenarioWorldOptionsPath(config);

        if (string.IsNullOrWhiteSpace(path) || !ScenarioWorldPathExists(path))
        {
            Console.WriteLine(
                $"[SCN-WORLD] Scenario path bulunamadı; config World fallback kullanılacak. path={path}");

            return fallback;
        }

        try
        {
            var loader = new ScenarioLoader();
            var scenario = await loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);

            var effective = ApplyScenarioWorldTags(
                fallback,
                scenario,
                path);

            return effective;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[SCN-WORLD] Scenario world metadata okunamadı; config World fallback kullanılacak. " +
                $"path={path} error={ex.Message}");

            return fallback;
        }
    }

    private static WorldOptions ApplyScenarioWorldTags(
        WorldOptions fallback,
        ScenarioDefinition scenario,
        string sourcePath)
    {
        var tags = scenario.Tags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var id = ReadTagString(
            tags,
            fallback.Id,
            "world.id",
            "worldId",
            "world_id");

        var name = ReadTagString(
            tags,
            fallback.Name,
            "world.name",
            "worldName",
            "world_name");

        var surfaceZ = ReadTagDouble(
            tags,
            fallback.SurfaceZ,
            "world.surfaceZ",
            "world.surface_z",
            "surfaceZ",
            "surface_z",
            "environment.surfaceZ",
            "environment.surface_z");

        var floorZ = ReadTagDouble(
            tags,
            fallback.FloorZ,
            "world.floorZ",
            "world.floor_z",
            "floorZ",
            "floor_z",
            "environment.floorZ",
            "environment.floor_z");

        var gravity = ReadTagDouble(
            tags,
            fallback.GravityMps2,
            "world.gravityMps2",
            "world.gravity_mps2",
            "gravityMps2",
            "gravity_mps2",
            "environment.gravityMps2",
            "environment.gravity_mps2");

        if (!double.IsFinite(surfaceZ))
            surfaceZ = fallback.SurfaceZ;

        if (!double.IsFinite(floorZ))
            floorZ = fallback.FloorZ;

        if (floorZ > surfaceZ)
        {
            Console.WriteLine(
                $"[SCN-WORLD] Invalid scenario floor/surface ignored. " +
                $"scenario={scenario.Id} FloorZ={floorZ:F2} SurfaceZ={surfaceZ:F2}");

            floorZ = fallback.FloorZ;
            surfaceZ = fallback.SurfaceZ;
        }

        if (!double.IsFinite(gravity) || gravity <= 0.0)
            gravity = fallback.GravityMps2;

        var effective = fallback with
        {
            Id = string.IsNullOrWhiteSpace(id) ? fallback.Id : id.Trim(),
            Name = string.IsNullOrWhiteSpace(name) ? fallback.Name : name.Trim(),
            SurfaceZ = surfaceZ,
            FloorZ = floorZ,
            GravityMps2 = gravity
        };

        Console.WriteLine(
            $"[SCN-WORLD] scenario={scenario.Id} source={ShortenPathForLog(sourcePath)} " +
            $"world={effective.Id} surfaceZ={effective.SurfaceZ:F2} " +
            $"floorZ={effective.FloorZ:F2} gravity={effective.GravityMps2:F5}");

        return effective;
    }

    private static string ReadTagString(
        IReadOnlyDictionary<string, string> tags,
        string fallback,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (tags.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return fallback;
    }

    private static double ReadTagDouble(
        IReadOnlyDictionary<string, string> tags,
        double fallback,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!tags.TryGetValue(key, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (double.TryParse(
                    value.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static string ResolveScenarioWorldOptionsPath(IConfiguration config)
    {
        var configuredPath = config["ScenarioRuntime:ScenarioPath"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath.Trim());

        var configuredScenarioId = config["ScenarioRuntime:ScenarioId"];

        if (!string.IsNullOrWhiteSpace(configuredScenarioId))
            return ResolveSampleScenarioWorldOptionsPath(configuredScenarioId.Trim());

        var parkur2Path = ResolveSampleScenarioWorldOptionsPath(
            "teknofest_2026_parkur_2_obstacle_point_tracking");

        if (ReadBool(config, "ScenarioRuntime:UseParkur2", false) &&
            ScenarioWorldPathExists(parkur2Path))
        {
            return parkur2Path;
        }

        return ResolveSampleScenarioWorldOptionsPath(
            "teknofest_2026_parkur_1_point_tracking");
    }

    private static string ResolveSampleScenarioWorldOptionsPath(string fileNameOrId)
    {
        var value = fileNameOrId.Trim();
        var sampleRoots = ResolveSampleScenarioWorldOptionsRoots();

        if (value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var sampleRoot in sampleRoots)
            {
                var legacyFilePath = Path.GetFullPath(Path.Combine(sampleRoot, value));

                if (File.Exists(legacyFilePath))
                    return legacyFilePath;
            }

            return Path.GetFullPath(Path.Combine(sampleRoots[^1], value));
        }

        var scenarioId = value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? value[..^".json".Length]
            : value;

        foreach (var sampleRoot in sampleRoots)
        {
            var packagePath = Path.GetFullPath(Path.Combine(sampleRoot, scenarioId));
            var packageScenarioFile = Path.Combine(packagePath, "scenario.json");

            if (Directory.Exists(packagePath) && File.Exists(packageScenarioFile))
                return packagePath;
        }

        var legacyFileName = $"{scenarioId}.json";

        foreach (var sampleRoot in sampleRoots)
        {
            var legacyFilePath = Path.GetFullPath(Path.Combine(sampleRoot, legacyFileName));

            if (File.Exists(legacyFilePath))
                return legacyFilePath;
        }

        return Path.GetFullPath(Path.Combine(sampleRoots[^1], legacyFileName));
    }

    private static IReadOnlyList<string> ResolveSampleScenarioWorldOptionsRoots()
    {
        return new[]
        {
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "Samples")),

            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Hydronom.Runtime",
                "Scenarios",
                "Samples"))
        };
    }

    private static bool ScenarioWorldPathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static string ShortenPathForLog(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "-";

        var normalized = path.Trim();

        if (normalized.Length <= 96)
            return normalized;

        return "..." + normalized[^93..];
    }
}