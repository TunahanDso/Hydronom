using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.World.Diagnostics;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planner'ın tek karar anında gördüğü dünya, araç ve görev bağlamıdır.
    ///
    /// Runtime tarafı kendi canlı RuntimeWorldModel'inden snapshot alıp
    /// bu modele dönüştürür. Böylece planner runtime store'a bağımlı olmaz.
    /// </summary>
    public sealed record PlanningContext
    {
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        public string VehicleId { get; init; } = "vehicle";

        public VehicleState VehicleState { get; init; } = VehicleState.Zero;

        public PlanningGoal Goal { get; init; } = PlanningGoal.Idle;

        public IReadOnlyList<HydronomWorldObject> WorldObjects { get; init; } =
            Array.Empty<HydronomWorldObject>();

        public WorldDiagnostics? Diagnostics { get; init; }

        public double LookAheadMeters { get; init; } = 12.0;

        public double SafetyMarginMeters { get; init; } = 1.25;

        public double VehicleRadiusMeters { get; init; } = 0.75;

        public double MaxPlanSpeedMps { get; init; } = 2.0;

        public double MaxTurnRateDegPerSec { get; init; } = 90.0;

        public string Source { get; init; } = "runtime";

        public static PlanningContext Idle { get; } = new();

        public PlanningContext Sanitized()
        {
            return this with
            {
                TimestampUtc = TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                VehicleId = Normalize(VehicleId, "vehicle"),
                VehicleState = VehicleState.Sanitized(),
                Goal = (Goal ?? PlanningGoal.Idle).Sanitized(),
                WorldObjects = NormalizeWorldObjects(WorldObjects),
                LookAheadMeters = SafePositive(LookAheadMeters, 12.0),
                SafetyMarginMeters = SafeNonNegative(SafetyMarginMeters, 1.25),
                VehicleRadiusMeters = SafePositive(VehicleRadiusMeters, 0.75),
                MaxPlanSpeedMps = SafePositive(MaxPlanSpeedMps, 2.0),
                MaxTurnRateDegPerSec = SafePositive(MaxTurnRateDegPerSec, 90.0),
                Source = Normalize(Source, "runtime")
            };
        }

        public IReadOnlyList<HydronomWorldObject> BlockingObjects()
        {
            return WorldObjects
                .Where(x => x.IsActive && x.IsObstacleLike)
                .ToArray();
        }

        private static IReadOnlyList<HydronomWorldObject> NormalizeWorldObjects(
            IReadOnlyList<HydronomWorldObject>? objects)
        {
            if (objects is null || objects.Count == 0)
                return Array.Empty<HydronomWorldObject>();

            return objects
                .Where(x => x is not null)
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double SafePositive(double value, double fallback)
        {
            return double.IsFinite(value) && value > 0.0 ? value : fallback;
        }

        private static double SafeNonNegative(double value, double fallback)
        {
            return double.IsFinite(value) && value >= 0.0 ? value : fallback;
        }
    }
}