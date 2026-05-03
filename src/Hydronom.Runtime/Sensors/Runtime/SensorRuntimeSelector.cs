using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Runtime.Sensors.PythonBackup;

namespace Hydronom.Runtime.Sensors.Runtime;

/// <summary>
/// Sensör runtime seçici.
/// CSharpPrimary normal moddur; PythonBackup yalnızca legacy/fallback içindir.
/// </summary>
public sealed class SensorRuntimeSelector
{
    private readonly SensorRuntimeOptions _options;

    public SensorRuntimeSelector(SensorRuntimeOptions? options = null)
    {
        _options = options ?? SensorRuntimeOptions.Default();
    }

    public ISensorRuntime CreateRuntime()
    {
        return _options.Mode switch
        {
            SensorRuntimeMode.CSharpPrimary => new CSharpSensorRuntime(_options),
            SensorRuntimeMode.PythonBackup => CreatePythonBackupOrThrow(),
            SensorRuntimeMode.CompareOnly => new CSharpSensorRuntime(_options),
            SensorRuntimeMode.Disabled => new DisabledSensorRuntime(),
            _ => new CSharpSensorRuntime(_options)
        };
    }

    private ISensorRuntime CreatePythonBackupOrThrow()
    {
        if (!_options.PythonBackupEnabled)
            throw new InvalidOperationException("PythonBackup modu istendi ancak PythonBackupEnabled=false.");

        return new PythonBackupSensorRuntime();
    }
}

internal sealed class DisabledSensorRuntime : ISensorRuntime
{
    public SensorRuntimeMode Mode => SensorRuntimeMode.Disabled;

    public bool IsRunning { get; private set; }

    public SensorCapabilitySet Capabilities => SensorCapabilitySet.Empty;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
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
            SensorRuntimeMode.Disabled,
            Array.Empty<SensorHealthSnapshot>()
        );
    }
}