using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Start-target hattını gerçekten etkileyen kritik route obstacle.
    ///
    /// Bu model "dünya objesi var" bilgisinden daha fazlasıdır:
    /// objenin route'a izdüşümü, clearance değeri, severity ve bypass offset ihtiyacını taşır.
    /// </summary>
    public sealed record CriticalRouteObstacle
    {
        public PlanningObstacle Obstacle { get; init; } = PlanningObstacle.Empty;

        public string ObstacleId => Obstacle.Id;

        public Vec3 Center => Obstacle.Position;

        /// <summary>
        /// Obstacle merkezinin start-target segmentine normalize izdüşümü.
        /// 0 = start, 1 = target.
        /// </summary>
        public double ProjectionT { get; init; }

        public double ClampedProjectionT { get; init; }

        public Vec3 ClosestPointOnRoute { get; init; }

        public double DistanceToRouteMeters { get; init; }

        /// <summary>
        /// Obstacle radius ve vehicle radius çıkarıldıktan sonra fiziksel kalan açıklık.
        /// Negatifse gerçek çakışma vardır.
        /// </summary>
        public double PhysicalClearanceMeters { get; init; }

        /// <summary>
        /// Physical clearance üstünden safety margin de çıkarılmış değer.
        /// Negatifse güvenlik bandı ihlal edilmiştir.
        /// </summary>
        public double SafetyClearanceMeters { get; init; }

        public double RequiredPhysicalClearanceMeters { get; init; }

        public double RecommendedMinOffsetMeters { get; init; }

        public double Severity { get; init; }

        public bool IsAhead { get; init; }

        public bool IsWithinRouteWindow { get; init; }

        public bool IsBlocking { get; init; }

        public bool IsCollision { get; init; }

        public bool IsSafetyViolation { get; init; }

        public string Reason { get; init; } = string.Empty;

        public bool RequiresBypass =>
            IsBlocking ||
            IsCollision ||
            IsSafetyViolation;

        public string Summary =>
            $"critical={ObstacleId} t={ProjectionT:F2} distRoute={DistanceToRouteMeters:F2} " +
            $"pClear={PhysicalClearanceMeters:F2} sClear={SafetyClearanceMeters:F2} " +
            $"req={RequiredPhysicalClearanceMeters:F2} severity={Severity:F2} " +
            $"bypass={RequiresBypass} reason={Reason}";

        public override string ToString()
            => Summary;
    }
}