using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;
using Hydronom.Core.State.Store;

namespace Hydronom.Runtime.StateRuntime;

/// <summary>
/// Authoritative VehicleOperationalState deposu.
/// Kabul edilen update'lerde current state güncellenir.
/// Reddedilen update'lerde current state korunur, fakat karar history'ye yazılır.
/// </summary>
public sealed class VehicleStateStore
{
    private readonly object _gate = new();
    private readonly int _maxHistoryCount;

    private VehicleOperationalState _currentState;
    private bool _hasState;
    private int _acceptedUpdateCount;
    private int _rejectedUpdateCount;
    private StateUpdateResult? _lastResult;
    private readonly Queue<StateUpdateResult> _recentResults = new();

    public VehicleStateStore(
        string vehicleId,
        StateAuthorityMode authorityMode = StateAuthorityMode.CSharpPrimary,
        int maxHistoryCount = 32)
    {
        _currentState = VehicleOperationalState.CreateInitial(vehicleId, authorityMode).Sanitized();
        _hasState = true;
        _maxHistoryCount = maxHistoryCount <= 0 ? 32 : maxHistoryCount;
    }

    public VehicleOperationalState Current
    {
        get
        {
            lock (_gate)
            {
                return _currentState.Sanitized();
            }
        }
    }

    public bool HasState
    {
        get
        {
            lock (_gate)
            {
                return _hasState;
            }
        }
    }

    public int AcceptedUpdateCount
    {
        get
        {
            lock (_gate)
            {
                return _acceptedUpdateCount;
            }
        }
    }

    public int RejectedUpdateCount
    {
        get
        {
            lock (_gate)
            {
                return _rejectedUpdateCount;
            }
        }
    }

    public StateUpdateResult? LastResult
    {
        get
        {
            lock (_gate)
            {
                return _lastResult;
            }
        }
    }

    public void Apply(StateUpdateResult result)
    {
        lock (_gate)
        {
            if (result.Accepted)
            {
                _currentState = result.StateAfter.Sanitized();
                _hasState = true;
                _acceptedUpdateCount++;
            }
            else
            {
                _rejectedUpdateCount++;
            }

            _lastResult = result;
            _recentResults.Enqueue(result);

            while (_recentResults.Count > _maxHistoryCount)
            {
                _recentResults.Dequeue();
            }
        }
    }

    public VehicleStateStoreSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var recent = _recentResults.ToArray();

            return new VehicleStateStoreSnapshot(
                VehicleId: _currentState.VehicleId,
                TimestampUtc: DateTime.UtcNow,
                CurrentState: _currentState,
                HasState: _hasState,
                AcceptedUpdateCount: _acceptedUpdateCount,
                RejectedUpdateCount: _rejectedUpdateCount,
                LastResult: _lastResult,
                RecentResults: recent,
                Summary: BuildSummary()
            ).Sanitized();
        }
    }

    private string BuildSummary()
    {
        var last = _lastResult is null
            ? "none"
            : $"{_lastResult.Value.Decision} accepted={_lastResult.Value.Accepted}";

        return $"Vehicle={_currentState.VehicleId}, accepted={_acceptedUpdateCount}, rejected={_rejectedUpdateCount}, last={last}";
    }
}