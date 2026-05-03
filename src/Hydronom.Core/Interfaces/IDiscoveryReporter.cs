using Hydronom.Core.Domain;
using System;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Otomatik motor/araÃ§ keÅŸfi (AutoDiscovery) sÄ±rasÄ±nda Ã¼retilen
    /// DiscoveryReport'larÄ±n kaydedilmesi, yayÄ±nlanmasÄ± veya iÅŸlenmesi iÃ§in
    /// soyut arayÃ¼z.
    ///
    /// Uygulamalar:
    ///   - CLI: JSON dosyasÄ±na yazabilir.
    ///   - Runtime: Son keÅŸfi bellekte saklayabilir.
    ///   - GUI: KullanÄ±cÄ±ya canlÄ± olarak gÃ¼ncellenen rapor gÃ¶sterebilir.
    ///   - Cloud: Raporu REST API'ye gÃ¶nderebilir.
    ///
    /// Temel gÃ¶rev:
    ///   DiscoveryReport Ã¼retildiÄŸinde bunu ilgili servislere iletmek.
    /// </summary>
    public interface IDiscoveryReporter
    {
        /// <summary>
        /// TamamlanmÄ±ÅŸ bir DiscoveryReport raporunu iÅŸler.
        /// Bu iÅŸleme dosyaya yazmak, GUI'ye iletmek,
        /// telemetri sunucusuna gÃ¶ndermek veya loglamak dahil olabilir.
        /// </summary>
        void Submit(DiscoveryReport report);

        /// <summary>
        /// Rapor Ã¼retimi sÄ±rasÄ±nda ara durum/ilerleme mesajlarÄ± iÃ§in isteÄŸe baÄŸlÄ± hook.
        /// Ã–rn: "Scanning channel 3", "IMU variance high", "Solvability 87%" gibi.
        /// GUI/CLI canlÄ± ilerleme gÃ¶stergelerinde kullanÄ±lÄ±r.
        /// </summary>
        void Progress(string message);

        /// <summary>
        /// KeÅŸif sÃ¼recinde kritik bir hata oluÅŸtuÄŸunda bildirim.
        /// OperatÃ¶rÃ¼n ekrana/uygulamaya hata mesajÄ± dÃ¼ÅŸebilmesi iÃ§in.
        /// </summary>
        void Error(string message, Exception? ex = null);
    }
}

