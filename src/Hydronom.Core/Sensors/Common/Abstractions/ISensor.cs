using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Abstractions
{
    /// <summary>
    /// Hydronom sensÃ¶r soyutlamasÄ±.
    ///
    /// Bu arayÃ¼z gerÃ§ek cihaz, sim sensÃ¶r veya replay sensÃ¶rÃ¼ olabilir.
    /// </summary>
    public interface ISensor
    {
        SensorIdentity Identity { get; }

        SensorSourceInfo Source { get; }

        SensorCapabilitySet Capabilities { get; }

        SensorHealthSnapshot GetHealthSnapshot();
    }
}

