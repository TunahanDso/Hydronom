using System;
using Hydronom.Core.Domain;
using Hydronom.Core.World;

partial class Program
{
    /// <summary>
    /// Runtime iç simülasyon için aracın bulunduğu ortamı çözer.
    ///
    /// V1 yaklaşımı:
    /// - Z <= surfaceZ ise su ortamı kabul edilir.
    /// - Z > surfaceZ ise hava ortamı kabul edilir.
    /// - İleride bu metot RuntimeWorldModel / Scenario environmentZones üzerinden beslenecek.
    ///
    /// Not:
    /// Hydronom'da Z ekseni yukarı kabul edilir.
    /// Bu yüzden sualtı konumları genellikle negatif Z değerleridir.
    /// </summary>
    private static EnvironmentSample ResolveSyntheticEnvironmentSample(
        VehicleState state,
        PhysicsOptions physics)
    {
        const double surfaceZ = 0.0;

        /*
         * V1 için taban derinliğini doğrudan config'ten okumuyoruz.
         * Şimdilik güvenli varsayılan: 2 metre test havuzu.
         * Sonraki pakette Runtime:Environment veya Scenario environmentZones üzerinden gelecek.
         */
        double floorZ = -2.0;

        if (state.Position.Z <= surfaceZ)
        {
            return EnvironmentSample.DefaultWaterPool(
                floorZ: floorZ,
                surfaceZ: surfaceZ);
        }

        return EnvironmentSample.DefaultAir() with
        {
            SurfaceZ = surfaceZ,
            FloorZ = floorZ
        };
    }

    /// <summary>
    /// Ortam farkındalıklı ilk güvenlik düzeltmesi.
    ///
    /// Amaç:
    /// - Sualtı simülasyonunda aracın su yüzeyinden yukarı uçmasını engellemek.
    /// - Aracın havuz / deniz tabanının altına düşmesini engellemek.
    /// - Yüzey ve taban temasında dikey hızı güvenli şekilde bastırmak.
    ///
    /// Bu yöntem gerçek temas çözümü değildir; V1 güvenlik clamp katmanıdır.
    /// Daha sonra ContactResolver / MediumAwarePhysicsEngine içine taşınabilir.
    /// </summary>
    private static VehicleState ApplyEnvironmentBoundaryClamp(
        VehicleState state,
        EnvironmentSample environment)
    {
        var position = state.Position;
        var velocity = state.LinearVelocity;

        bool changed = false;

        if (environment.IsWater || environment.Medium == EnvironmentMedium.Air)
        {
            if (position.Z > environment.SurfaceZ)
            {
                position = new Vec3(
                    position.X,
                    position.Y,
                    environment.SurfaceZ);

                /*
                 * Araç su yüzeyinin üstüne çıkmaya çalışıyorsa yukarı hızı sıfırla.
                 * Z yukarı olduğu için pozitif Z hızı yukarı harekettir.
                 */
                velocity = new Vec3(
                    velocity.X,
                    velocity.Y,
                    Math.Min(0.0, velocity.Z));

                changed = true;
            }

            if (position.Z < environment.FloorZ)
            {
                position = new Vec3(
                    position.X,
                    position.Y,
                    environment.FloorZ);

                /*
                 * Araç tabanın altına inmeye çalışıyorsa aşağı hızı sıfırla.
                 * Z yukarı olduğu için negatif Z hızı aşağı harekettir.
                 */
                velocity = new Vec3(
                    velocity.X,
                    velocity.Y,
                    Math.Max(0.0, velocity.Z));

                changed = true;
            }
        }

        if (!changed)
            return state.Sanitized();

        return state with
        {
            Position = position,
            LinearVelocity = velocity
        };
    }

    /// <summary>
    /// İlk environment-aware synthetic physics post process hattı.
    ///
    /// Şimdilik:
    /// - ortamı çözer,
    /// - yüzey/taban sınırlarını uygular.
    ///
    /// Sonraki paketlerde buraya:
    /// - kaldırma kuvveti,
    /// - su/hava ortamına göre drag seçimi,
    /// - akıntı/rüzgar relative velocity,
    /// - zemin teması,
    /// - sensör görüş koşulları
    /// eklenecek.
    /// </summary>
    private static VehicleState ApplyEnvironmentAwareSyntheticPhysicsPostStep(
        VehicleState state,
        PhysicsOptions physics,
        bool logVerbose,
        long tickIndex)
    {
        var environment = ResolveSyntheticEnvironmentSample(
            state,
            physics);

        var clamped = ApplyEnvironmentBoundaryClamp(
            state,
            environment);

        if (logVerbose && tickIndex % 25 == 0)
        {
            Console.WriteLine(
                $"[ENV-PHYS] {environment.CompactInfo()} " +
                $"z={clamped.Position.Z:F2} vz={clamped.LinearVelocity.Z:F2}");
        }

        return clamped.Sanitized();
    }
}