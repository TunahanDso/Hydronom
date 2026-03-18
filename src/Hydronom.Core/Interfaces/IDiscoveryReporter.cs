using Hydronom.Core.Domain;
using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Otomatik motor/araç keşfi (AutoDiscovery) sırasında üretilen
    /// DiscoveryReport'ların kaydedilmesi, yayınlanması veya işlenmesi için
    /// soyut arayüz.
    ///
    /// Uygulamalar:
    ///   - CLI: JSON dosyasına yazabilir.
    ///   - Runtime: Son keşfi bellekte saklayabilir.
    ///   - GUI: Kullanıcıya canlı olarak güncellenen rapor gösterebilir.
    ///   - Cloud: Raporu REST API'ye gönderebilir.
    ///
    /// Temel görev:
    ///   DiscoveryReport üretildiğinde bunu ilgili servislere iletmek.
    /// </summary>
    public interface IDiscoveryReporter
    {
        /// <summary>
        /// Tamamlanmış bir DiscoveryReport raporunu işler.
        /// Bu işleme dosyaya yazmak, GUI'ye iletmek,
        /// telemetri sunucusuna göndermek veya loglamak dahil olabilir.
        /// </summary>
        void Submit(DiscoveryReport report);

        /// <summary>
        /// Rapor üretimi sırasında ara durum/ilerleme mesajları için isteğe bağlı hook.
        /// Örn: "Scanning channel 3", "IMU variance high", "Solvability 87%" gibi.
        /// GUI/CLI canlı ilerleme göstergelerinde kullanılır.
        /// </summary>
        void Progress(string message);

        /// <summary>
        /// Keşif sürecinde kritik bir hata oluştuğunda bildirim.
        /// Operatörün ekrana/uygulamaya hata mesajı düşebilmesi için.
        /// </summary>
        void Error(string message, Exception? ex = null);
    }
}
