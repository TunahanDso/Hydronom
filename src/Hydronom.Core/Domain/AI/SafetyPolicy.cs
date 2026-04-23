using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Hydronom.Core.Domain.AI;

public enum AiMode
{
    Suggest = 0,
    Autopilot = 1
}

// Basit bir politika: hangi tool'lar autopilot'ta çalışabilir?
public sealed record SafetyPolicy
{
    public AiMode Mode { get; init; }
    public IReadOnlySet<string> AllowedToolsInAutopilot { get; init; }
    public IReadOnlySet<string> AlwaysRequireApprovalTools { get; init; }
    public int MaxToolCallsPerCycle { get; init; }

    public SafetyPolicy(
        AiMode mode,
        IReadOnlySet<string>? allowedToolsInAutopilot,
        IReadOnlySet<string>? alwaysRequireApprovalTools,
        int maxToolCallsPerCycle = 8)
    {
        if (maxToolCallsPerCycle <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxToolCallsPerCycle), "MaxToolCallsPerCycle 0'dan büyük olmalıdır.");

        Mode = mode;
        AllowedToolsInAutopilot = NormalizeSet(allowedToolsInAutopilot);
        AlwaysRequireApprovalTools = NormalizeSet(alwaysRequireApprovalTools);
        MaxToolCallsPerCycle = maxToolCallsPerCycle;
    }

    public bool IsToolAllowed(string toolName)
    {
        var normalized = NormalizeToolName(toolName);

        if (Mode == AiMode.Suggest)
            return true;

        if (AlwaysRequireApprovalTools.Contains(normalized))
            return false;

        return AllowedToolsInAutopilot.Contains(normalized);
    }

    public bool RequiresApproval(string toolName)
    {
        var normalized = NormalizeToolName(toolName);

        if (AlwaysRequireApprovalTools.Contains(normalized))
            return true;

        return Mode == AiMode.Suggest;
    }

    public static SafetyPolicy DefaultSuggest()
        => new(
            mode: AiMode.Suggest,
            allowedToolsInAutopilot: ToSet(Array.Empty<string>()),
            alwaysRequireApprovalTools: ToSet(Array.Empty<string>()),
            maxToolCallsPerCycle: 8
        );

    public static SafetyPolicy DefaultAutopilot(
        IEnumerable<string>? allowedToolsInAutopilot,
        IEnumerable<string>? alwaysRequireApprovalTools = null,
        int maxToolCallsPerCycle = 8)
        => new(
            mode: AiMode.Autopilot,
            allowedToolsInAutopilot: ToSet(allowedToolsInAutopilot),
            alwaysRequireApprovalTools: ToSet(alwaysRequireApprovalTools),
            maxToolCallsPerCycle: maxToolCallsPerCycle
        );

    private static IReadOnlySet<string> NormalizeSet(IReadOnlySet<string>? values)
    {
        if (values is null || values.Count == 0)
            return new ReadOnlySet<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            set.Add(value.Trim());
        }

        return new ReadOnlySet<string>(set);
    }

    private static IReadOnlySet<string> ToSet(IEnumerable<string>? values)
    {
        if (values is null)
            return new ReadOnlySet<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            set.Add(value.Trim());
        }

        return new ReadOnlySet<string>(set);
    }

    private static string NormalizeToolName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool adı boş olamaz.", nameof(toolName));

        return toolName.Trim();
    }

    private sealed class ReadOnlySet<T> : IReadOnlySet<T>
    {
        private readonly HashSet<T> _set;

        public ReadOnlySet(HashSet<T> set)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public int Count => _set.Count;

        public bool Contains(T item) => _set.Contains(item);

        public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _set.GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);
        public bool TryGetValue(T equalValue, out T actualValue) => _set.TryGetValue(equalValue, out actualValue!);
    }
}