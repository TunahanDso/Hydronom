namespace Hydronom.Core.Scenarios.Models;

/// <summary>
/// Senaryo JSON dosyasından okunan dünya objesi tanımıdır.
/// Bu sınıf runtime tarafındaki gerçek world object'e dönüştürülmeden önceki ham tanımdır.
/// </summary>
public sealed record ScenarioWorldObjectDefinition
{
    /// <summary>
    /// Senaryo içindeki benzersiz obje kimliği.
    /// Örnek: buoy_01, dock_main, no_go_north
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Obje tipi.
    /// Örnek: buoy, dock, gate, no_go_zone, waypoint, target, inspection_zone
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// İnsan tarafından okunabilir obje adı.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Objenin bağlı olduğu mantıksal katman.
    /// Örnek: mission, obstacle, safety, navigation, environment
    /// </summary>
    public string Layer { get; init; } = "mission";

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
    /// Objenin yaw açısı.
    /// Derece cinsindendir.
    /// </summary>
    public double YawDeg { get; init; }

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
    /// Ek metadata.
    /// Senaryo dosyasının ileride genişletilmesi için bırakılmıştır.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}