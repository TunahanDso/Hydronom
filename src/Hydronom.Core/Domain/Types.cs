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

    public record Obstacle(Vec2 Position, double RadiusM);

    public record FusedFrame(
        DateTime TimestampUtc,
        Vec2 Position,
        double HeadingDeg,
        IReadOnlyList<Obstacle> Obstacles,
        Vec2? Target
    )
    {
        public static FusedFrame Empty { get; } = new(
            TimestampUtc: DateTime.UtcNow,
            Position: Vec2.Zero,
            HeadingDeg: 0.0,
            Obstacles: Array.Empty<Obstacle>(),
            Target: null
        );
    }

    /// <summary>
    /// GÃ¶rev tanÄ±mÄ±:
    /// - Name: GÃ¶rev adÄ±
    /// - Target: Tek hedef noktasÄ±
    /// - Waypoints: Ã‡ok noktalÄ± rota
    /// - HoldOnArrive: Son noktada bekle
    /// - WaitSecondsPerPoint: Her waypoint'te bekleme sÃ¼resi
    /// - Loop: Rota bittiÄŸinde baÅŸa sar
    /// </summary>
    public record TaskDefinition(string Name, Vec3? Target)
    {
        public List<Vec3> Waypoints { get; set; } = new();

        public bool HoldOnArrive { get; set; } = false;

        public double WaitSecondsPerPoint { get; set; } = 0.0;

        public bool Loop { get; set; } = false;

        /// <summary>
        /// GÃ¶rev aktif olarak hedef iÃ§eriyor mu?
        /// </summary>
        public bool HasTarget => Target is not null || Waypoints.Count > 0;

        /// <summary>
        /// Tek hedefli gÃ¶rev oluÅŸturmak iÃ§in kÄ±sa yardÄ±mcÄ±.
        /// </summary>
        public static TaskDefinition GoTo(string name, Vec3 target, bool holdOnArrive = false)
        {
            return new TaskDefinition(name, target)
            {
                HoldOnArrive = holdOnArrive
            };
        }

        /// <summary>
        /// Ã‡ok noktalÄ± rota gÃ¶revi oluÅŸturmak iÃ§in yardÄ±mcÄ±.
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
    }

    public record Insights(bool HasObstacleAhead, double ClearanceLeft, double ClearanceRight)
    {
        public static Insights Clear { get; } = new(false, double.PositiveInfinity, double.PositiveInfinity);
    }

    /// <summary>
    /// Manuel sÃ¼rÃ¼ÅŸ komutu.
    /// Karar modÃ¼lÃ¼nden baÄŸÄ±msÄ±z doÄŸrudan kullanÄ±cÄ± veya Ã¼st runtime tarafÄ±ndan Ã¼retilebilir.
    /// Normalize alan Ã¶nerisi:
    /// - Surge/Sway/Heave/Roll/Pitch/Yaw -> genelde [-1, +1]
    /// Ancak bu sÄ±nÄ±f deÄŸeri zorla clamp etmez.
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
        /// Manuel komutu fiziksel karara dÃ¶nÃ¼ÅŸtÃ¼rmek iÃ§in basit yardÄ±mcÄ±.
        /// KatsayÄ±larÄ± Ã¼st katman verebilir.
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
    /// 6-DoF karar Ã§Ä±ktÄ±sÄ±:
    /// - Fx, Fy, Fz : body-frame kuvvet bileÅŸenleri
    /// - Tx, Ty, Tz : body-frame tork bileÅŸenleri
    ///
    /// Geriye dÃ¶nÃ¼k uyumluluk:
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
        /// Eski planar API alias'Ä±.
        /// </summary>
        public double Throttle01
        {
            get => Fx;
            init => Fx = value;
        }

        /// <summary>
        /// Eski planar API alias'Ä±.
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
        /// Geriye dÃ¶nÃ¼k planar kurucu:
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
        /// Belirli katsayÄ± ile tÃ¼m eksenleri Ã¶lÃ§ekler.
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
        /// Manual / safety override akÄ±ÅŸlarÄ±nda hÄ±zlÄ± toplama iÃ§in.
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
