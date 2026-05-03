using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hydronom.Core.Domain.AI;

/// <summary>
/// LLM mesajÄ±nda "kim konuÅŸuyor?" bilgisini taÅŸÄ±r.
/// System: sistem yÃ¶nergesi
/// User: kullanÄ±cÄ± mesajÄ±
/// Assistant: model cevabÄ±
/// Tool: tool Ã§Ä±ktÄ±sÄ± (ToolResult gibi)
/// </summary>
public enum AiRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3
}

/// <summary>
/// HydronomAI mesaj modeli.
/// - Role: mesajÄ±n kaynaÄŸÄ±
/// - Content: metin/iÃ§erik
/// - TimestampUtc: UTC zaman damgasÄ±
/// - Name: opsiyonel konuÅŸmacÄ± adÄ± (Ã¶r. tool adÄ±, kullanÄ±cÄ± adÄ±)
/// - Meta: opsiyonel kÃ¼Ã§Ã¼k ek alanlar (trace_id, model, channel vb.)
/// </summary>
public sealed record AiMessage
{
    public AiRole Role { get; init; }
    public string Content { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string? Name { get; init; }
    public IReadOnlyDictionary<string, string>? Meta { get; init; }

    public AiMessage(
        AiRole role,
        string content,
        DateTime timestampUtc,
        string? name = null,
        IReadOnlyDictionary<string, string>? meta = null)
    {
        Role = role;
        Content = NormalizeContent(content);
        TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc;
        Name = NormalizeOptional(name);
        Meta = NormalizeMeta(meta);
    }

    // SÄ±k kullanÄ±m: doÄŸru Role ile mesaj Ã¼retmek iÃ§in factory metotlar.
    // BÃ¶ylece AiRole'u yanlÄ±ÅŸ parametreye verme gibi hatalar azalÄ±r.

    public static AiMessage System(string content, string? name = null, IReadOnlyDictionary<string, string>? meta = null)
        => new(AiRole.System, content, DateTime.UtcNow, name, meta);

    public static AiMessage User(string content, string? name = null, IReadOnlyDictionary<string, string>? meta = null)
        => new(AiRole.User, content, DateTime.UtcNow, name, meta);

    public static AiMessage Assistant(string content, string? name = null, IReadOnlyDictionary<string, string>? meta = null)
        => new(AiRole.Assistant, content, DateTime.UtcNow, name, meta);

    public static AiMessage Tool(string content, string? name = null, IReadOnlyDictionary<string, string>? meta = null)
        => new(AiRole.Tool, content, DateTime.UtcNow, name, meta);

    // BazÄ± yerlerde timestamp'i dÄ±ÅŸarÄ±dan set etmek isteyebilirsin.
    public static AiMessage At(AiRole role, string content, DateTime timestampUtc, string? name = null, IReadOnlyDictionary<string, string>? meta = null)
        => new(role, content, timestampUtc, name, meta);

    // ToolResult -> AiMessage dÃ¶nÃ¼ÅŸÃ¼mÃ¼ iÃ§in pratik helper
    public static AiMessage FromToolResult(ToolResult result, IReadOnlyDictionary<string, string>? meta = null)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        var content = result.Ok
            ? result.Output
            : $"ERROR: {result.Error}{(string.IsNullOrWhiteSpace(result.Output) ? string.Empty : Environment.NewLine + result.Output)}";

        return new AiMessage(
            role: AiRole.Tool,
            content: content,
            timestampUtc: result.TimestampUtc,
            name: result.Name,
            meta: meta);
    }

    private static string NormalizeContent(string? value)
    {
        if (value is null)
            return string.Empty;

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static IReadOnlyDictionary<string, string>? NormalizeMeta(IReadOnlyDictionary<string, string>? meta)
    {
        if (meta is null || meta.Count == 0)
            return null;

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in meta)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            var key = pair.Key.Trim();
            var value = pair.Value?.Trim() ?? string.Empty;
            normalized[key] = value;
        }

        if (normalized.Count == 0)
            return null;

        return new ReadOnlyDictionary<string, string>(normalized);
    }
}
