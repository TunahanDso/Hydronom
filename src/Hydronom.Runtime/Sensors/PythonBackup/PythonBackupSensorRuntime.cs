using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Runtime.Sensors.PythonBackup;

/// <summary>
/// Eski Python sensör/fusion hattı için backup runtime iskeleti.
/// Normal modda authority sahibi değildir.
/// </summary>
public sealed class PythonBackupSensorRuntime : ISensorRuntime
{
    private bool _isRunning;

    public SensorRuntimeMode Mode => SensorRuntimeMode.PythonBackup;

    public bool IsRunning => _isRunning;

    public SensorCapabilitySet Capabilities => SensorCapabilitySet.Empty;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<SensorSample>> ReadBatchAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SensorSample> empty = Array.Empty<SensorSample>();
        return ValueTask.FromResult(empty);
    }

    public SensorRuntimeHealth GetHealth()
    {
        return SensorRuntimeHealth.FromSensors(
            SensorRuntimeMode.PythonBackup,
            Array.Empty<SensorHealthSnapshot>()
        );
    }
}