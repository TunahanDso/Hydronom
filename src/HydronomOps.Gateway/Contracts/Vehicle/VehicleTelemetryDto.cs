using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Vehicle;

/// <summary>
/// Harita Ã¼zerinde Ã§izilecek 2D nokta verisi.
/// </summary>
public sealed class ObstaclePointDto
{
    /// <summary>
    /// DÃ¼nya ekseninde X konumu.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// DÃ¼nya ekseninde Y konumu.
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// Runtime tarafÄ±ndan Ã¼retilen dairesel engel Ã¶zeti.
/// </summary>
public sealed class ObstacleDto
{
    /// <summary>
    /// Engel merkez X konumu.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Engel merkez Y konumu.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Engel yarÄ±Ã§apÄ±.
    /// </summary>
    public double R { get; set; }
}

/// <summary>
/// Landmark stil bilgisi.
/// Frontend tarafÄ±nda Ã§izim ipucu olarak kullanÄ±lÄ±r.
/// </summary>
public sealed class LandmarkStyleDto
{
    /// <summary>
    /// Renk bilgisi.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Ã‡izgi kalÄ±nlÄ±ÄŸÄ±.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Nokta yarÄ±Ã§apÄ±.
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// Etiket metni.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Ek stil alanlarÄ±.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// FusedState iÃ§indeki landmark verisini taÅŸÄ±r.
/// Ã–rnek: lidar taramasÄ±ndan Ã¼retilen polyline.
/// </summary>
public sealed class LandmarkDto
{
    /// <summary>
    /// Landmark kimliÄŸi.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Landmark tipi.
    /// Ã–rnek: occupancy_preview, occupancy_cells, trail_ekf, ekf_pose, odometry.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Landmark ÅŸekli.
    /// Ã–rnek: polyline, points, point.
    /// </summary>
    public string Shape { get; set; } = string.Empty;

    /// <summary>
    /// Landmark nokta listesi.
    /// </summary>
    public List<ObstaclePointDto> Points { get; set; } = new();

    /// <summary>
    /// Landmark stil bilgisi.
    /// </summary>
    public LandmarkStyleDto? Style { get; set; }

    /// <summary>
    /// Landmark sayÄ±sal ek alanlarÄ±.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Landmark metinsel ek alanlarÄ±.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// AraÃ§ telemetri Ã¶zetini taÅŸÄ±r.
/// </summary>
public sealed class VehicleTelemetryDto
{
    /// <summary>
    /// Paket zamanÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// AraÃ§ kimliÄŸi.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Konum X.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Konum Y.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Konum Z.
    /// </summary>
    public double Z { get; set; }

    /// <summary>
    /// Roll aÃ§Ä±sÄ± (derece).
    /// </summary>
    public double RollDeg { get; set; }

    /// <summary>
    /// Pitch aÃ§Ä±sÄ± (derece).
    /// </summary>
    public double PitchDeg { get; set; }

    /// <summary>
    /// Yaw / heading aÃ§Ä±sÄ± (derece).
    /// </summary>
    public double YawDeg { get; set; }

    /// <summary>
    /// Heading iÃ§in alternatif alan.
    /// </summary>
    public double HeadingDeg { get; set; }

    /// <summary>
    /// GÃ¶vde eksenindeki ileri hÄ±z.
    /// </summary>
    public double Vx { get; set; }

    /// <summary>
    /// GÃ¶vde eksenindeki yan hÄ±z.
    /// </summary>
    public double Vy { get; set; }

    /// <summary>
    /// GÃ¶vde eksenindeki dikey hÄ±z.
    /// </summary>
    public double Vz { get; set; }

    /// <summary>
    /// Roll hÄ±zÄ±.
    /// </summary>
    public double RollRateDeg { get; set; }

    /// <summary>
    /// Pitch hÄ±zÄ±.
    /// </summary>
    public double PitchRateDeg { get; set; }

    /// <summary>
    /// Yaw hÄ±zÄ±.
    /// </summary>
    public double YawRateDeg { get; set; }

    /// <summary>
    /// DÃ¼nya ekseninde hedef X.
    /// </summary>
    public double? TargetX { get; set; }

    /// <summary>
    /// DÃ¼nya ekseninde hedef Y.
    /// </summary>
    public double? TargetY { get; set; }

    /// <summary>
    /// Hedefe kalan mesafe.
    /// </summary>
    public double? DistanceToGoalM { get; set; }

    /// <summary>
    /// Heading hatasÄ±.
    /// </summary>
    public double? HeadingErrorDeg { get; set; }

    /// <summary>
    /// Ã–n bÃ¶lgede engel var mÄ±.
    /// </summary>
    public bool ObstacleAhead { get; set; }

    /// <summary>
    /// Toplam engel sayÄ±sÄ±.
    /// </summary>
    public int ObstacleCount { get; set; }

    /// <summary>
    /// Runtime obstacle listesi.
    /// Harita Ã¼zerinde dairesel engel Ã§izimi iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public List<ObstacleDto> Obstacles { get; set; } = new();

    /// <summary>
    /// FusedState landmark listesi.
    /// Ã–rnek olarak lidar taramasÄ±ndan gelen polyline verisi burada taÅŸÄ±nÄ±r.
    /// </summary>
    public List<LandmarkDto> Landmarks { get; set; } = new();

    /// <summary>
    /// YardÄ±mcÄ± sayÄ±sal ek alanlar.
    /// Ã–rnek: ekf covariance, occupancy export count, slam dÃ¼zeltme bÃ¼yÃ¼klÃ¼kleri.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// YardÄ±mcÄ± metinsel alanlar.
    /// Ã–rnek: origin, mapper bilgileri, landmark etiketleri.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Veri tazelik Ã¶zeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}
