using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Belirli bir araÃ§ ve belirli bir transport tÃ¼rÃ¼ iÃ§in baÄŸlantÄ± metriklerini tutar.
/// Ã–rn: VEHICLE-ALPHA-001 + WiFi, VEHICLE-ALPHA-001 + LoRa.
/// </summary>
public sealed class TransportLinkMetrics
{
    public TransportLinkMetrics(
        string vehicleId,
        TransportKind transportKind,
        DateTime firstSeenUtc)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
            throw new ArgumentException("VehicleId boÅŸ olamaz.", nameof(vehicleId));

        VehicleId = vehicleId;
        TransportKind = transportKind;
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = firstSeenUtc;
        LastUpdatedUtc = firstSeenUtc;
    }

    public string VehicleId { get; }

    public TransportKind TransportKind { get; }

    public DateTime FirstSeenUtc { get; }

    public DateTime LastSeenUtc { get; private set; }

    public DateTime LastUpdatedUtc { get; private set; }

    public DateTime? LastRouteSuccessUtc { get; private set; }

    public DateTime? LastRouteFailureUtc { get; private set; }

    public DateTime? LastAckUtc { get; private set; }

    public double? LastLatencyMs { get; private set; }

    public double? AverageLatencyMs { get; private set; }

    public int SentCount { get; private set; }

    public int SuccessCount { get; private set; }

    public int FailureCount { get; private set; }

    public int AckCount { get; private set; }

    public int TimeoutCount { get; private set; }

    public int LostPacketEstimateCount { get; private set; }

    public double SuccessRate
    {
        get
        {
            var total = SuccessCount + FailureCount;
            if (total <= 0)
                return 1.0;

            return SuccessCount / (double)total;
        }
    }

    public double FailureRate
    {
        get
        {
            var total = SuccessCount + FailureCount;
            if (total <= 0)
                return 0.0;

            return FailureCount / (double)total;
        }
    }

    public double TimeoutRate
    {
        get
        {
            if (SentCount <= 0)
                return 0.0;

            return TimeoutCount / (double)SentCount;
        }
    }

    /// <summary>
    /// 0-100 arasÄ± baÄŸlantÄ± kalite skoru.
    /// 100 mÃ¼kemmel, 0 kullanÄ±lamaz anlamÄ±na gelir.
    /// </summary>
    public double QualityScore { get; private set; } = 100.0;

    public LinkHealthStatus Status { get; private set; } = LinkHealthStatus.Unknown;

    public bool IsAvailable => Status is LinkHealthStatus.Good or LinkHealthStatus.Degraded;

    public void MarkSeen(DateTime nowUtc)
    {
        LastSeenUtc = nowUtc;
        LastUpdatedUtc = nowUtc;
        Recalculate(nowUtc);
    }

    public void RecordSend(DateTime nowUtc)
    {
        SentCount++;
        LastUpdatedUtc = nowUtc;
        Recalculate(nowUtc);
    }

    public void RecordRouteSuccess(DateTime nowUtc, double? latencyMs = null)
    {
        SuccessCount++;
        LastRouteSuccessUtc = nowUtc;
        LastUpdatedUtc = nowUtc;

        if (latencyMs.HasValue)
            UpdateLatency(latencyMs.Value);

        Recalculate(nowUtc);
    }

    public void RecordRouteFailure(DateTime nowUtc)
    {
        FailureCount++;
        LastRouteFailureUtc = nowUtc;
        LastUpdatedUtc = nowUtc;
        Recalculate(nowUtc);
    }

    public void RecordAck(DateTime nowUtc, double? latencyMs = null)
    {
        AckCount++;
        LastAckUtc = nowUtc;
        LastUpdatedUtc = nowUtc;

        if (latencyMs.HasValue)
            UpdateLatency(latencyMs.Value);

        Recalculate(nowUtc);
    }

    public void RecordTimeout(DateTime nowUtc)
    {
        TimeoutCount++;
        FailureCount++;
        LastRouteFailureUtc = nowUtc;
        LastUpdatedUtc = nowUtc;
        Recalculate(nowUtc);
    }

    public void RecordEstimatedPacketLoss(DateTime nowUtc, int lostPacketCount = 1)
    {
        if (lostPacketCount <= 0)
            return;

        LostPacketEstimateCount += lostPacketCount;
        LastUpdatedUtc = nowUtc;
        Recalculate(nowUtc);
    }

    public void RefreshStatus(DateTime nowUtc, TimeSpan lostAfter)
    {
        if (nowUtc - LastSeenUtc > lostAfter)
        {
            QualityScore = Math.Min(QualityScore, 10.0);
            Status = LinkHealthStatus.Lost;
            LastUpdatedUtc = nowUtc;
            return;
        }

        Recalculate(nowUtc);
    }

    private void UpdateLatency(double latencyMs)
    {
        if (latencyMs < 0)
            latencyMs = 0;

        LastLatencyMs = latencyMs;

        if (!AverageLatencyMs.HasValue)
        {
            AverageLatencyMs = latencyMs;
            return;
        }

        // Basit hareketli ortalama yaklaÅŸÄ±mÄ±.
        // Ä°leride EWMA parametresi konfigÃ¼rasyona alÄ±nabilir.
        AverageLatencyMs = (AverageLatencyMs.Value * 0.8) + (latencyMs * 0.2);
    }

    private void Recalculate(DateTime nowUtc)
    {
        var score = 100.0;

        score -= FailureRate * 35.0;
        score -= TimeoutRate * 30.0;

        if (AverageLatencyMs.HasValue)
        {
            if (AverageLatencyMs.Value > 1000)
                score -= 25.0;
            else if (AverageLatencyMs.Value > 500)
                score -= 15.0;
            else if (AverageLatencyMs.Value > 250)
                score -= 7.5;
        }

        if (LostPacketEstimateCount > 0 && SentCount > 0)
        {
            var lossRate = Math.Min(1.0, LostPacketEstimateCount / (double)Math.Max(1, SentCount + LostPacketEstimateCount));
            score -= lossRate * 25.0;
        }

        var silenceSeconds = (nowUtc - LastSeenUtc).TotalSeconds;

        if (silenceSeconds > 30)
            score -= 35.0;
        else if (silenceSeconds > 15)
            score -= 20.0;
        else if (silenceSeconds > 5)
            score -= 8.0;

        QualityScore = Math.Clamp(score, 0.0, 100.0);

        Status = QualityScore switch
        {
            >= 75.0 => LinkHealthStatus.Good,
            >= 45.0 => LinkHealthStatus.Degraded,
            >= 15.0 => LinkHealthStatus.Critical,
            _ => LinkHealthStatus.Lost
        };
    }
}
