using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// PWM–IMU tabanlı motor keşif sürecinin konfigürasyon parametreleri.
    /// AutoDiscoveryEngine tarafından referans alınır.
    /// </summary>
    public record DiscoveryConfig
    {
        // --- ZAMANLAMA ---

        /// <summary>
        /// Bir test darbesinden sonra suyun durulması için beklenecek süre [ms].
        /// Su altı araçlarında atalet yüksektir, bu süre ChannelDelay'den uzun olmalıdır.
        /// </summary>
        public int SettlingTimeMs { get; init; } = 2000;

        /// <summary>
        /// Her kanal için uygulanacak PWM "ON" süresi [ms].
        /// </summary>
        public int PulseDurationMs { get; init; } = 800;

        /// <summary>
        /// PWM kanalları arası minimum geçiş beklemesi [ms].
        /// </summary>
        public int ChannelDelayMs { get; init; } = 500;

        /// <summary>
        /// IMU örnekleme aralığı [ms].
        /// </summary>
        public int ImuSampleIntervalMs { get; init; } = 10;


        // --- EŞİK DEĞERLERİ (Hassasiyet) ---

        /// <summary>
        /// Eşik lineer hızlanma değeri [m/s²]. Gürültü filtresi.
        /// </summary>
        public double AccelThreshold { get; init; } = 0.02;

        /// <summary>
        /// Eşik açısal hızlanma değeri [rad/s].
        /// Tork çıkarımı için bu değerin aşılması gerekir.
        /// </summary>
        public double GyroThreshold { get; init; } = 0.05;

        /// <summary>
        /// Bir ölçümün geçerli sayılması için gereken minimum güven skoru (0.0–1.0).
        /// </summary>
        public double MinConfidenceScore { get; init; } = 0.3;


        // --- TEST PROSEDÜRÜ ---

        /// <summary>
        /// PWM taraması sırasında kullanılacak minimum sinyal oranı (0.0–1.0).
        /// </summary>
        public double MinThrottle { get; init; } = 0.25;

        /// <summary>
        /// PWM taraması sırasında kullanılacak maksimum sinyal oranı (0.0–1.0).
        /// </summary>
        public double MaxThrottle { get; init; } = 0.50;

        /// <summary>
        /// Motorların ters yönde de test edilip edilmeyeceği.
        /// (ReverseEfficiencyRatio için gereklidir.)
        /// </summary>
        public bool TestReverseDirection { get; init; } = true;

        /// <summary>
        /// Her kanalın kaç kez tekrar test edileceği.
        /// </summary>
        public int Repeats { get; init; } = 3;


        // --- VARSAYIMLAR (Fallback) ---

        /// <summary>
        /// Fiziksel çıkarım yapılamazsa varsayılan motor sayısı.
        /// </summary>
        public int AssumedMotorCount { get; init; } = 4;

        /// <summary>
        /// Fiziksel çıkarım yapılamazsa varsayılan motor yarıçapı (metre).
        /// </summary>
        public double AssumedRadiusM { get; init; } = 0.5;


        // --- SİSTEM ---

        /// <summary>
        /// Taranacak toplam PWM kanalı (PCA9685 = 16).
        /// </summary>
        public int TotalChannels { get; init; } = 16;

        /// <summary>
        /// Sadece belirli kanalları taramak için whitelist. Boş ise tüm kanallar taranır.
        /// </summary>
        public List<int> ChannelWhitelist { get; init; } = new();

        /// <summary>
        /// Simülasyon modunun açık olup olmadığı.
        /// </summary>
        public bool SimulationMode { get; init; } = false;

        /// <summary>
        /// Keşif raporlarının yazılacağı dizin.
        /// </summary>
        public string OutputDirectory { get; init; } = "Configs/AutoDiscovery";


        public override string ToString()
        {
            return $"Scan={TotalChannels}ch, Pulse={PulseDurationMs}ms, Settle={SettlingTimeMs}ms, " +
                   $"Thresh(Acc/Gyr)={AccelThreshold:F3}/{GyroThreshold:F3}, Rev={TestReverseDirection}";
        }
    }
}
