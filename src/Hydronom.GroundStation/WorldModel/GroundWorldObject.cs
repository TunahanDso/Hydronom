namespace Hydronom.GroundStation.WorldModel;

/// <summary>
/// GroundWorldModel iÃ§inde tutulan ortak dÃ¼nya nesnesini temsil eder.
/// 
/// Bu model, farklÄ± araÃ§lardan veya yer istasyonundan gelen bilgileri
/// ortak bir dÃ¼nya modelinde birleÅŸtirmek iÃ§in kullanÄ±lÄ±r.
/// 
/// Ã–rnekler:
/// - Alpha aracÄ± bir engel gÃ¶rÃ¼r.
/// - Beta aynÄ± engeli baÅŸka aÃ§Ä±dan doÄŸrular.
/// - Yer istasyonu bu bilgiyi tek bir GroundWorldObject olarak saklar.
/// - OperatÃ¶r haritaya no-go zone ekler.
/// - MissionPlanner gÃ¶rev alanÄ± oluÅŸturur.
/// 
/// Ä°lk fazda model bilinÃ§li olarak esnek tutulmuÅŸtur.
/// Ä°leride geometri, gÃ¼ven skoru, kaynak sayÄ±sÄ±, sÄ±nÄ±flandÄ±rma ve zaman aÅŸÄ±mÄ±
/// mantÄ±klarÄ± daha detaylÄ± hÃ¢le getirilebilir.
/// </summary>
public sealed record GroundWorldObject
{
    /// <summary>
    /// DÃ¼nya nesnesinin benzersiz kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - "OBS-001"
    /// - "TARGET-BUOY-01"
    /// - "NOGO-AREA-A"
    /// - "MISSION-AREA-SEARCH-1"
    /// 
    /// VarsayÄ±lan olarak GUID tabanlÄ± Ã¼retilir.
    /// </summary>
    public string ObjectId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Nesnenin tÃ¼rÃ¼.
    /// 
    /// Ã–rnek:
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
    /// Nesnenin insan tarafÄ±ndan okunabilir adÄ±.
    /// 
    /// Hydronom Ops Ã¼zerinde harita katmanÄ±, tooltip veya liste ekranlarÄ±nda gÃ¶sterilebilir.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Nesneyi ilk bildiren kaynak node kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// 
    /// Bu alan, nesnenin ilk hangi kaynaktan geldiÄŸini izlemek iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Nesneyi doÄŸrulayan veya gÃ¼ncelleyen kaynak node kimlikleri.
    /// 
    /// Ã–rnek:
    /// - Alpha engel gÃ¶rdÃ¼.
    /// - Beta aynÄ± engeli doÄŸruladÄ±.
    /// - SourceNodeId Alpha kalabilir, ContributorNodeIds iÃ§inde Alpha ve Beta olabilir.
    /// 
    /// Bu alan multi-vehicle fusion iÃ§in Ã¶nemlidir.
    /// </summary>
    public IReadOnlyList<string> ContributorNodeIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Nesnenin enlem deÄŸeri.
    /// 
    /// Noktasal nesneler iÃ§in kullanÄ±lÄ±r.
    /// Alan/poligon gibi nesnelerde merkez veya referans noktasÄ± olarak kullanÄ±labilir.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Nesnenin boylam deÄŸeri.
    /// 
    /// Noktasal nesneler iÃ§in kullanÄ±lÄ±r.
    /// Alan/poligon gibi nesnelerde merkez veya referans noktasÄ± olarak kullanÄ±labilir.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Nesnenin yerel X konumu.
    /// 
    /// GPS olmayan simÃ¼lasyon veya lokal harita koordinatlarÄ± iÃ§in kullanÄ±labilir.
    /// Metre cinsinden dÃ¼ÅŸÃ¼nÃ¼lÃ¼r.
    /// </summary>
    public double? X { get; init; }

    /// <summary>
    /// Nesnenin yerel Y konumu.
    /// 
    /// GPS olmayan simÃ¼lasyon veya lokal harita koordinatlarÄ± iÃ§in kullanÄ±labilir.
    /// Metre cinsinden dÃ¼ÅŸÃ¼nÃ¼lÃ¼r.
    /// </summary>
    public double? Y { get; init; }

    /// <summary>
    /// Nesnenin tahmini yarÄ±Ã§apÄ± veya etki alanÄ±.
    /// 
    /// Ã–rnek:
    /// - Engel yarÄ±Ã§apÄ±
    /// - No-go zone yaklaÅŸÄ±k yarÄ±Ã§apÄ±
    /// - Link quality Ã¶lÃ§Ã¼m alanÄ±
    /// </summary>
    public double? RadiusMeters { get; init; }

    /// <summary>
    /// Nesnenin gÃ¼ven skoru.
    /// 
    /// 0.0 - 1.0 aralÄ±ÄŸÄ±nda dÃ¼ÅŸÃ¼nÃ¼lÃ¼r.
    /// 
    /// Ã–rnek:
    /// - Tek araÃ§ zayÄ±f tespit yaptÄ±ysa 0.4
    /// - Birden fazla araÃ§ doÄŸruladÄ±ysa 0.8+
    /// - OperatÃ¶r elle eklediyse 1.0
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Nesnenin aktif olup olmadÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// false ise:
    /// - Nesne eski olabilir.
    /// - GÃ¶rev tamamlanmÄ±ÅŸ olabilir.
    /// - OperatÃ¶r nesneyi devre dÄ±ÅŸÄ± bÄ±rakmÄ±ÅŸ olabilir.
    /// - Fusion engine nesneyi artÄ±k geÃ§erli gÃ¶rmÃ¼yor olabilir.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Nesnenin ilk oluÅŸturulduÄŸu UTC zaman.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Nesnenin son gÃ¼ncellendiÄŸi UTC zaman.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Nesneyle ilgili ek metadata alanÄ±.
    /// 
    /// Ã–rnek:
    /// - "sensor": "lidar"
    /// - "class": "buoy"
    /// - "severity": "high"
    /// - "sourceFrame": "fused"
    /// - "mapLayer": "occupancy"
    /// 
    /// Ä°lk fazda esneklik saÄŸlar.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// DÃ¼nya nesnesinin temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En azÄ±ndan ObjectId ve Kind anlamlÄ± olmalÄ±dÄ±r.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ObjectId) &&
        Kind != WorldObjectKind.Unknown;

    /// <summary>
    /// Nesnenin yeni bir kaynak node tarafÄ±ndan doÄŸrulanmÄ±ÅŸ/gÃ¼ncellenmiÅŸ hÃ¢lini dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// Bu metot immutable record yapÄ±sÄ±nÄ± koruyarak yeni kopya Ã¼retir.
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
