namespace Hydronom.GroundStation.Diagnostics;

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