using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Yer istasyonu seviyesinde araç bağlantı kalitesini takip eder.
/// Bu sınıf şimdilik metrik toplar; ileride CommunicationRouter ve Diagnostics bunu karar girdisi olarak kullanacaktır.
/// </summary>
public sealed class LinkHealthTracker
{
    private readonly Dictionary<string, VehicleLinkHealth> _vehicles = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan LostAfter { get; set; } = TimeSpan.FromSeconds(30);

    public VehicleLinkHealth GetOrCreateVehicle(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
            throw new ArgumentException("VehicleId boş olamaz.", nameof(vehicleId));

        if (_vehicles.TryGetValue(vehicleId, out var existing))
            return existing;

        var created = new VehicleLinkHealth(vehicleId);
        _vehicles[vehicleId] = created;
        return created;
    }

    public void MarkSeen(string vehicleId, TransportKind transportKind, DateTime nowUtc)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.MarkSeen(nowUtc);
    }

    public void RecordSend(string vehicleId, TransportKind transportKind, DateTime nowUtc)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.RecordSend(nowUtc);
    }

    public void RecordRouteSuccess(
        string vehicleId,
        TransportKind transportKind,
        DateTime nowUtc,
        double? latencyMs = null)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.RecordRouteSuccess(nowUtc, latencyMs);
    }

    public void RecordRouteFailure(
        string vehicleId,
        TransportKind transportKind,
        DateTime nowUtc)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.RecordRouteFailure(nowUtc);
    }

    public void RecordAck(
        string vehicleId,
        TransportKind transportKind,
        DateTime nowUtc,
        double? latencyMs = null)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.RecordAck(nowUtc, latencyMs);
    }

    public void RecordTimeout(
        string vehicleId,
        TransportKind transportKind,
        DateTime nowUtc)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.RecordTimeout(nowUtc);
    }

    public void RecordEstimatedPacketLoss(
        string vehicleId,
        TransportKind transportKind,
        DateTime nowUtc,
        int lostPacketCount = 1)
    {
        var vehicle = GetOrCreateVehicle(vehicleId);
        var link = vehicle.GetOrCreateLink(transportKind, nowUtc);
        link.RecordEstimatedPacketLoss(nowUtc, lostPacketCount);
    }

    public TransportLinkMetrics? GetBestAvailableLink(string vehicleId)
    {
        if (!_vehicles.TryGetValue(vehicleId, out var vehicle))
            return null;

        return vehicle.GetBestAvailableLink();
    }

    public IReadOnlyList<TransportLinkMetrics> GetAvailableLinks(string vehicleId)
    {
        if (!_vehicles.TryGetValue(vehicleId, out var vehicle))
            return Array.Empty<TransportLinkMetrics>();

        return vehicle.GetAvailableLinks();
    }

    public void RefreshAll(DateTime nowUtc)
    {
        foreach (var vehicle in _vehicles.Values)
            vehicle.Refresh(nowUtc, LostAfter);
    }

    public IReadOnlyList<VehicleLinkHealthSnapshot> GetSnapshot(DateTime nowUtc)
    {
        RefreshAll(nowUtc);

        return _vehicles.Values
            .OrderBy(x => x.VehicleId, StringComparer.OrdinalIgnoreCase)
            .Select(vehicle => new VehicleLinkHealthSnapshot(
                vehicle.VehicleId,
                vehicle.OverallStatus,
                vehicle.OverallQualityScore,
                vehicle.Links
                    .OrderByDescending(link => link.QualityScore)
                    .Select(link => new LinkHealthSnapshot(
                        link.VehicleId,
                        link.TransportKind,
                        link.Status,
                        link.QualityScore,
                        link.SuccessRate,
                        link.FailureRate,
                        link.TimeoutRate,
                        link.LastLatencyMs,
                        link.AverageLatencyMs,
                        link.SentCount,
                        link.SuccessCount,
                        link.FailureCount,
                        link.AckCount,
                        link.TimeoutCount,
                        link.LostPacketEstimateCount,
                        link.LastSeenUtc,
                        link.LastUpdatedUtc))
                    .ToArray()))
            .ToArray();
    }
}