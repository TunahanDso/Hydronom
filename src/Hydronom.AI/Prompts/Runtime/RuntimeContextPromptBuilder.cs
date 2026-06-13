using System;
using System.Collections.Generic;
using System.Text;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Prompts.Runtime;

public static class RuntimeContextPromptBuilder
{
    public static AiMessage BuildSystemContext(
        string? runtimeSummary,
        string? vehicleSummary,
        string? sensorSummary,
        string? missionSummary)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Hydronom runtime context summary:");
        AppendSection(sb, "Runtime", runtimeSummary);
        AppendSection(sb, "Vehicle", vehicleSummary);
        AppendSection(sb, "Sensors", sensorSummary);
        AppendSection(sb, "Mission", missionSummary);

        return AiMessage.User(sb.ToString());
    }

    public static AiMessage BuildFromKeyValues(string title, IReadOnlyDictionary<string, object?> values)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);

        foreach (var pair in values)
            sb.AppendLine($"- {pair.Key}: {pair.Value}");

        return AiMessage.User(sb.ToString());
    }

    private static void AppendSection(StringBuilder sb, string title, string? value)
    {
        sb.AppendLine($"{title}:");
        sb.AppendLine(string.IsNullOrWhiteSpace(value) ? "- N/A" : value.Trim());
        sb.AppendLine();
    }
}
