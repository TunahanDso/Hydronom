namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// Bir Hydronom aracÄ±nÄ±n veya node'un sahip olduÄŸu kabiliyetleri temsil eder.
/// 
/// Fleet & Ground Station mimarisinde yer istasyonu sadece aracÄ±n var olduÄŸunu bilmemeli;
/// o aracÄ±n ne yapabildiÄŸini de bilmelidir.
/// 
/// Ã–rneÄŸin:
/// - Bu araÃ§ navigation yapabiliyor mu?
/// - LiDAR var mÄ±?
/// - Kamera var mÄ±?
/// - Mapping destekliyor mu?
/// - RF veya LoRa linki var mÄ±?
/// - ManipÃ¼latÃ¶r / Ã¶zel gÃ¶rev ekipmanÄ± var mÄ±?
/// 
/// FleetCoordinator ve MissionAllocator ileride gÃ¶rev daÄŸÄ±tÄ±rken bu kabiliyetleri kullanÄ±r.
/// </summary>
public sealed record VehicleCapability
{
    /// <summary>
    /// Kabiliyetin benzersiz adÄ±.
    /// 
    /// Ã–rnekler:
    /// - "navigation"
    /// - "lidar"
    /// - "camera"
    /// - "mapping"
    /// - "obstacle_detection"
    /// - "target_tracking"
    /// - "autonomous_mission"
    /// - "manual_control"
    /// - "relay"
    /// - "water_quality_sensor"
    /// 
    /// KÃ¼Ã§Ã¼k harfli ve snake_case tutulmasÄ± Ã¶nerilir.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Kabiliyetin kÄ±sa aÃ§Ä±klamasÄ±.
    /// 
    /// Bu alan Ã¶zellikle Hydronom Ops tarafÄ±nda aracÄ±n detay ekranÄ±nda
    /// operatÃ¶re okunabilir bilgi gÃ¶stermek iÃ§in kullanÄ±labilir.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Kabiliyetin aktif olup olmadÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// true:
    /// - Kabiliyet mevcut ve kullanÄ±labilir.
    /// 
    /// false:
    /// - Kabiliyet araÃ§ta var ama ÅŸu anda devre dÄ±ÅŸÄ± olabilir.
    /// - SensÃ¶r arÄ±zalÄ± olabilir.
    /// - YazÄ±lÄ±m modÃ¼lÃ¼ kapatÄ±lmÄ±ÅŸ olabilir.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Kabiliyetin saÄŸlÄ±k / kullanÄ±labilirlik durumu.
    /// 
    /// Ã–rnekler:
    /// - "OK"
    /// - "Warning"
    /// - "Fault"
    /// - "Unavailable"
    /// - "Simulated"
    /// 
    /// Åimdilik string bÄ±rakÄ±yoruz.
    /// Ã‡Ã¼nkÃ¼ health modelini ileride daha geniÅŸ bir yapÄ±ya baÄŸlayabiliriz.
    /// </summary>
    public string Health { get; init; } = "OK";

    /// <summary>
    /// Bu kabiliyetin simÃ¼lasyon Ã¼zerinden mi saÄŸlandÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// Ã–rnek:
    /// - Sim LiDAR
    /// - Sim GPS
    /// - Mock actuator
    /// - FileReplay telemetry
    /// 
    /// FleetRegistry ve Ops UI bu bilgiyi kullanarak gerÃ§ek/sim ayrÄ±mÄ± gÃ¶sterebilir.
    /// </summary>
    public bool IsSimulated { get; init; }

    /// <summary>
    /// Bu kabiliyetin iliÅŸkili olduÄŸu haberleÅŸme transport tÃ¼rleri.
    /// 
    /// Ã–zellikle haberleÅŸme kabiliyetleri iÃ§in Ã¶nemlidir.
    /// 
    /// Ã–rnek:
    /// - LoRa modÃ¼lÃ¼ iÃ§in: TransportKind.LoRa
    /// - RF modem iÃ§in: TransportKind.RfModem
    /// - Wi-Fi/TCP baÄŸlantÄ± iÃ§in: TransportKind.Tcp
    /// 
    /// SensÃ¶r kabiliyetlerinde boÅŸ kalabilir.
    /// </summary>
    public IReadOnlyList<TransportKind> RelatedTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Kabiliyetle ilgili ek metadata bilgileri.
    /// 
    /// Ã–rnekler:
    /// - "rangeMeters": "2000"
    /// - "maxPayloadBytes": "240"
    /// - "bandwidthClass": "Low"
    /// - "latencyClass": "High"
    /// - "sensorModel": "RPLidar A1"
    /// 
    /// Åimdilik string/string dictionary kullanÄ±yoruz.
    /// Ä°leride daha gÃ¼Ã§lÃ¼ capability schema'larÄ±na geÃ§ilebilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Kabiliyet kaydÄ±nÄ±n temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En azÄ±ndan Name dolu olmalÄ±dÄ±r.
    /// Ã‡Ã¼nkÃ¼ gÃ¶rev daÄŸÄ±tÄ±mÄ± ve filtreleme bu ad Ã¼zerinden yapÄ±lÄ±r.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name);
}
