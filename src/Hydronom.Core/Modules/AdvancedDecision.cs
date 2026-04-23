using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedDecision v2.3
    /// ------------------------------------------------------------
    /// - 6DoF wrench tabanlı karar modülü (Fx,Fy,Fz,Tx,Ty,Tz fiziksel).
    /// - Navigate modunda daha güçlü yaw PD + aktif yaklaşma frenlemesi.
    /// - Heading büyük hatadaysa ileri itki sert biçimde kapılanır.
    /// - Hedefe yaklaşırken body-frame hız dikkate alınır.
    /// - Gerekirse negatif Fx ile aktif yavaşlama yapılır.
    /// - Station keeping: body-frame XY + yaw + roll/pitch + heave.
    /// - Hold moduna girişte heading bir kez dondurulur; hold boyunca
    ///   hedef noktasına tekrar bakılmaya çalışılmaz.
    /// - Hedefe çok yaklaşınca zero wrench'e düşerek hunting azaltılır.
    /// - Heave için dt tabanlı integral ve reset mantığı.
    /// - Deterministik: Rastgelelik yok, sadece state/task/insights → komut.
    /// - dt artık dışarıdan verilir; zaman ölçümü karar modülü içinde yapılmaz.
    /// ------------------------------------------------------------
    /// Not:
    /// - DecisionCommand çıktı birimleri:
    ///   Fx,Fy,Fz → Newton, Tx,Ty,Tz → Newton-metre (body frame).
    /// - ActuatorManager, bu wrench’i thruster geometri bilgisi ile çözer.
    /// </summary>
    public class AdvancedDecision : IDecisionModule
    {
        // ---------------------------------------------------------------------
        // GLOBAL EFFORT SCALE
        // ---------------------------------------------------------------------
        private const double GlobalEffortScale = 0.75;

        // ---------------------------------------------------------------------
        // EKSEN KAPASİTELERİ (FİZİKSEL)
        // ---------------------------------------------------------------------
        private const double MaxFxN = 24.0;
        private const double MaxFyN = 12.0;
        private const double MaxFzN = 35.0;

        private const double MaxTxNm = 5.0;
        private const double MaxTyNm = 7.0;
        private const double MaxTzNm = 8.0;

        // ---------------------------------------------------------------------
        // MESAFE / HIZ PROFİLİ
        // ---------------------------------------------------------------------
        private const double SlowRadiusM = 4.0;
        private const double StopRadiusM = 0.7;

        private const double CloseEnoughRadiusM = 0.50;
        private const double CloseLinearVelThresh = 0.25;
        private const double CloseAngVelThreshDeg = 5.0;

        private const double CruiseThrottleNorm = 0.62;
        private const double MinApproachThrottleNorm = 0.07;

        // Yaklaşırken aktif frenleme
        private const double BrakeRadiusM = 1.60;
        private const double BrakeSpeedStartMps = 0.45;
        private const double BrakeSpeedFullMps = 1.20;
        private const double MaxReverseThrottleNorm = 0.32;

        // Çok büyük heading hatasında neredeyse yerinde dön
        private const double NearTurnInPlaceDeg = 95.0;

        // ---------------------------------------------------------------------
        // HEADING / YAW KONTROL
        // ---------------------------------------------------------------------
        private const double RudderFullAtDeg = 45.0;
        private const double RudderDeadbandDeg = 3.0;
        private const double YawRateDeadbandDeg = 2.0;

        // Navigate modunda eski değerden daha güçlü sönüm
        private const double NavYawKp = 1.0 / RudderFullAtDeg;
        private const double NavYawKd = 0.055;

        // Hedefe çok yakınken spin söndürme için ekstra kazanç
        private const double NearYawBrakeKp = 0.030;
        private const double NearYawBrakeKd = 0.020;

        // ---------------------------------------------------------------------
        // Roll/pitch PD
        // ---------------------------------------------------------------------
        private const double AttKp = 0.035;
        private const double AttKd = 0.020;

        // ---------------------------------------------------------------------
        // Sway damping
        // ---------------------------------------------------------------------
        private const double SwayVelGain = 0.15;
        private const double MaxSwayNorm = 0.9;

        // ---------------------------------------------------------------------
        // Station keeping X-Y (body frame)
        // ---------------------------------------------------------------------
        private const double HoldKp = 0.45;
        private const double HoldKd = 0.30;

        // ---------------------------------------------------------------------
        // Yaw PD (station keeping)
        // ---------------------------------------------------------------------
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
        // İç durum / hafıza
        // ---------------------------------------------------------------------
        private Vec3? _lastTarget = null;
        private double? _frozenHoldHeadingDeg = null;
        private bool _isHoldingPosition = false;

        // ---------------------------------------------------------------------
        // ANA KARAR FONKSİYONU
        // ---------------------------------------------------------------------
        public DecisionCommand Decide(Insights insights, TaskDefinition? task, VehicleState state, double dt)
        {
            dt = SanitizeDt(dt);

            if (task is null)
            {
                ResetControllerState();
                return Zero();
            }

            if (task.Target is not Vec3 target)
            {
                ResetControllerState();
                return Zero();
            }

            HandleTargetChange(target);

            if (insights.HasObstacleAhead)
            {
                ExitHoldMode();
                return Avoid(insights, task, state, dt);
            }

            double dx = target.X - state.Position.X;
            double dy = target.Y - state.Position.Y;
            double distXY = Math.Sqrt(dx * dx + dy * dy);

            if (distXY <= StopRadiusM)
            {
                EnterHoldMode(state);
                return HoldPosition(target, state, dt);
            }

            ExitHoldMode();
            return NavigateToTarget(task, state, dt);
        }

        // ---------------------------------------------------------------------
        // WRENCH OLUŞTURUCU
        // ---------------------------------------------------------------------
        private static DecisionCommand Wrench(double fx, double fy, double fz,
                                              double tx, double ty, double tz)
        {
            fx *= GlobalEffortScale;
            fy *= GlobalEffortScale;
            fz *= GlobalEffortScale;
            tx *= GlobalEffortScale;
            ty *= GlobalEffortScale;
            tz *= GlobalEffortScale;

            return new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: fz,
                tx: tx,
                ty: ty,
                tz: tz
            );
        }

        private DecisionCommand Zero()
        {
            ResetHeaveIntegral();
            return Wrench(0, 0, 0, 0, 0, 0);
        }

        // ---------------------------------------------------------------------
        // ENGEL KAÇINMA
        // ---------------------------------------------------------------------
        private DecisionCommand Avoid(Insights ins, TaskDefinition task, VehicleState state, double dt)
        {
            double sideSign = ins.ClearanceRight > ins.ClearanceLeft ? +1.0 : -1.0;

            double throttleNorm = 0.10;
            double rudderNorm = 0.55 * sideSign;

            return PlanarToWrench(throttleNorm, rudderNorm, task, state, dt);
        }

        // ---------------------------------------------------------------------
        // HEDEF NAVİGASYONU
        // ---------------------------------------------------------------------
        private DecisionCommand NavigateToTarget(TaskDefinition task, VehicleState state, double dt)
        {
            if (task.Target is not Vec3 target)
                return Zero();

            double dx = target.X - state.Position.X;
            double dy = target.Y - state.Position.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            Vec3 toTargetWorld = new Vec3(dx, dy, 0.0);
            Vec3 toTargetBody = state.Orientation.WorldToBody(toTargetWorld);
            Vec3 velBody = state.Orientation.WorldToBody(state.Velocity);

            double forwardSpeed = velBody.X;
            double yawRate = state.AngularVelocity.Z;
            double absYawRate = Math.Abs(yawRate);

            double targetHeading = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double delta = Normalize(targetHeading - state.Orientation.YawDeg);
            double absDelta = Math.Abs(delta);

            // -------------------------------------------------------------
            // 1) Navigate yaw PD
            // -------------------------------------------------------------
            double rudderNorm = 0.0;

            if (absDelta > RudderDeadbandDeg || absYawRate > YawRateDeadbandDeg)
            {
                double p = delta * NavYawKp;
                double d = -yawRate * NavYawKd;
                rudderNorm = Math.Clamp(p + d, -1.0, 1.0);
            }

            // Hedefe çok yaklaşınca mevcut spin'i daha sert söndür
            if (dist < BrakeRadiusM)
            {
                double nearBrake = delta * NearYawBrakeKp - yawRate * NearYawBrakeKd;
                rudderNorm = Math.Clamp(nearBrake, -1.0, 1.0);
            }

            // -------------------------------------------------------------
            // 2) Temel ileri itki profili
            // -------------------------------------------------------------
            double throttleNorm;
            if (dist >= SlowRadiusM)
            {
                throttleNorm = CruiseThrottleNorm;
            }
            else
            {
                double k = (dist - StopRadiusM) / (SlowRadiusM - StopRadiusM);
                k = Math.Clamp(k, 0.0, 1.0);
                throttleNorm = MinApproachThrottleNorm + k * (CruiseThrottleNorm - MinApproachThrottleNorm);
            }

            // Heading büyük hatadaysa önce dönsün
            throttleNorm *= HeadingScale(absDelta);
            throttleNorm *= HeadingThrottleGate(absDelta, absYawRate);

            if (absDelta >= NearTurnInPlaceDeg)
                throttleNorm = Math.Min(throttleNorm, 0.03);

            // -------------------------------------------------------------
            // 3) Yaklaşma yönü ve aktif frenleme
            // -------------------------------------------------------------
            // toTargetBody.X:
            // > 0 ise hedef burnun önünde
            // < 0 ise hedef burnun arkasında
            double desiredForwardSign = toTargetBody.X >= 0.0 ? 1.0 : -1.0;

            // Yakınken ve ileri hız fazlaysa ters thrust ile frenle
            if (dist < BrakeRadiusM && desiredForwardSign > 0.0)
            {
                double brakeNorm = ComputeApproachBrakeNorm(dist, forwardSpeed);

                if (brakeNorm > 0.0)
                {
                    // İleri komutu yerine aktif fren uygula
                    throttleNorm = -brakeNorm;
                }
            }

            // Heading çok bozukken ve araç hâlâ ileri akıyorsa ileri itkiyi bastır
            if (absDelta > 70.0 && forwardSpeed > 0.35)
            {
                double brakeAssist = Math.Clamp((forwardSpeed - 0.35) / 0.75, 0.0, MaxReverseThrottleNorm * 0.7);
                throttleNorm = Math.Min(throttleNorm, 0.0);
                throttleNorm -= brakeAssist;
            }

            throttleNorm = Math.Clamp(throttleNorm, -MaxReverseThrottleNorm, CruiseThrottleNorm);

            return PlanarToWrench(throttleNorm, rudderNorm, task, state, dt);
        }

        // ---------------------------------------------------------------------
        // STATION KEEPING
        // ---------------------------------------------------------------------
        private DecisionCommand HoldPosition(Vec3 target, VehicleState state, double dt)
        {
            double dx = target.X - state.Position.X;
            double dy = target.Y - state.Position.Y;
            double distXY = Math.Sqrt(dx * dx + dy * dy);

            // Pozisyon hatasını body-frame'e taşı
            Vec3 posErrWorld = new Vec3(dx, dy, 0.0);
            Vec3 posErrBody = state.Orientation.WorldToBody(posErrWorld);

            // Hızları body-frame'de ele al
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);

            bool linearSlow =
                Math.Abs(velB.X) < CloseLinearVelThresh &&
                Math.Abs(velB.Y) < CloseLinearVelThresh &&
                Math.Abs(state.Velocity.Z) < CloseLinearVelThresh;

            bool angularSlow =
                Math.Abs(state.AngularVelocity.X) < CloseAngVelThreshDeg &&
                Math.Abs(state.AngularVelocity.Y) < CloseAngVelThreshDeg &&
                Math.Abs(state.AngularVelocity.Z) < CloseAngVelThreshDeg;

            // Hedefe yeterince yakın ve zaten neredeyse durmuşsak tam zero wrench ver
            if (distXY < CloseEnoughRadiusM && linearSlow && angularSlow)
            {
                ResetHeaveIntegral();

                if (_frozenHoldHeadingDeg is null)
                    FreezeHoldHeading(state.Orientation.YawDeg);

                return Wrench(0, 0, 0, 0, 0, 0);
            }

            double posScale = Math.Clamp(distXY / StopRadiusM, 0.2, 1.0);

            // Pozisyon hatası ve hız body-frame olduğu için PD tutarlı
            double fxNorm = (posErrBody.X * HoldKp - velB.X * HoldKd) * posScale;
            double fyNorm = (posErrBody.Y * HoldKp - velB.Y * HoldKd) * posScale;

            fxNorm = Math.Clamp(fxNorm, -1.0, 1.0);
            fyNorm = Math.Clamp(fyNorm, -1.0, 1.0);

            double fx = fxNorm * MaxFxN;
            double fy = fyNorm * MaxFyN;

            // Hold modunda heading artık hedef noktasına göre değil,
            // hold'a girişte dondurulan mevcut yaw'a göre korunur.
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

            return Wrench(fx, fy, fz, tx, ty, tz);
        }

        // ---------------------------------------------------------------------
        // PLANAR → 6DoF
        // ---------------------------------------------------------------------
        private DecisionCommand PlanarToWrench(double throttleNorm, double rudderNorm,
                                               TaskDefinition task, VehicleState state, double dt)
        {
            var (fy, fz, tx, ty) = ComputeSecondaryAxes(task, state, dt);

            double fx = Math.Clamp(throttleNorm, -1.0, 1.0) * MaxFxN;
            double tz = -Math.Clamp(rudderNorm, -1.0, 1.0) * MaxTzNm;

            return Wrench(
                fx: fx,
                fy: fy,
                fz: fz,
                tx: tx,
                ty: ty,
                tz: tz
            );
        }

        // ---------------------------------------------------------------------
        // İKİNCİL EKSENLER
        // ---------------------------------------------------------------------
        private (double Fy, double Fz, double Tx, double Ty)
            ComputeSecondaryAxes(TaskDefinition task, VehicleState state, double dt)
        {
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);
            double vy = velB.Y;

            double fyNorm = -vy * SwayVelGain;
            fyNorm = Math.Clamp(fyNorm, -MaxSwayNorm, MaxSwayNorm);
            double fy = fyNorm * MaxFyN;

            double txNorm = (-state.Orientation.RollDeg) * AttKp
                            - state.AngularVelocity.X * AttKd;

            double tyNorm = (-state.Orientation.PitchDeg) * AttKp
                            - state.AngularVelocity.Y * AttKd;

            txNorm = Math.Clamp(txNorm, -1.0, 1.0);
            tyNorm = Math.Clamp(tyNorm, -1.0, 1.0);

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
            double error = targetZ - state.Position.Z;
            double vz = state.Velocity.Z;

            _heaveIntegral += error * HeaveKi * dt;
            _heaveIntegral = Math.Clamp(_heaveIntegral, -HeaveImax, HeaveImax);

            double fzNorm = error * HeaveKp
                            + _heaveIntegral
                            - vz * HeaveKd;

            return Math.Clamp(fzNorm, -MaxHeaveNorm, MaxHeaveNorm);
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
        // YARDIMCILAR
        // ---------------------------------------------------------------------
        private static double Normalize(double deg)
        {
            while (deg > 180) deg -= 360;
            while (deg < -180) deg += 360;
            return deg;
        }

        private static double SanitizeDt(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt))
                return 0.1;

            if (dt <= 1e-4)
                return 1e-4;

            if (dt > 0.25)
                return 0.25;

            return dt;
        }

        private double HeadingScale(double absDelta)
        {
            if (absDelta <= 10.0) return 1.0;
            if (absDelta >= 90.0) return 0.22;

            double x = (absDelta - 10.0) / 80.0;
            double s = x * x * (3.0 - 2.0 * x);
            return 1.0 + (0.22 - 1.0) * s;
        }

        /// <summary>
        /// Heading hatasına ve yaw rate'e göre throttle için ek kapı fonksiyonu.
        /// Amaç: büyük açı hatasında önce yönelimi toparlamak.
        /// </summary>
        private double HeadingThrottleGate(double absDeltaDeg, double absYawRateDeg)
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

        private double ComputeApproachBrakeNorm(double dist, double forwardSpeed)
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
    }
}