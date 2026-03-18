using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Tek bir sensör için temel meta bilgiler.
    /// 
    /// Notlar:
    ///   - Bu meta bilgiler FusedFrame üreticisi (FrameSource) tarafından da
    ///     Capability mesajlarında gösterilir.
    ///   - Donanım/sim ayrımı yapılmaz; sadece kimlik + sağlık bilgisi.
    /// </summary>
    public interface ISensor
    {
        /// <summary>Sensör adı (örn. "IMU", "GPS", "LiDAR", "Camera").</summary>
        string Name { get; }

        /// <summary>Bu sensörün yayın yaptığı frame id (örn. "imu_link").</summary>
        string FrameId { get; }

        /// <summary>Nominal yayın oranı (Hz).</summary>
        double RateHz { get; }

        /// <summary>Sağlık durumu (örn. bağlantı var mı?).</summary>
        bool IsHealthy { get; }

        /// <summary>Son gelen örnek zaman damgası.</summary>
        DateTime? LastStamp { get; }

        /// <summary>
        /// Son örneğin tazeliği (ms).
        /// LastStamp yoksa null döner.
        /// FrameSource ve Diagnostik için önemlidir.
        /// </summary>
        double? AgeMs
        {
            get
            {
                if (LastStamp is null) return null;
                return (DateTime.UtcNow - LastStamp.Value).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Sensör kategorisi (IMU/GPS/LiDAR/Kamera vb.).
        /// Konsol ve UI tarafındaki Capability listesinde kullanılır.
        /// </summary>
        string Type { get; }
    }
}
