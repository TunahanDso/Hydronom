using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Sensors.Common.Connections;
using Hydronom.Core.Sensors.Common.Discovery;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Abstractions
{
    /// <summary>
    /// Sensör keşif probe sözleşmesi.
    ///
    /// Probe sensörü sürekli çalıştırmaz.
    /// Sadece belirli bir bağlantıda belirli bir sensör/backend çalışabilir mi diye güvenli test yapar.
    ///
    /// Örnek:
    /// - RPLIDAR probe: serial portta RPLIDAR handshake dener.
    /// - LDRobot D500 probe: frame header/protokol işareti arar.
    /// - NMEA GPS probe: NMEA cümlesi yakalar.
    /// - Sim probe: sim backend adayını üretir.
    /// </summary>
    public interface ISensorProbe
    {
        string ProbeId { get; }

        SensorDataKind DataKind { get; }

        string BackendKey { get; }

        bool CanProbe(SensorConnectionDescriptor connection);

        ValueTask<SensorDiscoveryCandidate?> ProbeAsync(
            SensorConnectionDescriptor connection,
            CancellationToken cancellationToken = default);
    }
}