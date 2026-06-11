using System;
using Hydronom.Core.Domain;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planner'ın kullanacağı normalize edilmiş world-object temsili.
    ///
    /// HydronomWorldObject ham veri modelidir.
    /// PlanningObstacle ise "bu obje rota planlamasında ne ifade ediyor?"
    /// sorusunun cevabıdır.
    /// </summary>
    public sealed record PlanningObstacle
    {
        public string Id { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Layer { get; init; } = string.Empty;

        public Vec3 Position { get; init; }

        public double RadiusMeters { get; init; }

        public double InflatedRadiusMeters { get; init; }

        public double VehicleRadiusMeters { get; init; }

        public double SafetyMarginMeters { get; init; }

        public bool IsActive { get; init; }

        public bool IsPhysicalObstacle { get; init; }

        public bool IsBlockingForRoute { get; init; }

        public bool IsPassableMarker { get; init; }

        public bool IsBoundaryLike { get; init; }

        public bool IsCorridorMarker { get; init; }

        public bool IsGateMarker { get; init; }

        public bool IsNoGoZone { get; init; }

        public bool IsHintMarker { get; init; }

        public bool IsUnknownPhysicalObject { get; init; }

        public double PlanningWeight { get; init; } = 1.0;

        public string Source { get; init; } = "unknown";

        public HydronomWorldObject Raw { get; init; } = new();

        public string SemanticSummary =>
            $"id={Id} kind={Kind} physical={IsPhysicalObstacle} blocking={IsBlockingForRoute} " +
            $"gate={IsGateMarker} corridor={IsCorridorMarker} boundary={IsBoundaryLike} " +
            $"r={RadiusMeters:F2} inflated={InflatedRadiusMeters:F2}";

        public bool CanBlockRoute =>
            IsActive &&
            IsBlockingForRoute &&
            IsPhysicalObstacle &&
            !IsPassableMarker;

        public static PlanningObstacle Empty { get; } = new()
        {
            Id = string.Empty,
            Kind = string.Empty,
            Name = string.Empty,
            Layer = string.Empty,
            Position = Vec3.Zero,
            RadiusMeters = 0.0,
            InflatedRadiusMeters = 0.0,
            VehicleRadiusMeters = 0.0,
            SafetyMarginMeters = 0.0,
            IsActive = false,
            IsPhysicalObstacle = false,
            IsBlockingForRoute = false,
            IsPassableMarker = false,
            IsBoundaryLike = false,
            IsCorridorMarker = false,
            IsGateMarker = false,
            IsNoGoZone = false,
            IsHintMarker = false,
            IsUnknownPhysicalObject = false,
            PlanningWeight = 0.0,
            Source = "empty",
            Raw = new HydronomWorldObject()
        };

        public override string ToString()
            => SemanticSummary;
    }
}