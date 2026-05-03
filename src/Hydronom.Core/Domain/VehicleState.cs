using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// 6-DoF platform baÄŸÄ±msÄ±z araÃ§ durum modeli.
    ///
    /// Bu model mevcut runtime ve fizik entegrasyonu iÃ§in korunur.
    /// Yeni authoritative operasyonel state modeli ayrÄ±ca Hydronom.Core.State.Models
    /// altÄ±nda tanÄ±mlanÄ±r.
    ///
    /// Frame sÃ¶zleÅŸmesi:
    /// - Position: dÃ¼nya frame
    /// - Orientation: body frame'in dÃ¼nya frame'e gÃ¶re yÃ¶nelimi
    /// - LinearVelocity: dÃ¼nya frame [m/s]
    /// - AngularVelocity: body frame [deg/s]
    /// - LinearForce: dÃ¼nya frame [N]
    /// - AngularTorque: body frame [NÂ·m]
    ///
    /// AÃ§Ä±sal hÄ±z dÄ±ÅŸ arayÃ¼zde deg/s kalÄ±r. Fizik hesabÄ± iÃ§eride rad/s ile yapÄ±lÄ±r.
    /// </summary>
    public readonly record struct VehicleState(
        Vec3 Position,
        Orientation Orientation,
        Vec3 LinearVelocity,
        Vec3 AngularVelocity,
        Vec3 LinearForce,
        Vec3 AngularTorque
    )
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        private const double DefaultMassKg = 1.0;
        private const double MinPositive = 1e-12;

        /// <summary>BaÅŸlangÄ±Ã§ta tÃ¼m bileÅŸenleri sÄ±fÄ±r olan varsayÄ±lan durum.</summary>
        public static VehicleState Zero => new(
            Vec3.Zero,
            Orientation.Zero,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero
        );

        /// <summary>Geriye dÃ¶nÃ¼k uyumluluk iÃ§in X konum alias'Ä±.</summary>
        public double X => Position.X;

        /// <summary>Geriye dÃ¶nÃ¼k uyumluluk iÃ§in Y konum alias'Ä±.</summary>
        public double Y => Position.Y;

        /// <summary>Geriye dÃ¶nÃ¼k uyumluluk iÃ§in Z konum alias'Ä±.</summary>
        public double Z => Position.Z;

        /// <summary>Planar modÃ¼ller iÃ§in X-Y dÃ¼zlemi konum yardÄ±mcÄ±sÄ±.</summary>
        public Vec2 Position2D => new(Position.X, Position.Y);

        /// <summary>Geriye dÃ¶nÃ¼k uyumluluk iÃ§in Velocity alias'Ä±.</summary>
        public Vec3 Velocity => LinearVelocity;

        /// <summary>Geriye dÃ¶nÃ¼k uyumluluk iÃ§in AngularRate alias'Ä±.</summary>
        public Vec3 AngularRate => AngularVelocity;

        /// <summary>
        /// Durumun temel sayÄ±sal saÄŸlÄ±ÄŸÄ±nÄ± kontrol eder.
        /// NaN veya Infinity iÃ§eren state fizik Ã§ekirdeÄŸine girmemelidir.
        /// </summary>
        public bool IsFinite =>
            IsFiniteVec(Position) &&
            Orientation.IsFinite &&
            IsFiniteVec(LinearVelocity) &&
            IsFiniteVec(AngularVelocity) &&
            IsFiniteVec(LinearForce) &&
            IsFiniteVec(AngularTorque);

        /// <summary>
        /// Kuvvet ve moment bileÅŸenlerini sÄ±fÄ±rlar.
        /// Konum, yÃ¶nelim ve hÄ±zlar korunur.
        /// </summary>
        public VehicleState ClearForces() =>
            this with
            {
                LinearForce = Vec3.Zero,
                AngularTorque = Vec3.Zero
            };

        /// <summary>
        /// NaN / Infinity gibi sayÄ±sal bozulmalarÄ± temizler.
        /// </summary>
        public VehicleState Sanitized()
        {
            return new VehicleState(
                SanitizeVec(Position),
                Orientation.Sanitized(),
                SanitizeVec(LinearVelocity),
                SanitizeVec(AngularVelocity),
                SanitizeVec(LinearForce),
                SanitizeVec(AngularTorque)
            );
        }

        /// <summary>
        /// DÄ±ÅŸ pose kaynaÄŸÄ±ndan durum gÃ¼ncellemesi iÃ§in yardÄ±mcÄ± metot.
        ///
        /// Bu metot geriye dÃ¶nÃ¼k uyumluluk iÃ§in korunur.
        /// Yeni authoritative pipeline'da dÄ±ÅŸ kaynaklar doÄŸrudan bu metodu Ã§aÄŸÄ±rmak yerine
        /// StateUpdateCandidate Ã¼retip StateAuthorityManager Ã¼zerinden geÃ§melidir.
        /// </summary>
        public VehicleState WithExternalPose(
            double x,
            double y,
            double? z,
            double yawDeg,
            double? rollDeg = null,
            double? pitchDeg = null,
            Vec3? linearVelocity = null,
            Vec3? angularVelocity = null
        )
        {
            var next = this with
            {
                Position = new Vec3(
                    SanitizeScalar(x),
                    SanitizeScalar(y),
                    z.HasValue ? SanitizeScalar(z.Value) : Position.Z
                ),
                Orientation = new Orientation(
                    rollDeg.HasValue ? SanitizeScalar(rollDeg.Value) : Orientation.RollDeg,
                    pitchDeg.HasValue ? SanitizeScalar(pitchDeg.Value) : Orientation.PitchDeg,
                    NormalizeDeg(SanitizeScalar(yawDeg))
                ),
                LinearVelocity = linearVelocity.HasValue ? SanitizeVec(linearVelocity.Value) : LinearVelocity,
                AngularVelocity = angularVelocity.HasValue ? SanitizeVec(angularVelocity.Value) : AngularVelocity
            };

            return next.Sanitized();
        }

        /// <summary>
        /// Eski runtime akÄ±ÅŸÄ±nÄ± bozmayan temel entegrasyon metodu.
        ///
        /// Yeni kullanÄ±mda IntegrateAdvanced / IntegrateWithReport tercih edilmelidir.
        /// </summary>
        public VehicleState Integrate(double dt, double mass, Vec3 inertia)
        {
            var body = new RigidBodyProperties(
                MassKg: mass,
                InertiaBody: inertia,
                MaxLinearSpeed: 100.0,
                MaxAngularSpeedDeg: 720.0
            );

            var loads = new PhysicsLoads(
                ForceWorld: LinearForce,
                TorqueBody: AngularTorque
            );

            var options = PhysicsIntegrationOptions.Default;

            return IntegrateAdvanced(dt, body, loads, options, out _);
        }

        /// <summary>
        /// Rapor Ã¼reten genel amaÃ§lÄ± 6-DoF entegrasyon metodu.
        /// </summary>
        public VehicleState IntegrateWithReport(
            double dt,
            RigidBodyProperties body,
            PhysicsLoads loads,
            out PhysicsStepReport report
        )
        {
            return IntegrateAdvanced(dt, body, loads, PhysicsIntegrationOptions.Default, out report);
        }

        /// <summary>
        /// Platform baÄŸÄ±msÄ±z geliÅŸmiÅŸ 6-DoF entegrasyon metodu.
        /// </summary>
        public VehicleState IntegrateAdvanced(
            double dt,
            RigidBodyProperties body,
            PhysicsLoads loads,
            PhysicsIntegrationOptions options,
            out PhysicsStepReport report
        )
        {
            var safeOptions = options.Sanitized();
            var safeBody = body.Sanitized();
            var safeLoads = loads.Sanitized();

            if (dt <= 0.0 || !double.IsFinite(dt))
            {
                report = PhysicsStepReport.NoStep(this, dt, "INVALID_DT");
                return this;
            }

            double safeDt = Math.Min(dt, safeOptions.MaxTimeStep);
            var current = Sanitized();

            double invMass = 1.0 / Math.Max(safeBody.MassKg, DefaultMassKg);

            var forceWorld = ClampMagnitude(safeLoads.ForceWorld, safeOptions.MaxForceMagnitude);
            var linearAccWorld = forceWorld * invMass;

            var oldVelWorld = current.LinearVelocity;
            var oldPosWorld = current.Position;

            Vec3 newVelWorld;
            Vec3 newPosWorld;

            if (safeOptions.IntegrationMode == PhysicsIntegrationMode.ExplicitEuler)
            {
                newPosWorld = oldPosWorld + oldVelWorld * safeDt;
                newVelWorld = oldVelWorld + linearAccWorld * safeDt;
            }
            else
            {
                newVelWorld = oldVelWorld + linearAccWorld * safeDt;
                newPosWorld = oldPosWorld + newVelWorld * safeDt;
            }

            newVelWorld = ClampMagnitude(newVelWorld, safeBody.MaxLinearSpeed);

            var inertia = safeBody.InertiaBody;
            var oldOmegaRad = current.AngularVelocity * DegToRad;

            var torqueBody = ClampMagnitude(safeLoads.TorqueBody, safeOptions.MaxTorqueMagnitude);
            var effectiveTorqueBody = torqueBody;

            if (safeOptions.EnableGyroscopicTerm)
            {
                var iOmega = ComponentMultiply(inertia, oldOmegaRad);
                var gyro = Cross(oldOmegaRad, iOmega);
                effectiveTorqueBody = torqueBody - gyro;
            }

            var angularAccRad = ComponentDivide(effectiveTorqueBody, inertia);

            Vec3 integrationOmegaRad;
            Vec3 newOmegaRad;

            if (safeOptions.IntegrationMode == PhysicsIntegrationMode.ExplicitEuler)
            {
                integrationOmegaRad = oldOmegaRad;
                newOmegaRad = oldOmegaRad + angularAccRad * safeDt;
            }
            else
            {
                newOmegaRad = oldOmegaRad + angularAccRad * safeDt;
                integrationOmegaRad = newOmegaRad;
            }

            double maxAngularSpeedRad = safeBody.MaxAngularSpeedDeg * DegToRad;
            newOmegaRad = ClampComponents(newOmegaRad, -maxAngularSpeedRad, maxAngularSpeedRad);
            integrationOmegaRad = ClampComponents(integrationOmegaRad, -maxAngularSpeedRad, maxAngularSpeedRad);

            var newOrientation = current.Orientation.IntegrateBodyAngularVelocityRad(integrationOmegaRad, safeDt);

            var next = current with
            {
                Position = newPosWorld,
                Orientation = newOrientation,
                LinearVelocity = newVelWorld,
                AngularVelocity = newOmegaRad * RadToDeg,
                LinearForce = current.LinearForce,
                AngularTorque = current.AngularTorque
            };

            next = next.Sanitized();

            report = new PhysicsStepReport(
                WasIntegrated: true,
                Reason: "OK",
                DtRequested: dt,
                DtUsed: safeDt,
                StateBefore: current,
                StateAfter: next,
                ForceWorld: forceWorld,
                TorqueBody: torqueBody,
                EffectiveTorqueBody: effectiveTorqueBody,
                LinearAccelerationWorld: linearAccWorld,
                AngularAccelerationBodyRad: angularAccRad,
                LinearSpeed: Magnitude(newVelWorld),
                AngularSpeedDeg: Magnitude(newOmegaRad) * RadToDeg,
                LinearSpeedLimited: Magnitude(newVelWorld) >= safeBody.MaxLinearSpeed - 1e-9,
                AngularSpeedLimited: Magnitude(newOmegaRad) >= maxAngularSpeedRad - 1e-9,
                UsedGyroscopicTerm: safeOptions.EnableGyroscopicTerm,
                IntegrationMode: safeOptions.IntegrationMode
            );

            return next;
        }

        /// <summary>
        /// Deniz / sualtÄ± akÄ±ÅŸkan ortamlarÄ± iÃ§in geriye dÃ¶nÃ¼k uyumluluk metodu.
        /// </summary>
        public VehicleState IntegrateMarine(
            double dt,
            double mass,
            Vec3 inertia,
            Vec3 linearDragBody,
            Vec3 quadraticDragBody,
            Vec3 angularLinearDragBody,
            Vec3 angularQuadraticDragBody,
            double maxLinearSpeed = 50.0,
            double maxAngularSpeedDeg = 720.0
        )
        {
            if (dt <= 0.0 || !double.IsFinite(dt))
                return this;

            var current = Sanitized();

            var externalForceBody = current.Orientation.WorldToBody(current.LinearForce);
            var bodyVelocity = current.Orientation.WorldToBody(current.LinearVelocity);

            var dragForceBody = ComputeLinearQuadraticResistance(
                bodyVelocity,
                linearDragBody,
                quadraticDragBody
            );

            var netForceBody = externalForceBody + dragForceBody;
            var netForceWorld = current.Orientation.BodyToWorld(netForceBody);

            var omegaRad = current.AngularVelocity * DegToRad;

            var dragTorqueBody = ComputeLinearQuadraticResistance(
                omegaRad,
                angularLinearDragBody,
                angularQuadraticDragBody
            );

            var netTorqueBody = current.AngularTorque + dragTorqueBody;

            var body = new RigidBodyProperties(
                MassKg: mass,
                InertiaBody: inertia,
                MaxLinearSpeed: maxLinearSpeed,
                MaxAngularSpeedDeg: maxAngularSpeedDeg
            );

            var loads = new PhysicsLoads(
                ForceWorld: netForceWorld,
                TorqueBody: netTorqueBody
            );

            var options = PhysicsIntegrationOptions.Default with
            {
                EnableGyroscopicTerm = true
            };

            return current.IntegrateAdvanced(dt, body, loads, options, out _);
        }

        private static Vec3 ComputeLinearQuadraticResistance(Vec3 velocity, Vec3 linear, Vec3 quadratic)
        {
            return new Vec3(
                -(SafeAbs(linear.X) * velocity.X + SafeAbs(quadratic.X) * velocity.X * Math.Abs(velocity.X)),
                -(SafeAbs(linear.Y) * velocity.Y + SafeAbs(quadratic.Y) * velocity.Y * Math.Abs(velocity.Y)),
                -(SafeAbs(linear.Z) * velocity.Z + SafeAbs(quadratic.Z) * velocity.Z * Math.Abs(velocity.Z))
            );
        }

        private static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        private static Vec3 ComponentMultiply(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.X * b.X,
                a.Y * b.Y,
                a.Z * b.Z
            );
        }

        private static Vec3 ComponentDivide(Vec3 numerator, Vec3 denominator)
        {
            return new Vec3(
                numerator.X / SafePositive(denominator.X, 1.0),
                numerator.Y / SafePositive(denominator.Y, 1.0),
                numerator.Z / SafePositive(denominator.Z, 1.0)
            );
        }

        private static Vec3 ClampMagnitude(Vec3 v, double maxMagnitude)
        {
            if (maxMagnitude <= 0.0 || !double.IsFinite(maxMagnitude))
                return v;

            double magSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            double maxSq = maxMagnitude * maxMagnitude;

            if (!double.IsFinite(magSq) || magSq <= maxSq)
                return v;

            double mag = Math.Sqrt(magSq);
            if (mag < MinPositive)
                return v;

            double scale = maxMagnitude / mag;
            return new Vec3(v.X * scale, v.Y * scale, v.Z * scale);
        }

        private static Vec3 ClampComponents(Vec3 v, double min, double max)
        {
            return new Vec3(
                Clamp(v.X, min, max),
                Clamp(v.Y, min, max),
                Clamp(v.Z, min, max)
            );
        }

        private static double Magnitude(Vec3 v)
        {
            double s = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            return s <= 0.0 || !double.IsFinite(s) ? 0.0 : Math.Sqrt(s);
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                SanitizeScalar(v.X),
                SanitizeScalar(v.Y),
                SanitizeScalar(v.Z)
            );
        }

        private static bool IsFiniteVec(Vec3 v) =>
            double.IsFinite(v.X) &&
            double.IsFinite(v.Y) &&
            double.IsFinite(v.Z);

        private static double SanitizeScalar(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Abs(value) < MinPositive ? fallback : value;
        }

        private static double SafeAbs(double value)
        {
            return double.IsFinite(value) ? Math.Abs(value) : 0.0;
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

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
