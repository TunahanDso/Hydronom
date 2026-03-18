using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Twin
{
    /// <summary>
    /// Runtime iç durumundan twin mesajları üretip dış dünyaya yayınlayan köprü arayüzü.
    ///
    /// Amaç:
    /// - C# runtime state'ini Python csharp_sim backend'lerine aktarmak
    /// - TwinGps ve TwinImu benzeri mesajları tek bir yayın katmanında toplamak
    /// - Gerçek yayın yöntemi (TCP, başka bir taşıyıcı vs.) implementasyona bırakmak
    /// </summary>
    public interface ITwinPublisher
    {
        /// <summary>
        /// Mevcut araç durumundan twin mesajlarını yayınlar.
        /// </summary>
        Task PublishAsync(VehicleState state, CancellationToken ct = default);
    }
}