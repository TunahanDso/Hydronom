// File: Hydronom.Core/Domain/ChannelProfile.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Bir aktÃ¼atÃ¶r/motor kanalÄ±na ait detaylÄ± fiziksel ve dinamik profil.
    /// AutoDiscoveryEngine tarafÄ±ndan otomatik oluÅŸturulur, ActuatorManager tarafÄ±ndan kontrol dÃ¶ngÃ¼sÃ¼nde kullanÄ±lÄ±r.
    /// 
    /// Notlar:
    /// - 3 eksenli (X, Y, Z) gÃ¶vde koordinat sistemine gÃ¶re tanÄ±mlÄ±dÄ±r.
    /// - Surface / Submarine / ROV senaryolarÄ±nda, Ã¶zellikle Z ekseni (heave thruster) iÃ§in ForceDir ve Position kritik Ã¶nemdedir.
    /// - ChannelProfileSet tipik olarak JSON dosyasÄ±na serileÅŸtirilerek kalÄ±cÄ± hale getirilebilir.
    /// </summary>
    public record ChannelProfile
    {
        // --- KÄ°MLÄ°K & TEMEL BÄ°LGÄ°LER ---

        /// <summary>
        /// PWM kanal numarasÄ± (DonanÄ±m portu).
        /// </summary>
        public int Channel { get; init; }

        /// <summary>
        /// Motor kimliÄŸi (Ã¶r. FL, Aft-Main, Fin-Left vb.).
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Motorun sisteme ters baÄŸlanÄ±p baÄŸlanmadÄ±ÄŸÄ±.
        /// (AutoDiscovery bunu algÄ±layÄ±p true yapabilir).
        /// </summary>
        public bool Reversed { get; init; } = false;

        /// <summary>
        /// Profilin oluÅŸturulma/gÃ¼ncellenme zamanÄ±.
        /// </summary>
        public DateTime LastCalibrationUtc { get; init; } = DateTime.UtcNow;


        // --- FÄ°ZÄ°KSEL KONUMLANDIRMA ---

        /// <summary>
        /// Fiziksel konum (Metre cinsinden, AraÃ§ GÃ¶vde Merkezi'ne gÃ¶re).
        /// X: Ä°leri (+) / Geri (-)
        /// Y: Sancak (+) / Ä°skele (-)
        /// Z: YukarÄ± (+) / AÅŸaÄŸÄ± (-)
        /// </summary>
        public Vec3 Position { get; init; } = Vec3.Zero;

        /// <summary>
        /// Ä°tki yÃ¶n vektÃ¶rÃ¼ (Normalize birim vektÃ¶r).
        /// </summary>
        public Vec3 ForceDir { get; init; } = new(1, 0, 0);

        /// <summary>
        /// Bu kanalÄ±n keÅŸif gÃ¼ven skoru (0.0 - 1.0).
        /// DÃ¼ÅŸÃ¼kse sistem bu motoru "Yedek" olarak kullanabilir.
        /// </summary>
        public double Confidence { get; init; } = 1.0;

        /// <summary>
        /// KanalÄ±n tipik rolÃ¼ (Ã¶r. "main", "bow", "stern", "heave", "lateral", "rudder-assist" vb.).
        /// Tamamen bilgilendirici, kontrol algoritmalarÄ± isterse kullanÄ±r.
        /// </summary>
        public string RoleTag { get; init; } = string.Empty;


        // --- DÄ°NAMÄ°K KARAKTERÄ°STÄ°K (Control Loop Ä°Ã§in Kritik) ---

        /// <summary>
        /// Ä°leri yÃ¶ndeki maksimum itki kuvveti [Newton].
        /// </summary>
        public double MaxThrustN { get; init; } = 10.0;

        /// <summary>
        /// Geri yÃ¶n verimlilik oranÄ± (0.0 - 1.0).
        /// Pervaneler genelde geri dÃ¶nÃ¼ÅŸte daha az itki Ã¼retir (Ã¶rn. 0.7).
        /// </summary>
        public double ReverseEfficiencyRatio { get; init; } = 0.75;

        /// <summary>
        /// Motorun harekete geÃ§mesi iÃ§in gereken minimum sinyal oranÄ± (0.0 - 1.0).
        /// (Stiction / Static Friction yenmek iÃ§in).
        /// </summary>
        public double DeadbandThreshold { get; init; } = 0.05;

        /// <summary>
        /// Motorun komuta tepki verme gecikmesi (Time Constant - Tau) [ms].
        /// PID kontrolcÃ¼sÃ¼ bu gecikmeyi bilirse salÄ±nÄ±mÄ± Ã¶nler.
        /// </summary>
        public double TimeConstantMs { get; init; } = 50.0;

        /// <summary>
        /// Ä°tki eÄŸrisi Ã¼ssÃ¼.
        /// 1.0 = Lineer (Ä°deal), 2.0 = Karesel (AkÄ±ÅŸkan dinamiÄŸi genelde bÃ¶yledir: Force ~ RPM^2).
        /// </summary>
        public double ResponsePowerFactor { get; init; } = 1.0;


        // --- Ä°STATÄ°STÄ°K & KALÄ°BRASYON ---

        /// <summary>
        /// KanalÄ±n PWM ofset hatasÄ± (Bias).
        /// </summary>
        public double OffsetBias { get; init; } = 0.0;

        /// <summary>
        /// KanalÄ±n yÃ¶n hatasÄ± (Radyan).
        /// </summary>
        public double DirectionErrorRad { get; init; } = 0.0;

        /// <summary>
        /// Ä°leriye dÃ¶nÃ¼k istatistiksel hata metrikleri.
        /// </summary>
        public ChannelErrorStats ErrorStats { get; init; } = new();


        // --- TÃœREV / KOLAYLAÅTIRICI Ã–ZELLÄ°KLER (hesaplanmÄ±ÅŸ) ---

        /// <summary>
        /// Geri yÃ¶nde ulaÅŸÄ±labilir tahmini maksimum itki [Newton].
        /// MaxThrustN * ReverseEfficiencyRatio olarak hesaplanÄ±r.
        /// </summary>
        [JsonIgnore]
        public double MaxReverseThrustN => MaxThrustN * ReverseEfficiencyRatio;

        /// <summary>
        /// Bu kanalÄ±n baskÄ±n olarak heave (Z ekseni) thruster'Ä± olup olmadÄ±ÄŸÄ±nÄ±
        /// kaba bir ÅŸekilde belirler.
        /// </summary>
        [JsonIgnore]
        public bool IsHeaveLike =>
            Math.Abs(ForceDir.Z) >= Math.Max(Math.Abs(ForceDir.X), Math.Abs(ForceDir.Y)) &&
            Math.Abs(ForceDir.Z) > 1e-3;

        /// <summary>
        /// Bu kanalÄ±n baskÄ±n olarak ileri/geri (X ekseni) thruster'Ä± olup olmadÄ±ÄŸÄ±nÄ± belirtir.
        /// </summary>
        [JsonIgnore]
        public bool IsSurgeLike =>
            Math.Abs(ForceDir.X) >= Math.Max(Math.Abs(ForceDir.Y), Math.Abs(ForceDir.Z)) &&
            Math.Abs(ForceDir.X) > 1e-3;

        /// <summary>
        /// Bu kanalÄ±n baskÄ±n olarak yanal (Y ekseni) thruster'Ä± olup olmadÄ±ÄŸÄ±nÄ± belirtir.
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
    /// Kanal bazÄ±nda Ã¶lÃ§Ã¼m istatistikleri.
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
    /// Profil koleksiyonu (VeritabanÄ± veya JSON dosyasÄ± karÅŸÄ±lÄ±ÄŸÄ±).
    /// Genellikle bir araÃ§ iÃ§in tÃ¼m kanallarÄ±n profilini taÅŸÄ±r.
    /// </summary>
    public record ChannelProfileSet
    {
        /// <summary>
        /// Bu set iÃ§indeki tÃ¼m kanal profilleri.
        /// </summary>
        public List<ChannelProfile> Profiles { get; init; } = new();

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Bu profil setini kim oluÅŸturdu? (Ã¶rn: "AutoDiscovery", "ManualOverride", "FactoryDefault")
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

