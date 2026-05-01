namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafında gelen HydronomEnvelope mesajlarını
/// mesaj tipine göre ilgili handler'a yönlendiren küçük dispatcher sınıfıdır.
/// 
/// Bu sınıfın amacı:
/// - GroundStationEngine içinde büyüyen if/switch karmaşasını engellemek,
/// - Mesaj işleme mantığını merkezi hale getirmek,
/// - İleride FleetHeartbeat, FleetCommandResult, TelemetryFrame,
///   CapabilityAnnouncement, LinkQualityReport gibi mesajları daha temiz yönetmektir.
/// 
/// Şu an ilk fazda sadece FleetHeartbeat desteklenir.
/// </summary>
public sealed class GroundMessageDispatcher
{
    /// <summary>
    /// FleetHeartbeat mesajı geldiğinde çalıştırılacak handler.
    /// 
    /// GroundStationEngine bu handler'ı FleetRegistry.ApplyHeartbeat'e bağlayabilir.
    /// Böylece dispatcher registry'yi doğrudan bilmez; sadece mesajı yönlendirir.
    /// </summary>
    private readonly Func<FleetHeartbeat, bool> _onHeartbeat;

    /// <summary>
    /// FleetCommandResult mesajı geldiğinde çalıştırılabilecek handler.
    /// 
    /// Şimdilik opsiyonel bırakılmıştır.
    /// İleride komut sonucu takibi, operatör paneli ve event timeline için kullanılacak.
    /// </summary>
    private readonly Func<FleetCommandResult, bool>? _onCommandResult;

    /// <summary>
    /// GroundMessageDispatcher oluşturur.
    /// 
    /// İlk zorunlu handler FleetHeartbeat içindir.
    /// Çünkü FleetRegistry'nin güncel kalması için heartbeat temel mesajdır.
    /// </summary>
    public GroundMessageDispatcher(
        Func<FleetHeartbeat, bool> onHeartbeat,
        Func<FleetCommandResult, bool>? onCommandResult = null)
    {
        _onHeartbeat = onHeartbeat ?? throw new ArgumentNullException(nameof(onHeartbeat));
        _onCommandResult = onCommandResult;
    }

    /// <summary>
    /// Gelen envelope'u MessageType alanına göre ilgili işleyiciye yönlendirir.
    /// 
    /// Dönüş:
    /// - true: mesaj tanındı ve başarıyla işlendi
    /// - false: mesaj tanınmadı, payload uyumsuzdu veya handler başarısız oldu
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
    /// Payload içinden FleetHeartbeat modelini çıkarır ve heartbeat handler'ına yollar.
    /// 
    /// Not:
    /// Şu an aynı proses içinde object payload taşıdığımız için doğrudan cast yeterli.
    /// Gerçek transport/JSON aşamasında payload deserialize katmanı eklenecek.
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
    /// Payload içinden FleetCommandResult modelini çıkarır ve varsa command result handler'ına yollar.
    /// 
    /// Şimdilik handler verilmemişse false döner.
    /// İleride GroundStation komut geçmişi tuttuğunda bu aktifleşecek.
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