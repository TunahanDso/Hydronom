using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planning katmanında kullanılan temel vektör güvenlik yardımcıları.
    ///
    /// Vec3 domain modelini değiştirmeden planner tarafında NaN/Infinity
    /// kaynaklı rota, trajectory ve risk hesaplama hatalarını engeller.
    /// </summary>
    public static class PlanningVectorExtensions
    {
        public static Vec3 Sanitized(this Vec3 value)
        {
            return new Vec3(
                X: Safe(value.X),
                Y: Safe(value.Y),
                Z: Safe(value.Z)
            );
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }
}