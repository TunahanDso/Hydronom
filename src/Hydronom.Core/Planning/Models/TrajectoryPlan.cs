using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Controller katmanına verilecek uygulanabilir hareket planıdır.
    ///
    /// Bu model ileride ControlIntent'e doğrudan bağlanacak.
    /// Şimdilik planlanan path, lookahead noktası ve hız/heading referanslarını taşır.
    /// </summary>
    public sealed record TrajectoryPlan
    {
        public string TrajectoryId { get; init; } = Guid.NewGuid().ToString("N");

        public PlanningMode Mode { get; init; } = PlanningMode.Navigate;

        public PlannedPath SourcePath { get; init; } = PlannedPath.Empty;

        public IReadOnlyList<TrajectoryPoint> Points { get; init; } =
            Array.Empty<TrajectoryPoint>();

        public TrajectoryPoint? LookAheadPoint { get; init; }

        public PlanningRiskReport Risk { get; init; } = PlanningRiskReport.Clear;

        public bool IsValid { get; init; } = true;

        public bool RequiresReplan { get; init; }

        public bool RequiresSlowMode { get; init; }

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        public string Source { get; init; } = "trajectory";

        public string Summary { get; init; } = "TRAJECTORY";

        public static TrajectoryPlan Empty { get; } = new()
        {
            TrajectoryId = "empty",
            Mode = PlanningMode.Idle,
            SourcePath = PlannedPath.Empty,
            Points = Array.Empty<TrajectoryPoint>(),
            LookAheadPoint = null,
            Risk = PlanningRiskReport.Clear,
            IsValid = false,
            RequiresReplan = false,
            RequiresSlowMode = false,
            Source = "system",
            Summary = "EMPTY"
        };

        public TrajectoryPlan Sanitized()
        {
            var path = (SourcePath ?? PlannedPath.Empty).Sanitized();
            var points = NormalizePoints(Points);
            var risk = (Risk ?? PlanningRiskReport.Clear).Sanitized();

            var lookAhead = LookAheadPoint?.Sanitized();

            if (lookAhead is null && points.Count > 0)
                lookAhead = points[0];

            return this with
            {
                TrajectoryId = string.IsNullOrWhiteSpace(TrajectoryId)
                    ? Guid.NewGuid().ToString("N")
                    : TrajectoryId.Trim(),
                SourcePath = path,
                Points = points,
                LookAheadPoint = lookAhead,
                Risk = risk,
                IsValid = IsValid && path.IsValid && points.Count > 0,
                RequiresReplan = RequiresReplan || path.RequiresReplan || risk.RequiresReplan,
                RequiresSlowMode = RequiresSlowMode || risk.RequiresSlowMode,
                CreatedUtc = CreatedUtc == default ? DateTime.UtcNow : CreatedUtc,
                Source = string.IsNullOrWhiteSpace(Source) ? "trajectory" : Source.Trim(),
                Summary = string.IsNullOrWhiteSpace(Summary) ? "TRAJECTORY" : Summary.Trim()
            };
        }

        public TrajectoryPoint? FirstPoint =>
            Points.Count > 0 ? Points[0] : null;

        public TrajectoryPoint? LastPoint =>
            Points.Count > 0 ? Points[^1] : null;

        public ControlReference ToControlReference(Vec3 fallbackTarget)
        {
            var safe = Sanitized();

            var lookAhead = safe.LookAheadPoint;

            if (lookAhead is null)
            {
                return new ControlReference(
                    TargetPosition: fallbackTarget.Sanitized(),
                    TargetHeadingDeg: 0.0,
                    DesiredSpeedMps: 0.0,
                    RiskScore: 1.0,
                    RequiresHeadingAlignment: true,
                    RequiresSlowMode: true,
                    Reason: "EMPTY_TRAJECTORY_REFERENCE"
                );
            }

            return new ControlReference(
                TargetPosition: lookAhead.Position,
                TargetHeadingDeg: lookAhead.HeadingDeg,
                DesiredSpeedMps: lookAhead.DesiredSpeedMps,
                RiskScore: Math.Max(safe.Risk.RiskScore, lookAhead.RiskScore),
                RequiresHeadingAlignment: lookAhead.RequiresHeadingAlignment,
                RequiresSlowMode: safe.RequiresSlowMode || lookAhead.RequiresSlowMode,
                Reason: lookAhead.Reason
            );
        }

        private static IReadOnlyList<TrajectoryPoint> NormalizePoints(
            IReadOnlyList<TrajectoryPoint>? points)
        {
            if (points is null || points.Count == 0)
                return Array.Empty<TrajectoryPoint>();

            return points
                .Where(x => x is not null)
                .Select(x => x.Sanitized())
                .ToArray();
        }
    }

    /// <summary>
    /// TrajectoryPlan'dan ControlIntent'e taşınacak sade referans modelidir.
    ///
    /// Not: Bunu ayrı dosyaya bölmek ileride mümkün; şimdilik trajectory sözleşmesinin
    /// yanında tutuyoruz çünkü controller köprüsünde doğrudan kullanılacak.
    /// </summary>
    public sealed record ControlReference(
        Vec3 TargetPosition,
        double TargetHeadingDeg,
        double DesiredSpeedMps,
        double RiskScore,
        bool RequiresHeadingAlignment,
        bool RequiresSlowMode,
        string Reason
    );
}