using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Basit 6-DoF fizik integratörü.
    /// ActuatorManager tarafından üretilen toplam kuvvet (F) ve moment (T)
    /// kullanılarak araç durumunu zaman adımlarıyla günceller.
    ///
    /// NOT:
    /// - State her zaman "tek gerçek 6DoF çekirdek durumu" temsil eder.
    /// - Kuvvet/moment body-frame’de gelir, burada gerekirse dünya frame’ine dönüştürülür.
    /// </summary>
    public class PhysicsIntegrator
    {
        /// <summary>Şu anki 6DoF araç durumu.</summary>
        public VehicleState State { get; private set; } = VehicleState.Zero;

        /// <summary>[kg] Toplam kütle.</summary>
        public double Mass { get; set; } = 10.0;

        /// <summary>[kg·m²] Atalet momenti (Ix, Iy, Iz).</summary>
        public Vec3 Inertia { get; set; } = new(1.0, 1.0, 1.0);

        /// <summary>[s] Entegrasyon zaman adımı.</summary>
        public double TimeStep { get; set; } = 0.01;

        public PhysicsIntegrator(double mass = 10.0, Vec3? inertia = null)
        {
            Mass    = mass;
            Inertia = inertia ?? new Vec3(1.0, 1.0, 1.0);
        }

        /// <summary>
        /// Dış bir modülden (ör: estimator, twin senkronizasyon) gelen durumu
        /// sert şekilde set etmek için kullanılır.
        /// Kuvvet/moment bileşenleri isteğe bağlı olarak sıfırlanabilir.
        /// </summary>
        public void ResetState(VehicleState newState, bool clearForces = true)
        {
            State = clearForces ? newState.ClearForces() : newState;
        }

        /// <summary>
        /// Yalnızca konum ve oryantasyonu günceller.
        /// Hızlar ve kuvvetler korunur. (Örn: GPS/SLAM ile "pose correction")
        /// </summary>
        public void SetPose(Vec3 position, Orientation orientation)
        {
            State = State with
            {
                Position    = position,
                Orientation = orientation
            };
        }

        /// <summary>
        /// Harici bir kuvvet ve moment uygular (örnek: ActuatorManager çıktısı).
        /// Beklenen:
        /// - totalForceBody ve totalTorqueBody GÖVDE eksenlerinde (Fb, Tb).
        /// Burada:
        /// - Kuvvet dünya eksenlerine dönüştürülerek state'e yazılır.
        /// - Moment body eksenlerinde tutulur (VehicleState.Integrate bunu bekler).
        /// </summary>
        public void ApplyForces(Vec3 totalForceBody, Vec3 totalTorqueBody)
        {
            // Orientation artık tam 6DoF kuaterniyon olduğu için
            // gövde→dünya dönüşümünü doğrudan kullanabiliriz.
            // Fx_body,Fy_body,Fz_body → dünya frame’e:
            var worldForce = State.Orientation.BodyToWorld(totalForceBody);

            State = State with
            {
                // Kuvvet: dünya eksenlerinde (VehicleState.Integrate lineer tarafta dünya frame kullanıyor)
                LinearForce = worldForce,

                // Tork: gövde eksenlerinde tutulmaya devam eder
                AngularTorque = totalTorqueBody
            };
        }

        /// <summary>
        /// Zaman adımı kadar fiziksel entegrasyon gerçekleştirir.
        /// </summary>
        public void Step(double? dtOverride = null)
        {
            var dt = dtOverride ?? TimeStep;
            if (dt <= 0) return;

            // Basit lineer ve açısal sönüm (suyun/havanın sürüklemesini kabaca temsil eder).
            const double linDamping = 0.2; // [1/s] yaklaşık lineer sönüm katsayısı
            const double angDamping = 0.5; // [1/s] yaklaşık açısal sönüm katsayısı

            double linFactor = Math.Max(0.0, 1.0 - linDamping * dt);
            double angFactor = Math.Max(0.0, 1.0 - angDamping * dt);

            State = State with
            {
                LinearVelocity  = State.LinearVelocity * linFactor,
                AngularVelocity = State.AngularVelocity * angFactor
            };

            // 6DoF Euler entegrasyonu (konum + hız + orientation + açısal hız)
            State = State.Integrate(dt, Mass, Inertia);

            // Bir adımlık kuvvetleri tüket, bir sonraki adımda tekrar yazılacak.
            State = State.ClearForces();
        }

        /// <summary>
        /// Simülasyonun o anki temel parametrelerini konsola yazar (debug amaçlı).
        /// </summary>
        public void PrintStatus()
        {
            Console.WriteLine(
                $"[Physics] Pos={Fmt(State.Position)} " +
                $"Vel={Fmt(State.LinearVelocity)} " +
                $"AngVel={Fmt(State.AngularVelocity)} " +
                $"Yaw={State.Orientation.YawDeg:F1}°"
            );
        }

        private static string Fmt(Vec3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";
    }
}
