using System;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// Thruster geometri tanÄ±mÄ±.
    ///
    /// Position ve ForceDir body frame'dedir.
    /// Bu tanÄ±m platform baÄŸÄ±msÄ±zdÄ±r; tekne, denizaltÄ±, drone, kara robotu veya
    /// fabrika iÃ§i mobil platform aynÄ± geometri sÃ¶zleÅŸmesini kullanabilir.
    /// </summary>
    public readonly record struct ThrusterDesc(
        string Id,
        int Channel,
        Vec3 Position,
        Vec3 ForceDir,
        bool Reversed = false,
        bool CanReverse = false
    );

    /// <summary>
    /// GerÃ§ek zamanlÄ± thruster/actuator nesnesi.
    ///
    /// Bu sÄ±nÄ±f fiziksel yerleÅŸimi, komutlanan Ã§Ä±kÄ±ÅŸÄ±, feedback deÄŸerlerini
    /// ve saÄŸlÄ±k durumunu tek yerde taÅŸÄ±r.
    /// </summary>
    public sealed class Thruster
    {
        public string Id { get; }
        public int Channel { get; }
        public Vec3 Position { get; }
        public Vec3 ForceDir { get; }

        /// <summary>
        /// YazÄ±lÄ±msal yÃ¶n kalibrasyonu.
        /// true ise geometri/komut yorumu terslenir.
        /// </summary>
        public bool Reversed { get; }

        /// <summary>
        /// Motor/ESC fiziksel olarak negatif komutu destekliyor mu?
        ///
        /// true  => motor Ã§Ä±kÄ±ÅŸÄ± -1.0 ile +1.0 arasÄ±nda kullanÄ±labilir.
        /// false => negatif motor komutu fiziksel Ã§Ä±kÄ±ÅŸa gitmeden Ã¶nce nÃ¶tre/sÄ±fÄ±ra kÄ±rpÄ±lmalÄ±dÄ±r.
        /// </summary>
        public bool CanReverse { get; }

        /// <summary>
        /// Normalize Ã§Ä±kÄ±ÅŸ komutu.
        /// CanReverse=true iÃ§in beklenen aralÄ±k: [-1, +1]
        /// CanReverse=false iÃ§in fiziksel Ã§Ä±kÄ±ÅŸta beklenen aralÄ±k: [0, +1]
        /// </summary>
        public double Current { get; set; }

        /// <summary>
        /// DonanÄ±mdan gelen akÄ±m geri bildirimi.
        /// Birim: mA
        /// </summary>
        public int CurrentSenseMilliAmp { get; set; }

        /// <summary>
        /// DonanÄ±mdan gelen RPM geri bildirimi.
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
            CanReverse = d.CanReverse;
        }
    }

    [Flags]
    public enum ThrusterHealthFlags
    {
        None = 0,

        /// <summary>
        /// Telemetry belirlenen sÃ¼re iÃ§inde gÃ¼ncellenmedi.
        /// </summary>
        TelemetryStale = 1 << 0,

        /// <summary>
        /// Komut verilmesine raÄŸmen yÃ¼ksek akÄ±m + dÃ¼ÅŸÃ¼k RPM gÃ¶rÃ¼ldÃ¼.
        /// SÄ±kÄ±ÅŸma veya mekanik engel ÅŸÃ¼phesi.
        /// </summary>
        JamSuspected = 1 << 1,

        /// <summary>
        /// Alt seviye kontrolcÃ¼ uyarÄ± bayraÄŸÄ± gÃ¶nderdi.
        /// </summary>
        ControllerWarning = 1 << 2
    }

    /// <summary>
    /// Bir eksenin pozitif ve negatif yÃ¶ndeki teorik otoritesi.
    /// Ã–rnek: Fx ileri/geri, Tz saÄŸ/sol dÃ¶nme momenti.
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
        /// Eksenin iki yÃ¶ne de etki edip edemediÄŸini gÃ¶sterir.
        /// </summary>
        public bool IsBidirectional => HasPositive && HasNegative;

        public override string ToString() => $"(+{Positive:F2}/-{Negative:F2})";
    }

    /// <summary>
    /// AraÃ§ Ã¼stÃ¼ndeki actuator diziliminin hangi eksenlerde otoriteye sahip olduÄŸunu gÃ¶sterir.
    ///
    /// Bu profil Decision, Safety, Analysis, Mission Compatibility ve Hydronom Ops
    /// tarafÄ±nda kullanÄ±labilir.
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
    /// Wrench allocation sonucunun aÃ§Ä±klanabilir raporu.
    ///
    /// Bu rapor ÅŸunu cevaplar:
    /// - Decision hangi kuvvet/torku istedi?
    /// - Thruster dizilimi gerÃ§ekte ne Ã¼retebildi?
    /// - Hata ne kadar?
    /// - Saturation oldu mu?
    /// - SaÄŸlÄ±ksÄ±z thruster var mÄ±?
    /// - Hareket fiziksel/aktÃ¼asyonel olarak sÄ±nÄ±rlÄ± mÄ±?
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
        bool AuthorityLimited,
        int ReverseClampCount = 0
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
                AuthorityLimited: false,
                ReverseClampCount: 0
            );

        public bool IsGood => Success && !AuthorityLimited && NormalizedError <= 0.25;

        public bool IsPoor => !Success || NormalizedError > 0.50 || AuthorityLimited;

        public bool HadReverseClamp => ReverseClampCount > 0;

        public override string ToString()
        {
            return
                $"{Reason} " +
                $"err={NormalizedError:F3} " +
                $"sat={SaturationRatio:F2} " +
                $"active={ActiveThrusterCount} " +
                $"healthy={HealthyThrusterCount} " +
                $"limited={AuthorityLimited} " +
                $"revClamp={ReverseClampCount}";
        }
    }

    /// <summary>
    /// Solver cache.
    ///
    /// B:
    /// 6xM control effectiveness matrix.
    ///
    /// Bs:
    /// Ã–lÃ§eklenmiÅŸ B matrisi.
    ///
    /// ColScale:
    /// Her thruster kolonunun normalize Ã¶lÃ§eÄŸi.
    ///
    /// AInv:
    /// Ridge LS Ã§Ã¶zÃ¼mÃ¼ iÃ§in Ã¶nceden hesaplanmÄ±ÅŸ ters matris.
    ///
    /// ActiveMask:
    /// SaÄŸlÄ±klÄ±/aktif thruster maskesi.
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
