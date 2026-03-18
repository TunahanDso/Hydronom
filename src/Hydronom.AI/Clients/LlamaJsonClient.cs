using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Orchestration;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Clients
{
    /// <summary>
    /// LLaMA JSON client.
    /// Beklenen akış:
    /// context + tools + policy (+ recentResults) -> MissionPlan JSON
    ///
    /// Desteklenen response biçimleri:
    /// 1) Doğrudan MissionPlan JSON
    /// 2) { "plan": { ... } }
    /// 3) { "response": "{...json...}" }
    /// 4) Markdown code fence içindeki JSON
    /// </summary>
    public sealed class LlamaJsonClient : IAiClient
    {
        private static readonly JsonSerializerOptions RequestJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions ResponseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _http;
        private readonly string _endpointUrl;

        public LlamaJsonClient(HttpClient http, string endpointUrl)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl)
                ? throw new ArgumentException("endpointUrl boş olamaz.", nameof(endpointUrl))
                : endpointUrl.Trim();
        }

        public Task<MissionPlan> GeneratePlanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ct.ThrowIfCancellationRequested();

            var req = new LlamaPlanRequest
            {
                Mode = "plan",
                Context = context ?? Array.Empty<AiMessage>(),
                Tools = tools ?? Array.Empty<ToolSpec>(),
                Policy = policy,
                RecentResults = Array.Empty<ToolResult>()
            };

            return PostForPlanAsync(req, ct);
        }

        public Task<MissionPlan> GenerateReplanAsync(
            IReadOnlyList<AiMessage> context,
            IReadOnlyList<ToolSpec> tools,
            SafetyPolicy policy,
            IReadOnlyList<ToolResult> recentResults,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ct.ThrowIfCancellationRequested();

            var req = new LlamaPlanRequest
            {
                Mode = "replan",
                Context = context ?? Array.Empty<AiMessage>(),
                Tools = tools ?? Array.Empty<ToolSpec>(),
                Policy = policy,
                RecentResults = recentResults ?? Array.Empty<ToolResult>()
            };

            return PostForPlanAsync(req, ct);
        }

        private async Task<MissionPlan> PostForPlanAsync(LlamaPlanRequest req, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(req, RequestJsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(_endpointUrl, content, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"LLaMA endpoint'e bağlanılamadı: {_endpointUrl}. Detay: {ex.Message}",
                    ex);
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"LLaMA endpoint hata döndürdü. HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Trim(body, 1000)}");
            }

            var extractedJson = ExtractPlanJson(body);

            try
            {
                var plan = JsonSerializer.Deserialize<MissionPlan>(extractedJson, ResponseJsonOptions);

                if (plan is null)
                    throw new InvalidOperationException("MissionPlan deserialize sonucu null.");

                ValidatePlan(plan);
                return plan;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"MissionPlan JSON parse edilemedi. Body: {Trim(body, 1000)}",
                    ex);
            }
        }

        private static string ExtractPlanJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                throw new InvalidOperationException("LLaMA endpoint boş response döndürdü.");

            var trimmed = body.Trim();

            var fenced = TryExtractJsonFromCodeFence(trimmed);
            if (!string.IsNullOrWhiteSpace(fenced))
                trimmed = fenced.Trim();

            if (LooksLikeJsonObject(trimmed))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    if (LooksLikeMissionPlan(root))
                        return root.GetRawText();

                    if (root.ValueKind == JsonValueKind.Object &&
                        root.TryGetProperty("plan", out var planElement) &&
                        planElement.ValueKind == JsonValueKind.Object)
                    {
                        return planElement.GetRawText();
                    }

                    if (root.ValueKind == JsonValueKind.Object &&
                        root.TryGetProperty("response", out var responseElement) &&
                        responseElement.ValueKind == JsonValueKind.String)
                    {
                        var inner = responseElement.GetString();
                        if (string.IsNullOrWhiteSpace(inner))
                            throw new InvalidOperationException("LLaMA response alanı boş.");

                        var innerTrimmed = inner.Trim();

                        var innerFenced = TryExtractJsonFromCodeFence(innerTrimmed);
                        if (!string.IsNullOrWhiteSpace(innerFenced))
                            innerTrimmed = innerFenced.Trim();

                        if (LooksLikeJsonObject(innerTrimmed))
                            return innerTrimmed;
                    }
                }
                catch (JsonException)
                {
                    // Ham içerik son şans olarak aşağıda yeniden denenir.
                }
            }

            if (LooksLikeJsonObject(trimmed))
                return trimmed;

            throw new InvalidOperationException("Response içinden MissionPlan JSON ayıklanamadı.");
        }

        private static bool LooksLikeMissionPlan(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            return element.TryGetProperty("id", out _) &&
                   element.TryGetProperty("goal", out _) &&
                   element.TryGetProperty("steps", out _);
        }

        private static void ValidatePlan(MissionPlan plan)
        {
            if (string.IsNullOrWhiteSpace(plan.Id))
                throw new InvalidOperationException("MissionPlan.Id boş olamaz.");

            if (string.IsNullOrWhiteSpace(plan.Goal))
                throw new InvalidOperationException("MissionPlan.Goal boş olamaz.");

            if (plan.Steps is null)
                throw new InvalidOperationException("MissionPlan.Steps null olamaz.");

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];

                if (string.IsNullOrWhiteSpace(step.Title))
                    throw new InvalidOperationException($"MissionStep[{i}].Title boş olamaz.");

                if (string.IsNullOrWhiteSpace(step.Description))
                    throw new InvalidOperationException($"MissionStep[{i}].Description boş olamaz.");

                if (step.ExpectedTools is null)
                    throw new InvalidOperationException($"MissionStep[{i}].ExpectedTools null olamaz.");
            }
        }

        private static bool LooksLikeJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            return text.StartsWith("{", StringComparison.Ordinal) &&
                   text.EndsWith("}", StringComparison.Ordinal);
        }

        private static string? TryExtractJsonFromCodeFence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var start = text.IndexOf("```", StringComparison.Ordinal);
            if (start < 0)
                return null;

            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (end <= start)
                return null;

            var inner = text[(start + 3)..end].Trim();

            if (inner.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                inner = inner[4..].Trim();

            return inner;
        }

        private static string Trim(string? s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            if (s.Length <= max)
                return s;

            return s[..max] + "...";
        }

        private sealed class LlamaPlanRequest
        {
            public string Mode { get; set; } = "plan";
            public IReadOnlyList<AiMessage> Context { get; set; } = Array.Empty<AiMessage>();
            public IReadOnlyList<ToolSpec> Tools { get; set; } = Array.Empty<ToolSpec>();
            public SafetyPolicy? Policy { get; set; }
            public IReadOnlyList<ToolResult> RecentResults { get; set; } = Array.Empty<ToolResult>();
        }
    }
}