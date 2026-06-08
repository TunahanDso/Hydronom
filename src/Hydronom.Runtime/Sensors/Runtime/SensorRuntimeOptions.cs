using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Runtime.Sensors.Runtime;

/// <summary>
/// Hydronom sensör runtime ayarları.
///
/// Önemli mimari karar:
/// CSharpPrimary, fiziksel sensörlerin tamamını C# doğrudan okuyacak demek değildir.
/// CSharpPrimary, sensör verisinin Hydronom içinde C# authority/fusion/state hattına gireceği ana çalışma modudur.
///
/// Fiziksel sensörlerde varsayılan yol:
/// Sensör → Pico/MCU → USB raw frame → C# backend → SensorSample → Fusion/StateAuthority
/// </summary>
public sealed class SensorRuntimeOptions
{
    /// <summary>
    /// Ana sensör runtime modu.
    ///
    /// CSharpPrimary:
    /// Hydronom'un ana sensör, füzyon ve state authority hattı C# tarafındadır.
    ///
    /// PythonBackup:
    /// Eski Python pipeline yalnızca açıkça seçilirse fallback olarak çalışır.
    ///
    /// CompareOnly:
    /// C# çıktıları ile yedek/harici pipeline çıktıları karşılaştırılır; harici taraf authority sahibi olmaz.
    ///
    /// Disabled:
    /// Sensör runtime devre dışıdır.
    /// </summary>
    public SensorRuntimeMode Mode { get; set; } = SensorRuntimeMode.CSharpPrimary;

    /// <summary>
    /// Eski Python backup sisteminin kullanılmasına izin verilip verilmediği.
    /// Bu değer true olsa bile Python normal modda ana state'i yönetemez.
    /// </summary>
    public bool PythonBackupEnabled { get; set; } = true;

    /// <summary>
    /// Normal modda Python'ın authoritative state kaynağı olmasına izin verilmez.
    /// Güvenlik için varsayılan false kalmalıdır.
    /// </summary>
    public bool AllowPythonAuthority { get; set; } = false;

    /// <summary>
    /// CompareOnly modunda yedek/harici pipeline çıktıları C# çıktıları ile karşılaştırılır.
    /// Bu mod test/debug amaçlıdır.
    /// </summary>
    public bool CompareWithExternalPipeline { get; set; } = false;

    /// <summary>
    /// Sensörlerden okuma yapılırken runtime host'un ana poll hedefi.
    /// Bu değer tüm sensörlerin gerçek örnekleme frekansı değildir.
    /// </summary>
    public double RuntimeRateHz { get; set; } = 20.0;

    /// <summary>
    /// Sensör örneği bu süreden eskiyse stale kabul edilir.
    /// </summary>
    public double StaleSampleMs { get; set; } = 750.0;

    /// <summary>
    /// Ardışık hata sayısı bu eşiği geçerse sensör degraded/failing durumuna yaklaşır.
    /// </summary>
    public int ConsecutiveFailureWarningThreshold { get; set; } = 3;

    /// <summary>
    /// Ardışık hata sayısı bu eşiği geçerse sensör critical/failing kabul edilebilir.
    /// </summary>
    public int ConsecutiveFailureCriticalThreshold { get; set; } = 8;

    /// <summary>
    /// Simülasyon backend'lerinin otomatik eklenmesine izin verir.
    ///
    /// Gerçek sistemde varsayılan false olmalıdır.
    /// Aksi halde Pico/Gerçek sensör beklerken sistem sim IMU/GPS ile kendini kandırabilir.
    /// </summary>
    public bool EnableDefaultSimSensors { get; set; } = false;

    /// <summary>
    /// Pico/MCU üzerinden USB ile gelen gerçek ham sensör verisi hattını etkinleştirir.
    ///
    /// Bu seçenek firmware kodu oluşturmaz.
    /// Sadece C# runtime tarafında Pico'dan gelen raw frame'lerin sensör sample'a dönüştürüleceğini belirtir.
    /// </summary>
    public bool EnablePicoUsbSensors { get; set; } = true;

    /// <summary>
    /// IMU sensör ailesini etkinleştirir.
    /// Gerçek modda IMU verisi Pico USB hattından gelebilir.
    /// Sim modda sim backend üzerinden üretilebilir.
    /// </summary>
    public bool EnableImu { get; set; } = true;

    /// <summary>
    /// GPS/GNSS sensör ailesini etkinleştirir.
    /// Gerçek modda GPS verisi Pico USB hattından gelebilir.
    /// Sim modda sim backend üzerinden üretilebilir.
    /// </summary>
    public bool EnableGps { get; set; } = true;

    /// <summary>
    /// LiDAR sensör ailesini etkinleştirir.
    /// Kamera hariç sensörler Pico hattına taşınacağı için gerçek LiDAR bağlantısı da ileride Pico/MCU protokolüyle gelebilir.
    /// </summary>
    public bool EnableLidar { get; set; } = false;

    /// <summary>
    /// Kamera sensör ailesini etkinleştirir.
    /// Kamera Pico hattına dahil değildir; ayrı yüksek bant genişlikli yol kullanır.
    /// </summary>
    public bool EnableCamera { get; set; } = false;

    /// <summary>
    /// Varsayılan ayar nesnesi üretir.
    /// </summary>
    public static SensorRuntimeOptions Default()
    {
        return new SensorRuntimeOptions();
    }
}