namespace Hydronom.GroundStation.FleetRegistry;

using Hydronom.Core.Fleet;

/// <summary>
/// Yer istasyonunun filo içindeki araçları/node'ları takip ettiği ana kayıt defteridir.
/// 
/// FleetRegistry, Hydronom Ground Station tarafının ilk temel modülüdür.
/// Görevi:
/// - Araçlardan gelen heartbeat mesajlarını almak,
/// - Araçların son bilinen durumunu saklamak,
/// - Hangi araç online/offline takip etmek,
/// - Araçları NodeId üzerinden bulmak,
/// - Hydronom Ops / Gateway tarafına güncel filo görünümü sağlamaktır.
/// 
/// Bu sınıf şu anda bilinçli olarak basit tutulmuştur.
/// İlk hedef:
/// Birden fazla Hydronom aracını yer istasyonunda kayıtlı ve izlenebilir hale getirmek.
/// </summary>
public sealed class FleetRegistry
{
    /// <summary>
    /// NodeId -> VehicleNodeStatus eşlemesini tutar.
    /// 
    /// Key:
    /// - VEHICLE-ALPHA-001
    /// - VEHICLE-BETA-001
    /// - SIM-VEHICLE-001
    /// 
    /// Value:
    /// - Aracın son bilinen Fleet status bilgisi.
    /// </summary>
    private readonly Dictionary<string, VehicleNodeStatus> _nodes = new();

    /// <summary>
    /// Registry erişimlerini thread-safe tutmak için kullanılan lock objesi.
    /// 
    /// GroundStation ileride aynı anda:
    /// - Transport reader,
    /// - Gateway API,
    /// - Ops WebSocket publisher,
    /// - Analysis engine
    /// gibi farklı katmanlardan erişim alabilir.
    /// 
    /// İlk sürüm için basit lock yeterlidir.
    /// İleride ConcurrentDictionary veya daha gelişmiş state store kullanılabilir.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// Registry içinde kayıtlı toplam node sayısını döndürür.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _nodes.Count;
            }
        }
    }

    /// <summary>
    /// Bir heartbeat mesajını registry'ye işler.
    /// 
    /// Heartbeat geçerliyse:
    /// - Heartbeat VehicleNodeStatus modeline dönüştürülür.
    /// - NodeId üzerinden registry'ye eklenir veya mevcut kayıt güncellenir.
    /// 
    /// Geçersiz heartbeat gelirse false döner.
    /// </summary>
    public bool ApplyHeartbeat(FleetHeartbeat heartbeat)
    {
        if (heartbeat is null || !heartbeat.IsValid)
            return false;

        return Upsert(heartbeat.ToStatus());
    }

    /// <summary>
    /// Bir VehicleNodeStatus kaydını registry'ye ekler veya mevcut kaydı günceller.
    /// 
    /// Upsert:
    /// - Kayıt yoksa ekle,
    /// - Kayıt varsa güncelle
    /// anlamına gelir.
    /// 
    /// Bu metot ileride sadece heartbeat değil,
    /// CapabilityAnnouncement veya FleetStatus gibi mesajlardan da beslenebilir.
    /// </summary>
    public bool Upsert(VehicleNodeStatus status)
    {
        if (status is null || !status.IsValid)
            return false;

        var nodeId = status.Identity.NodeId;

        lock (_sync)
        {
            _nodes[nodeId] = status;
        }

        return true;
    }

    /// <summary>
    /// NodeId ile kayıtlı bir node durumunu bulmaya çalışır.
    /// 
    /// Başarılıysa true döner ve status dışarı verilir.
    /// Bulunamazsa false döner.
    /// </summary>
    public bool TryGet(string nodeId, out VehicleNodeStatus? status)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            status = null;
            return false;
        }

        lock (_sync)
        {
            return _nodes.TryGetValue(nodeId, out status);
        }
    }

    /// <summary>
    /// Registry içindeki tüm node durumlarının snapshot kopyasını döndürür.
    /// 
    /// Snapshot kopyası dönmemizin sebebi:
    /// - Dış katmanların internal dictionary üzerinde değişiklik yapmasını engellemek,
    /// - Lock süresini kısa tutmak,
    /// - Gateway/Ops tarafına güvenli veri vermektir.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetSnapshot()
    {
        lock (_sync)
        {
            return _nodes.Values
                .OrderBy(x => x.Identity.DisplayName)
                .ThenBy(x => x.Identity.NodeId)
                .ToArray();
        }
    }

    /// <summary>
    /// Online kabul edilen node'ların snapshot listesini döndürür.
    /// 
    /// Bu metot sadece VehicleNodeStatus.IsOnline alanına bakar.
    /// Zaman aşımı kontrolü için MarkStaleNodesOffline metodu kullanılmalıdır.
    /// </summary>
    public IReadOnlyList<VehicleNodeStatus> GetOnlineNodes()
    {
        lock (_sync)
        {
            return _nodes.Values
                .Where(x => x.IsOnline)
                .OrderBy(x => x.Identity.DisplayName)
                .ThenBy(x => x.Identity.NodeId)
                .ToArray();
        }
    }

    /// <summary>
    /// Belirtilen süre boyunca heartbeat göndermeyen node'ları offline olarak işaretler.
    /// 
    /// Örnek:
    /// timeout = TimeSpan.FromSeconds(5)
    /// 
    /// Eğer bir araç 5 saniyeden uzun süredir görülmediyse IsOnline=false yapılır.
    /// 
    /// Bu metot bağlantı kopmasını anlamak için GroundStation ana döngüsü
    /// veya watchdog tarafından periyodik çağrılabilir.
    /// </summary>
    public int MarkStaleNodesOffline(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var changed = 0;

        lock (_sync)
        {
            foreach (var pair in _nodes.ToArray())
            {
                var status = pair.Value;
                var age = now - status.LastSeenUtc;

                if (status.IsOnline && age > timeout)
                {
                    _nodes[pair.Key] = status with
                    {
                        IsOnline = false
                    };

                    changed++;
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Registry'den bir node kaydını siler.
    /// 
    /// Kullanım alanları:
    /// - Simülasyon node'u kaldırma,
    /// - Test temizliği,
    /// - Operasyondan çıkan aracı listeden alma.
    /// </summary>
    public bool Remove(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        lock (_sync)
        {
            return _nodes.Remove(nodeId);
        }
    }

    /// <summary>
    /// Tüm registry kayıtlarını temizler.
    /// 
    /// Genellikle test, replay reset veya yeni operasyon başlatma sırasında kullanılır.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _nodes.Clear();
        }
    }
}