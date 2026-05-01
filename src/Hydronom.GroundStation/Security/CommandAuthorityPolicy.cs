namespace Hydronom.GroundStation.Security;

/// <summary>
/// Ground Station komutları için ilk seviye yetki politikasını temsil eder.
/// </summary>
public sealed record CommandAuthorityPolicy
{
    /// <summary>
    /// Operator-issued olmayan komutlara izin verilsin mi?
    /// </summary>
    public bool AllowNonOperatorCommands { get; init; } = true;

    /// <summary>
    /// EmergencyCommand için operator-issued zorunlu mu?
    /// </summary>
    public bool RequireOperatorForEmergencyCommands { get; init; } = true;

    /// <summary>
    /// EmergencyCommand için Priority Emergency zorunlu mu?
    /// </summary>
    public bool RequireEmergencyPriorityForEmergencyCommands { get; init; } = true;

    /// <summary>
    /// Bilinmeyen hedef araca komut gönderimi engellensin mi?
    /// </summary>
    public bool RejectUnknownTargets { get; init; } = true;

    /// <summary>
    /// Offline hedef araca komut gönderimi engellensin mi?
    /// </summary>
    public bool RejectOfflineTargets { get; init; } = true;

    /// <summary>
    /// Broadcast komutlara izin verilsin mi?
    /// </summary>
    public bool AllowBroadcastCommands { get; init; } = true;

    /// <summary>
    /// Aynı CommandId daha önce görüldüyse replay/duplicate olarak reddedilsin mi?
    /// </summary>
    public bool RejectDuplicateCommandIds { get; init; } = true;

    /// <summary>
    /// Komut yaşı kontrol edilsin mi?
    /// </summary>
    public bool RejectStaleCommands { get; init; } = true;

    /// <summary>
    /// Maksimum kabul edilebilir komut yaşı.
    /// </summary>
    public TimeSpan MaxCommandAge { get; init; } = TimeSpan.FromSeconds(30);
}