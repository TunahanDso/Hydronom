using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Abstractions
{
    /// <summary>
    /// Hydronom sensÃ¶r runtime sÃ¶zleÅŸmesi.
    ///
    /// Runtime birden fazla backend'i yÃ¶netir, sample toplar, health Ã¼retir ve
    /// Ã¼st katmana ortak SensorSample akÄ±ÅŸÄ± saÄŸlar.
    /// </summary>
    public interface ISensorRuntime
    {
        SensorRuntimeMode Mode { get; }

        bool IsRunning { get; }

        SensorCapabilitySet Capabilities { get; }

        ValueTask StartAsync(CancellationToken cancellationToken = default);

        ValueTask StopAsync(CancellationToken cancellationToken = default);

        ValueTask<IReadOnlyList<SensorSample>> ReadBatchAsync(CancellationToken cancellationToken = default);

        SensorRuntimeHealth GetHealth();
    }
}

