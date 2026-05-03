using System;
using Hydronom.Core.Telemetry.World;

namespace Hydronom.Runtime.Simulation.World
{
    /// <summary>
    /// RuntimeSimWorld modelini Ops/Gateway/Ground Station iÃ§in WorldTelemetryFrame'e Ã§evirir.
    ///
    /// Bu sÄ±nÄ±f Gateway'e doÄŸrudan baÄŸÄ±mlÄ± deÄŸildir.
    /// Sadece telemetry frame Ã¼retir.
    /// Gateway veya Ground Station bunu daha sonra kendi transport formatÄ±na Ã§evirebilir.
    /// </summary>
    public sealed class RuntimeWorldTelemetryProjector
    {
        private readonly RuntimeSimWorldStore _store;

        public RuntimeWorldTelemetryProjector(RuntimeSimWorldStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public WorldTelemetryFrame Project()
        {
            var runtimeWorld = _store.GetRuntimeWorld().Sanitized();

            return WorldTelemetryFrame.FromWorld(
                world: runtimeWorld.World,
                environment: runtimeWorld.Environment,
                missionObjects: runtimeWorld.MissionObjects,
                source: runtimeWorld.Source
            ).Sanitized();
        }
    }
}
