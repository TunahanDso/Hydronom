namespace Hydronom.GroundStation.Coordination;

/// <summary>
/// Ground Station tarafından bir görevin filo içindeki uygun araca atanması için kullanılan görev isteği modelidir.
/// 
/// MissionRequest, MissionAllocator'a şunu anlatır:
/// - Ne tür bir görev istiyoruz?
/// - Hangi kabiliyetler gerekli?
/// - Hangi araç tipleri uygun?
/// - Görev ne kadar öncelikli?
/// - Hangi alanda veya hedefte çalışılacak?
/// 
/// Bu model PDF'deki MissionPlanner / MissionAllocator mantığının ilk küçük çekirdeğidir.
/// </summary>
public sealed record MissionRequest
{
    /// <summary>
    /// Görev isteğinin benzersiz kimliği.
    /// 
    /// Örnek:
    /// - "MISSION-SEARCH-001"
    /// - "MISSION-MAP-AREA-A"
    /// - "MISSION-INSPECT-BUOY-01"
    /// </summary>
    public string MissionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Görevin insan tarafından okunabilir adı.
    /// 
    /// Hydronom Ops üzerinde görev listesinde gösterilebilir.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Görev tipi.
    /// 
    /// Örnekler:
    /// - "Search"
    /// - "Mapping"
    /// - "InspectTarget"
    /// - "Patrol"
    /// - "ReturnHome"
    /// - "Relay"
    /// </summary>
    public string MissionType { get; init; } = string.Empty;

    /// <summary>
    /// Bu görevi yapabilmek için gerekli kabiliyet adları.
    /// 
    /// Örnek:
    /// Mapping görevi:
    /// - "navigation"
    /// - "mapping"
    /// - "lidar"
    /// 
    /// Target inspection görevi:
    /// - "navigation"
    /// - "camera"
    /// - "target_tracking"
    /// </summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Görev için tercih edilen ama zorunlu olmayan kabiliyet adları.
    /// 
    /// Bu kabiliyetlere sahip araçlar daha yüksek skor alır.
    /// </summary>
    public IReadOnlyList<string> PreferredCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Görev için uygun araç tipleri.
    /// 
    /// Boş ise her araç tipi uygun kabul edilebilir.
    /// 
    /// Örnek:
    /// - "SurfaceVessel"
    /// - "Submarine"
    /// - "SailingVessel"
    /// - "AerialVehicle"
    /// </summary>
    public IReadOnlyList<string> AllowedVehicleTypes { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Görev önceliği.
    /// 
    /// Daha yüksek değer daha öncelikli görev anlamına gelir.
    /// İlk fazda basit int kullanıyoruz.
    /// </summary>
    public int Priority { get; init; } = 1;

    /// <summary>
    /// Görev için hedef enlem.
    /// 
    /// Ops map üzerinden seçilen nokta veya görev alanı merkezi olabilir.
    /// </summary>
    public double? TargetLatitude { get; init; }

    /// <summary>
    /// Görev için hedef boylam.
    /// 
    /// Ops map üzerinden seçilen nokta veya görev alanı merkezi olabilir.
    /// </summary>
    public double? TargetLongitude { get; init; }

    /// <summary>
    /// Görev için yerel X hedef koordinatı.
    /// 
    /// Simülasyon veya GPS olmayan görevlerde kullanılabilir.
    /// </summary>
    public double? TargetX { get; init; }

    /// <summary>
    /// Görev için yerel Y hedef koordinatı.
    /// 
    /// Simülasyon veya GPS olmayan görevlerde kullanılabilir.
    /// </summary>
    public double? TargetY { get; init; }

    /// <summary>
    /// Görevle ilişkili dünya nesnesi kimliği.
    /// 
    /// Örnek:
    /// - Belirli bir target object
    /// - MissionArea object
    /// - NoGoZone object
    /// </summary>
    public string RelatedWorldObjectId { get; init; } = string.Empty;

    /// <summary>
    /// Görevle ilgili ek metadata alanı.
    /// 
    /// Örnek:
    /// - "areaId": "AREA-A"
    /// - "operator": "Tunahan"
    /// - "source": "ops_map"
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Görev isteğinin temel olarak geçerli olup olmadığını döndürür.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(MissionId) &&
        !string.IsNullOrWhiteSpace(MissionType);
}