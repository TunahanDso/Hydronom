// File: Hydronom.Core/Domain/ChannelProfile.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Bir aktüatör/motor kanalına ait detaylı fiziksel ve dinamik profil.
    /// AutoDiscoveryEngine tarafından otomatik oluşturulur, ActuatorManager tarafından kontrol döngüsünde kullanılır.
    /// 
    /// Notlar:
    /// - 3 eksenli (X, Y, Z) gövde koordinat sistemine göre tanımlıdır.
    /// - Surface / Submarine / ROV senaryolarında, özellikle Z ekseni (heave thruster) için ForceDir ve Position kritik önemdedir.
    /// - ChannelProfileSet tipik olarak JSON dosyasına serileştirilerek kalıcı hale getirilebilir.
    /// </summary>
    public record ChannelProfile
    {
        // --- KİMLİK & TEMEL BİLGİLER ---

        /// <summary>
        /// PWM kanal numarası (Donanım portu).
        /// </summary>
        public int Channel { get; init; }

        /// <summary>
        /// Motor kimliği (ör. FL, Aft-Main, Fin-Left vb.).
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Motorun sisteme ters bağlanıp bağlanmadığı.
        /// (AutoDiscovery bunu algılayıp true yapabilir).
        /// </summary>
        public bool Reversed { get; init; } = false;

        /// <summary>
        /// Profilin oluşturulma/güncellenme zamanı.
        /// </summary>
        public DateTime LastCalibrationUtc { get; init; } = DateTime.UtcNow;


        // --- FİZİKSEL KONUMLANDIRMA ---

        /// <summary>
        /// Fiziksel konum (Metre cinsinden, Araç Gövde Merkezi'ne göre).
        /// X: İleri (+) / Geri (-)
        /// Y: Sancak (+) / İskele (-)
        /// Z: Yukarı (+) / Aşağı (-)
        /// </summary>
        public Vec3 Position { get; init; } = Vec3.Zero;

        /// <summary>
        /// İtki yön vektörü (Normalize birim vektör).
        /// </summary>
        public Vec3 ForceDir { get; init; } = new(1, 0, 0);

        /// <summary>
        /// Bu kanalın keşif güven skoru (0.0 - 1.0).
        /// Düşükse sistem bu motoru "Yedek" olarak kullanabilir.
        /// </summary>
        public double Confidence { get; init; } = 1.0;

        /// <summary>
        /// Kanalın tipik rolü (ör. "main", "bow", "stern", "heave", "lateral", "rudder-assist" vb.).
        /// Tamamen bilgilendirici, kontrol algoritmaları isterse kullanır.
        /// </summary>
        public string RoleTag { get; init; } = string.Empty;


        // --- DİNAMİK KARAKTERİSTİK (Control Loop İçin Kritik) ---

        /// <summary>
        /// İleri yöndeki maksimum itki kuvveti [Newton].
        /// </summary>
        public double MaxThrustN { get; init; } = 10.0;

        /// <summary>
        /// Geri yön verimlilik oranı (0.0 - 1.0).
        /// Pervaneler genelde geri dönüşte daha az itki üretir (örn. 0.7).
        /// </summary>
        public double ReverseEfficiencyRatio { get; init; } = 0.75;

        /// <summary>
        /// Motorun harekete geçmesi için gereken minimum sinyal oranı (0.0 - 1.0).
        /// (Stiction / Static Friction yenmek için).
        /// </summary>
        public double DeadbandThreshold { get; init; } = 0.05;

        /// <summary>
        /// Motorun komuta tepki verme gecikmesi (Time Constant - Tau) [ms].
        /// PID kontrolcüsü bu gecikmeyi bilirse salınımı önler.
        /// </summary>
        public double TimeConstantMs { get; init; } = 50.0;

        /// <summary>
        /// İtki eğrisi üssü.
        /// 1.0 = Lineer (İdeal), 2.0 = Karesel (Akışkan dinamiği genelde böyledir: Force ~ RPM^2).
        /// </summary>
        public double ResponsePowerFactor { get; init; } = 1.0;


        // --- İSTATİSTİK & KALİBRASYON ---

        /// <summary>
        /// Kanalın PWM ofset hatası (Bias).
        /// </summary>
        public double OffsetBias { get; init; } = 0.0;

        /// <summary>
        /// Kanalın yön hatası (Radyan).
        /// </summary>
        public double DirectionErrorRad { get; init; } = 0.0;

        /// <summary>
        /// İleriye dönük istatistiksel hata metrikleri.
        /// </summary>
        public ChannelErrorStats ErrorStats { get; init; } = new();


        // --- TÜREV / KOLAYLAŞTIRICI ÖZELLİKLER (hesaplanmış) ---

        /// <summary>
        /// Geri yönde ulaşılabilir tahmini maksimum itki [Newton].
        /// MaxThrustN * ReverseEfficiencyRatio olarak hesaplanır.
        /// </summary>
        [JsonIgnore]
        public double MaxReverseThrustN => MaxThrustN * ReverseEfficiencyRatio;

        /// <summary>
        /// Bu kanalın baskın olarak heave (Z ekseni) thruster'ı olup olmadığını
        /// kaba bir şekilde belirler.
        /// </summary>
        [JsonIgnore]
        public bool IsHeaveLike =>
            Math.Abs(ForceDir.Z) >= Math.Max(Math.Abs(ForceDir.X), Math.Abs(ForceDir.Y)) &&
            Math.Abs(ForceDir.Z) > 1e-3;

        /// <summary>
        /// Bu kanalın baskın olarak ileri/geri (X ekseni) thruster'ı olup olmadığını belirtir.
        /// </summary>
        [JsonIgnore]
        public bool IsSurgeLike =>
            Math.Abs(ForceDir.X) >= Math.Max(Math.Abs(ForceDir.Y), Math.Abs(ForceDir.Z)) &&
            Math.Abs(ForceDir.X) > 1e-3;

        /// <summary>
        /// Bu kanalın baskın olarak yanal (Y ekseni) thruster'ı olup olmadığını belirtir.
        /// </summary>
        [JsonIgnore]
        public bool IsSwayLike =>
            Math.Abs(ForceDir.Y) >= Math.Max(Math.Abs(ForceDir.X), Math.Abs(ForceDir.Z)) &&
            Math.Abs(ForceDir.Y) > 1e-3;


        public override string ToString()
        {
            return $"CH{Channel:D2} [{Id}] Pos={Fmt(Position)} Dir={Fmt(ForceDir)} MaxN={MaxThrustN:F1} RevEff={ReverseEfficiencyRatio:F2} Conf={Confidence:F2}";
        }

        private static string Fmt(Vec3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";
    }

    /// <summary>
    /// Kanal bazında ölçüm istatistikleri.
    /// </summary>
    public record ChannelErrorStats
    {
        [JsonPropertyName("err_mean")]
        public Vec3 MeanError { get; init; } = Vec3.Zero;

        [JsonPropertyName("err_std")]
        public Vec3 StdDeviation { get; init; } = Vec3.Zero;

        [JsonPropertyName("sample_count")]
        public int SampleCount { get; init; } = 0;

        public override string ToString()
        {
            return $"n={SampleCount} mean={Fmt(MeanError)} std={Fmt(StdDeviation)}";
        }

        private static string Fmt(Vec3 v) => $"({v.X:F3},{v.Y:F3},{v.Z:F3})";
    }

    /// <summary>
    /// Profil koleksiyonu (Veritabanı veya JSON dosyası karşılığı).
    /// Genellikle bir araç için tüm kanalların profilini taşır.
    /// </summary>
    public record ChannelProfileSet
    {
        /// <summary>
        /// Bu set içindeki tüm kanal profilleri.
        /// </summary>
        public List<ChannelProfile> Profiles { get; init; } = new();

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Bu profil setini kim oluşturdu? (örn: "AutoDiscovery", "ManualOverride", "FactoryDefault")
        /// </summary>
        public string SourceTag { get; init; } = "autodiscovery";

        public ChannelProfile? FindByChannel(int ch) =>
            Profiles.Find(p => p.Channel == ch);

        public void AddOrUpdate(ChannelProfile p)
        {
            var existing = FindByChannel(p.Channel);
            if (existing is not null)
                Profiles.Remove(existing);
            Profiles.Add(p);
        }

        public override string ToString() =>
            $"ChannelProfileSet: {Profiles.Count} profiles [{SourceTag}], {CreatedUtc:u}";
    }
}
