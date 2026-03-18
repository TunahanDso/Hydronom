using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hydronom.Core.Domain.AI;

public sealed record ToolResult
{
    public string ToolCallId { get; init; }
    public string Name { get; init; }
    public bool Ok { get; init; }
    public string Output { get; init; }
    public DateTime TimestampUtc { get; init; }
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
    public string? Error { get; init; }

    public ToolResult(
        string toolCallId,
        string name,
        bool ok,
        string output,
        DateTime timestampUtc,
        IReadOnlyDictionary<string, object?>? data = null,
        string? error = null)
    {
        ToolCallId = RequireText(toolCallId, nameof(toolCallId));
        Name = RequireText(name, nameof(name));
        Ok = ok;
        Output = output ?? string.Empty;
        TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc;
        Data = NormalizeData(data);
        Error = NormalizeOptional(error);

        if (Ok && !string.IsNullOrWhiteSpace(Error))
            throw new ArgumentException("Başarılı ToolResult için Error dolu olamaz.", nameof(error));

        if (!Ok && string.IsNullOrWhiteSpace(Error))
            throw new ArgumentException("Başarısız ToolResult için Error boş olamaz.", nameof(error));
    }

    public static ToolResult Success(
        string toolCallId,
        string name,
        string output,
        IReadOnlyDictionary<string, object?>? data = null)
        => new(
            toolCallId: toolCallId,
            name: name,
            ok: true,
            output: output ?? string.Empty,
            timestampUtc: DateTime.UtcNow,
            data: data,
            error: null);

    public static ToolResult Fail(
        string toolCallId,
        string name,
        string error,
        string output = "",
        IReadOnlyDictionary<string, object?>? data = null)
        => new(
            toolCallId: toolCallId,
            name: name,
            ok: false,
            output: output ?? string.Empty,
            timestampUtc: DateTime.UtcNow,
            data: data,
            error: error);

    private static string RequireText(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Alan boş olamaz.", paramName);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static IReadOnlyDictionary<string, object?>? NormalizeData(IReadOnlyDictionary<string, object?>? data)
    {
        if (data is null || data.Count == 0)
            return null;

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in data)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            normalized[pair.Key.Trim()] = pair.Value;
        }

        if (normalized.Count == 0)
            return null;

        return new ReadOnlyDictionary<string, object?>(normalized);
    }
}