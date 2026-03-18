using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Sensör/fused verilerinden araç durumu kestirir.
    /// </summary>
    public interface IStateEstimator
    {
        /// <summary>Güncel araç durumu (kestirilmiş).</summary>
        VehicleState Current { get; }

        /// <summary>Zamanla çağrılır; "son bilinen" sensör/fused verilerini kullanarak durumu günceller.</summary>
        void Update(DateTime now, ISensorBus sensors);
    }
}
