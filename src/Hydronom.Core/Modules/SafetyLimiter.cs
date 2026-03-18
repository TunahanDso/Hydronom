using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Komut güvenliği:
    /// - 6DoF wrench komutları için NaN/∞ koruması
    /// - eksen bazlı rate limit
    /// - eksen bazlı mikro-oynama filtresi (deadband)
    /// - dt clamp
    /// - planar kullanım için turn-assist
    ///
    /// Amaç:
    /// - Decision katmanının ürettiği 6DoF komutu bozmadan yumuşatmak
    /// - ani sıçramaları ve jitter'ı azaltmak
    /// - fakat sistemi gereksiz yere hantallaştırmamak
    ///
    /// Not:
    /// - Sert saturasyon burada yapılmaz.
    /// - Gerçek fiziksel saturasyon / authority / mixer sınırı ActuatorManager tarafındadır.
    /// </summary>
    public sealed class SafetyLimiter
    {
        // ---------------------------------------------------------------------
        // dt güvenliği
        // ---------------------------------------------------------------------
        private const double DtMin = 1e-4;
        private const double DtMax = 0.25;
        private const double DtFallback = 0.10;

        // ---------------------------------------------------------------------
        // Varsayılan rate limit değerleri
        // Birimler:
        // Fx,Fy,Fz -> N/s
        // Tx,Ty,Tz -> N·m/s
        // ---------------------------------------------------------------------
        private double _fxRatePerSec;
        private double _fyRatePerSec;
        private double _fzRatePerSec;

        private double _txRatePerSec;
        private double _tyRatePerSec;
        private double _tzRatePerSec;

        // ---------------------------------------------------------------------
        // Deadband (mikro jitter filtresi)
        // Kuvvet eksenleri için N, tork eksenleri için N·m
        // ---------------------------------------------------------------------
        private double _fxDeadband;
        private double _fyDeadband;
        private double _fzDeadband;

        private double _txDeadband;
        private double _tyDeadband;
        private double _tzDeadband;

        // ---------------------------------------------------------------------
        // Planar turn assist
        // ---------------------------------------------------------------------
        private bool _turnAssistEnabled = true;
        private double _minFxWhenTurning = 2.0;      // N
        private double _turnTzAbsThreshold = 1.2;    // N·m

        // ---------------------------------------------------------------------
        // Geçmiş komut
        // ---------------------------------------------------------------------
        private DecisionCommand _last = DecisionCommand.Zero;
        private bool _hasLast = false;

        // ---------------------------------------------------------------------
        // Geri uyum API'si
        // ---------------------------------------------------------------------
        public double ThrottleRatePerSec => _fxRatePerSec;
        public double RudderRatePerSec => _tzRatePerSec;

        public SafetyLimiter(
            double throttleRatePerSec = 100.0,
            double rudderRatePerSec = 35.0)
        {
            // Ana eksenler
            _fxRatePerSec = ClampNonNegative(throttleRatePerSec);
            _tzRatePerSec = ClampNonNegative(rudderRatePerSec);

            // Yardımcı eksenler
            _fyRatePerSec = ClampNonNegative(throttleRatePerSec * 0.85);
            _fzRatePerSec = ClampNonNegative(throttleRatePerSec * 0.85);

            _txRatePerSec = ClampNonNegative(rudderRatePerSec * 0.80);
            _tyRatePerSec = ClampNonNegative(rudderRatePerSec * 0.80);

            // Deadband varsayılanları
            _fxDeadband = 0.03;
            _fyDeadband = 0.03;
            _fzDeadband = 0.03;

            _txDeadband = 0.015;
            _tyDeadband = 0.015;
            _tzDeadband = 0.015;
        }

        /// <summary>
        /// Geri uyumlu ayar:
        /// - throttleRatePerSec -> Fx
        /// - rudderRatePerSec   -> Tz
        ///
        /// Not:
        /// Bu çağrı, yan eksenleri otomatik yeniden türetmez.
        /// Yan eksenleri özel yönetmek isteyen akışlar için daha güvenlidir.
        /// </summary>
        public void SetRates(double? thrRatePerSec, double? rudRatePerSec)
        {
            if (thrRatePerSec.HasValue)
                _fxRatePerSec = ClampNonNegative(thrRatePerSec.Value);

            if (rudRatePerSec.HasValue)
                _tzRatePerSec = ClampNonNegative(rudRatePerSec.Value);
        }

        /// <summary>
        /// Fx/Tz tabanlı hızlı profil ayarı.
        /// Yan eksenler de otomatik türetilir.
        /// Operasyon sırasında toplu yeniden ayarlama için uygundur.
        /// </summary>
        public void SetProfile(double throttleRatePerSec, double rudderRatePerSec)
        {
            _fxRatePerSec = ClampNonNegative(throttleRatePerSec);
            _tzRatePerSec = ClampNonNegative(rudderRatePerSec);

            _fyRatePerSec = ClampNonNegative(throttleRatePerSec * 0.85);
            _fzRatePerSec = ClampNonNegative(throttleRatePerSec * 0.85);

            _txRatePerSec = ClampNonNegative(rudderRatePerSec * 0.80);
            _tyRatePerSec = ClampNonNegative(rudderRatePerSec * 0.80);
        }

        /// <summary>
        /// 6DoF eksenleri için ayrı rate limit ayarı.
        /// Null verilen eksen değişmeden kalır.
        /// </summary>
        public void SetAxisRates(
            double? fx = null, double? fy = null, double? fz = null,
            double? tx = null, double? ty = null, double? tz = null)
        {
            if (fx.HasValue) _fxRatePerSec = ClampNonNegative(fx.Value);
            if (fy.HasValue) _fyRatePerSec = ClampNonNegative(fy.Value);
            if (fz.HasValue) _fzRatePerSec = ClampNonNegative(fz.Value);

            if (tx.HasValue) _txRatePerSec = ClampNonNegative(tx.Value);
            if (ty.HasValue) _tyRatePerSec = ClampNonNegative(ty.Value);
            if (tz.HasValue) _tzRatePerSec = ClampNonNegative(tz.Value);
        }

        /// <summary>
        /// 6DoF eksenleri için deadband ayarı.
        /// Null verilen eksen değişmeden kalır.
        /// </summary>
        public void SetAxisDeadbands(
            double? fx = null, double? fy = null, double? fz = null,
            double? tx = null, double? ty = null, double? tz = null)
        {
            if (fx.HasValue) _fxDeadband = ClampNonNegative(fx.Value);
            if (fy.HasValue) _fyDeadband = ClampNonNegative(fy.Value);
            if (fz.HasValue) _fzDeadband = ClampNonNegative(fz.Value);

            if (tx.HasValue) _txDeadband = ClampNonNegative(tx.Value);
            if (ty.HasValue) _tyDeadband = ClampNonNegative(ty.Value);
            if (tz.HasValue) _tzDeadband = ClampNonNegative(tz.Value);
        }

        /// <summary>
        /// Turn-assist parametreleri.
        /// - minFxWhenTurning: dönüşte ileri itiş çok küçük kalırsa taban uygular
        /// - turnTzAbsThreshold: ne kadar yaw torkundan sonra "dönüş" sayılacağı
        /// </summary>
        public void SetTurnAssist(
            bool? enabled = null,
            double? minFxWhenTurning = null,
            double? turnTzAbsThreshold = null)
        {
            if (enabled.HasValue)
                _turnAssistEnabled = enabled.Value;

            if (minFxWhenTurning.HasValue)
                _minFxWhenTurning = ClampNonNegative(minFxWhenTurning.Value);

            if (turnTzAbsThreshold.HasValue)
                _turnTzAbsThreshold = ClampNonNegative(turnTzAbsThreshold.Value);
        }

        /// <summary>
        /// Geçmiş limiter durumunu sıfırlar.
        /// Görev değişimlerinde, emergency stop sonrası veya mod değişiminde çağrılabilir.
        /// </summary>
        public void Reset()
        {
            _last = DecisionCommand.Zero;
            _hasLast = false;
        }

        /// <summary>
        /// 6DoF limiter ana fonksiyonu.
        /// </summary>
        public (DecisionCommand cmd, LimitFlags flags) Limit(DecisionCommand desired, double dtSeconds)
        {
            var flags = new LimitFlags();

            // -----------------------------------------------------------------
            // 0) dt clamp
            // -----------------------------------------------------------------
            double dt = NormalizeDt(dtSeconds, ref flags);

            // -----------------------------------------------------------------
            // 1) Ham komutları al
            // -----------------------------------------------------------------
            double fx = desired.Fx;
            double fy = desired.Fy;
            double fz = desired.Fz;

            double tx = desired.Tx;
            double ty = desired.Ty;
            double tz = desired.Tz;

            // -----------------------------------------------------------------
            // 2) NaN / Infinity guard
            // -----------------------------------------------------------------
            fx = Sanitize(fx, ref flags.InvalidFx);
            fy = Sanitize(fy, ref flags.InvalidFy);
            fz = Sanitize(fz, ref flags.InvalidFz);

            tx = Sanitize(tx, ref flags.InvalidTx);
            ty = Sanitize(ty, ref flags.InvalidTy);
            tz = Sanitize(tz, ref flags.InvalidTz);

            // -----------------------------------------------------------------
            // 3) Deadband
            // -----------------------------------------------------------------
            if (_hasLast)
            {
                fx = ApplyDeadband(fx, _last.Fx, _fxDeadband, ref flags.DeadbandedFx);
                fy = ApplyDeadband(fy, _last.Fy, _fyDeadband, ref flags.DeadbandedFy);
                fz = ApplyDeadband(fz, _last.Fz, _fzDeadband, ref flags.DeadbandedFz);

                tx = ApplyDeadband(tx, _last.Tx, _txDeadband, ref flags.DeadbandedTx);
                ty = ApplyDeadband(ty, _last.Ty, _tyDeadband, ref flags.DeadbandedTy);
                tz = ApplyDeadband(tz, _last.Tz, _tzDeadband, ref flags.DeadbandedTz);
            }

            // -----------------------------------------------------------------
            // 4) Rate limit
            // -----------------------------------------------------------------
            if (_hasLast)
            {
                fx = ApplyRateLimit(fx, _last.Fx, _fxRatePerSec, dt, ref flags.RateLimitedFx);
                fy = ApplyRateLimit(fy, _last.Fy, _fyRatePerSec, dt, ref flags.RateLimitedFy);
                fz = ApplyRateLimit(fz, _last.Fz, _fzRatePerSec, dt, ref flags.RateLimitedFz);

                tx = ApplyRateLimit(tx, _last.Tx, _txRatePerSec, dt, ref flags.RateLimitedTx);
                ty = ApplyRateLimit(ty, _last.Ty, _tyRatePerSec, dt, ref flags.RateLimitedTy);
                tz = ApplyRateLimit(tz, _last.Tz, _tzRatePerSec, dt, ref flags.RateLimitedTz);
            }

            // -----------------------------------------------------------------
            // 5) Turn assist
            // Büyük yaw torku isterken ileri itiş çok küçük ve pozitifse,
            // teknenin "ölü kalmaması" için minimum Fx uygula.
            // -----------------------------------------------------------------
            if (_turnAssistEnabled &&
                fx > 0.0 &&
                Math.Abs(tz) >= _turnTzAbsThreshold &&
                fx < _minFxWhenTurning)
            {
                fx = _minFxWhenTurning;
                flags.TurnAssistApplied = true;
            }

            var output = new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: fz,
                tx: tx,
                ty: ty,
                tz: tz
            );

            _last = output;
            _hasLast = true;

            return (output, flags);
        }

        private static double NormalizeDt(double dtSeconds, ref LimitFlags flags)
        {
            if (double.IsNaN(dtSeconds) || double.IsInfinity(dtSeconds) || dtSeconds <= 0.0)
            {
                flags.DtClamped = true;
                return DtFallback;
            }

            if (dtSeconds < DtMin || dtSeconds > DtMax)
            {
                flags.DtClamped = true;
                return Math.Clamp(dtSeconds, DtMin, DtMax);
            }

            return dtSeconds;
        }

        private static double Sanitize(double value, ref bool flagged)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                flagged = true;
                return 0.0;
            }

            return value;
        }

        private static double ApplyDeadband(double current, double previous, double epsilon, ref bool flagged)
        {
            if (epsilon > 0.0 && Math.Abs(current - previous) < epsilon)
            {
                flagged = true;
                return previous;
            }

            return current;
        }

        private static double ApplyRateLimit(double current, double previous, double ratePerSec, double dt, ref bool flagged)
        {
            // 0 veya negatif ise bu eksende rate limit kapalı kabul edilir
            if (ratePerSec <= 0.0)
                return current;

            double deltaMax = ratePerSec * dt;
            double delta = current - previous;

            if (Math.Abs(delta) > deltaMax)
            {
                flagged = true;
                return previous + Math.Sign(delta) * deltaMax;
            }

            return current;
        }

        private static double ClampNonNegative(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            return Math.Max(0.0, value);
        }
    }

    /// <summary>
    /// Limiter’in yaptığı işlemleri raporlar.
    /// 6DoF ayrıntılarını saklar, ToString içinde özet verir.
    /// </summary>
    public struct LimitFlags
    {
        public bool InvalidFx;
        public bool InvalidFy;
        public bool InvalidFz;
        public bool InvalidTx;
        public bool InvalidTy;
        public bool InvalidTz;

        public bool RateLimitedFx;
        public bool RateLimitedFy;
        public bool RateLimitedFz;
        public bool RateLimitedTx;
        public bool RateLimitedTy;
        public bool RateLimitedTz;

        public bool DeadbandedFx;
        public bool DeadbandedFy;
        public bool DeadbandedFz;
        public bool DeadbandedTx;
        public bool DeadbandedTy;
        public bool DeadbandedTz;

        public bool DtClamped;
        public bool TurnAssistApplied;

        public bool AnyInvalid =>
            InvalidFx || InvalidFy || InvalidFz ||
            InvalidTx || InvalidTy || InvalidTz;

        public bool AnyRateLimited =>
            RateLimitedFx || RateLimitedFy || RateLimitedFz ||
            RateLimitedTx || RateLimitedTy || RateLimitedTz;

        public bool AnyDeadbanded =>
            DeadbandedFx || DeadbandedFy || DeadbandedFz ||
            DeadbandedTx || DeadbandedTy || DeadbandedTz;

        public bool Any =>
            AnyInvalid ||
            AnyRateLimited ||
            AnyDeadbanded ||
            DtClamped ||
            TurnAssistApplied;

        public override string ToString()
        {
            if (!Any)
                return "none";

            return
                $"inv=({B(InvalidFx)}{B(InvalidFy)}{B(InvalidFz)}|{B(InvalidTx)}{B(InvalidTy)}{B(InvalidTz)}), " +
                $"rl=({B(RateLimitedFx)}{B(RateLimitedFy)}{B(RateLimitedFz)}|{B(RateLimitedTx)}{B(RateLimitedTy)}{B(RateLimitedTz)}), " +
                $"db=({B(DeadbandedFx)}{B(DeadbandedFy)}{B(DeadbandedFz)}|{B(DeadbandedTx)}{B(DeadbandedTy)}{B(DeadbandedTz)}), " +
                $"dt={DtClamped}, assist={TurnAssistApplied}";
        }

        private static char B(bool v) => v ? '1' : '0';
    }
}