using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Abstractions;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// World-aware trajectory generator.
    ///
    /// PlannedPath noktalarını hız, heading, curvature, lookahead ve geçiş fazı bilgisi
    /// taşıyan trajectory noktalarına çevirir.
    ///
    /// Bu sınıf sadece "path noktalarını kopyalayan" basit bir dönüştürücü değildir.
    /// Araç-hedef mesafesi, path progress, risk, corridor/gate güvenliği, final capture
    /// bölgesi ve CanReverse gibi görev/araç kabiliyetlerini birlikte değerlendirerek
    /// daha profesyonel bir hız profili üretir.
    ///
    /// Temel prensip:
    /// - Araç hedefe uzakken cruise hızına izin verilir.
    /// - Riskli corridor/gate noktalarında hız yumuşatılır.
    /// - Final hedefe yaklaşınca arrival/capture hızına düşülür.
    /// - Final noktanın trajectory point olarak hedefe yakın olması, araç uzaktayken tüm
    ///   yolu arrival hızına kilitlemez.
    /// </summary>
    public sealed class SimpleTrajectoryGenerator : ITrajectoryGenerator
    {
        private const double PassedPointSlackMeters = 0.75;
        private const double FinalCaptureLookAheadFactor = 0.55;

        /*
         * Hız profili sabitleri.
         *
         * Bunlar doğrudan "yarış hızı" değildir; planner'ın trajectory üst sınırı /
         * faz davranışıdır. Asıl fiziksel limitler yine PlanningContext, Control,
         * SafetyLimiter ve Actuator katmanlarında korunur.
         */
        private const double StartPointSpeedLimitMps = 0.35;
        private const double MinimumMovingSpeedMps = 0.12;
        private const double CleanPathMinimumCruiseMps = 0.55;

        private const double MaximumNominalCruiseMps = 1.20;
        private const double MaximumCorridorCruiseMps = 0.85;
        private const double MaximumAvoidanceCruiseMps = 0.65;

        /*
         * Hedefe yaklaşma bölgeleri.
         *
         * Bu değerler acceptance radius ile ölçeklenir. Böylece küçük toleranslı hassas
         * görevlerde daha erken yavaşlama, geniş fly-through görevlerinde daha akıcı
         * geçiş elde edilir.
         */
        private const double CaptureZoneFactor = 1.35;
        private const double ApproachZoneFactor = 3.00;
        private const double CautionZoneFactor = 6.00;

        public TrajectoryPlan GenerateTrajectory(
            PlanningContext context,
            PlannedPath path)
        {
            var safe = (context ?? PlanningContext.Idle).Sanitized();
            var planned = (path ?? PlannedPath.Empty).Sanitized();

            if (!planned.IsValid || planned.Points.Count == 0)
                return TrajectoryPlan.Empty with
                {
                    Summary = "TRAJECTORY_SKIPPED_INVALID_PATH"
                };

            var vehiclePosition = safe.VehicleState.Position.Sanitized();
            var vehicleDistanceToGoal = Distance(vehiclePosition, safe.Goal.TargetPosition);
            var speedProfile = ResolveSpeedProfile(safe, planned, vehicleDistanceToGoal);

            var points = new List<TrajectoryPoint>();
            double distanceAlong = 0.0;

            for (var i = 0; i < planned.Points.Count; i++)
            {
                var current = planned.Points[i];

                if (i > 0)
                    distanceAlong += Distance(planned.Points[i - 1].Position, current.Position);

                var next = i + 1 < planned.Points.Count
                    ? planned.Points[i + 1]
                    : current;

                var heading = ResolveHeading(current, next);

                var speed = ResolveSpeed(
                    safe,
                    planned,
                    current,
                    speedProfile,
                    i);

                var pointRisk = Math.Max(current.RiskScore, planned.Risk.RiskScore);

                var point = new TrajectoryPoint
                {
                    Id = $"traj-{i}-{current.Id}",
                    Position = current.Position,
                    HeadingDeg = heading,
                    DesiredSpeedMps = speed,
                    DesiredYawRateDegPerSec = 0.0,
                    Curvature = EstimateCurvature(planned, i),
                    DistanceAlongPathMeters = distanceAlong,
                    AcceptanceRadiusMeters = current.AcceptanceRadiusMeters,
                    RiskScore = pointRisk,
                    RequiresHeadingAlignment = ShouldRequireHeadingAlignment(safe, planned, current, i),
                    RequiresSlowMode = planned.Risk.RequiresSlowMode || pointRisk >= 0.45 || speedProfile.RequiresSlowMode,
                    Reason = BuildReason(planned, current, i, speedProfile)
                }.Sanitized();

                points.Add(point);
            }

            var progress = EstimatePathProgress(
                vehiclePosition,
                points);

            var lookAhead = SelectLookAheadPoint(
                safe,
                points,
                progress);

            var minSpeed = points.Count == 0 ? 0.0 : points.Min(x => x.DesiredSpeedMps);
            var maxSpeed = points.Count == 0 ? 0.0 : points.Max(x => x.DesiredSpeedMps);

            return new TrajectoryPlan
            {
                Mode = planned.Mode,
                SourcePath = planned,
                Points = points,
                LookAheadPoint = lookAhead,
                Risk = planned.Risk,
                IsValid = points.Count > 0,
                RequiresReplan = planned.RequiresReplan,
                RequiresSlowMode = planned.Risk.RequiresSlowMode || speedProfile.RequiresSlowMode,
                Source = nameof(SimpleTrajectoryGenerator),
                Summary =
                    $"TRAJECTORY points={points.Count} " +
                    $"progress={progress.DistanceAlongPathMeters:F2}m " +
                    $"seg={progress.SegmentIndex} " +
                    $"lookahead={lookAhead?.Id ?? "none"} " +
                    $"lookaheadSpeed={lookAhead?.DesiredSpeedMps ?? 0.0:F2}mps " +
                    $"vehicleGoalDist={vehicleDistanceToGoal:F2}m " +
                    $"profile={speedProfile.Phase} " +
                    $"targetSpeed={speedProfile.TargetSpeedMps:F2}mps " +
                    $"cruise={speedProfile.CruiseSpeedMps:F2}mps " +
                    $"riskScale={speedProfile.RiskScale:F2} " +
                    $"speedRange={minSpeed:F2}-{maxSpeed:F2}mps " +
                    $"risk={planned.Risk.RiskScore:F2}"
            }.Sanitized();
        }

        private static double ResolveHeading(
            PlannedPathPoint current,
            PlannedPathPoint next)
        {
            if (double.IsFinite(current.PreferredHeadingDeg))
                return current.PreferredHeadingDeg;

            var dx = next.Position.X - current.Position.X;
            var dy = next.Position.Y - current.Position.Y;

            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return 0.0;

            return NormalizeDeg(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        private static TrajectorySpeedProfile ResolveSpeedProfile(
            PlanningContext context,
            PlannedPath path,
            double vehicleDistanceToGoal)
        {
            var acceptance = Math.Max(0.25, context.Goal.AcceptanceRadiusMeters);

            var captureDistance = Math.Max(
                acceptance * CaptureZoneFactor,
                acceptance + 0.35);

            var approachDistance = Math.Max(
                acceptance * ApproachZoneFactor,
                captureDistance + 0.75);

            var cautionDistance = Math.Max(
                acceptance * CautionZoneFactor,
                approachDistance + 1.25);

            /*
             * Temel cruise hızı üç kaynaktan sınırlanır:
             * - PlanningContext.MaxPlanSpeedMps: runtime/platform üst sınırı
             * - Goal.DesiredCruiseSpeedMps: görevin istediği seyir hızı
             * - planner nominal sınırı: bu generator'ın güvenli default tavanı
             */
            var nominalCruise = Math.Min(
                context.MaxPlanSpeedMps,
                Math.Min(context.Goal.DesiredCruiseSpeedMps, MaximumNominalCruiseMps));

            nominalCruise = Math.Max(0.0, nominalCruise);

            /*
             * Mod bazlı tavan.
             * Corridor geçişlerinde fazla hız, gate merkezlerini overshoot ettirebilir.
             * Avoidance modunda clearance/risk daha önemli olduğu için hız düşürülür.
             */
            var modeLimitedCruise = path.Mode switch
            {
                PlanningMode.Avoidance => Math.Min(nominalCruise, MaximumAvoidanceCruiseMps),
                PlanningMode.Corridor => Math.Min(nominalCruise, MaximumCorridorCruiseMps),
                PlanningMode.Arrival => Math.Min(nominalCruise, Math.Max(context.Goal.DesiredArrivalSpeedMps, 0.45)),
                _ => nominalCruise
            };

            /*
             * Risk yumuşatması.
             * Risk çok düşükse cruise korunur, orta riskte hafif azaltılır, yüksek riskte
             * slow-mode'a yaklaşılır.
             */
            var risk = Math.Clamp(path.Risk.RiskScore, 0.0, 1.0);

            var riskScale = risk switch
            {
                >= 0.85 => 0.35,
                >= 0.65 => 0.50,
                >= 0.45 => 0.65,
                >= 0.25 => 0.82,
                _ => 1.00
            };

            if (path.Risk.RequiresSlowMode)
                riskScale = Math.Min(riskScale, 0.55);

            var cruise = Math.Clamp(
                modeLimitedCruise * riskScale,
                0.0,
                context.MaxPlanSpeedMps);

            /*
             * Final yaklaşma fazı araç-hedef mesafesine göre hesaplanır.
             * Eski hatada trajectory point final hedefte olduğu için distance=0 çıkıyor ve
             * araç uzaktayken bile final nokta arrival speed'e kilitleniyordu.
             */
            var arrivalSpeed = Math.Clamp(
                context.Goal.DesiredArrivalSpeedMps,
                0.0,
                Math.Max(context.MaxPlanSpeedMps, 0.01));

            var phase = TrajectorySpeedPhase.Cruise;
            var phaseScale = 1.0;

            if (vehicleDistanceToGoal <= captureDistance)
            {
                phase = TrajectorySpeedPhase.Capture;
                phaseScale = 0.0;
            }
            else if (vehicleDistanceToGoal <= approachDistance)
            {
                phase = TrajectorySpeedPhase.Approach;

                var t = Normalize01(
                    (vehicleDistanceToGoal - captureDistance) /
                    Math.Max(1e-6, approachDistance - captureDistance));

                /*
                 * Capture bölgesinden approach dışına doğru hız yumuşak artar.
                 * SmoothStep ani hız sıçramasını engeller.
                 */
                phaseScale = Lerp(
                    0.35,
                    0.72,
                    SmoothStep(t));
            }
            else if (vehicleDistanceToGoal <= cautionDistance)
            {
                phase = TrajectorySpeedPhase.Caution;

                var t = Normalize01(
                    (vehicleDistanceToGoal - approachDistance) /
                    Math.Max(1e-6, cautionDistance - approachDistance));

                phaseScale = Lerp(
                    0.72,
                    0.92,
                    SmoothStep(t));
            }

            /*
             * Reverse olmayan araçlarda finale yaklaşırken daha erken ve daha kontrollü hız
             * profili gerekir. Bu, cruise'u öldürmez; sadece approach/capture davranışını
             * daha saygılı hale getirir.
             */
            if (!context.Goal.AllowReverse &&
                phase is TrajectorySpeedPhase.Approach or TrajectorySpeedPhase.Capture)
            {
                phaseScale *= 0.85;
            }

            var targetSpeed = phase switch
            {
                TrajectorySpeedPhase.Capture => Math.Min(arrivalSpeed, cruise),
                TrajectorySpeedPhase.Approach => Math.Max(
                    Math.Min(arrivalSpeed, cruise),
                    cruise * phaseScale),
                _ => cruise * phaseScale
            };

            /*
             * Araç hedefe hâlâ uzaktaysa ve rota temizse, final arrival hızına çakılı
             * kalmasını engelle. Bu özellikle iki noktalı direct path'te kritik.
             */
            if (phase is TrajectorySpeedPhase.Cruise or TrajectorySpeedPhase.Caution &&
                !path.Risk.RequiresSlowMode &&
                risk < 0.35)
            {
                targetSpeed = Math.Max(
                    targetSpeed,
                    Math.Min(cruise, CleanPathMinimumCruiseMps));
            }

            /*
             * Capture dışındayken sıfıra düşen hız, görev ilerlemesini öldürür.
             * Bu taban sadece hâlâ hedefe yaklaşma mesafesi varsa uygulanır.
             */
            if (phase != TrajectorySpeedPhase.Capture &&
                vehicleDistanceToGoal > captureDistance)
            {
                targetSpeed = Math.Max(targetSpeed, MinimumMovingSpeedMps);
            }

            targetSpeed = Math.Clamp(targetSpeed, 0.0, context.MaxPlanSpeedMps);

            return new TrajectorySpeedProfile(
                Phase: phase,
                VehicleDistanceToGoalMeters: vehicleDistanceToGoal,
                CaptureDistanceMeters: captureDistance,
                ApproachDistanceMeters: approachDistance,
                CautionDistanceMeters: cautionDistance,
                CruiseSpeedMps: cruise,
                TargetSpeedMps: targetSpeed,
                ArrivalSpeedMps: arrivalSpeed,
                RiskScale: riskScale,
                RequiresSlowMode: path.Risk.RequiresSlowMode || risk >= 0.45);
        }

        private static double ResolveSpeed(
            PlanningContext context,
            PlannedPath path,
            PlannedPathPoint point,
            TrajectorySpeedProfile profile,
            int index)
        {
            var goalCruise = SafeNonNegative(
                context.Goal.DesiredCruiseSpeedMps,
                MaximumNominalCruiseMps);

            var preferredFallback = goalCruise > 0.0
                ? goalCruise
                : profile.TargetSpeedMps;

            var pointPreferred = SafeNonNegative(
                point.PreferredSpeedMps,
                preferredFallback);

            /*
             * Start noktası çoğu zaman 0 hız taşır. Bu doğru.
             * Ancak start dışındaki noktalarda 0 tercihli hız gelirse path'i öldürmesin diye
             * hedef hız fallback olarak kullanılır.
             */
            if (index > 0 && pointPreferred <= 1e-6)
                pointPreferred = preferredFallback;

            pointPreferred = Math.Min(pointPreferred, goalCruise);

            var speed = Math.Min(
                context.MaxPlanSpeedMps,
                Math.Min(pointPreferred, profile.TargetSpeedMps));

            /*
             * Path'in ilk noktası genellikle aracın o anki pozisyonudur.
             * Bu noktanın çok yüksek hız taşıması lookahead seçimi/başlangıç geçişinde
             * gereksiz agresif davranış üretebilir.
             */
            if (index == 0)
                speed = Math.Min(speed, StartPointSpeedLimitMps);

            /*
             * Nokta bazlı risk yumuşatması.
             * Corridor gate darsa veya lokal planner nokta riskini yükselttiyse, sadece
             * ilgili noktada hız düşer; bütün path gereksiz yavaşlamaz.
             */
            var pointRisk = Math.Clamp(Math.Max(point.RiskScore, path.Risk.RiskScore), 0.0, 1.0);

            if (path.Risk.RequiresSlowMode || pointRisk >= 0.45)
                speed *= 0.70;

            if (pointRisk >= 0.65)
                speed *= 0.70;

            if (pointRisk >= 0.85)
                speed *= 0.55;

            /*
             * Final capture bölgesinde tüm noktaların hızı arrival hızını aşmamalı.
             * Ama bu karar araç-hedef mesafesine göre verilir, point-hedef mesafesine göre değil.
             */
            if (profile.Phase == TrajectorySpeedPhase.Capture)
                speed = Math.Min(speed, profile.ArrivalSpeedMps);

            /*
             * Approach fazında hız sıfıra düşmesin ama profile.TargetSpeedMps üstüne de
             * kontrolsüz çıkmasın.
             */
            if (profile.Phase == TrajectorySpeedPhase.Approach && index > 0)
            {
                speed = Math.Max(speed, Math.Min(profile.ArrivalSpeedMps, profile.TargetSpeedMps));
                speed = Math.Min(speed, profile.TargetSpeedMps);
            }

            /*
             * Eğer görev hareket istiyorsa ve hedefe hâlâ uzaksak hızın tamamen sıfıra
             * düşmesini engelle. Start noktası hariç tutulur; start noktası 0 kalabilir.
             */
            if (index > 0 &&
                profile.Phase is TrajectorySpeedPhase.Cruise or TrajectorySpeedPhase.Caution &&
                speed > 0.0)
            {
                speed = Math.Max(speed, MinimumMovingSpeedMps);
            }

            return Math.Clamp(speed, 0.0, context.MaxPlanSpeedMps);
        }

        private static TrajectoryPoint? SelectLookAheadPoint(
            PlanningContext context,
            IReadOnlyList<TrajectoryPoint> points,
            PathProgress progress)
        {
            if (points.Count == 0)
                return null;

            if (points.Count == 1)
                return points[0];

            var vehicle = context.VehicleState.Position;
            var lookAheadDistance = Math.Max(1.0, context.LookAheadMeters);

            var final = points[^1];
            var distanceToFinal = Distance(vehicle, final.Position);

            /*
             * Final hedefe yaklaşıldığında lookahead zorla daha eski corridor/gate
             * noktalarına dönmemeli. Hedefe yeterince yaklaşıldıysa final referansı korunur.
             */
            if (distanceToFinal <= Math.Max(
                    final.AcceptanceRadiusMeters * 3.0,
                    lookAheadDistance * FinalCaptureLookAheadFactor))
            {
                return final;
            }

            var desiredDistanceAlong = progress.DistanceAlongPathMeters + lookAheadDistance;

            TrajectoryPoint? bestForward = null;

            foreach (var point in points)
            {
                if (IsPointClearlyBehindProgress(point, progress))
                    continue;

                if (point.DistanceAlongPathMeters + PassedPointSlackMeters < progress.DistanceAlongPathMeters)
                    continue;

                if (point.DistanceAlongPathMeters >= desiredDistanceAlong)
                {
                    bestForward = point;
                    break;
                }
            }

            if (bestForward is not null)
                return bestForward;

            /*
             * Eğer lookahead mesafesi path sonunu aşıyorsa, ileride kalan son noktayı seç.
             * Burada geride kalan gate noktalarını tekrar seçmiyoruz.
             */
            for (var i = points.Count - 1; i >= 0; i--)
            {
                var point = points[i];

                if (!IsPointClearlyBehindProgress(point, progress) &&
                    point.DistanceAlongPathMeters + PassedPointSlackMeters >= progress.DistanceAlongPathMeters)
                {
                    return point;
                }
            }

            return final;
        }

        private static bool IsPointClearlyBehindProgress(
            TrajectoryPoint point,
            PathProgress progress)
        {
            if (progress.SegmentIndex < 0)
                return false;

            /*
             * Noktanın path distance'ı aracın projection ilerlemesinden belirgin şekilde
             * gerideyse bu nokta lookahead olamaz.
             */
            return point.DistanceAlongPathMeters < progress.DistanceAlongPathMeters - PassedPointSlackMeters;
        }

        private static PathProgress EstimatePathProgress(
            Vec3 vehicle,
            IReadOnlyList<TrajectoryPoint> points)
        {
            if (points.Count == 0)
                return new PathProgress(
                    SegmentIndex: -1,
                    SegmentT: 0.0,
                    DistanceAlongPathMeters: 0.0,
                    CrossTrackErrorMeters: double.PositiveInfinity);

            if (points.Count == 1)
            {
                return new PathProgress(
                    SegmentIndex: 0,
                    SegmentT: 0.0,
                    DistanceAlongPathMeters: points[0].DistanceAlongPathMeters,
                    CrossTrackErrorMeters: Distance(vehicle, points[0].Position));
            }

            var bestSegment = 0;
            var bestT = 0.0;
            var bestDistance = double.PositiveInfinity;
            var bestAlong = 0.0;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];

                var projection = ProjectPointToSegment2D(
                    vehicle,
                    a.Position,
                    b.Position);

                var segmentLength = Math.Max(
                    1e-6,
                    Distance2D(a.Position, b.Position));

                var along =
                    a.DistanceAlongPathMeters +
                    projection.T * segmentLength;

                if (projection.DistanceMeters < bestDistance)
                {
                    bestDistance = projection.DistanceMeters;
                    bestSegment = i;
                    bestT = projection.T;
                    bestAlong = along;
                }
            }

            return new PathProgress(
                SegmentIndex: bestSegment,
                SegmentT: bestT,
                DistanceAlongPathMeters: Math.Max(0.0, bestAlong),
                CrossTrackErrorMeters: bestDistance);
        }

        private static SegmentProjection ProjectPointToSegment2D(
            Vec3 point,
            Vec3 a,
            Vec3 b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;

            var segmentLengthSq = vx * vx + vy * vy;

            if (segmentLengthSq <= 1e-12)
            {
                return new SegmentProjection(
                    T: 0.0,
                    DistanceMeters: Distance2D(point, a));
            }

            var t = (vx * wx + vy * wy) / segmentLengthSq;
            t = Math.Clamp(t, 0.0, 1.0);

            var px = a.X + t * vx;
            var py = a.Y + t * vy;

            var dx = point.X - px;
            var dy = point.Y - py;

            return new SegmentProjection(
                T: t,
                DistanceMeters: Math.Sqrt(dx * dx + dy * dy));
        }

        private static bool ShouldRequireHeadingAlignment(
            PlanningContext context,
            PlannedPath path,
            PlannedPathPoint point,
            int index)
        {
            if (context.Goal.RequiresHeadingAlignment)
                return true;

            if (index == 0)
                return true;

            if (path.Mode is PlanningMode.Corridor or PlanningMode.Avoidance or PlanningMode.Arrival)
                return true;

            return point.RiskScore >= 0.45;
        }

        private static double EstimateCurvature(
            PlannedPath path,
            int index)
        {
            if (path.Points.Count < 3)
                return 0.0;

            if (index <= 0 || index >= path.Points.Count - 1)
                return 0.0;

            var a = path.Points[index - 1].Position;
            var b = path.Points[index].Position;
            var c = path.Points[index + 1].Position;

            var h1 = HeadingDeg(a, b);
            var h2 = HeadingDeg(b, c);

            var delta = Math.Abs(NormalizeDeg(h2 - h1));
            var segmentLength = Math.Max(0.1, Distance(a, b));

            return Math.Clamp(delta / segmentLength / 90.0, 0.0, 10.0);
        }

        private static string BuildReason(
            PlannedPath path,
            PlannedPathPoint point,
            int index,
            TrajectorySpeedProfile profile)
        {
            return
                $"{path.Mode}_TRAJECTORY_{index}_{point.Reason} " +
                $"phase={profile.Phase} " +
                $"vehGoalDist={profile.VehicleDistanceToGoalMeters:F1}m " +
                $"targetSpeed={profile.TargetSpeedMps:F2}mps";
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double Distance2D(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double HeadingDeg(Vec3 from, Vec3 to)
        {
            return NormalizeDeg(Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI);
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

        private static double SafeNonNegative(double value, double fallback)
        {
            if (!double.IsFinite(value) || value < 0.0)
                return Math.Max(0.0, fallback);

            return value;
        }

        private static double Normalize01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }

        private static double SmoothStep(double value)
        {
            var t = Normalize01(value);
            return t * t * (3.0 - 2.0 * t);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Normalize01(t);
        }

        private enum TrajectorySpeedPhase
        {
            Cruise,
            Caution,
            Approach,
            Capture
        }

        private readonly record struct TrajectorySpeedProfile(
            TrajectorySpeedPhase Phase,
            double VehicleDistanceToGoalMeters,
            double CaptureDistanceMeters,
            double ApproachDistanceMeters,
            double CautionDistanceMeters,
            double CruiseSpeedMps,
            double TargetSpeedMps,
            double ArrivalSpeedMps,
            double RiskScale,
            bool RequiresSlowMode);

        private readonly record struct PathProgress(
            int SegmentIndex,
            double SegmentT,
            double DistanceAlongPathMeters,
            double CrossTrackErrorMeters);

        private readonly record struct SegmentProjection(
            double T,
            double DistanceMeters);
    }
}