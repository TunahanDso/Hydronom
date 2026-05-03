using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Runtime.Sensors.Supervision;

/// <summary>
/// Sensör runtime sağlık denetleyicisi.
/// Tek tek sensör health snapshot'larını Core diagnostics modeline toplar.
/// </summary>
public sealed class SensorSupervisor
{
    public SensorRuntimeHealth Evaluate(
        SensorRuntimeMode mode,
        IReadOnlyList<SensorHealthSnapshot> sensors)
    {
        return SensorRuntimeHealth.FromSensors(mode, sensors);
    }
}