using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Simulation.Environment;
using Hydronom.Core.Simulation.MissionObjects;
using Hydronom.Core.Simulation.World;

namespace Hydronom.Runtime.Simulation.World
{
    /// <summary>
    /// RuntimeSimWorldStore iÃ§in okunabilir provider adaptÃ¶rÃ¼.
    ///
    /// Bu sÄ±nÄ±f Ã¶zellikle baÅŸka modÃ¼llerin store'u doÄŸrudan deÄŸiÅŸtirmeden dÃ¼nya state'ini
    /// okumasÄ± iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public sealed class RuntimeSimWorldProvider : ISimWorldProvider
    {
        private readonly RuntimeSimWorldStore _store;

        public RuntimeSimWorldProvider(RuntimeSimWorldStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string ProviderName => "RuntimeSimWorldProvider";

        public bool IsAvailable => _store.IsAvailable;

        public DateTime LastWorldUpdateUtc => _store.LastWorldUpdateUtc;

        public SimWorldState GetLatestWorld()
        {
            return _store.GetLatestWorld();
        }

        public RuntimeSimWorld GetRuntimeWorld()
        {
            return _store.GetRuntimeWorld();
        }

        public SimEnvironmentState GetEnvironment()
        {
            return _store.GetRuntimeWorld().Environment.Sanitized();
        }

        public IReadOnlyList<SimMissionObject> GetMissionObjects()
        {
            return _store.GetRuntimeWorld().MissionObjects;
        }

        public SimWorldSnapshot GetSnapshot()
        {
            return _store.GetSnapshot();
        }

        public ValueTask<SimWorldState> GetLatestWorldAsync(CancellationToken cancellationToken = default)
        {
            return _store.GetLatestWorldAsync(cancellationToken);
        }
    }
}
