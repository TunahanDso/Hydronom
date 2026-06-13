using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Orchestration;
using Hydronom.AI.Prompts.Mission;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Clients.Local;

public sealed class LocalLlamaClient : IAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly HttpClient _http;
    private readonly string _endpointUrl;
    private readonly string _model;

    public LocalLlamaClient(HttpClient http, string endpointUrl, string model)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));

        _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl)
            ? "http://localhost:11434/api/generate"
            : endpointUrl.Trim();

        _model = string.IsNullOrWhiteSpace(model)
            ? "llama3.2:3b"
            : model.Trim();
    }

    public Task<MissionPlan> GeneratePlanAsync(
        IReadOnlyList<AiMessage> context,
        IReadOnlyList<ToolSpec> tools,
        SafetyPolicy policy,
        CancellationToken ct)
    {
        var prompt = MissionPlanningPromptBuilder.BuildMissionPlanPrompt(
            context ?? Array.Empty<AiMessage>(),
            tools ?? Array.Empty<ToolSpec>(),
            policy ?? SafetyPolicy.DefaultSuggest(),
            recentResults: Array.Empty<ToolResult>(),
            mode: "plan");

        return GenerateFromPromptAsync(prompt, ct);
    }

    public Task<MissionPlan> GenerateReplanAsync(
        IReadOnlyList<AiMessage> context,
        IReadOnlyList<ToolSpec> tools,
        SafetyPolicy policy,
        IReadOnlyList<ToolResult> recentResults,
        CancellationToken ct)
    {
        var prompt = MissionPlanningPromptBuilder.BuildMissionPlanPrompt(
            context ?? Array.Empty<AiMessage>(),
            tools ?? Array.Empty<ToolSpec>(),
            policy ?? SafetyPolicy.DefaultSuggest(),
            recentResults ?? Array.Empty<ToolResult>(),
            mode: "replan");

        return GenerateFromPromptAsync(prompt, ct);
    }

    private async Task<MissionPlan> GenerateFromPromptAsync(string prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var first = await GenerateRawAsync(
            prompt,
            numPredict: 520,
            numCtx: 3072,
            ct).ConfigureAwait(false);

        if (TryParseMissionPlan(first.Body, out var plan, out var parseError))
            return plan;

        // 3B modeller uzun sistem promptlarında bazen JSON'u uzatıp length'e düşürüyor.
        // Bu durumda görevi çok daha kısa bir prompt ile ikinci kez istiyoruz.
        if (first.DoneReasonEquals("length") || parseError.Contains("Tamamlanmış JSON object", StringComparison.OrdinalIgnoreCase))
        {
            var compactPrompt = BuildCompactRetryPrompt(prompt);

            var second = await GenerateRawAsync(
                compactPrompt,
                numPredict: 520,
                numCtx: 2048,
                ct).ConfigureAwait(false);

            if (TryParseMissionPlan(second.Body, out plan, out parseError))
                return plan;

            if (second.DoneReasonEquals("length"))
                throw new InvalidOperationException(
                    $"Local LLaMA/Ollama cevabı ikinci denemede de uzunluk sınırında kesildi. Body: {Trim(second.Body, 2200)}");
        }

        throw new InvalidOperationException(
            $"MissionPlan JSON parse edilemedi. ParseError: {parseError}. Body: {Trim(first.Body, 2500)}");
    }

    private async Task<OllamaRawResponse> GenerateRawAsync(
        string prompt,
        int numPredict,
        int numCtx,
        CancellationToken ct)
    {
        var req = new OllamaGenerateRequest
        {
            Model = _model,
            Prompt = prompt,
            Stream = false,
            Format = "json",
            Options = new Dictionary<string, object?>
            {
                ["temperature"] = 0.05,
                ["top_p"] = 0.80,
                ["num_predict"] = numPredict,
                ["num_ctx"] = numCtx,
                ["repeat_penalty"] = 1.25
            }
        };

        var json = JsonSerializer.Serialize(req, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _http.PostAsync(_endpointUrl, content, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Local LLaMA/Ollama endpoint'e bağlanılamadı: {_endpointUrl}. Detay: {ex.Message}",
                ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Local LLaMA/Ollama endpoint hata döndürdü. HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Trim(body, 1500)}");
        }

        var doneReason = TryReadDoneReason(body);
        return new OllamaRawResponse(body, doneReason);
    }

    private static bool TryParseMissionPlan(string body, out MissionPlan plan, out string error)
    {
        plan = null!;
        error = string.Empty;

        try
        {
            plan = ParseMissionPlanFromOllamaBody(body);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static MissionPlan ParseMissionPlanFromOllamaBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Local LLaMA/Ollama boş response döndürdü.");

        var responseText = ExtractResponseText(body);
        var jsonObject = ExtractFirstJsonObject(responseText);

        using var doc = JsonDocument.Parse(jsonObject);
        var root = UnwrapPlanObject(doc.RootElement);

        return BuildMissionPlan(root);
    }

    private static string ExtractResponseText(string body)
    {
        var trimmed = body.Trim();

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("response", out var responseElement) &&
                responseElement.ValueKind == JsonValueKind.String)
            {
                var response = responseElement.GetString();

                if (!string.IsNullOrWhiteSpace(response))
                    return response.Trim();
            }

            return root.GetRawText();
        }
        catch (JsonException)
        {
            return trimmed;
        }
    }

    private static JsonElement UnwrapPlanObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("MissionPlan root JSON object değil.");

        if (root.TryGetProperty("plan", out var planElement) &&
            planElement.ValueKind == JsonValueKind.Object)
            return planElement;

        if (root.TryGetProperty("missionPlan", out var missionPlanElement) &&
            missionPlanElement.ValueKind == JsonValueKind.Object)
            return missionPlanElement;

        return root;
    }

    private static MissionPlan BuildMissionPlan(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("MissionPlan object bekleniyordu.");

        var id = GetString(root, "id");

        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");

        var goal =
            GetString(root, "goal") ??
            GetString(root, "missionGoal") ??
            GetString(root, "objective") ??
            "AI generated mission plan";

        goal = SanitizeText(goal, "AI generated mission plan", maxLength: 500);

        var steps = new List<MissionStep>();

        if (root.TryGetProperty("steps", out var stepsElement) &&
            stepsElement.ValueKind == JsonValueKind.Array)
        {
            var i = 0;

            foreach (var item in stepsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var index = GetInt(item, "index") ?? i;

                var rawTitle =
                    GetString(item, "title") ??
                    GetString(item, "name") ??
                    $"Adım {index + 1}";

                var rawDescription =
                    GetString(item, "description") ??
                    GetString(item, "details") ??
                    rawTitle;

                var title = SanitizeText(rawTitle, $"Adım {index + 1}", maxLength: 120);
                var description = SanitizeText(rawDescription, title, maxLength: 280);
                var expectedTools = GetStringArray(item, "expectedTools");

                steps.Add(new MissionStep(
                    Index: index,
                    Title: title,
                    Description: description,
                    ExpectedTools: expectedTools
                ));

                i++;

                if (steps.Count >= 8)
                    break;
            }
        }

        if (steps.Count == 0)
        {
            steps.Add(new MissionStep(
                Index: 0,
                Title: "Görev değerlendirmesi",
                Description: goal,
                ExpectedTools: Array.Empty<string>()
            ));
        }

        return new MissionPlan(
            Id: id,
            Goal: goal,
            Steps: steps,
            CreatedUtc: DateTime.UtcNow
        );
    }

    private static string ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("JSON ayıklanacak metin boş.");

        var cleaned = StripMarkdownFence(text.Trim());

        var start = cleaned.IndexOf('{');
        if (start < 0)
            throw new InvalidOperationException("Metin içinde JSON object başlangıcı bulunamadı.");

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < cleaned.Length; i++)
        {
            var ch = cleaned[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (ch == '{')
                depth++;

            if (ch == '}')
            {
                depth--;

                if (depth == 0)
                    return cleaned[start..(i + 1)];
            }
        }

        throw new InvalidOperationException("Tamamlanmış JSON object bulunamadı.");
    }

    private static string StripMarkdownFence(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        if (firstNewLine < 0 || lastFence <= firstNewLine)
            return trimmed;

        return trimmed[(firstNewLine + 1)..lastFence].Trim();
    }

    private static string BuildCompactRetryPrompt(string originalPrompt)
    {
        var goal = ExtractGoalForRetry(originalPrompt);

        return
            "Sadece geçerli JSON döndür. Markdown yok. Açıklama yok.\n" +
            "JSON şeması tam olarak şu alanları içersin: id, goal, steps, createdUtc.\n" +
            "steps içinde TAM OLARAK 3 kısa Türkçe adım olsun. Her adım: index, title, description, expectedTools.\n" +
            "Her description en fazla 18 kelime olsun.\n" +
            "expectedTools boş array olsun. Doğrudan motor/actuator komutu yazma.\n" +
            "Açıklamalar gerçek görev niyetini anlatsın, placeholder olmasın.\n" +
            $"Görev: {goal}\n";
    }

    private static string ExtractGoalForRetry(string prompt)
    {
        const string marker = "Goal:";
        var idx = prompt.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (idx < 0)
            return "Tekne güvenli şekilde görevi tamamlasın.";

        var goal = prompt[(idx + marker.Length)..].Trim();

        var newline = goal.IndexOf('\n');
        if (newline >= 0)
            goal = goal[..newline].Trim();

        if (goal.Length > 500)
            goal = goal[..500];

        return string.IsNullOrWhiteSpace(goal)
            ? "Tekne güvenli şekilde görevi tamamlasın."
            : goal;
    }

    private static string? TryReadDoneReason(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("done_reason", out var doneReasonElement) &&
                doneReasonElement.ValueKind == JsonValueKind.String)
            {
                return doneReasonElement.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }

    private static string SanitizeText(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var text = value.Trim();

        if (IsDegenerateText(text))
            return fallback;

        if (text.Length > maxLength)
            text = text[..maxLength].Trim();

        return text;
    }

    private static bool IsDegenerateText(string text)
    {
        if (text.Contains("safe operational description", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.Contains("ççççç", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.Length < 16)
            return false;

        var repeatedCount = 0;
        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] == text[i - 1])
                repeatedCount++;
        }

        return repeatedCount > text.Length / 3;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
            return n;

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return Array.Empty<string>();

        if (value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var s = item.GetString();

            if (!string.IsNullOrWhiteSpace(s))
                list.Add(s.Trim());
        }

        return list;
    }

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        return s.Length <= max ? s : s[..max] + "...";
    }

    private sealed record OllamaRawResponse(string Body, string? DoneReason)
    {
        public bool DoneReasonEquals(string value)
            => string.Equals(DoneReason, value, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OllamaGenerateRequest
    {
        public string Model { get; set; } = "";
        public string Prompt { get; set; } = "";
        public bool Stream { get; set; }
        public string Format { get; set; } = "json";
        public Dictionary<string, object?> Options { get; set; } = new();
    }
}