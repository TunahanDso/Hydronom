using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// SensÃ¶r/fused verilerinden araÃ§ durumu kestirir.
    /// </summary>
    public interface IStateEstimator
    {
        /// <summary>GÃ¼ncel araÃ§ durumu (kestirilmiÅŸ).</summary>
        VehicleState Current { get; }

        /// <summary>Zamanla Ã§aÄŸrÄ±lÄ±r; "son bilinen" sensÃ¶r/fused verilerini kullanarak durumu gÃ¼nceller.</summary>
        void Update(DateTime now, ISensorBus sensors);
    }
}

