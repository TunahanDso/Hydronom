using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// SensÃ¶r/fÃ¼zyon verilerinin "son bilinen" Ã¶rneklerini paylaÅŸan hafif veri otobÃ¼sÃ¼.
    /// Adapters yayÄ±nlar, Core tÃ¼ketir.
    ///
    /// Not:
    ///   - T hem class hem struct (Ã¶r. record struct ImuData) olabilir.
    ///   - Ä°Ã§ implementasyon genelde tip baÅŸÄ±na tek bir "last sample" tutar.
    /// </summary>
    public interface ISensorBus
    {
        /// <summary>
        /// Son bilinen veriyi dÃ¶ndÃ¼rÃ¼r (varsa true).
        /// T hem referans tip (record class) hem deÄŸer tipi (record struct) olabilir.
        /// </summary>
        /// <typeparam name="T">SensÃ¶r verisi veya fused veri tipi.</typeparam>
        /// <param name="data">Bulunursa son Ã¶rnek; bulunamazsa default(T).</param>
        /// <returns>true â†’ veri bulunursa, false â†’ hiÃ§ kayÄ±t yoksa.</returns>
        bool TryGetLast<T>(out T data);

        /// <summary>
        /// Ä°lgili tipin son zaman damgasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r (yoksa null).
        /// </summary>
        /// <typeparam name="T">SensÃ¶r verisi veya fused veri tipi.</typeparam>
        /// <returns>Son Ã¶rneÄŸin zaman damgasÄ±; yoksa null.</returns>
        DateTime? LastStampOf<T>();
    }

    /// <summary>
    /// FÃ¼zyonlanmÄ±ÅŸ durum (FusedFrame/VehicleState vb.) iÃ§in ayrÄ± kanal gerekiyorsa kullanÄ±lÄ±r.
    /// </summary>
    public interface IFusedBus : ISensorBus
    {
        /// <summary>Yeni bir fused Ã§erÃ§eve geldiÄŸinde tetiklenir.</summary>
        event Action<object>? FusedUpdated;
    }
}

