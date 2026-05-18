namespace Hydronom.Core.Communication.Security;

public sealed class AntiReplayWindow
{
    private readonly object _gate = new();

    private readonly Dictionary<string, ulong> _lastSequenceBySource = new();

    private readonly Dictionary<string, DateTimeOffset> _lastSeenBySource = new();

    public HydronomSecurityResult CheckAndRemember(
        string sourceId,
        ulong sequence,
        long timestampUnixMs,
        HydronomSecurityProfile profile)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return HydronomSecurityResult.Reject(
                sourceId,
                sequence,
                "SOURCE_ID_EMPTY",
                "Kaynak kimliği boş olduğu için mesaj reddedildi.");
        }

        var now = DateTimeOffset.UtcNow;
        var messageTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampUnixMs);

        if (profile.RequireFreshTimestamp)
        {
            var skew = (now - messageTime).Duration();

            if (skew > profile.MaxClockSkew)
            {
                return HydronomSecurityResult.Reject(
                    sourceId,
                    sequence,
                    "STALE_OR_FUTURE_MESSAGE",
                    $"Mesaj zamanı kabul edilen sınırın dışında. SkewMs={skew.TotalMilliseconds:0}");
            }
        }

        lock (_gate)
        {
            if (profile.RequireMonotonicSequence &&
                _lastSequenceBySource.TryGetValue(sourceId, out var lastSequence) &&
                sequence <= lastSequence)
            {
                return HydronomSecurityResult.Reject(
                    sourceId,
                    sequence,
                    "REPLAY_DETECTED",
                    $"Sequence eski veya tekrarlandı. Last={lastSequence}, Current={sequence}");
            }

            _lastSequenceBySource[sourceId] = sequence;
            _lastSeenBySource[sourceId] = now;
        }

        return HydronomSecurityResult.Accept(sourceId, sequence);
    }

    public IReadOnlyDictionary<string, ulong> SnapshotLastSequences()
    {
        lock (_gate)
        {
            return new Dictionary<string, ulong>(_lastSequenceBySource);
        }
    }

    public void ForgetSource(string sourceId)
    {
        lock (_gate)
        {
            _lastSequenceBySource.Remove(sourceId);
            _lastSeenBySource.Remove(sourceId);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _lastSequenceBySource.Clear();
            _lastSeenBySource.Clear();
        }
    }
}