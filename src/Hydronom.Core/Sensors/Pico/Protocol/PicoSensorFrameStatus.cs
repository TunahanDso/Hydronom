namespace Hydronom.Core.Sensors.Pico.Protocol;

/// <summary>
/// Pico raw frame'in Hydronom tarafındaki çözümleme durumudur.
/// Bu değer sensör verisinin kendisinden çok, taşıma/protokol doğruluğunu anlatır.
/// </summary>
public enum PicoSensorFrameStatus
{
    Unknown = 0,

    /// <summary>
    /// Frame yapısal olarak geçerli.
    /// </summary>
    Valid = 1,

    /// <summary>
    /// Frame henüz tamamlanmamış veya eksik byte içeriyor.
    /// </summary>
    Incomplete = 2,

    /// <summary>
    /// Checksum doğrulaması başarısız.
    /// </summary>
    ChecksumFailed = 3,

    /// <summary>
    /// Protokol sürümü runtime tarafından desteklenmiyor.
    /// </summary>
    UnsupportedVersion = 4,

    /// <summary>
    /// Paket türü bilinmiyor veya desteklenmiyor.
    /// </summary>
    UnsupportedPacketKind = 5,

    /// <summary>
    /// Sensör kanalı bilinmiyor veya desteklenmiyor.
    /// </summary>
    UnsupportedChannel = 6,

    /// <summary>
    /// Payload beklenen formata çözülemedi.
    /// </summary>
    DecodeError = 7,

    /// <summary>
    /// Frame zaman olarak çok eski.
    /// </summary>
    Stale = 8,

    /// <summary>
    /// Payload boş veya beklenen uzunlukla uyumsuz.
    /// </summary>
    EmptyPayload = 9
}