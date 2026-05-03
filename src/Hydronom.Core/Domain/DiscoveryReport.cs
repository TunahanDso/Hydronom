// File: Hydronom.Core/Domain/DiscoveryReport.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Otomatik motor keÅŸfi (AutoDiscovery) sonucu oluÅŸturulan "AraÃ§ Kimlik KartÄ±".
    /// Bu rapor, aracÄ±n fiziksel karakteristiÄŸini ve motor yerleÅŸimini belgeler.
    /// </summary>
    public record DiscoveryReport
    {
        // --- BAÅLIK BÄ°LGÄ°LERÄ° ---

        /// <summary>
        /// Raporun benzersiz kimliÄŸi (UUID).
        /// </summary>
        public string ReportId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Raporun oluÅŸturulduÄŸu tarih (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// KeÅŸifin yapÄ±ldÄ±ÄŸÄ± platform/cihaz adÄ± (Hostname).
        /// </summary>
        public string PlatformId { get; init; } = Environment.MachineName;

        /// <summary>
        /// KullanÄ±lan yazÄ±lÄ±m sÃ¼rÃ¼mÃ¼.
        /// </summary>
        public string SoftwareVersion { get; init; } = "Hydronom.Discovery v2.1-Physics";

        /// <summary>
        /// KeÅŸfi baÅŸlatan operatÃ¶r.
        /// </summary>
        public string Operator { get; init; } = Environment.UserName;


        // --- SONUÃ‡LAR ---

        /// <summary>
        /// Tespit edilen kanal profilleri (MotorlarÄ±n haritasÄ±).
        /// </summary>
        public List<ChannelProfile> Channels { get; init; } = new();

        /// <summary>
        /// KeÅŸif sÃ¼reci istatistikleri ve kalite metrikleri.
        /// </summary>
        public DiscoveryStats Stats { get; init; } = new();

        /// <summary>
        /// AracÄ±n tespit edilen fiziksel imzasÄ± (Simetri, Atalet vb.).
        /// </summary>
        public VehicleSignature Signature { get; init; } = new();

        /// <summary>
        /// KullanÄ±lan konfigÃ¼rasyonun bir kopyasÄ± (Tekrarlanabilirlik iÃ§in).
        /// </summary>
        public DiscoveryConfig Config { get; init; } = new();


        // --- HAM VERÄ° (Opsiyonel Debug Ä°Ã§in) ---

        /// <summary>
        /// Analiz iÃ§in kaydedilen ham IMU verileri (Json'da ÅŸiÅŸkinlik yapmasÄ±n diye null olabilir).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ImuSample>? SampleLog { get; init; }

        public record ImuSample(DateTime TimestampUtc, Vec3 Accel, Vec3 Gyro);


        public override string ToString()
        {
            return $"[Report {ReportId.Substring(0, 6)}] {Channels.Count} thrusters found. Quality={Stats.PhysicsSolvabilityScore:P0}";
        }
    }

    /// <summary>
    /// KeÅŸif sÃ¼recinin teknik baÅŸarÄ±sÄ±nÄ± Ã¶lÃ§en istatistikler.
    /// </summary>
    public record DiscoveryStats
    {
        public double MeanConfidence { get; init; } = 0.0;
        public int TotalSamples { get; init; } = 0;
        public int ChannelsScanned { get; init; } = 0;
        public double DurationSec { get; init; } = 0.0;
        public int ErrorCount { get; init; } = 0;

        /// <summary>
        /// Sistemin fiziksel hesaplama (Gyro/Tork) kullanma oranÄ±.
        /// 1.0 = TÃ¼m motorlar fiziksel olarak doÄŸrulandÄ±.
        /// 0.0 = Tamamen varsayÄ±mlara (Assumption) dayanÄ±ldÄ±.
        /// </summary>
        public double PhysicsSolvabilityScore { get; init; } = 0.0;

        /// <summary>
        /// OrtamÄ±n gÃ¼rÃ¼ltÃ¼ seviyesi (IMU varyansÄ±).
        /// </summary>
        public double NoiseLevel { get; init; } = 0.0;

        public override string ToString() =>
            $"Conf={MeanConfidence:F2}, PhysScore={PhysicsSolvabilityScore:F2}, Noise={NoiseLevel:F3}";
    }

    /// <summary>
    /// AracÄ±n keÅŸfedilen genel fiziksel karakteri.
    /// "Hydronom bu aracÄ± nasÄ±l algÄ±ladÄ±?" sorusunun cevabÄ±.
    /// </summary>
    public record VehicleSignature
    {
        /// <summary>
        /// AracÄ±n tipi tahmini (Surface, Submersible, Ground).
        /// Z ekseni hareketine ve sÃ¶nÃ¼mlenme sÃ¼resine gÃ¶re tahmin edilir.
        /// </summary>
        public string EstimatedVehicleType { get; init; } = "Unknown";

        /// <summary>
        /// Ä°tki simetrisi skoru (0â€“1).
        /// Motorlar merkeze gÃ¶re dengeli mi daÄŸÄ±lmÄ±ÅŸ?
        /// </summary>
        public double SymmetryScore { get; init; } = 1.0;

        /// <summary>
        /// DÃ¶nÃ¼ÅŸ tepkisi (Yaw Authority).
        /// AraÃ§ ne kadar Ã§evik dÃ¶nÃ¼yor? (YÃ¼ksek deÄŸer = Ã‡evik).
        /// </summary>
        public double RotationalAgility { get; init; } = 0.0;

        /// <summary>
        /// GÃ¶zlemlenen sÃ¶nÃ¼mlenme katsayÄ±sÄ± (OrtamÄ±n su mu hava mÄ± olduÄŸunu anlamaya yarar).
        /// </summary>
        public double DampingFactorObserved { get; init; } = 0.0;
    }
}

