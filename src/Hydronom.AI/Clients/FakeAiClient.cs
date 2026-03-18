using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Orchestration;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Clients
{
    /// <summary>
    /// Gerçek LLaMA/LLM entegrasyonu gelene kadar sistemi uçtan uca derleyip çalıştırmak için
    /// deterministik MissionPlan üreten sahte istemci.
    /// </summary>
    public sealed class FakeAiClient : IAiClient
    {
        public Task<MissionPlan> GeneratePlanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var goal = TryExtractGoal(context) ?? "No goal provided";
            var expectedTools = SelectSuggestedTools(tools);

            var steps = new List<MissionStep>
            {
                new(
                    Index: 0,
                    Title: "Goal Assessment",
                    Description: $"FAKE client received goal: {goal}",
                    ExpectedTools: expectedTools
                ),
                new(
                    Index: 1,
                    Title: "Plan Stub",
                    Description: $"Generate a safe initial plan in {policy.Mode} mode.",
                    ExpectedTools: expectedTools
                )
            };

            return Task.FromResult(MissionPlan.Create(goal, steps));
        }

        public Task<MissionPlan> GenerateReplanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            IReadOnlyList<ToolResult> recentResults,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var goal = TryExtractGoal(context) ?? "No goal provided";
            var expectedTools = SelectSuggestedTools(tools);
            var okCount = recentResults?.Count(r => r.Ok) ?? 0;
            var failCount = recentResults?.Count(r => !r.Ok) ?? 0;

            var steps = new List<MissionStep>
            {
                new(
                    Index: 0,
                    Title: "Review Recent Results",
                    Description: $"FAKE client reviewed tool results. Success={okCount}, Fail={failCount}.",
                    ExpectedTools: expectedTools
                ),
                new(
                    Index: 1,
                    Title: "Replan Stub",
                    Description: $"Replan goal '{goal}' safely in {policy.Mode} mode.",
                    ExpectedTools: expectedTools
                )
            };

            return Task.FromResult(MissionPlan.Create(goal, steps));
        }

        private static IReadOnlyList<string> SelectSuggestedTools(IReadOnlyList<ToolSpec>? tools)
        {
            if (tools is null || tools.Count == 0)
                return Array.Empty<string>();

            return tools
                .Select(t => t.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();
        }

        private static string? TryExtractGoal(IReadOnlyList<AiMessage>? context)
        {
            if (context is null || context.Count == 0)
                return null;

            var lastUser = context.LastOrDefault(m => m.Role == AiRole.User);
            if (lastUser is null || string.IsNullOrWhiteSpace(lastUser.Content))
                return null;

            var content = lastUser.Content.Trim();
            var idx = content.IndexOf("Goal:", StringComparison.OrdinalIgnoreCase);

            if (idx >= 0)
            {
                var goal = content[(idx + "Goal:".Length)..].Trim();
                return string.IsNullOrWhiteSpace(goal) ? null : goal;
            }

            return content;
        }
    }
}