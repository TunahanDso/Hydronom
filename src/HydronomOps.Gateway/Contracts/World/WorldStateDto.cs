using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.World;

/// <summary>
/// Runtime/Gateway üzerinden Ops'a taşınan dünya/senaryo katmanı.
/// Checkpoint, finish, duba, engel, rota ve aktif hedef gibi 3D mission view verilerini taşır.
/// </summary>
public sealed class WorldStateDto
{
    /// <summary>
    /// Paket zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Dünya/senaryo verisinin kaynağı.
    /// Örn: runtime-scenario-controller.
    /// </summary>
    public string Source { get; set; } = "runtime";

    /// <summary>
    /// Aktif senaryo id.
    /// </summary>
    public string? ScenarioId { get; set; }

    /// <summary>
    /// Aktif senaryo adı.
    /// </summary>
    public string? ScenarioName { get; set; }

    /// <summary>
    /// Aktif runtime scenario run id.
    /// </summary>
    public string? RunId { get; set; }

    /// <summary>
    /// Aktif objective id.
    /// </summary>
    public string? CurrentObjectiveId { get; set; }

    /// <summary>
    /// Aktif hedef noktası.
    /// </summary>
    public WorldTargetDto? ActiveObjectiveTarget { get; set; }

    /// <summary>
    /// Rota noktaları.
    /// </summary>
    public List<WorldRoutePointDto> Route { get; set; } = new();

    /// <summary>
    /// Dünya/senaryo objeleri.
    /// START, checkpoint, finish, buoy, obstacle vb.
    /// </summary>
    public List<WorldObjectDto> Objects { get; set; } = new();

    /// <summary>
    /// Sayısal ek metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Metinsel ek alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Veri tazelik özeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}

/// <summary>
/// Aktif objective hedefi.
/// </summary>
public sealed class WorldTargetDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double? ToleranceMeters { get; set; }
}

/// <summary>
/// 3D sahnede çizilecek rota noktası.
/// </summary>
public sealed class WorldRoutePointDto
{
    public string Id { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? ObjectiveId { get; set; }
    public int Index { get; set; }
    public string Type { get; set; } = "route-point";

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public double? ToleranceMeters { get; set; }

    public bool Active { get; set; }
    public bool Completed { get; set; }
}

/// <summary>
/// 3D sahnede çizilecek dünya/senaryo objesi.
/// </summary>
public sealed class WorldObjectDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// start, checkpoint, finish, buoy, obstacle, gate vb.
    /// </summary>
    public string Type { get; set; } = "object";

    public string? Label { get; set; }
    public string? ObjectiveId { get; set; }
    public string? Side { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public double Radius { get; set; } = 0.5;
    public string? Color { get; set; }

    public bool Active { get; set; }
    public bool Completed { get; set; }

    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}