using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public sealed partial class AdvancedAnalysis
    {
        private const double VehicleNominalWidthM = 1.00;
        private const double CorridorSafetyMarginEachSideM = 0.55;
        private const double MinUsefulCorridorWidthM = 2.00;
        private const double MaxGateLikeCorridorWidthM = 9.00;
        private const double MaxCorridorPairDistanceDeltaM = 8.00;
        private const double MinCorridorConfidence = 0.30;

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

            var consideredSamples = new List<ObstacleSample>();

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
                    consideredSamples.Add(sample);

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

            double desiredAngleDeg = ResolveDesiredHeadingOffsetDeg(
                ownPosition,
                headingRad,
                frame);

            var corridor = FindBestPassableCorridor(
                consideredSamples,
                parameters,
                desiredAngleDeg);

            return BuildReport(
                hasObstacleAhead,
                leftClear,
                rightClear,
                closestSurfaceDistance,
                totalObstacleCount,
                consideredObstacleCount,
                sectorRisk,
                sectorClearance,
                parameters,
                corridor);
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

        private static double ResolveDesiredHeadingOffsetDeg(
            Vec2 ownPosition,
            double headingRad,
            FusedFrame frame)
        {
            if (frame.Target is not Vec2 target)
                return 0.0;

            double dx = Sanitize(target.X) - Sanitize(ownPosition.X);
            double dy = Sanitize(target.Y) - Sanitize(ownPosition.Y);

            if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6)
                return 0.0;

            double targetAngle = Math.Atan2(dy, dx);
            double relativeDeg = NormalizeDeg(RadToDeg(targetAngle - headingRad));

            if (!double.IsFinite(relativeDeg))
                return 0.0;

            return relativeDeg;
        }

        private static PassableCorridorCandidate FindBestPassableCorridor(
            IReadOnlyList<ObstacleSample> samples,
            AnalysisParameters parameters,
            double desiredAngleDeg)
        {
            if (samples.Count < 2)
                return PassableCorridorCandidate.None;

            double requiredWidth = VehicleNominalWidthM + 2.0 * CorridorSafetyMarginEachSideM;
            requiredWidth = Math.Max(requiredWidth, MinUsefulCorridorWidthM);

            PassableCorridorCandidate best = PassableCorridorCandidate.None;
            double bestScore = double.MinValue;

            for (int i = 0; i < samples.Count; i++)
            {
                var a = samples[i];

                if (!IsCorridorCandidateObstacle(a, parameters))
                    continue;

                for (int j = i + 1; j < samples.Count; j++)
                {
                    var b = samples[j];

                    if (!IsCorridorCandidateObstacle(b, parameters))
                        continue;

                    if (!PairCanBracketDesiredDirection(a, b, desiredAngleDeg))
                        continue;

                    double distanceDelta = Math.Abs(a.CenterDistanceM - b.CenterDistanceM);
                    if (distanceDelta > MaxCorridorPairDistanceDeltaM)
                        continue;

                    double centerDistance = (a.CenterDistanceM + b.CenterDistanceM) * 0.5;
                    if (centerDistance > parameters.AheadDistanceM + 4.0)
                        continue;

                    double pairCenterDistance = DistanceBetweenObstacleCenters(a, b);
                    double corridorWidth = pairCenterDistance - a.RadiusM - b.RadiusM;

                    if (!double.IsFinite(corridorWidth))
                        continue;

                    if (corridorWidth < requiredWidth)
                        continue;

                    if (corridorWidth > MaxGateLikeCorridorWidthM)
                        continue;

                    double clearance = corridorWidth - requiredWidth;
                    double centerOffset = ComputeCorridorCenterOffsetDeg(a, b);

                    if (Math.Abs(centerOffset) > parameters.HalfFovDeg)
                        continue;

                    double targetAlignmentPenalty =
                        Math.Abs(NormalizeDeg(centerOffset - desiredAngleDeg)) /
                        Math.Max(1.0, parameters.HalfFovDeg);

                    double widthConfidence = Math.Clamp(clearance / Math.Max(1.0, requiredWidth), 0.0, 1.0);
                    double distanceConfidence = 1.0 - Math.Clamp(centerDistance / Math.Max(1.0, parameters.AheadDistanceM + 4.0), 0.0, 1.0);
                    double alignmentConfidence = 1.0 - Math.Clamp(targetAlignmentPenalty, 0.0, 1.0);

                    double confidence =
                        widthConfidence * 0.45 +
                        alignmentConfidence * 0.40 +
                        distanceConfidence * 0.15;

                    confidence = Math.Clamp(confidence, 0.0, 1.0);

                    if (confidence < MinCorridorConfidence)
                        continue;

                    double score =
                        confidence * 2.0 +
                        alignmentConfidence * 1.25 +
                        widthConfidence * 0.75 -
                        Math.Abs(centerOffset) / Math.Max(1.0, parameters.HalfFovDeg) * 0.25;

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    best = new PassableCorridorCandidate(
                        IsValid: true,
                        CenterOffsetDeg: centerOffset,
                        WidthMeters: corridorWidth,
                        ClearanceMeters: clearance,
                        Confidence: confidence);
                }
            }

            return best;
        }

        private static bool IsCorridorCandidateObstacle(
            ObstacleSample sample,
            AnalysisParameters parameters)
        {
            if (sample.CenterDistanceM <= 0.25)
                return false;

            if (sample.CenterDistanceM > parameters.AheadDistanceM + 4.0)
                return false;

            if (Math.Abs(sample.RelativeAngleDeg) > parameters.HalfFovDeg + 20.0)
                return false;

            return true;
        }

        private static bool PairCanBracketDesiredDirection(
            ObstacleSample a,
            ObstacleSample b,
            double desiredAngleDeg)
        {
            double minAngle = Math.Min(a.RelativeAngleDeg, b.RelativeAngleDeg);
            double maxAngle = Math.Max(a.RelativeAngleDeg, b.RelativeAngleDeg);

            if (desiredAngleDeg >= minAngle && desiredAngleDeg <= maxAngle)
                return true;

            double pairCenter = (a.RelativeAngleDeg + b.RelativeAngleDeg) * 0.5;
            double distanceFromPairCenter = Math.Abs(NormalizeDeg(desiredAngleDeg - pairCenter));
            double angularSpan = Math.Abs(maxAngle - minAngle);

            return distanceFromPairCenter <= Math.Max(6.0, angularSpan * 0.35);
        }

        private static double DistanceBetweenObstacleCenters(
            ObstacleSample a,
            ObstacleSample b)
        {
            double ax = a.CenterDistanceM * Math.Cos(DegToRad(a.RelativeAngleDeg));
            double ay = a.CenterDistanceM * Math.Sin(DegToRad(a.RelativeAngleDeg));

            double bx = b.CenterDistanceM * Math.Cos(DegToRad(b.RelativeAngleDeg));
            double by = b.CenterDistanceM * Math.Sin(DegToRad(b.RelativeAngleDeg));

            double dx = ax - bx;
            double dy = ay - by;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double ComputeCorridorCenterOffsetDeg(
            ObstacleSample a,
            ObstacleSample b)
        {
            double ax = a.CenterDistanceM * Math.Cos(DegToRad(a.RelativeAngleDeg));
            double ay = a.CenterDistanceM * Math.Sin(DegToRad(a.RelativeAngleDeg));

            double bx = b.CenterDistanceM * Math.Cos(DegToRad(b.RelativeAngleDeg));
            double by = b.CenterDistanceM * Math.Sin(DegToRad(b.RelativeAngleDeg));

            double cx = (ax + bx) * 0.5;
            double cy = (ay + by) * 0.5;

            if (Math.Abs(cx) < 1e-6 && Math.Abs(cy) < 1e-6)
                return 0.0;

            return NormalizeDeg(RadToDeg(Math.Atan2(cy, cx)));
        }
    }
}