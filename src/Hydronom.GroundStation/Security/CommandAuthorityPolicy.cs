namespace Hydronom.GroundStation.Security;

/// <summary>
/// Ground Station komutlarÄ± iÃ§in ilk seviye yetki politikasÄ±nÄ± temsil eder.
/// </summary>
public sealed record CommandAuthorityPolicy
{
    /// <summary>
    /// Operator-issued olmayan komutlara izin verilsin mi?
    /// </summary>
    public bool AllowNonOperatorCommands { get; init; } = true;

    /// <summary>
    /// EmergencyCommand iÃ§in operator-issued zorunlu mu?
    /// </summary>
    public bool RequireOperatorForEmergencyCommands { get; init; } = true;

    /// <summary>
    /// EmergencyCommand iÃ§in Priority Emergency zorunlu mu?
    /// </summary>
    public bool RequireEmergencyPriorityForEmergencyCommands { get; init; } = true;

    /// <summary>
    /// Bilinmeyen hedef araca komut gÃ¶nderimi engellensin mi?
    /// </summary>
    public bool RejectUnknownTargets { get; init; } = true;

    /// <summary>
    /// Offline hedef araca komut gÃ¶nderimi engellensin mi?
    /// </summary>
    public bool RejectOfflineTargets { get; init; } = true;

    /// <summary>
    /// Broadcast komutlara izin verilsin mi?
    /// </summary>
    public bool AllowBroadcastCommands { get; init; } = true;

    /// <summary>
    /// AynÄ± CommandId daha Ã¶nce gÃ¶rÃ¼ldÃ¼yse replay/duplicate olarak reddedilsin mi?
    /// </summary>
    public bool RejectDuplicateCommandIds { get; init; } = true;

    /// <summary>
    /// Komut yaÅŸÄ± kontrol edilsin mi?
    /// </summary>
    public bool RejectStaleCommands { get; init; } = true;

    /// <summary>
    /// Maksimum kabul edilebilir komut yaÅŸÄ±.
    /// </summary>
    public TimeSpan MaxCommandAge { get; init; } = TimeSpan.FromSeconds(30);
}
