using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// 6-DoF komut güvenliği ve hareket yumuşatma katmanı.
    ///
    /// Decision katmanından gelen wrench komutunu güvenli hale getirir:
    /// - NaN / Infinity temizliği
    /// - dt güvenliği
    /// - eksen bazlı deadband
    /// - eksen bazlı rate limit
    /// - opsiyonel mutlak eksen limiti
    /// - planar turn-assist
    /// - açıklanabilir rapor üretimi
    /// </summary>
    public sealed class SafetyLimiter
    {
        private const double DtMin = 1e-4;
        private const double DtMax = 0.25;
        private const double DtFallback = 0.10;

        private AxisValues _ratePerSec;
        private AxisValues _deadband;
        private AxisValues _maxAbs;

        private bool _absoluteLimitsEnabled;

        private bool _turnAssistEnabled = true;
        private double _minFxWhenTurning = 2.0;
        private double _turnTzAbsThreshold = 1.2;

        private DecisionCommand _last = DecisionCommand.Zero;
        private bool _hasLast;

        /// <summary>
        /// Son limiter çalışmasının açıklanabilir raporu.
        /// Runtime log, Analysis, Diagnostics ve Ops tarafı bunu okuyabilir.
        /// </summary>
        public SafetyLimitReport LastReport { get; private set; } =
            SafetyLimitReport.Empty;

        public double ThrottleRatePerSec => _ratePerSec.Fx;
        public double RudderRatePerSec => _ratePerSec.Tz;

        public SafetyLimiter(
            double throttleRatePerSec = 100.0,
            double rudderRatePerSec = 35.0)
        {
            SetProfile(throttleRatePerSec, rudderRatePerSec);

            _deadband = new AxisValues(
                Fx: 0.03,
                Fy: 0.03,
                Fz: 0.03,
                Tx: 0.015,
                Ty: 0.015,
                Tz: 0.015
            );

            _maxAbs = AxisValues.DisabledLimits;
            _absoluteLimitsEnabled = false;
        }

        /// <summary>
        /// Geri uyumlu ayar:
        /// - throttleRatePerSec -> Fx
        /// - rudderRatePerSec   -> Tz
        /// Yan eksenleri değiştirmez.
        /// </summary>
        public void SetRates(double? thrRatePerSec, double? rudRatePerSec)
        {
            if (thrRatePerSec.HasValue)
                _ratePerSec = _ratePerSec with { Fx = ClampNonNegative(thrRatePerSec.Value) };

            if (rudRatePerSec.HasValue)
                _ratePerSec = _ratePerSec with { Tz = ClampNonNegative(rudRatePerSec.Value) };
        }

        /// <summary>
        /// Fx/Tz tabanlı hızlı profil ayarı.
        /// Yan eksenler otomatik türetilir.
        /// </summary>
        public void SetProfile(double throttleRatePerSec, double rudderRatePerSec)
        {
            double fx = ClampNonNegative(throttleRatePerSec);
            double tz = ClampNonNegative(rudderRatePerSec);

            _ratePerSec = new AxisValues(
                Fx: fx,
                Fy: fx * 0.85,
                Fz: fx * 0.85,
                Tx: tz * 0.80,
                Ty: tz * 0.80,
                Tz: tz
            );
        }

        /// <summary>
        /// 6-DoF eksenleri için ayrı rate limit ayarı.
        /// Null verilen eksen değişmeden kalır.
        /// </summary>
        public void SetAxisRates(
            double? fx = null, double? fy = null, double? fz = null,
            double? tx = null, double? ty = null, double? tz = null)
        {
            _ratePerSec = new AxisValues(
                Fx: fx.HasValue ? ClampNonNegative(fx.Value) : _ratePerSec.Fx,
                Fy: fy.HasValue ? ClampNonNegative(fy.Value) : _ratePerSec.Fy,
                Fz: fz.HasValue ? ClampNonNegative(fz.Value) : _ratePerSec.Fz,
                Tx: tx.HasValue ? ClampNonNegative(tx.Value) : _ratePerSec.Tx,
                Ty: ty.HasValue ? ClampNonNegative(ty.Value) : _ratePerSec.Ty,
                Tz: tz.HasValue ? ClampNonNegative(tz.Value) : _ratePerSec.Tz
            );
        }

        /// <summary>
        /// 6-DoF eksenleri için deadband ayarı.
        /// Null verilen eksen değişmeden kalır.
        /// </summary>
        public void SetAxisDeadbands(
            double? fx = null, double? fy = null, double? fz = null,
            double? tx = null, double? ty = null, double? tz = null)
        {
            _deadband = new AxisValues(
                Fx: fx.HasValue ? ClampNonNegative(fx.Value) : _deadband.Fx,
                Fy: fy.HasValue ? ClampNonNegative(fy.Value) : _deadband.Fy,
                Fz: fz.HasValue ? ClampNonNegative(fz.Value) : _deadband.Fz,
                Tx: tx.HasValue ? ClampNonNegative(tx.Value) : _deadband.Tx,
                Ty: ty.HasValue ? ClampNonNegative(ty.Value) : _deadband.Ty,
                Tz: tz.HasValue ? ClampNonNegative(tz.Value) : _deadband.Tz
            );
        }

        /// <summary>
        /// Opsiyonel mutlak eksen limitleri.
        ///
        /// Varsayılan olarak kapalıdır.
        /// Bu limitler açılırsa DecisionCommand değerleri belirtilen maksimum mutlak değerleri aşamaz.
        /// Fiziksel actuator authority yine ActuatorManager tarafında denetlenmelidir.
        /// </summary>
        public void SetAxisAbsoluteLimits(
            bool enabled,
            double? fx = null, double? fy = null, double? fz = null,
            double? tx = null, double? ty = null, double? tz = null)
        {
            _absoluteLimitsEnabled = enabled;

            _maxAbs = new AxisValues(
                Fx: fx.HasValue ? ClampNonNegative(fx.Value) : _maxAbs.Fx,
                Fy: fy.HasValue ? ClampNonNegative(fy.Value) : _maxAbs.Fy,
                Fz: fz.HasValue ? ClampNonNegative(fz.Value) : _maxAbs.Fz,
                Tx: tx.HasValue ? ClampNonNegative(tx.Value) : _maxAbs.Tx,
                Ty: ty.HasValue ? ClampNonNegative(ty.Value) : _maxAbs.Ty,
                Tz: tz.HasValue ? ClampNonNegative(tz.Value) : _maxAbs.Tz
            );
        }

        /// <summary>
        /// Planar turn-assist parametreleri.
        ///
        /// Turn-assist rate limitten önce uygulanır.
        /// Böylece assist de güvenli geçiş filtresinden geçer.
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
        /// Görev değişimi, emergency stop, mode transition veya external override sonrası çağrılabilir.
        /// </summary>
        public void Reset()
        {
            _last = DecisionCommand.Zero;
            _hasLast = false;
            LastReport = SafetyLimitReport.Empty;
        }

        /// <summary>
        /// Geri uyumlu ana API.
        /// </summary>
        public (DecisionCommand cmd, LimitFlags flags) Limit(DecisionCommand desired, double dtSeconds)
        {
            var report = LimitAdvanced(desired, dtSeconds);
            return (report.Output, report.Flags);
        }

        /// <summary>
        /// Açıklanabilir gelişmiş limiter API'si.
        /// </summary>
        public SafetyLimitReport LimitAdvanced(DecisionCommand desired, double dtSeconds)
        {
            var flags = new LimitFlags();

            double dt = NormalizeDt(dtSeconds, ref flags);

            var input = AxisValues.FromCommand(desired).Sanitized(ref flags);

            var afterAssist = ApplyTurnAssist(input, ref flags);

            var afterDeadband = _hasLast
                ? ApplyDeadbands(afterAssist, AxisValues.FromCommand(_last), ref flags)
                : afterAssist;

            var afterRate = _hasLast
                ? ApplyRateLimits(afterDeadband, AxisValues.FromCommand(_last), dt, ref flags)
                : afterDeadband;

            var afterAbsolute = _absoluteLimitsEnabled
                ? ApplyAbsoluteLimits(afterRate, ref flags)
                : afterRate;

            var output = afterAbsolute.ToCommand();

            _last = output;
            _hasLast = true;

            var report = new SafetyLimitReport(
                Input: input.ToCommand(),
                Output: output,
                Flags: flags,
                DtRequested: dtSeconds,
                DtUsed: dt,
                RateProfile: _ratePerSec,
                DeadbandProfile: _deadband,
                AbsoluteLimitProfile: _maxAbs,
                AbsoluteLimitsEnabled: _absoluteLimitsEnabled
            );

            LastReport = report;
            return report;
        }

        private AxisValues ApplyTurnAssist(AxisValues current, ref LimitFlags flags)
        {
            if (!_turnAssistEnabled)
                return current;

            if (current.Fx > 0.0 &&
                Math.Abs(current.Tz) >= _turnTzAbsThreshold &&
                current.Fx < _minFxWhenTurning)
            {
                flags.TurnAssistApplied = true;
                return current with { Fx = _minFxWhenTurning };
            }

            return current;
        }

        private AxisValues ApplyDeadbands(AxisValues current, AxisValues previous, ref LimitFlags flags)
        {
            return new AxisValues(
                Fx: ApplyDeadband(current.Fx, previous.Fx, _deadband.Fx, ref flags.DeadbandedFx),
                Fy: ApplyDeadband(current.Fy, previous.Fy, _deadband.Fy, ref flags.DeadbandedFy),
                Fz: ApplyDeadband(current.Fz, previous.Fz, _deadband.Fz, ref flags.DeadbandedFz),
                Tx: ApplyDeadband(current.Tx, previous.Tx, _deadband.Tx, ref flags.DeadbandedTx),
                Ty: ApplyDeadband(current.Ty, previous.Ty, _deadband.Ty, ref flags.DeadbandedTy),
                Tz: ApplyDeadband(current.Tz, previous.Tz, _deadband.Tz, ref flags.DeadbandedTz)
            );
        }

        private AxisValues ApplyRateLimits(AxisValues current, AxisValues previous, double dt, ref LimitFlags flags)
        {
            return new AxisValues(
                Fx: ApplyRateLimit(current.Fx, previous.Fx, _ratePerSec.Fx, dt, ref flags.RateLimitedFx),
                Fy: ApplyRateLimit(current.Fy, previous.Fy, _ratePerSec.Fy, dt, ref flags.RateLimitedFy),
                Fz: ApplyRateLimit(current.Fz, previous.Fz, _ratePerSec.Fz, dt, ref flags.RateLimitedFz),
                Tx: ApplyRateLimit(current.Tx, previous.Tx, _ratePerSec.Tx, dt, ref flags.RateLimitedTx),
                Ty: ApplyRateLimit(current.Ty, previous.Ty, _ratePerSec.Ty, dt, ref flags.RateLimitedTy),
                Tz: ApplyRateLimit(current.Tz, previous.Tz, _ratePerSec.Tz, dt, ref flags.RateLimitedTz)
            );
        }

        private AxisValues ApplyAbsoluteLimits(AxisValues current, ref LimitFlags flags)
        {
            return new AxisValues(
                Fx: ApplyAbsoluteLimit(current.Fx, _maxAbs.Fx, ref flags.AbsoluteLimitedFx),
                Fy: ApplyAbsoluteLimit(current.Fy, _maxAbs.Fy, ref flags.AbsoluteLimitedFy),
                Fz: ApplyAbsoluteLimit(current.Fz, _maxAbs.Fz, ref flags.AbsoluteLimitedFz),
                Tx: ApplyAbsoluteLimit(current.Tx, _maxAbs.Tx, ref flags.AbsoluteLimitedTx),
                Ty: ApplyAbsoluteLimit(current.Ty, _maxAbs.Ty, ref flags.AbsoluteLimitedTy),
                Tz: ApplyAbsoluteLimit(current.Tz, _maxAbs.Tz, ref flags.AbsoluteLimitedTz)
            );
        }

        private static double NormalizeDt(double dtSeconds, ref LimitFlags flags)
        {
            if (!double.IsFinite(dtSeconds) || dtSeconds <= 0.0)
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

        private static double ApplyAbsoluteLimit(double value, double maxAbs, ref bool flagged)
        {
            if (maxAbs <= 0.0 || !double.IsFinite(maxAbs))
                return value;

            double limited = Math.Clamp(value, -maxAbs, maxAbs);

            if (Math.Abs(limited - value) > 1e-9)
                flagged = true;

            return limited;
        }

        private static double ClampNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Max(0.0, value);
        }
    }

    /// <summary>
    /// 6 eksenli değer paketi.
    /// Fx,Fy,Fz kuvvet; Tx,Ty,Tz tork eksenleridir.
    /// </summary>
    public readonly record struct AxisValues(
        double Fx,
        double Fy,
        double Fz,
        double Tx,
        double Ty,
        double Tz
    )
    {
        public static AxisValues Zero => new(
            Fx: 0.0,
            Fy: 0.0,
            Fz: 0.0,
            Tx: 0.0,
            Ty: 0.0,
            Tz: 0.0
        );

        public static AxisValues DisabledLimits => Zero;

        public static AxisValues FromCommand(DecisionCommand cmd)
        {
            return new AxisValues(
                Fx: cmd.Fx,
                Fy: cmd.Fy,
                Fz: cmd.Fz,
                Tx: cmd.Tx,
                Ty: cmd.Ty,
                Tz: cmd.Tz
            );
        }

        public DecisionCommand ToCommand()
        {
            return new DecisionCommand(
                fx: Fx,
                fy: Fy,
                fz: Fz,
                tx: Tx,
                ty: Ty,
                tz: Tz
            );
        }

        public AxisValues Sanitized(ref LimitFlags flags)
        {
            return new AxisValues(
                Fx: Sanitize(Fx, ref flags.InvalidFx),
                Fy: Sanitize(Fy, ref flags.InvalidFy),
                Fz: Sanitize(Fz, ref flags.InvalidFz),
                Tx: Sanitize(Tx, ref flags.InvalidTx),
                Ty: Sanitize(Ty, ref flags.InvalidTy),
                Tz: Sanitize(Tz, ref flags.InvalidTz)
            );
        }

        private static double Sanitize(double value, ref bool flag)
        {
            if (!double.IsFinite(value))
            {
                flag = true;
                return 0.0;
            }

            return value;
        }

        public override string ToString()
            => $"F=({Fx:F2},{Fy:F2},{Fz:F2}) T=({Tx:F2},{Ty:F2},{Tz:F2})";
    }

    /// <summary>
    /// Limiter'in yaptığı işlemleri raporlar.
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

        public bool AbsoluteLimitedFx;
        public bool AbsoluteLimitedFy;
        public bool AbsoluteLimitedFz;
        public bool AbsoluteLimitedTx;
        public bool AbsoluteLimitedTy;
        public bool AbsoluteLimitedTz;

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

        public bool AnyAbsoluteLimited =>
            AbsoluteLimitedFx || AbsoluteLimitedFy || AbsoluteLimitedFz ||
            AbsoluteLimitedTx || AbsoluteLimitedTy || AbsoluteLimitedTz;

        public bool Any =>
            AnyInvalid ||
            AnyRateLimited ||
            AnyDeadbanded ||
            AnyAbsoluteLimited ||
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
                $"abs=({B(AbsoluteLimitedFx)}{B(AbsoluteLimitedFy)}{B(AbsoluteLimitedFz)}|{B(AbsoluteLimitedTx)}{B(AbsoluteLimitedTy)}{B(AbsoluteLimitedTz)}), " +
                $"dt={DtClamped}, assist={TurnAssistApplied}";
        }

        private static char B(bool v) => v ? '1' : '0';
    }

    /// <summary>
    /// SafetyLimiter çalışmasının açıklanabilir raporu.
    /// </summary>
    public readonly record struct SafetyLimitReport(
        DecisionCommand Input,
        DecisionCommand Output,
        LimitFlags Flags,
        double DtRequested,
        double DtUsed,
        AxisValues RateProfile,
        AxisValues DeadbandProfile,
        AxisValues AbsoluteLimitProfile,
        bool AbsoluteLimitsEnabled
    )
    {
        public static SafetyLimitReport Empty { get; } =
            new(
                Input: DecisionCommand.Zero,
                Output: DecisionCommand.Zero,
                Flags: new LimitFlags(),
                DtRequested: 0.0,
                DtUsed: 0.0,
                RateProfile: AxisValues.Zero,
                DeadbandProfile: AxisValues.Zero,
                AbsoluteLimitProfile: AxisValues.DisabledLimits,
                AbsoluteLimitsEnabled: false
            );

        public bool WasLimited => Flags.Any;

        public override string ToString()
        {
            return
                $"SafetyLimit limited={WasLimited} " +
                $"dt={DtUsed:F4}s " +
                $"in={AxisValues.FromCommand(Input)} " +
                $"out={AxisValues.FromCommand(Output)} " +
                $"flags=[{Flags}]";
        }
    }
}