/*using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// SimpleDecision v4.2 (Hydronom için optimize, fiziksel wrench ölçeği ile)
    /// ------------------------------------------------------------
    /// - Navigasyon davranışı 6DoF olarak hesaplanır.
    /// - X-Y pozisyonu için aktif station-keeping PD kontrolü eklendi.
    /// - Heave (Z) için PID (Kp + Kd + Ki + anti-windup) eklendi.
    /// - Roll ve Pitch için PD stabilizasyonu eklendi.
    /// - Lateral sway için hem velocity damping hem de position-hold eklendi.
    /// - Hard-turn throttle kırpma yumuşatıldı.
    /// - Hedef yakınlığında kafa karışması engellendi.
    /// - ÇIKTI: DecisionCommand NORMALİZE değil, fiziksel ölçekli wrench üretir.
    ///   Fx,Fy,Fz (N), Tx,Ty,Tz (Nm) eksen kapasitesine göre ölçeklenir.
    ///
    /// v4.1:
    /// - Heading hatası büyüğken throttle agresif şekilde kısılır.
    ///   Böylece tekne hedef arkadayken ileri kaçmak yerine yerinde dönmeye yakın davranır.
    ///
    /// v4.2:
    /// - Küçük heading hatalarında zorunlu rudder bias kaldırıldı.
    ///   absDelta < RudderDeadbandDeg iken dümen tamamen serbest (0.0).
    ///   Böylece “default sağa/sola akma” davranışı ortadan kalkar.
    /// ------------------------------------------------------------
    /// </summary>
    public class SimpleDecision : IDecisionModule
    {
        // ---------------------------------------------------------------------
        // EKSEN KAPASİTELERİ (FİZİKSEL)  → ActuatorManager.ControlAuthorityProfile ile uyumlu
        // ---------------------------------------------------------------------

        private const double MaxFxN  = 28.28; // Surge (+X) azami kuvvet
        private const double MaxFyN  = 14.14; // Sway  (±Y) azami kuvvet
        private const double MaxFzN  = 40.00; // Heave (-Z/+Z) azami kuvvet

        private const double MaxTxNm = 6.00;  // Roll  azami tork
        private const double MaxTyNm = 8.00;  // Pitch azami tork
        private const double MaxTzNm = 9.90;  // Yaw   azami tork

        // ---------------------------------------------------------------------
        // TEMEL PARAMETRELER
        // ---------------------------------------------------------------------

        private const double SlowRadiusM = 6.0;
        private const double StopRadiusM = 0.9;

        // Throttle normalize alanında (0..1). Fx = throttleNorm * MaxFxN ile fiziğe gider.
        private const double CruiseThrottleNorm      = 0.65;
        private const double MinApproachThrottleNorm = 0.06;

        private const double RudderFullAtDeg   = 45.0; // rudderNorm = delta / 45°
        private const double RudderDeadbandDeg = 1.5;  // v4.2: biraz genişletildi
        private const double MinRudderBias     = 0.0;  // v4.2: fiilen kullanılmıyor (bias yok)

        // Turn scaling
        private const double TurnDeltaLoDeg = 10.0;
        private const double TurnDeltaHiDeg = 90.0;
        private const double TurnScaleMin   = 0.20;

        // Hard turn
        private const double HardTurnThreshDeg  = 110.0;
        private const double HardTurnExtraScale = 1.0; // v4.1: artık ekstra boost vermiyoruz

        // Roll/pitch PD (normalize alanında)
        private const double AttKp = 0.035;
        private const double AttKd = 0.02;

        // Sway control (normalize alanında)
        private const double SwayVelGain = 0.15;    // damping
        private const double SwayPosGain = 0.20;    // (ileride yan pozisyon telafisi için)
        private const double MaxSwayNorm = 0.9;     // |Fy_norm| ≤ 0.9 → Fy = Fy_norm * MaxFyN

        // Station keeping X-Y (normalize alanında)
        private const double HoldKp = 0.45;
        private const double HoldKd = 0.30;

        // Yaw PD (normalize alanında)
        private const double YawKp = 0.015;
        private const double YawKd = 0.008;

        // -------------------
        // Heave (Z) PID (normalize alanında)
        // -------------------
        private double _heaveIntegral = 0.0;
        private const double HeaveKp      = 0.25;
        private const double HeaveKd      = 0.50;
        private const double HeaveKi      = 0.02;
        private const double HeaveImax    = 0.2; // integral normalize sınırı
        private const double MaxHeaveNorm = 1.0; // |Fz_norm| ≤ 1.0 → Fz = Fz_norm * MaxFzN

        // ---------------------------------------------------------------------
        // KARAR VERİCİ
        // ---------------------------------------------------------------------
        public DecisionCommand Decide(Insights insights, TaskDefinition? task, VehicleState state)
        {
            if (task is null)
                return Zero();

            // 1) ENGEL KAÇINMA
            if (insights.HasObstacleAhead)
                return Avoid(insights, task, state);

            // 2) HEDEF → 3D Vec3
            if (task.Target is not Vec3 target)
                return Zero();

            // Pozisyon farkları
            double dx = target.X - state.Position.X;
            double dy = target.Y - state.Position.Y;
            double distXY = Math.Sqrt(dx * dx + dy * dy);

            // 3) Stop bölgesi: FULL station keeping
            if (distXY <= StopRadiusM)
                return HoldPosition(target, state);

            // 4) Yönelme + yaklaşım
            return NavigateToTarget(task, state);
        }

        // ---------------------------------------------------------------------
        // GENEL WRENCH OLUŞTURUCU (FİZİKSEL N / Nm)
        // ---------------------------------------------------------------------
        private static DecisionCommand Wrench(double fx, double fy, double fz,
                                              double tx, double ty, double tz)
        {
            return new DecisionCommand
            {
                Fx = fx,
                Fy = fy,
                Fz = fz,
                Tx = tx,
                Ty = ty,
                Tz = tz
            };
        }

        private static DecisionCommand Zero() => Wrench(0, 0, 0, 0, 0, 0);

        // ---------------------------------------------------------------------
        // ENGEL KAÇINMA
        // ---------------------------------------------------------------------
        private DecisionCommand Avoid(Insights ins, TaskDefinition task, VehicleState state)
        {
            double sideSign =
                ins.ClearanceRight > ins.ClearanceLeft ? +1 : -1;

            // Normalize komutlar
            double throttleNorm = 0.15;
            double rudderNorm   = 0.55 * sideSign;

            return PlanarToWrench(throttleNorm, rudderNorm, task, state);
        }

        // ---------------------------------------------------------------------
        // HEDEF NAVİGASYONU
        //  - Artık TaskDefinition alıyor; yeni TaskDefinition yaratmıyoruz.
        //  - v4.1: Heading büyükken throttle sert şekilde kısılıyor.
        //  - v4.2: Küçük heading hatalarında rudder tamamen 0 (bias yok).
        // ---------------------------------------------------------------------
        private DecisionCommand NavigateToTarget(TaskDefinition task, VehicleState state)
        {
            if (task.Target is not Vec3 target)
                return Zero();

            double dx = target.X - state.Position.X;
            double dy = target.Y - state.Position.Y;

            // Heading
            double targetHeading =
                Math.Atan2(dy, dx) * 180.0 / Math.PI;

            double delta    = Normalize(targetHeading - state.Orientation.YawDeg);
            double absDelta = Math.Abs(delta);

            // Dümen (normalize)
            double rudderNorm = 0.0;

            // Küçük açı hatalarında dümen tamamen bırakılır (bias yok).
            if (absDelta > RudderDeadbandDeg)
            {
                rudderNorm = Math.Clamp(delta / RudderFullAtDeg, -1.0, 1.0);
            }

            // Mesafe
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // 1) Mesafeye göre temel throttle (normalize)
            double throttleNorm;
            if (dist >= SlowRadiusM)
            {
                throttleNorm = CruiseThrottleNorm;
            }
            else
            {
                double k = (dist - StopRadiusM) / (SlowRadiusM - StopRadiusM);
                throttleNorm = MinApproachThrottleNorm + k * (CruiseThrottleNorm - MinApproachThrottleNorm);
            }

            // 2) HeadingScale (küçük açı hatalarında çok kısmayan, yumuşak ölçek)
            throttleNorm *= HeadingScale(absDelta);

            // 3) Büyük açı hatası için agresif heading gate
            //    |dHead| büyüdükçe throttle çarpanı düşüyor.
            throttleNorm *= HeadingThrottleGate(absDelta);

            // 4) Hard turn → eski kodda boost ediyordu; v4.1/4.2'de ekstra scale 1.0
            if (absDelta >= HardTurnThreshDeg)
                throttleNorm *= HardTurnExtraScale;

            // 5) Son kırpma: heading gate sonrası alt sınır 0.0, üst sınır cruise.
            throttleNorm = Math.Clamp(throttleNorm, 0.0, CruiseThrottleNorm);

            // Normalize komutları fiziksel wrench'e çevir.
            return PlanarToWrench(throttleNorm, rudderNorm, task, state);
        }

        // ---------------------------------------------------------------------
        // STATION KEEPING (X-Y + YAW + ROLL/PITCH + DEPTH)
        // ---------------------------------------------------------------------
        private DecisionCommand HoldPosition(Vec3 target, VehicleState state)
        {
            // --- Pozisyon PD --- (Dünya frame)
            double dx = target.X - state.Position.X;
            double dy = target.Y - state.Position.Y;

            // Dünya hızları → Body frame
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);

            // Fx/Fy PD (station keeping) → normalize alanında
            double fxNorm = dx * HoldKp - velB.X * HoldKd;
            double fyNorm = dy * HoldKp - velB.Y * HoldKd;

            fxNorm = Math.Clamp(fxNorm, -1.0, 1.0);
            fyNorm = Math.Clamp(fyNorm, -1.0, 1.0);

            // Fiziksel kuvvetler
            double fx = fxNorm * MaxFxN;
            double fy = fyNorm * MaxFyN;

            // --- Yaw PD --- normalize
            double targetHeading =
                Math.Atan2(dy, dx) * 180.0 / Math.PI;

            double dyaw    = Normalize(targetHeading - state.Orientation.YawDeg);
            double yawRate = state.AngularVelocity.Z;

            double tzNorm = dyaw * YawKp - yawRate * YawKd;
            tzNorm = Math.Clamp(tzNorm, -1.0, 1.0);

            double tz = tzNorm * MaxTzNm;

            // --- Attitude PD (Roll/Pitch) normalize ---
            double rollRate  = state.AngularVelocity.X;
            double pitchRate = state.AngularVelocity.Y;

            double txNorm = (-state.Orientation.RollDeg) * AttKp - rollRate * AttKd;
            double tyNorm = (-state.Orientation.PitchDeg) * AttKp - pitchRate * AttKd;

            txNorm = Math.Clamp(txNorm, -1.0, 1.0);
            tyNorm = Math.Clamp(tyNorm, -1.0, 1.0);

            double tx = txNorm * MaxTxNm;
            double ty = tyNorm * MaxTyNm;

            // --- Depth PID --- normalize → fiziksel
            double fzNorm = ComputeHeave(target.Z, state);
            double fz     = fzNorm * MaxFzN;

            return Wrench(fx, fy, fz, tx, ty, tz);
        }

        // ---------------------------------------------------------------------
        // THROTTLE + RUDDER (NORMALİZE) → 6DoF WRENCH (FİZİKSEL)
        // ---------------------------------------------------------------------
        private DecisionCommand PlanarToWrench(double throttleNorm, double rudderNorm,
                                               TaskDefinition task, VehicleState state)
        {
            // İkincil eksenler (sway, heave, roll, pitch) fiziksel olarak hesaplanır
            var (fy, fz, tx, ty) = ComputeSecondaryAxes(task, state);

            // İleri kuvvet: throttleNorm ∈ [0..1] → Fx (N)
            double fx = Math.Clamp(throttleNorm, -1.0, 1.0) * MaxFxN;

            // Yaw torku: rudderNorm ∈ [-1..1] → Tz (Nm)
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
        // DEPTH + SWAY + ROLL/PITCH STABILIZATION
        // ---------------------------------------------------------------------
        private (double Fy, double Fz, double Tx, double Ty)
            ComputeSecondaryAxes(TaskDefinition task, VehicleState state)
        {
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);
            double vy = velB.Y;

            // Sway damping (normalize)
            double fyNorm = -vy * SwayVelGain;
            fyNorm = Math.Clamp(fyNorm, -MaxSwayNorm, MaxSwayNorm);
            double fy = fyNorm * MaxFyN;

            // Roll/Pitch PD (normalize)
            double txNorm = (-state.Orientation.RollDeg) * AttKp
                            - state.AngularVelocity.X * AttKd;

            double tyNorm = (-state.Orientation.PitchDeg) * AttKp
                            - state.AngularVelocity.Y * AttKd;

            txNorm = Math.Clamp(txNorm, -1.0, 1.0);
            tyNorm = Math.Clamp(tyNorm, -1.0, 1.0);

            double tx = txNorm * MaxTxNm;
            double ty = tyNorm * MaxTyNm;

            // Heave PID sadece görevde Z varsa
            double fz = 0.0;
            if (task.Target is Vec3 t3d)
            {
                double fzNorm = ComputeHeave(t3d.Z, state);
                fz = fzNorm * MaxFzN;
            }

            return (fy, fz, tx, ty);
        }

        // ---------------------------------------------------------------------
        // HEAVE PID (ANTI-WINDUP, NORMALİZE ÇIKIŞ)
        // ---------------------------------------------------------------------
        private double ComputeHeave(double targetZ, VehicleState state)
        {
            double error = targetZ - state.Position.Z;
            double vz    = state.Velocity.Z;

            _heaveIntegral += error * HeaveKi;
            _heaveIntegral = Math.Clamp(_heaveIntegral, -HeaveImax, HeaveImax);

            double fzNorm = error * HeaveKp
                            + _heaveIntegral
                            - vz * HeaveKd;

            return Math.Clamp(fzNorm, -MaxHeaveNorm, MaxHeaveNorm);
        }

        // ---------------------------------------------------------------------
        // AÇI NORMALİZASYONU
        // ---------------------------------------------------------------------
        private static double Normalize(double deg)
        {
            while (deg > 180) deg -= 360;
            while (deg < -180) deg += 360;
            return deg;
        }

        private double HeadingScale(double absDelta)
        {
            if (absDelta <= TurnDeltaLoDeg) return 1.0;
            if (absDelta >= TurnDeltaHiDeg) return TurnScaleMin;

            double x = (absDelta - TurnDeltaLoDeg)
                        / (TurnDeltaHiDeg - TurnDeltaLoDeg);

            double s = x * x * (3 - 2 * x);
            return 1.0 + (TurnScaleMin - 1.0) * s;
        }

        /// <summary>
        /// Heading hatasına göre throttle için ek "kapı" fonksiyonu.
        /// Amaç: |dHead| büyükken önce kafayı çevir, ileri kaçma.
        /// </summary>
        private double HeadingThrottleGate(double absDeltaDeg)
        {
            // Çok arkaya bakıyorsak neredeyse dur
            if (absDeltaDeg >= 150.0)
                return 0.025;   // sadece çok hafif sürünme

            // 120–150° arası → çok düşük gaz
            if (absDeltaDeg >= 120.0)
                return 0.10;

            // 90–120° arası → düşük gaz
            if (absDeltaDeg >= 90.0)
                return 0.25;

            // 60–90° arası → orta kırpma
            if (absDeltaDeg >= 60.0)
                return 0.45;

            // 30–60° arası → orta kırpma
            if (absDeltaDeg >= 30.0)
                return 0.65;

            // 30° altı → normal yaklaşma
            return 1.0;
        }
    }
}
*/