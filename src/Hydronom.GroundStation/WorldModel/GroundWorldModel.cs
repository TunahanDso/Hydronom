namespace Hydronom.GroundStation.WorldModel;

/// <summary>
/// Ground Station tarafındaki ortak dünya modelini temsil eder.
/// 
/// Bu sınıf, farklı araçlardan ve yer istasyonundan gelen dünya bilgilerini
/// merkezi bir modelde saklar.
/// 
/// İlk fazda amaç:
/// - Engel eklemek/güncellemek,
/// - Hedef eklemek/güncellemek,
/// - No-go zone veya mission area gibi nesneleri tutmak,
/// - Nesneleri türüne göre listelemek,
/// - Eski nesneleri pasifleştirmek,
/// - Hydronom Ops ve ileride TelemetryFusionEngine için ortak veri kaynağı sağlamaktır.
/// 
/// Bu sınıf henüz karmaşık geometri/fusion algoritması yapmaz.
/// Şimdilik güvenli, thread-safe, basit bir store olarak tasarlanmıştır.
/// </summary>
public sealed class GroundWorldModel
{
    /// <summary>
    /// ObjectId -> GroundWorldObject eşlemesini tutar.
    /// </summary>
    private readonly Dictionary<string, GroundWorldObject> _objects = new();

    /// <summary>
    /// World model erişimlerini thread-safe tutmak için kullanılan lock objesi.
    /// 
    /// İleride aynı anda:
    /// - TelemetryFusionEngine,
    /// - GroundAnalysisEngine,
    /// - Hydronom Ops Gateway,
    /// - ReplayRecorder,
    /// - Operator map tools
    /// bu modele erişebilir.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// Kayıtlı toplam dünya nesnesi sayısını döndürür.
    /// Aktif/pasif ayrımı yapmaz.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _objects.Count;
            }
        }
    }

    /// <summary>
    /// Aktif dünya nesnesi sayısını döndürür.
    /// </summary>
    public int ActiveCount
    {
        get
        {
            lock (_sync)
            {
                return _objects.Values.Count(x => x.IsActive);
            }
        }
    }

    /// <summary>
    /// Bir dünya nesnesini ekler veya aynı ObjectId varsa günceller.
    /// 
    /// Geçersiz nesne gelirse false döner.
    /// </summary>
    public bool Upsert(GroundWorldObject worldObject)
    {
        if (worldObject is null || !worldObject.IsValid)
            return false;

        lock (_sync)
        {
            _objects[worldObject.ObjectId] = worldObject with
            {
                UpdatedUtc = DateTimeOffset.UtcNow
            };
        }

        return true;
    }

    /// <summary>
    /// Bir dünya nesnesini ObjectId ile bulmaya çalışır.
    /// </summary>
    public bool TryGet(string objectId, out GroundWorldObject? worldObject)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            worldObject = null;
            return false;
        }

        lock (_sync)
        {
            return _objects.TryGetValue(objectId, out worldObject);
        }
    }

    /// <summary>
    /// Mevcut bir dünya nesnesine yeni kaynak katkısı ekler.
    /// 
    /// Örnek:
    /// Alpha bir engel bildirdi.
    /// Beta aynı ObjectId için katkı verdi.
    /// Bu durumda ContributorNodeIds listesi Alpha + Beta olacak şekilde güncellenir.
    /// </summary>
    public bool AddContribution(string objectId, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(nodeId))
            return false;

        lock (_sync)
        {
            if (!_objects.TryGetValue(objectId, out var existing))
                return false;

            _objects[objectId] = existing.WithContribution(nodeId);
            return true;
        }
    }

    /// <summary>
    /// Tüm dünya nesnelerinin snapshot kopyasını döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetSnapshot()
    {
        lock (_sync)
        {
            return _objects.Values
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.ObjectId)
                .ToArray();
        }
    }

    /// <summary>
    /// Sadece aktif dünya nesnelerinin snapshot kopyasını döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveSnapshot()
    {
        lock (_sync)
        {
            return _objects.Values
                .Where(x => x.IsActive)
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.ObjectId)
                .ToArray();
        }
    }

    /// <summary>
    /// Belirli türdeki dünya nesnelerinin snapshot kopyasını döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetByKind(WorldObjectKind kind, bool onlyActive = true)
    {
        lock (_sync)
        {
            var query = _objects.Values
                .Where(x => x.Kind == kind);

            if (onlyActive)
                query = query.Where(x => x.IsActive);

            return query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ObjectId)
                .ToArray();
        }
    }

    /// <summary>
    /// Aktif engellerin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveObstacles()
    {
        return GetByKind(WorldObjectKind.Obstacle);
    }

    /// <summary>
    /// Aktif hedeflerin snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveTargets()
    {
        return GetByKind(WorldObjectKind.Target);
    }

    /// <summary>
    /// Aktif no-go zone listesini döndürür.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveNoGoZones()
    {
        return GetByKind(WorldObjectKind.NoGoZone);
    }

    /// <summary>
    /// Bir dünya nesnesini pasif hâle getirir.
    /// 
    /// Nesne silinmez; sadece IsActive=false yapılır.
    /// Böylece replay/event timeline/after-action analysis için geçmiş korunabilir.
    /// </summary>
    public bool Deactivate(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return false;

        lock (_sync)
        {
            if (!_objects.TryGetValue(objectId, out var existing))
                return false;

            _objects[objectId] = existing with
            {
                IsActive = false,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            return true;
        }
    }

    /// <summary>
    /// ObjectId ile dünya nesnesini tamamen siler.
    /// 
    /// Genellikle test/cleanup için kullanılmalıdır.
    /// Operasyon geçmişinde kalması gereken nesnelerde Deactivate tercih edilmelidir.
    /// </summary>
    public bool Remove(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return false;

        lock (_sync)
        {
            return _objects.Remove(objectId);
        }
    }

    /// <summary>
    /// Belirli süreden uzun süredir güncellenmeyen aktif nesneleri pasif hale getirir.
    /// 
    /// Örnek:
    /// - Geçici engel 30 saniye boyunca tekrar görülmediyse pasifleştirilebilir.
    /// - Target kaybolduysa eski sayılabilir.
    /// 
    /// Bu metot özellikle dinamik obstacle/target bilgileri için faydalıdır.
    /// </summary>
    public int DeactivateStaleObjects(TimeSpan maxAge, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var changed = 0;

        lock (_sync)
        {
            foreach (var pair in _objects.ToArray())
            {
                var worldObject = pair.Value;

                if (!worldObject.IsActive)
                    continue;

                var age = now - worldObject.UpdatedUtc;

                if (age <= maxAge)
                    continue;

                _objects[pair.Key] = worldObject with
                {
                    IsActive = false,
                    UpdatedUtc = DateTimeOffset.UtcNow
                };

                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Tüm dünya modelini temizler.
    /// 
    /// Test, replay reset veya yeni operasyon başlatma sırasında kullanılabilir.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _objects.Clear();
        }
    }
}