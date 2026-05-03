using System;
using System.Collections.Generic;

namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// VehicleState authority kabul/red kurallarÄ±.
    ///
    /// Bu policy, Hydronom'un ana state'inin rastgele dÄ±ÅŸ kaynaklarla ezilmesini engeller.
    /// </summary>
    public sealed record StateAuthorityPolicy
    {
        public StateAuthorityMode Mode { get; init; } = StateAuthorityMode.CSharpPrimary;

        public double MaxStateAgeMs { get; init; } = 1_000.0;

        public double MinConfidence { get; init; } = 0.10;

        public double MaxTeleportDistanceMeters { get; init; } = 25.0;

        public double MaxPlausibleSpeedMps { get; init; } = 25.0;

        public double MaxPlausibleYawRateDegSec { get; init; } = 180.0;

        public bool RequireFrameMatch { get; init; } = true;

        public bool AllowPhysicsTruthAsAuthorityInSimulation { get; init; } = true;

        public bool AllowPythonBackupAuthority { get; init; } = false;

        public IReadOnlySet<VehicleStateSourceKind> ExplicitAllowedSources { get; init; } =
            new HashSet<VehicleStateSourceKind>
            {
                VehicleStateSourceKind.CSharpFusion,
                VehicleStateSourceKind.CSharpEstimator
            };

        public static StateAuthorityPolicy CSharpPrimary => new()
        {
            Mode = StateAuthorityMode.CSharpPrimary,
            AllowPythonBackupAuthority = false,
            ExplicitAllowedSources = new HashSet<VehicleStateSourceKind>
            {
                VehicleStateSourceKind.CSharpFusion,
                VehicleStateSourceKind.CSharpEstimator
            }
        };

        public static StateAuthorityPolicy Simulation => new()
        {
            Mode = StateAuthorityMode.Simulation,
            AllowPhysicsTruthAsAuthorityInSimulation = true,
            ExplicitAllowedSources = new HashSet<VehicleStateSourceKind>
            {
                VehicleStateSourceKind.CSharpFusion,
                VehicleStateSourceKind.CSharpEstimator,
                VehicleStateSourceKind.PhysicsTruth
            }
        };

        public static StateAuthorityPolicy Replay => new()
        {
            Mode = StateAuthorityMode.Replay,
            ExplicitAllowedSources = new HashSet<VehicleStateSourceKind>
            {
                VehicleStateSourceKind.ReplayEstimate,
                VehicleStateSourceKind.CSharpFusion,
                VehicleStateSourceKind.CSharpEstimator
            }
        };

        public StateAuthorityPolicy Sanitized()
        {
            return this with
            {
                MaxStateAgeMs = SafePositive(MaxStateAgeMs, 1_000.0),
                MinConfidence = Clamp(MinConfidence, 0.0, 1.0),
                MaxTeleportDistanceMeters = SafePositive(MaxTeleportDistanceMeters, 25.0),
                MaxPlausibleSpeedMps = SafePositive(MaxPlausibleSpeedMps, 25.0),
                MaxPlausibleYawRateDegSec = SafePositive(MaxPlausibleYawRateDegSec, 180.0),
                ExplicitAllowedSources = ExplicitAllowedSources ?? new HashSet<VehicleStateSourceKind>()
            };
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
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
    }
}
