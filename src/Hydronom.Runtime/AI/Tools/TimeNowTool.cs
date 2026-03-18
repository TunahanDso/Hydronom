using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain.AI;
using Hydronom.Core.Interfaces.AI;

namespace Hydronom.Runtime.AI.Tools;

public sealed class TimeNowTool : IAiTool
{
    private static readonly ToolSpec _spec = new(
        Name: "time.now",
        Description: "Runtime'ın UTC saatini döndürür.",
        Args: Array.Empty<ToolArgSpec>(),
        Dangerous: false
    );

    public ToolSpec Spec => _spec;

    public Task<ToolResult> ExecuteAsync(ToolCall call, AiToolContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["utc"] = now.UtcDateTime.ToString("O"),
            ["unix_ms"] = now.ToUnixTimeMilliseconds(),
            ["unix_s"] = now.ToUnixTimeSeconds()
        };

        return Task.FromResult(
            ToolResult.Success(
                toolCallId: call.Id,
                name: Spec.Name,
                output: now.UtcDateTime.ToString("O"),
                data: data
            )
        );
    }
}