namespace Hydronom.GroundStation.Coordination;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// FleetCoordinator tarafından üretilen koordinasyon sonucunu temsil eder.
/// 
/// MissionAllocator sadece şu soruya cevap verir:
/// - Bu göreve en uygun araç hangisi?
/// 
/// FleetCoordinator ise bir adım daha ileri gider:
/// - Görev atanabildi mi?
/// - Hangi araç seçildi?
/// - Seçilen araca gönderilecek FleetCommand üretildi mi?
/// - Bu komut HydronomEnvelope içine sarıldı mı?
/// - Operatör/Gateway/CommunicationRouter bu sonucu kullanabilir mi?
/// 
/// Bu model, görev atama kararını komut üretimiyle birleştiren ilk koordinasyon çıktısıdır.
/// </summary>
public sealed record FleetCoordinationResult
{
    /// <summary>
    /// İlgili görev atama isteği.
    /// 
    /// Bu alan, koordinasyon sonucunun hangi görev isteğinden üretildiğini takip etmeyi sağlar.
    /// </summary>
    public MissionRequest? Request { get; init; }

    /// <summary>
    /// MissionAllocator tarafından üretilen atama sonucu.
    /// 
    /// Bu sonuç:
    /// - Hangi aracın seçildiğini,
    /// - Adayları,
    /// - Ret sebeplerini,
    /// - Skoru
    /// içerir.
    /// </summary>
    public MissionAllocationResult? Allocation { get; init; }

    /// <summary>
    /// Koordinasyon işlemi başarılı mı?
    /// 
    /// true ise:
    /// - Görev için uygun araç bulunmuştur.
    /// - FleetCommand üretilmiştir.
    /// - Envelope üretilmiştir.
    /// 
    /// false ise:
    /// - Görev isteği geçersiz olabilir.
    /// - Uygun araç bulunamamış olabilir.
    /// - Komut üretimi başarısız olmuş olabilir.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Başarı veya başarısızlık sebebinin kısa açıklaması.
    /// 
    /// Hydronom Ops üzerinde operatöre gösterilebilir.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Seçilen araca gönderilmek üzere üretilen FleetCommand.
    /// 
    /// Success false ise null olabilir.
    /// </summary>
    public FleetCommand? Command { get; init; }

    /// <summary>
    /// Üretilen FleetCommand'ın HydronomEnvelope içine sarılmış hâli.
    /// 
    /// CommunicationRouter ileride bu envelope'u alıp uygun transport üzerinden gönderecektir.
    /// </summary>
    public HydronomEnvelope? Envelope { get; init; }

    /// <summary>
    /// Koordinasyon sonucunun üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Başarısız koordinasyon sonucu üretir.
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
    /// Başarılı koordinasyon sonucu üretir.
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