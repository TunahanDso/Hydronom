using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Abstractions
{
    /// <summary>
    /// SensÃ¶r backend sÃ¶zleÅŸmesi.
    ///
    /// Backend veri kaynaÄŸÄ±dÄ±r:
    /// - sim backend
    /// - real hardware backend
    /// - replay backend
    /// - serial/network backend
    /// </summary>
    public interface ISensorBackend : ISensor
    {
        bool IsOpen { get; }

        ValueTask OpenAsync(CancellationToken cancellationToken = default);

        ValueTask CloseAsync(CancellationToken cancellationToken = default);

        ValueTask<SensorSample?> ReadAsync(CancellationToken cancellationToken = default);

        ValueTask<SensorHealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}

