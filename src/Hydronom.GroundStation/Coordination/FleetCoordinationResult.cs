namespace Hydronom.GroundStation.Coordination;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// FleetCoordinator tarafÄ±ndan Ã¼retilen koordinasyon sonucunu temsil eder.
/// 
/// MissionAllocator sadece ÅŸu soruya cevap verir:
/// - Bu gÃ¶reve en uygun araÃ§ hangisi?
/// 
/// FleetCoordinator ise bir adÄ±m daha ileri gider:
/// - GÃ¶rev atanabildi mi?
/// - Hangi araÃ§ seÃ§ildi?
/// - SeÃ§ilen araca gÃ¶nderilecek FleetCommand Ã¼retildi mi?
/// - Bu komut HydronomEnvelope iÃ§ine sarÄ±ldÄ± mÄ±?
/// - OperatÃ¶r/Gateway/CommunicationRouter bu sonucu kullanabilir mi?
/// 
/// Bu model, gÃ¶rev atama kararÄ±nÄ± komut Ã¼retimiyle birleÅŸtiren ilk koordinasyon Ã§Ä±ktÄ±sÄ±dÄ±r.
/// </summary>
public sealed record FleetCoordinationResult
{
    /// <summary>
    /// Ä°lgili gÃ¶rev atama isteÄŸi.
    /// 
    /// Bu alan, koordinasyon sonucunun hangi gÃ¶rev isteÄŸinden Ã¼retildiÄŸini takip etmeyi saÄŸlar.
    /// </summary>
    public MissionRequest? Request { get; init; }

    /// <summary>
    /// MissionAllocator tarafÄ±ndan Ã¼retilen atama sonucu.
    /// 
    /// Bu sonuÃ§:
    /// - Hangi aracÄ±n seÃ§ildiÄŸini,
    /// - AdaylarÄ±,
    /// - Ret sebeplerini,
    /// - Skoru
    /// iÃ§erir.
    /// </summary>
    public MissionAllocationResult? Allocation { get; init; }

    /// <summary>
    /// Koordinasyon iÅŸlemi baÅŸarÄ±lÄ± mÄ±?
    /// 
    /// true ise:
    /// - GÃ¶rev iÃ§in uygun araÃ§ bulunmuÅŸtur.
    /// - FleetCommand Ã¼retilmiÅŸtir.
    /// - Envelope Ã¼retilmiÅŸtir.
    /// 
    /// false ise:
    /// - GÃ¶rev isteÄŸi geÃ§ersiz olabilir.
    /// - Uygun araÃ§ bulunamamÄ±ÅŸ olabilir.
    /// - Komut Ã¼retimi baÅŸarÄ±sÄ±z olmuÅŸ olabilir.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// BaÅŸarÄ± veya baÅŸarÄ±sÄ±zlÄ±k sebebinin kÄ±sa aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops Ã¼zerinde operatÃ¶re gÃ¶sterilebilir.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// SeÃ§ilen araca gÃ¶nderilmek Ã¼zere Ã¼retilen FleetCommand.
    /// 
    /// Success false ise null olabilir.
    /// </summary>
    public FleetCommand? Command { get; init; }

    /// <summary>
    /// Ãœretilen FleetCommand'Ä±n HydronomEnvelope iÃ§ine sarÄ±lmÄ±ÅŸ hÃ¢li.
    /// 
    /// CommunicationRouter ileride bu envelope'u alÄ±p uygun transport Ã¼zerinden gÃ¶nderecektir.
    /// </summary>
    public HydronomEnvelope? Envelope { get; init; }

    /// <summary>
    /// Koordinasyon sonucunun Ã¼retildiÄŸi UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// BaÅŸarÄ±sÄ±z koordinasyon sonucu Ã¼retir.
    /// </summary>
    public static FleetCoordinationResult Failed(
        MissionRequest? request,
        MissionAllocationResult? allocation,
        string reason)
    {
        return new FleetCoordinationResult
        {
            Request = request,
            Allocation = allocation,
            Success = false,
            Reason = reason
        };
    }

    /// <summary>
    /// BaÅŸarÄ±lÄ± koordinasyon sonucu Ã¼retir.
    /// </summary>
    public static FleetCoordinationResult Succeeded(
        MissionRequest request,
        MissionAllocationResult allocation,
        FleetCommand command,
        HydronomEnvelope envelope,
        string reason)
    {
        return new FleetCoordinationResult
        {
            Request = request,
            Allocation = allocation,
            Success = true,
            Reason = reason,
            Command = command,
            Envelope = envelope
        };
    }
}
