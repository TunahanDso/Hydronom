namespace Hydronom.GroundStation;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.Routing;
using FleetRegistryStore = Hydronom.GroundStation.FleetRegistry.FleetRegistry;

/// <summary>
/// Hydronom Ground Station tarafının ana giriş sınıfıdır.
/// 
/// Bu sınıf, yer istasyonunun ana koordinasyon kabuğudur.
/// Amacı:
/// - FleetRegistry'yi tek merkezden yönetmek,
/// - CommandTracker ile gönderilen komutları ve sonuçlarını takip etmek,
/// - Gelen HydronomEnvelope mesajlarını dispatcher üzerinden yorumlamak,
/// - Heartbeat mesajlarını registry'ye işlemek,
/// - Komut sonuçlarını izlenebilir hale getirmek,
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
    /// Yer istasyonu tarafından gönderilen komutları ve araçlardan dönen sonuçları takip eder.
    /// 
    /// Bu yapı ileride Hydronom Ops tarafındaki:
    /// - Command History
    /// - Operator Timeline
    /// - Safety Rejection Log
    /// - Mission Command Audit
    /// ekranlarının temel veri kaynağı olacaktır.
    /// </summary>
    public CommandTracker CommandTracker { get; } = new();

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
    /// Dispatcher burada:
    /// - FleetHeartbeat mesajlarını FleetRegistry.ApplyHeartbeat metoduna,
    /// - FleetCommandResult mesajlarını CommandTracker.ApplyResult metoduna
    /// bağlar.
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
    /// Yer istasyonu tarafından üretilecek/gönderilecek bir komutu kayıt altına alır
    /// ve aynı komutu HydronomEnvelope içine sararak döndürür.
    /// 
    /// Bu metot komutu henüz fiziksel olarak göndermez.
    /// Sadece:
    /// - CommandTracker içine kaydeder,
    /// - HydronomEnvelopeFactory ile envelope üretir.
    /// 
    /// Gerçek gönderim ileride CommunicationRouter / TransportManager üzerinden yapılacaktır.
    /// </summary>
    public HydronomEnvelope? CreateTrackedCommandEnvelope(FleetCommand command)
    {
        if (command is null || !command.IsValid)
            return null;

        var tracked = CommandTracker.TrackCommand(command);

        if (!tracked)
            return null;

        return HydronomEnvelopeFactory.CreateCommand(command);
    }

    /// <summary>
    /// FleetCommandResult mesajlarını işler.
    /// 
    /// Gelen result, CommandTracker içinde daha önce kaydedilmiş komutla eşleştirilir.
    /// Böylece Ground Station:
    /// - Komut kabul edildi mi?
    /// - SafetyGate engelledi mi?
    /// - Komut uygulandı mı?
    /// - Komut başarısız mı oldu?
    /// sorularını takip edebilir.
    /// </summary>
    private bool HandleCommandResult(FleetCommandResult result)
    {
        if (result is null || !result.IsValid)
            return false;

        return CommandTracker.ApplyResult(result);
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
    /// Kayıtlı tüm komut geçmişinin snapshot listesini döndürür.
    /// 
    /// Hydronom Ops ileride bu metotla komut geçmişi ekranını besleyebilir.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetCommandHistorySnapshot()
    {
        return CommandTracker.GetSnapshot();
    }

    /// <summary>
    /// Henüz sonuç bekleyen komutların snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetPendingCommandSnapshot()
    {
        return CommandTracker.GetPendingCommands();
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

    /// <summary>
    /// Belirli süre boyunca sonuç dönmeyen pending komutları expired olarak işaretler.
    /// 
    /// Bu metot ileride GroundStation watchdog veya timer tarafından çağrılabilir.
    /// </summary>
    public int MarkExpiredCommands(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        return CommandTracker.MarkExpiredCommands(timeout, nowUtc);
    }
}