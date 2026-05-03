using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Mission;

/// <summary>
/// AraÃ§ gÃ¶rev durumunu ve gÃ¶rev akÄ±ÅŸ Ã¶zetini taÅŸÄ±r.
/// </summary>
public sealed class MissionStateDto
{
    /// <summary>
    /// GÃ¶rev durum paketinin Ã¼retim zamanÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// AraÃ§ kimliÄŸi.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Aktif gÃ¶rev kimliÄŸi.
    /// </summary>
    public string? MissionId { get; set; }

    /// <summary>
    /// Aktif gÃ¶rev adÄ±.
    /// </summary>
    public string? MissionName { get; set; }

    /// <summary>
    /// GÃ¶rev durumu.
    /// Ã–rn: idle, planned, running, paused, completed, failed.
    /// </summary>
    public string Status { get; set; } = "idle";

    /// <summary>
    /// Aktif adÄ±m indeksi.
    /// </summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Toplam adÄ±m sayÄ±sÄ±.
    /// </summary>
    public int TotalStepCount { get; set; }

    /// <summary>
    /// Aktif adÄ±m baÅŸlÄ±ÄŸÄ±.
    /// </summary>
    public string? CurrentStepTitle { get; set; }

    /// <summary>
    /// Sonraki hedef veya gÃ¶rev aÃ§Ä±klamasÄ±.
    /// </summary>
    public string? NextObjective { get; set; }

    /// <summary>
    /// Hedefe kalan yaklaÅŸÄ±k mesafe.
    /// </summary>
    public double? RemainingDistanceMeters { get; set; }

    /// <summary>
    /// GÃ¶rev baÅŸlangÄ±Ã§ zamanÄ±.
    /// </summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>
    /// GÃ¶rev bitiÅŸ zamanÄ±.
    /// </summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>
    /// GÃ¶revle iliÅŸkili uyarÄ±lar.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Veri tazelik Ã¶zeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}
