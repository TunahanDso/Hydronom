using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public sealed partial class AdvancedAnalysis
    {
        private static AdvancedAnalysisReport AnalyzeObstacles(
            FusedFrame frame,
            AnalysisParameters parameters)
        {
            double headingDeg = Sanitize(frame.HeadingDeg);
            double headingRad = DegToRad(headingDeg);

            Vec2 ownPosition = frame.Position;

            double[] sectorRisk = new double[parameters.SectorCount];
            double[] sectorClearance = new double[parameters.SectorCount];

            for (int i = 0; i < sectorClearance.Length; i++)
                sectorClearance[i] = parameters.AheadDistanceM;

            bool hasObstacleAhead = false;
            double leftClear = parameters.AheadDistanceM;
            double rightClear = parameters.AheadDistanceM;

            int totalObstacleCount = 0;
            int consideredObstacleCount = 0;
            double closestSurfaceDistance = parameters.AheadDistanceM;

            if (frame.Obstacles is not null)
            {
                foreach (var obstacle in frame.Obstacles)
                {
                    totalObstacleCount++;

                    if (!TryBuildObstacleSample(
                            ownPosition,
                            headingRad,
                            obstacle,
                            parameters.SafetyMarginM,
                            out var sample))
                    {
                        continue;
                    }

                    consideredObstacleCount++;
                    closestSurfaceDistance = Math.Min(closestSurfaceDistance, sample.SurfaceDistanceM);

                    bool inFrontCone = Math.Abs(sample.RelativeAngleDeg) <= parameters.HalfFovDeg;
                    bool inRange = sample.CenterDistanceM <= parameters.AheadDistanceM + sample.RadiusM + parameters.SafetyMarginM;

                    if (inFrontCone && inRange)
                        hasObstacleAhead = true;

                    if (sample.RelativeAngleDeg < 0.0 &&
                        Math.Abs(sample.RelativeAngleDeg) <= parameters.SideWindowDeg)
                    {
                        leftClear = Math.Min(leftClear, sample.SurfaceDistanceM);
                    }

                    if (sample.RelativeAngleDeg >= 0.0 &&
                        Math.Abs(sample.RelativeAngleDeg) <= parameters.SideWindowDeg)
                    {
                        rightClear = Math.Min(rightClear, sample.SurfaceDistanceM);
                    }

                    AccumulateSectorRisk(
                        sample,
                        sectorRisk,
                        sectorClearance,
                        parameters.SectorCount,
                        parameters.AheadDistanceM,
                        parameters.HalfFovDeg,
                        parameters.FrontWeight,
                        parameters.SizeWeight);
                }
            }

            return BuildReport(
                hasObstacleAhead,
                leftClear,
                rightClear,
                closestSurfaceDistance,
                totalObstacleCount,
                consideredObstacleCount,
                sectorRisk,
                sectorClearance,
                parameters);
        }

        private static bool TryBuildObstacleSample(
            Vec2 ownPosition,
            double headingRad,
            Obstacle obstacle,
            double safetyMarginM,
            out ObstacleSample sample)
        {
            sample = default;

            double ox = Sanitize(obstacle.Position.X);
            double oy = Sanitize(obstacle.Position.Y);
            double radius = Math.Max(0.0, Sanitize(obstacle.RadiusM));

            double dx = ox - Sanitize(ownPosition.X);
            double dy = oy - Sanitize(ownPosition.Y);

            double centerDist = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(centerDist))
                return false;

            if (centerDist < 1e-6)
                centerDist = 1e-6;

            double surfaceDist = Math.Max(0.0, centerDist - radius - safetyMarginM);

            double absAngle = Math.Atan2(dy, dx);
            double relAngleDeg = NormalizeDeg(RadToDeg(absAngle - headingRad));

            double angularRadiusDeg;
            if (centerDist > radius)
            {
                double ratio = Math.Clamp((radius + safetyMarginM) / centerDist, 0.0, 1.0);
                angularRadiusDeg = RadToDeg(Math.Asin(ratio));
            }
            else
            {
                angularRadiusDeg = 90.0;
            }

            if (!double.IsFinite(angularRadiusDeg))
                angularRadiusDeg = 90.0;

            sample = new ObstacleSample(
                CenterDistanceM: centerDist,
                SurfaceDistanceM: surfaceDist,
                RelativeAngleDeg: relAngleDeg,
                AngularRadiusDeg: angularRadiusDeg,
                RadiusM: radius
            );

            return true;
        }
    }
}