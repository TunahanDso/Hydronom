namespace Hydronom.GroundStation.Diagnostics;

using Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Ground Station tarafının tek bakışta okunabilir operasyon özetini temsil eder.
/// 
/// Bu model, Hydronom Ops veya diagnostics ekranı için tek çağrıda genel durum bilgisi sağlar.
/// Amaç, farklı modüllerden gelen bilgileri sade bir snapshot halinde toplamaktır.
/// 
/// Örnek olarak şunları özetler:
/// - Filo durumu,
/// - Komut geçmişi,
/// - Ortak dünya modeli,
/// - Bağlantı/link sağlığı,
/// - Genel health değerlendirmesi,
/// - Kısa açıklama.
/// </summary>
public sealed record GroundOperationSnapshot
{
    /// <summary>
    /// Snapshot'ın üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Toplam kayıtlı araç/node sayısı.
    /// </summary>
    public int TotalNodeCount { get; init; }

    /// <summary>
    /// Online durumdaki araç/node sayısı.
    /// </summary>
    public int OnlineNodeCount { get; init; }

    /// <summary>
    /// Offline durumdaki araç/node sayısı.
    /// </summary>
    public int OfflineNodeCount { get; init; }

    /// <summary>
    /// Health değeri OK olan araç/node sayısı.
    /// </summary>
    public int HealthyNodeCount { get; init; }

    /// <summary>
    /// Health değeri Warning olan araç/node sayısı.
    /// </summary>
    public int WarningNodeCount { get; init; }

    /// <summary>
    /// Health değeri Critical veya Fault olan araç/node sayısı.
    /// </summary>
    public int CriticalNodeCount { get; init; }

    /// <summary>
    /// Ortalama batarya yüzdesi.
    /// 
    /// Batarya bilgisi olmayan araçlar ortalamaya dahil edilmez.
    /// Hiç batarya bilgisi yoksa null kalır.
    /// </summary>
    public double? AverageBatteryPercent { get; init; }

    /// <summary>
    /// Kayıtlı toplam komut sayısı.
    /// </summary>
    public int TotalCommandCount { get; init; }

    /// <summary>
    /// Henüz sonuç bekleyen komut sayısı.
    /// </summary>
    public int PendingCommandCount { get; init; }

    /// <summary>
    /// Tamamlanmış komut sayısı.
    /// </summary>
    public int CompletedCommandCount { get; init; }

    /// <summary>
    /// Başarılı komut sayısı.
    /// </summary>
    public int SuccessfulCommandCount { get; init; }

    /// <summary>
    /// Başarısız veya expired komut sayısı.
    /// </summary>
    public int FailedCommandCount { get; init; }

    /// <summary>
    /// GroundWorldModel içindeki toplam dünya nesnesi sayısı.
    /// </summary>
    public int TotalWorldObjectCount { get; init; }

    /// <summary>
    /// GroundWorldModel içindeki aktif dünya nesnesi sayısı.
    /// </summary>
    public int ActiveWorldObjectCount { get; init; }

    /// <summary>
    /// Aktif obstacle sayısı.
    /// </summary>
    public int ActiveObstacleCount { get; init; }

    /// <summary>
    /// Aktif target sayısı.
    /// </summary>
    public int ActiveTargetCount { get; init; }

    /// <summary>
    /// Aktif no-go zone sayısı.
    /// </summary>
    public int ActiveNoGoZoneCount { get; init; }

    /// <summary>
    /// LinkHealthTracker tarafından takip edilen toplam araç bağlantı grubu sayısı.
    /// 
    /// Bu değer araç bazlıdır.
    /// Örneğin Alpha için WiFi + LoRa takip ediliyorsa bu alan yine 1 olur.
    /// </summary>
    public int LinkVehicleCount { get; init; }

    /// <summary>
    /// Takip edilen toplam transport link sayısı.
    /// 
    /// Örneğin Alpha/WiFi, Alpha/LoRa, Beta/RF toplam 3 link sayılır.
    /// </summary>
    public int TotalLinkCount { get; init; }

    /// <summary>
    /// Good durumundaki link sayısı.
    /// </summary>
    public int GoodLinkCount { get; init; }

    /// <summary>
    /// Degraded durumundaki link sayısı.
    /// </summary>
    public int DegradedLinkCount { get; init; }

    /// <summary>
    /// Critical durumundaki link sayısı.
    /// </summary>
    public int CriticalLinkCount { get; init; }

    /// <summary>
    /// Lost durumundaki link sayısı.
    /// </summary>
    public int LostLinkCount { get; init; }

    /// <summary>
    /// Unknown durumundaki link sayısı.
    /// </summary>
    public int UnknownLinkCount { get; init; }

    /// <summary>
    /// Araçlar arasındaki en iyi linklerin ortalama kalite skoru.
    /// 
    /// Araç başına OverallQualityScore değerlerinin ortalamasıdır.
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? AverageVehicleLinkQualityScore { get; init; }

    /// <summary>
    /// Tüm transport linklerinin ortalama kalite skoru.
    /// 
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? AverageTransportLinkQualityScore { get; init; }

    /// <summary>
    /// En düşük araç bağlantı kalite skoru.
    /// 
    /// Filodaki zayıf halkayı hızlı görmek için kullanılır.
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? WorstVehicleLinkQualityScore { get; init; }

    /// <summary>
    /// En düşük transport bağlantı kalite skoru.
    /// 
    /// Tekil transport seviyesindeki en kötü linki gösterir.
    /// Link verisi yoksa null kalır.
    /// </summary>
    public double? WorstTransportLinkQualityScore { get; init; }

    /// <summary>
    /// Link health durumunun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops Communication Links panelinde veya diagnostics özetinde gösterilebilir.
    /// </summary>
    public string LinkHealthSummary { get; init; } = "No link health data.";

    /// <summary>
    /// Araç bazlı link health snapshot listesi.
    /// 
    /// Bu alan Hydronom Ops tarafında araç kartları, link health paneli,
    /// communication diagnostics ekranı ve ileride route karar izleme için kullanılabilir.
    /// </summary>
    public IReadOnlyList<VehicleLinkHealthSnapshot> LinkHealth { get; init; } =
        Array.Empty<VehicleLinkHealthSnapshot>();

    /// <summary>
    /// Ground Station genel health değerlendirmesi.
    /// 
    /// Örnek:
    /// - OK
    /// - Warning
    /// - Critical
    /// </summary>
    public string OverallHealth { get; init; } = "Unknown";

    /// <summary>
    /// Genel durumun kısa insan-okunabilir açıklaması.
    /// 
    /// Hydronom Ops üst panelinde veya log ekranında gösterilebilir.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Snapshot üzerinde kritik durum olup olmadığını hızlıca döndürür.
    /// </summary>
    public bool HasCriticalIssues =>
        string.Equals(OverallHealth, "Critical", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Snapshot üzerinde uyarı durumu olup olmadığını hızlıca döndürür.
    /// </summary>
    public bool HasWarnings =>
        string.Equals(OverallHealth, "Warning", StringComparison.OrdinalIgnoreCase);
}