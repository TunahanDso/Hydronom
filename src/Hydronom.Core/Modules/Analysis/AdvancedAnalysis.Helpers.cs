using System;

namespace Hydronom.Core.Modules
{
    public sealed partial class AdvancedAnalysis
    {
        internal static double ClampAhead(double value) => ClampRange(value, 1.0, 1000.0, 12.0);

        internal static double ClampFov(double value) => ClampRange(value, 5.0, 120.0, 60.0);

        internal static int ClampSectorCount(int value)
        {
            int clamped = Math.Clamp(value, 9, 121);

            if (clamped % 2 == 0)
                clamped++;

            if (clamped > 121)
                clamped = 121;

            return clamped;
        }

        internal static double ClampRange(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
        }

        private static double Sanitize(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        private static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

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

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}