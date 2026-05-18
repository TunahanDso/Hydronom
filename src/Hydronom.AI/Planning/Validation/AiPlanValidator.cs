using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.AI.Policies;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Planning.Validation;

public enum AiPlanIssueSeverity
{
    Info = 0,
    Warning = 1,
    Blocking = 2
}

public sealed record AiPlanValidationIssue(
    AiPlanIssueSeverity Severity,
    string Code,
    string Message
);

public sealed record AiPlanValidationResult(
    bool IsValid,
    IReadOnlyList<AiPlanValidationIssue> Issues
)
{
    public IReadOnlyList<AiPlanValidationIssue> BlockingIssues
        => Issues.Where(i => i.Severity == AiPlanIssueSeverity.Blocking).ToArray();

    public IReadOnlyList<AiPlanValidationIssue> Warnings
        => Issues.Where(i => i.Severity == AiPlanIssueSeverity.Warning).ToArray();

    public static AiPlanValidationResult Ok()
        => new(true, Array.Empty<AiPlanValidationIssue>());

    public static AiPlanValidationResult FromIssues(IReadOnlyList<AiPlanValidationIssue> issues)
        => new(!issues.Any(i => i.Severity == AiPlanIssueSeverity.Blocking), issues);
}

public sealed class AiPlanValidator
{
    public AiPlanValidationResult Validate(
        MissionPlan? plan,
        SafetyPolicy? safetyPolicy = null,
        AiAuthorityPolicy? authorityPolicy = null)
    {
        var issues = new List<AiPlanValidationIssue>();
        safetyPolicy ??= SafetyPolicy.DefaultSuggest();
        authorityPolicy ??= AiAuthorityPolicy.SuggestOnly();

        if (plan is null)
        {
            issues.Add(Block("PLAN_NULL", "AI MissionPlan null döndü."));
            return AiPlanValidationResult.FromIssues(issues);
        }

        if (string.IsNullOrWhiteSpace(plan.Id))
            issues.Add(Block("PLAN_ID_EMPTY", "MissionPlan.Id boş olamaz."));

        if (string.IsNullOrWhiteSpace(plan.Goal))
            issues.Add(Block("PLAN_GOAL_EMPTY", "MissionPlan.Goal boş olamaz."));
        else
            ValidateTextQuality(plan.Goal, "PLAN_GOAL", issues, minLetters: 8, minWords: 3);

        if (plan.Steps is null)
        {
            issues.Add(Block("PLAN_STEPS_NULL", "MissionPlan.Steps null olamaz."));
            return AiPlanValidationResult.FromIssues(issues);
        }

        if (plan.Steps.Count == 0)
            issues.Add(Block("PLAN_STEPS_EMPTY", "MissionPlan en az bir adım içermelidir."));

        if (plan.Steps.Count > authorityPolicy.MaxPlanSteps)
            issues.Add(Block("PLAN_TOO_LONG", $"Plan {plan.Steps.Count} adım içeriyor; izin verilen üst sınır {authorityPolicy.MaxPlanSteps}."));

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];

            if (step.Index != i)
                issues.Add(Warn("STEP_INDEX_MISMATCH", $"Step index beklenen {i}, gelen {step.Index}."));

            if (string.IsNullOrWhiteSpace(step.Title))
                issues.Add(Block("STEP_TITLE_EMPTY", $"Step[{i}].Title boş olamaz."));
            else
                ValidateTextQuality(step.Title, $"STEP_{i}_TITLE", issues, minLetters: 3, minWords: 1);

            if (string.IsNullOrWhiteSpace(step.Description))
                issues.Add(Block("STEP_DESCRIPTION_EMPTY", $"Step[{i}].Description boş olamaz."));
            else
                ValidateTextQuality(step.Description, $"STEP_{i}_DESCRIPTION", issues, minLetters: 10, minWords: 3);

            if (!string.IsNullOrWhiteSpace(step.Title) &&
                !string.IsNullOrWhiteSpace(step.Description) &&
                string.Equals(step.Title.Trim(), step.Description.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Warn("STEP_DESCRIPTION_EQUALS_TITLE", $"Step[{i}] açıklaması başlıkla aynı; açıklama daha açıklayıcı olmalı."));
            }

            var text = $"{step.Title} {step.Description}";

            foreach (var forbidden in authorityPolicy.ForbiddenPhrases)
            {
                if (!string.IsNullOrWhiteSpace(forbidden) &&
                    text.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Block("FORBIDDEN_PHRASE", $"Step[{i}] yasaklı ifade içeriyor: {forbidden}"));
                }
            }

            if (step.ExpectedTools is null)
            {
                issues.Add(Block("STEP_TOOLS_NULL", $"Step[{i}].ExpectedTools null olamaz."));
                continue;
            }

            foreach (var tool in step.ExpectedTools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    issues.Add(Warn("TOOL_EMPTY", $"Step[{i}] boş tool adı içeriyor."));
                    continue;
                }

                if (!safetyPolicy.IsToolAllowed(tool))
                    issues.Add(Block("TOOL_NOT_ALLOWED", $"Tool safety policy tarafından engellendi: {tool}"));

                if (safetyPolicy.RequiresApproval(tool))
                    issues.Add(Warn("TOOL_REQUIRES_APPROVAL", $"Tool insan onayı gerektiriyor: {tool}"));
            }
        }

        if (authorityPolicy.RequireHumanApprovalForMissionStart)
            issues.Add(Warn("HUMAN_APPROVAL_REQUIRED", "Bu plan suggest/advisory seviyesindedir; runtime görevi başlatmadan önce operatör onayı gerekir."));

        if (!authorityPolicy.AllowRuntimeCommandEmission)
            issues.Add(Warn("NO_DIRECT_RUNTIME_COMMAND", "AI doğrudan runtime/motor komutu üretme yetkisine sahip değildir."));

        return AiPlanValidationResult.FromIssues(issues);
    }

    private static void ValidateTextQuality(
        string text,
        string fieldCode,
        List<AiPlanValidationIssue> issues,
        int minLetters,
        int minWords)
    {
        var normalized = text.Trim();

        if (normalized.Length > 1200)
            issues.Add(Block($"{fieldCode}_TOO_LONG", $"{fieldCode} aşırı uzun görünüyor."));

        if (CountLetters(normalized) < minLetters)
            issues.Add(Block($"{fieldCode}_LOW_LETTER_COUNT", $"{fieldCode} yeterli anlamlı harf içermiyor."));

        if (CountWords(normalized) < minWords)
            issues.Add(Warn($"{fieldCode}_LOW_WORD_COUNT", $"{fieldCode} çok kısa görünüyor."));

        if (ContainsMojibake(normalized))
            issues.Add(Block($"{fieldCode}_MOJIBAKE", $"{fieldCode} bozuk encoding/mojibake içeriyor."));

        if (ContainsSuspiciousEscapes(normalized))
            issues.Add(Block($"{fieldCode}_SUSPICIOUS_ESCAPE", $"{fieldCode} şüpheli escape/backslash dizileri içeriyor."));

        if (ContainsDegenerateRepetition(normalized))
            issues.Add(Block($"{fieldCode}_DEGENERATE_REPETITION", $"{fieldCode} anlamsız tekrar veya model çökmesi belirtisi içeriyor."));

        if (ContainsPlaceholder(normalized))
            issues.Add(Block($"{fieldCode}_PLACEHOLDER", $"{fieldCode} placeholder açıklama içeriyor."));

        if (LooksLikeSchemaExample(normalized))
            issues.Add(Block($"{fieldCode}_SCHEMA_EXAMPLE", $"{fieldCode} gerçek cevap yerine şema/örnek metni gibi görünüyor."));
    }

    private static bool ContainsMojibake(string text)
    {
        var suspiciousTokens = new[]
        {
            "\\u00f0",
            "\\u00c7",
            "\\x",
            "Ã",
            "Ä",
            "Å",
            "Ð",
            "Ñ",
            "�",
            "\uFFFD"
        };

        foreach (var token in suspiciousTokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var nonLatinSuspicious = 0;

        foreach (var ch in text)
        {
            if (ch >= '\u0400' && ch <= '\u04FF') // Cyrillic block
                nonLatinSuspicious++;
        }

        return nonLatinSuspicious >= 3;
    }

    private static bool ContainsSuspiciousEscapes(string text)
    {
        var slashCount = text.Count(ch => ch == '\\');
        if (slashCount >= 3)
            return true;

        return text.Contains("\\n", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\\t", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\\u", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\\x", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsDegenerateRepetition(string text)
    {
        if (text.Contains("ççç", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.Contains("ööö", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.Length < 20)
            return false;

        var repeatedAdjacent = 0;

        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] == text[i - 1] && !char.IsWhiteSpace(text[i]))
                repeatedAdjacent++;
        }

        if (repeatedAdjacent > text.Length / 4)
            return true;

        var words = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.Trim(',', '.', ';', ':', '!', '?', '"', '\''))
            .Where(w => w.Length > 2)
            .ToArray();

        if (words.Length >= 8)
        {
            var mostCommon = words
                .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
                .Max(g => g.Count());

            if (mostCommon >= Math.Max(5, words.Length / 2))
                return true;
        }

        return false;
    }

    private static bool ContainsPlaceholder(string text)
    {
        var placeholders = new[]
        {
            "safe operational description",
            "short step title",
            "clear mission goal",
            "tool_name_if_needed",
            "ai generated mission plan",
            "bu adımda aracın neyi, neden",
            "görevin açık ve kısa hedef cümlesi"
        };

        foreach (var placeholder in placeholders)
        {
            if (text.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool LooksLikeSchemaExample(string text)
    {
        return text.Contains("\"index\"", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\"expectedTools\"", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\"createdUtc\"", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountLetters(string text)
    {
        var count = 0;

        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
                count++;
        }

        return count;
    }

    private static int CountWords(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static AiPlanValidationIssue Block(string code, string message)
        => new(AiPlanIssueSeverity.Blocking, code, message);

    private static AiPlanValidationIssue Warn(string code, string message)
        => new(AiPlanIssueSeverity.Warning, code, message);
}