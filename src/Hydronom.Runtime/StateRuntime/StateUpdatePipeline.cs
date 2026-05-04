using Hydronom.Core.State.Authority;

namespace Hydronom.Runtime.StateRuntime;

/// <summary>
/// Fusion/External/Replay kaynaklarından gelen StateUpdateCandidate modellerini
/// StateAuthorityManager'a geçirir ve sonucu VehicleStateStore'a uygular.
/// </summary>
public sealed class StateUpdatePipeline
{
    private readonly StateAuthorityManager _authorityManager;
    private readonly VehicleStateStore _store;

    public StateUpdatePipeline(
        StateAuthorityManager authorityManager,
        VehicleStateStore store)
    {
        _authorityManager = authorityManager ?? throw new ArgumentNullException(nameof(authorityManager));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public StateUpdateResult Submit(
        StateUpdateCandidate candidate,
        DateTime? utcNow = null)
    {
        var current = _store.Current;
        var result = _authorityManager.Evaluate(current, candidate, utcNow);

        _store.Apply(result);

        return result;
    }
}