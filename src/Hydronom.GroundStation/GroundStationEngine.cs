namespace Hydronom.GroundStation;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Commanding;
using Hydronom.GroundStation.Coordination;
using Hydronom.GroundStation.Routing;
using Hydronom.GroundStation.WorldModel;
using FleetRegistryStore = Hydronom.GroundStation.FleetRegistry.FleetRegistry;

/// <summary>
/// Hydronom Ground Station tarafının ana giriş sınıfıdır.
/// 
/// Bu sınıf, yer istasyonunun ana koordinasyon kabuğudur.
/// Amacı:
/// - FleetRegistry'yi tek merkezden yönetmek,
/// - CommandTracker ile gönderilen komutları ve sonuçlarını takip etmek,
/// - GroundWorldModel ile ortak operasyon dünyasını tutmak,
/// - MissionAllocator ile görev için uygun araç seçimini başlatmak,
/// - FleetCoordinator ile görev isteğinden komut üretmek,
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
    /// Yer istasyonunun ortak dünya modelidir.
    /// 
    /// Farklı araçlardan gelen obstacle, target, no-go zone, mission area,
    /// link quality ve event bilgileri burada tutulabilir.
    /// 
    /// Bu yapı ileride:
    /// - TelemetryFusionEngine,
    /// - GroundAnalysisEngine,
    /// - MissionPlanner,
    /// - Hydronom Ops map paneli
    /// için temel veri kaynağı olacaktır.
    /// </summary>
    public GroundWorldModel WorldModel { get; } = new();

    /// <summary>
    /// Görev isteklerini filo içindeki uygun araca atamaya çalışan ilk görev dağıtım modülüdür.
    /// 
    /// Bu yapı şu an:
    /// - Online araçları,
    /// - Araç tipini,
    /// - Zorunlu/tercih edilen kabiliyetleri,
    /// - Batarya ve health durumunu
    /// dikkate alarak basit bir skor üretir.
    /// </summary>
    public MissionAllocator MissionAllocator { get; } = new();

    /// <summary>
    /// Görev isteğini alıp uygun aracı seçen ve araca gönderilecek FleetCommand envelope üreten
    /// koordinasyon modülüdür.
    /// 
    /// Bu yapı MissionAllocator'ın bir üst katmanıdır:
    /// - MissionAllocator hangi aracın uygun olduğunu seçer.
    /// - FleetCoordinator bu kararı FleetCommand'a dönüştürür.
    /// </summary>
    public FleetCoordinator FleetCoordinator { get; }

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
    /// 
    /// FleetCoordinator ise aynı MissionAllocator örneğiyle oluşturulur.
    /// Böylece görev atama skorlama mantığı tek yerde kalır.
    /// </summary>
    public GroundStationEngine()
    {
        FleetCoordinator = new FleetCoordinator(MissionAllocator);

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
    /// Verilen görev isteği için mevcut fleet snapshot üzerinden en uygun aracı seçer.
    /// 
    /// Bu metot görevi fiziksel olarak araca göndermez.
    /// Sadece MissionAllocator üzerinden görev atama kararı üretir.
    /// 
    /// İleride bu karar:
    /// - FleetCommand üretimine,
    /// - Operatör onayına,
    /// - AI öneri katmanına,
    /// - MissionPlanner akışına
    /// bağlanabilir.
    /// </summary>
    public MissionAllocationResult AllocateMission(MissionRequest request)
    {
        return MissionAllocator.Allocate(
            request,
            FleetRegistry.GetSnapshot());
    }

    /// <summary>
    /// Verilen görev isteğini filo koordinasyon sonucuna çevirir.
    /// 
    /// Bu metot:
    /// - FleetCoordinator ile uygun aracı seçer,
    /// - AssignMission FleetCommand üretir,
    /// - Üretilen komutu CommandTracker'a kaydeder,
    /// - Tracked HydronomEnvelope döndürür.
    /// 
    /// Eğer koordinasyon başarılı fakat tracking başarısız olursa sonuç başarısız kabul edilir.
    /// </summary>
    public FleetCoordinationResult CoordinateMission(MissionRequest request)
    {
        var coordination = FleetCoordinator.CoordinateMission(
            request,
            FleetRegistry.GetSnapshot(),
            Identity.NodeId,
            isOperatorIssued: true);

        if (!coordination.Success || coordination.Command is null)
            return coordination;

        var trackedEnvelope = CreateTrackedCommandEnvelope(coordination.Command);

        if (trackedEnvelope is null)
        {
            return FleetCoordinationResult.Failed(
                request,
                coordination.Allocation,
                "Mission command was generated but could not be tracked by CommandTracker.");
        }

        return coordination with
        {
            Envelope = trackedEnvelope,
            Reason = $"{coordination.Reason} Command tracked by GroundStationEngine."
        };
    }

    /// <summary>
    /// GroundWorldModel içine yeni bir dünya nesnesi ekler veya mevcut nesneyi günceller.
    /// 
    /// Bu metot şu an doğrudan WorldModel.Upsert çağırır.
    /// İleride burada:
    /// - TelemetryFusionEngine ile nesne birleştirme,
    /// - confidence güncelleme,
    /// - kaynak doğrulama,
    /// - duplicate obstacle merge
    /// gibi işlemler eklenebilir.
    /// </summary>
    public bool UpsertWorldObject(GroundWorldObject worldObject)
    {
        return WorldModel.Upsert(worldObject);
    }

    /// <summary>
    /// GroundWorldModel içindeki tüm dünya nesnelerinin snapshot listesini döndürür.
    /// 
    /// Hydronom Ops map paneli, diagnostics ekranı veya test kodu
    /// bu metotla ortak dünya modelini okuyabilir.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetWorldSnapshot()
    {
        return WorldModel.GetSnapshot();
    }

    /// <summary>
    /// GroundWorldModel içindeki aktif dünya nesnelerinin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveWorldSnapshot()
    {
        return WorldModel.GetActiveSnapshot();
    }

    /// <summary>
    /// Aktif engellerin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveObstacles()
    {
        return WorldModel.GetActiveObstacles();
    }

    /// <summary>
    /// Aktif hedeflerin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveTargets()
    {
        return WorldModel.GetActiveTargets();
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

    /// <summary>
    /// Belirli süreden uzun süredir güncellenmeyen aktif dünya nesnelerini pasif hale getirir.
    /// 
    /// Özellikle geçici obstacle/target bilgileri için kullanılabilir.
    /// </summary>
    public int DeactivateStaleWorldObjects(TimeSpan maxAge, DateTimeOffset? nowUtc = null)
    {
        return WorldModel.DeactivateStaleObjects(maxAge, nowUtc);
    }
}