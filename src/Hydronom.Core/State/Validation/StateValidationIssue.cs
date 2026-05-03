using System;

namespace Hydronom.Core.State.Validation
{
    /// <summary>
    /// State pipeline iÃ§inde yakalanan doÄŸrulama bulgusu.
    ///
    /// Bu yapÄ± StateAuthority, Diagnostics, Ops ve Ground Station tarafÄ±nda
    /// "state neden kabul edildi / reddedildi / riskli gÃ¶rÃ¼ldÃ¼?" sorusunu aÃ§Ä±klamak iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct StateValidationIssue(
        string Code,
        StateValidationSeverity Severity,
        string Message,
        string Source,
        DateTime TimestampUtc
    )
    {
        public static StateValidationIssue Info(string code, string message, string source = "StateValidation")
        {
            return new StateValidationIssue(
                Code: Normalize(code, "INFO"),
                Severity: StateValidationSeverity.Info,
                Message: Normalize(message, "Bilgi mesajÄ±."),
                Source: Normalize(source, "StateValidation"),
                TimestampUtc: DateTime.UtcNow
            );
        }

        public static StateValidationIssue Warning(string code, string message, string source = "StateValidation")
        {
            return new StateValidationIssue(
                Code: Normalize(code, "WARNING"),
                Severity: StateValidationSeverity.Warning,
                Message: Normalize(message, "UyarÄ± mesajÄ±."),
                Source: Normalize(source, "StateValidation"),
                TimestampUtc: DateTime.UtcNow
            );
        }

        public static StateValidationIssue Error(string code, string message, string source = "StateValidation")
        {
            return new StateValidationIssue(
                Code: Normalize(code, "ERROR"),
                Severity: StateValidationSeverity.Error,
                Message: Normalize(message, "Hata mesajÄ±."),
                Source: Normalize(source, "StateValidation"),
                TimestampUtc: DateTime.UtcNow
            );
        }

        public static StateValidationIssue Critical(string code, string message, string source = "StateValidation")
        {
            return new StateValidationIssue(
                Code: Normalize(code, "CRITICAL"),
                Severity: StateValidationSeverity.Critical,
                Message: Normalize(message, "Kritik hata mesajÄ±."),
                Source: Normalize(source, "StateValidation"),
                TimestampUtc: DateTime.UtcNow
            );
        }

        public StateValidationIssue Sanitized()
        {
            return new StateValidationIssue(
                Code: Normalize(Code, "UNKNOWN"),
                Severity: Severity,
                Message: Normalize(Message, "AÃ§Ä±klama yok."),
                Source: Normalize(Source, "StateValidation"),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
