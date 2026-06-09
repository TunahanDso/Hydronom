namespace Hydronom.Core.Sensors.Pico.Protocol;

/// <summary>
/// Pico frame bütünlük kontrolünde kullanılan checksum türüdür.
/// İlk aşamada runtime bu bilgiyi taşır; gerçek doğrulama Runtime decoder tarafında yapılır.
/// </summary>
public enum PicoSensorChecksumKind
{
    None = 0,

    /// <summary>
    /// Basit 8-bit toplam kontrolü.
    /// Debug/prototip için uygundur, kalıcı güvenlik için zayıftır.
    /// </summary>
    Sum8 = 1,

    /// <summary>
    /// Ciddi embedded haberleşme için uygun CRC-16 CCITT.
    /// </summary>
    Crc16Ccitt = 2,

    /// <summary>
    /// Daha yüksek bütünlük kontrolü gereken paketler için CRC-32.
    /// </summary>
    Crc32 = 3
}