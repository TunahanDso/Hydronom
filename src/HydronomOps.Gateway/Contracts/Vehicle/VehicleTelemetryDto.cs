using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Vehicle;

/// <summary>
/// Harita üzerinde çizilecek 2D nokta verisi.
/// </summary>
public sealed class ObstaclePointDto
{
    /// <summary>
    /// Dünya ekseninde X konumu.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Dünya ekseninde Y konumu.
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// Runtime tarafından üretilen dairesel engel özeti.
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
    /// Engel yarıçapı.
    /// </summary>
    public double R { get; set; }
}

/// <summary>
/// Landmark stil bilgisi.
/// Frontend tarafında çizim ipucu olarak kullanılır.
/// </summary>
public sealed class LandmarkStyleDto
{
    /// <summary>
    /// Renk bilgisi.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Çizgi kalınlığı.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Nokta yarıçapı.
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// Etiket metni.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Ek stil alanları.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// FusedState içindeki landmark verisini taşır.
/// Örnek: lidar taramasından üretilen polyline.
/// </summary>
public sealed class LandmarkDto
{
    /// <summary>
    /// Landmark kimliği.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Landmark tipi.
    /// Örnek: occupancy_preview, occupancy_cells, trail_ekf, ekf_pose, odometry.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Landmark şekli.
    /// Örnek: polyline, points, point.
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
    /// Landmark sayısal ek alanları.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Landmark metinsel ek alanları.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Araç telemetri özetini taşır.
/// </summary>
public sealed class VehicleTelemetryDto
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
    /// Roll açısı (derece).
    /// </summary>
    public double RollDeg { get; set; }

    /// <summary>
    /// Pitch açısı (derece).
    /// </summary>
    public double PitchDeg { get; set; }

    /// <summary>
    /// Yaw / heading açısı (derece).
    /// </summary>
    public double YawDeg { get; set; }

    /// <summary>
    /// Heading için alternatif alan.
    /// </summary>
    public double HeadingDeg { get; set; }

    /// <summary>
    /// Gövde eksenindeki ileri hız.
    /// </summary>
    public double Vx { get; set; }

    /// <summary>
    /// Gövde eksenindeki yan hız.
    /// </summary>
    public double Vy { get; set; }

    /// <summary>
    /// Gövde eksenindeki dikey hız.
    /// </summary>
    public double Vz { get; set; }

    /// <summary>
    /// Roll hızı.
    /// </summary>
    public double RollRateDeg { get; set; }

    /// <summary>
    /// Pitch hızı.
    /// </summary>
    public double PitchRateDeg { get; set; }

    /// <summary>
    /// Yaw hızı.
    /// </summary>
    public double YawRateDeg { get; set; }

    /// <summary>
    /// Dünya ekseninde hedef X.
    /// </summary>
    public double? TargetX { get; set; }

    /// <summary>
    /// Dünya ekseninde hedef Y.
    /// </summary>
    public double? TargetY { get; set; }

    /// <summary>
    /// Hedefe kalan mesafe.
    /// </summary>
    public double? DistanceToGoalM { get; set; }

    /// <summary>
    /// Heading hatası.
    /// </summary>
    public double? HeadingErrorDeg { get; set; }

    /// <summary>
    /// Ön bölgede engel var mı.
    /// </summary>
    public bool ObstacleAhead { get; set; }

    /// <summary>
    /// Toplam engel sayısı.
    /// </summary>
    public int ObstacleCount { get; set; }

    /// <summary>
    /// Runtime obstacle listesi.
    /// Harita üzerinde dairesel engel çizimi için kullanılır.
    /// </summary>
    public List<ObstacleDto> Obstacles { get; set; } = new();

    /// <summary>
    /// FusedState landmark listesi.
    /// Örnek olarak lidar taramasından gelen polyline verisi burada taşınır.
    /// </summary>
    public List<LandmarkDto> Landmarks { get; set; } = new();

    /// <summary>
    /// Yardımcı sayısal ek alanlar.
    /// Örnek: ekf covariance, occupancy export count, slam düzeltme büyüklükleri.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Yardımcı metinsel alanlar.
    /// Örnek: origin, mapper bilgileri, landmark etiketleri.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Veri tazelik özeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}