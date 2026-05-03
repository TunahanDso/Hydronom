namespace Hydronom.GroundStation.Coordination;

/// <summary>
/// Ground Station tarafÄ±ndan bir gÃ¶revin filo iÃ§indeki uygun araca atanmasÄ± iÃ§in kullanÄ±lan gÃ¶rev isteÄŸi modelidir.
/// 
/// MissionRequest, MissionAllocator'a ÅŸunu anlatÄ±r:
/// - Ne tÃ¼r bir gÃ¶rev istiyoruz?
/// - Hangi kabiliyetler gerekli?
/// - Hangi araÃ§ tipleri uygun?
/// - GÃ¶rev ne kadar Ã¶ncelikli?
/// - Hangi alanda veya hedefte Ã§alÄ±ÅŸÄ±lacak?
/// 
/// Bu model PDF'deki MissionPlanner / MissionAllocator mantÄ±ÄŸÄ±nÄ±n ilk kÃ¼Ã§Ã¼k Ã§ekirdeÄŸidir.
/// </summary>
public sealed record MissionRequest
{
    /// <summary>
    /// GÃ¶rev isteÄŸinin benzersiz kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - "MISSION-SEARCH-001"
    /// - "MISSION-MAP-AREA-A"
    /// - "MISSION-INSPECT-BUOY-01"
    /// </summary>
    public string MissionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// GÃ¶revin insan tarafÄ±ndan okunabilir adÄ±.
    /// 
    /// Hydronom Ops Ã¼zerinde gÃ¶rev listesinde gÃ¶sterilebilir.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// GÃ¶rev tipi.
    /// 
    /// Ã–rnekler:
    /// - "Search"
    /// - "Mapping"
    /// - "InspectTarget"
    /// - "Patrol"
    /// - "ReturnHome"
    /// - "Relay"
    /// </summary>
    public string MissionType { get; init; } = string.Empty;

    /// <summary>
    /// Bu gÃ¶revi yapabilmek iÃ§in gerekli kabiliyet adlarÄ±.
    /// 
    /// Ã–rnek:
    /// Mapping gÃ¶revi:
    /// - "navigation"
    /// - "mapping"
    /// - "lidar"
    /// 
    /// Target inspection gÃ¶revi:
    /// - "navigation"
    /// - "camera"
    /// - "target_tracking"
    /// </summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// GÃ¶rev iÃ§in tercih edilen ama zorunlu olmayan kabiliyet adlarÄ±.
    /// 
    /// Bu kabiliyetlere sahip araÃ§lar daha yÃ¼ksek skor alÄ±r.
    /// </summary>
    public IReadOnlyList<string> PreferredCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// GÃ¶rev iÃ§in uygun araÃ§ tipleri.
    /// 
    /// BoÅŸ ise her araÃ§ tipi uygun kabul edilebilir.
    /// 
    /// Ã–rnek:
    /// - "SurfaceVessel"
    /// - "Submarine"
    /// - "SailingVessel"
    /// - "AerialVehicle"
    /// </summary>
    public IReadOnlyList<string> AllowedVehicleTypes { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// GÃ¶rev Ã¶nceliÄŸi.
    /// 
    /// Daha yÃ¼ksek deÄŸer daha Ã¶ncelikli gÃ¶rev anlamÄ±na gelir.
    /// Ä°lk fazda basit int kullanÄ±yoruz.
    /// </summary>
    public int Priority { get; init; } = 1;

    /// <summary>
    /// GÃ¶rev iÃ§in hedef enlem.
    /// 
    /// Ops map Ã¼zerinden seÃ§ilen nokta veya gÃ¶rev alanÄ± merkezi olabilir.
    /// </summary>
    public double? TargetLatitude { get; init; }

    /// <summary>
    /// GÃ¶rev iÃ§in hedef boylam.
    /// 
    /// Ops map Ã¼zerinden seÃ§ilen nokta veya gÃ¶rev alanÄ± merkezi olabilir.
    /// </summary>
    public double? TargetLongitude { get; init; }

    /// <summary>
    /// GÃ¶rev iÃ§in yerel X hedef koordinatÄ±.
    /// 
    /// SimÃ¼lasyon veya GPS olmayan gÃ¶revlerde kullanÄ±labilir.
    /// </summary>
    public double? TargetX { get; init; }

    /// <summary>
    /// GÃ¶rev iÃ§in yerel Y hedef koordinatÄ±.
    /// 
    /// SimÃ¼lasyon veya GPS olmayan gÃ¶revlerde kullanÄ±labilir.
    /// </summary>
    public double? TargetY { get; init; }

    /// <summary>
    /// GÃ¶revle iliÅŸkili dÃ¼nya nesnesi kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - Belirli bir target object
    /// - MissionArea object
    /// - NoGoZone object
    /// </summary>
    public string RelatedWorldObjectId { get; init; } = string.Empty;

    /// <summary>
    /// GÃ¶revle ilgili ek metadata alanÄ±.
    /// 
    /// Ã–rnek:
    /// - "areaId": "AREA-A"
    /// - "operator": "Tunahan"
    /// - "source": "ops_map"
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// GÃ¶rev isteÄŸinin temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(MissionId) &&
        !string.IsNullOrWhiteSpace(MissionType);
}
