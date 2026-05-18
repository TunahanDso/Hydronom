namespace Hydronom.Core.Communication.Commands;

public sealed class HydronomCommandAuthorityValidator
{
    private readonly HydronomCommandAuthorityPolicy _policy;

    public HydronomCommandAuthorityValidator()
        : this(HydronomCommandAuthorityPolicy.Race)
    {
    }

    public HydronomCommandAuthorityValidator(HydronomCommandAuthorityPolicy policy)
    {
        _policy = policy ?? HydronomCommandAuthorityPolicy.Race;
    }

    public HydronomCommandAuthorityDecision Validate(HydronomCommandFrame command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var basicResult = ValidateBasicFields(command);

        if (!basicResult.Allowed)
        {
            return basicResult;
        }

        var sourceResult = ValidateSource(command);

        if (!sourceResult.Allowed)
        {
            return sourceResult;
        }

        var roleResult = ValidateRoleConsistency(command);

        if (!roleResult.Allowed)
        {
            return roleResult;
        }

        var permissionResult = ValidateCommandPermission(command);

        if (!permissionResult.Allowed)
        {
            return permissionResult;
        }

        var safetyResult = ValidateSafetyRules(command);

        if (!safetyResult.Allowed)
        {
            return safetyResult;
        }

        return HydronomCommandAuthorityDecision.Allow(command);
    }

    private static HydronomCommandAuthorityDecision ValidateBasicFields(HydronomCommandFrame command)
    {
        var issues = new List<string>();

        if (command.Kind == HydronomCommandKind.Unknown)
        {
            issues.Add("COMMAND_KIND_UNKNOWN");
        }

        if (command.Authority == HydronomCommandAuthority.Unknown)
        {
            issues.Add("COMMAND_AUTHORITY_UNKNOWN");
        }

        if (string.IsNullOrWhiteSpace(command.SourceId))
        {
            issues.Add("SOURCE_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(command.TargetId))
        {
            issues.Add("TARGET_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(command.VehicleId))
        {
            issues.Add("VEHICLE_ID_EMPTY");
        }

        if (command.Sequence == 0)
        {
            issues.Add("SEQUENCE_ZERO");
        }

        if (issues.Count > 0)
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "COMMAND_BASIC_FIELDS_INVALID",
                issues.ToArray());
        }

        return HydronomCommandAuthorityDecision.Allow(
            command,
            "COMMAND_BASIC_FIELDS_VALID");
    }

    private HydronomCommandAuthorityDecision ValidateSource(HydronomCommandFrame command)
    {
        if (_policy.TrustedSourceIds.Count > 0 &&
            !_policy.TrustedSourceIds.Contains(command.SourceId))
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "SOURCE_NOT_TRUSTED",
                $"SourceId güvenilir kaynak listesinde değil. SourceId={command.SourceId}");
        }

        if (_policy.KnownSourceAuthorities.Count == 0)
        {
            return HydronomCommandAuthorityDecision.Allow(
                command,
                "SOURCE_AUTHORITY_MAP_EMPTY");
        }

        if (!_policy.KnownSourceAuthorities.TryGetValue(command.SourceId, out var expectedAuthority))
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "SOURCE_AUTHORITY_UNKNOWN",
                $"SourceId kayıtlı authority map içinde yok. SourceId={command.SourceId}");
        }

        if (expectedAuthority != command.Authority)
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "SOURCE_AUTHORITY_MISMATCH",
                $"SourceId beklenen authority ile gelmedi. SourceId={command.SourceId}, Expected={expectedAuthority}, Actual={command.Authority}");
        }

        return HydronomCommandAuthorityDecision.Allow(
            command,
            "SOURCE_AUTHORITY_VALID");
    }

    private HydronomCommandAuthorityDecision ValidateRoleConsistency(HydronomCommandFrame command)
    {
        if (!_policy.AllowUnknownAuthority &&
            command.Authority == HydronomCommandAuthority.Unknown)
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "UNKNOWN_AUTHORITY_REJECTED",
                "Unknown authority kabul edilmiyor.");
        }

        if (_policy.DeveloperMode &&
            _policy.AllowDeveloperOverride &&
            command.Authority == HydronomCommandAuthority.Developer)
        {
            return HydronomCommandAuthorityDecision.Allow(
                command,
                "DEVELOPER_OVERRIDE_ALLOWED");
        }

        if (command.Authority == HydronomCommandAuthority.Observer &&
            _policy.AllowObserverStatusOnly &&
            command.Kind is not HydronomCommandKind.RequestStatus
                and not HydronomCommandKind.RequestSnapshot)
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "OBSERVER_STATUS_ONLY",
                "Observer yalnızca status/snapshot isteyebilir.");
        }

        return HydronomCommandAuthorityDecision.Allow(
            command,
            "ROLE_CONSISTENCY_VALID");
    }

    private HydronomCommandAuthorityDecision ValidateCommandPermission(HydronomCommandFrame command)
    {
        if (_policy.DeveloperMode &&
            _policy.AllowDeveloperOverride &&
            command.Authority == HydronomCommandAuthority.Developer)
        {
            return HydronomCommandAuthorityDecision.Allow(
                command,
                "DEVELOPER_PERMISSION_OVERRIDE_ALLOWED");
        }

        if (!_policy.AllowedCommandsByAuthority.TryGetValue(command.Authority, out var allowedKinds))
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "AUTHORITY_NOT_CONFIGURED",
                $"Authority için komut izin listesi yok. Authority={command.Authority}");
        }

        if (!allowedKinds.Contains(command.Kind))
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "COMMAND_NOT_ALLOWED_FOR_AUTHORITY",
                $"Bu authority bu komutu veremez. Authority={command.Authority}, Kind={command.Kind}");
        }

        return HydronomCommandAuthorityDecision.Allow(
            command,
            "COMMAND_ALLOWED_FOR_AUTHORITY");
    }

    private HydronomCommandAuthorityDecision ValidateSafetyRules(HydronomCommandFrame command)
    {
        if (_policy.RequireOperatorIdForOperatorCommands &&
            command.Authority is HydronomCommandAuthority.Operator
                or HydronomCommandAuthority.Supervisor
                or HydronomCommandAuthority.SafetyOfficer
                or HydronomCommandAuthority.EmergencyConsole
                or HydronomCommandAuthority.Developer &&
            string.IsNullOrWhiteSpace(command.OperatorId))
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "OPERATOR_ID_REQUIRED",
                "Operatör yetkili komutlarda OperatorId boş olamaz.");
        }

        if (_policy.RequireReasonForSafetyCriticalCommands &&
            command.SafetyCritical &&
            string.IsNullOrWhiteSpace(command.Reason))
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "SAFETY_CRITICAL_REASON_REQUIRED",
                "Safety critical komutlarda Reason boş olamaz.");
        }

        if (!_policy.AllowAutonomousRuntimeEmergencyStop &&
            command.Authority == HydronomCommandAuthority.AutonomousRuntime &&
            command.Kind == HydronomCommandKind.EmergencyStop)
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "AUTONOMOUS_RUNTIME_ESTOP_NOT_ALLOWED",
                "AutonomousRuntime haberleşme yetkisiyle EmergencyStop gönderemez.");
        }

        if (!_policy.AllowAutonomousRuntimeArmDisarm &&
            command.Authority == HydronomCommandAuthority.AutonomousRuntime &&
            command.Kind is HydronomCommandKind.Arm or HydronomCommandKind.Disarm)
        {
            return HydronomCommandAuthorityDecision.Reject(
                command,
                "AUTONOMOUS_RUNTIME_ARM_DISARM_NOT_ALLOWED",
                "AutonomousRuntime haberleşme yetkisiyle Arm/Disarm gönderemez.");
        }

        return HydronomCommandAuthorityDecision.Allow(
            command,
            "SAFETY_AUTHORITY_RULES_VALID");
    }
}