namespace Hydronom.GroundStation.WorldModel;

/// <summary>
/// GroundWorldModel içinde tutulan ortak dünya nesnesini temsil eder.
/// 
/// Bu model, farklı araçlardan veya yer istasyonundan gelen bilgileri
/// ortak bir dünya modelinde birleştirmek için kullanılır.
/// 
/// Örnekler:
/// - Alpha aracı bir engel görür.
/// - Beta aynı engeli başka açıdan doğrular.
/// - Yer istasyonu bu bilgiyi tek bir GroundWorldObject olarak saklar.
/// - Operatör haritaya no-go zone ekler.
/// - MissionPlanner görev alanı oluşturur.
/// 
/// İlk fazda model bilinçli olarak esnek tutulmuştur.
/// İleride geometri, güven skoru, kaynak sayısı, sınıflandırma ve zaman aşımı
/// mantıkları daha detaylı hâle getirilebilir.
/// </summary>
public sealed record GroundWorldObject
{
    /// <summary>
    /// Dünya nesnesinin benzersiz kimliği.
    /// 
    /// Örnek:
    /// - "OBS-001"
    /// - "TARGET-BUOY-01"
    /// - "NOGO-AREA-A"
    /// - "MISSION-AREA-SEARCH-1"
    /// 
    /// Varsayılan olarak GUID tabanlı üretilir.
    /// </summary>
    public string ObjectId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Nesnenin türü.
    /// 
    /// Örnek:
    /// - Vehicle
    /// - Obstacle
    /// - Target
    /// - NoGoZone
    /// - MissionArea
    /// - MapLayer
    /// - LinkQuality
    /// - Event
    /// </summary>
    public WorldObjectKind Kind { get; init; } = WorldObjectKind.Unknown;

    /// <summary>
    /// Nesnenin insan tarafından okunabilir adı.
    /// 
    /// Hydronom Ops üzerinde harita katmanı, tooltip veya liste ekranlarında gösterilebilir.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Nesneyi ilk bildiren kaynak node kimliği.
    /// 
    /// Örnek:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// 
    /// Bu alan, nesnenin ilk hangi kaynaktan geldiğini izlemek için kullanılır.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Nesneyi doğrulayan veya güncelleyen kaynak node kimlikleri.
    /// 
    /// Örnek:
    /// - Alpha engel gördü.
    /// - Beta aynı engeli doğruladı.
    /// - SourceNodeId Alpha kalabilir, ContributorNodeIds içinde Alpha ve Beta olabilir.
    /// 
    /// Bu alan multi-vehicle fusion için önemlidir.
    /// </summary>
    public IReadOnlyList<string> ContributorNodeIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Nesnenin enlem değeri.
    /// 
    /// Noktasal nesneler için kullanılır.
    /// Alan/poligon gibi nesnelerde merkez veya referans noktası olarak kullanılabilir.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Nesnenin boylam değeri.
    /// 
    /// Noktasal nesneler için kullanılır.
    /// Alan/poligon gibi nesnelerde merkez veya referans noktası olarak kullanılabilir.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Nesnenin yerel X konumu.
    /// 
    /// GPS olmayan simülasyon veya lokal harita koordinatları için kullanılabilir.
    /// Metre cinsinden düşünülür.
    /// </summary>
    public double? X { get; init; }

    /// <summary>
    /// Nesnenin yerel Y konumu.
    /// 
    /// GPS olmayan simülasyon veya lokal harita koordinatları için kullanılabilir.
    /// Metre cinsinden düşünülür.
    /// </summary>
    public double? Y { get; init; }

    /// <summary>
    /// Nesnenin tahmini yarıçapı veya etki alanı.
    /// 
    /// Örnek:
    /// - Engel yarıçapı
    /// - No-go zone yaklaşık yarıçapı
    /// - Link quality ölçüm alanı
    /// </summary>
    public double? RadiusMeters { get; init; }

    /// <summary>
    /// Nesnenin güven skoru.
    /// 
    /// 0.0 - 1.0 aralığında düşünülür.
    /// 
    /// Örnek:
    /// - Tek araç zayıf tespit yaptıysa 0.4
    /// - Birden fazla araç doğruladıysa 0.8+
    /// - Operatör elle eklediyse 1.0
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Nesnenin aktif olup olmadığını belirtir.
    /// 
    /// false ise:
    /// - Nesne eski olabilir.
    /// - Görev tamamlanmış olabilir.
    /// - Operatör nesneyi devre dışı bırakmış olabilir.
    /// - Fusion engine nesneyi artık geçerli görmüyor olabilir.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Nesnenin ilk oluşturulduğu UTC zaman.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Nesnenin son güncellendiği UTC zaman.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Nesneyle ilgili ek metadata alanı.
    /// 
    /// Örnek:
    /// - "sensor": "lidar"
    /// - "class": "buoy"
    /// - "severity": "high"
    /// - "sourceFrame": "fused"
    /// - "mapLayer": "occupancy"
    /// 
    /// İlk fazda esneklik sağlar.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Dünya nesnesinin temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// En azından ObjectId ve Kind anlamlı olmalıdır.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ObjectId) &&
        Kind != WorldObjectKind.Unknown;

    /// <summary>
    /// Nesnenin yeni bir kaynak node tarafından doğrulanmış/güncellenmiş hâlini döndürür.
    /// 
    /// Bu metot immutable record yapısını koruyarak yeni kopya üretir.
    /// </summary>
    public GroundWorldObject WithContribution(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return this;

        var contributors = ContributorNodeIds
            .Append(nodeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with
        {
            ContributorNodeIds = contributors,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
    }
}