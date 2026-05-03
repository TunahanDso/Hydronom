using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Orchestration
{
    /// <summary>
    /// HydronomAI planlama kÃ¶prÃ¼sÃ¼.
    /// Åimdilik tool execution yapmaz; yalnÄ±zca plan ve replan Ã¼retimini IAiClient Ã¼zerinden yÃ¼rÃ¼tÃ¼r.
    /// </summary>
    public sealed class AiOrchestrator
    {
        private readonly IAiClient _client;

        public AiOrchestrator(IAiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<MissionPlan> PlanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(tools);
            ArgumentNullException.ThrowIfNull(policy);
            ct.ThrowIfCancellationRequested();

            return _client.GeneratePlanAsync(context, tools, policy, ct);
        }

        public Task<MissionPlan> ReplanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            IReadOnlyList<ToolResult> recentResults,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(tools);
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(recentResults);
            ct.ThrowIfCancellationRequested();

            return _client.GenerateReplanAsync(context, tools, policy, recentResults, ct);
        }
    }

    public interface IAiClient
    {
        Task<MissionPlan> GeneratePlanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            CancellationToken ct);

        Task<MissionPlan> GenerateReplanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            IReadOnlyList<ToolResult> recentResults,
            CancellationToken ct);
    }
}
