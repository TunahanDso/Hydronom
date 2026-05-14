using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// WorldModel / senaryo içindeki ortam bölgelerinden aracın o anki ortamını çözer.
    /// V1 basit AABB zone seçimi yapar. İleride mesh, terrain, fluid field ve material map desteklenebilir.
    /// </summary>
    public sealed class EnvironmentResolver
    {
        private readonly List<EnvironmentZone> _zones = new();

        public EnvironmentResolver()
        {
        }

        public EnvironmentResolver(IEnumerable<EnvironmentZone> zones)
        {
            SetZones(zones);
        }

        public IReadOnlyList<EnvironmentZone> Zones => _zones;

        public void SetZones(IEnumerable<EnvironmentZone>? zones)
        {
            _zones.Clear();

            if (zones is null)
                return;

            foreach (var zone in zones)
                _zones.Add(zone.Sanitized());
        }

        public void AddZone(EnvironmentZone zone)
        {
            _zones.Add(zone.Sanitized());
        }

        public EnvironmentSample Resolve(Vec3 position)
        {
            var zone = _zones
                .Where(z => z.Contains(position))
                .OrderByDescending(z => z.Priority)
                .FirstOrDefault();

            if (zone is null)
            {
                // V1 güvenli fallback:
                // Z <= 0 ise su, Z > 0 ise hava kabul edilir.
                return position.Z <= 0.0
                    ? EnvironmentSample.DefaultWaterPool()
                    : EnvironmentSample.DefaultAir();
            }

            return new EnvironmentSample
            {
                Medium = zone.Medium,
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                WaterDensityKgM3 = zone.WaterDensityKgM3,
                AirDensityKgM3 = zone.AirDensityKgM3,
                GravityMps2 = zone.GravityMps2,
                CurrentWorld = zone.CurrentWorld,
                WindWorld = zone.WindWorld,
                SurfaceZ = zone.SurfaceZ,
                FloorZ = zone.FloorZ,
                VisibilityMeters = zone.VisibilityMeters,
                FrictionCoefficient = zone.FrictionCoefficient
            };
        }
    }
}
