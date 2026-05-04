using Hydronom.Core.World.Diagnostics;
using Hydronom.Core.World.Models;

namespace Hydronom.Runtime.World.Runtime;

/// <summary>
/// Runtime tarafında kullanılan canlı dünya modeli.
/// Şimdilik thread-safe basit object store görevi görür.
/// </summary>
public sealed class RuntimeWorldModel
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HydronomWorldObject> _objects = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset UpdatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _objects.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _objects.Clear();
            UpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    public void Upsert(HydronomWorldObject worldObject)
    {
        if (string.IsNullOrWhiteSpace(worldObject.Id))
        {
            throw new ArgumentException("World object Id boş olamaz.", nameof(worldObject));
        }

        lock (_gate)
        {
            _objects[worldObject.Id] = worldObject;
            UpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    public void UpsertMany(IEnumerable<HydronomWorldObject> objects)
    {
        lock (_gate)
        {
            foreach (var worldObject in objects)
            {
                if (string.IsNullOrWhiteSpace(worldObject.Id))
                {
                    continue;
                }

                _objects[worldObject.Id] = worldObject;
            }

            UpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    public IReadOnlyList<HydronomWorldObject> Snapshot()
    {
        lock (_gate)
        {
            return _objects.Values
                .OrderBy(x => x.Layer, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyList<HydronomWorldObject> ActiveObjects()
    {
        lock (_gate)
        {
            return _objects.Values
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public WorldDiagnostics GetDiagnostics()
    {
        lock (_gate)
        {
            return new WorldDiagnostics
            {
                ObjectCount = _objects.Count,
                ActiveObjectCount = _objects.Values.Count(x => x.IsActive),
                BlockingObjectCount = _objects.Values.Count(x => x.IsActive && x.IsObstacleLike),
                UpdatedUtc = UpdatedUtc
            };
        }
    }
}