using System;
using System.Collections.Generic;
using Hydronom.Core.State.Validation;

namespace Hydronom.Core.State.Diagnostics
{
    /// <summary>
    /// State pipeline iÃ§in toplu diagnostics modeli.
    ///
    /// Bu yapÄ± ileride OperationSnapshot, Gateway telemetry ve Ops diagnostics paneline baÄŸlanabilir.
    /// </summary>
    public readonly record struct StatePipelineDiagnostics(
        DateTime TimestampUtc,
        StateAuthoritySnapshot Authority,
        IReadOnlyList<StateValidationIssue> ValidationIssues,
        bool HasWarnings,
        bool HasErrors,
        bool HasCriticalIssues,
        string Summary
    )
    {
        public static StatePipelineDiagnostics Create(
            StateAuthoritySnapshot authority,
            IReadOnlyList<StateValidationIssue>? issues = null
        )
        {
            issues ??= Array.Empty<StateValidationIssue>();

            bool hasWarnings = false;
            bool hasErrors = false;
            bool hasCritical = false;

            foreach (var issue in issues)
            {
                if (issue.Severity == StateValidationSeverity.Warning)
                    hasWarnings = true;

                if (issue.Severity == StateValidationSeverity.Error)
                    hasErrors = true;

                if (issue.Severity == StateValidationSeverity.Critical)
                    hasCritical = true;
            }

            return new StatePipelineDiagnostics(
                TimestampUtc: DateTime.UtcNow,
                Authority: authority,
                ValidationIssues: issues,
                HasWarnings: hasWarnings,
                HasErrors: hasErrors,
                HasCriticalIssues: hasCritical,
                Summary: BuildSummary(authority, hasWarnings, hasErrors, hasCritical)
            );
        }

        private static string BuildSummary(
            StateAuthoritySnapshot authority,
            bool hasWarnings,
            bool hasErrors,
            bool hasCritical
        )
        {
            if (hasCritical)
                return $"Critical state pipeline issue. {authority.Summary}";

            if (hasErrors)
                return $"State pipeline has errors. {authority.Summary}";

            if (hasWarnings)
                return $"State pipeline has warnings. {authority.Summary}";

            return $"State pipeline nominal. {authority.Summary}";
        }
    }
}
