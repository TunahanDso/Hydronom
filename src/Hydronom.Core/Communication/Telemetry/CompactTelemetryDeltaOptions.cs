namespace Hydronom.Core.Communication.Telemetry;

public sealed record CompactTelemetryDeltaOptions
{
    public double PositionEpsilonM { get; init; } = 0.01;

    public double AngleEpsilonRad { get; init; } = 0.001;

    public double VelocityEpsilonMps { get; init; } = 0.01;

    public double AngularVelocityEpsilonRadps { get; init; } = 0.001;

    public double SpeedEpsilonMps { get; init; } = 0.01;

    public double DistanceEpsilonM { get; init; } = 0.01;

    public double BatteryVoltageEpsilonV { get; init; } = 0.01;

    public double BatteryPercentEpsilon { get; init; } = 0.1;

    public double Ratio01Epsilon { get; init; } = 0.001;

    public double ForceEpsilonN { get; init; } = 0.1;

    public double TorqueEpsilonNm { get; init; } = 0.01;

    public bool AlwaysIncludeVehicleIdentity { get; init; } = true;

    public bool ForceFullFrameWhenPreviousMissing { get; init; } = true;

    public CompactTelemetryField ForcedFields { get; init; } = CompactTelemetryField.None;

    public static CompactTelemetryDeltaOptions Default { get; } = new();

    public static CompactTelemetryDeltaOptions Sensitive { get; } = new()
    {
        PositionEpsilonM = 0.005,
        AngleEpsilonRad = 0.0005,
        VelocityEpsilonMps = 0.005,
        AngularVelocityEpsilonRadps = 0.0005,
        SpeedEpsilonMps = 0.005,
        DistanceEpsilonM = 0.005,
        BatteryVoltageEpsilonV = 0.005,
        BatteryPercentEpsilon = 0.05,
        Ratio01Epsilon = 0.0005,
        ForceEpsilonN = 0.05,
        TorqueEpsilonNm = 0.005
    };

    public static CompactTelemetryDeltaOptions LowBandwidth { get; } = new()
    {
        // Zayıf bağlantıda küçük konum titreşimleri gönderilmez.
        PositionEpsilonM = 0.05,

        // Küçük açı salınımları ve IMU kaynaklı mikro değişimler bastırılır.
        AngleEpsilonRad = 0.005,

        // Lokal eksen hızlarında küçük farklar düşük bant modunda önemsiz kabul edilir.
        VelocityEpsilonMps = 0.03,

        // Küçük açısal hız değişimleri gönderilmez.
        AngularVelocityEpsilonRadps = 0.003,

        // 0.04 m/s gibi küçük hız farkları zayıf bağlantıda bastırılır.
        SpeedEpsilonMps = 0.05,

        // Mesafe hedefe göre anlamlı değiştiğinde gönderilir.
        DistanceEpsilonM = 0.05,

        // Küçük voltaj dalgalanmaları telemetriyi şişirmesin.
        BatteryVoltageEpsilonV = 0.05,

        // Batarya yüzdesinde yarım puandan küçük değişimler gönderilmez.
        BatteryPercentEpsilon = 0.5,

        // Risk/progress gibi 0..1 oranlarında küçük salınımlar bastırılır.
        Ratio01Epsilon = 0.02,

        // Kuvvet ve tork tarafında küçük solver titreşimleri bastırılır.
        ForceEpsilonN = 0.5,

        TorqueEpsilonNm = 0.05
    };
}