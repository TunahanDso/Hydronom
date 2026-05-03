namespace Hydronom.GroundStation.WorldModel;

/// <summary>
/// GroundWorldModel iÃ§inde tutulabilecek dÃ¼nya nesnesi tÃ¼rlerini temsil eder.
/// 
/// Yer istasyonu tarafÄ±nda farklÄ± araÃ§lardan gelen bilgiler ortak bir dÃ¼nya modelinde
/// birleÅŸecektir. Bu enum, o ortak modeldeki nesnelerin temel sÄ±nÄ±flandÄ±rmasÄ±dÄ±r.
/// 
/// Ã–rnek:
/// - Bir araÃ§ LiDAR ile engel gÃ¶rÃ¼r.
/// - BaÅŸka bir araÃ§ kamera ile hedef tespit eder.
/// - OperatÃ¶r haritada no-go zone Ã§izer.
/// - MissionPlanner gÃ¶rev alanÄ± tanÄ±mlar.
/// 
/// BunlarÄ±n hepsi GroundWorldModel iÃ§inde farklÄ± WorldObjectKind deÄŸerleriyle tutulabilir.
/// </summary>
public enum WorldObjectKind
{
    /// <summary>
    /// Nesne tÃ¼rÃ¼ bilinmiyor veya henÃ¼z sÄ±nÄ±flandÄ±rÄ±lmadÄ±.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Filo iÃ§indeki araÃ§/node.
    /// 
    /// Not:
    /// AraÃ§larÄ±n ana canlÄ± durumu FleetRegistry iÃ§inde tutulur.
    /// GroundWorldModel iÃ§inde ise araÃ§ dÃ¼nya Ã¼zerindeki bir nesne olarak da temsil edilebilir.
    /// </summary>
    Vehicle = 1,

    /// <summary>
    /// Engel nesnesi.
    /// 
    /// Ã–rnek:
    /// - LiDAR ile gÃ¶rÃ¼len sabit engel
    /// - Kamera ile algÄ±lanan riskli obje
    /// - Harita Ã¼zerinden gelen bilinen engel
    /// </summary>
    Obstacle = 2,

    /// <summary>
    /// Hedef nesnesi.
    /// 
    /// Ã–rnek:
    /// - Tespit edilen ÅŸamandÄ±ra
    /// - Takip edilmesi gereken obje
    /// - GÃ¶rev hedef noktasÄ±
    /// </summary>
    Target = 3,

    /// <summary>
    /// Girilmemesi gereken bÃ¶lge.
    /// 
    /// Ã–rnek:
    /// - Yasak alan
    /// - SÄ±ÄŸ bÃ¶lge
    /// - OperatÃ¶r tarafÄ±ndan Ã§izilen risk alanÄ±
    /// - YarÄ±ÅŸma alanÄ±nda kÄ±sÄ±tlÄ± bÃ¶lge
    /// </summary>
    NoGoZone = 4,

    /// <summary>
    /// GÃ¶rev alanÄ±.
    /// 
    /// Ã–rnek:
    /// - Arama yapÄ±lacak bÃ¶lge
    /// - Haritalanacak alan
    /// - Devriye alanÄ±
    /// - YarÄ±ÅŸma gÃ¶rev sahasÄ±
    /// </summary>
    MissionArea = 5,

    /// <summary>
    /// Harita katmanÄ±.
    /// 
    /// Ã–rnek:
    /// - Occupancy grid
    /// - Derinlik haritasÄ±
    /// - Risk haritasÄ±
    /// - Link quality heatmap
    /// </summary>
    MapLayer = 6,

    /// <summary>
    /// BaÄŸlantÄ±/link kalitesiyle ilgili dÃ¼nya nesnesi veya bÃ¶lgesel bilgi.
    /// 
    /// Ã–rnek:
    /// - RF sinyal zayÄ±f bÃ¶lge
    /// - LoRa kapsama alanÄ±
    /// - Wi-Fi baÄŸlantÄ± kalitesi noktasÄ±
    /// </summary>
    LinkQuality = 7,

    /// <summary>
    /// Operasyon sÄ±rasÄ±nda oluÅŸan olay.
    /// 
    /// Ã–rnek:
    /// - AraÃ§ baÄŸlantÄ±sÄ± koptu
    /// - Engel tespit edildi
    /// - Komut reddedildi
    /// - EmergencyStop uygulandÄ±
    /// </summary>
    Event = 8
}
