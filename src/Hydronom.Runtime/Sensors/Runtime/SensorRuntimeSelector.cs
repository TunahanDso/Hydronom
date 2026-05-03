using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.PythonBackup;

namespace Hydronom.Runtime.Sensors.Runtime;

/// <summary>
/// Sensör runtime seçici.
/// 
/// Bu sınıf artık sadece "hangi runtime sınıfı new'lenecek?" sorusunu cevaplamaz.
/// Aynı zamanda CSharpPrimary modda registry + builder hattını kullanarak
/// backend'leri otomatik bağlanmış bir runtime üretir.
/// 
/// Normal hedef:
/// - CSharpPrimary
/// 
/// PythonBackup:
/// - Yalnızca legacy/fallback modudur.
/// - Normal authority değildir.
/// </summary>
public sealed class SensorRuntimeSelector
{
    private readonly SensorRuntimeOptions _options;
    private readonly IServiceProvider? _services;

    public SensorRuntimeSelector(
        SensorRuntimeOptions? options = null,
        IServiceProvider? services = null)
    {
        _options = options ?? SensorRuntimeOptions.Default();
        _services = services;
    }

    /// <summary>
    /// Seçili moda göre sensör runtime oluşturur.
    ///
    /// CSharpPrimary ve CompareOnly modlarında artık boş CSharpSensorRuntime dönmez.
    /// Registry kurulur, varsayılan sim backend'ler kaydedilir ve builder üzerinden runtime oluşturulur.
    /// </summary>
    public ISensorRuntime CreateRuntime()
    {
        return _options.Mode switch
        {
            SensorRuntimeMode.CSharpPrimary => CreateCSharpPrimaryRuntime(),
            SensorRuntimeMode.CompareOnly => CreateCSharpPrimaryRuntime(),
            SensorRuntimeMode.PythonBackup => CreatePythonBackupOrThrow(),
            SensorRuntimeMode.Disabled => new DisabledSensorRuntime(),
            _ => CreateCSharpPrimaryRuntime()
        };
    }

    /// <summary>
    /// C# Primary sensör runtime oluşturur.
    ///
    /// Bu aşamada:
    /// - SensorBackendRegistry kurulur
    /// - sim_imu ve sim_gps varsayılan backend olarak kaydedilir
    /// - SensorRuntimeBuilder options'a göre backend'leri runtime'a ekler
    ///
    /// Böylece EnableDefaultSimSensors=true, EnableImu=true, EnableGps=true iken
    /// CSharpSensorRuntime otomatik olarak SimImuSensor + SimGpsSensor ile başlar.
    /// </summary>
    private ISensorRuntime CreateCSharpPrimaryRuntime()
    {
        var registry = new SensorBackendRegistry()
            .RegisterDefaultSimulationBackends();

        var builder = new SensorRuntimeBuilder(
            registry: registry,
            services: _services
        );

        return builder.Build(_options);
    }

    /// <summary>
    /// Python backup runtime oluşturur.
    ///
    /// Python artık normal çalışma modunun ana authority kaynağı değildir.
    /// Sadece açıkça PythonBackup modu seçildiğinde kullanılabilir.
    /// </summary>
    private ISensorRuntime CreatePythonBackupOrThrow()
    {
        if (!_options.PythonBackupEnabled)
            throw new InvalidOperationException("PythonBackup modu istendi ancak PythonBackupEnabled=false.");

        return new PythonBackupSensorRuntime();
    }
}

/// <summary>
/// Sensör runtime'ın tamamen devre dışı olduğu mod.
/// 
/// Dry-run, güvenli başlatma veya sensörsüz test senaryolarında kullanılabilir.
/// </summary>
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