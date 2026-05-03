using System;
using System.Diagnostics;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedTaskManager
    {
        private void ResetInternalState(bool clearLastIdentity)
        {
            _currentIndex = 0;
            _taskStartTicks = null;
            _waypointArriveTicks = null;
            _lastProgressDist = null;
            _lastProgressTicks = null;
            _waypointStartDistance = null;
            _obstacleSinceTicks = null;
            _completedWaypointCount = 0;
            LastStatusReason = null;

            if (clearLastIdentity)
            {
                _lastTaskName = null;
                _lastTarget = null;
            }
        }

        private void ResetProgressTracking()
        {
            _lastProgressDist = null;
            _lastProgressTicks = null;
            _waypointStartDistance = null;
        }

        private double EstimateCurrentDistance(VehicleState? state)
        {
            if (state is null || _routePoints.Count == 0 || _currentIndex < 0 || _currentIndex >= _routePoints.Count)
                return 0.0;

            return Distance3D(state.Value.Position, _routePoints[_currentIndex]);
        }

        private double EstimateFinalDistance(VehicleState? state)
        {
            if (state is null || _routePoints.Count == 0)
                return 0.0;

            return Distance3D(state.Value.Position, _routePoints[^1]);
        }

        private double ComputeEffectiveArrivalThreshold(double speedMps)
        {
            double speed = SafeNonNegative(speedMps, 0.0);
            double threshold = _arriveThresholdM + speed * _dynamicAcceptanceTauSeconds;
            return Math.Clamp(threshold, _arriveThresholdM, _maxArrivalThresholdM);
        }

        private static double ComputeSpeedMps(VehicleState? state)
        {
            if (state is null)
                return 0.0;

            var v = state.Value.Velocity;

            double s =
                Safe(v.X) * Safe(v.X) +
                Safe(v.Y) * Safe(v.Y) +
                Safe(v.Z) * Safe(v.Z);

            double speed = Math.Sqrt(s);
            return double.IsFinite(speed) ? speed : 0.0;
        }

        private static bool InferHoldFromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var n = name.Trim().ToLowerInvariant();
            return n.Contains("hold") || n.Contains("station") || n.Contains("keep");
        }

        private static double Distance3D(Vec3 a, Vec3 b)
        {
            var dx = Safe(a.X - b.X);
            var dy = Safe(a.Y - b.Y);
            var dz = Safe(a.Z - b.Z);

            var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return double.IsFinite(dist) ? dist : 0.0;
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                Safe(v.X),
                Safe(v.Y),
                Safe(v.Z)
            );
        }

        private static long NowTicks() => Stopwatch.GetTimestamp();

        private static double ElapsedSeconds(long startTicks, long nowTicks)
        {
            if (nowTicks <= startTicks)
                return 0.0;

            return (nowTicks - startTicks) / (double)Stopwatch.Frequency;
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafeNonNegative(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Max(0.0, value);
        }

        private static double ClampPositive(double value, double fallback, double min)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Max(min, value);
        }

        private static double ClampNonNegative(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Max(0.0, value);
        }
    }
}
