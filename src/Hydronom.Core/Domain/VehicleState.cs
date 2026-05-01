using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// 6-DoF platform bağımsız araç durum modeli.
    ///
    /// Bu model Hydronom çekirdeğinde tek bir platforma bağlı değildir.
    /// Kara, deniz, hava, sualtı, fabrika içi AGV, paletli araç veya simülasyon
    /// platformları aynı temel fiziksel durum temsilini kullanabilir.
    ///
    /// Önemli frame sözleşmesi:
    /// - Position: dünya frame
    /// - Orientation: body frame'in dünya frame'e göre yönelimi
    /// - LinearVelocity: dünya frame [m/s]
    /// - AngularVelocity: body frame [deg/s], geriye dönük uyumluluk için derece/saniye tutulur
    /// - LinearForce: dünya frame [N]
    /// - AngularTorque: body frame [N·m]
    ///
    /// Not:
    /// Açısal hız dış arayüzde deg/s kalır. Fizik hesabı içeride rad/s ile yapılır.
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

        /// <summary>Başlangıçta tüm bileşenleri sıfır olan varsayılan durum.</summary>
        public static VehicleState Zero => new(
            Vec3.Zero,
            Orientation.Zero,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero
        );

        /// <summary>Geriye dönük uyumluluk için X konum alias'ı.</summary>
        public double X => Position.X;

        /// <summary>Geriye dönük uyumluluk için Y konum alias'ı.</summary>
        public double Y => Position.Y;

        /// <summary>Geriye dönük uyumluluk için Z konum alias'ı.</summary>
        public double Z => Position.Z;

        /// <summary>Planar modüller için X-Y düzlemi konum yardımcısı.</summary>
        public Vec2 Position2D => new(Position.X, Position.Y);

        /// <summary>Geriye dönük uyumluluk için Velocity alias'ı.</summary>
        public Vec3 Velocity => LinearVelocity;

        /// <summary>Geriye dönük uyumluluk için AngularRate alias'ı.</summary>
        public Vec3 AngularRate => AngularVelocity;

        /// <summary>
        /// Durumun temel sayısal sağlığını kontrol eder.
        /// NaN veya Infinity içeren state fizik çekirdeğine girmemelidir.
        /// </summary>
        public bool IsFinite =>
            IsFiniteVec(Position) &&
            Orientation.IsFinite &&
            IsFiniteVec(LinearVelocity) &&
            IsFiniteVec(AngularVelocity) &&
            IsFiniteVec(LinearForce) &&
            IsFiniteVec(AngularTorque);

        /// <summary>
        /// Kuvvet ve moment bileşenlerini sıfırlar.
        /// Konum, yönelim ve hızlar korunur.
        /// </summary>
        public VehicleState ClearForces() =>
            this with
            {
                LinearForce = Vec3.Zero,
                AngularTorque = Vec3.Zero
            };

        /// <summary>
        /// NaN / Infinity gibi sayısal bozulmaları temizler.
        /// Bu metot özellikle replay, simülasyon, sensör hatası veya dış pose override sonrası
        /// güvenli çalışma için kullanılabilir.
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
        /// Dış bir pose kaynağından durum ezmesi yapılırken kullanılan yardımcı metot.
        /// GPS, SLAM, external tracker veya twin senkronizasyonu gibi kaynaklar için uygundur.
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
        /// Eski runtime akışını bozmayan temel entegrasyon metodu.
        ///
        /// Geriye dönük uyumluluk için korunur.
        /// Yeni kullanımda IntegrateAdvanced / IntegrateWithReport tercih edilmelidir.
        /// </summary>
        /// <param name="dt">Zaman adımı [s]</param>
        /// <param name="mass">Kütle [kg]</param>
        /// <param name="inertia">Body frame diagonal atalet momenti [kg·m²]</param>
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
        /// Rapor üreten genel amaçlı 6-DoF entegrasyon metodu.
        ///
        /// Bu metot platform bağımsızdır. Deniz, kara, hava veya fabrika aracı fark etmez.
        /// Platforma özel sürüklenme, kaldırma, yer teması, akıntı, rüzgar gibi etkiler
        /// dışarıda PhysicsLoads olarak hesaplanıp bu metoda verilmelidir.
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
        /// Platform bağımsız gelişmiş 6-DoF entegrasyon metodu.
        ///
        /// Bu metot:
        /// - Sayısal güvenlik uygular.
        /// - dt değerini güvenli aralıkta sınırlar.
        /// - Lineer ve açısal ivmeleri hesaplar.
        /// - İsteğe bağlı gyroscopic coupling uygular.
        /// - Semi-implicit Euler veya explicit Euler ile ilerler.
        /// - Quaternion tabanlı yönelim günceller.
        /// - Sonucu PhysicsStepReport ile açıklanabilir hale getirir.
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

            // -----------------------------------------------------------------
            // 1) Lineer dinamik
            // -----------------------------------------------------------------
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

            // -----------------------------------------------------------------
            // 2) Açısal dinamik
            // -----------------------------------------------------------------
            var inertia = safeBody.InertiaBody;
            var oldOmegaRad = current.AngularVelocity * DegToRad;

            var torqueBody = ClampMagnitude(safeLoads.TorqueBody, safeOptions.MaxTorqueMagnitude);
            var effectiveTorqueBody = torqueBody;

            if (safeOptions.EnableGyroscopicTerm)
            {
                // Rijit cisim rotasyon denklemi:
                // I * omegaDot + omega x (I * omega) = tau
                // omegaDot = I^-1 * (tau - omega x (I * omega))
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
        /// Deniz / sualtı akışkan ortamları için geriye dönük uyumluluk metodu.
        ///
        /// Bu metot korunur; fakat yeni mimaride deniz ortamına özel drag/akıntı/batmazlık
        /// etkilerinin ayrı force model dosyalarında hesaplanıp IntegrateAdvanced metoduna
        /// PhysicsLoads olarak verilmesi önerilir.
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

            // State içindeki kuvvet dünya frame'de taşınır.
            // Marine drag body frame'de hesaplanır.
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

    /// <summary>
    /// Rijit cisim fizik parametreleri.
    ///
    /// Platform bağımsızdır:
    /// - Tekne
    /// - Denizaltı
    /// - Paletli araç
    /// - İHA
    /// - AGV
    /// - Endüstriyel makine
    ///
    /// aynı parametre sözleşmesini kullanabilir.
    /// </summary>
    public readonly record struct RigidBodyProperties(
        double MassKg,
        Vec3 InertiaBody,
        double MaxLinearSpeed = 100.0,
        double MaxAngularSpeedDeg = 720.0
    )
    {
        public static RigidBodyProperties Default => new(
            MassKg: 1.0,
            InertiaBody: new Vec3(1.0, 1.0, 1.0),
            MaxLinearSpeed: 100.0,
            MaxAngularSpeedDeg: 720.0
        );

        public RigidBodyProperties Sanitized()
        {
            return new RigidBodyProperties(
                MassKg: SafePositive(MassKg, 1.0),
                InertiaBody: new Vec3(
                    SafePositive(InertiaBody.X, 1.0),
                    SafePositive(InertiaBody.Y, 1.0),
                    SafePositive(InertiaBody.Z, 1.0)
                ),
                MaxLinearSpeed: SafePositive(MaxLinearSpeed, 100.0),
                MaxAngularSpeedDeg: SafePositive(MaxAngularSpeedDeg, 720.0)
            );
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Abs(value) < 1e-12 ? fallback : Math.Abs(value);
        }
    }

    /// <summary>
    /// Bir fizik adımında uygulanacak dış yükler.
    ///
    /// ForceWorld dünya frame'de, TorqueBody body frame'de tutulur.
    /// Platforma özel modeller toplam yükleri hesaplayıp buraya aktarır.
    /// </summary>
    public readonly record struct PhysicsLoads(
        Vec3 ForceWorld,
        Vec3 TorqueBody
    )
    {
        public static PhysicsLoads Zero => new(Vec3.Zero, Vec3.Zero);

        public PhysicsLoads Sanitized()
        {
            return new PhysicsLoads(
                SanitizeVec(ForceWorld),
                SanitizeVec(TorqueBody)
            );
        }

        public static PhysicsLoads operator +(PhysicsLoads a, PhysicsLoads b)
        {
            return new PhysicsLoads(
                a.ForceWorld + b.ForceWorld,
                a.TorqueBody + b.TorqueBody
            );
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                double.IsFinite(v.X) ? v.X : 0.0,
                double.IsFinite(v.Y) ? v.Y : 0.0,
                double.IsFinite(v.Z) ? v.Z : 0.0
            );
        }
    }

    /// <summary>
    /// Entegrasyon yöntemi.
    /// SemiImplicitEuler varsayılan olarak daha kararlı olduğu için tercih edilir.
    /// </summary>
    public enum PhysicsIntegrationMode
    {
        ExplicitEuler = 0,
        SemiImplicitEuler = 1
    }

    /// <summary>
    /// Fizik entegrasyonu için güvenlik ve yöntem ayarları.
    /// </summary>
    public readonly record struct PhysicsIntegrationOptions(
        PhysicsIntegrationMode IntegrationMode,
        bool EnableGyroscopicTerm,
        double MaxTimeStep,
        double MaxForceMagnitude,
        double MaxTorqueMagnitude
    )
    {
        public static PhysicsIntegrationOptions Default => new(
            IntegrationMode: PhysicsIntegrationMode.SemiImplicitEuler,
            EnableGyroscopicTerm: true,
            MaxTimeStep: 0.05,
            MaxForceMagnitude: 1_000_000.0,
            MaxTorqueMagnitude: 1_000_000.0
        );

        public PhysicsIntegrationOptions Sanitized()
        {
            return new PhysicsIntegrationOptions(
                IntegrationMode,
                EnableGyroscopicTerm,
                MaxTimeStep: SafePositive(MaxTimeStep, 0.05),
                MaxForceMagnitude: SafePositive(MaxForceMagnitude, 1_000_000.0),
                MaxTorqueMagnitude: SafePositive(MaxTorqueMagnitude, 1_000_000.0)
            );
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }
    }

    /// <summary>
    /// Bir fizik entegrasyon adımının açıklanabilir raporu.
    ///
    /// Bu yapı Analysis, Safety, Replay, Simulation ve Diagnostics katmanlarının
    /// "araç neden böyle hareket etti?" sorusunu cevaplamasını sağlar.
    /// </summary>
    public readonly record struct PhysicsStepReport(
        bool WasIntegrated,
        string Reason,
        double DtRequested,
        double DtUsed,
        VehicleState StateBefore,
        VehicleState StateAfter,
        Vec3 ForceWorld,
        Vec3 TorqueBody,
        Vec3 EffectiveTorqueBody,
        Vec3 LinearAccelerationWorld,
        Vec3 AngularAccelerationBodyRad,
        double LinearSpeed,
        double AngularSpeedDeg,
        bool LinearSpeedLimited,
        bool AngularSpeedLimited,
        bool UsedGyroscopicTerm,
        PhysicsIntegrationMode IntegrationMode
    )
    {
        public static PhysicsStepReport NoStep(VehicleState state, double dtRequested, string reason)
        {
            return new PhysicsStepReport(
                WasIntegrated: false,
                Reason: reason,
                DtRequested: dtRequested,
                DtUsed: 0.0,
                StateBefore: state,
                StateAfter: state,
                ForceWorld: Vec3.Zero,
                TorqueBody: Vec3.Zero,
                EffectiveTorqueBody: Vec3.Zero,
                LinearAccelerationWorld: Vec3.Zero,
                AngularAccelerationBodyRad: Vec3.Zero,
                LinearSpeed: 0.0,
                AngularSpeedDeg: 0.0,
                LinearSpeedLimited: false,
                AngularSpeedLimited: false,
                UsedGyroscopicTerm: false,
                IntegrationMode: PhysicsIntegrationMode.SemiImplicitEuler
            );
        }
    }

    /// <summary>
    /// 6-DoF yönelim modeli.
    ///
    /// Dışarıya Euler açıları derece cinsinden verilir.
    /// İçeride quaternion tek gerçek yönelim kaynağı olarak tutulur.
    /// </summary>
    public readonly record struct Orientation
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        public double RollDeg { get; init; }
        public double PitchDeg { get; init; }
        public double YawDeg { get; init; }

        public double Qw { get; init; }
        public double Qx { get; init; }
        public double Qy { get; init; }
        public double Qz { get; init; }

        public static Orientation Zero => new(0.0, 0.0, 0.0);

        public bool IsFinite =>
            double.IsFinite(RollDeg) &&
            double.IsFinite(PitchDeg) &&
            double.IsFinite(YawDeg) &&
            double.IsFinite(Qw) &&
            double.IsFinite(Qx) &&
            double.IsFinite(Qy) &&
            double.IsFinite(Qz);

        /// <summary>
        /// Euler açılarıyla yönelim oluşturur.
        /// </summary>
        public Orientation(double rollDeg, double pitchDeg, double yawDeg)
        {
            rollDeg = SanitizeScalar(rollDeg);
            pitchDeg = SanitizeScalar(pitchDeg);
            yawDeg = SanitizeScalar(yawDeg);

            RollDeg = NormalizeDeg(rollDeg);
            PitchDeg = Clamp(pitchDeg, -90.0, 90.0);
            YawDeg = NormalizeDeg(yawDeg);

            (Qw, Qx, Qy, Qz) = FromEuler(RollDeg, PitchDeg, YawDeg);
        }

        /// <summary>
        /// Quaternion ile yönelim oluşturur.
        /// </summary>
        public Orientation(double qw, double qx, double qy, double qz, bool fromQuaternion)
        {
            qw = SanitizeScalar(qw, 1.0);
            qx = SanitizeScalar(qx);
            qy = SanitizeScalar(qy);
            qz = SanitizeScalar(qz);

            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);

            Qw = qw;
            Qx = qx;
            Qy = qy;
            Qz = qz;

            (var rollDeg, var pitchDeg, var yawDeg) = ToEuler(qw, qx, qy, qz);

            RollDeg = NormalizeDeg(rollDeg);
            PitchDeg = Clamp(pitchDeg, -90.0, 90.0);
            YawDeg = NormalizeDeg(yawDeg);
        }

        /// <summary>
        /// Sayısal olarak bozulmuş orientation değerlerini temizler.
        /// </summary>
        public Orientation Sanitized()
        {
            if (!IsFinite)
                return Zero;

            return new Orientation(Qw, Qx, Qy, Qz, fromQuaternion: true);
        }

        /// <summary>
        /// Vektörü dünya frame'den body frame'e döndürür.
        /// </summary>
        public Vec3 WorldToBody(Vec3 worldVector)
        {
            var safe = Sanitized();
            return RotateByQuaternion(safe.Qw, -safe.Qx, -safe.Qy, -safe.Qz, SanitizeVec(worldVector));
        }

        /// <summary>
        /// Vektörü body frame'den dünya frame'e döndürür.
        /// </summary>
        public Vec3 BodyToWorld(Vec3 bodyVector)
        {
            var safe = Sanitized();
            return RotateByQuaternion(safe.Qw, safe.Qx, safe.Qy, safe.Qz, SanitizeVec(bodyVector));
        }

        /// <summary>
        /// Body frame açısal hız ile quaternion yönelimi entegre eder.
        /// angularVelocityRad body frame'de [rad/s] kabul edilir.
        /// </summary>
        public Orientation IntegrateBodyAngularVelocityRad(Vec3 angularVelocityRad, double dt)
        {
            if (dt <= 0.0 || !double.IsFinite(dt))
                return this;

            var safe = Sanitized();
            var omega = SanitizeVec(angularVelocityRad);

            double omegaMag = Math.Sqrt(
                omega.X * omega.X +
                omega.Y * omega.Y +
                omega.Z * omega.Z
            );

            if (omegaMag < 1e-12)
                return safe;

            double angle = omegaMag * dt;
            double half = angle * 0.5;

            double sinHalf = Math.Sin(half);
            double cosHalf = Math.Cos(half);

            double invOmega = 1.0 / omegaMag;

            var delta = new Orientation(
                cosHalf,
                omega.X * invOmega * sinHalf,
                omega.Y * invOmega * sinHalf,
                omega.Z * invOmega * sinHalf,
                fromQuaternion: true
            );

            return safe.Combine(delta);
        }

        /// <summary>
        /// İki orientation değerini birleştirir.
        /// Sonuç: this ⊗ other
        /// </summary>
        public Orientation Combine(Orientation other)
        {
            var a = Sanitized();
            var b = other.Sanitized();

            double qw = a.Qw * b.Qw - a.Qx * b.Qx - a.Qy * b.Qy - a.Qz * b.Qz;
            double qx = a.Qw * b.Qx + a.Qx * b.Qw + a.Qy * b.Qz - a.Qz * b.Qy;
            double qy = a.Qw * b.Qy - a.Qx * b.Qz + a.Qy * b.Qw + a.Qz * b.Qx;
            double qz = a.Qw * b.Qz + a.Qx * b.Qy - a.Qy * b.Qx + a.Qz * b.Qw;

            return new Orientation(qw, qx, qy, qz, fromQuaternion: true);
        }

        /// <summary>
        /// Body frame ileri ekseninin dünya frame karşılığı.
        /// </summary>
        public Vec3 ForwardWorld => BodyToWorld(new Vec3(1.0, 0.0, 0.0));

        /// <summary>
        /// Body frame sağ ekseninin dünya frame karşılığı.
        /// </summary>
        public Vec3 RightWorld => BodyToWorld(new Vec3(0.0, 1.0, 0.0));

        /// <summary>
        /// Body frame yukarı/aşağı ekseninin dünya frame karşılığı.
        /// </summary>
        public Vec3 UpWorld => BodyToWorld(new Vec3(0.0, 0.0, 1.0));

        public override string ToString()
        {
            return $"R={RollDeg:F1}°, P={PitchDeg:F1}°, Y={YawDeg:F1}°";
        }

        public static (double qw, double qx, double qy, double qz) FromEuler(
            double rollDeg,
            double pitchDeg,
            double yawDeg
        )
        {
            double r = SanitizeScalar(rollDeg) * DegToRad;
            double p = SanitizeScalar(pitchDeg) * DegToRad;
            double y = SanitizeScalar(yawDeg) * DegToRad;

            double cy = Math.Cos(y * 0.5);
            double sy = Math.Sin(y * 0.5);
            double cp = Math.Cos(p * 0.5);
            double sp = Math.Sin(p * 0.5);
            double cr = Math.Cos(r * 0.5);
            double sr = Math.Sin(r * 0.5);

            double qw = cr * cp * cy + sr * sp * sy;
            double qx = sr * cp * cy - cr * sp * sy;
            double qy = cr * sp * cy + sr * cp * sy;
            double qz = cr * cp * sy - sr * sp * cy;

            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);
            return (qw, qx, qy, qz);
        }

        public static (double rollDeg, double pitchDeg, double yawDeg) ToEuler(
            double qw,
            double qx,
            double qy,
            double qz
        )
        {
            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);

            double sinrCosp = 2.0 * (qw * qx + qy * qz);
            double cosrCosp = 1.0 - 2.0 * (qx * qx + qy * qy);
            double roll = Math.Atan2(sinrCosp, cosrCosp);

            double sinp = 2.0 * (qw * qy - qz * qx);
            double pitch = Math.Abs(sinp) >= 1.0
                ? Math.CopySign(Math.PI / 2.0, sinp)
                : Math.Asin(sinp);

            double sinyCosp = 2.0 * (qw * qz + qx * qy);
            double cosyCosp = 1.0 - 2.0 * (qy * qy + qz * qz);
            double yaw = Math.Atan2(sinyCosp, cosyCosp);

            return (
                roll * RadToDeg,
                pitch * RadToDeg,
                yaw * RadToDeg
            );
        }

        private static Vec3 RotateByQuaternion(double qw, double qx, double qy, double qz, Vec3 v)
        {
            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);

            double vx = v.X;
            double vy = v.Y;
            double vz = v.Z;

            double tx = 2.0 * (qy * vz - qz * vy);
            double ty = 2.0 * (qz * vx - qx * vz);
            double tz = 2.0 * (qx * vy - qy * vx);

            double cx = qy * tz - qz * ty;
            double cy = qz * tx - qx * tz;
            double cz = qx * ty - qy * tx;

            return new Vec3(
                vx + qw * tx + cx,
                vy + qw * ty + cy,
                vz + qw * tz + cz
            );
        }

        private static void NormalizeQuaternion(ref double qw, ref double qx, ref double qy, ref double qz)
        {
            double norm = Math.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz);

            if (!double.IsFinite(norm) || norm < 1e-12)
            {
                qw = 1.0;
                qx = 0.0;
                qy = 0.0;
                qz = 0.0;
                return;
            }

            qw /= norm;
            qx /= norm;
            qy /= norm;
            qz /= norm;
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                SanitizeScalar(v.X),
                SanitizeScalar(v.Y),
                SanitizeScalar(v.Z)
            );
        }

        private static double SanitizeScalar(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
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