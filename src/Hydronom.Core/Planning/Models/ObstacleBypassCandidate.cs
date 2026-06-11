using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    public enum ObstacleBypassSide
    {
        Unknown = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// Tek bir obstacle için üretilmiş sağ/sol bypass adayı.
    ///
    /// Bu model sadece geometrik adayı temsil eder.
    /// Risk/skor hesaplama planner tarafında yapılır.
    /// </summary>
    public sealed record ObstacleBypassCandidate
    {
        public string ObstacleId { get; init; } = string.Empty;

        public ObstacleBypassSide Side { get; init; } = ObstacleBypassSide.Unknown;

        public Vec3 Point { get; init; }

        public Vec3 ObstacleCenter { get; init; }

        public Vec3 RouteDirection { get; init; }

        public Vec3 LateralDirection { get; init; }

        public double ProjectionT { get; init; }

        public double LateralOffsetMeters { get; init; }

        public double ForwardBiasMeters { get; init; }

        public double RequiredClearanceMeters { get; init; }

        public double SourcePhysicalClearanceMeters { get; init; }

        public double SourceSafetyClearanceMeters { get; init; }

        public double Severity { get; init; }

        public string Reason { get; init; } = string.Empty;

        public bool IsValid =>
            Side != ObstacleBypassSide.Unknown &&
            !string.IsNullOrWhiteSpace(ObstacleId) &&
            IsFinite(Point.X) &&
            IsFinite(Point.Y);

        public string Tag =>
            $"obstacle={ObstacleId}," +
            $"side={Side}," +
            $"mode=obstacle-bypass," +
            $"t={ProjectionT:F2}," +
            $"lat={LateralOffsetMeters:F2}," +
            $"fwd={ForwardBiasMeters:F2}," +
            $"pClear={SourcePhysicalClearanceMeters:F2}," +
            $"sClear={SourceSafetyClearanceMeters:F2}," +
            $"sev={Severity:F2}," +
            $"reason={Reason}";

        public string Summary =>
            $"BYPASS_CANDIDATE {Tag} point=({Point.X:F2},{Point.Y:F2})";

        public override string ToString()
            => Summary;

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}