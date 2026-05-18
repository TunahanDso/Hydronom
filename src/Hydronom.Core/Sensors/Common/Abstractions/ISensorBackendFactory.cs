using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Sensors.Common.Discovery;

namespace Hydronom.Core.Sensors.Common.Abstractions
{
    /// <summary>
    /// Keşfedilmiş adaydan gerçek backend üreten factory sözleşmesi.
    ///
    /// Registry string key ile backend üretmeye devam edebilir;
    /// ama plug-and-play sistemde bir noktadan sonra candidate bilgisi de gerekir:
    /// port, baudrate, protocol, metadata, mount/config eşleşmesi vb.
    /// </summary>
    public interface ISensorBackendFactory
    {
        string BackendKey { get; }

        bool CanCreate(SensorDiscoveryCandidate candidate);

        ValueTask<ISensorBackend> CreateAsync(
            SensorDiscoveryCandidate candidate,
            IServiceProvider? services = null,
            CancellationToken cancellationToken = default);
    }
}