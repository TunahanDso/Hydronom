using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Bir aracın tüm transport bağlantılarını tek yerde tutar.
/// Örn: Alpha'nın WiFi, RF, LoRa bağlantılarının birlikte değerlendirilmesi.
/// </summary>
public sealed class VehicleLinkHealth
{
    private readonly Dictionary<TransportKind, TransportLinkMetrics> _links = new();

    public VehicleLinkHealth(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
            throw new ArgumentException("VehicleId boş olamaz.", nameof(vehicleId));

        VehicleId = vehicleId;
    }

    public string VehicleId { get; }

    public IReadOnlyCollection<TransportLinkMetrics> Links => _links.Values.ToArray();

    public double OverallQualityScore
    {
        get
        {
            if (_links.Count == 0)
                return 0.0;

            return _links.Values.Max(x => x.QualityScore);
        }
    }

    public LinkHealthStatus OverallStatus
    {
        get
        {
            if (_links.Count == 0)
                return LinkHealthStatus.Unknown;

            if (_links.Values.Any(x => x.Status == LinkHealthStatus.Good))
                return LinkHealthStatus.Good;

            if (_links.Values.Any(x => x.Status == LinkHealthStatus.Degraded))
                return LinkHealthStatus.Degraded;

            if (_links.Values.Any(x => x.Status == LinkHealthStatus.Critical))
                return LinkHealthStatus.Critical;

            return LinkHealthStatus.Lost;
        }
    }

    public TransportLinkMetrics GetOrCreateLink(TransportKind transportKind, DateTime nowUtc)
    {
        if (_links.TryGetValue(transportKind, out var existing))
            return existing;

        var created = new TransportLinkMetrics(VehicleId, transportKind, nowUtc);
        _links[transportKind] = created;
        return created;
    }

    public TransportLinkMetrics? GetLink(TransportKind transportKind)
    {
        return _links.TryGetValue(transportKind, out var link) ? link : null;
    }

    public TransportLinkMetrics? GetBestAvailableLink()
    {
        return _links.Values
            .Where(x => x.IsAvailable)
            .OrderByDescending(x => x.QualityScore)
            .ThenBy(x => x.AverageLatencyMs ?? double.MaxValue)
            .FirstOrDefault();
    }

    public IReadOnlyList<TransportLinkMetrics> GetAvailableLinks()
    {
        return _links.Values
            .Where(x => x.IsAvailable)
            .OrderByDescending(x => x.QualityScore)
            .ToArray();
    }

    public void Refresh(DateTime nowUtc, TimeSpan lostAfter)
    {
        foreach (var link in _links.Values)
            link.RefreshStatus(nowUtc, lostAfter);
    }
}