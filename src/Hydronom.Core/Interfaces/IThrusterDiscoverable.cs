using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Motor keÅŸfi (auto-discovery) algoritmalarÄ± tarafÄ±ndan uygulanmasÄ± gereken arayÃ¼z.
    /// Thruster'larÄ±n (itki Ã¼nitelerinin) kanal, yÃ¶n, konum ve etki parametrelerinin otomatik belirlenmesini saÄŸlar.
    /// </summary>
    public interface IThrusterDiscoverable
    {
        /// <summary>
        /// KeÅŸfedilecek motor sayÄ±sÄ± (Ã¶rneÄŸin 4, 6, 8...).
        /// </summary>
        int ThrusterCount { get; }

        /// <summary>
        /// Motorlara komut gÃ¶ndermek iÃ§in eriÅŸilen dÃ¼ÅŸÃ¼k seviye sÃ¼rÃ¼cÃ¼.
        /// Ã–rneÄŸin PCA9685, ESC sÃ¼rÃ¼cÃ¼sÃ¼, veya simÃ¼lasyon arayÃ¼zÃ¼.
        /// </summary>
        IMotorController MotorController { get; }

        /// <summary>
        /// SensÃ¶r verilerini saÄŸlayan IMU veya sensÃ¶r fÃ¼zyon kaynaÄŸÄ±.
        /// </summary>
        IInertialSensor InertialSensor { get; }

        /// <summary>
        /// Her motoru sÄ±rayla veya belirli bir algoritmaya gÃ¶re test eder.
        /// Testler sÄ±rasÄ±nda IMU verisinden ivme ve yÃ¶n deÄŸiÅŸimi Ã¶lÃ§Ã¼lÃ¼r.
        /// </summary>
        /// <returns>KeÅŸfedilen motor profilleri listesi.</returns>
        Task<IReadOnlyList<ThrusterProfile>> DiscoverAsync(CancellationToken token = default);

        /// <summary>
        /// KeÅŸif Ã¶ncesi sistem hazÄ±rlÄ±ÄŸÄ±nÄ± yapar (Ã¶rneÄŸin sÄ±fÄ±rlama, kalibrasyon, gÃ¼venlik kontrolleri).
        /// </summary>
        void Prepare();

        /// <summary>
        /// KeÅŸif sÃ¼reci tamamlandÄ±ktan sonra kaydedilecek raporu oluÅŸturur.
        /// Not: Bu Ã¶zet rapor, Domain tarafÄ±ndaki "DiscoveryReport" (AraÃ§ Kimlik KartÄ±) ile karÄ±ÅŸmamasÄ± iÃ§in
        /// "ThrusterDiscoveryReport" tipini kullanÄ±r.
        /// </summary>
        /// <param name="profiles">KeÅŸfedilen thruster profilleri</param>
        /// <returns>Ã–zet rapor nesnesi</returns>
        ThrusterDiscoveryReport GenerateReport(IReadOnlyList<ThrusterProfile> profiles);
    }

    /// <summary>
    /// KeÅŸif sonrasÄ± oluÅŸturulan motor profili.
    /// Her motorun fiziksel konumu, yÃ¶nÃ¼, maksimum itki oranÄ± ve PWM kanalÄ± iÃ§erir.
    /// </summary>
    public readonly record struct ThrusterProfile(
        string Id,
        int Channel,
        Vec3 Position,
        Vec3 ForceDirection,
        double MaxThrustN,
        double Confidence,
        DateTime TimestampUtc
    )
    {
        public static ThrusterProfile Default =>
            new("?", -1, Vec3.Zero, new Vec3(1, 0, 0), 0, 0, DateTime.UtcNow);
    }

    /// <summary>
    /// Motor keÅŸfi iÃ§in Ã¶zet rapor (AutoDiscoveryEngine â†’ dÄ±ÅŸ dÃ¼nya).
    /// Domain tarafÄ±ndaki "Hydronom.Core.Domain.DiscoveryReport" ile karÄ±ÅŸmamasÄ± iÃ§in
    /// isim netleÅŸtirildi.
    /// </summary>
    public readonly record struct ThrusterDiscoveryReport(
        string DeviceName,
        int ThrusterCount,
        DateTime StartTime,
        DateTime EndTime,
        IReadOnlyList<ThrusterProfile> Profiles,
        string Notes
    )
    {
        public static ThrusterDiscoveryReport Empty =>
            new("Unknown", 0, DateTime.UtcNow, DateTime.UtcNow,
                Array.Empty<ThrusterProfile>(), "No data");
    }
}

