using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Prompts.Mission;

public static class MissionPlanningPromptBuilder
{
    public static string BuildMissionPlanPrompt(
        IReadOnlyList<AiMessage> context,
        IReadOnlyList<ToolSpec> tools,
        SafetyPolicy policy,
        IReadOnlyList<ToolResult>? recentResults,
        string mode)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are HydronomAI, a mission planning assistant for a custom autonomous vehicle runtime.");
        sb.AppendLine("Return ONLY valid JSON. Do not use markdown. Do not add commentary.");
        sb.AppendLine("The returned JSON must be directly deserializable as MissionPlan.");
        sb.AppendLine();

        sb.AppendLine("Required JSON schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"id\": \"ai-plan-short-id\",");
        sb.AppendLine("  \"goal\": \"görevin açık ve kısa hedef cümlesi\",");
        sb.AppendLine("  \"steps\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"index\": 0,");
        sb.AppendLine("      \"title\": \"kısa görev adımı başlığı\",");
        sb.AppendLine("      \"description\": \"bu adımda aracın neyi, neden ve hangi güvenli sınırla yapacağını açıkla\",");
        sb.AppendLine("      \"expectedTools\": []");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"createdUtc\": \"2026-01-01T00:00:00Z\"");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Never produce direct motor, actuator, PWM, ESC, thrust, rudder, servo, or low-level control commands.");
        sb.AppendLine("- Never bypass safety, emergency stop, authority, geofence, mission approval, or human approval.");
        sb.AppendLine("- Produce a high-level mission plan, not low-level control.");
        sb.AppendLine("- If the user writes Turkish, all goal, title and description fields MUST be Turkish.");
        sb.AppendLine("- Do not write placeholder descriptions such as 'safe operational description'.");
        sb.AppendLine("- Do not copy the schema example text as the real answer.");
        sb.AppendLine("- Every description must explain the real operational intent of that step.");
        sb.AppendLine("- Keep steps practical, testable, observable, and safe.");
        sb.AppendLine("- Mention safety behavior in natural language when useful, but do not create fake low-level commands.");
        sb.AppendLine("- If tools are needed, reference their names in expectedTools.");
        sb.AppendLine("- If no tool is needed for a step, expectedTools must be an empty array.");
        sb.AppendLine("- Prefer 3 to 7 steps unless the mission clearly requires more.");
        sb.AppendLine("- The plan must require operator approval before execution.");
        sb.AppendLine();

        sb.AppendLine($"AI mode: {mode}");
        sb.AppendLine($"Safety mode: {policy.Mode}");
        sb.AppendLine($"Max tool calls per cycle: {policy.MaxToolCallsPerCycle}");
        sb.AppendLine();

        if (tools.Count > 0)
        {
            sb.AppendLine("Available tools:");
            foreach (var tool in tools.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"- {tool.Name}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Available tools: none");
            sb.AppendLine("Since no tools are available, expectedTools arrays should usually be empty.");
            sb.AppendLine();
        }

        if (recentResults is not null && recentResults.Count > 0)
        {
            sb.AppendLine("Recent tool results summary:");
            foreach (var result in recentResults.TakeLast(8))
                sb.AppendLine($"- ok={result.Ok}");
            sb.AppendLine();
        }

        sb.AppendLine("Conversation/context:");
        foreach (var message in context)
        {
            if (message is null || string.IsNullOrWhiteSpace(message.Content))
                continue;

            sb.AppendLine($"[{message.Role}] {message.Content.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("Output requirements:");
        sb.AppendLine("- Return JSON only.");
        sb.AppendLine("- Do not wrap JSON in markdown.");
        sb.AppendLine("- Do not add explanation outside JSON.");
        sb.AppendLine("- Do not use placeholder English descriptions.");
        sb.AppendLine("- If the mission story is Turkish, write Turkish JSON values.");
        sb.AppendLine();
        sb.AppendLine("Now return the MissionPlan JSON only.");

        return sb.ToString();
    }

    public static IReadOnlyList<AiMessage> BuildGroundMissionContext(
        string story,
        string? runtimeContext = null,
        string? vehicleContext = null)
    {
        if (string.IsNullOrWhiteSpace(story))
            throw new ArgumentException("Görev hikayesi boş olamaz.", nameof(story));

        var list = new List<AiMessage>
        {
            AiMessage.System(
                "Sen HydronomAI yer istasyonu görev planlama asistanısın. " +
                "Türkçe doğal görev anlatımını güvenli, yüksek seviyeli MissionPlan adımlarına çevirirsin. " +
                "Doğrudan motor, actuator, PWM, ESC veya düşük seviye kontrol komutu üretmezsin. " +
                "Runtime bağlamı ve araç bağlamı sadece kısıt bilgisidir; asıl görev hedefi en son verilen görev hikayesidir. " +
                "Cevapta görev adımlarını gerçek operasyon niyetiyle, Türkçe ve uygulanabilir şekilde yazarsın."
            )
        };

        if (!string.IsNullOrWhiteSpace(vehicleContext))
            list.Add(AiMessage.User("Araç bağlamı / kısıt: " + vehicleContext.Trim()));

        if (!string.IsNullOrWhiteSpace(runtimeContext))
            list.Add(AiMessage.User("Runtime/Görev durumu / kısıt: " + runtimeContext.Trim()));

        // Bilerek en sona ekliyoruz:
        // FakeAiClient son User mesajını Goal kabul ediyor.
        // LLM tarafında da son kullanıcı mesajının ana görev hedefi olması daha temiz.
        list.Add(AiMessage.User("Goal: " + story.Trim()));

        return list;
    }
}