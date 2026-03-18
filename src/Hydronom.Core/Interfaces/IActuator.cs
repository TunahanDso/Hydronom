using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Her türlü aktüatör/itki uygulayıcısı için ortak arayüz.
    /// 
    /// Bu arayüz, kontrol katmanından gelen 6-DoF komutunu (DecisionCommand)
    /// alır ve ilgili motor kontrol sistemine uygular:
    /// 
    /// DecisionCommand içeriği:
    ///   - Fx, Fy, Fz : gövde ekseninde lineer kuvvet komutları
    ///   - Tx, Ty, Tz : gövde ekseninde tork komutları
    /// 
    /// Uygulamalar şunlar olabilir:
    ///   - Gerçek gömülü sistem (STM32/ESC/PWM driver)
    ///   - Simülasyon motor modeli
    ///   - Unity/ROS köprüleri
    ///   - Kayıt/log oluşturucu
    /// 
    /// Not: Eski API’den kalan Throttle01 / RudderNeg1To1 özellikleri
    /// DecisionCommand içinde hâlâ desteklenir, ancak tam 6DoF komutlar
    /// üretildiği sürece tüm eksenler aktif kullanılmalıdır.
    /// </summary>
    public interface IActuator
    {
        /// <summary>
        /// Tam 6-DoF kuvvet/tork komutunu uygular.
        /// Command içeriği:
        ///   Fx, Fy, Fz : lineer kuvvetler
        ///   Tx, Ty, Tz : tork bileşenleri
        /// </summary>
        void Apply(DecisionCommand cmd);
    }
}
