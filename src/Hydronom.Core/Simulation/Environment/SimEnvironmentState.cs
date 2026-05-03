using System;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// Hydronom simÃ¼lasyon dÃ¼nyasÄ±nÄ±n Ã§evresel durum modeli.
    ///
    /// Bu model sadece gÃ¶rsellik iÃ§in deÄŸildir.
    /// Ä°leride:
    /// - physics loads
    /// - drag / buoyancy / current
    /// - yelkenli rÃ¼zgar davranÄ±ÅŸÄ±
    /// - sualtÄ± GPS yokluÄŸu / sonar-DVL etkisi
    /// - kamera gÃ¶rÃ¼nÃ¼rlÃ¼k confidence
    /// - gÃ¶rev risk skoru
    /// - Ops world overlay
    /// katmanlarÄ±nÄ± besleyecektir.
    /// </summary>
    public readonly record struct SimEnvironmentState(
        string EnvironmentId,
        DateTime TimestampUtc,
        SimMediumKind Medium,
        SimWaterState Water,
        SimWindState Wind,
        SimCurrentState Current,
        SimWeatherState Weather,
        SimTerrainState Terrain,
        SimVisibilityState Visibility,
        SimVector3 GravityWorld,
        string Summary
    )
    {
        public static SimEnvironmentState DefaultMarine => new(
            EnvironmentId: "marine_default",
            TimestampUtc: DateTime.UtcNow,
            Medium: SimMediumKind.SurfaceWater,
            Water: SimWaterState.DefaultSea,
            Wind: SimWindState.Calm,
            Current: SimCurrentState.None,
            Weather: SimWeatherState.Clear,
            Terrain: SimTerrainState.FlatGround,
            Visibility: SimVisibilityState.Clear,
            GravityWorld: new SimVector3(0.0, 0.0, -9.80665),
            Summary: "Default marine simulation environment."
        );

        public static SimEnvironmentState DefaultGround => new(
            EnvironmentId: "ground_default",
            TimestampUtc: DateTime.UtcNow,
            Medium: SimMediumKind.Ground,
            Water: SimWaterState.None,
            Wind: SimWindState.Calm,
            Current: SimCurrentState.None,
            Weather: SimWeatherState.Clear,
            Terrain: SimTerrainState.FlatGround,
            Visibility: SimVisibilityState.Clear,
            GravityWorld: new SimVector3(0.0, 0.0, -9.80665),
            Summary: "Default ground simulation environment."
        );

        public static SimEnvironmentState DefaultAir => new(
            EnvironmentId: "air_default",
            TimestampUtc: DateTime.UtcNow,
            Medium: SimMediumKind.Air,
            Water: SimWaterState.None,
            Wind: SimWindState.Calm,
            Current: SimCurrentState.None,
            Weather: SimWeatherState.Clear,
            Terrain: SimTerrainState.FlatGround,
            Visibility: SimVisibilityState.Clear,
            GravityWorld: new SimVector3(0.0, 0.0, -9.80665),
            Summary: "Default air simulation environment."
        );

        public SimEnvironmentState Sanitized()
        {
            return new SimEnvironmentState(
                EnvironmentId: Normalize(EnvironmentId, "environment"),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Medium: Medium,
                Water: Water.Sanitized(),
                Wind: Wind.Sanitized(),
                Current: Current.Sanitized(),
                Weather: Weather.Sanitized(),
                Terrain: Terrain.Sanitized(),
                Visibility: Visibility.Sanitized(),
                GravityWorld: SanitizeGravity(GravityWorld),
                Summary: Normalize(Summary, "Simulation environment.")
            );
        }

        public SimVector3 GetFluidVelocityAt(SimVector3 worldPosition)
        {
            var safe = Sanitized();
            var p = worldPosition.Sanitized();

            if (safe.Medium == SimMediumKind.SurfaceWater ||
                safe.Medium == SimMediumKind.Underwater ||
                safe.Water.IsPointUnderwater(p))
            {
                return safe.Current.GetVelocityAtDepth(p.Z);
            }

            if (safe.Medium == SimMediumKind.Air)
                return safe.Wind.VelocityWorld;

            return SimVector3.Zero;
        }

        public bool IsUnderwater(SimVector3 worldPosition)
        {
            return Water.Sanitized().IsPointUnderwater(worldPosition);
        }

        private static SimVector3 SanitizeGravity(SimVector3 gravity)
        {
            var safe = gravity.Sanitized();

            if (safe.Length < 1e-12)
                return new SimVector3(0.0, 0.0, -9.80665);

            return safe;
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
