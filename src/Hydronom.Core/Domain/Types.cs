using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain
{
    public readonly record struct Vec2(double X, double Y)
    {
        public double Length => Math.Sqrt(X * X + Y * Y);

        public static double Distance(Vec2 a, Vec2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Vec2 Normalize()
        {
            var l = Length;
            return l == 0 ? new Vec2(0, 0) : new Vec2(X / l, Y / l);
        }

        public static Vec2 Zero => new(0, 0);
    }

    /// <summary>
    /// Legacy planar obstacle model.
    ///
    /// Not:
    /// - Bu model eski analysis / runtime frame hatlarını kırmamak için korunur.
    /// - Yeni 3D / 6DOF dünya modeli için SpatialObstacle kullanılmalıdır.
    /// </summary>
    public record Obstacle(Vec2 Position, double RadiusM);

    /// <summary>
    /// 3D / platform-independent obstacle representation.
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
        public static SpatialObstacle FromLegacy(Obstacle obstacle, string? source = "legacy-2d")
        {
            return new SpatialObstacle(
                Position: new Vec3(obstacle.Position.X, obstacle.Position.Y, 0.0),
                RadiusM: obstacle.RadiusM,
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
                new Vec2(Position.X, Position.Y),
                RadiusM
            );
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
    /// Böylece eski analysis/decision kodları kırılmadan yeni world-aware / 6DOF
    /// katmanlar gerçek 3D state okuyabilir.
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
        public Orientation Orientation { get; init; } = new(0.0, 0.0, HeadingDeg);

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

        private static IReadOnlyList<SpatialObstacle> ConvertLegacyObstacles(IReadOnlyList<Obstacle> obstacles)
        {
            if (obstacles is null || obstacles.Count == 0)
                return Array.Empty<SpatialObstacle>();

            var list = new List<SpatialObstacle>(obstacles.Count);
            foreach (var obstacle in obstacles)
                list.Add(SpatialObstacle.FromLegacy(obstacle));

            return list;
        }
    }

    /// <summary>
    /// Görev tamamlanma yetkisinin kimde olduğunu belirtir.
    ///
    /// TaskManager:
    /// - Normal GoToPoint / manuel rota görevleri için kullanılır.
    /// - TaskManager hedefe vardığını düşündüğünde görevi temizleyebilir.
    ///
    /// ExternalScenario:
    /// - Scenario / parkur / yarış görevi için kullanılır.
    /// - TaskManager görevi erken temizlememelidir.
    /// - Objective gerçekten tamamlandı mı kararını RuntimeScenarioController / ScenarioObjectiveTracker verir.
    /// </summary>
    public enum TaskCompletionAuthority
    {
        TaskManager = 0,
        ExternalScenario = 1
    }

    /// <summary>
    /// Görev tanımı:
    /// - Name: Görev adı
    /// - Target: Tek hedef noktası
    /// - Waypoints: Çok noktalı rota
    /// - HoldOnArrive: Son noktada bekle
    /// - WaitSecondsPerPoint: Her waypoint'te bekleme süresi
    /// - Loop: Rota bittiğinde başa sar
    /// - CompletionAuthority: Görevin kim tarafından tamamlanacağı
    /// - ExternalOwnerId: Scenario / mission / fleet operation gibi dış sahiplik bilgisi
    /// - ExternalObjectiveId: Dış görev sistemindeki objective kimliği
    /// </summary>
    public record TaskDefinition(string Name, Vec3? Target)
    {
        public List<Vec3> Waypoints { get; set; } = new();

        public bool HoldOnArrive { get; set; } = false;

        public double WaitSecondsPerPoint { get; set; } = 0.0;

        public bool Loop { get; set; } = false;

        /// <summary>
        /// Görevin tamamlanma yetkisi.
        /// Varsayılan olarak normal görevlerde TaskManager yetkilidir.
        /// Scenario görevlerinde ExternalScenario yapılmalıdır.
        /// </summary>
        public TaskCompletionAuthority CompletionAuthority { get; set; } = TaskCompletionAuthority.TaskManager;

        /// <summary>
        /// Görevi dışarıdan sahiplenen sistemin kimliği.
        /// Örnek: scenario id, mission id, fleet operation id.
        /// </summary>
        public string? ExternalOwnerId { get; set; }

        /// <summary>
        /// Dış görev sistemindeki objective/hedef kimliği.
        /// Örnek: reach_wp_1.
        /// </summary>
        public string? ExternalObjectiveId { get; set; }

        /// <summary>
        /// Bu task dış scenario/controller tarafından mı tamamlanacak?
        /// </summary>
        public bool IsExternallyCompleted =>
            CompletionAuthority == TaskCompletionAuthority.ExternalScenario;

        /// <summary>
        /// Görev aktif olarak hedef içeriyor mu?
        /// </summary>
        public bool HasTarget => Target is not null || Waypoints.Count > 0;

        /// <summary>
        /// Tek hedefli görev oluşturmak için kısa yardımcı.
        /// </summary>
        public static TaskDefinition GoTo(string name, Vec3 target, bool holdOnArrive = false)
        {
            return new TaskDefinition(name, target)
            {
                HoldOnArrive = holdOnArrive
            };
        }

        /// <summary>
        /// Scenario / parkur objective'i için tek hedefli görev oluşturur.
        ///
        /// Bu görevlerde TaskManager görevi erken temizlememelidir;
        /// tamamlanma kararı dış scenario controller tarafındadır.
        /// </summary>
        public static TaskDefinition ScenarioGoTo(
            string name,
            Vec3 target,
            string scenarioId,
            string objectiveId,
            bool holdOnArrive = false)
        {
            return new TaskDefinition(name, target)
            {
                HoldOnArrive = holdOnArrive,
                CompletionAuthority = TaskCompletionAuthority.ExternalScenario,
                ExternalOwnerId = scenarioId,
                ExternalObjectiveId = objectiveId
            };
        }

        /// <summary>
        /// Çok noktalı rota görevi oluşturmak için yardımcı.
        /// </summary>
        public static TaskDefinition Route(
            string name,
            IEnumerable<Vec3> waypoints,
            bool loop = false,
            bool holdOnArrive = false,
            double waitSecondsPerPoint = 0.0)
        {
            var task = new TaskDefinition(name, null)
            {
                Loop = loop,
                HoldOnArrive = holdOnArrive,
                WaitSecondsPerPoint = waitSecondsPerPoint
            };

            task.Waypoints.AddRange(waypoints);
            return task;
        }

        /// <summary>
        /// Mevcut görevi dış scenario completion authority ile işaretlemek için yardımcı.
        /// </summary>
        public TaskDefinition WithExternalScenarioCompletion(
            string scenarioId,
            string objectiveId)
        {
            CompletionAuthority = TaskCompletionAuthority.ExternalScenario;
            ExternalOwnerId = scenarioId;
            ExternalObjectiveId = objectiveId;
            return this;
        }
    }

    public record Insights(bool HasObstacleAhead, double ClearanceLeft, double ClearanceRight)
    {
        public static Insights Clear { get; } = new(false, double.PositiveInfinity, double.PositiveInfinity);
    }

    /// <summary>
    /// Manuel sürüş komutu.
    /// Karar modülünden bağımsız doğrudan kullanıcı veya üst runtime tarafından üretilebilir.
    /// Normalize alan önerisi:
    /// - Surge/Sway/Heave/Roll/Pitch/Yaw -> genelde [-1, +1]
    /// Ancak bu sınıf değeri zorla clamp etmez.
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

        /// <summary>
        /// Manuel komutu fiziksel karara dönüştürmek için basit yardımcı.
        /// Katsayıları üst katman verebilir.
        /// </summary>
        public DecisionCommand ToDecisionCommand(
            double maxFx,
            double maxFy,
            double maxFz,
            double maxTx,
            double maxTy,
            double maxTz)
        {
            return new DecisionCommand(
                fx: Surge * maxFx,
                fy: Sway * maxFy,
                fz: Heave * maxFz,
                tx: Roll * maxTx,
                ty: Pitch * maxTy,
                tz: Yaw * maxTz
            );
        }
    }

    /// <summary>
    /// 6-DoF karar çıktısı:
    /// - Fx, Fy, Fz : body-frame kuvvet bileşenleri
    /// - Tx, Ty, Tz : body-frame tork bileşenleri
    ///
    /// Geriye dönük uyumluluk:
    /// - Throttle01      -> Fx
    /// - RudderNeg1To1   -> Tz
    /// </summary>
    public record DecisionCommand
    {
        // 6DoF kuvvetler
        public double Fx { get; init; }
        public double Fy { get; init; }
        public double Fz { get; init; }

        // 6DoF torklar
        public double Tx { get; init; }
        public double Ty { get; init; }
        public double Tz { get; init; }

        /// <summary>
        /// Eski planar API alias'ı.
        /// </summary>
        public double Throttle01
        {
            get => Fx;
            init => Fx = value;
        }

        /// <summary>
        /// Eski planar API alias'ı.
        /// </summary>
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

        /// <summary>
        /// Geriye dönük planar kurucu:
        /// throttle -> Fx
        /// rudder   -> Tz
        /// </summary>
        public DecisionCommand(double throttle01, double rudderNeg1To1)
        {
            Fx = throttle01;
            Tz = rudderNeg1To1;
        }

        /// <summary>
        /// Tam 6DoF kurucu.
        /// </summary>
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

        /// <summary>
        /// Belirli katsayı ile tüm eksenleri ölçekler.
        /// </summary>
        public DecisionCommand Scale(double factor)
        {
            return new DecisionCommand(
                fx: Fx * factor,
                fy: Fy * factor,
                fz: Fz * factor,
                tx: Tx * factor,
                ty: Ty * factor,
                tz: Tz * factor
            );
        }

        /// <summary>
        /// Manual / safety override akışlarında hızlı toplama için.
        /// </summary>
        public DecisionCommand Add(DecisionCommand other)
        {
            return new DecisionCommand(
                fx: Fx + other.Fx,
                fy: Fy + other.Fy,
                fz: Fz + other.Fz,
                tx: Tx + other.Tx,
                ty: Ty + other.Ty,
                tz: Tz + other.Tz
            );
        }
    }
}