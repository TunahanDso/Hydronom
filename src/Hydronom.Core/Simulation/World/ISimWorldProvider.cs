using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nÄ± salt okunur saÄŸlayan ortak sÃ¶zleÅŸme.
    ///
    /// Sim sensÃ¶rler, physics adapter, telemetry projector ve Ops/Gateway baÄŸlantÄ±larÄ±
    /// dÃ¼nya state'ini bu arayÃ¼z Ã¼zerinden okuyabilir.
    /// </summary>
    public interface ISimWorldProvider
    {
        string ProviderName { get; }

        bool IsAvailable { get; }

        DateTime LastWorldUpdateUtc { get; }

        SimWorldState GetLatestWorld();

        SimWorldSnapshot GetSnapshot();

        ValueTask<SimWorldState> GetLatestWorldAsync(CancellationToken cancellationToken = default);
    }
}
