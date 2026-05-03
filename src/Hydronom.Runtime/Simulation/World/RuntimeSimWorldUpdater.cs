using System;
using Hydronom.Core.Simulation.Environment;
using Hydronom.Core.Simulation.MissionObjects;
using Hydronom.Core.Simulation.World;

namespace Hydronom.Runtime.Simulation.World
{
    /// <summary>
    /// Runtime simÃ¼lasyon dÃ¼nyasÄ±nÄ± gÃ¼ncelleyen servis.
    ///
    /// Bu sÄ±nÄ±f ileride:
    /// - senaryo adÄ±mÄ±
    /// - dinamik engel hareketi
    /// - hava/akÄ±ntÄ±/rÃ¼zgar deÄŸiÅŸimi
    /// - gÃ¶rev nesnesi tamamlanma durumu
    /// - Ops mission editor deÄŸiÅŸiklikleri
    ///
    /// iÃ§in geniÅŸletilecek ana gÃ¼ncelleme noktasÄ±dÄ±r.
    /// </summary>
    public sealed class RuntimeSimWorldUpdater
    {
        private readonly RuntimeSimWorldStore _store;

        public RuntimeSimWorldUpdater(RuntimeSimWorldStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public RuntimeSimWorld Tick(double dtSeconds)
        {
            var safeDt = SanitizeDt(dtSeconds);
            var runtimeWorld = _store.GetRuntimeWorld();

            if (!runtimeWorld.Enabled)
                return runtimeWorld;

            // Ä°lk sÃ¼rÃ¼mde dÃ¼nya nesnelerini fiziksel olarak hareket ettirmiyoruz.
            // Bu sÄ±nÄ±f bilinÃ§li olarak iskelet halinde bÄ±rakÄ±ldÄ±.
            // Ä°leride dynamic obstacles, scripted scenario ve environmental drift burada ilerletilecek.
            var updated = runtimeWorld with
            {
                TimestampUtc = DateTime.UtcNow,
                World = runtimeWorld.World.Sanitized() with
                {
                    TimestampUtc = DateTime.UtcNow,
                    Summary = $"Runtime world ticked. dt={safeDt:F3}s"
                },
                Summary = $"Runtime world updated. dt={safeDt:F3}s"
            };

            _store.SetRuntimeWorld(updated);

            return updated;
        }

        public RuntimeSimWorld SetEnvironment(SimEnvironmentState environment)
        {
            var runtimeWorld = _store.GetRuntimeWorld().WithEnvironment(environment);
            _store.SetRuntimeWorld(runtimeWorld);
            return runtimeWorld;
        }

        public RuntimeSimWorld AddOrUpdateMissionObject(SimMissionObject missionObject, string layerId = "mission_objects")
        {
            var runtimeWorld = _store.GetRuntimeWorld().WithMissionObject(missionObject, layerId);
            _store.SetRuntimeWorld(runtimeWorld);
            return runtimeWorld;
        }

        public RuntimeSimWorld RemoveMissionObject(string missionObjectId)
        {
            var runtimeWorld = _store.GetRuntimeWorld().WithoutMissionObject(missionObjectId);
            _store.SetRuntimeWorld(runtimeWorld);
            return runtimeWorld;
        }

        public RuntimeSimWorld AddOrUpdateWorldObject(SimWorldObject obj, string layerId = "world_objects")
        {
            var runtimeWorld = _store.GetRuntimeWorld().WithObject(obj, layerId);
            _store.SetRuntimeWorld(runtimeWorld);
            return runtimeWorld;
        }

        public RuntimeSimWorld RemoveWorldObject(string objectId)
        {
            var runtimeWorld = _store.GetRuntimeWorld().WithoutObject(objectId);
            _store.SetRuntimeWorld(runtimeWorld);
            return runtimeWorld;
        }

        private static double SanitizeDt(double dtSeconds)
        {
            if (!double.IsFinite(dtSeconds))
                return 0.0;

            if (dtSeconds < 0.0)
                return 0.0;

            if (dtSeconds > 1.0)
                return 1.0;

            return dtSeconds;
        }
    }
}
