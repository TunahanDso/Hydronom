using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain.AI;
using Hydronom.Core.Interfaces.AI;

namespace Hydronom.Runtime.AI.Tools;

public sealed class TelemetrySnapshotTool : IAiTool
{
    private static readonly ToolSpec _spec = new(
        Name: "telemetry.snapshot",
        Description: "Tool context iÃ§indeki telemetri snapshot'Ä±nÄ± dÃ¶ndÃ¼rÃ¼r.",
        Args: new[]
        {
            new ToolArgSpec(
                Name: "keys",
                Type: "array",
                Description: "Sadece istenen alanlarÄ± dÃ¶ndÃ¼rmek iÃ§in anahtar listesi. BoÅŸsa tamamÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.",
                Required: false,
                Example: new[] { "pos", "yaw_deg", "dist_to_target" }
            )
        },
        Dangerous: false
    );

    public ToolSpec Spec => _spec;

    public Task<ToolResult> ExecuteAsync(ToolCall call, AiToolContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var snapshot = ctx.TelemetrySnapshot;
        var requestedKeys = TryReadRequestedKeys(call);

        if (requestedKeys.Count > 0)
        {
            var filtered = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var key in requestedKeys)
            {
                if (snapshot.TryGetValue(key, out var value))
                    filtered[key] = value;
            }

            return Task.FromResult(
                ToolResult.Success(
                    toolCallId: call.Id,
                    name: Spec.Name,
                    output: $"FiltrelenmiÅŸ telemetri snapshot dÃ¶ndÃ¼rÃ¼ldÃ¼. Alan sayÄ±sÄ±: {filtered.Count}.",
                    data: filtered
                )
            );
        }

        var all = new Dictionary<string, object?>(snapshot, StringComparer.Ordinal);

        return Task.FromResult(
            ToolResult.Success(
                toolCallId: call.Id,
                name: Spec.Name,
                output: $"Tam telemetri snapshot dÃ¶ndÃ¼rÃ¼ldÃ¼. Alan sayÄ±sÄ±: {all.Count}.",
                data: all
            )
        );
    }

    private static IReadOnlyList<string> TryReadRequestedKeys(ToolCall call)
    {
        if (!call.Args.TryGetValue("keys", out var keysObj) || keysObj is null)
            return Array.Empty<string>();

        if (keysObj is IEnumerable<object?> objectEnumerable)
        {
            return objectEnumerable
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray();
        }

        if (keysObj is IEnumerable<string> stringEnumerable)
        {
            return stringEnumerable
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
