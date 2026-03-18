using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain.AI;

namespace Hydronom.Core.Interfaces.AI;

public interface IAiTool
{
    ToolSpec Spec { get; }

    Task<ToolResult> ExecuteAsync(
        ToolCall call,
        AiToolContext ctx,
        CancellationToken ct
    );
}