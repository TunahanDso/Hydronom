using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hydronom.Core.Simulation.Truth
{
    /// <summary>
    /// Sim sensÃ¶rlerin fizik motorundan truth state okuyabilmesi iÃ§in ortak sÃ¶zleÅŸme.
    ///
    /// Sim IMU, Sim GPS, Sim LiDAR ve Sim Camera kendi kafasÄ±na gÃ¶re veri Ã¼retmemelidir.
    /// Bu provider Ã¼zerinden PhysicsTruthState okuyup gerÃ§ek sensÃ¶r gibi Ã¶lÃ§Ã¼m Ã¼retmelidir.
    /// </summary>
    public interface IPhysicsTruthProvider
    {
        string ProviderName { get; }

        bool IsAvailable { get; }

        DateTime LastTruthUtc { get; }

        PhysicsTruthState GetLatestTruth();

        ValueTask<PhysicsTruthState> GetLatestTruthAsync(CancellationToken cancellationToken = default);
    }
}
