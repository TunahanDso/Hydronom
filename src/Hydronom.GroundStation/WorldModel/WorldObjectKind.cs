namespace Hydronom.GroundStation.WorldModel;

/// <summary>
/// GroundWorldModel içinde tutulabilecek dünya nesnesi türlerini temsil eder.
/// 
/// Yer istasyonu tarafında farklı araçlardan gelen bilgiler ortak bir dünya modelinde
/// birleşecektir. Bu enum, o ortak modeldeki nesnelerin temel sınıflandırmasıdır.
/// 
/// Örnek:
/// - Bir araç LiDAR ile engel görür.
/// - Başka bir araç kamera ile hedef tespit eder.
/// - Operatör haritada no-go zone çizer.
/// - MissionPlanner görev alanı tanımlar.
/// 
/// Bunların hepsi GroundWorldModel içinde farklı WorldObjectKind değerleriyle tutulabilir.
/// </summary>
public enum WorldObjectKind
{
    /// <summary>
    /// Nesne türü bilinmiyor veya henüz sınıflandırılmadı.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Filo içindeki araç/node.
    /// 
    /// Not:
    /// Araçların ana canlı durumu FleetRegistry içinde tutulur.
    /// GroundWorldModel içinde ise araç dünya üzerindeki bir nesne olarak da temsil edilebilir.
    /// </summary>
    Vehicle = 1,

    /// <summary>
    /// Engel nesnesi.
    /// 
    /// Örnek:
    /// - LiDAR ile görülen sabit engel
    /// - Kamera ile algılanan riskli obje
    /// - Harita üzerinden gelen bilinen engel
    /// </summary>
    Obstacle = 2,

    /// <summary>
    /// Hedef nesnesi.
    /// 
    /// Örnek:
    /// - Tespit edilen şamandıra
    /// - Takip edilmesi gereken obje
    /// - Görev hedef noktası
    /// </summary>
    Target = 3,

    /// <summary>
    /// Girilmemesi gereken bölge.
    /// 
    /// Örnek:
    /// - Yasak alan
    /// - Sığ bölge
    /// - Operatör tarafından çizilen risk alanı
    /// - Yarışma alanında kısıtlı bölge
    /// </summary>
    NoGoZone = 4,

    /// <summary>
    /// Görev alanı.
    /// 
    /// Örnek:
    /// - Arama yapılacak bölge
    /// - Haritalanacak alan
    /// - Devriye alanı
    /// - Yarışma görev sahası
    /// </summary>
    MissionArea = 5,

    /// <summary>
    /// Harita katmanı.
    /// 
    /// Örnek:
    /// - Occupancy grid
    /// - Derinlik haritası
    /// - Risk haritası
    /// - Link quality heatmap
    /// </summary>
    MapLayer = 6,

    /// <summary>
    /// Bağlantı/link kalitesiyle ilgili dünya nesnesi veya bölgesel bilgi.
    /// 
    /// Örnek:
    /// - RF sinyal zayıf bölge
    /// - LoRa kapsama alanı
    /// - Wi-Fi bağlantı kalitesi noktası
    /// </summary>
    LinkQuality = 7,

    /// <summary>
    /// Operasyon sırasında oluşan olay.
    /// 
    /// Örnek:
    /// - Araç bağlantısı koptu
    /// - Engel tespit edildi
    /// - Komut reddedildi
    /// - EmergencyStop uygulandı
    /// </summary>
    Event = 8
}