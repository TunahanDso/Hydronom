using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// COLREG kural danıştırıcısı: mevcut durum + engeller → tavsiye.
    /// Karar modülü bunu engel kaçınma aşamasında kullanır.
    /// </summary>
    public interface IColregAdvisor
    {
        ColregAdvisory Advise(VehicleState own, FusedFrame frame);
    }
}
