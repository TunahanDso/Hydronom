using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Platform bağımsız 6-DoF fizik yürütücüsü.
    ///
    /// Bu sınıfın görevi:
    /// - Araç durumunu tek merkezde tutmak
    /// - Body frame'de gelen kuvvet/momentleri doğru frame sözleşmesine çevirmek
    /// - Rijit cisim parametreleriyle fizik entegrasyonu yapmak
    /// - Her fizik adımı için açıklanabilir PhysicsStepReport üretmek
    ///
    /// Bu sınıf deniz, kara, hava veya sualtı ortamına özel fizik yazmaz.
    /// Platforma özel etkiler ayrı force/environment modellerinde hesaplanmalı ve
    /// ApplyWorldLoads / ApplyBodyLoads üzerinden buraya verilmelidir.
    /// </summary>
    public class PhysicsIntegrator
    {
        private const double DefaultMaxLinearSpeed = 100.0;
        private const double DefaultMaxAngularSpeedDeg = 720.0;

        private PhysicsLoads _pendingLoads = PhysicsLoads.Zero;

        /// <summary>
        /// Şu anki 6-DoF araç durumu.
        /// </summary>
        public VehicleState State { get; private set; } = VehicleState.Zero;

        /// <summary>
        /// Son fizik adımının açıklanabilir raporu.
        /// Analysis, Safety, Replay ve Diagnostics katmanları bunu okuyabilir.
        /// </summary>
        public PhysicsStepReport LastStepReport { get; private set; } =
            PhysicsStepReport.NoStep(VehicleState.Zero, 0.0, "NOT_STARTED");

        /// <summary>
        /// [kg] Toplam kütle.
        /// Geriye dönük uyumluluk için korunur.
        /// Yeni kodlarda BodyProperties üzerinden okunur.
        /// </summary>
        public double Mass
        {
            get => BodyProperties.MassKg;
            set => BodyProperties = BodyProperties with { MassKg = value };
        }

        /// <summary>
        /// [kg·m²] Body frame diagonal atalet momenti.
        /// Geriye dönük uyumluluk için korunur.
        /// Yeni kodlarda BodyProperties üzerinden okunur.
        /// </summary>
        public Vec3 Inertia
        {
            get => BodyProperties.InertiaBody;
            set => BodyProperties = BodyProperties with { InertiaBody = value };
        }

        /// <summary>
        /// [s] Varsayılan entegrasyon zaman adımı.
        /// </summary>
        public double TimeStep { get; set; } = 0.01;

        /// <summary>
        /// Platform bağımsız rijit cisim parametreleri.
        /// Tekne, denizaltı, İHA, kara robotu veya AGV aynı sözleşmeyi kullanır.
        /// </summary>
        public RigidBodyProperties BodyProperties { get; private set; }

        /// <summary>
        /// Entegrasyon yöntemi ve sayısal güvenlik ayarları.
        /// </summary>
        public PhysicsIntegrationOptions IntegrationOptions { get; set; } =
            PhysicsIntegrationOptions.Default;

        /// <summary>
        /// Basit sönümleme modu.
        ///
        /// Varsayılan olarak kapalıdır.
        /// Çünkü platform bağımsız çekirdekte sürüklenme/sürtünme bu sınıfa gömülmemelidir.
        /// Eski davranışı kabaca korumak istenirse true yapılabilir.
        /// Gerçek yükseltme için ayrı force model dosyaları kullanılmalıdır.
        /// </summary>
        public bool EnableLegacyDamping { get; set; } = false;

        /// <summary>
        /// [1/s] Eski basit lineer sönüm katsayısı.
        /// Sadece EnableLegacyDamping true ise uygulanır.
        /// </summary>
        public double LegacyLinearDamping { get; set; } = 0.2;

        /// <summary>
        /// [1/s] Eski basit açısal sönüm katsayısı.
        /// Sadece EnableLegacyDamping true ise uygulanır.
        /// </summary>
        public double LegacyAngularDamping { get; set; } = 0.5;

        public PhysicsIntegrator(double mass = 10.0, Vec3? inertia = null)
        {
            BodyProperties = new RigidBodyProperties(
                MassKg: mass,
                InertiaBody: inertia ?? new Vec3(1.0, 1.0, 1.0),
                MaxLinearSpeed: DefaultMaxLinearSpeed,
                MaxAngularSpeedDeg: DefaultMaxAngularSpeedDeg
            ).Sanitized();
        }

        /// <summary>
        /// Rijit cisim parametrelerini tek seferde günceller.
        /// </summary>
        public void ConfigureBody(RigidBodyProperties bodyProperties)
        {
            BodyProperties = bodyProperties.Sanitized();
        }

        /// <summary>
        /// Entegrasyon seçeneklerini tek seferde günceller.
        /// </summary>
        public void ConfigureIntegration(PhysicsIntegrationOptions options)
        {
            IntegrationOptions = options.Sanitized();
        }

        /// <summary>
        /// Dış bir modülden gelen durumu doğrudan set eder.
        /// Estimator, replay, digital twin veya external pose correction için kullanılabilir.
        /// </summary>
        public void ResetState(VehicleState newState, bool clearForces = true)
        {
            var safe = newState.Sanitized();
            State = clearForces ? safe.ClearForces() : safe;

            _pendingLoads = clearForces
                ? PhysicsLoads.Zero
                : new PhysicsLoads(State.LinearForce, State.AngularTorque).Sanitized();

            LastStepReport = PhysicsStepReport.NoStep(State, 0.0, "RESET_STATE");
        }

        /// <summary>
        /// Yalnızca konum ve oryantasyonu günceller.
        /// Hızlar korunur.
        /// GPS/SLAM/vision pose correction gibi durumlarda kullanılır.
        /// </summary>
        public void SetPose(Vec3 position, Orientation orientation)
        {
            State = State with
            {
                Position = position,
                Orientation = orientation.Sanitized()
            };

            State = State.Sanitized();
        }

        /// <summary>
        /// Harici pose kaynağı ile güvenli pose düzeltmesi yapar.
        /// </summary>
        public void SetExternalPose(
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
            State = State.WithExternalPose(
                x,
                y,
                z,
                yawDeg,
                rollDeg,
                pitchDeg,
                linearVelocity,
                angularVelocity
            );
        }

        /// <summary>
        /// Body frame'de kuvvet ve tork uygular.
        ///
        /// Beklenen:
        /// - totalForceBody: body frame [N]
        /// - totalTorqueBody: body frame [N·m]
        ///
        /// Kuvvet dünya frame'e dönüştürülür.
        /// Tork body frame'de tutulur.
        /// </summary>
        public void ApplyForces(Vec3 totalForceBody, Vec3 totalTorqueBody)
        {
            ApplyBodyLoads(totalForceBody, totalTorqueBody, replaceExisting: true);
        }

        /// <summary>
        /// Body frame'de yük uygular.
        /// replaceExisting true ise önceki pending load ezilir.
        /// false ise mevcut yüklerin üzerine eklenir.
        /// </summary>
        public void ApplyBodyLoads(Vec3 forceBody, Vec3 torqueBody, bool replaceExisting = false)
        {
            var forceWorld = State.Orientation.BodyToWorld(forceBody);

            var loads = new PhysicsLoads(
                ForceWorld: forceWorld,
                TorqueBody: torqueBody
            ).Sanitized();

            _pendingLoads = replaceExisting
                ? loads
                : (_pendingLoads + loads).Sanitized();

            State = State with
            {
                LinearForce = _pendingLoads.ForceWorld,
                AngularTorque = _pendingLoads.TorqueBody
            };

            State = State.Sanitized();
        }

        /// <summary>
        /// Dünya frame'de kuvvet ve body frame'de tork uygular.
        /// Çevresel modeller, global rüzgar/akıntı veya dış kuvvetler için uygundur.
        /// </summary>
        public void ApplyWorldLoads(Vec3 forceWorld, Vec3 torqueBody, bool replaceExisting = false)
        {
            var loads = new PhysicsLoads(
                ForceWorld: forceWorld,
                TorqueBody: torqueBody
            ).Sanitized();

            _pendingLoads = replaceExisting
                ? loads
                : (_pendingLoads + loads).Sanitized();

            State = State with
            {
                LinearForce = _pendingLoads.ForceWorld,
                AngularTorque = _pendingLoads.TorqueBody
            };

            State = State.Sanitized();
        }

        /// <summary>
        /// Daha önce uygulanmış bekleyen kuvvet/tork yüklerini temizler.
        /// </summary>
        public void ClearPendingLoads()
        {
            _pendingLoads = PhysicsLoads.Zero;
            State = State.ClearForces();
        }

        /// <summary>
        /// Zaman adımı kadar fiziksel entegrasyon gerçekleştirir.
        ///
        /// Bu metot artık eski sabit damping yaklaşımını merkeze almaz.
        /// Sınıfın ana sorumluluğu pending load -> rigid body integration -> report üretimidir.
        /// Platforma özel direnç/sürtünme/sürüklenme modelleri dışarıdan yük olarak verilmelidir.
        /// </summary>
        public void Step(double? dtOverride = null)
        {
            var dt = dtOverride ?? TimeStep;

            if (dt <= 0.0 || !double.IsFinite(dt))
            {
                LastStepReport = PhysicsStepReport.NoStep(State, dt, "INVALID_DT");
                return;
            }

            State = State.Sanitized();

            if (EnableLegacyDamping)
                ApplyLegacyDamping(dt);

            var loads = _pendingLoads.Sanitized();

            State = State with
            {
                LinearForce = loads.ForceWorld,
                AngularTorque = loads.TorqueBody
            };

            State = State.IntegrateAdvanced(
                dt,
                BodyProperties,
                loads,
                IntegrationOptions,
                out var report
            );

            LastStepReport = report;

            ClearPendingLoads();
        }

        /// <summary>
        /// Dışarıdan doğrudan yük verilerek tek adımlık entegrasyon yapar.
        /// Simülasyon, replay ve test senaryolarında kullanışlıdır.
        /// </summary>
        public PhysicsStepReport StepWithLoads(
            double dt,
            PhysicsLoads loads,
            bool clearAfterStep = true
        )
        {
            ApplyWorldLoads(loads.ForceWorld, loads.TorqueBody, replaceExisting: true);

            State = State.IntegrateAdvanced(
                dt,
                BodyProperties,
                _pendingLoads,
                IntegrationOptions,
                out var report
            );

            LastStepReport = report;

            if (clearAfterStep)
                ClearPendingLoads();

            return report;
        }

        /// <summary>
        /// Body frame'de verilen yüklerle tek adımlık entegrasyon yapar.
        /// Thruster allocation testleri için uygundur.
        /// </summary>
        public PhysicsStepReport StepWithBodyLoads(
            double dt,
            Vec3 forceBody,
            Vec3 torqueBody,
            bool clearAfterStep = true
        )
        {
            var forceWorld = State.Orientation.BodyToWorld(forceBody);

            return StepWithLoads(
                dt,
                new PhysicsLoads(forceWorld, torqueBody),
                clearAfterStep
            );
        }

        /// <summary>
        /// Eski davranışa yakın basit hız sönümlemesi.
        ///
        /// Bu yöntem platform bağımsız ana fizik modeli değildir.
        /// Sadece geçici simülasyon uyumluluğu için korunur.
        /// </summary>
        private void ApplyLegacyDamping(double dt)
        {
            double linearFactor = ComputeDampingFactor(LegacyLinearDamping, dt);
            double angularFactor = ComputeDampingFactor(LegacyAngularDamping, dt);

            State = State with
            {
                LinearVelocity = State.LinearVelocity * linearFactor,
                AngularVelocity = State.AngularVelocity * angularFactor
            };

            State = State.Sanitized();
        }

        private static double ComputeDampingFactor(double damping, double dt)
        {
            if (!double.IsFinite(damping) || damping <= 0.0)
                return 1.0;

            if (!double.IsFinite(dt) || dt <= 0.0)
                return 1.0;

            return Math.Max(0.0, 1.0 - damping * dt);
        }

        /// <summary>
        /// Simülasyonun o anki temel parametrelerini konsola yazar.
        /// </summary>
        public void PrintStatus()
        {
            Console.WriteLine(
                $"[Physics] Pos={Fmt(State.Position)} " +
                $"Vel={Fmt(State.LinearVelocity)} " +
                $"AngVel={Fmt(State.AngularVelocity)} " +
                $"Yaw={State.Orientation.YawDeg:F1}° " +
                $"F={Fmt(State.LinearForce)} " +
                $"T={Fmt(State.AngularTorque)} " +
                $"Last={LastStepReport.Reason}"
            );
        }

        /// <summary>
        /// Son fizik adımını kısa, log dostu formatta döndürür.
        /// </summary>
        public string GetLastStepSummary()
        {
            var r = LastStepReport;

            return
                $"PhysicsStep[{r.Reason}] " +
                $"dt={r.DtUsed:F4}s " +
                $"pos={Fmt(State.Position)} " +
                $"vel={Fmt(State.LinearVelocity)} " +
                $"linAcc={Fmt(r.LinearAccelerationWorld)} " +
                $"angAccRad={Fmt(r.AngularAccelerationBodyRad)} " +
                $"speed={r.LinearSpeed:F2}m/s " +
                $"yaw={State.Orientation.YawDeg:F1}°";
        }

        private static string Fmt(Vec3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";
    }
}