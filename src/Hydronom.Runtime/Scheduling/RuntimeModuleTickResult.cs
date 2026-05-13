namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Bir modül tick çalışmasının sonucunu taşır.
///
/// Scheduler modülün:
/// - başarılı çalışıp çalışmadığını
/// - çıktı üretip üretmediğini
/// - warning/failure durumlarını
/// standart şekilde takip edebilir.
/// </summary>
public readonly record struct RuntimeModuleTickResult(
    bool Success,
    string Reason,
    bool ProducedOutput = false,
    bool ShouldWarn = false
)
{
    /// <summary>
    /// Başarılı tick sonucu.
    /// </summary>
    public static RuntimeModuleTickResult Ok(
        string reason = "OK",
        bool producedOutput = false)
    {
        return new RuntimeModuleTickResult(
            Success: true,
            Reason: reason,
            ProducedOutput: producedOutput,
            ShouldWarn: false
        );
    }

    /// <summary>
    /// Warning üreten ama sistemi durdurmayan tick sonucu.
    /// </summary>
    public static RuntimeModuleTickResult Warn(string reason)
    {
        return new RuntimeModuleTickResult(
            Success: true,
            Reason: string.IsNullOrWhiteSpace(reason)
                ? "WARN"
                : reason.Trim(),
            ProducedOutput: false,
            ShouldWarn: true
        );
    }

    /// <summary>
    /// Başarısız tick sonucu.
    /// </summary>
    public static RuntimeModuleTickResult Fail(string reason)
    {
        return new RuntimeModuleTickResult(
            Success: false,
            Reason: string.IsNullOrWhiteSpace(reason)
                ? "FAILED"
                : reason.Trim(),
            ProducedOutput: false,
            ShouldWarn: true
        );
    }

    /// <summary>
    /// Güvenli normalize edilmiş sonuç üretir.
    /// </summary>
    public RuntimeModuleTickResult Sanitized()
    {
        return new RuntimeModuleTickResult(
            Success,
            string.IsNullOrWhiteSpace(Reason)
                ? (Success ? "OK" : "FAILED")
                : Reason.Trim(),
            ProducedOutput,
            ShouldWarn
        );
    }
}