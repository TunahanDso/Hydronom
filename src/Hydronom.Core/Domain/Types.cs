using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain
{
    public readonly record struct Vec2(double X, double Y)
    {
        public double Length
        {
            get
            {
                var x = Safe(X);
                var y = Safe(Y);
                var len = Math.Sqrt(x * x + y * y);
                return double.IsFinite(len) ? len : 0.0;
            }
        }

        public static Vec2 Zero => new(0.0, 0.0);

        public static double Distance(Vec2 a, Vec2 b)
        {
            var dx = Safe(a.X - b.X);
            var dy = Safe(a.Y - b.Y);

            var dist = Math.Sqrt(dx * dx + dy * dy);
            return double.IsFinite(dist) ? dist : 0.0;
        }

        public Vec2 Normalize()
        {
            var l = Length;
            return l <= 1e-12
                ? Zero
                : new Vec2(Safe(X) / l, Safe(Y) / l);
        }

        public Vec2 Sanitized()
        {
            return new Vec2(Safe(X), Safe(Y));
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }

    /// <summary>
    /// Legacy planar obstacle model.
    ///
    /// Not:
    /// - Bu model eski analysis/runtime frame hatlarını kırmamak için korunur.
    /// - Yeni 3D / 6DOF dünya modeli için SpatialObstacle kullanılmalıdır.
    /// - Bu model world model ground-truth değildir; sadece frame/observation taşıyıcısıdır.
    /// </summary>
    public record Obstacle(Vec2 Position, double RadiusM)
    {
        public double EffectiveRadiusM =>
            SanitizeRadius(RadiusM);

        public Obstacle Sanitized()
        {
            return new Obstacle(
                Position.Sanitized(),
                EffectiveRadiusM
            );
        }

        private static double SanitizeRadius(double radiusM)
        {
            if (!double.IsFinite(radiusM))
                return 0.0;

            return Math.Max(0.0, radiusM);
        }
    }

    /// <summary>
    /// 3D / platform-independent obstacle representation.
    ///
    /// Bu model hâlâ yalnızca bir obstacle observation / obstacle representation'dır.
    /// World Model'e doğrudan scenario'dan basılacak ground-truth nesne değildir.
    ///
    /// Hydronom dünya koordinat sözleşmesi:
    /// - X: yatay ileri / local-east benzeri eksen
    /// - Y: yatay sağ / local-north benzeri eksen
    /// - Z: yukarı eksen
    ///
    /// Depth pozitif aşağı yönde tutulacaksa:
    /// - depth = -Z
    /// </summary>
    public record SpatialObstacle(
        Vec3 Position,
        double RadiusM,
        Vec3? SizeM = null,
        Orientation? Orientation = null,
        string? Kind = null,
        string? Medium = null,
        string? Source = null)
    {
        public double EffectiveRadiusM =>
            SanitizeRadius(RadiusM);

        public bool HasSize =>
            SizeM is not null;

        public bool IsSensorDerived =>
            !string.IsNullOrWhiteSpace(Source) &&
            !Source.Contains("scenario", StringComparison.OrdinalIgnoreCase) &&
            !Source.Contains("ground-truth", StringComparison.OrdinalIgnoreCase);

        public static SpatialObstacle FromLegacy(Obstacle obstacle, string? source = "legacy-2d")
        {
            var safe = obstacle.Sanitized();

            return new SpatialObstacle(
                Position: new Vec3(safe.Position.X, safe.Position.Y, 0.0),
                RadiusM: safe.EffectiveRadiusM,
                SizeM: null,
                Orientation: null,
                Kind: "Obstacle",
                Medium: null,
                Source: source
            );
        }

        public Obstacle ToLegacy2D()
        {
            return new Obstacle(
                new Vec2(Position.X, Position.Y).Sanitized(),
                EffectiveRadiusM
            );
        }

        public SpatialObstacle Sanitized()
        {
            return new SpatialObstacle(
                Position: SanitizeVec(Position),
                RadiusM: EffectiveRadiusM,
                SizeM: SizeM is Vec3 size ? SanitizeVec(size) : null,
                Orientation: Orientation,
                Kind: NormalizeText(Kind),
                Medium: NormalizeText(Medium),
                Source: NormalizeText(Source)
            );
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                Safe(v.X),
                Safe(v.Y),
                Safe(v.Z)
            );
        }

        private static string? NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static double SanitizeRadius(double radiusM)
        {
            if (!double.IsFinite(radiusM))
                return 0.0;

            return Math.Max(0.0, radiusM);
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }

    /// <summary>
    /// Runtime frame taşıyıcısı.
    ///
    /// Bu modelin constructor imzası eski sistemle uyumludur:
    /// - Position: legacy 2D position
    /// - HeadingDeg: legacy yaw/heading
    /// - Obstacles: legacy 2D circular obstacles
    /// - Target: legacy 2D target
    ///
    /// Yeni 6DOF alanlar init-only property olarak eklenmiştir:
    /// - Position3D
    /// - Orientation
    /// - LinearVelocity
    /// - AngularVelocityDegSec
    /// - SpatialObstacles
    /// - Target3D
    ///
    /// Önemli mimari not:
    /// FusedFrame world model değildir.
    /// FusedFrame sadece sensör/fusion/perception hattından gelen anlık frame'dir.
    /// World Model bu frame'lerden observation/track üretir.
    /// </summary>
    public record FusedFrame(
        DateTime TimestampUtc,
        Vec2 Position,
        double HeadingDeg,
        IReadOnlyList<Obstacle> Obstacles,
        Vec2? Target
    )
    {
        /// <summary>
        /// 3D dünya konumu. Eski frame kaynakları için Z=0 kabul edilir.
        /// </summary>
        public Vec3 Position3D { get; init; } = new(Position.X, Position.Y, 0.0);

        /// <summary>
        /// 6DOF orientation. Eski kaynaklarda roll/pitch=0, yaw=HeadingDeg kabul edilir.
        /// </summary>
        public Orientation Orientation { get; init; } = new(0.0, 0.0, SafeAngle(HeadingDeg));

        /// <summary>
        /// Dünya frame lineer hız [m/s].
        /// </summary>
        public Vec3 LinearVelocity { get; init; } = Vec3.Zero;

        /// <summary>
        /// Body frame açısal hız [deg/s].
        /// </summary>
        public Vec3 AngularVelocityDegSec { get; init; } = Vec3.Zero;

        /// <summary>
        /// 3D obstacle listesi. Eski kaynaklar için legacy obstacles'tan türetilir.
        /// </summary>
        public IReadOnlyList<SpatialObstacle> SpatialObstacles { get; init; } =
            ConvertLegacyObstacles(Obstacles);

        /// <summary>
        /// 3D hedef. Eski hedefler için Z=0 kabul edilir.
        /// Bu alan navigation target değildir; sadece frame target taşıyıcısıdır.
        /// </summary>
        public Vec3? Target3D { get; init; } =
            Target.HasValue ? new Vec3(Target.Value.X, Target.Value.Y, 0.0) : null;

        /// <summary>
        /// Frame gerçekten 3D/6DOF bilgi taşıyor mu?
        /// Bu değer log/diagnostics için yardımcıdır.
        /// </summary>
        public bool Has6DofData =>
            Math.Abs(Position3D.Z) > 1e-9 ||
            Math.Abs(Orientation.RollDeg) > 1e-9 ||
            Math.Abs(Orientation.PitchDeg) > 1e-9 ||
            Math.Abs(LinearVelocity.Z) > 1e-9 ||
            Math.Abs(AngularVelocityDegSec.X) > 1e-9 ||
            Math.Abs(AngularVelocityDegSec.Y) > 1e-9;

        /// <summary>
        /// Sualtı/altitude kullanımları için okunabilir depth alias'ı.
        /// Hydronom world convention: Z up, depth positive down.
        /// </summary>
        public double DepthM => -Position3D.Z;

        public double AgeSeconds(DateTime utcNow)
        {
            var age = utcNow - TimestampUtc;
            if (age.TotalSeconds < 0.0)
                return 0.0;

            return double.IsFinite(age.TotalSeconds) ? age.TotalSeconds : 0.0;
        }

        public FusedFrame Sanitized()
        {
            return this with
            {
                Position = Position.Sanitized(),
                HeadingDeg = SafeAngle(HeadingDeg),
                Position3D = SanitizeVec(Position3D),
                Orientation = Orientation,
                LinearVelocity = SanitizeVec(LinearVelocity),
                AngularVelocityDegSec = SanitizeVec(AngularVelocityDegSec),
                SpatialObstacles = SanitizeSpatialObstacles(SpatialObstacles),
                Target3D = Target3D is Vec3 t ? SanitizeVec(t) : null
            };
        }

        public static FusedFrame Empty { get; } = new(
            TimestampUtc: DateTime.UtcNow,
            Position: Vec2.Zero,
            HeadingDeg: 0.0,
            Obstacles: Array.Empty<Obstacle>(),
            Target: null
        )
        {
            Position3D = Vec3.Zero,
            Orientation = Orientation.Zero,
            LinearVelocity = Vec3.Zero,
            AngularVelocityDegSec = Vec3.Zero,
            SpatialObstacles = Array.Empty<SpatialObstacle>(),
            Target3D = null
        };

        private static IReadOnlyList<SpatialObstacle> ConvertLegacyObstacles(IReadOnlyList<Obstacle>? obstacles)
        {
            if (obstacles is null || obstacles.Count == 0)
                return Array.Empty<SpatialObstacle>();

            var list = new List<SpatialObstacle>(obstacles.Count);
            foreach (var obstacle in obstacles)
                list.Add(SpatialObstacle.FromLegacy(obstacle));

            return list;
        }

        private static IReadOnlyList<SpatialObstacle> SanitizeSpatialObstacles(
            IReadOnlyList<SpatialObstacle>? obstacles)
        {
            if (obstacles is null || obstacles.Count == 0)
                return Array.Empty<SpatialObstacle>();

            var list = new List<SpatialObstacle>(obstacles.Count);
            foreach (var obstacle in obstacles)
                list.Add(obstacle.Sanitized());

            return list;
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

        private static double SafeAngle(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }

    /// <summary>
    /// Analysis / Perception tarafından üretilebilecek hafif içgörü modeli.
    ///
    /// Bu model sürüş kararı değildir.
    /// Decision/Planner bu bilgiyi doğrudan "aracı kır" emri gibi kullanmamalıdır.
    /// Kalıcı mimaride obstacle/clearance bilgileri World Model + Geometry üzerinden gelmelidir.
    /// </summary>
    public record Insights(bool HasObstacleAhead, double ClearanceLeft, double ClearanceRight)
    {
        public static Insights Clear { get; } = new(
            HasObstacleAhead: false,
            ClearanceLeft: double.PositiveInfinity,
            ClearanceRight: double.PositiveInfinity
        );

        public Insights Sanitized()
        {
            return new Insights(
                HasObstacleAhead,
                SanitizeClearance(ClearanceLeft),
                SanitizeClearance(ClearanceRight)
            );
        }

        private static double SanitizeClearance(double value)
        {
            if (double.IsPositiveInfinity(value))
                return double.PositiveInfinity;

            if (!double.IsFinite(value))
                return 0.0;

            return Math.Max(0.0, value);
        }
    }

    /// <summary>
    /// Manuel sürüş komutu.
    ///
    /// Normalize alan önerisi:
    /// - Surge/Sway/Heave/Roll/Pitch/Yaw -> genelde [-1, +1]
    ///
    /// Bu sınıf ham komutu taşır.
    /// Fiziksel clamp/safety limitleme Safety/Limiter katmanının işidir.
    /// </summary>
    public readonly record struct ManualDriveCommand(
        double Surge,
        double Sway,
        double Heave,
        double Roll,
        double Pitch,
        double Yaw)
    {
        public static ManualDriveCommand Zero => new(0, 0, 0, 0, 0, 0);

        public bool IsZero =>
            Math.Abs(Surge) < 1e-12 &&
            Math.Abs(Sway) < 1e-12 &&
            Math.Abs(Heave) < 1e-12 &&
            Math.Abs(Roll) < 1e-12 &&
            Math.Abs(Pitch) < 1e-12 &&
            Math.Abs(Yaw) < 1e-12;

        public ManualDriveCommand Sanitized()
        {
            return new ManualDriveCommand(
                Safe(Surge),
                Safe(Sway),
                Safe(Heave),
                Safe(Roll),
                Safe(Pitch),
                Safe(Yaw)
            );
        }

        /// <summary>
        /// Manuel komutu fiziksel karara dönüştürmek için basit yardımcı.
        ///
        /// Not:
        /// Bu yardımcı geriye dönük uyumluluk içindir.
        /// Kalıcı mimaride manual komut önce Decision Authority tarafından mod seçimine,
        /// sonra Controller/Limiter hattına girmelidir.
        /// </summary>
        public DecisionCommand ToDecisionCommand(
            double maxFx,
            double maxFy,
            double maxFz,
            double maxTx,
            double maxTy,
            double maxTz)
        {
            var m = Sanitized();

            return new DecisionCommand(
                fx: m.Surge * Safe(maxFx),
                fy: m.Sway * Safe(maxFy),
                fz: m.Heave * Safe(maxFz),
                tx: m.Roll * Safe(maxTx),
                ty: m.Pitch * Safe(maxTy),
                tz: m.Yaw * Safe(maxTz)
            );
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }

    /// <summary>
    /// 6-DoF karar çıktısı:
    /// - Fx, Fy, Fz : body-frame kuvvet bileşenleri
    /// - Tx, Ty, Tz : body-frame tork bileşenleri
    ///
    /// Önemli mimari not:
    /// DecisionCommand nihai wrench/komut taşıyıcısıdır.
    /// Task Manager, World Model, Geometry veya Planner bunu üretmemelidir.
    ///
    /// Geriye dönük uyumluluk:
    /// - Throttle01      -> Fx
    /// - RudderNeg1To1   -> Tz
    /// </summary>
    public record DecisionCommand
    {
        public double Fx { get; init; }
        public double Fy { get; init; }
        public double Fz { get; init; }

        public double Tx { get; init; }
        public double Ty { get; init; }
        public double Tz { get; init; }

        public double Throttle01
        {
            get => Fx;
            init => Fx = value;
        }

        public double RudderNeg1To1
        {
            get => Tz;
            init => Tz = value;
        }

        public static DecisionCommand Zero { get; } = new();

        public bool IsZero =>
            Math.Abs(Fx) < 1e-12 &&
            Math.Abs(Fy) < 1e-12 &&
            Math.Abs(Fz) < 1e-12 &&
            Math.Abs(Tx) < 1e-12 &&
            Math.Abs(Ty) < 1e-12 &&
            Math.Abs(Tz) < 1e-12;

        public DecisionCommand()
        {
        }

        public DecisionCommand(double throttle01, double rudderNeg1To1)
        {
            Fx = throttle01;
            Tz = rudderNeg1To1;
        }

        public DecisionCommand(
            double fx, double fy, double fz,
            double tx, double ty, double tz)
        {
            Fx = fx;
            Fy = fy;
            Fz = fz;
            Tx = tx;
            Ty = ty;
            Tz = tz;
        }

        public static DecisionCommand FromPlanar(double throttle01, double rudderNeg1To1)
            => new(throttle01, rudderNeg1To1);

        public static DecisionCommand FromWrench(
            double fx, double fy, double fz,
            double tx, double ty, double tz)
            => new(fx, fy, fz, tx, ty, tz);

        public static DecisionCommand FromManual(
            ManualDriveCommand manual,
            double maxFx,
            double maxFy,
            double maxFz,
            double maxTx,
            double maxTy,
            double maxTz)
            => manual.ToDecisionCommand(maxFx, maxFy, maxFz, maxTx, maxTy, maxTz);

        public DecisionCommand Sanitized()
        {
            return new DecisionCommand(
                fx: Safe(Fx),
                fy: Safe(Fy),
                fz: Safe(Fz),
                tx: Safe(Tx),
                ty: Safe(Ty),
                tz: Safe(Tz)
            );
        }

        public DecisionCommand Scale(double factor)
        {
            double f = Safe(factor);
            var c = Sanitized();

            return new DecisionCommand(
                fx: c.Fx * f,
                fy: c.Fy * f,
                fz: c.Fz * f,
                tx: c.Tx * f,
                ty: c.Ty * f,
                tz: c.Tz * f
            );
        }

        public DecisionCommand Add(DecisionCommand other)
        {
            var a = Sanitized();
            var b = other?.Sanitized() ?? Zero;

            return new DecisionCommand(
                fx: a.Fx + b.Fx,
                fy: a.Fy + b.Fy,
                fz: a.Fz + b.Fz,
                tx: a.Tx + b.Tx,
                ty: a.Ty + b.Ty,
                tz: a.Tz + b.Tz
            );
        }

        public DecisionCommand Deadband(double epsilon = 1e-9)
        {
            double e = Math.Max(0.0, Safe(epsilon));
            var c = Sanitized();

            return new DecisionCommand(
                fx: Math.Abs(c.Fx) <= e ? 0.0 : c.Fx,
                fy: Math.Abs(c.Fy) <= e ? 0.0 : c.Fy,
                fz: Math.Abs(c.Fz) <= e ? 0.0 : c.Fz,
                tx: Math.Abs(c.Tx) <= e ? 0.0 : c.Tx,
                ty: Math.Abs(c.Ty) <= e ? 0.0 : c.Ty,
                tz: Math.Abs(c.Tz) <= e ? 0.0 : c.Tz
            );
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }
}