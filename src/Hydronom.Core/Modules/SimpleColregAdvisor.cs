using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// Minimal COLREG sınıflandırıcı (yalnızca göreli geometri)
    public sealed class SimpleColregAdvisor : IColregAdvisor
    {
        private readonly double _considerRangeM;
        private readonly double _headOnTolDeg;

        public SimpleColregAdvisor(double considerRangeM = 200.0, double headOnTolDeg = 5.0)
        {
            _considerRangeM = considerRangeM;
            _headOnTolDeg = headOnTolDeg;
        }

        public ColregAdvisory Advise(VehicleState own, FusedFrame frame)
        {
            // En yakın kontağı seç
            Obstacle? closest = null;
            double closestDist = double.MaxValue;

            foreach (var o in frame.Obstacles)
            {
                var dx = o.Position.X - own.Position.X;
                var dy = o.Position.Y - own.Position.Y;
                var d  = Math.Sqrt(dx * dx + dy * dy);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = o;
                }
            }

            if (closest is null || closestDist > _considerRangeM)
                return ColregAdvisory.None;

            // Göreli bearing: own heading'e göre (+) sancak / (-) iskele
            var relX = closest.Position.X - own.Position.X;
            var relY = closest.Position.Y - own.Position.Y;
            var brgAbsDeg = Math.Atan2(relY, relX) * 180.0 / Math.PI;

            var ownHdg = own.Orientation.YawDeg; // VehicleState -> Orientation.YawDeg
            var relBearing = NormalizePm180(brgAbsDeg - ownHdg);
            var abs = Math.Abs(relBearing);

            // Sınıflandırma (POZİSYONEL kurucu kullanımı!)
            if (abs <= _headOnTolDeg)
                return new ColregAdvisory(EncounterType.HeadOn,               false, +1.0, "R14");

            if (abs > 112.5)
                return new ColregAdvisory(EncounterType.Overtaking,           false, +1.0, "R13");

            if (relBearing > 0)
                return new ColregAdvisory(EncounterType.CrossingFromStarboard,false, +1.0, "R15");

            return new ColregAdvisory(EncounterType.CrossingFromPort,         true,  0.0, "R15/R17");
        }

        private static double NormalizePm180(double deg)
        {
            while (deg > 180) deg -= 360;
            while (deg < -180) deg += 360;
            return deg;
        }
    }
}
