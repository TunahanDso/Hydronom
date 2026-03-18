using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Motor keşfi (auto-discovery) algoritmaları tarafından uygulanması gereken arayüz.
    /// Thruster'ların (itki ünitelerinin) kanal, yön, konum ve etki parametrelerinin otomatik belirlenmesini sağlar.
    /// </summary>
    public interface IThrusterDiscoverable
    {
        /// <summary>
        /// Keşfedilecek motor sayısı (örneğin 4, 6, 8...).
        /// </summary>
        int ThrusterCount { get; }

        /// <summary>
        /// Motorlara komut göndermek için erişilen düşük seviye sürücü.
        /// Örneğin PCA9685, ESC sürücüsü, veya simülasyon arayüzü.
        /// </summary>
        IMotorController MotorController { get; }

        /// <summary>
        /// Sensör verilerini sağlayan IMU veya sensör füzyon kaynağı.
        /// </summary>
        IInertialSensor InertialSensor { get; }

        /// <summary>
        /// Her motoru sırayla veya belirli bir algoritmaya göre test eder.
        /// Testler sırasında IMU verisinden ivme ve yön değişimi ölçülür.
        /// </summary>
        /// <returns>Keşfedilen motor profilleri listesi.</returns>
        Task<IReadOnlyList<ThrusterProfile>> DiscoverAsync(CancellationToken token = default);

        /// <summary>
        /// Keşif öncesi sistem hazırlığını yapar (örneğin sıfırlama, kalibrasyon, güvenlik kontrolleri).
        /// </summary>
        void Prepare();

        /// <summary>
        /// Keşif süreci tamamlandıktan sonra kaydedilecek raporu oluşturur.
        /// Not: Bu özet rapor, Domain tarafındaki "DiscoveryReport" (Araç Kimlik Kartı) ile karışmaması için
        /// "ThrusterDiscoveryReport" tipini kullanır.
        /// </summary>
        /// <param name="profiles">Keşfedilen thruster profilleri</param>
        /// <returns>Özet rapor nesnesi</returns>
        ThrusterDiscoveryReport GenerateReport(IReadOnlyList<ThrusterProfile> profiles);
    }

    /// <summary>
    /// Keşif sonrası oluşturulan motor profili.
    /// Her motorun fiziksel konumu, yönü, maksimum itki oranı ve PWM kanalı içerir.
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
    /// Motor keşfi için özet rapor (AutoDiscoveryEngine → dış dünya).
    /// Domain tarafındaki "Hydronom.Core.Domain.DiscoveryReport" ile karışmaması için
    /// isim netleştirildi.
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
