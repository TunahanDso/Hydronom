using System;
using Hydronom.Core.Domain;
using Hydronom.Core.World;

partial class Program
{
    /// <summary>
    /// Runtime iÃ§ simÃ¼lasyon iÃ§in aracÄ±n bulunduÄŸu ortamÄ± Ã§Ã¶zer.
    ///
    /// VP9A-1 yaklaÅŸÄ±mÄ±:
    /// - Ortam artÄ±k doÄŸrudan hard-coded surface/floor deÄŸerlerinden deÄŸil,
    ///   WorldModel.SampleAt(position) Ã¼zerinden alÄ±nÄ±r.
    /// - BÃ¶ylece ileride scenario/world surfaceZ, floorZ, current zone,
    ///   visibility zone ve pipe/tunnel katmanlarÄ± aynÄ± Ã¶rnek Ã¼zerinden taÅŸÄ±nabilir.
    ///
    /// Not:
    /// Hydronom'da Z ekseni yukarÄ± kabul edilir.
    /// Bu yÃ¼zden sualtÄ± konumlarÄ± genellikle negatif Z deÄŸerleridir.
    /// </summary>
    private static EnvironmentSample ResolveSyntheticEnvironmentSample(
        VehicleState state,
        PhysicsOptions physics,
        WorldOptions worldOptions)
    {
        var world = CreateWorldPhysicsModel(worldOptions);
        var worldSample = world.SampleAt(state.Position);

        return worldSample.Environment;
    }

    /// <summary>
    /// Ortam farkÄ±ndalÄ±klÄ± ilk gÃ¼venlik dÃ¼zeltmesi.
    ///
    /// AmaÃ§:
    /// - SualtÄ± simÃ¼lasyonunda aracÄ±n su yÃ¼zeyinden yukarÄ± uÃ§masÄ±nÄ± engellemek.
    /// - AracÄ±n havuz / deniz tabanÄ±nÄ±n altÄ±na dÃ¼ÅŸmesini engellemek.
    /// - YÃ¼zey ve taban temasÄ±nda dikey hÄ±zÄ± gÃ¼venli ÅŸekilde bastÄ±rmak.
    ///
    /// Bu yÃ¶ntem gerÃ§ek temas Ã§Ã¶zÃ¼mÃ¼ deÄŸildir; VP9A iÃ§inde ContactModel /
    /// WorldPhysicsEngine tarafÄ±na taÅŸÄ±nacak gÃ¼venlik clamp katmanÄ±dÄ±r.
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
                 * AraÃ§ su yÃ¼zeyinin Ã¼stÃ¼ne Ã§Ä±kmaya Ã§alÄ±ÅŸÄ±yorsa yukarÄ± hÄ±zÄ± sÄ±fÄ±rla.
                 * Z yukarÄ± olduÄŸu iÃ§in pozitif Z hÄ±zÄ± yukarÄ± harekettir.
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
                 * AraÃ§ tabanÄ±n altÄ±na inmeye Ã§alÄ±ÅŸÄ±yorsa aÅŸaÄŸÄ± hÄ±zÄ± sÄ±fÄ±rla.
                 * Z yukarÄ± olduÄŸu iÃ§in negatif Z hÄ±zÄ± aÅŸaÄŸÄ± harekettir.
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
    /// Environment-aware synthetic physics post process hattÄ±.
    ///
    /// VP9A-1:
    /// - ortamÄ± WorldModel Ã¼zerinden Ã§Ã¶zer,
    /// - yÃ¼zey/taban sÄ±nÄ±rlarÄ±nÄ± uygular.
    ///
    /// Sonraki paketlerde buraya veya WorldPhysicsEngine iÃ§ine:
    /// - kaldÄ±rma kuvveti,
    /// - su/hava ortamÄ±na gÃ¶re drag seÃ§imi,
    /// - akÄ±ntÄ±/rÃ¼zgar relative velocity,
    /// - zemin temasÄ±,
    /// - sensÃ¶r gÃ¶rÃ¼ÅŸ koÅŸullarÄ±
    /// eklenecek.
    /// </summary>
    private static VehicleState ApplyEnvironmentAwareSyntheticPhysicsPostStep(
        VehicleState state,
        PhysicsOptions physics,
        WorldOptions worldOptions,
        bool logVerbose,
        long tickIndex)
    {
        var environment = ResolveSyntheticEnvironmentSample(
            state,
            physics,
            worldOptions);

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