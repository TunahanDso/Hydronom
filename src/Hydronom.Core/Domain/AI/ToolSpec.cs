using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hydronom.Core.Domain.AI;

// Basit JSON-schema benzeri tanım: LLM'e "hangi argümanlar var" bilgisini verir.
public sealed record ToolSpec
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "number",
        "integer",
        "boolean",
        "object",
        "array"
    };

    public string Name { get; init; }
    public string Description { get; init; }
    public IReadOnlyList<ToolArgSpec> Args { get; init; }
    public bool Dangerous { get; init; }

    public ToolSpec(
        string Name,
        string Description,
        IReadOnlyList<ToolArgSpec> Args,
        bool Dangerous = false)
    {
        this.Name = RequireText(Name, nameof(Name));
        this.Description = RequireText(Description, nameof(Description));
        this.Args = NormalizeArgs(Args);
        this.Dangerous = Dangerous;
    }

    private static string RequireText(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Alan boş olamaz.", paramName);

        return value.Trim();
    }

    private static IReadOnlyList<ToolArgSpec> NormalizeArgs(IReadOnlyList<ToolArgSpec>? args)
    {
        if (args is null || args.Count == 0)
            return Array.Empty<ToolArgSpec>();

        var normalized = new List<ToolArgSpec>(args.Count);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            if (arg is null)
                continue;

            if (!seenNames.Add(arg.Name))
                throw new ArgumentException($"Aynı isimde birden fazla tool argümanı var: '{arg.Name}'.", nameof(args));

            if (!AllowedTypes.Contains(arg.Type))
                throw new ArgumentException(
                    $"Desteklenmeyen argüman tipi: '{arg.Type}'. İzin verilen tipler: string, number, integer, boolean, object, array.",
                    nameof(args));

            normalized.Add(arg);
        }

        if (normalized.Count == 0)
            return Array.Empty<ToolArgSpec>();

        return new ReadOnlyCollection<ToolArgSpec>(normalized);
    }
}

public sealed record ToolArgSpec
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "number",
        "integer",
        "boolean",
        "object",
        "array"
    };

    public string Name { get; init; }
    public string Type { get; init; } // "string" | "number" | "integer" | "boolean" | "object" | "array"
    public string Description { get; init; }
    public bool Required { get; init; }
    public object? Example { get; init; }

    public ToolArgSpec(
        string Name,
        string Type,
        string Description,
        bool Required = false,
        object? Example = null)
    {
        this.Name = RequireText(Name, nameof(Name));
        this.Type = NormalizeType(Type, nameof(Type));
        this.Description = RequireText(Description, nameof(Description));
        this.Required = Required;
        this.Example = Example;
    }

    private static string RequireText(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Alan boş olamaz.", paramName);

        return value.Trim();
    }

    private static string NormalizeType(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Alan boş olamaz.", paramName);

        var normalized = value.Trim().ToLowerInvariant();

        if (!AllowedTypes.Contains(normalized))
            throw new ArgumentException(
                $"Desteklenmeyen argüman tipi: '{value}'. İzin verilen tipler: string, number, integer, boolean, object, array.",
                paramName);

        return normalized;
    }
}