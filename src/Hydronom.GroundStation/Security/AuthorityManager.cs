namespace Hydronom.GroundStation.Security;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafında komut yetki politikasını değerlendirir.
/// </summary>
public sealed class AuthorityManager
{
    private readonly HashSet<string> _seenCommandIds = new(StringComparer.OrdinalIgnoreCase);

    public CommandAuthorityPolicy Policy { get; }

    public AuthorityManager(CommandAuthorityPolicy? policy = null)
    {
        Policy = policy ?? new CommandAuthorityPolicy();
    }

    /// <summary>
    /// Komutun authority seviyesini ve replay/duplicate riskini değerlendirir.
    /// </summary>
    public CommandValidationResult Evaluate(FleetCommand? command, DateTimeOffset? nowUtc = null)
    {
        var issues = new List<CommandValidationIssue>();

        if (command is null)
        {
            issues.Add(CommandValidationIssue.Blocking(
                "COMMAND_NULL",
                "Command is null."));

            return CommandValidationResult.FromIssues(issues);
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;

        if (!Policy.AllowNonOperatorCommands && !command.IsOperatorIssued)
        {
            issues.Add(CommandValidationIssue.Blocking(
                "NON_OPERATOR_COMMAND_REJECTED",
                "Non-operator command is rejected by authority policy."));
        }

        if (Policy.RejectDuplicateCommandIds &&
            !string.IsNullOrWhiteSpace(command.CommandId) &&
            _seenCommandIds.Contains(command.CommandId))
        {
            issues.Add(CommandValidationIssue.Blocking(
                "DUPLICATE_COMMAND_ID",
                $"CommandId '{command.CommandId}' was already evaluated."));
        }

        if (Policy.RejectStaleCommands &&
            command.Metadata is not null &&
            command.Metadata.TryGetValue("createdUtc", out var createdUtcText) &&
            DateTimeOffset.TryParse(createdUtcText, out var createdUtc))
        {
            var age = now - createdUtc;

            if (age > Policy.MaxCommandAge)
            {
                issues.Add(CommandValidationIssue.Blocking(
                    "STALE_COMMAND",
                    $"Command is stale. Age={age.TotalMilliseconds:0} ms, max={Policy.MaxCommandAge.TotalMilliseconds:0} ms."));
            }
        }

        if (string.Equals(command.AuthorityLevel, "EmergencyCommand", StringComparison.OrdinalIgnoreCase))
        {
            if (Policy.RequireOperatorForEmergencyCommands && !command.IsOperatorIssued)
            {
                issues.Add(CommandValidationIssue.Blocking(
                    "EMERGENCY_REQUIRES_OPERATOR",
                    "EmergencyCommand requires operator-issued command."));
            }

            if (Policy.RequireEmergencyPriorityForEmergencyCommands &&
                command.Priority != MessagePriority.Emergency)
            {
                issues.Add(CommandValidationIssue.Blocking(
                    "EMERGENCY_PRIORITY_REQUIRED",
                    "EmergencyCommand must use Emergency priority."));
            }
        }

        if (!string.IsNullOrWhiteSpace(command.CommandId) &&
            !issues.Any(x => x.IsBlocking && x.Code == "DUPLICATE_COMMAND_ID"))
        {
            _seenCommandIds.Add(command.CommandId);
        }

        return CommandValidationResult.FromIssues(
            issues,
            issues.Count == 0
                ? "Command authority policy accepted."
                : null);
    }
}