using Hydronom.Core.Domain;
using Hydronom.Core.World;

namespace Hydronom.Core.Physics
{
    /// <summary>
    /// Surface/floor temas çözümü için VP9A iskeleti.
    /// Mevcut clamp davranışı buraya taşınmadan önce sınıf ayrıştırması yapılmıştır.
    /// </summary>
    public static class ContactModel
    {
        public static bool IsBelowFloor(Vec3 position, EnvironmentSample environment)
        {
            return position.Z < environment.FloorZ;
        }

        public static bool IsAboveSurface(Vec3 position, EnvironmentSample environment)
        {
            return position.Z > environment.SurfaceZ;
        }
    }
}