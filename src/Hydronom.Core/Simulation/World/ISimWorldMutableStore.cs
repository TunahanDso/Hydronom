using System.Threading;
using System.Threading.Tasks;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nÄ± gÃ¼ncelleyebilen store sÃ¶zleÅŸmesi.
    ///
    /// Runtime tarafÄ± gÃ¶rev nesnesi ekleme, engel ekleme, zone gÃ¼ncelleme,
    /// mission editor deÄŸiÅŸiklikleri ve scenario loading iÅŸlemlerini bu arayÃ¼zle yapabilir.
    /// </summary>
    public interface ISimWorldMutableStore : ISimWorldProvider
    {
        void SetWorld(SimWorldState world);

        void AddOrUpdateObject(SimWorldObject obj, string? layerId = null);

        bool RemoveObject(string objectId);

        void AddOrUpdateLayer(SimWorldLayer layer);

        bool RemoveLayer(string layerId);

        ValueTask SetWorldAsync(SimWorldState world, CancellationToken cancellationToken = default);

        ValueTask AddOrUpdateObjectAsync(
            SimWorldObject obj,
            string? layerId = null,
            CancellationToken cancellationToken = default
        );

        ValueTask<bool> RemoveObjectAsync(string objectId, CancellationToken cancellationToken = default);
    }
}
