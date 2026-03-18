using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Sensör/füzyon verilerinin "son bilinen" örneklerini paylaşan hafif veri otobüsü.
    /// Adapters yayınlar, Core tüketir.
    ///
    /// Not:
    ///   - T hem class hem struct (ör. record struct ImuData) olabilir.
    ///   - İç implementasyon genelde tip başına tek bir "last sample" tutar.
    /// </summary>
    public interface ISensorBus
    {
        /// <summary>
        /// Son bilinen veriyi döndürür (varsa true).
        /// T hem referans tip (record class) hem değer tipi (record struct) olabilir.
        /// </summary>
        /// <typeparam name="T">Sensör verisi veya fused veri tipi.</typeparam>
        /// <param name="data">Bulunursa son örnek; bulunamazsa default(T).</param>
        /// <returns>true → veri bulunursa, false → hiç kayıt yoksa.</returns>
        bool TryGetLast<T>(out T data);

        /// <summary>
        /// İlgili tipin son zaman damgasını döndürür (yoksa null).
        /// </summary>
        /// <typeparam name="T">Sensör verisi veya fused veri tipi.</typeparam>
        /// <returns>Son örneğin zaman damgası; yoksa null.</returns>
        DateTime? LastStampOf<T>();
    }

    /// <summary>
    /// Füzyonlanmış durum (FusedFrame/VehicleState vb.) için ayrı kanal gerekiyorsa kullanılır.
    /// </summary>
    public interface IFusedBus : ISensorBus
    {
        /// <summary>Yeni bir fused çerçeve geldiğinde tetiklenir.</summary>
        event Action<object>? FusedUpdated;
    }
}
