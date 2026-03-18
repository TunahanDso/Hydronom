using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// 6-DoF (Six Degrees of Freedom) durum modeli.
    /// Tüm platformlarda (kara, deniz, hava, sualtı) ortak çekirdek temeli sağlar.
    /// Konum, yönelim, hız, kuvvet ve moment bileşenlerini içerir.
    /// </summary>
    public readonly record struct VehicleState(
        Vec3 Position,            // [m] Dünya koordinatında konum
        Orientation Orientation,  // [deg] Roll, Pitch, Yaw açıları
        Vec3 LinearVelocity,      // [m/s] Lineer hız (vx, vy, vz) - Dünya Frame'de
        Vec3 AngularVelocity,     // [deg/s] Açısal hız (rollRate, pitchRate, yawRate) - Body Frame
        Vec3 LinearForce,         // [N] Toplam kuvvet - Dünya Frame'de
        Vec3 AngularTorque        // [N·m] Toplam moment - Body Frame'de
    )
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>Başlangıçta tüm bileşenleri sıfır olan varsayılan durum.</summary>
        public static VehicleState Zero => new(
            Vec3.Zero,
            Orientation.Zero,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero
        );

        /// <summary>
        /// Geriye dönük uyumluluk için: Eski kodlarda doğrudan X/Y/Z kullanan
        /// kısımlar çalışmaya devam etsin diye alias property'ler.
        /// </summary>
        public double X => Position.X;
        public double Y => Position.Y;
        public double Z => Position.Z;

        /// <summary>
        /// Sadece X-Y düzlemini kullanan planar modüller için yardımcı vektör.
        /// Örneğin AdvancedTaskManager, basit navigasyon vb.
        /// </summary>
        public Vec2 Position2D => new(Position.X, Position.Y);

        /// <summary>Geriye dönük uyumluluk için: Velocity → LinearVelocity alias'ı.</summary>
        public Vec3 Velocity => LinearVelocity;

        /// <summary>Geriye dönük / anlamlı alias: AngularRate → AngularVelocity.</summary>
        public Vec3 AngularRate => AngularVelocity;

        /// <summary>Kuvvet ve moment bileşenlerini sıfırlar, konum/hız korunur.</summary>
        public VehicleState ClearForces() =>
            this with { LinearForce = Vec3.Zero, AngularTorque = Vec3.Zero };

        /// <summary>
        /// Basit genel amaçlı entegrasyon.
        /// Mevcut eski akışı bozmamak için korunur.
        /// Açısal tarafta birim düzeltmesi yapılmıştır:
        /// - AngularVelocity dışarıda deg/s tutulur
        /// - fizik hesabı içeride rad/s ile yapılır
        /// </summary>
        /// <param name="dt">Zaman adımı (s)</param>
        /// <param name="mass">Kütle (kg)</param>
        /// <param name="inertia">Atalet momenti (Ix, Iy, Iz)</param>
        public VehicleState Integrate(double dt, double mass, Vec3 inertia)
        {
            if (dt <= 0) return this;
            if (mass <= 1e-9) mass = 1.0;

            double invMass = 1.0 / mass;

            // 1) Lineer taraf
            // Semi-implicit Euler: önce hız, sonra konum güncellenir.
            var linearAccWorld = LinearForce * invMass;
            var newVelWorld = LinearVelocity + linearAccWorld * dt;
            var newPosWorld = Position + newVelWorld * dt;

            // 2) Açısal taraf
            // Dışarıda deg/s tutuyoruz ama tork / inertia hesabı fiziksel olarak rad/s^2 üretir.
            var angVelRad = AngularVelocity * DegToRad;
            var angAccRad = ComponentDivide(AngularTorque, inertia);

            var newAngVelRad = angVelRad + angAccRad * dt;

            // Sayısal patlamayı engelle
            const double maxAngVelDeg = 720.0;
            double maxAngVelRad = maxAngVelDeg * DegToRad;

            newAngVelRad = new Vec3(
                Clamp(newAngVelRad.X, -maxAngVelRad, maxAngVelRad),
                Clamp(newAngVelRad.Y, -maxAngVelRad, maxAngVelRad),
                Clamp(newAngVelRad.Z, -maxAngVelRad, maxAngVelRad)
            );

            var newOrientation = IntegrateOrientation(Orientation, newAngVelRad, dt);

            return this with
            {
                Position = newPosWorld,
                LinearVelocity = newVelWorld,
                AngularVelocity = newAngVelRad * RadToDeg,
                Orientation = newOrientation
            };
        }

        /// <summary>
        /// Deniz / sualtı / hareketli akışkan ortamlar için daha doğru entegrasyon.
        /// Bu metot:
        /// - lineer drag'ı body frame'de uygular
        /// - açısal drag'ı body frame'de uygular
        /// - açısal tarafta rad/s iç hesap mantığı kullanır
        /// - dış dünyaya yine deg/s döner
        /// </summary>
        /// <param name="dt">Zaman adımı (s)</param>
        /// <param name="mass">Kütle (kg)</param>
        /// <param name="inertia">Atalet momenti (Ix, Iy, Iz)</param>
        /// <param name="linearDragBody">
        /// Body frame lineer lineer drag katsayıları (X,Y,Z)
        /// Birim yaklaşık N / (m/s)
        /// </param>
        /// <param name="quadraticDragBody">
        /// Body frame lineer kuadratik drag katsayıları (X,Y,Z)
        /// Birim yaklaşık N / (m/s)^2
        /// </param>
        /// <param name="angularLinearDragBody">
        /// Body frame açısal lineer drag katsayıları (roll,pitch,yaw)
        /// Birim yaklaşık N·m / (rad/s)
        /// </param>
        /// <param name="angularQuadraticDragBody">
        /// Body frame açısal kuadratik drag katsayıları (roll,pitch,yaw)
        /// Birim yaklaşık N·m / (rad/s)^2
        /// </param>
        /// <param name="maxLinearSpeed">
        /// Sayısal taşmaları önlemek için lineer hız üst sınırı [m/s]
        /// </param>
        /// <param name="maxAngularSpeedDeg">
        /// Sayısal taşmaları önlemek için açısal hız üst sınırı [deg/s]
        /// </param>
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
            if (dt <= 0) return this;
            if (mass <= 1e-9) mass = 1.0;

            double invMass = 1.0 / mass;

            // -----------------------------------------------------------------
            // 1) Lineer taraf
            // -----------------------------------------------------------------
            // Dış kuvvet şu an state içinde dünya frame'de taşınıyor.
            // Marine drag ise body frame'de hesaplanmalı.
            var externalForceBody = Orientation.WorldToBody(LinearForce);
            var bodyVel = Orientation.WorldToBody(LinearVelocity);

            var dragForceBody = new Vec3(
                -(linearDragBody.X * bodyVel.X + quadraticDragBody.X * bodyVel.X * Math.Abs(bodyVel.X)),
                -(linearDragBody.Y * bodyVel.Y + quadraticDragBody.Y * bodyVel.Y * Math.Abs(bodyVel.Y)),
                -(linearDragBody.Z * bodyVel.Z + quadraticDragBody.Z * bodyVel.Z * Math.Abs(bodyVel.Z))
            );

            var netForceBody = externalForceBody + dragForceBody;
            var linearAccBody = netForceBody * invMass;
            var linearAccWorld = Orientation.BodyToWorld(linearAccBody);

            var newVelWorld = LinearVelocity + linearAccWorld * dt;
            newVelWorld = ClampMagnitude(newVelWorld, maxLinearSpeed);

            var newPosWorld = Position + newVelWorld * dt;

            // -----------------------------------------------------------------
            // 2) Açısal taraf
            // -----------------------------------------------------------------
            // Açısal hız dışarıda deg/s tutuluyor; fizik hesabı içeride rad/s yapılır.
            var angVelRad = AngularVelocity * DegToRad;

            var dragTorqueBody = new Vec3(
                -(angularLinearDragBody.X * angVelRad.X + angularQuadraticDragBody.X * angVelRad.X * Math.Abs(angVelRad.X)),
                -(angularLinearDragBody.Y * angVelRad.Y + angularQuadraticDragBody.Y * angVelRad.Y * Math.Abs(angVelRad.Y)),
                -(angularLinearDragBody.Z * angVelRad.Z + angularQuadraticDragBody.Z * angVelRad.Z * Math.Abs(angVelRad.Z))
            );

            var netTorqueBody = AngularTorque + dragTorqueBody;
            var angAccRad = ComponentDivide(netTorqueBody, inertia);

            var newAngVelRad = angVelRad + angAccRad * dt;

            double maxAngVelRad = maxAngularSpeedDeg * DegToRad;
            newAngVelRad = new Vec3(
                Clamp(newAngVelRad.X, -maxAngVelRad, maxAngVelRad),
                Clamp(newAngVelRad.Y, -maxAngVelRad, maxAngVelRad),
                Clamp(newAngVelRad.Z, -maxAngVelRad, maxAngVelRad)
            );

            var newOrientation = IntegrateOrientation(Orientation, newAngVelRad, dt);

            return this with
            {
                Position = newPosWorld,
                LinearVelocity = newVelWorld,
                AngularVelocity = newAngVelRad * RadToDeg,
                Orientation = newOrientation
            };
        }

        /// <summary>
        /// Dış bir pose kaynağından durum ezmesi yapılırken,
        /// istersek hızları da tutarlı şekilde güncellemek için yardımcı metod.
        /// Program.cs tarafında external override sırasında kullanılabilir.
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
            return this with
            {
                Position = new Vec3(
                    x,
                    y,
                    z ?? Position.Z
                ),
                Orientation = new Orientation(
                    rollDeg ?? Orientation.RollDeg,
                    pitchDeg ?? Orientation.PitchDeg,
                    NormalizeDeg(yawDeg)
                ),
                LinearVelocity = linearVelocity ?? LinearVelocity,
                AngularVelocity = angularVelocity ?? AngularVelocity
            };
        }

        private static Orientation IntegrateOrientation(Orientation current, Vec3 angularVelocityRad, double dt)
        {
            double wx = angularVelocityRad.X;
            double wy = angularVelocityRad.Y;
            double wz = angularVelocityRad.Z;

            double qw = current.Qw;
            double qx = current.Qx;
            double qy = current.Qy;
            double qz = current.Qz;

            // dq/dt = 0.5 * q ⊗ [0, wx, wy, wz]
            double qwDot = -0.5 * (qx * wx + qy * wy + qz * wz);
            double qxDot =  0.5 * (qw * wx + qy * wz - qz * wy);
            double qyDot =  0.5 * (qw * wy - qx * wz + qz * wx);
            double qzDot =  0.5 * (qw * wz + qx * wy - qy * wx);

            double newQw = qw + qwDot * dt;
            double newQx = qx + qxDot * dt;
            double newQy = qy + qyDot * dt;
            double newQz = qz + qzDot * dt;

            double norm = Math.Sqrt(newQw * newQw + newQx * newQx + newQy * newQy + newQz * newQz);
            if (norm < 1e-9) norm = 1.0;

            newQw /= norm;
            newQx /= norm;
            newQy /= norm;
            newQz /= norm;

            return new Orientation(newQw, newQx, newQy, newQz, fromQuaternion: true);
        }

        private static Vec3 ClampMagnitude(Vec3 v, double max)
        {
            if (max <= 0) return v;

            double magSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            double maxSq = max * max;

            if (magSq <= maxSq)
                return v;

            double mag = Math.Sqrt(magSq);
            if (mag < 1e-12)
                return v;

            double s = max / mag;
            return new Vec3(v.X * s, v.Y * s, v.Z * s);
        }

        private static double NormalizeDeg(double deg)
        {
            while (deg > 180.0) deg -= 360.0;
            while (deg < -180.0) deg += 360.0;
            return deg;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        // Bileşen-bileşen bölme (0 veya çok küçük değerleri 1 kabul eder)
        private static Vec3 ComponentDivide(in Vec3 num, in Vec3 den) =>
            new(
                num.X / (Math.Abs(den.X) < 1e-12 ? 1.0 : den.X),
                num.Y / (Math.Abs(den.Y) < 1e-12 ? 1.0 : den.Y),
                num.Z / (Math.Abs(den.Z) < 1e-12 ? 1.0 : den.Z)
            );
    }

    /// <summary>
    /// Euler oryantasyonu (Roll, Pitch, Yaw) — derece cinsinden.
    /// Aynı zamanda quaternion temsili de içerir; 6DoF çekirdek için tek kaynak.
    /// </summary>
    public readonly record struct Orientation
    {
        public double RollDeg { get; init; }
        public double PitchDeg { get; init; }
        public double YawDeg { get; init; }

        // Quaternion temsili
        public double Qw { get; init; }
        public double Qx { get; init; }
        public double Qy { get; init; }
        public double Qz { get; init; }

        /// <summary>
        /// Sadece Euler açılarıyla oluşturur.
        /// </summary>
        public Orientation(double rollDeg, double pitchDeg, double yawDeg)
        {
            RollDeg = NormalizeDeg(rollDeg);
            PitchDeg = Clamp(pitchDeg, -90.0, 90.0);
            YawDeg = NormalizeDeg(yawDeg);

            (var qw, var qx, var qy, var qz) = FromEuler(RollDeg, PitchDeg, YawDeg);
            Qw = qw;
            Qx = qx;
            Qy = qy;
            Qz = qz;
        }

        /// <summary>
        /// Quaternion ile oluşturur.
        /// </summary>
        public Orientation(double qw, double qx, double qy, double qz, bool fromQuaternion)
        {
            double norm = Math.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz);
            if (norm < 1e-9) norm = 1.0;

            qw /= norm;
            qx /= norm;
            qy /= norm;
            qz /= norm;

            Qw = qw;
            Qx = qx;
            Qy = qy;
            Qz = qz;

            (var rollDeg, var pitchDeg, var yawDeg) = ToEuler(qw, qx, qy, qz);

            RollDeg = NormalizeDeg(rollDeg);
            PitchDeg = Clamp(pitchDeg, -90.0, 90.0);
            YawDeg = NormalizeDeg(yawDeg);
        }

        public static Orientation Zero => new(0, 0, 0);

        /// <summary>
        /// Vektörü dünya frame'den gövde frame'e dönüştürür.
        /// V_body = R(q)^T * V_world
        /// </summary>
        public Vec3 WorldToBody(Vec3 worldVector)
        {
            return RotateByQuaternion(Qw, -Qx, -Qy, -Qz, worldVector);
        }

        /// <summary>
        /// Vektörü gövde frame'den dünya frame'e dönüştürür.
        /// V_world = R(q) * V_body
        /// </summary>
        public Vec3 BodyToWorld(Vec3 bodyVector)
        {
            return RotateByQuaternion(Qw, Qx, Qy, Qz, bodyVector);
        }

        /// <summary>
        /// Bir vektörü verilen normalize kuaterniyon ile döndürür.
        /// </summary>
        private static Vec3 RotateByQuaternion(double qw, double qx, double qy, double qz, Vec3 v)
        {
            double vx = v.X;
            double vy = v.Y;
            double vz = v.Z;

            // t = 2 * (q.xyz × v)
            double tx = 2.0 * (qy * vz - qz * vy);
            double ty = 2.0 * (qz * vx - qx * vz);
            double tz = 2.0 * (qx * vy - qy * vx);

            // v' = v + qw * t + (q.xyz × t)
            double cx = qy * tz - qz * ty;
            double cy = qz * tx - qx * tz;
            double cz = qx * ty - qy * tx;

            return new Vec3(
                vx + qw * tx + cx,
                vy + qw * ty + cy,
                vz + qw * tz + cz
            );
        }

        public static (double qw, double qx, double qy, double qz) FromEuler(double rollDeg, double pitchDeg, double yawDeg)
        {
            double r = rollDeg * Math.PI / 180.0;
            double p = pitchDeg * Math.PI / 180.0;
            double y = yawDeg * Math.PI / 180.0;

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

            return (qw, qx, qy, qz);
        }

        public static (double rollDeg, double pitchDeg, double yawDeg) ToEuler(double qw, double qx, double qy, double qz)
        {
            // roll (x-axis)
            double sinrCosp = 2.0 * (qw * qx + qy * qz);
            double cosrCosp = 1.0 - 2.0 * (qx * qx + qy * qy);
            double roll = Math.Atan2(sinrCosp, cosrCosp);

            // pitch (y-axis)
            double sinp = 2.0 * (qw * qy - qz * qx);
            double pitch = Math.Abs(sinp) >= 1.0
                ? Math.CopySign(Math.PI / 2.0, sinp)
                : Math.Asin(sinp);

            // yaw (z-axis)
            double sinyCosp = 2.0 * (qw * qz + qx * qy);
            double cosyCosp = 1.0 - 2.0 * (qy * qy + qz * qz);
            double yaw = Math.Atan2(sinyCosp, cosyCosp);

            return (
                roll * 180.0 / Math.PI,
                pitch * 180.0 / Math.PI,
                yaw * 180.0 / Math.PI
            );
        }

        /// <summary>
        /// Orientation nesnesini başka bir orientation ile birleştirir.
        /// </summary>
        public Orientation Combine(Orientation other)
        {
            var (w1, x1, y1, z1) = (Qw, Qx, Qy, Qz);
            var (w2, x2, y2, z2) = (other.Qw, other.Qx, other.Qy, other.Qz);

            double qw = w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2;
            double qx = w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2;
            double qy = w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2;
            double qz = w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2;

            return new Orientation(qw, qx, qy, qz, fromQuaternion: true);
        }

        public override string ToString()
        {
            return $"R={RollDeg:F1}°, P={PitchDeg:F1}°, Y={YawDeg:F1}°";
        }

        private static double NormalizeDeg(double deg)
        {
            while (deg > 180.0) deg -= 360.0;
            while (deg < -180.0) deg += 360.0;
            return deg;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}