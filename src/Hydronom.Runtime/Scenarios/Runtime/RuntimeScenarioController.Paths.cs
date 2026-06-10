using System;
using System.Collections.Generic;
using System.IO;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed partial class RuntimeScenarioController
{
    private string ResolveScenarioPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
            return Path.GetFullPath(requestedPath.Trim());

        var configuredPath = _config["ScenarioRuntime:ScenarioPath"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath.Trim());

        var configuredScenarioId = _config["ScenarioRuntime:ScenarioId"];

        if (!string.IsNullOrWhiteSpace(configuredScenarioId))
            return ResolveSampleScenarioPath(configuredScenarioId.Trim());

        var parkur2Path = ResolveSampleScenarioPath("teknofest_2026_parkur_2_obstacle_point_tracking");
        if (ReadBool("ScenarioRuntime:UseParkur2", false) && ScenarioPathExists(parkur2Path))
            return parkur2Path;

        var parkur1Path = ResolveSampleScenarioPath("teknofest_2026_parkur_1_point_tracking");
        if (ScenarioPathExists(parkur1Path))
            return parkur1Path;

        return parkur1Path;
    }

    private static string ResolveSampleScenarioPath(string fileNameOrId)
    {
        var value = fileNameOrId.Trim();
        var sampleRoots = ResolveSampleScenarioRoots();

        if (IsExplicitJsonFileName(value))
        {
            foreach (var sampleRoot in sampleRoots)
            {
                var legacyFilePath = Path.GetFullPath(Path.Combine(sampleRoot, value));

                if (File.Exists(legacyFilePath))
                    return legacyFilePath;
            }

            return Path.GetFullPath(Path.Combine(sampleRoots[sampleRoots.Count - 1], value));
        }

        var scenarioId = StripJsonExtension(value);

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

        return Path.GetFullPath(Path.Combine(sampleRoots[sampleRoots.Count - 1], legacyFileName));
    }

    private static IReadOnlyList<string> ResolveSampleScenarioRoots()
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

    private static bool ScenarioPathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static bool IsExplicitJsonFileName(string value)
    {
        return value.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripJsonExtension(string value)
    {
        return IsExplicitJsonFileName(value)
            ? value[..^".json".Length]
            : value;
    }
}