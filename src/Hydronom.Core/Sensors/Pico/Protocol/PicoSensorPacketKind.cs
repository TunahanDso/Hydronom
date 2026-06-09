namespace Hydronom.Core.Sensors.Pico.Protocol;

/// <summary>
/// Pico tarafından Hydronom'a gönderilebilecek paket ailesini belirtir.
/// Bu enum payload içeriğinin nasıl yorumlanacağını belirleyen ilk seviye ayrımdır.
/// </summary>
public enum PicoSensorPacketKind
{
    Unknown = 0,

    /// <summary>
    /// Pico node canlılık bildirimi.
    /// </summary>
    Heartbeat = 1,

    /// <summary>
    /// Pico node üzerinde hangi sensör kanallarının bulunduğunu bildirir.
    /// </summary>
    Capabilities = 2,

    /// <summary>
    /// Pico node veya kanal sağlık bildirimi.
    /// </summary>
    Health = 3,

    /// <summary>
    /// Tek bir sensör örneği taşır.
    /// </summary>
    SensorSample = 10,

    /// <summary>
    /// Birden fazla sensör örneği taşır.
    /// </summary>
    SensorBatch = 11,

    /// <summary>
    /// Pico ile Hydronom arasında zaman hizalama paketi.
    /// </summary>
    TimeSync = 20,

    /// <summary>
    /// Komut veya konfigürasyon onayı.
    /// </summary>
    Ack = 30,

    /// <summary>
    /// Pico tarafında oluşan hata bildirimi.
    /// </summary>
    Error = 31,

    Custom = 1000
}