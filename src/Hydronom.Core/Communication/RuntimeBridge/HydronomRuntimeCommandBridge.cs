using Hydronom.Core.Communication.Commands;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed class HydronomRuntimeCommandBridge
{
    public HydronomRuntimeCommandBridgeResult Convert(
        HydronomCommandFrame command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = ValidateCommand(command);

        if (validation.Count > 0)
        {
            return HydronomRuntimeCommandBridgeResult.Reject(
                command,
                "COMMAND_NOT_CONVERTIBLE_TO_RUNTIME_INTENT",
                validation.ToArray());
        }

        var intentKind = MapIntentKind(command);

        if (intentKind == HydronomRuntimeCommandIntentKind.Unknown)
        {
            return HydronomRuntimeCommandBridgeResult.Reject(
                command,
                "COMMAND_KIND_NOT_MAPPED_TO_RUNTIME_INTENT",
                $"Command kind runtime intent'e çevrilemedi. Kind={command.Kind}");
        }

        var parameters = NormalizeParameters(command);

        var intent = new HydronomRuntimeCommandIntent
        {
            Kind = intentKind,
            SourceCommandKind = command.Kind,
            Authority = command.Authority,

            CommandId = command.CommandId,
            SourceId = command.SourceId,
            TargetId = command.TargetId,
            VehicleId = command.VehicleId,
            OperatorId = command.OperatorId,

            Sequence = command.Sequence,
            TimestampUnixMs = command.TimestampUnixMs,

            RequiresAck = command.RequiresAck,
            SafetyCritical = command.SafetyCritical,
            Reason = command.Reason,

            Parameters = parameters
        };

        return HydronomRuntimeCommandBridgeResult.Accept(
            command,
            intent);
    }

    private static HydronomRuntimeCommandIntentKind MapIntentKind(
        HydronomCommandFrame command)
    {
        return command.Kind switch
        {
            HydronomCommandKind.Arm =>
                HydronomRuntimeCommandIntentKind.Arm,

            HydronomCommandKind.Disarm =>
                HydronomRuntimeCommandIntentKind.Disarm,

            HydronomCommandKind.EmergencyStop =>
                HydronomRuntimeCommandIntentKind.EmergencyStop,

            HydronomCommandKind.ManualControl =>
                HydronomRuntimeCommandIntentKind.ManualControl,

            HydronomCommandKind.MissionCommand =>
                MapMissionCommand(command),

            HydronomCommandKind.ScenarioCommand =>
                MapScenarioCommand(command),

            HydronomCommandKind.PauseMission =>
                HydronomRuntimeCommandIntentKind.PauseMission,

            HydronomCommandKind.ResumeMission =>
                HydronomRuntimeCommandIntentKind.ResumeMission,

            HydronomCommandKind.AbortMission =>
                HydronomRuntimeCommandIntentKind.AbortMission,

            HydronomCommandKind.SetMode =>
                HydronomRuntimeCommandIntentKind.SetMode,

            HydronomCommandKind.SetTarget =>
                HydronomRuntimeCommandIntentKind.SetTarget,

            HydronomCommandKind.RequestStatus =>
                HydronomRuntimeCommandIntentKind.RequestStatus,

            HydronomCommandKind.RequestSnapshot =>
                HydronomRuntimeCommandIntentKind.RequestSnapshot,

            HydronomCommandKind.AuthorityClaim =>
                HydronomRuntimeCommandIntentKind.AuthorityClaim,

            HydronomCommandKind.AuthorityRelease =>
                HydronomRuntimeCommandIntentKind.AuthorityRelease,

            HydronomCommandKind.Custom =>
                HydronomRuntimeCommandIntentKind.Custom,

            _ =>
                HydronomRuntimeCommandIntentKind.Unknown
        };
    }

    private static HydronomRuntimeCommandIntentKind MapMissionCommand(
        HydronomCommandFrame command)
    {
        var action = GetAction(command);

        return action switch
        {
            "start" or "startmission" or "mission.start" =>
                HydronomRuntimeCommandIntentKind.StartMission,

            "stop" or "stopmission" or "mission.stop" =>
                HydronomRuntimeCommandIntentKind.StopMission,

            "pause" or "pausemission" or "mission.pause" =>
                HydronomRuntimeCommandIntentKind.PauseMission,

            "resume" or "resumemission" or "mission.resume" =>
                HydronomRuntimeCommandIntentKind.ResumeMission,

            "abort" or "abortmission" or "mission.abort" =>
                HydronomRuntimeCommandIntentKind.AbortMission,

            "startscenario" or "scenario.start" =>
                HydronomRuntimeCommandIntentKind.StartScenario,

            "stopscenario" or "scenario.stop" =>
                HydronomRuntimeCommandIntentKind.StopScenario,

            _ =>
                HydronomRuntimeCommandIntentKind.StartMission
        };
    }

    private static HydronomRuntimeCommandIntentKind MapScenarioCommand(
        HydronomCommandFrame command)
    {
        var action = GetAction(command);

        return action switch
        {
            "start" or "startscenario" or "scenario.start" =>
                HydronomRuntimeCommandIntentKind.StartScenario,

            "stop" or "stopscenario" or "scenario.stop" =>
                HydronomRuntimeCommandIntentKind.StopScenario,

            "pause" or "pausescenario" or "scenario.pause" =>
                HydronomRuntimeCommandIntentKind.PauseScenario,

            "resume" or "resumescenario" or "scenario.resume" =>
                HydronomRuntimeCommandIntentKind.ResumeScenario,

            _ =>
                HydronomRuntimeCommandIntentKind.StartScenario
        };
    }

    private static string GetAction(HydronomCommandFrame command)
    {
        if (command.Parameters.TryGetValue("action", out var action) &&
            !string.IsNullOrWhiteSpace(action))
        {
            return NormalizeToken(action);
        }

        if (command.Parameters.TryGetValue("command", out var commandName) &&
            !string.IsNullOrWhiteSpace(commandName))
        {
            return NormalizeToken(commandName);
        }

        if (command.Parameters.TryGetValue("type", out var type) &&
            !string.IsNullOrWhiteSpace(type))
        {
            return NormalizeToken(type);
        }

        return "";
    }

    private static IReadOnlyDictionary<string, string> NormalizeParameters(
        HydronomCommandFrame command)
    {
        var normalized = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in command.Parameters)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            normalized[item.Key.Trim()] = item.Value ?? "";
        }

        if (!string.IsNullOrWhiteSpace(command.Reason) &&
            !normalized.ContainsKey("reason"))
        {
            normalized["reason"] = command.Reason;
        }

        if (!string.IsNullOrWhiteSpace(command.OperatorId) &&
            !normalized.ContainsKey("operatorId"))
        {
            normalized["operatorId"] = command.OperatorId;
        }

        return normalized;
    }

    private static IReadOnlyList<string> ValidateCommand(
        HydronomCommandFrame command)
    {
        var issues = new List<string>();

        if (command.Kind == HydronomCommandKind.Unknown)
        {
            issues.Add("COMMAND_KIND_UNKNOWN");
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

        return issues;
    }

    private static string NormalizeToken(string value)
    {
        return value
            .Trim()
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}