using Hydronom.Core.Domain;
using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Karar/denetim Ã§Ä±ktÄ±sÄ±nÄ± uygulayan Ã§oklayÄ±cÄ± arayÃ¼z.
    /// 
    /// AmaÃ§:
    ///   - GNC / Decision katmanÄ±ndan gelen 6-DoF komutlarÄ± (DecisionCommand)
    ///     bir veya birden fazla alt aktÃ¼atÃ¶re (IActuator, logger, sim vb.)
    ///     fan-out etmek.
    ///   - AynÄ± komutu telemetri/log iÃ§in saklayÄ±p yayÄ±mlamak.
    /// 
    /// DecisionCommand, gÃ¶vde ekseninde 6-DoF wrench taÅŸÄ±r:
    ///   Fx, Fy, Fz : lineer kuvvet bileÅŸenleri
    ///   Tx, Ty, Tz : tork bileÅŸenleri
    /// 
    /// Eski planar API (Throttle01 / RudderNeg1To1) DecisionCommand iÃ§inde
    /// alias olarak hÃ¢lÃ¢ mevcuttur, ancak bus tarafÄ±nda tam 6DoF komut
    /// taÅŸÄ±ndÄ±ÄŸÄ± varsayÄ±lÄ±r.
    /// </summary>
    public interface IActuatorBus
    {
        /// <summary>
        /// GNC / Decision Ã§Ä±ktÄ±sÄ± olan 6-DoF komutu bus Ã¼zerinden uygular.
        /// Tipik olarak:
        ///   - Alttaki IActuator(lar)a iletilir,
        ///   - LastApplied alanÄ±na yazÄ±lÄ±r,
        ///   - Applied olayÄ± tetiklenir.
        /// </summary>
        /// <param name="cmd">
        /// Fx, Fy, Fz, Tx, Ty, Tz bileÅŸenlerini iÃ§eren karar komutu.
        /// </param>
        void Apply(DecisionCommand cmd);

        /// <summary>
        /// Son uygulanan 6-DoF komut (telemetri, log veya debugging iÃ§in).
        /// Komut hiÃ§ uygulanmadÄ±ysa null olabilir.
        /// </summary>
        DecisionCommand? LastApplied { get; }

        /// <summary>
        /// Yeni bir komut bus Ã¼zerinden baÅŸarÄ±yla uygulandÄ±ÄŸÄ±nda tetiklenir.
        /// Dinleyiciler, DecisionCommand iÃ§eriÄŸini loglama, GUI gÃ¼ncelleme
        /// veya ek analiz iÃ§in kullanabilir.
        /// </summary>
        event Action<DecisionCommand>? Applied;
    }
}

