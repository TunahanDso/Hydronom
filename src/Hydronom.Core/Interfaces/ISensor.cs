using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Tek bir sensÃ¶r iÃ§in temel meta bilgiler.
    /// 
    /// Notlar:
    ///   - Bu meta bilgiler FusedFrame Ã¼reticisi (FrameSource) tarafÄ±ndan da
    ///     Capability mesajlarÄ±nda gÃ¶sterilir.
    ///   - DonanÄ±m/sim ayrÄ±mÄ± yapÄ±lmaz; sadece kimlik + saÄŸlÄ±k bilgisi.
    /// </summary>
    public interface ISensor
    {
        /// <summary>SensÃ¶r adÄ± (Ã¶rn. "IMU", "GPS", "LiDAR", "Camera").</summary>
        string Name { get; }

        /// <summary>Bu sensÃ¶rÃ¼n yayÄ±n yaptÄ±ÄŸÄ± frame id (Ã¶rn. "imu_link").</summary>
        string FrameId { get; }

        /// <summary>Nominal yayÄ±n oranÄ± (Hz).</summary>
        double RateHz { get; }

        /// <summary>SaÄŸlÄ±k durumu (Ã¶rn. baÄŸlantÄ± var mÄ±?).</summary>
        bool IsHealthy { get; }

        /// <summary>Son gelen Ã¶rnek zaman damgasÄ±.</summary>
        DateTime? LastStamp { get; }

        /// <summary>
        /// Son Ã¶rneÄŸin tazeliÄŸi (ms).
        /// LastStamp yoksa null dÃ¶ner.
        /// FrameSource ve Diagnostik iÃ§in Ã¶nemlidir.
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
        /// SensÃ¶r kategorisi (IMU/GPS/LiDAR/Kamera vb.).
        /// Konsol ve UI tarafÄ±ndaki Capability listesinde kullanÄ±lÄ±r.
        /// </summary>
        string Type { get; }
    }
}

