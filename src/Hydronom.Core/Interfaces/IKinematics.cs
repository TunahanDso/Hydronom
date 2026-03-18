using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// PLATFORMDAN BAĞIMSIZ SİMÜLASYON KİNEMATİK MODELİ
    /// -----------------------------------------------------------
    /// Gerçek donanımda kullanılmayabilir, yalnızca simülasyon katmanında
    /// 6-DoF hareket modeline ek kuvvetler, sürükleme, su kaldırma veya 
    /// basitleştirilmiş kinematik integrasyon eklemek için kullanılır.
    ///
    /// Not:
    ///  - VehicleState'in kendisi dışarıda tutulur (örn. SimStateManager).
    ///  - Bu arayüz 'Propagate' adımıyla iç modelini dt kadar ilerletir.
    ///  - Gerçek donanımda uygulanması zorunlu değildir.
    /// </summary>
    public interface IKinematics
    {
        /// <summary>
        /// Simülasyon zamanını dt saniye ilerletir.
        /// İç kinematik/dinamik modeli günceller.
        /// </summary>
        void Propagate(double dtSeconds);
    }
}
