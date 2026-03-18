using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Mission;

/// <summary>
/// Araç görev durumunu ve görev akış özetini taşır.
/// </summary>
public sealed class MissionStateDto
{
    /// <summary>
    /// Görev durum paketinin üretim zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Aktif görev kimliği.
    /// </summary>
    public string? MissionId { get; set; }

    /// <summary>
    /// Aktif görev adı.
    /// </summary>
    public string? MissionName { get; set; }

    /// <summary>
    /// Görev durumu.
    /// Örn: idle, planned, running, paused, completed, failed.
    /// </summary>
    public string Status { get; set; } = "idle";

    /// <summary>
    /// Aktif adım indeksi.
    /// </summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Toplam adım sayısı.
    /// </summary>
    public int TotalStepCount { get; set; }

    /// <summary>
    /// Aktif adım başlığı.
    /// </summary>
    public string? CurrentStepTitle { get; set; }

    /// <summary>
    /// Sonraki hedef veya görev açıklaması.
    /// </summary>
    public string? NextObjective { get; set; }

    /// <summary>
    /// Hedefe kalan yaklaşık mesafe.
    /// </summary>
    public double? RemainingDistanceMeters { get; set; }

    /// <summary>
    /// Görev başlangıç zamanı.
    /// </summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>
    /// Görev bitiş zamanı.
    /// </summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>
    /// Görevle ilişkili uyarılar.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Veri tazelik özeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}