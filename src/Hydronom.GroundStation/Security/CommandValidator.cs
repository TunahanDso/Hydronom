namespace Hydronom.GroundStation.Security;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// FleetCommand modelinin temel yapısal doğrulamasını yapar.
/// </summary>
public sealed class CommandValidator
{
    /// <summary>
    /// Komutun temel alanlarını doğrular.
    /// </summary>
    public CommandValidationResult Validate(FleetCommand? command)
    {
        var issues = new List<CommandValidationIssue>();

        if (command is null)
        {
            issues.Add(CommandValidationIssue.Blocking(
                "COMMAND_NULL",
                "Command is null."));

            return CommandValidationResult.FromIssues(issues);
        }

        if (string.IsNullOrWhiteSpace(command.CommandId))
        {
            issues.Add(CommandValidationIssue.Blocking(
                "COMMAND_ID_EMPTY",
                "CommandId is empty."));
        }

        if (string.IsNullOrWhiteSpace(command.SourceNodeId))
        {
            issues.Add(CommandValidationIssue.Blocking(
                "SOURCE_EMPTY",
                "SourceNodeId is empty."));
        }

        if (string.IsNullOrWhiteSpace(command.TargetNodeId))
        {
            issues.Add(CommandValidationIssue.Blocking(
                "TARGET_EMPTY",
                "TargetNodeId is empty."));
        }

        if (string.IsNullOrWhiteSpace(command.CommandType))
        {
            issues.Add(CommandValidationIssue.Blocking(
                "COMMAND_TYPE_EMPTY",
                "CommandType is empty."));
        }

        if (string.IsNullOrWhiteSpace(command.AuthorityLevel))
        {
            issues.Add(CommandValidationIssue.Blocking(
                "AUTHORITY_EMPTY",
                "AuthorityLevel is empty."));
        }

        if (command.Priority == MessagePriority.Unknown)
        {
            issues.Add(CommandValidationIssue.Warning(
                "PRIORITY_UNKNOWN",
                "Command priority is Unknown."));
        }

        return CommandValidationResult.FromIssues(
            issues,
            issues.Count == 0
                ? "Command structure is valid."
                : null);
    }
}