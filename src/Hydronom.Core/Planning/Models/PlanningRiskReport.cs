using System;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planlama sonucunun risk değerlendirmesidir.
    /// Bu model Ops tarafında "neden bu rota?" sorusunu açıklamak için de kullanılır.
    /// </summary>
    public sealed record PlanningRiskReport
    {
        public double RiskScore { get; init; }

        public double ObstacleRisk { get; init; }

        public double NoGoZoneRisk { get; init; }

        public double CorridorRisk { get; init; }

        public double DynamicUncertaintyRisk { get; init; }

        public double MinimumClearanceMeters { get; init; } = double.PositiveInfinity;

        public int BlockingObjectCount { get; init; }

        public int ConsideredObjectCount { get; init; }

        public bool RequiresReplan { get; init; }

        public bool RequiresSlowMode { get; init; }

        public string Summary { get; init; } = "CLEAR";

        public static PlanningRiskReport Clear { get; } = new()
        {
            RiskScore = 0.0,
            ObstacleRisk = 0.0,
            NoGoZoneRisk = 0.0,
            CorridorRisk = 0.0,
            DynamicUncertaintyRisk = 0.0,
            MinimumClearanceMeters = double.PositiveInfinity,
            BlockingObjectCount = 0,
            ConsideredObjectCount = 0,
            RequiresReplan = false,
            RequiresSlowMode = false,
            Summary = "CLEAR"
        };

        public PlanningRiskReport Sanitized()
        {
            var risk = Clamp01(RiskScore);
            var obstacle = Clamp01(ObstacleRisk);
            var noGo = Clamp01(NoGoZoneRisk);
            var corridor = Clamp01(CorridorRisk);
            var uncertainty = Clamp01(DynamicUncertaintyRisk);

            var maxRisk = Math.Max(risk, Math.Max(obstacle, Math.Max(noGo, Math.Max(corridor, uncertainty))));

            return this with
            {
                RiskScore = maxRisk,
                ObstacleRisk = obstacle,
                NoGoZoneRisk = noGo,
                CorridorRisk = corridor,
                DynamicUncertaintyRisk = uncertainty,
                MinimumClearanceMeters = double.IsFinite(MinimumClearanceMeters)
                    ? Math.Max(0.0, MinimumClearanceMeters)
                    : double.PositiveInfinity,
                BlockingObjectCount = Math.Max(0, BlockingObjectCount),
                ConsideredObjectCount = Math.Max(0, ConsideredObjectCount),
                RequiresReplan = RequiresReplan || maxRisk >= 0.75,
                RequiresSlowMode = RequiresSlowMode || maxRisk >= 0.45,
                Summary = string.IsNullOrWhiteSpace(Summary) ? "CLEAR" : Summary.Trim()
            };
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }
    }
}