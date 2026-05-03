using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Twin
{
    /// <summary>
    /// Runtime iÃ§ durumundan twin mesajlarÄ± Ã¼retip dÄ±ÅŸ dÃ¼nyaya yayÄ±nlayan kÃ¶prÃ¼ arayÃ¼zÃ¼.
    ///
    /// AmaÃ§:
    /// - C# runtime state'ini Python csharp_sim backend'lerine aktarmak
    /// - TwinGps ve TwinImu benzeri mesajlarÄ± tek bir yayÄ±n katmanÄ±nda toplamak
    /// - GerÃ§ek yayÄ±n yÃ¶ntemi (TCP, baÅŸka bir taÅŸÄ±yÄ±cÄ± vs.) implementasyona bÄ±rakmak
    /// </summary>
    public interface ITwinPublisher
    {
        /// <summary>
        /// Mevcut araÃ§ durumundan twin mesajlarÄ±nÄ± yayÄ±nlar.
        /// </summary>
        Task PublishAsync(VehicleState state, CancellationToken ct = default);
    }
}
