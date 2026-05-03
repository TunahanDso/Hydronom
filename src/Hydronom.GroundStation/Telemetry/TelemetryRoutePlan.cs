namespace Hydronom.GroundStation.Telemetry;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;

/// <summary>
/// CommunicationRouter tarafÄ±ndan Ã¼retilen route sonucuna gÃ¶re
/// seÃ§ilmiÅŸ telemetry planÄ±nÄ± temsil eder.
/// 
/// Bu model ÅŸu sorulara cevap verir:
/// - Mesaj/araÃ§ route edilebilir mi?
/// - Hangi telemetry profili seÃ§ildi?
/// - Hangi transport'lar Ã¼zerinden telemetry mantÄ±klÄ±?
/// - Bu karar neden verildi?
/// 
/// Ä°lk fazda gerÃ§ek telemetry payload Ã¼retmez.
/// Sadece Ground Station tarafÄ±nda telemetry yoÄŸunluÄŸu kararÄ±nÄ± route sonucuyla birleÅŸtirir.
/// </summary>
public sealed record TelemetryRoutePlan
{
    /// <summary>
    /// Route edilen mesaj kimliÄŸi.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Route edilen mesaj tipi.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node kimliÄŸi.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Route edilebilir mi?
    /// 
    /// false ise telemetry profili yine gÃ¼venli varsayÄ±lan olarak Light olabilir;
    /// fakat plan uygulanabilir kabul edilmez.
    /// </summary>
    public bool CanRoute { get; init; }

    /// <summary>
    /// SeÃ§ilen telemetry profili.
    /// 
    /// Ã–rnek:
    /// - Light
    /// - Normal
    /// - Full
    /// </summary>
    public TelemetryProfile Profile { get; init; } = TelemetryProfile.Unknown;

    /// <summary>
    /// Profile kararÄ±nÄ±n aÃ§Ä±klamasÄ±.
    /// </summary>
    public string ProfileReason { get; init; } = string.Empty;

    /// <summary>
    /// Route kararÄ±nÄ±n aÃ§Ä±klamasÄ±.
    /// </summary>
    public string RouteReason { get; init; } = string.Empty;

    /// <summary>
    /// Telemetry iÃ§in kullanÄ±labilir birincil transport'lar.
    /// </summary>
    public IReadOnlyList<TransportKind> PrimaryTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Telemetry iÃ§in kullanÄ±labilir fallback transport'lar.
    /// </summary>
    public IReadOnlyList<TransportKind> FallbackTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// PlanÄ±n Ã¼retildiÄŸi UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Route sonucundan telemetry planÄ± Ã¼retir.
    /// </summary>
    public static TelemetryRoutePlan FromRoute(
        CommunicationRouteResult route,
        TelemetryProfile profile,
        string profileReason)
    {
        return new TelemetryRoutePlan
        {
            MessageId = route.MessageId,
            MessageType = route.MessageType,
            TargetNodeId = route.TargetNodeId,
            CanRoute = route.CanRoute,
            Profile = profile,
            ProfileReason = profileReason,
            RouteReason = route.Reason,
            PrimaryTransports = route.PrimaryTransports,
            FallbackTransports = route.FallbackTransports
        };
    }
}
