using Hydronom.Core.Domain;
using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Karar/denetim çıktısını uygulayan çoklayıcı arayüz.
    /// 
    /// Amaç:
    ///   - GNC / Decision katmanından gelen 6-DoF komutları (DecisionCommand)
    ///     bir veya birden fazla alt aktüatöre (IActuator, logger, sim vb.)
    ///     fan-out etmek.
    ///   - Aynı komutu telemetri/log için saklayıp yayımlamak.
    /// 
    /// DecisionCommand, gövde ekseninde 6-DoF wrench taşır:
    ///   Fx, Fy, Fz : lineer kuvvet bileşenleri
    ///   Tx, Ty, Tz : tork bileşenleri
    /// 
    /// Eski planar API (Throttle01 / RudderNeg1To1) DecisionCommand içinde
    /// alias olarak hâlâ mevcuttur, ancak bus tarafında tam 6DoF komut
    /// taşındığı varsayılır.
    /// </summary>
    public interface IActuatorBus
    {
        /// <summary>
        /// GNC / Decision çıktısı olan 6-DoF komutu bus üzerinden uygular.
        /// Tipik olarak:
        ///   - Alttaki IActuator(lar)a iletilir,
        ///   - LastApplied alanına yazılır,
        ///   - Applied olayı tetiklenir.
        /// </summary>
        /// <param name="cmd">
        /// Fx, Fy, Fz, Tx, Ty, Tz bileşenlerini içeren karar komutu.
        /// </param>
        void Apply(DecisionCommand cmd);

        /// <summary>
        /// Son uygulanan 6-DoF komut (telemetri, log veya debugging için).
        /// Komut hiç uygulanmadıysa null olabilir.
        /// </summary>
        DecisionCommand? LastApplied { get; }

        /// <summary>
        /// Yeni bir komut bus üzerinden başarıyla uygulandığında tetiklenir.
        /// Dinleyiciler, DecisionCommand içeriğini loglama, GUI güncelleme
        /// veya ek analiz için kullanabilir.
        /// </summary>
        event Action<DecisionCommand>? Applied;
    }
}
