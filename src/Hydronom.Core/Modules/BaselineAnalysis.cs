using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// ÇEVRE ANALİZİ (dinamik – uzaktan güncellenebilir, thread-safe)
    /// - Ön koni (±HalfFovDeg) ve AheadDistanceM içinde engel var mı?
    /// - Sol/Sağ açıklıkları (en yakın engel yüzeyi mesafesi) döndürür.
    /// - Parametreler SetParameters(...) ile canlı değiştirilebilir.
    /// </summary>
    public class BaselineAnalysis : IAnalysisModule
    {
        // Tunable parametreler
        private double _aheadDistanceM; // m
        private double _halfFovDeg;     // °

        // Eşzamanlı erişim için
        private readonly object _lock = new();

        // Debug
        private static bool VerboseObstacleDebug =>
            Environment.GetEnvironmentVariable("HYDRONOM_VERBOSE_OBS") == "1";

        // Durum raporu için güvenli getter’lar
        public double AheadDistanceM { get { lock (_lock) return _aheadDistanceM; } }
        public double HalfFovDeg { get { lock (_lock) return _halfFovDeg; } }

        public BaselineAnalysis(double aheadDistanceM = 12.0, double halfFovDeg = 45.0)
        {
            _aheadDistanceM = ClampAhead(aheadDistanceM);
            _halfFovDeg = ClampFov(halfFovDeg);
        }

        /// <summary>
        /// Uzaktan tuning: null olmayan alanları günceller (thread-safe).
        /// Program.cs ve InlineTuner bununla konuşuyor.
        /// </summary>
        public void SetParameters(double? aheadDistanceM, double? halfFovDeg)
        {
            lock (_lock)
            {
                if (aheadDistanceM.HasValue) _aheadDistanceM = ClampAhead(aheadDistanceM.Value);
                if (halfFovDeg.HasValue) _halfFovDeg = ClampFov(halfFovDeg.Value);
            }
        }

        // Eski isimle çağrılmaları da desteklemek istersen:
        public void Update(double? aheadDistanceM = null, double? halfFovDeg = null)
            => SetParameters(aheadDistanceM, halfFovDeg);

        public Insights Analyze(FusedFrame frame)
        {
            // Mevcut değerleri tek seferde çek
            double ahead, halfFov;
            lock (_lock)
            {
                ahead = _aheadDistanceM;
                halfFov = _halfFovDeg;
            }

            bool hasObstacleAhead = false;
            double leftClear = double.MaxValue;
            double rightClear = double.MaxValue;

            double headingRad = Math.PI * frame.HeadingDeg / 180.0;
            var fwd = new Vec2(Math.Cos(headingRad), Math.Sin(headingRad));

            if (VerboseObstacleDebug)
            {
                Console.WriteLine(
                    $"[ANA] framePos=({frame.Position.X:0.00},{frame.Position.Y:0.00}) " +
                    $"head={frame.HeadingDeg:0.0} obs={frame.Obstacles.Count} " +
                    $"ahead={ahead:0.0} fov={halfFov:0.0}"
                );
            }

            foreach (var o in frame.Obstacles)
            {
                var rel = new Vec2(
                    o.Position.X - frame.Position.X,
                    o.Position.Y - frame.Position.Y
                );

                double centerDist = rel.Length;
                double distSurface = Math.Max(0, centerDist - o.RadiusM);

                double angDeg;
                if (centerDist <= 1e-9)
                {
                    angDeg = 0.0;
                }
                else
                {
                    double cos = Math.Clamp(
                        (rel.X * fwd.X + rel.Y * fwd.Y) / centerDist,
                        -1.0,
                        1.0
                    );
                    angDeg = Math.Acos(cos) * 180.0 / Math.PI;
                }

                bool inCone = angDeg <= halfFov;
                bool inRange = centerDist <= (ahead + o.RadiusM);

                if (inCone && inRange)
                    hasObstacleAhead = true;

                // side >= 0 → sağ, < 0 → sol
                double side = (-fwd.Y) * rel.X + (fwd.X) * rel.Y;
                if (side >= 0)
                    rightClear = Math.Min(rightClear, distSurface);
                else
                    leftClear = Math.Min(leftClear, distSurface);

                if (VerboseObstacleDebug)
                {
                    Console.WriteLine(
                        $"[ANA] obs=({o.Position.X:0.00},{o.Position.Y:0.00}) r={o.RadiusM:0.00} " +
                        $"rel=({rel.X:0.00},{rel.Y:0.00}) centerDist={centerDist:0.00} " +
                        $"distSurface={distSurface:0.00} ang={angDeg:0.0} " +
                        $"inCone={inCone} inRange={inRange} side={(side >= 0 ? "R" : "L")}"
                    );
                }
            }

            if (leftClear == double.MaxValue) leftClear = ahead;
            if (rightClear == double.MaxValue) rightClear = ahead;

            if (VerboseObstacleDebug)
            {
                Console.WriteLine(
                    $"[ANA] result hasAhead={hasObstacleAhead} " +
                    $"leftClear={leftClear:0.00} rightClear={rightClear:0.00}"
                );
            }

            return new Insights(
                HasObstacleAhead: hasObstacleAhead,
                ClearanceLeft: leftClear,
                ClearanceRight: rightClear
            );
        }

        private static double ClampAhead(double v) => Math.Clamp(v, 1.0, 1000.0);
        private static double ClampFov(double v) => Math.Clamp(v, 1.0, 89.0);
    }
}
