using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Her tÃ¼rlÃ¼ aktÃ¼atÃ¶r/itki uygulayÄ±cÄ±sÄ± iÃ§in ortak arayÃ¼z.
    /// 
    /// Bu arayÃ¼z, kontrol katmanÄ±ndan gelen 6-DoF komutunu (DecisionCommand)
    /// alÄ±r ve ilgili motor kontrol sistemine uygular:
    /// 
    /// DecisionCommand iÃ§eriÄŸi:
    ///   - Fx, Fy, Fz : gÃ¶vde ekseninde lineer kuvvet komutlarÄ±
    ///   - Tx, Ty, Tz : gÃ¶vde ekseninde tork komutlarÄ±
    /// 
    /// Uygulamalar ÅŸunlar olabilir:
    ///   - GerÃ§ek gÃ¶mÃ¼lÃ¼ sistem (STM32/ESC/PWM driver)
    ///   - SimÃ¼lasyon motor modeli
    ///   - Unity/ROS kÃ¶prÃ¼leri
    ///   - KayÄ±t/log oluÅŸturucu
    /// 
    /// Not: Eski APIâ€™den kalan Throttle01 / RudderNeg1To1 Ã¶zellikleri
    /// DecisionCommand iÃ§inde hÃ¢lÃ¢ desteklenir, ancak tam 6DoF komutlar
    /// Ã¼retildiÄŸi sÃ¼rece tÃ¼m eksenler aktif kullanÄ±lmalÄ±dÄ±r.
    /// </summary>
    public interface IActuator
    {
        /// <summary>
        /// Tam 6-DoF kuvvet/tork komutunu uygular.
        /// Command iÃ§eriÄŸi:
        ///   Fx, Fy, Fz : lineer kuvvetler
        ///   Tx, Ty, Tz : tork bileÅŸenleri
        /// </summary>
        void Apply(DecisionCommand cmd);
    }
}

