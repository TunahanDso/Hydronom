namespace Hydronom.Core.Communication.Telemetry;

public sealed class CompactTelemetryDeltaBuilder
{
    private readonly CompactTelemetryDeltaOptions _options;

    public CompactTelemetryDeltaBuilder()
        : this(CompactTelemetryDeltaOptions.Default)
    {
    }

    public CompactTelemetryDeltaBuilder(CompactTelemetryDeltaOptions options)
    {
        _options = options ?? CompactTelemetryDeltaOptions.Default;
    }

    public CompactTelemetryFrame BuildDelta(
        CompactTelemetryFrame? previous,
        CompactTelemetryFrame current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null && _options.ForceFullFrameWhenPreviousMissing)
        {
            return current with
            {
                FieldMask = current.FieldMask == CompactTelemetryField.None
                    ? CompactTelemetryField.All
                    : current.FieldMask
            };
        }

        if (previous is null)
        {
            return current with
            {
                FieldMask = current.FieldMask | _options.ForcedFields
            };
        }

        var mask = CompactTelemetryField.None;

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.PositionX,
            previous.PositionXM,
            current.PositionXM,
            _options.PositionEpsilonM);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.PositionY,
            previous.PositionYM,
            current.PositionYM,
            _options.PositionEpsilonM);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.PositionZ,
            previous.PositionZM,
            current.PositionZM,
            _options.PositionEpsilonM);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.Roll,
            previous.RollRad,
            current.RollRad,
            _options.AngleEpsilonRad);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.Pitch,
            previous.PitchRad,
            current.PitchRad,
            _options.AngleEpsilonRad);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.Yaw,
            previous.YawRad,
            current.YawRad,
            _options.AngleEpsilonRad);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.VelocityX,
            previous.VelocityXMps,
            current.VelocityXMps,
            _options.VelocityEpsilonMps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.VelocityY,
            previous.VelocityYMps,
            current.VelocityYMps,
            _options.VelocityEpsilonMps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.VelocityZ,
            previous.VelocityZMps,
            current.VelocityZMps,
            _options.VelocityEpsilonMps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.AngularVelocityX,
            previous.AngularVelocityXRadps,
            current.AngularVelocityXRadps,
            _options.AngularVelocityEpsilonRadps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.AngularVelocityY,
            previous.AngularVelocityYRadps,
            current.AngularVelocityYRadps,
            _options.AngularVelocityEpsilonRadps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.AngularVelocityZ,
            previous.AngularVelocityZRadps,
            current.AngularVelocityZRadps,
            _options.AngularVelocityEpsilonRadps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.Speed,
            previous.SpeedMps,
            current.SpeedMps,
            _options.SpeedEpsilonMps);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.HeadingError,
            previous.HeadingErrorRad,
            current.HeadingErrorRad,
            _options.AngleEpsilonRad);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.DistanceToTarget,
            previous.DistanceToTargetM,
            current.DistanceToTargetM,
            _options.DistanceEpsilonM);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.BatteryVoltage,
            previous.BatteryVoltageV,
            current.BatteryVoltageV,
            _options.BatteryVoltageEpsilonV);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.BatteryPercent,
            previous.BatteryPercent,
            current.BatteryPercent,
            _options.BatteryPercentEpsilon);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.MissionProgress,
            previous.MissionProgress01,
            current.MissionProgress01,
            _options.Ratio01Epsilon);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.RiskScore,
            previous.RiskScore01,
            current.RiskScore01,
            _options.Ratio01Epsilon);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.ForceX,
            previous.ForceXN,
            current.ForceXN,
            _options.ForceEpsilonN);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.ForceY,
            previous.ForceYN,
            current.ForceYN,
            _options.ForceEpsilonN);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.ForceZ,
            previous.ForceZN,
            current.ForceZN,
            _options.ForceEpsilonN);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.TorqueX,
            previous.TorqueXNm,
            current.TorqueXNm,
            _options.TorqueEpsilonNm);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.TorqueY,
            previous.TorqueYNm,
            current.TorqueYNm,
            _options.TorqueEpsilonNm);

        mask = IncludeIfChanged(
            mask,
            CompactTelemetryField.TorqueZ,
            previous.TorqueZNm,
            current.TorqueZNm,
            _options.TorqueEpsilonNm);

        mask |= _options.ForcedFields;

        return current with
        {
            FieldMask = mask
        };
    }

    public bool HasMeaningfulChange(
        CompactTelemetryFrame? previous,
        CompactTelemetryFrame current)
    {
        var delta = BuildDelta(previous, current);
        return delta.FieldMask != CompactTelemetryField.None;
    }

    private static CompactTelemetryField IncludeIfChanged(
        CompactTelemetryField mask,
        CompactTelemetryField field,
        double previous,
        double current,
        double epsilon)
    {
        if (Math.Abs(current - previous) >= epsilon)
        {
            return mask | field;
        }

        return mask;
    }
}