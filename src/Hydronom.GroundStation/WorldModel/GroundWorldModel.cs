namespace Hydronom.GroundStation.WorldModel;

/// <summary>
/// Ground Station tarafÄ±ndaki ortak dÃ¼nya modelini temsil eder.
/// 
/// Bu sÄ±nÄ±f, farklÄ± araÃ§lardan ve yer istasyonundan gelen dÃ¼nya bilgilerini
/// merkezi bir modelde saklar.
/// 
/// Ä°lk fazda amaÃ§:
/// - Engel eklemek/gÃ¼ncellemek,
/// - Hedef eklemek/gÃ¼ncellemek,
/// - No-go zone veya mission area gibi nesneleri tutmak,
/// - Nesneleri tÃ¼rÃ¼ne gÃ¶re listelemek,
/// - Eski nesneleri pasifleÅŸtirmek,
/// - Hydronom Ops ve ileride TelemetryFusionEngine iÃ§in ortak veri kaynaÄŸÄ± saÄŸlamaktÄ±r.
/// 
/// Bu sÄ±nÄ±f henÃ¼z karmaÅŸÄ±k geometri/fusion algoritmasÄ± yapmaz.
/// Åimdilik gÃ¼venli, thread-safe, basit bir store olarak tasarlanmÄ±ÅŸtÄ±r.
/// </summary>
public sealed class GroundWorldModel
{
    /// <summary>
    /// ObjectId -> GroundWorldObject eÅŸlemesini tutar.
    /// </summary>
    private readonly Dictionary<string, GroundWorldObject> _objects = new();

    /// <summary>
    /// World model eriÅŸimlerini thread-safe tutmak iÃ§in kullanÄ±lan lock objesi.
    /// 
    /// Ä°leride aynÄ± anda:
    /// - TelemetryFusionEngine,
    /// - GroundAnalysisEngine,
    /// - Hydronom Ops Gateway,
    /// - ReplayRecorder,
    /// - Operator map tools
    /// bu modele eriÅŸebilir.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// KayÄ±tlÄ± toplam dÃ¼nya nesnesi sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// Aktif/pasif ayrÄ±mÄ± yapmaz.
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
    /// Aktif dÃ¼nya nesnesi sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// Bir dÃ¼nya nesnesini ekler veya aynÄ± ObjectId varsa gÃ¼nceller.
    /// 
    /// GeÃ§ersiz nesne gelirse false dÃ¶ner.
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
    /// Bir dÃ¼nya nesnesini ObjectId ile bulmaya Ã§alÄ±ÅŸÄ±r.
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
    /// Mevcut bir dÃ¼nya nesnesine yeni kaynak katkÄ±sÄ± ekler.
    /// 
    /// Ã–rnek:
    /// Alpha bir engel bildirdi.
    /// Beta aynÄ± ObjectId iÃ§in katkÄ± verdi.
    /// Bu durumda ContributorNodeIds listesi Alpha + Beta olacak ÅŸekilde gÃ¼ncellenir.
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
    /// TÃ¼m dÃ¼nya nesnelerinin snapshot kopyasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// Sadece aktif dÃ¼nya nesnelerinin snapshot kopyasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// Belirli tÃ¼rdeki dÃ¼nya nesnelerinin snapshot kopyasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// Aktif engellerin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveObstacles()
    {
        return GetByKind(WorldObjectKind.Obstacle);
    }

    /// <summary>
    /// Aktif hedeflerin snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveTargets()
    {
        return GetByKind(WorldObjectKind.Target);
    }

    /// <summary>
    /// Aktif no-go zone listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundWorldObject> GetActiveNoGoZones()
    {
        return GetByKind(WorldObjectKind.NoGoZone);
    }

    /// <summary>
    /// Bir dÃ¼nya nesnesini pasif hÃ¢le getirir.
    /// 
    /// Nesne silinmez; sadece IsActive=false yapÄ±lÄ±r.
    /// BÃ¶ylece replay/event timeline/after-action analysis iÃ§in geÃ§miÅŸ korunabilir.
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
    /// ObjectId ile dÃ¼nya nesnesini tamamen siler.
    /// 
    /// Genellikle test/cleanup iÃ§in kullanÄ±lmalÄ±dÄ±r.
    /// Operasyon geÃ§miÅŸinde kalmasÄ± gereken nesnelerde Deactivate tercih edilmelidir.
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
    /// Belirli sÃ¼reden uzun sÃ¼redir gÃ¼ncellenmeyen aktif nesneleri pasif hale getirir.
    /// 
    /// Ã–rnek:
    /// - GeÃ§ici engel 30 saniye boyunca tekrar gÃ¶rÃ¼lmediyse pasifleÅŸtirilebilir.
    /// - Target kaybolduysa eski sayÄ±labilir.
    /// 
    /// Bu metot Ã¶zellikle dinamik obstacle/target bilgileri iÃ§in faydalÄ±dÄ±r.
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
    /// TÃ¼m dÃ¼nya modelini temizler.
    /// 
    /// Test, replay reset veya yeni operasyon baÅŸlatma sÄ±rasÄ±nda kullanÄ±labilir.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _objects.Clear();
        }
    }
}
