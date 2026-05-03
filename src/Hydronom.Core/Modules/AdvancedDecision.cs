using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedDecision v3.0
    /// ------------------------------------------------------------
    /// Platform baÄŸÄ±msÄ±z 6-DoF wrench tabanlÄ± karar modÃ¼lÃ¼.
    ///
    /// Ã‡Ä±ktÄ± sÃ¶zleÅŸmesi:
    /// - Fx,Fy,Fz â†’ Newton
    /// - Tx,Ty,Tz â†’ Newton-metre
    /// - TÃ¼m wrench body frame'dedir.
    ///
    /// Bu sÃ¼rÃ¼m:
    /// - Navigate / Avoid / Hold ayrÄ±mÄ±nÄ± aÃ§Ä±klanabilir hale getirir.
    /// - LastDecisionReport Ã¼retir.
    /// - NaN / Infinity / bozuk dt korumasÄ± yapar.
    /// - Hedefe yaklaÅŸÄ±rken aktif frenleme uygular.
    /// - BÃ¼yÃ¼k heading hatasÄ±nda ileri itmeyi kapÄ±lar.
    /// - Body-frame hÄ±z ve pozisyon hatasÄ±nÄ± kullanÄ±r.
    /// - Station keeping iÃ§in XY + yaw + roll/pitch + heave kontrolÃ¼ yapar.
    /// - Heave integral anti-windup iÃ§erir.
    /// - Deterministiktir; rastgelelik yoktur.
    /// ------------------------------------------------------------
    /// Not:
    /// SafetyLimiter komutu ayrÄ±ca yumuÅŸatÄ±r.
    /// ActuatorManager ise gerÃ§ekten Ã¼retilebilen wrench'i allocation raporuyla aÃ§Ä±klar.
    /// </summary>
    public class AdvancedDecision : IDecisionModule
    {
        // ---------------------------------------------------------------------
        // GLOBAL EFFORT SCALE
        // ---------------------------------------------------------------------
        private const double GlobalEffortScale = 0.75;

        // ---------------------------------------------------------------------
        // EKSEN KAPASÄ°TELERÄ°
        // ---------------------------------------------------------------------
        private const double MaxFxN = 24.0;
        private const double MaxFyN = 12.0;
        private const double MaxFzN = 35.0;

        private const double MaxTxNm = 5.0;
        private const double MaxTyNm = 7.0;
        private const double MaxTzNm = 8.0;

        // ---------------------------------------------------------------------
        // MESAFE / HIZ PROFÄ°LÄ°
        // ---------------------------------------------------------------------
        private const double SlowRadiusM = 4.0;
        private const double StopRadiusM = 0.7;

        private const double CloseEnoughRadiusM = 0.50;
        private const double CloseLinearVelThresh = 0.25;
        private const double CloseAngVelThreshDeg = 5.0;

        private const double CruiseThrottleNorm = 0.62;
        private const double MinApproachThrottleNorm = 0.07;

        private const double BrakeRadiusM = 1.60;
        private const double BrakeSpeedStartMps = 0.45;
        private const double BrakeSpeedFullMps = 1.20;
        private const double MaxReverseThrottleNorm = 0.32;

        private const double NearTurnInPlaceDeg = 95.0;

        // ---------------------------------------------------------------------
        // HEADING / YAW KONTROL
        // ---------------------------------------------------------------------
        private const double RudderFullAtDeg = 45.0;
        private const double RudderDeadbandDeg = 3.0;
        private const double YawRateDeadbandDeg = 2.0;

        private const double NavYawKp = 1.0 / RudderFullAtDeg;
        private const double NavYawKd = 0.055;

        private const double NearYawBrakeKp = 0.030;
        private const double NearYawBrakeKd = 0.020;

        // ---------------------------------------------------------------------
        // Roll / pitch PD
        // ---------------------------------------------------------------------
        private const double AttKp = 0.035;
        private const double AttKd = 0.020;

        // ---------------------------------------------------------------------
        // Sway damping
        // ---------------------------------------------------------------------
        private const double SwayVelGain = 0.15;
        private const double MaxSwayNorm = 0.9;

        // ---------------------------------------------------------------------
        // Station keeping
        // ---------------------------------------------------------------------
        private const double HoldKp = 0.45;
        private const double HoldKd = 0.30;
        private const double YawKp = 0.018;
        private const double YawKd = 0.012;

        // ---------------------------------------------------------------------
        // Heave PID
        // ---------------------------------------------------------------------
        private double _heaveIntegral = 0.0;
        private const double HeaveKp = 0.25;
        private const double HeaveKd = 0.50;
        private const double HeaveKi = 0.02;
        private const double HeaveImax = 0.2;
        private const double MaxHeaveNorm = 1.0;

        // ---------------------------------------------------------------------
        // Ä°Ã§ durum / hafÄ±za
        // ---------------------------------------------------------------------
        private Vec3? _lastTarget = null;
        private double? _frozenHoldHeadingDeg = null;
        private bool _isHoldingPosition = false;

        /// <summary>
        /// Son kararÄ±n aÃ§Ä±klanabilir raporu.
        /// Diagnostics, log, Hydronom Ops veya test kodu bunu okuyabilir.
        /// </summary>
        public AdvancedDecisionReport LastDecisionReport { get; private set; } =
            AdvancedDecisionReport.Empty;

        // ---------------------------------------------------------------------
        // ANA KARAR FONKSÄ°YONU
        // ---------------------------------------------------------------------
        public DecisionCommand Decide(Insights insights, TaskDefinition? task, VehicleState state, double dt)
        {
            dt = SanitizeDt(dt);
            state = state.Sanitized();

            if (task is null)
            {
                ResetControllerState();
                return ReportAndReturn(
                    DecisionMode.Idle,
                    "NO_TASK",
                    DecisionCommand.Zero,
                    DecisionCommand.Zero,
                    state,
                    target: null,
                    distanceXY: 0.0,
                    headingErrorDeg: 0.0,
                    forwardSpeedMps: 0.0,
                    yawRateDeg: state.AngularVelocity.Z,
                    obstacleAhead: insights.HasObstacleAhead
                );
            }

            if (task.Target is not Vec3 target)
            {
                ResetControllerState();
                return ReportAndReturn(
                    DecisionMode.Idle,
                    "TASK_HAS_NO_VEC3_TARGET",
                    DecisionCommand.Zero,
                    DecisionCommand.Zero,
                    state,
                    target: null,
                    distanceXY: 0.0,
                    headingErrorDeg: 0.0,
                    forwardSpeedMps: 0.0,
                    yawRateDeg: state.AngularVelocity.Z,
                    obstacleAhead: insights.HasObstacleAhead
                );
            }

            target = SanitizeVec(target);
            HandleTargetChange(target);

            var nav = ComputeNavigationGeometry(target, state);

            if (insights.HasObstacleAhead)
            {
                ExitHoldMode();

                var avoidCmd = Avoid(insights, task, state, dt, nav);
                return ReportAndReturn(
                    DecisionMode.Avoid,
                    "OBSTACLE_AHEAD",
                    avoidCmd.RawCommand,
                    avoidCmd.OutputCommand,
                    state,
                    target,
                    nav.DistanceXY,
                    nav.HeadingErrorDeg,
                    nav.ForwardSpeedMps,
                    nav.YawRateDeg,
                    obstacleAhead: true,
                    throttleNorm: avoidCmd.ThrottleNorm,
                    rudderNorm: avoidCmd.RudderNorm
                );
            }

            if (nav.DistanceXY <= StopRadiusM)
            {
                EnterHoldMode(state);

                var hold = HoldPosition(target, state, dt, nav);
                return ReportAndReturn(
                    DecisionMode.Hold,
                    hold.Reason,
                    hold.RawCommand,
                    hold.OutputCommand,
                    state,
                    target,
                    nav.DistanceXY,
                    nav.HeadingErrorDeg,
                    nav.ForwardSpeedMps,
                    nav.YawRateDeg,
                    obstacleAhead: false,
                    throttleNorm: 0.0,
                    rudderNorm: 0.0
                );
            }

            ExitHoldMode();

            var navCmd = NavigateToTarget(task, state, dt, nav);
            return ReportAndReturn(
                DecisionMode.Navigate,
                navCmd.Reason,
                navCmd.RawCommand,
                navCmd.OutputCommand,
                state,
                target,
                nav.DistanceXY,
                nav.HeadingErrorDeg,
                nav.ForwardSpeedMps,
                nav.YawRateDeg,
                obstacleAhead: false,
                throttleNorm: navCmd.ThrottleNorm,
                rudderNorm: navCmd.RudderNorm
            );
        }

        // ---------------------------------------------------------------------
        // ENGEL KAÃ‡INMA
        // ---------------------------------------------------------------------
        private DecisionResult Avoid(
            Insights ins,
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            double left = SafeNonNegative(ins.ClearanceLeft, 0.0);
            double right = SafeNonNegative(ins.ClearanceRight, 0.0);

            double sideSign;
            if (Math.Abs(right - left) < 0.10)
            {
                // AÃ§Ä±klÄ±klar eÅŸitse hedefe gÃ¶re daha az ters dÃ¼ÅŸen yÃ¶nÃ¼ seÃ§.
                sideSign = nav.HeadingErrorDeg >= 0.0 ? +1.0 : -1.0;
            }
            else
            {
                sideSign = right > left ? +1.0 : -1.0;
            }

            double clearanceMax = Math.Max(left, right);
            double clearanceMin = Math.Min(left, right);
            double clearanceBalance = (clearanceMax - clearanceMin) / Math.Max(0.5, clearanceMax);

            double throttleNorm = 0.10;

            // Ã‡ok yakÄ±n engelde ileri itmeyi daha fazla bastÄ±r.
            if (clearanceMin < 1.0)
                throttleNorm = 0.02;
            else if (clearanceMin < 2.0)
                throttleNorm = 0.06;

            double rudderNorm = (0.50 + 0.35 * Math.Clamp(clearanceBalance, 0.0, 1.0)) * sideSign;
            rudderNorm = Math.Clamp(rudderNorm, -0.90, 0.90);

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: $"AVOID side={(sideSign > 0 ? "right" : "left")} clearL={left:F2} clearR={right:F2}",
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );
        }

        // ---------------------------------------------------------------------
        // HEDEF NAVÄ°GASYONU
        // ---------------------------------------------------------------------
        private DecisionResult NavigateToTarget(
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            double absDelta = Math.Abs(nav.HeadingErrorDeg);
            double absYawRate = Math.Abs(nav.YawRateDeg);

            double rudderNorm = 0.0;

            if (absDelta > RudderDeadbandDeg || absYawRate > YawRateDeadbandDeg)
            {
                double p = nav.HeadingErrorDeg * NavYawKp;
                double d = -nav.YawRateDeg * NavYawKd;
                rudderNorm = Math.Clamp(p + d, -1.0, 1.0);
            }

            if (nav.DistanceXY < BrakeRadiusM)
            {
                double nearBrake = nav.HeadingErrorDeg * NearYawBrakeKp - nav.YawRateDeg * NearYawBrakeKd;
                rudderNorm = Math.Clamp(nearBrake, -1.0, 1.0);
            }

            double throttleNorm = ComputeApproachThrottle(nav.DistanceXY);

            throttleNorm *= HeadingScale(absDelta);
            throttleNorm *= HeadingThrottleGate(absDelta, absYawRate);

            if (absDelta >= NearTurnInPlaceDeg)
                throttleNorm = Math.Min(throttleNorm, 0.03);

            double desiredForwardSign = nav.TargetBody.X >= 0.0 ? 1.0 : -1.0;

            if (nav.DistanceXY < BrakeRadiusM && desiredForwardSign > 0.0)
            {
                double brakeNorm = ComputeApproachBrakeNorm(nav.DistanceXY, nav.ForwardSpeedMps);
                if (brakeNorm > 0.0)
                    throttleNorm = -brakeNorm;
            }

            if (absDelta > 70.0 && nav.ForwardSpeedMps > 0.35)
            {
                double brakeAssist = Math.Clamp(
                    (nav.ForwardSpeedMps - 0.35) / 0.75,
                    0.0,
                    MaxReverseThrottleNorm * 0.7
                );

                throttleNorm = Math.Min(throttleNorm, 0.0);
                throttleNorm -= brakeAssist;
            }

            throttleNorm = Math.Clamp(throttleNorm, -MaxReverseThrottleNorm, CruiseThrottleNorm);

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: "NAVIGATE",
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );
        }

        private static double ComputeApproachThrottle(double distanceM)
        {
            if (distanceM >= SlowRadiusM)
                return CruiseThrottleNorm;

            double k = (distanceM - StopRadiusM) / (SlowRadiusM - StopRadiusM);
            k = Math.Clamp(k, 0.0, 1.0);

            return MinApproachThrottleNorm + k * (CruiseThrottleNorm - MinApproachThrottleNorm);
        }

        // ---------------------------------------------------------------------
        // STATION KEEPING
        // ---------------------------------------------------------------------
        private DecisionResult HoldPosition(
            Vec3 target,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            Vec3 posErrWorld = new Vec3(
                target.X - state.Position.X,
                target.Y - state.Position.Y,
                0.0
            );

            Vec3 posErrBody = state.Orientation.WorldToBody(posErrWorld);
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);

            bool linearSlow =
                Math.Abs(velB.X) < CloseLinearVelThresh &&
                Math.Abs(velB.Y) < CloseLinearVelThresh &&
                Math.Abs(state.Velocity.Z) < CloseLinearVelThresh;

            bool angularSlow =
                Math.Abs(state.AngularVelocity.X) < CloseAngVelThreshDeg &&
                Math.Abs(state.AngularVelocity.Y) < CloseAngVelThreshDeg &&
                Math.Abs(state.AngularVelocity.Z) < CloseAngVelThreshDeg;

            if (nav.DistanceXY < CloseEnoughRadiusM && linearSlow && angularSlow)
            {
                ResetHeaveIntegral();

                if (_frozenHoldHeadingDeg is null)
                    FreezeHoldHeading(state.Orientation.YawDeg);

                var zero = DecisionCommand.Zero;
                return new DecisionResult(
                    RawCommand: zero,
                    OutputCommand: zero,
                    Reason: "HOLD_SETTLED_ZERO_WRENCH",
                    ThrottleNorm: 0.0,
                    RudderNorm: 0.0
                );
            }

            double posScale = Math.Clamp(nav.DistanceXY / StopRadiusM, 0.2, 1.0);

            double fxNorm = (posErrBody.X * HoldKp - velB.X * HoldKd) * posScale;
            double fyNorm = (posErrBody.Y * HoldKp - velB.Y * HoldKd) * posScale;

            fxNorm = Math.Clamp(fxNorm, -1.0, 1.0);
            fyNorm = Math.Clamp(fyNorm, -1.0, 1.0);

            double fx = fxNorm * MaxFxN;
            double fy = fyNorm * MaxFyN;

            if (_frozenHoldHeadingDeg is null)
                FreezeHoldHeading(state.Orientation.YawDeg);

            double desiredYawDeg = _frozenHoldHeadingDeg!.Value;

            double dyaw = Normalize(desiredYawDeg - state.Orientation.YawDeg);
            double yawRate = state.AngularVelocity.Z;
            double yawScale = posScale;

            double tzNorm = (dyaw * YawKp - yawRate * YawKd) * yawScale;
            tzNorm = Math.Clamp(tzNorm, -1.0, 1.0);
            double tz = tzNorm * MaxTzNm;

            double rollRate = state.AngularVelocity.X;
            double pitchRate = state.AngularVelocity.Y;

            double txNorm = (-state.Orientation.RollDeg) * AttKp - rollRate * AttKd;
            double tyNorm = (-state.Orientation.PitchDeg) * AttKp - pitchRate * AttKd;

            txNorm = Math.Clamp(txNorm, -1.0, 1.0);
            tyNorm = Math.Clamp(tyNorm, -1.0, 1.0);

            double tx = txNorm * MaxTxNm;
            double ty = tyNorm * MaxTyNm;

            double fzNorm = ComputeHeave(target.Z, state, dt) * posScale;
            double fz = fzNorm * MaxFzN;

            var raw = new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: fz,
                tx: tx,
                ty: ty,
                tz: tz
            );

            var output = ScaleCommand(raw);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: "HOLD_POSITION",
                ThrottleNorm: fxNorm,
                RudderNorm: tzNorm
            );
        }

        // ---------------------------------------------------------------------
        // PLANAR â†’ 6DoF
        // ---------------------------------------------------------------------
        private DecisionCommand PlanarToRawWrench(
            double throttleNorm,
            double rudderNorm,
            TaskDefinition task,
            VehicleState state,
            double dt)
        {
            var secondary = ComputeSecondaryAxes(task, state, dt);

            double fx = Math.Clamp(throttleNorm, -1.0, 1.0) * MaxFxN;
            double tz = -Math.Clamp(rudderNorm, -1.0, 1.0) * MaxTzNm;

            return new DecisionCommand(
                fx: fx,
                fy: secondary.Fy,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            );
        }

        private static DecisionCommand ScaleCommand(DecisionCommand raw)
        {
            return new DecisionCommand(
                fx: Safe(raw.Fx) * GlobalEffortScale,
                fy: Safe(raw.Fy) * GlobalEffortScale,
                fz: Safe(raw.Fz) * GlobalEffortScale,
                tx: Safe(raw.Tx) * GlobalEffortScale,
                ty: Safe(raw.Ty) * GlobalEffortScale,
                tz: Safe(raw.Tz) * GlobalEffortScale
            );
        }

        // ---------------------------------------------------------------------
        // Ä°KÄ°NCÄ°L EKSENLER
        // ---------------------------------------------------------------------
        private (double Fy, double Fz, double Tx, double Ty)
            ComputeSecondaryAxes(TaskDefinition task, VehicleState state, double dt)
        {
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);
            double vy = Safe(velB.Y);

            double fyNorm = -vy * SwayVelGain;
            fyNorm = Math.Clamp(fyNorm, -MaxSwayNorm, MaxSwayNorm);
            double fy = fyNorm * MaxFyN;

            double txNorm = (-state.Orientation.RollDeg) * AttKp
                            - state.AngularVelocity.X * AttKd;

            double tyNorm = (-state.Orientation.PitchDeg) * AttKp
                            - state.AngularVelocity.Y * AttKd;

            txNorm = Math.Clamp(Safe(txNorm), -1.0, 1.0);
            tyNorm = Math.Clamp(Safe(tyNorm), -1.0, 1.0);

            double tx = txNorm * MaxTxNm;
            double ty = tyNorm * MaxTyNm;

            double fz = 0.0;
            if (task.Target is Vec3 t3d)
            {
                double fzNorm = ComputeHeave(t3d.Z, state, dt);
                fz = fzNorm * MaxFzN;
            }
            else
            {
                ResetHeaveIntegral();
            }

            return (fy, fz, tx, ty);
        }

        // ---------------------------------------------------------------------
        // HEAVE PID
        // ---------------------------------------------------------------------
        private double ComputeHeave(double targetZ, VehicleState state, double dt)
        {
            double error = Safe(targetZ - state.Position.Z);
            double vz = Safe(state.Velocity.Z);

            // Hata Ã§ok kÃ¼Ã§Ã¼kse integral yavaÅŸÃ§a sÃ¶nsÃ¼n.
            if (Math.Abs(error) < 0.03)
                _heaveIntegral *= 0.90;

            _heaveIntegral += error * HeaveKi * dt;
            _heaveIntegral = Math.Clamp(_heaveIntegral, -HeaveImax, HeaveImax);

            double fzNorm = error * HeaveKp + _heaveIntegral - vz * HeaveKd;
            return Math.Clamp(Safe(fzNorm), -MaxHeaveNorm, MaxHeaveNorm);
        }

        private void ResetHeaveIntegral()
        {
            _heaveIntegral = 0.0;
        }

        private void ResetControllerState()
        {
            ResetHeaveIntegral();
            _frozenHoldHeadingDeg = null;
            _lastTarget = null;
            _isHoldingPosition = false;
        }

        private void HandleTargetChange(Vec3 currentTarget)
        {
            if (_lastTarget is null)
            {
                _lastTarget = currentTarget;
                return;
            }

            bool changed =
                Math.Abs(_lastTarget.Value.X - currentTarget.X) > 1e-6 ||
                Math.Abs(_lastTarget.Value.Y - currentTarget.Y) > 1e-6 ||
                Math.Abs(_lastTarget.Value.Z - currentTarget.Z) > 1e-6;

            if (changed)
            {
                ResetHeaveIntegral();
                _frozenHoldHeadingDeg = null;
                _isHoldingPosition = false;
                _lastTarget = currentTarget;
            }
        }

        private void EnterHoldMode(VehicleState state)
        {
            if (_isHoldingPosition)
                return;

            FreezeHoldHeading(state.Orientation.YawDeg);
            _isHoldingPosition = true;
        }

        private void ExitHoldMode()
        {
            _frozenHoldHeadingDeg = null;
            _isHoldingPosition = false;
        }

        private void FreezeHoldHeading(double yawDeg)
        {
            _frozenHoldHeadingDeg = Normalize(yawDeg);
        }

        // ---------------------------------------------------------------------
        // RAPORLAMA
        // ---------------------------------------------------------------------
        private DecisionCommand ReportAndReturn(
            DecisionMode mode,
            string reason,
            DecisionCommand rawCommand,
            DecisionCommand outputCommand,
            VehicleState state,
            Vec3? target,
            double distanceXY,
            double headingErrorDeg,
            double forwardSpeedMps,
            double yawRateDeg,
            bool obstacleAhead,
            double throttleNorm = 0.0,
            double rudderNorm = 0.0)
        {
            LastDecisionReport = new AdvancedDecisionReport(
                Mode: mode,
                Reason: reason,
                Target: target,
                Position: state.Position,
                DistanceXY: SafeNonNegative(distanceXY, 0.0),
                HeadingErrorDeg: Safe(headingErrorDeg),
                ForwardSpeedMps: Safe(forwardSpeedMps),
                YawRateDeg: Safe(yawRateDeg),
                ObstacleAhead: obstacleAhead,
                IsHoldingPosition: _isHoldingPosition,
                FrozenHoldHeadingDeg: _frozenHoldHeadingDeg,
                ThrottleNorm: Math.Clamp(Safe(throttleNorm), -1.0, 1.0),
                RudderNorm: Math.Clamp(Safe(rudderNorm), -1.0, 1.0),
                RawCommand: rawCommand,
                OutputCommand: outputCommand
            );

            return outputCommand;
        }

        // ---------------------------------------------------------------------
        // GEOMETRÄ° / YARDIMCILAR
        // ---------------------------------------------------------------------
        private static NavigationGeometry ComputeNavigationGeometry(Vec3 target, VehicleState state)
        {
            double dx = Safe(target.X - state.Position.X);
            double dy = Safe(target.Y - state.Position.Y);

            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(dist))
                dist = 0.0;

            Vec3 toTargetWorld = new Vec3(dx, dy, 0.0);
            Vec3 toTargetBody = state.Orientation.WorldToBody(toTargetWorld);
            Vec3 velBody = state.Orientation.WorldToBody(state.Velocity);

            double targetHeading = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double delta = Normalize(targetHeading - state.Orientation.YawDeg);

            return new NavigationGeometry(
                DistanceXY: dist,
                TargetBody: toTargetBody,
                VelocityBody: velBody,
                HeadingErrorDeg: delta,
                ForwardSpeedMps: velBody.X,
                YawRateDeg: state.AngularVelocity.Z
            );
        }

        private static double Normalize(double deg)
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

        private static double SanitizeDt(double dt)
        {
            if (!double.IsFinite(dt))
                return 0.1;

            if (dt <= 1e-4)
                return 1e-4;

            if (dt > 0.25)
                return 0.25;

            return dt;
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                Safe(v.X),
                Safe(v.Y),
                Safe(v.Z)
            );
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

        private static double HeadingScale(double absDelta)
        {
            if (absDelta <= 10.0) return 1.0;
            if (absDelta >= 90.0) return 0.22;

            double x = (absDelta - 10.0) / 80.0;
            double s = x * x * (3.0 - 2.0 * x);

            return 1.0 + (0.22 - 1.0) * s;
        }

        /// <summary>
        /// Heading hatasÄ±na ve yaw rate'e gÃ¶re throttle kapÄ±sÄ±.
        /// AmaÃ§: bÃ¼yÃ¼k aÃ§Ä± hatasÄ±nda Ã¶nce yÃ¶nelimi toparlamak.
        /// </summary>
        private static double HeadingThrottleGate(double absDeltaDeg, double absYawRateDeg)
        {
            double gate;

            if (absDeltaDeg >= 150.0)
                gate = 0.03;
            else if (absDeltaDeg >= 120.0)
                gate = 0.10;
            else if (absDeltaDeg >= 90.0)
                gate = 0.25;
            else if (absDeltaDeg >= 60.0)
                gate = 0.55;
            else
                gate = 1.0;

            if (absYawRateDeg > 50.0)
                gate *= 0.25;
            else if (absYawRateDeg > 25.0)
                gate *= 0.55;

            return gate;
        }

        private static double ComputeApproachBrakeNorm(double dist, double forwardSpeed)
        {
            if (dist >= BrakeRadiusM)
                return 0.0;

            if (forwardSpeed <= BrakeSpeedStartMps)
                return 0.0;

            double distFactor = 1.0 - Math.Clamp((dist - StopRadiusM) / (BrakeRadiusM - StopRadiusM), 0.0, 1.0);
            double speedFactor = Math.Clamp(
                (forwardSpeed - BrakeSpeedStartMps) / (BrakeSpeedFullMps - BrakeSpeedStartMps),
                0.0,
                1.0
            );

            double brake = distFactor * speedFactor * MaxReverseThrottleNorm;
            return Math.Clamp(brake, 0.0, MaxReverseThrottleNorm);
        }

        private readonly record struct NavigationGeometry(
            double DistanceXY,
            Vec3 TargetBody,
            Vec3 VelocityBody,
            double HeadingErrorDeg,
            double ForwardSpeedMps,
            double YawRateDeg
        );

        private readonly record struct DecisionResult(
            DecisionCommand RawCommand,
            DecisionCommand OutputCommand,
            string Reason,
            double ThrottleNorm,
            double RudderNorm
        );
    }

    public enum DecisionMode
    {
        Idle = 0,
        Navigate = 1,
        Avoid = 2,
        Hold = 3
    }

    public readonly record struct AdvancedDecisionReport(
        DecisionMode Mode,
        string Reason,
        Vec3? Target,
        Vec3 Position,
        double DistanceXY,
        double HeadingErrorDeg,
        double ForwardSpeedMps,
        double YawRateDeg,
        bool ObstacleAhead,
        bool IsHoldingPosition,
        double? FrozenHoldHeadingDeg,
        double ThrottleNorm,
        double RudderNorm,
        DecisionCommand RawCommand,
        DecisionCommand OutputCommand
    )
    {
        public static AdvancedDecisionReport Empty { get; } =
            new(
                Mode: DecisionMode.Idle,
                Reason: "NOT_COMPUTED",
                Target: null,
                Position: Vec3.Zero,
                DistanceXY: 0.0,
                HeadingErrorDeg: 0.0,
                ForwardSpeedMps: 0.0,
                YawRateDeg: 0.0,
                ObstacleAhead: false,
                IsHoldingPosition: false,
                FrozenHoldHeadingDeg: null,
                ThrottleNorm: 0.0,
                RudderNorm: 0.0,
                RawCommand: DecisionCommand.Zero,
                OutputCommand: DecisionCommand.Zero
            );

        public override string ToString()
        {
            return
                $"Decision mode={Mode} reason={Reason} " +
                $"dist={DistanceXY:F2}m dHead={HeadingErrorDeg:F1}Â° " +
                $"vFwd={ForwardSpeedMps:F2}m/s yawRate={YawRateDeg:F1}Â°/s " +
                $"obs={ObstacleAhead} hold={IsHoldingPosition} " +
                $"thr={ThrottleNorm:F2} rud={RudderNorm:F2}";
        }
    }
}
