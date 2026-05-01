namespace Hydronom.GroundStation;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Routing;
using FleetRegistryStore = Hydronom.GroundStation.FleetRegistry.FleetRegistry;

/// <summary>
/// Hydronom Ground Station tarafının ana giriş sınıfıdır.
/// 
/// Bu sınıf, yer istasyonunun ana koordinasyon kabuğudur.
/// Amacı:
/// - FleetRegistry'yi tek merkezden yönetmek,
/// - Gelen HydronomEnvelope mesajlarını dispatcher üzerinden yorumlamak,
/// - Heartbeat mesajlarını registry'ye işlemek,
/// - Komut sonuçlarını ileride izlenebilir hale getirmek,
/// - Yer istasyonu tarafında büyüyecek modüller için ana koordinasyon noktası olmaktır.
/// 
/// İleride bu sınıfın altına şunlar bağlanabilir:
/// - FleetCoordinator
/// - MissionPlanner
/// - MissionAllocator
/// - CommunicationRouter
/// - TelemetryFusionEngine
/// - GroundAnalysisEngine
/// - ReplayRecorder
/// - GroundWorldModel
/// </summary>
public sealed class GroundStationEngine
{
    /// <summary>
    /// Yer istasyonunun araç/node kayıt defteri.
    /// 
    /// Araçlardan gelen heartbeat/status mesajları bu registry'yi günceller.
    /// Hydronom Ops veya Gateway tarafı güncel filo görünümünü buradan alabilir.
    /// </summary>
    public FleetRegistryStore FleetRegistry { get; } = new();

    /// <summary>
    /// Ground Station tarafında gelen mesajları MessageType değerine göre
    /// ilgili handler'a yönlendiren dispatcher.
    /// 
    /// Bu yapı sayesinde GroundStationEngine içinde sürekli büyüyen
    /// if/switch blokları oluşmaz.
    /// </summary>
    public GroundMessageDispatcher Dispatcher { get; }

    /// <summary>
    /// Yer istasyonunun kendi node kimliği.
    /// 
    /// Varsayılan olarak GROUND-001 kullanıyoruz.
    /// İleride config üzerinden değiştirilebilir.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new()
    {
        NodeId = "GROUND-001",
        DisplayName = "Hydronom Ground Station",
        NodeType = "GroundStation",
        Role = "Coordinator"
    };

    /// <summary>
    /// GroundStationEngine oluşturur.
    /// 
    /// Dispatcher burada FleetRegistry.ApplyHeartbeat metoduna bağlanır.
    /// Böylece gelen FleetHeartbeat mesajları doğrudan registry'yi günceller.
    /// </summary>
    public GroundStationEngine()
    {
        Dispatcher = new GroundMessageDispatcher(
            onHeartbeat: FleetRegistry.ApplyHeartbeat,
            onCommandResult: HandleCommandResult);
    }

    /// <summary>
    /// Gelen HydronomEnvelope mesajını işler.
    /// 
    /// Artık mesaj tipi ayrıştırma işi GroundMessageDispatcher tarafından yapılır.
    /// Bu sınıf yalnızca ana giriş noktası olarak davranır.
    /// </summary>
    public bool HandleEnvelope(HydronomEnvelope envelope)
    {
        return Dispatcher.Dispatch(envelope);
    }

    /// <summary>
    /// FleetCommandResult mesajlarını işler.
    /// 
    /// Şu an ilk fazda sadece geçerli sonucu başarıyla alınmış kabul ediyoruz.
    /// İleride burada:
    /// - Komut geçmişi,
    /// - Operatör event timeline,
    /// - Command ACK tracking,
    /// - Safety rejection kayıtları,
    /// - UI bildirimleri
    /// tutulacak.
    /// </summary>
    private bool HandleCommandResult(FleetCommandResult result)
    {
        return result is not null && result.IsValid;
    }

    /// <summary>
    /// Registry içindeki tüm araç/node durumlarının snapshot listesini döndürür.
    /// 
    /// Ops Gateway veya test kodu bu metotla güncel filo görünümünü alabilir.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetFleetSnapshot()
    {
        return FleetRegistry.GetSnapshot();
    }

    /// <summary>
    /// Online kabul edilen araç/node durumlarının snapshot listesini döndürür.
    /// 
    /// Bu metot ileride Hydronom Ops tarafında sadece aktif araçları göstermek için
    /// kullanılabilir.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetOnlineFleetSnapshot()
    {
        return FleetRegistry.GetOnlineNodes();
    }

    /// <summary>
    /// Belirli süre heartbeat göndermeyen araçları offline olarak işaretler.
    /// 
    /// Bu metot GroundStation ana döngüsü veya timer tarafından çağrılabilir.
    /// </summary>
    public int MarkStaleNodesOffline(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        return FleetRegistry.MarkStaleNodesOffline(timeout, nowUtc);
    }
}