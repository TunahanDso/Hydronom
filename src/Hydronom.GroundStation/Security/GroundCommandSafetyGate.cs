namespace Hydronom.GroundStation.Security;

using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafÄ±nda komutun hedef araÃ§ ve operasyon baÄŸlamÄ±na gÃ¶re gÃ¶nderilebilirliÄŸini kontrol eder.
/// 
/// Bu gate araÃ§ Ã¼zerindeki SafetyGate'in yerine geÃ§mez.
/// Sadece yer istasyonu seviyesinde Ã¶n gÃ¼venlik filtresi saÄŸlar.
/// AraÃ§ Ã¼zerindeki local safety her zaman son karar kapÄ±sÄ± olarak kalmalÄ±dÄ±r.
/// </summary>
public sealed class GroundCommandSafetyGate
{
    private readonly CommandValidator _validator;
    private readonly AuthorityManager _authorityManager;
    private readonly CommandAuthorityPolicy _policy;

    public GroundCommandSafetyGate(
        CommandValidator? validator = null,
        AuthorityManager? authorityManager = null,
        CommandAuthorityPolicy? policy = null)
    {
        _policy = policy ?? new CommandAuthorityPolicy();
        _validator = validator ?? new CommandValidator();
        _authorityManager = authorityManager ?? new AuthorityManager(_policy);
    }

    /// <summary>
    /// Komutu yapÄ±sal, yetki ve hedef araÃ§ baÄŸlamÄ±nda deÄŸerlendirir.
    /// </summary>
    public CommandValidationResult Evaluate(
        FleetCommand? command,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        DateTimeOffset? nowUtc = null)
    {
        var issues = new List<CommandValidationIssue>();

        var structureResult = _validator.Validate(command);
        issues.AddRange(structureResult.Issues);

        if (command is null)
            return CommandValidationResult.FromIssues(issues);

        var authorityResult = _authorityManager.Evaluate(command, nowUtc);
        issues.AddRange(authorityResult.Issues);

        fleetSnapshot ??= Array.Empty<VehicleNodeStatus>();

        var isBroadcast =
            string.Equals(command.TargetNodeId, "BROADCAST", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.TargetNodeId, "*", StringComparison.OrdinalIgnoreCase);

        if (isBroadcast)
        {
            if (!_policy.AllowBroadcastCommands)
            {
                issues.Add(CommandValidationIssue.Blocking(
                    "BROADCAST_REJECTED",
                    "Broadcast commands are rejected by policy."));
            }
        }
        else
        {
            var target = fleetSnapshot.FirstOrDefault(x =>
                string.Equals(x.Identity.NodeId, command.TargetNodeId, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                if (_policy.RejectUnknownTargets)
                {
                    issues.Add(CommandValidationIssue.Blocking(
                        "TARGET_UNKNOWN",
                        $"Target node '{command.TargetNodeId}' is not known by FleetRegistry."));
                }
                else
                {
                    issues.Add(CommandValidationIssue.Warning(
                        "TARGET_UNKNOWN_WARNING",
                        $"Target node '{command.TargetNodeId}' is not known by FleetRegistry."));
                }
            }
            else if (!target.IsOnline)
            {
                if (_policy.RejectOfflineTargets)
                {
                    issues.Add(CommandValidationIssue.Blocking(
                        "TARGET_OFFLINE",
                        $"Target node '{command.TargetNodeId}' is offline."));
                }
                else
                {
                    issues.Add(CommandValidationIssue.Warning(
                        "TARGET_OFFLINE_WARNING",
                        $"Target node '{command.TargetNodeId}' is offline."));
                }
            }
        }

        return CommandValidationResult.FromIssues(
            issues,
            issues.Any(x => x.IsBlocking)
                ? "Command rejected by GroundCommandSafetyGate."
                : "Command accepted by GroundCommandSafetyGate.");
    }
}
