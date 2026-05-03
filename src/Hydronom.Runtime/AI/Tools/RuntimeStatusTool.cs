using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain.AI;
using Hydronom.Core.Interfaces.AI;

namespace Hydronom.Runtime.AI.Tools;

public sealed class RuntimeStatusTool : IAiTool
{
    private static readonly ToolSpec _spec = new(
        Name: "runtime.status",
        Description: "Runtime instance bilgilerini ve capability Ã¶zetini dÃ¶ndÃ¼rÃ¼r.",
        Args: Array.Empty<ToolArgSpec>(),
        Dangerous: false
    );

    public ToolSpec Spec => _spec;

    public Task<ToolResult> ExecuteAsync(ToolCall call, AiToolContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var capabilityKeys = ctx.Capabilities.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var telemetryKeys = ctx.TelemetrySnapshot.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["runtime_instance_id"] = ctx.RuntimeInstanceId,
            ["capability_keys"] = capabilityKeys,
            ["telemetry_keys"] = telemetryKeys,
            ["capability_count"] = capabilityKeys.Length,
            ["telemetry_count"] = telemetryKeys.Length,
            ["utc"] = DateTime.UtcNow.ToString("O")
        };

        return Task.FromResult(
            ToolResult.Success(
                toolCallId: call.Id,
                name: Spec.Name,
                output: "Runtime status alÄ±ndÄ±.",
                data: data
            )
        );
    }
}
