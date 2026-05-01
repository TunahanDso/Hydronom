namespace Hydronom.GroundStation.Telemetry;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;

/// <summary>
/// CommunicationRouter tarafından üretilen route sonucuna göre
/// seçilmiş telemetry planını temsil eder.
/// 
/// Bu model şu sorulara cevap verir:
/// - Mesaj/araç route edilebilir mi?
/// - Hangi telemetry profili seçildi?
/// - Hangi transport'lar üzerinden telemetry mantıklı?
/// - Bu karar neden verildi?
/// 
/// İlk fazda gerçek telemetry payload üretmez.
/// Sadece Ground Station tarafında telemetry yoğunluğu kararını route sonucuyla birleştirir.
/// </summary>
public sealed record TelemetryRoutePlan
{
    /// <summary>
    /// Route edilen mesaj kimliği.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Route edilen mesaj tipi.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node kimliği.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Route edilebilir mi?
    /// 
    /// false ise telemetry profili yine güvenli varsayılan olarak Light olabilir;
    /// fakat plan uygulanabilir kabul edilmez.
    /// </summary>
    public bool CanRoute { get; init; }

    /// <summary>
    /// Seçilen telemetry profili.
    /// 
    /// Örnek:
    /// - Light
    /// - Normal
    /// - Full
    /// </summary>
    public TelemetryProfile Profile { get; init; } = TelemetryProfile.Unknown;

    /// <summary>
    /// Profile kararının açıklaması.
    /// </summary>
    public string ProfileReason { get; init; } = string.Empty;

    /// <summary>
    /// Route kararının açıklaması.
    /// </summary>
    public string RouteReason { get; init; } = string.Empty;

    /// <summary>
    /// Telemetry için kullanılabilir birincil transport'lar.
    /// </summary>
    public IReadOnlyList<TransportKind> PrimaryTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Telemetry için kullanılabilir fallback transport'lar.
    /// </summary>
    public IReadOnlyList<TransportKind> FallbackTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Planın üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Route sonucundan telemetry planı üretir.
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