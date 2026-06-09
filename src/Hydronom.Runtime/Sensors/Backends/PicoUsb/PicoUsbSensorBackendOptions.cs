using Hydronom.Core.Sensors.Common.Connections;

namespace Hydronom.Runtime.Sensors.Backends.PicoUsb;

/// <summary>
/// Pico USB sensör backend ayarları.
/// 
/// Bu sınıf Pico firmware değildir.
/// Sadece Hydronom Runtime'ın Pico tabanlı sensör node'una nasıl bağlanacağını anlatır.
/// </summary>
public sealed record PicoUsbSensorBackendOptions
{
    /// <summary>
    /// Registry/backend adı.
    /// </summary>
    public string BackendName { get; init; } = "pico_usb";

    /// <summary>
    /// Pico node kimliği.
    /// Örnek: pico0, pico_bow, pico_sensor_hub
    /// </summary>
    public string DeviceId { get; init; } = "pico0";

    /// <summary>
    /// COM port adı.
    /// Windows örneği: COM7
    /// Linux örneği: /dev/ttyACM0
    /// "auto" ise ileride discovery sistemi port seçecek.
    /// </summary>
    public string PortName { get; init; } = "auto";

    /// <summary>
    /// USB CDC/Serial baudrate.
    /// Pico USB CDC için çoğu durumda semboliktir ama debug/adapter senaryolarında kullanılır.
    /// </summary>
    public int BaudRate { get; init; } = 115200;

    /// <summary>
    /// Hedef okuma oranı.
    /// Bu backend tek bir fiziksel sensör değil, çoklu kanal hub olduğu için genel hedef rate'tir.
    /// </summary>
    public double TargetRateHz { get; init; } = 50.0;

    /// <summary>
    /// Frame bayatlama eşiği.
    /// </summary>
    public double StaleAfterMs { get; init; } = 1000.0;

    /// <summary>
    /// Açılışta gerçek port yoksa backend'in hata vermeden offline/degraded kalmasına izin verir.
    /// İlk mimari pakette true kalmalı.
    /// </summary>
    public bool AllowPassiveOpenWithoutPort { get; init; } = true;

    /// <summary>
    /// İlk pakette gerçek serial reader aktif değil.
    /// Bu flag ileride System.IO.Ports veya platform adapter bağlandığında kullanılacak.
    /// </summary>
    public bool EnablePhysicalSerialRead { get; init; } = false;

    /// <summary>
    /// Payload decode edilemezse sample üretmek yerine null dön.
    /// </summary>
    public bool DropInvalidFrames { get; init; } = true;

    /// <summary>
    /// Kalibrasyon etiketi.
    /// </summary>
    public string CalibrationId { get; init; } = "pico_uncalibrated";

    public SensorConnectionDescriptor Connection => SensorConnectionDescriptor.UsbSerial(
        portName: PortName,
        baudRate: BaudRate
    );

    public static PicoUsbSensorBackendOptions Default()
    {
        return new PicoUsbSensorBackendOptions();
    }

    public PicoUsbSensorBackendOptions Sanitized()
    {
        return this with
        {
            BackendName = Normalize(BackendName, "pico_usb"),
            DeviceId = Normalize(DeviceId, "pico0"),
            PortName = Normalize(PortName, "auto"),
            BaudRate = BaudRate <= 0 ? 115200 : BaudRate,
            TargetRateHz = SafeNonNegative(TargetRateHz, 50.0),
            StaleAfterMs = SafeNonNegative(StaleAfterMs, 1000.0),
            CalibrationId = Normalize(CalibrationId, "pico_uncalibrated")
        };
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static double SafeNonNegative(double value, double fallback)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            return fallback;
        }

        return value;
    }
}