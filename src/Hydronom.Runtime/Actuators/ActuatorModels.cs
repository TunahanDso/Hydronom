using System;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// Thruster geometri tanımı.
    ///
    /// Position ve ForceDir body frame'dedir.
    /// Bu tanım platform bağımsızdır; tekne, denizaltı, drone, kara robotu veya
    /// fabrika içi mobil platform aynı geometri sözleşmesini kullanabilir.
    /// </summary>
    public readonly record struct ThrusterDesc(
        string Id,
        int Channel,
        Vec3 Position,
        Vec3 ForceDir,
        bool Reversed = false
    );

    /// <summary>
    /// Gerçek zamanlı thruster/actuator nesnesi.
    ///
    /// Bu sınıf fiziksel yerleşimi, komutlanan çıkışı, feedback değerlerini
    /// ve sağlık durumunu tek yerde taşır.
    /// </summary>
    public sealed class Thruster
    {
        public string Id { get; }
        public int Channel { get; }
        public Vec3 Position { get; }
        public Vec3 ForceDir { get; }
        public bool Reversed { get; }

        /// <summary>
        /// Normalize çıkış komutu.
        /// Aralık: [-1, +1]
        /// </summary>
        public double Current { get; set; }

        /// <summary>
        /// Donanımdan gelen akım geri bildirimi.
        /// Birim: mA
        /// </summary>
        public int CurrentSenseMilliAmp { get; set; }

        /// <summary>
        /// Donanımdan gelen RPM geri bildirimi.
        /// </summary>
        public int RpmFeedback { get; set; }

        public ThrusterHealthFlags HealthFlags { get; set; }

        public bool IsHealthy { get; set; } = true;

        public DateTime LastCommandUtc { get; set; }

        public DateTime LastFeedbackUtc { get; set; }

        public Thruster(ThrusterDesc d)
        {
            Id = string.IsNullOrWhiteSpace(d.Id)
                ? $"THRUSTER_CH{d.Channel}"
                : d.Id;

            Channel = d.Channel;
            Position = d.Position;

            var dir = d.ForceDir;

            if (d.Reversed)
                dir *= -1.0;

            ForceDir = dir.Normalize();
            Reversed = d.Reversed;
        }
    }

    [Flags]
    public enum ThrusterHealthFlags
    {
        None = 0,

        /// <summary>
        /// Telemetry belirlenen süre içinde güncellenmedi.
        /// </summary>
        TelemetryStale = 1 << 0,

        /// <summary>
        /// Komut verilmesine rağmen yüksek akım + düşük RPM görüldü.
        /// Sıkışma veya mekanik engel şüphesi.
        /// </summary>
        JamSuspected = 1 << 1,

        /// <summary>
        /// Alt seviye kontrolcü uyarı bayrağı gönderdi.
        /// </summary>
        ControllerWarning = 1 << 2
    }

    /// <summary>
    /// Bir eksenin pozitif ve negatif yöndeki teorik otoritesi.
    /// Örnek: Fx ileri/geri, Tz sağ/sol dönme momenti.
    /// </summary>
    public readonly record struct AxisAuthority(double Positive, double Negative)
    {
        public bool HasPositive => Positive > 1e-6;
        public bool HasNegative => Negative > 1e-6;

        /// <summary>
        /// Pozitif + negatif toplam kapasite.
        /// </summary>
        public double Span => Positive + Negative;

        /// <summary>
        /// Eksenin iki yöne de etki edip edemediğini gösterir.
        /// </summary>
        public bool IsBidirectional => HasPositive && HasNegative;

        public override string ToString() => $"(+{Positive:F2}/-{Negative:F2})";
    }

    /// <summary>
    /// Araç üstündeki actuator diziliminin hangi eksenlerde otoriteye sahip olduğunu gösterir.
    ///
    /// Bu profil Decision, Safety, Analysis, Mission Compatibility ve Hydronom Ops
    /// tarafında kullanılabilir.
    /// </summary>
    public readonly record struct ControlAuthorityProfile(
        AxisAuthority Fx,
        AxisAuthority Fy,
        AxisAuthority Fz,
        AxisAuthority Tx,
        AxisAuthority Ty,
        AxisAuthority Tz)
    {
        public bool CanSurge => Fx.Span > 1e-6;
        public bool CanSway => Fy.Span > 1e-6;
        public bool CanHeave => Fz.Span > 1e-6;
        public bool CanRoll => Tx.Span > 1e-6;
        public bool CanPitch => Ty.Span > 1e-6;
        public bool CanYaw => Tz.Span > 1e-6;

        public static ControlAuthorityProfile Empty { get; } =
            new(
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0)
            );

        public override string ToString()
            => $"Fx{Fx} Fy{Fy} Fz{Fz} Tx{Tx} Ty{Ty} Tz{Tz}";
    }

    /// <summary>
    /// Wrench allocation sonucunun açıklanabilir raporu.
    ///
    /// Bu rapor şunu cevaplar:
    /// - Decision hangi kuvvet/torku istedi?
    /// - Thruster dizilimi gerçekte ne üretebildi?
    /// - Hata ne kadar?
    /// - Saturation oldu mu?
    /// - Sağlıksız thruster var mı?
    /// - Hareket fiziksel/aktüasyonel olarak sınırlı mı?
    /// </summary>
    public readonly record struct ActuatorAllocationReport(
        bool Success,
        string Reason,
        Vec3 RequestedForceBody,
        Vec3 RequestedTorqueBody,
        Vec3 AchievedForceBody,
        Vec3 AchievedTorqueBody,
        Vec3 ForceErrorBody,
        Vec3 TorqueErrorBody,
        double NormalizedError,
        double SaturationRatio,
        int ActiveThrusterCount,
        int HealthyThrusterCount,
        bool HadSaturation,
        bool HadUnhealthyThruster,
        bool AuthorityLimited
    )
    {
        public static ActuatorAllocationReport Empty { get; } =
            new(
                Success: false,
                Reason: "NOT_COMPUTED",
                RequestedForceBody: Vec3.Zero,
                RequestedTorqueBody: Vec3.Zero,
                AchievedForceBody: Vec3.Zero,
                AchievedTorqueBody: Vec3.Zero,
                ForceErrorBody: Vec3.Zero,
                TorqueErrorBody: Vec3.Zero,
                NormalizedError: 0.0,
                SaturationRatio: 0.0,
                ActiveThrusterCount: 0,
                HealthyThrusterCount: 0,
                HadSaturation: false,
                HadUnhealthyThruster: false,
                AuthorityLimited: false
            );

        public bool IsGood => Success && !AuthorityLimited && NormalizedError <= 0.25;

        public bool IsPoor => !Success || NormalizedError > 0.50 || AuthorityLimited;

        public override string ToString()
        {
            return
                $"{Reason} " +
                $"err={NormalizedError:F3} " +
                $"sat={SaturationRatio:F2} " +
                $"active={ActiveThrusterCount} " +
                $"healthy={HealthyThrusterCount} " +
                $"limited={AuthorityLimited}";
        }
    }

    /// <summary>
    /// Solver cache.
    ///
    /// B:
    /// 6xM control effectiveness matrix.
    ///
    /// Bs:
    /// Ölçeklenmiş B matrisi.
    ///
    /// ColScale:
    /// Her thruster kolonunun normalize ölçeği.
    ///
    /// AInv:
    /// Ridge LS çözümü için önceden hesaplanmış ters matris.
    ///
    /// ActiveMask:
    /// Sağlıklı/aktif thruster maskesi.
    /// </summary>
    internal readonly record struct SolverCache(
        double[,] B,
        double[,] Bs,
        double[] ColScale,
        double[,] AInv,
        bool[] ActiveMask)
    {
        public static SolverCache Empty { get; } =
            new(
                new double[0, 0],
                new double[0, 0],
                Array.Empty<double>(),
                new double[0, 0],
                Array.Empty<bool>()
            );

        public bool IsEmpty => ColScale.Length == 0;
    }
}