using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// PLATFORMDAN BAÄIMSIZ SÄ°MÃœLASYON KÄ°NEMATÄ°K MODELÄ°
    /// -----------------------------------------------------------
    /// GerÃ§ek donanÄ±mda kullanÄ±lmayabilir, yalnÄ±zca simÃ¼lasyon katmanÄ±nda
    /// 6-DoF hareket modeline ek kuvvetler, sÃ¼rÃ¼kleme, su kaldÄ±rma veya 
    /// basitleÅŸtirilmiÅŸ kinematik integrasyon eklemek iÃ§in kullanÄ±lÄ±r.
    ///
    /// Not:
    ///  - VehicleState'in kendisi dÄ±ÅŸarÄ±da tutulur (Ã¶rn. SimStateManager).
    ///  - Bu arayÃ¼z 'Propagate' adÄ±mÄ±yla iÃ§ modelini dt kadar ilerletir.
    ///  - GerÃ§ek donanÄ±mda uygulanmasÄ± zorunlu deÄŸildir.
    /// </summary>
    public interface IKinematics
    {
        /// <summary>
        /// SimÃ¼lasyon zamanÄ±nÄ± dt saniye ilerletir.
        /// Ä°Ã§ kinematik/dinamik modeli gÃ¼nceller.
        /// </summary>
        void Propagate(double dtSeconds);
    }
}

