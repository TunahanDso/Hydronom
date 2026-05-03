namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafÄ±nda gelen HydronomEnvelope mesajlarÄ±nÄ±
/// mesaj tipine gÃ¶re ilgili handler'a yÃ¶nlendiren kÃ¼Ã§Ã¼k dispatcher sÄ±nÄ±fÄ±dÄ±r.
/// 
/// Bu sÄ±nÄ±fÄ±n amacÄ±:
/// - GroundStationEngine iÃ§inde bÃ¼yÃ¼yen if/switch karmaÅŸasÄ±nÄ± engellemek,
/// - Mesaj iÅŸleme mantÄ±ÄŸÄ±nÄ± merkezi hale getirmek,
/// - Ä°leride FleetHeartbeat, FleetCommandResult, TelemetryFrame,
///   CapabilityAnnouncement, LinkQualityReport gibi mesajlarÄ± daha temiz yÃ¶netmektir.
/// 
/// Åu an ilk fazda sadece FleetHeartbeat desteklenir.
/// </summary>
public sealed class GroundMessageDispatcher
{
    /// <summary>
    /// FleetHeartbeat mesajÄ± geldiÄŸinde Ã§alÄ±ÅŸtÄ±rÄ±lacak handler.
    /// 
    /// GroundStationEngine bu handler'Ä± FleetRegistry.ApplyHeartbeat'e baÄŸlayabilir.
    /// BÃ¶ylece dispatcher registry'yi doÄŸrudan bilmez; sadece mesajÄ± yÃ¶nlendirir.
    /// </summary>
    private readonly Func<FleetHeartbeat, bool> _onHeartbeat;

    /// <summary>
    /// FleetCommandResult mesajÄ± geldiÄŸinde Ã§alÄ±ÅŸtÄ±rÄ±labilecek handler.
    /// 
    /// Åimdilik opsiyonel bÄ±rakÄ±lmÄ±ÅŸtÄ±r.
    /// Ä°leride komut sonucu takibi, operatÃ¶r paneli ve event timeline iÃ§in kullanÄ±lacak.
    /// </summary>
    private readonly Func<FleetCommandResult, bool>? _onCommandResult;

    /// <summary>
    /// GroundMessageDispatcher oluÅŸturur.
    /// 
    /// Ä°lk zorunlu handler FleetHeartbeat iÃ§indir.
    /// Ã‡Ã¼nkÃ¼ FleetRegistry'nin gÃ¼ncel kalmasÄ± iÃ§in heartbeat temel mesajdÄ±r.
    /// </summary>
    public GroundMessageDispatcher(
        Func<FleetHeartbeat, bool> onHeartbeat,
        Func<FleetCommandResult, bool>? onCommandResult = null)
    {
        _onHeartbeat = onHeartbeat ?? throw new ArgumentNullException(nameof(onHeartbeat));
        _onCommandResult = onCommandResult;
    }

    /// <summary>
    /// Gelen envelope'u MessageType alanÄ±na gÃ¶re ilgili iÅŸleyiciye yÃ¶nlendirir.
    /// 
    /// DÃ¶nÃ¼ÅŸ:
    /// - true: mesaj tanÄ±ndÄ± ve baÅŸarÄ±yla iÅŸlendi
    /// - false: mesaj tanÄ±nmadÄ±, payload uyumsuzdu veya handler baÅŸarÄ±sÄ±z oldu
    /// </summary>
    public bool Dispatch(HydronomEnvelope envelope)
    {
        if (envelope is null)
            return false;

        if (string.IsNullOrWhiteSpace(envelope.MessageType))
            return false;

        return envelope.MessageType switch
        {
            "FleetHeartbeat" => DispatchHeartbeat(envelope.Payload),
            "FleetCommandResult" => DispatchCommandResult(envelope.Payload),

            _ => false
        };
    }

    /// <summary>
    /// Payload iÃ§inden FleetHeartbeat modelini Ã§Ä±karÄ±r ve heartbeat handler'Ä±na yollar.
    /// 
    /// Not:
    /// Åu an aynÄ± proses iÃ§inde object payload taÅŸÄ±dÄ±ÄŸÄ±mÄ±z iÃ§in doÄŸrudan cast yeterli.
    /// GerÃ§ek transport/JSON aÅŸamasÄ±nda payload deserialize katmanÄ± eklenecek.
    /// </summary>
    private bool DispatchHeartbeat(object? payload)
    {
        if (payload is not FleetHeartbeat heartbeat)
            return false;

        if (!heartbeat.IsValid)
            return false;

        return _onHeartbeat(heartbeat);
    }

    /// <summary>
    /// Payload iÃ§inden FleetCommandResult modelini Ã§Ä±karÄ±r ve varsa command result handler'Ä±na yollar.
    /// 
    /// Åimdilik handler verilmemiÅŸse false dÃ¶ner.
    /// Ä°leride GroundStation komut geÃ§miÅŸi tuttuÄŸunda bu aktifleÅŸecek.
    /// </summary>
    private bool DispatchCommandResult(object? payload)
    {
        if (_onCommandResult is null)
            return false;

        if (payload is not FleetCommandResult result)
            return false;

        if (!result.IsValid)
            return false;

        return _onCommandResult(result);
    }
}
