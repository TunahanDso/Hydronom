using Hydronom.Core.Domain;

namespace Hydronom.Core.Control
{
    /// <summary>
    /// Decision katmanının controller'a verdiği hedef davranış modeli.
    ///
    /// Bu model:
    /// - araçtan bağımsızdır
    /// - fiziksel motor çıktısı içermez
    /// - operasyonel hedef taşır
    /// </summary>
    public sealed record ControlIntent(
        ControlIntentKind Kind,

        Vec3 TargetPosition,

        double TargetHeadingDeg,

        double DesiredForwardSpeedMps,

        double DesiredDepthMeters,

        double DesiredAltitudeMeters,

        bool HoldHeading,

        bool HoldDepth,

        bool AllowReverse,

        double RiskLevel,

        string Reason
    )
    {
        public static ControlIntent Idle { get; } =
            new(
                Kind: ControlIntentKind.Idle,
                TargetPosition: Vec3.Zero,
                TargetHeadingDeg: 0.0,
                DesiredForwardSpeedMps: 0.0,
                DesiredDepthMeters: 0.0,
                DesiredAltitudeMeters: 0.0,
                HoldHeading: false,
                HoldDepth: false,
                AllowReverse: false,
                RiskLevel: 0.0,
                Reason: "IDLE");
    }
}