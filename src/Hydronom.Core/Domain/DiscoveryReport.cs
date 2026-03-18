// File: Hydronom.Core/Domain/DiscoveryReport.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Otomatik motor keşfi (AutoDiscovery) sonucu oluşturulan "Araç Kimlik Kartı".
    /// Bu rapor, aracın fiziksel karakteristiğini ve motor yerleşimini belgeler.
    /// </summary>
    public record DiscoveryReport
    {
        // --- BAŞLIK BİLGİLERİ ---

        /// <summary>
        /// Raporun benzersiz kimliği (UUID).
        /// </summary>
        public string ReportId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Raporun oluşturulduğu tarih (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Keşifin yapıldığı platform/cihaz adı (Hostname).
        /// </summary>
        public string PlatformId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Kullanılan yazılım sürümü.
        /// </summary>
        public string SoftwareVersion { get; init; } = "Hydronom.Discovery v2.1-Physics";

        /// <summary>
        /// Keşfi başlatan operatör.
        /// </summary>
        public string Operator { get; init; } = Environment.UserName;


        // --- SONUÇLAR ---

        /// <summary>
        /// Tespit edilen kanal profilleri (Motorların haritası).
        /// </summary>
        public List<ChannelProfile> Channels { get; init; } = new();

        /// <summary>
        /// Keşif süreci istatistikleri ve kalite metrikleri.
        /// </summary>
        public DiscoveryStats Stats { get; init; } = new();

        /// <summary>
        /// Aracın tespit edilen fiziksel imzası (Simetri, Atalet vb.).
        /// </summary>
        public VehicleSignature Signature { get; init; } = new();

        /// <summary>
        /// Kullanılan konfigürasyonun bir kopyası (Tekrarlanabilirlik için).
        /// </summary>
        public DiscoveryConfig Config { get; init; } = new();


        // --- HAM VERİ (Opsiyonel Debug İçin) ---

        /// <summary>
        /// Analiz için kaydedilen ham IMU verileri (Json'da şişkinlik yapmasın diye null olabilir).
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
    /// Keşif sürecinin teknik başarısını ölçen istatistikler.
    /// </summary>
    public record DiscoveryStats
    {
        public double MeanConfidence { get; init; } = 0.0;
        public int TotalSamples { get; init; } = 0;
        public int ChannelsScanned { get; init; } = 0;
        public double DurationSec { get; init; } = 0.0;
        public int ErrorCount { get; init; } = 0;

        /// <summary>
        /// Sistemin fiziksel hesaplama (Gyro/Tork) kullanma oranı.
        /// 1.0 = Tüm motorlar fiziksel olarak doğrulandı.
        /// 0.0 = Tamamen varsayımlara (Assumption) dayanıldı.
        /// </summary>
        public double PhysicsSolvabilityScore { get; init; } = 0.0;

        /// <summary>
        /// Ortamın gürültü seviyesi (IMU varyansı).
        /// </summary>
        public double NoiseLevel { get; init; } = 0.0;

        public override string ToString() =>
            $"Conf={MeanConfidence:F2}, PhysScore={PhysicsSolvabilityScore:F2}, Noise={NoiseLevel:F3}";
    }

    /// <summary>
    /// Aracın keşfedilen genel fiziksel karakteri.
    /// "Hydronom bu aracı nasıl algıladı?" sorusunun cevabı.
    /// </summary>
    public record VehicleSignature
    {
        /// <summary>
        /// Aracın tipi tahmini (Surface, Submersible, Ground).
        /// Z ekseni hareketine ve sönümlenme süresine göre tahmin edilir.
        /// </summary>
        public string EstimatedVehicleType { get; init; } = "Unknown";

        /// <summary>
        /// İtki simetrisi skoru (0–1).
        /// Motorlar merkeze göre dengeli mi dağılmış?
        /// </summary>
        public double SymmetryScore { get; init; } = 1.0;

        /// <summary>
        /// Dönüş tepkisi (Yaw Authority).
        /// Araç ne kadar çevik dönüyor? (Yüksek değer = Çevik).
        /// </summary>
        public double RotationalAgility { get; init; } = 0.0;

        /// <summary>
        /// Gözlemlenen sönümlenme katsayısı (Ortamın su mu hava mı olduğunu anlamaya yarar).
        /// </summary>
        public double DampingFactorObserved { get; init; } = 0.0;
    }
}
