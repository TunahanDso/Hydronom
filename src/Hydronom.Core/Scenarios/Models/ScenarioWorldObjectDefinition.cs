namespace Hydronom.Core.Scenarios.Models;

/// <summary>
/// Senaryo JSON dosyasından okunan dünya objesi tanımıdır.
/// Bu sınıf runtime tarafındaki gerçek world object'e dönüştürülmeden önceki ham tanımdır.
/// </summary>
public sealed record ScenarioWorldObjectDefinition
{
    /// <summary>
    /// Senaryo içindeki benzersiz obje kimliği.
    /// Örnek: buoy_01, gate_01, dock_main, no_go_north.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Obje tipi.
    /// Örnek: buoy, dock, gate, no_go_zone, waypoint, target_zone, obstacle, inspection_zone.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// İnsan tarafından okunabilir obje adı.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Objenin bağlı olduğu mantıksal katman.
    /// Örnek: mission, obstacle, safety, navigation, environment, judge.
    /// </summary>
    public string Layer { get; init; } = "mission";

    /// <summary>
    /// Obje rolü.
    /// Örnek: start, finish, gate_left, gate_right, target, boundary, no_go.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Dünya koordinatlarında X konumu.
    /// Şimdilik metre tabanlı lokal koordinat sistemi varsayılır.
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Dünya koordinatlarında Y konumu.
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Dünya koordinatlarında Z konumu.
    /// Deniz yüzeyi/zemin için çoğu senaryoda 0 olabilir.
    /// </summary>
    public double Z { get; init; }

    /// <summary>
    /// Objenin roll açısı.
    /// Derece cinsindendir.
    /// </summary>
    public double RollDeg { get; init; }

    /// <summary>
    /// Objenin pitch açısı.
    /// Derece cinsindendir.
    /// </summary>
    public double PitchDeg { get; init; }

    /// <summary>
    /// Objenin yaw açısı.
    /// Derece cinsindendir.
    /// </summary>
    public double YawDeg { get; init; }

    /// <summary>
    /// Objenin yaklaşık yarıçapı.
    /// Circular obstacle, buoy veya target için kullanılabilir.
    /// </summary>
    public double Radius { get; init; }

    /// <summary>
    /// Obje genişliği.
    /// Dock, gate, zone gibi objeler için kullanılabilir.
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Obje yüksekliği/uzunluğu.
    /// 2D parkurda length veya height gibi yorumlanabilir.
    /// </summary>
    public double Height { get; init; }

    /// <summary>
    /// Obje uzunluğu.
    /// Height ile karıştırılmaması gereken platform/engel uzunlukları için kullanılabilir.
    /// </summary>
    public double Length { get; init; }

    /// <summary>
    /// Obje aktif mi?
    /// Testlerde bazı objeler pasif bırakılabilir.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Obje güvenlik açısından engel/no-go gibi davranmalı mı?
    /// </summary>
    public bool IsBlocking { get; init; }

    /// <summary>
    /// Obje sensör simülasyonunda algılanabilir mi?
    /// Örneğin LiDAR raycast veya kamera simülasyonu için kullanılır.
    /// </summary>
    public bool IsDetectable { get; init; } = true;

    /// <summary>
    /// Obje mission judge tarafından görev/ihlal/puanlama için takip edilmeli mi?
    /// </summary>
    public bool IsJudgeTracked { get; init; } = true;

    /// <summary>
    /// Obje no-go zone olarak yorumlanmalı mı?
    /// </summary>
    public bool IsNoGoZone { get; init; }

    /// <summary>
    /// Obje hedef bölge olarak yorumlanmalı mı?
    /// </summary>
    public bool IsTargetZone { get; init; }

    /// <summary>
    /// Obje gate/kapı olarak yorumlanmalı mı?
    /// </summary>
    public bool IsGate { get; init; }

    /// <summary>
    /// Gate objeleri için sol marker/duba kimliği.
    /// </summary>
    public string? LeftObjectId { get; init; }

    /// <summary>
    /// Gate objeleri için sağ marker/duba kimliği.
    /// </summary>
    public string? RightObjectId { get; init; }

    /// <summary>
    /// Objenin bağlı olduğu görev hedefi kimliği.
    /// Örnek: pass_gate_1, reach_target_zone.
    /// </summary>
    public string? ObjectiveId { get; init; }

    /// <summary>
    /// Objenin tamamlanma veya algılama toleransı.
    /// Sıfır veya negatifse obje özel toleransı yok kabul edilir.
    /// </summary>
    public double ToleranceMeters { get; init; }

    /// <summary>
    /// Obje içinden geçiş doğru yön gerektiriyor mu?
    /// Gate gibi objelerde kullanılabilir.
    /// </summary>
    public bool RequiresDirectionCheck { get; init; }

    /// <summary>
    /// Doğru geçiş yönü.
    /// Derece cinsinden yaw/heading.
    /// </summary>
    public double RequiredHeadingDeg { get; init; }

    /// <summary>
    /// Doğru geçiş yönü için tolerans.
    /// Derece cinsindendir.
    /// </summary>
    public double HeadingToleranceDeg { get; init; } = 45.0;

    /// <summary>
    /// Polygon/zone tabanlı objeler için nokta listesi.
    /// Eğer boşsa X/Y/Width/Height/Radius alanları kullanılır.
    /// </summary>
    public IReadOnlyList<ScenarioPoint2DDefinition> Points { get; init; }
        = Array.Empty<ScenarioPoint2DDefinition>();

    /// <summary>
    /// Görselleştirme için önerilen renk.
    /// Örnek: red, green, blue, orange.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Ops/Mission Theater üzerinde gösterilecek kısa etiket.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Obje skor değeri.
    /// Örneğin hedef bölgeye ulaşma veya gate geçme puanı.
    /// </summary>
    public double ScoreValue { get; init; }

    /// <summary>
    /// Obje ihlal edildiğinde uygulanacak ceza.
    /// No-go zone veya çarpışma bölgeleri için kullanılabilir.
    /// </summary>
    public double PenaltyValue { get; init; }

    /// <summary>
    /// Ek metadata.
    /// Senaryo dosyasının ileride genişletilmesi için bırakılmıştır.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasPoints => Points.Count > 0;

    public bool HasRadius => Radius > 0.0;

    public bool HasRectSize => Width > 0.0 || Height > 0.0 || Length > 0.0;

    public bool HasGateMarkers =>
        !string.IsNullOrWhiteSpace(LeftObjectId) &&
        !string.IsNullOrWhiteSpace(RightObjectId);
}

/// <summary>
/// Senaryo içindeki 2D nokta tanımı.
/// Polygon, boundary, no-go zone ve özel parkur çizimleri için kullanılır.
/// </summary>
public sealed record ScenarioPoint2DDefinition
{
    /// <summary>
    /// X koordinatı.
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Y koordinatı.
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Nokta etiketi.
    /// </summary>
    public string? Label { get; init; }
}