using System;

namespace Hydronom.Runtime.Planning;

/// <summary>
/// Runtime planning çıktısını thread-safe şekilde saklar.
/// 
/// Planning scheduler slot'u bu cache'i günceller.
/// Decision scheduler slot'u buradan son trajectory planını okuyabilir.
/// </summary>
public sealed class RuntimePlanningCache
{
    private readonly object _gate = new();

    private RuntimePlanningSnapshot _snapshot = RuntimePlanningSnapshot.Empty;
    private long _version;

    public void Update(RuntimePlanningSnapshot snapshot)
    {
        lock (_gate)
        {
            _version++;

            _snapshot = (snapshot ?? RuntimePlanningSnapshot.Empty) with
            {
                Version = _version,
                TimestampUtc = DateTime.UtcNow
            };

            _snapshot = _snapshot.Sanitized();
        }
    }

    public void SetError(string error)
    {
        lock (_gate)
        {
            _version++;

            _snapshot = RuntimePlanningSnapshot.Empty with
            {
                Version = _version,
                TimestampUtc = DateTime.UtcNow,
                HasPlan = false,
                IsValid = false,
                Summary = "PLANNING_ERROR",
                Error = string.IsNullOrWhiteSpace(error) ? "Unknown planning error." : error.Trim()
            };
        }
    }

    public RuntimePlanningSnapshot Snapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }
}