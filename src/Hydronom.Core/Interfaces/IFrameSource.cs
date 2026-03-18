using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces;

/// <summary>
/// PLATFORM-BAĞIMSIZ KUVVETLİ FRAME KAYNAĞI ARAYÜZÜ
/// -----------------------------------------------------------
/// Amaç:
///   - Sensör füzyonundan gelen "FusedFrame" akışını tek tip bir arayüzle sağlamak.
///   - Kaynak türü tamamen soyutlanır:
///       • TCP JSON (Python → C#)
///       • ROS bridge / Foxglove
///       • Simulator (Unity / Gazebo)
///       • Replay (kayıt dosyasından okuma)
///       • Pipe / named pipe / shared memory
///
/// Kullanım Modeli:
///   - Runtime ana döngüsü her tick’te TryGetLatestFrame() çağırır.
///   - Eğer frame yeniyse (fresh) döner.
///   - Eğer güncel bir şey yoksa false döner; çağıran fallback kullanabilir.
///
/// Tazelik İlkesi:
///   - Kaynak implementasyonu son alınan frame’i saklar.
///   - Ayrıca frame üzerinde genelde:
///         • frame.Seq (sıra numarası)
///         • frame.Timestamp (kaynağın ürettiği zaman)
///     bulunur. TryGetLatestFrame yalnızca “yeni” olanı döndürmelidir.
/// 
/// NOT:
///   - Tazelik eşiği (örn. 300 ms) kaynak implementasyonunda uygulanabilir.
///   - TryGetLatestFrame() kesinlikle thread-safe olmalıdır.
///
/// Önerilen Ek Davranış (zorunlu değil):
///   - IO kapanırken kaynak IDisposable olabilir.
///   - Replay kaynakları Pause/Seek özellikleri ekleyebilir.
/// </summary>
public interface IFrameSource
{
    /// <summary>
    /// Son (en taze) frame’i döndürmeye çalışır.
    /// </summary>
    /// <param name="frame">Taze bir <see cref="FusedFrame"/> varsa atanır, yoksa null.</param>
    /// <returns>
    ///   true  → Yeni / taze bir frame ulaştı (frame != null).  
    ///   false → Kaynakta yeni frame yok (frame == null).
    /// </returns>
    bool TryGetLatestFrame(out FusedFrame? frame);
}
