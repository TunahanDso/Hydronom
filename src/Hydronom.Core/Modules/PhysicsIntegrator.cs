using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Platform baÄŸÄ±msÄ±z 6-DoF fizik yÃ¼rÃ¼tÃ¼cÃ¼sÃ¼.
    ///
    /// Bu sÄ±nÄ±fÄ±n gÃ¶revi:
    /// - AraÃ§ durumunu tek merkezde tutmak
    /// - Body frame'de gelen kuvvet/momentleri doÄŸru frame sÃ¶zleÅŸmesine Ã§evirmek
    /// - Rijit cisim parametreleriyle fizik entegrasyonu yapmak
    /// - Her fizik adÄ±mÄ± iÃ§in aÃ§Ä±klanabilir PhysicsStepReport Ã¼retmek
    ///
    /// Bu sÄ±nÄ±f deniz, kara, hava veya sualtÄ± ortamÄ±na Ã¶zel fizik yazmaz.
    /// Platforma Ã¶zel etkiler ayrÄ± force/environment modellerinde hesaplanmalÄ± ve
    /// ApplyWorldLoads / ApplyBodyLoads Ã¼zerinden buraya verilmelidir.
    /// </summary>
    public class PhysicsIntegrator
    {
        private const double DefaultMaxLinearSpeed = 100.0;
        private const double DefaultMaxAngularSpeedDeg = 720.0;

        private PhysicsLoads _pendingLoads = PhysicsLoads.Zero;

        /// <summary>
        /// Åu anki 6-DoF araÃ§ durumu.
        /// </summary>
        public VehicleState State { get; private set; } = VehicleState.Zero;

        /// <summary>
        /// Son fizik adÄ±mÄ±nÄ±n aÃ§Ä±klanabilir raporu.
        /// Analysis, Safety, Replay ve Diagnostics katmanlarÄ± bunu okuyabilir.
        /// </summary>
        public PhysicsStepReport LastStepReport { get; private set; } =
            PhysicsStepReport.NoStep(VehicleState.Zero, 0.0, "NOT_STARTED");

        /// <summary>
        /// [kg] Toplam kÃ¼tle.
        /// Geriye dÃ¶nÃ¼k uyumluluk iÃ§in korunur.
        /// Yeni kodlarda BodyProperties Ã¼zerinden okunur.
        /// </summary>
        public double Mass
        {
            get => BodyProperties.MassKg;
            set => BodyProperties = BodyProperties with { MassKg = value };
        }

        /// <summary>
        /// [kgÂ·mÂ²] Body frame diagonal atalet momenti.
        /// Geriye dÃ¶nÃ¼k uyumluluk iÃ§in korunur.
        /// Yeni kodlarda BodyProperties Ã¼zerinden okunur.
        /// </summary>
        public Vec3 Inertia
        {
            get => BodyProperties.InertiaBody;
            set => BodyProperties = BodyProperties with { InertiaBody = value };
        }

        /// <summary>
        /// [s] VarsayÄ±lan entegrasyon zaman adÄ±mÄ±.
        /// </summary>
        public double TimeStep { get; set; } = 0.01;

        /// <summary>
        /// Platform baÄŸÄ±msÄ±z rijit cisim parametreleri.
        /// Tekne, denizaltÄ±, Ä°HA, kara robotu veya AGV aynÄ± sÃ¶zleÅŸmeyi kullanÄ±r.
        /// </summary>
        public RigidBodyProperties BodyProperties { get; private set; }

        /// <summary>
        /// Entegrasyon yÃ¶ntemi ve sayÄ±sal gÃ¼venlik ayarlarÄ±.
        /// </summary>
        public PhysicsIntegrationOptions IntegrationOptions { get; set; } =
            PhysicsIntegrationOptions.Default;

        /// <summary>
        /// Basit sÃ¶nÃ¼mleme modu.
        ///
        /// VarsayÄ±lan olarak kapalÄ±dÄ±r.
        /// Ã‡Ã¼nkÃ¼ platform baÄŸÄ±msÄ±z Ã§ekirdekte sÃ¼rÃ¼klenme/sÃ¼rtÃ¼nme bu sÄ±nÄ±fa gÃ¶mÃ¼lmemelidir.
        /// Eski davranÄ±ÅŸÄ± kabaca korumak istenirse true yapÄ±labilir.
        /// GerÃ§ek yÃ¼kseltme iÃ§in ayrÄ± force model dosyalarÄ± kullanÄ±lmalÄ±dÄ±r.
        /// </summary>
        public bool EnableLegacyDamping { get; set; } = false;

        /// <summary>
        /// [1/s] Eski basit lineer sÃ¶nÃ¼m katsayÄ±sÄ±.
        /// Sadece EnableLegacyDamping true ise uygulanÄ±r.
        /// </summary>
        public double LegacyLinearDamping { get; set; } = 0.2;

        /// <summary>
        /// [1/s] Eski basit aÃ§Ä±sal sÃ¶nÃ¼m katsayÄ±sÄ±.
        /// Sadece EnableLegacyDamping true ise uygulanÄ±r.
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
        /// Rijit cisim parametrelerini tek seferde gÃ¼nceller.
        /// </summary>
        public void ConfigureBody(RigidBodyProperties bodyProperties)
        {
            BodyProperties = bodyProperties.Sanitized();
        }

        /// <summary>
        /// Entegrasyon seÃ§eneklerini tek seferde gÃ¼nceller.
        /// </summary>
        public void ConfigureIntegration(PhysicsIntegrationOptions options)
        {
            IntegrationOptions = options.Sanitized();
        }

        /// <summary>
        /// DÄ±ÅŸ bir modÃ¼lden gelen durumu doÄŸrudan set eder.
        /// Estimator, replay, digital twin veya external pose correction iÃ§in kullanÄ±labilir.
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
        /// YalnÄ±zca konum ve oryantasyonu gÃ¼nceller.
        /// HÄ±zlar korunur.
        /// GPS/SLAM/vision pose correction gibi durumlarda kullanÄ±lÄ±r.
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
        /// Harici pose kaynaÄŸÄ± ile gÃ¼venli pose dÃ¼zeltmesi yapar.
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
        /// - totalTorqueBody: body frame [NÂ·m]
        ///
        /// Kuvvet dÃ¼nya frame'e dÃ¶nÃ¼ÅŸtÃ¼rÃ¼lÃ¼r.
        /// Tork body frame'de tutulur.
        /// </summary>
        public void ApplyForces(Vec3 totalForceBody, Vec3 totalTorqueBody)
        {
            ApplyBodyLoads(totalForceBody, totalTorqueBody, replaceExisting: true);
        }

        /// <summary>
        /// Body frame'de yÃ¼k uygular.
        /// replaceExisting true ise Ã¶nceki pending load ezilir.
        /// false ise mevcut yÃ¼klerin Ã¼zerine eklenir.
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
        /// DÃ¼nya frame'de kuvvet ve body frame'de tork uygular.
        /// Ã‡evresel modeller, global rÃ¼zgar/akÄ±ntÄ± veya dÄ±ÅŸ kuvvetler iÃ§in uygundur.
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
        /// Daha Ã¶nce uygulanmÄ±ÅŸ bekleyen kuvvet/tork yÃ¼klerini temizler.
        /// </summary>
        public void ClearPendingLoads()
        {
            _pendingLoads = PhysicsLoads.Zero;
            State = State.ClearForces();
        }

        /// <summary>
        /// Zaman adÄ±mÄ± kadar fiziksel entegrasyon gerÃ§ekleÅŸtirir.
        ///
        /// Bu metot artÄ±k eski sabit damping yaklaÅŸÄ±mÄ±nÄ± merkeze almaz.
        /// SÄ±nÄ±fÄ±n ana sorumluluÄŸu pending load -> rigid body integration -> report Ã¼retimidir.
        /// Platforma Ã¶zel direnÃ§/sÃ¼rtÃ¼nme/sÃ¼rÃ¼klenme modelleri dÄ±ÅŸarÄ±dan yÃ¼k olarak verilmelidir.
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
        /// DÄ±ÅŸarÄ±dan doÄŸrudan yÃ¼k verilerek tek adÄ±mlÄ±k entegrasyon yapar.
        /// SimÃ¼lasyon, replay ve test senaryolarÄ±nda kullanÄ±ÅŸlÄ±dÄ±r.
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
        /// Body frame'de verilen yÃ¼klerle tek adÄ±mlÄ±k entegrasyon yapar.
        /// Thruster allocation testleri iÃ§in uygundur.
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
        /// Eski davranÄ±ÅŸa yakÄ±n basit hÄ±z sÃ¶nÃ¼mlemesi.
        ///
        /// Bu yÃ¶ntem platform baÄŸÄ±msÄ±z ana fizik modeli deÄŸildir.
        /// Sadece geÃ§ici simÃ¼lasyon uyumluluÄŸu iÃ§in korunur.
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
        /// SimÃ¼lasyonun o anki temel parametrelerini konsola yazar.
        /// </summary>
        public void PrintStatus()
        {
            Console.WriteLine(
                $"[Physics] Pos={Fmt(State.Position)} " +
                $"Vel={Fmt(State.LinearVelocity)} " +
                $"AngVel={Fmt(State.AngularVelocity)} " +
                $"Yaw={State.Orientation.YawDeg:F1}Â° " +
                $"F={Fmt(State.LinearForce)} " +
                $"T={Fmt(State.AngularTorque)} " +
                $"Last={LastStepReport.Reason}"
            );
        }

        /// <summary>
        /// Son fizik adÄ±mÄ±nÄ± kÄ±sa, log dostu formatta dÃ¶ndÃ¼rÃ¼r.
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
                $"yaw={State.Orientation.YawDeg:F1}Â°";
        }

        private static string Fmt(Vec3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";
    }
}
