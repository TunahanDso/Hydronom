using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Orchestration;
using Hydronom.Core.Domain.AI;

namespace Hydronom.Runtime.AI
{
    /// <summary>
    /// Runtime tarafÄ±nda Hydronom.AI orkestratÃ¶rÃ¼nÃ¼ Ã§alÄ±ÅŸtÄ±ran kÃ¶prÃ¼.
    /// Åimdilik yalnÄ±zca Suggest Mode Ã¼zerinden plan Ã¼retir.
    /// Tool execution yapmaz; sadece plan alÄ±r ve runtime'a geri verir.
    /// </summary>
    public sealed class AiGateway
    {
        private readonly AiOrchestrator _orchestrator;
        private readonly ToolRegistry _toolRegistry;

        public AiGateway(IAiClient client, ToolRegistry toolRegistry)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(toolRegistry);

            _orchestrator = new AiOrchestrator(client);
            _toolRegistry = toolRegistry;
        }

        public async Task<MissionPlan> SuggestPlanAsync(
            string goal,
            IReadOnlyList<AiMessage>? extraContext,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(goal))
                throw new ArgumentException("Goal boÅŸ olamaz.", nameof(goal));

            goal = goal.Trim();

            var policy = SafetyPolicy.DefaultSuggest();
            var tools = _toolRegistry.GetAllToolSpecs();
            var context = BuildSuggestContext(goal, extraContext);

            Console.WriteLine("[AI] SuggestPlanAsync baÅŸladÄ±.");
            Console.WriteLine($"[AI] Goal: {goal}");
            Console.WriteLine($"[AI] Tool count: {tools.Count}");
            Console.WriteLine($"[AI] Context count: {context.Count}");

            var plan = await _orchestrator
                .PlanAsync(context, tools, policy, ct)
                .ConfigureAwait(false);

            ValidatePlan(plan);

            Console.WriteLine($"[AI] Plan oluÅŸturuldu. Id={plan.Id}, Steps={plan.Steps.Count}");

            return plan;
        }

        private static IReadOnlyList<AiMessage> BuildSuggestContext(
            string goal,
            IReadOnlyList<AiMessage>? extraContext)
        {
            var context = new List<AiMessage>
            {
                AiMessage.System(
                    "You are HydronomAI. Return only a MissionPlan. " +
                    "Do not execute tools. " +
                    "If tools are needed, reference them clearly inside the plan steps."
                ),
                AiMessage.User($"Goal: {goal}")
            };

            if (extraContext is not null && extraContext.Count > 0)
            {
                foreach (var message in extraContext)
                {
                    if (message is not null)
                        context.Add(message);
                }
            }

            return context;
        }

        private static void ValidatePlan(MissionPlan? plan)
        {
            if (plan is null)
                throw new InvalidOperationException("AI null MissionPlan dÃ¶ndÃ¼rdÃ¼.");

            if (string.IsNullOrWhiteSpace(plan.Id))
                throw new InvalidOperationException("MissionPlan.Id boÅŸ olamaz.");

            if (string.IsNullOrWhiteSpace(plan.Goal))
                throw new InvalidOperationException("MissionPlan.Goal boÅŸ olamaz.");

            if (plan.Steps is null)
                throw new InvalidOperationException("MissionPlan.Steps null olamaz.");

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];

                if (string.IsNullOrWhiteSpace(step.Title))
                    throw new InvalidOperationException($"MissionStep[{i}].Title boÅŸ olamaz.");

                if (string.IsNullOrWhiteSpace(step.Description))
                    throw new InvalidOperationException($"MissionStep[{i}].Description boÅŸ olamaz.");

                if (step.ExpectedTools is null)
                    throw new InvalidOperationException($"MissionStep[{i}].ExpectedTools null olamaz.");
            }
        }
    }
}
