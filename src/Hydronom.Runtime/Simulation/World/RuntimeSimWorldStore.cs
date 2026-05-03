using System;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Simulation.World;

namespace Hydronom.Runtime.Simulation.World
{
    /// <summary>
    /// Runtime tarafÄ±nda thread-safe simÃ¼lasyon dÃ¼nyasÄ± deposu.
    ///
    /// Bu store:
    /// - Runtime loop
    /// - physics adapter
    /// - sim sensor backends
    /// - telemetry projector
    /// - Gateway / Ops bridge
    ///
    /// tarafÄ±ndan ortak dÃ¼nya state'i okumak ve gÃ¼ncellemek iÃ§in kullanÄ±labilir.
    /// </summary>
    public sealed class RuntimeSimWorldStore : ISimWorldMutableStore
    {
        private readonly object _lock = new();

        private RuntimeSimWorld _runtimeWorld;

        public RuntimeSimWorldStore(RuntimeSimWorld? initialWorld = null)
        {
            _runtimeWorld = (initialWorld ?? RuntimeSimWorld.CreateDefaultMarine()).Sanitized();
        }

        public string ProviderName => "RuntimeSimWorldStore";

        public bool IsAvailable => _runtimeWorld.Enabled;

        public DateTime LastWorldUpdateUtc => _runtimeWorld.TimestampUtc;

        public RuntimeSimWorld GetRuntimeWorld()
        {
            lock (_lock)
            {
                return _runtimeWorld.Sanitized();
            }
        }

        public void SetRuntimeWorld(RuntimeSimWorld runtimeWorld)
        {
            lock (_lock)
            {
                _runtimeWorld = runtimeWorld.Sanitized();
            }
        }

        public SimWorldState GetLatestWorld()
        {
            lock (_lock)
            {
                return _runtimeWorld.World.Sanitized();
            }
        }

        public SimWorldSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return SimWorldSnapshot.FromWorld(
                    providerName: ProviderName,
                    isAvailable: IsAvailable,
                    world: _runtimeWorld.World
                );
            }
        }

        public ValueTask<SimWorldState> GetLatestWorldAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GetLatestWorld());
        }

        public void SetWorld(SimWorldState world)
        {
            lock (_lock)
            {
                _runtimeWorld = _runtimeWorld.WithWorld(world);
            }
        }

        public void AddOrUpdateObject(SimWorldObject obj, string? layerId = null)
        {
            lock (_lock)
            {
                _runtimeWorld = _runtimeWorld.WithObject(obj, layerId);
            }
        }

        public bool RemoveObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return false;

            lock (_lock)
            {
                var before = _runtimeWorld.World.Objects.Count;
                _runtimeWorld = _runtimeWorld.WithoutObject(objectId);
                var after = _runtimeWorld.World.Objects.Count;

                return after < before;
            }
        }

        public void AddOrUpdateLayer(SimWorldLayer layer)
        {
            lock (_lock)
            {
                var safeWorld = _runtimeWorld.World.Sanitized();
                var safeLayer = layer.Sanitized();

                var layers = new System.Collections.Generic.List<SimWorldLayer>();

                foreach (var existing in safeWorld.Layers)
                {
                    if (!string.Equals(existing.LayerId, safeLayer.LayerId, StringComparison.OrdinalIgnoreCase))
                        layers.Add(existing);
                }

                layers.Add(safeLayer);

                _runtimeWorld = _runtimeWorld.WithWorld(
                    safeWorld with
                    {
                        TimestampUtc = DateTime.UtcNow,
                        Layers = layers.ToArray()
                    }
                );
            }
        }

        public bool RemoveLayer(string layerId)
        {
            if (string.IsNullOrWhiteSpace(layerId))
                return false;

            lock (_lock)
            {
                var safeWorld = _runtimeWorld.World.Sanitized();
                var before = safeWorld.Layers.Count;

                var layers = new System.Collections.Generic.List<SimWorldLayer>();

                foreach (var layer in safeWorld.Layers)
                {
                    if (!string.Equals(layer.LayerId, layerId.Trim(), StringComparison.OrdinalIgnoreCase))
                        layers.Add(layer);
                }

                _runtimeWorld = _runtimeWorld.WithWorld(
                    safeWorld with
                    {
                        TimestampUtc = DateTime.UtcNow,
                        Layers = layers.ToArray()
                    }
                );

                return layers.Count < before;
            }
        }

        public ValueTask SetWorldAsync(SimWorldState world, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetWorld(world);
            return ValueTask.CompletedTask;
        }

        public ValueTask AddOrUpdateObjectAsync(
            SimWorldObject obj,
            string? layerId = null,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddOrUpdateObject(obj, layerId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> RemoveObjectAsync(string objectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(RemoveObject(objectId));
        }
    }
}
