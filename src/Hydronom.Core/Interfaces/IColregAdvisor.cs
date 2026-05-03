using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// COLREG kural danÄ±ÅŸtÄ±rÄ±cÄ±sÄ±: mevcut durum + engeller â†’ tavsiye.
    /// Karar modÃ¼lÃ¼ bunu engel kaÃ§Ä±nma aÅŸamasÄ±nda kullanÄ±r.
    /// </summary>
    public interface IColregAdvisor
    {
        ColregAdvisory Advise(VehicleState own, FusedFrame frame);
    }
}

