using System;
using Hydronom.Core.Simulation.Environment;

namespace Hydronom.Core.Telemetry.World
{
    /// <summary>
    /// Ops/Gateway/Ground Station iÃ§in Ã§evresel durum telemetry modeli.
    ///
    /// Bu model rÃ¼zgar, akÄ±ntÄ±, su, hava, zemin ve gÃ¶rÃ¼ÅŸ bilgilerini UI tarafÄ±na taÅŸÄ±r.
    /// </summary>
    public readonly record struct WorldEnvironmentTelemetry(
        string EnvironmentId,
        DateTime TimestampUtc,
        string Medium,
        bool WaterEnabled,
        double WaterLevelZ,
        double WaterDensityKgM3,
        double WaterTemperatureC,
        double WaterSalinityPsu,
        double WaterTurbidityNtu,
        double WaveHeightMeters,
        double WaveDirectionDeg,
        bool WindEnabled,
        double WindVx,
        double WindVy,
        double WindVz,
        double WindSpeedMps,
        double WindDirectionDeg,
        bool CurrentEnabled,
        double CurrentVx,
        double CurrentVy,
        double CurrentVz,
        double CurrentSpeedMps,
        double CurrentDirectionDeg,
        bool WeatherEnabled,
        double AirTemperatureC,
        double PressureHPa,
        double HumidityPercent,
        double RainIntensity,
        double FogDensity,
        double Cloudiness,
        double StormRisk,
        bool TerrainEnabled,
        string TerrainKind,
        double TerrainRoughness,
        double TerrainSlopeDeg,
        double TerrainFriction,
        bool VisibilityEnabled,
        double VisibilityMeters,
        double LightLevel,
        double UnderwaterVisibilityMeters,
        double OpticalClarity,
        double SensorOcclusionRisk,
        double GravityX,
        double GravityY,
        double GravityZ,
        string Summary
    )
    {
        public static WorldEnvironmentTelemetry FromEnvironment(SimEnvironmentState environment)
        {
            var safe = environment.Sanitized();

            return new WorldEnvironmentTelemetry(
                EnvironmentId: safe.EnvironmentId,
                TimestampUtc: safe.TimestampUtc,
                Medium: safe.Medium.ToString(),
                WaterEnabled: safe.Water.Enabled,
                WaterLevelZ: safe.Water.WaterLevelZ,
                WaterDensityKgM3: safe.Water.DensityKgM3,
                WaterTemperatureC: safe.Water.TemperatureC,
                WaterSalinityPsu: safe.Water.SalinityPsu,
                WaterTurbidityNtu: safe.Water.TurbidityNtu,
                WaveHeightMeters: safe.Water.WaveHeightMeters,
                WaveDirectionDeg: safe.Water.WaveDirectionDeg,
                WindEnabled: safe.Wind.Enabled,
                WindVx: safe.Wind.VelocityWorld.X,
                WindVy: safe.Wind.VelocityWorld.Y,
                WindVz: safe.Wind.VelocityWorld.Z,
                WindSpeedMps: safe.Wind.SpeedMps,
                WindDirectionDeg: safe.Wind.DirectionDeg,
                CurrentEnabled: safe.Current.Enabled,
                CurrentVx: safe.Current.VelocityWorld.X,
                CurrentVy: safe.Current.VelocityWorld.Y,
                CurrentVz: safe.Current.VelocityWorld.Z,
                CurrentSpeedMps: safe.Current.SpeedMps,
                CurrentDirectionDeg: safe.Current.DirectionDeg,
                WeatherEnabled: safe.Weather.Enabled,
                AirTemperatureC: safe.Weather.AirTemperatureC,
                PressureHPa: safe.Weather.PressureHPa,
                HumidityPercent: safe.Weather.HumidityPercent,
                RainIntensity: safe.Weather.RainIntensity,
                FogDensity: safe.Weather.FogDensity,
                Cloudiness: safe.Weather.Cloudiness,
                StormRisk: safe.Weather.StormRisk,
                TerrainEnabled: safe.Terrain.Enabled,
                TerrainKind: safe.Terrain.TerrainKind,
                TerrainRoughness: safe.Terrain.Roughness,
                TerrainSlopeDeg: safe.Terrain.SlopeDeg,
                TerrainFriction: safe.Terrain.Friction,
                VisibilityEnabled: safe.Visibility.Enabled,
                VisibilityMeters: safe.Visibility.VisibilityMeters,
                LightLevel: safe.Visibility.LightLevel,
                UnderwaterVisibilityMeters: safe.Visibility.UnderwaterVisibilityMeters,
                OpticalClarity: safe.Visibility.OpticalClarity,
                SensorOcclusionRisk: safe.Visibility.SensorOcclusionRisk,
                GravityX: safe.GravityWorld.X,
                GravityY: safe.GravityWorld.Y,
                GravityZ: safe.GravityWorld.Z,
                Summary: safe.Summary
            ).Sanitized();
        }

        public WorldEnvironmentTelemetry Sanitized()
        {
            return this with
            {
                EnvironmentId = Normalize(EnvironmentId, "environment"),
                TimestampUtc = TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Medium = Normalize(Medium, "Unknown"),
                WaterLevelZ = Safe(WaterLevelZ),
                WaterDensityKgM3 = SafePositive(WaterDensityKgM3, 997.0),
                WaterTemperatureC = Safe(WaterTemperatureC, 20.0),
                WaterSalinityPsu = SafeNonNegative(WaterSalinityPsu),
                WaterTurbidityNtu = SafeNonNegative(WaterTurbidityNtu),
                WaveHeightMeters = SafeNonNegative(WaveHeightMeters),
                WaveDirectionDeg = NormalizeDeg(WaveDirectionDeg),
                WindVx = Safe(WindVx),
                WindVy = Safe(WindVy),
                WindVz = Safe(WindVz),
                WindSpeedMps = SafeNonNegative(WindSpeedMps),
                WindDirectionDeg = NormalizeDeg(WindDirectionDeg),
                CurrentVx = Safe(CurrentVx),
                CurrentVy = Safe(CurrentVy),
                CurrentVz = Safe(CurrentVz),
                CurrentSpeedMps = SafeNonNegative(CurrentSpeedMps),
                CurrentDirectionDeg = NormalizeDeg(CurrentDirectionDeg),
                AirTemperatureC = Safe(AirTemperatureC, 20.0),
                PressureHPa = SafePositive(PressureHPa, 1013.25),
                HumidityPercent = Clamp(HumidityPercent, 0.0, 100.0),
                RainIntensity = Clamp01(RainIntensity),
                FogDensity = Clamp01(FogDensity),
                Cloudiness = Clamp01(Cloudiness),
                StormRisk = Clamp01(StormRisk),
                TerrainKind = Normalize(TerrainKind, "generic"),
                TerrainRoughness = Clamp01(TerrainRoughness),
                TerrainSlopeDeg = Clamp(TerrainSlopeDeg, -90.0, 90.0),
                TerrainFriction = Clamp01(TerrainFriction),
                VisibilityMeters = SafeNonNegative(VisibilityMeters),
                LightLevel = Clamp01(LightLevel),
                UnderwaterVisibilityMeters = SafeNonNegative(UnderwaterVisibilityMeters),
                OpticalClarity = Clamp01(OpticalClarity),
                SensorOcclusionRisk = Clamp01(SensorOcclusionRisk),
                GravityX = Safe(GravityX),
                GravityY = Safe(GravityY),
                GravityZ = Safe(GravityZ, -9.80665),
                Summary = Normalize(Summary, "Environment telemetry.")
            };
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double Safe(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }

        private static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return min;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }
    }
}
