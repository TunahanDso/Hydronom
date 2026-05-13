using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Global/Local planner tarafından üretilen soyut rota sonucudur.
    ///
    /// Bu model:
    /// - world-space path noktalarını,
    /// - risk raporunu,
    /// - açıklanabilir plan özetini,
    /// - replan ihtiyacını
    /// taşır.
    /// </summary>
    public sealed record PlannedPath
    {
        public string PlanId { get; init; } = Guid.NewGuid().ToString("N");

        public PlanningMode Mode { get; init; } = PlanningMode.Navigate;

        public PlanningGoal Goal { get; init; } = PlanningGoal.Idle;

        public IReadOnlyList<PlannedPathPoint> Points { get; init; } =
            Array.Empty<PlannedPathPoint>();

        public PlanningRiskReport Risk { get; init; } = PlanningRiskReport.Clear;

        public bool IsValid { get; init; } = true;

        public bool RequiresReplan { get; init; }

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        public string Source { get; init; } = "planner";

        public string Summary { get; init; } = "PLAN";

        public static PlannedPath Empty { get; } = new()
        {
            PlanId = "empty",
            Mode = PlanningMode.Idle,
            Goal = PlanningGoal.Idle,
            Points = Array.Empty<PlannedPathPoint>(),
            Risk = PlanningRiskReport.Clear,
            IsValid = false,
            RequiresReplan = false,
            Source = "system",
            Summary = "EMPTY"
        };

        public PlannedPath Sanitized()
        {
            var points = NormalizePoints(Points);
            var risk = (Risk ?? PlanningRiskReport.Clear).Sanitized();

            return this with
            {
                PlanId = string.IsNullOrWhiteSpace(PlanId) ? Guid.NewGuid().ToString("N") : PlanId.Trim(),
                Goal = (Goal ?? PlanningGoal.Idle).Sanitized(),
                Points = points,
                Risk = risk,
                IsValid = IsValid && points.Count > 0,
                RequiresReplan = RequiresReplan || risk.RequiresReplan,
                CreatedUtc = CreatedUtc == default ? DateTime.UtcNow : CreatedUtc,
                Source = string.IsNullOrWhiteSpace(Source) ? "planner" : Source.Trim(),
                Summary = string.IsNullOrWhiteSpace(Summary) ? "PLAN" : Summary.Trim()
            };
        }

        public PlannedPathPoint? FirstPoint =>
            Points.Count > 0 ? Points[0] : null;

        public PlannedPathPoint? LastPoint =>
            Points.Count > 0 ? Points[^1] : null;

        public double ApproxLengthMeters()
        {
            var points = Points;

            if (points.Count < 2)
                return 0.0;

            double total = 0.0;

            for (var i = 1; i < points.Count; i++)
            {
                total += Distance(points[i - 1].Position, points[i].Position);
            }

            return total;
        }

        private static IReadOnlyList<PlannedPathPoint> NormalizePoints(
            IReadOnlyList<PlannedPathPoint>? points)
        {
            if (points is null || points.Count == 0)
                return Array.Empty<PlannedPathPoint>();

            return points
                .Where(x => x is not null)
                .Select(x => x.Sanitized())
                .ToArray();
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}