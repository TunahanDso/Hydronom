namespace Hydronom.GroundStation.FleetRegistry;

using Hydronom.Core.Fleet;

/// <summary>
/// Yer istasyonunun filo iÃ§indeki araÃ§larÄ±/node'larÄ± takip ettiÄŸi ana kayÄ±t defteridir.
/// 
/// FleetRegistry, Hydronom Ground Station tarafÄ±nÄ±n ilk temel modÃ¼lÃ¼dÃ¼r.
/// GÃ¶revi:
/// - AraÃ§lardan gelen heartbeat mesajlarÄ±nÄ± almak,
/// - AraÃ§larÄ±n son bilinen durumunu saklamak,
/// - Hangi araÃ§ online/offline takip etmek,
/// - AraÃ§larÄ± NodeId Ã¼zerinden bulmak,
/// - Hydronom Ops / Gateway tarafÄ±na gÃ¼ncel filo gÃ¶rÃ¼nÃ¼mÃ¼ saÄŸlamaktÄ±r.
/// 
/// Bu sÄ±nÄ±f ÅŸu anda bilinÃ§li olarak basit tutulmuÅŸtur.
/// Ä°lk hedef:
/// Birden fazla Hydronom aracÄ±nÄ± yer istasyonunda kayÄ±tlÄ± ve izlenebilir hale getirmek.
/// </summary>
public sealed class FleetRegistry
{
    /// <summary>
    /// NodeId -> VehicleNodeStatus eÅŸlemesini tutar.
    /// 
    /// Key:
    /// - VEHICLE-ALPHA-001
    /// - VEHICLE-BETA-001
    /// - SIM-VEHICLE-001
    /// 
    /// Value:
    /// - AracÄ±n son bilinen Fleet status bilgisi.
    /// </summary>
    private readonly Dictionary<string, VehicleNodeStatus> _nodes = new();

    /// <summary>
    /// Registry eriÅŸimlerini thread-safe tutmak iÃ§in kullanÄ±lan lock objesi.
    /// 
    /// GroundStation ileride aynÄ± anda:
    /// - Transport reader,
    /// - Gateway API,
    /// - Ops WebSocket publisher,
    /// - Analysis engine
    /// gibi farklÄ± katmanlardan eriÅŸim alabilir.
    /// 
    /// Ä°lk sÃ¼rÃ¼m iÃ§in basit lock yeterlidir.
    /// Ä°leride ConcurrentDictionary veya daha geliÅŸmiÅŸ state store kullanÄ±labilir.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// Registry iÃ§inde kayÄ±tlÄ± toplam node sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// Bir heartbeat mesajÄ±nÄ± registry'ye iÅŸler.
    /// 
    /// Heartbeat geÃ§erliyse:
    /// - Heartbeat VehicleNodeStatus modeline dÃ¶nÃ¼ÅŸtÃ¼rÃ¼lÃ¼r.
    /// - NodeId Ã¼zerinden registry'ye eklenir veya mevcut kayÄ±t gÃ¼ncellenir.
    /// 
    /// GeÃ§ersiz heartbeat gelirse false dÃ¶ner.
    /// </summary>
    public bool ApplyHeartbeat(FleetHeartbeat heartbeat)
    {
        if (heartbeat is null || !heartbeat.IsValid)
            return false;

        return Upsert(heartbeat.ToStatus());
    }

    /// <summary>
    /// Bir VehicleNodeStatus kaydÄ±nÄ± registry'ye ekler veya mevcut kaydÄ± gÃ¼nceller.
    /// 
    /// Upsert:
    /// - KayÄ±t yoksa ekle,
    /// - KayÄ±t varsa gÃ¼ncelle
    /// anlamÄ±na gelir.
    /// 
    /// Bu metot ileride sadece heartbeat deÄŸil,
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
    /// NodeId ile kayÄ±tlÄ± bir node durumunu bulmaya Ã§alÄ±ÅŸÄ±r.
    /// 
    /// BaÅŸarÄ±lÄ±ysa true dÃ¶ner ve status dÄ±ÅŸarÄ± verilir.
    /// Bulunamazsa false dÃ¶ner.
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
    /// Registry iÃ§indeki tÃ¼m node durumlarÄ±nÄ±n snapshot kopyasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// Snapshot kopyasÄ± dÃ¶nmemizin sebebi:
    /// - DÄ±ÅŸ katmanlarÄ±n internal dictionary Ã¼zerinde deÄŸiÅŸiklik yapmasÄ±nÄ± engellemek,
    /// - Lock sÃ¼resini kÄ±sa tutmak,
    /// - Gateway/Ops tarafÄ±na gÃ¼venli veri vermektir.
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
    /// Online kabul edilen node'larÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// Bu metot sadece VehicleNodeStatus.IsOnline alanÄ±na bakar.
    /// Zaman aÅŸÄ±mÄ± kontrolÃ¼ iÃ§in MarkStaleNodesOffline metodu kullanÄ±lmalÄ±dÄ±r.
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
    /// Belirtilen sÃ¼re boyunca heartbeat gÃ¶ndermeyen node'larÄ± offline olarak iÅŸaretler.
    /// 
    /// Ã–rnek:
    /// timeout = TimeSpan.FromSeconds(5)
    /// 
    /// EÄŸer bir araÃ§ 5 saniyeden uzun sÃ¼redir gÃ¶rÃ¼lmediyse IsOnline=false yapÄ±lÄ±r.
    /// 
    /// Bu metot baÄŸlantÄ± kopmasÄ±nÄ± anlamak iÃ§in GroundStation ana dÃ¶ngÃ¼sÃ¼
    /// veya watchdog tarafÄ±ndan periyodik Ã§aÄŸrÄ±labilir.
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
    /// Registry'den bir node kaydÄ±nÄ± siler.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - SimÃ¼lasyon node'u kaldÄ±rma,
    /// - Test temizliÄŸi,
    /// - Operasyondan Ã§Ä±kan aracÄ± listeden alma.
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
    /// TÃ¼m registry kayÄ±tlarÄ±nÄ± temizler.
    /// 
    /// Genellikle test, replay reset veya yeni operasyon baÅŸlatma sÄ±rasÄ±nda kullanÄ±lÄ±r.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _nodes.Clear();
        }
    }
}
