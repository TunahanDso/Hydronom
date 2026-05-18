namespace Hydronom.Core.Communication.Telemetry;

public sealed record CompactTelemetryFrame
{
    public string VehicleId { get; init; } = "";

    public ulong Sequence { get; init; }

    public long TimestampUnixMs { get; init; }

    public CompactTelemetryField FieldMask { get; init; } = CompactTelemetryField.None;

    public double PositionXM { get; init; }

    public double PositionYM { get; init; }

    public double PositionZM { get; init; }

    public double RollRad { get; init; }

    public double PitchRad { get; init; }

    public double YawRad { get; init; }

    public double VelocityXMps { get; init; }

    public double VelocityYMps { get; init; }

    public double VelocityZMps { get; init; }

    public double AngularVelocityXRadps { get; init; }

    public double AngularVelocityYRadps { get; init; }

    public double AngularVelocityZRadps { get; init; }

    public double SpeedMps { get; init; }

    public double HeadingErrorRad { get; init; }

    public double DistanceToTargetM { get; init; }

    public double BatteryVoltageV { get; init; }

    public double BatteryPercent { get; init; }

    public double MissionProgress01 { get; init; }

    public double RiskScore01 { get; init; }

    public double ForceXN { get; init; }

    public double ForceYN { get; init; }

    public double ForceZN { get; init; }

    public double TorqueXNm { get; init; }

    public double TorqueYNm { get; init; }

    public double TorqueZNm { get; init; }

    public bool Has(CompactTelemetryField field)
    {
        return FieldMask.HasFlag(field);
    }

    public static CompactTelemetryFrame Full(
        string vehicleId,
        ulong sequence)
    {
        return new CompactTelemetryFrame
        {
            VehicleId = vehicleId,
            Sequence = sequence,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            FieldMask = CompactTelemetryField.All
        };
    }
}