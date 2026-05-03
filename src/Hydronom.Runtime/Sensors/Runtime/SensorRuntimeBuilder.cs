using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.PythonBackup;

namespace Hydronom.Runtime.Sensors.Runtime;

/// <summary>
/// Sensör runtime oluşturucu.
///
/// Görevi:
/// - SensorRuntimeOptions'a bakmak
/// - Doğru runtime modunu seçmek
/// - CSharpPrimary modunda registry üzerinden backend'leri oluşturmak
/// - PythonBackup ve Disabled modlarını ayrı tutmak
///
/// Bu sınıf sensör okumaz.
/// Sadece runtime nesnesini kurar.
/// </summary>
public sealed class SensorRuntimeBuilder
{
    private readonly SensorBackendRegistry _registry;
    private readonly IServiceProvider? _services;

    public SensorRuntimeBuilder(
        SensorBackendRegistry registry,
        IServiceProvider? services = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _services = services;
    }

    /// <summary>
    /// Options'a göre ana runtime'ı kurar.
    /// </summary>
    public ISensorRuntime Build(SensorRuntimeOptions? options = null)
    {
        var safeOptions = options ?? SensorRuntimeOptions.Default();

        return safeOptions.Mode switch
        {
            SensorRuntimeMode.CSharpPrimary => BuildCSharpPrimary(safeOptions),
            SensorRuntimeMode.CompareOnly => BuildCSharpPrimary(safeOptions),
            SensorRuntimeMode.PythonBackup => BuildPythonBackup(safeOptions),
            SensorRuntimeMode.Disabled => BuildDisabled(),
            _ => BuildCSharpPrimary(safeOptions)
        };
    }

    /// <summary>
    /// C# Primary runtime kurar.
    ///
    /// Registry içinde kayıtlı backend anahtarlarına göre runtime içine backend ekler.
    /// Şimdilik anahtarları options üzerindeki EnableDefaultSimSensors / EnableImu / EnableGps / EnableLidar
    /// bayraklarından çözüyoruz.
    /// </summary>
    public CSharpSensorRuntime BuildCSharpPrimary(SensorRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runtime = new CSharpSensorRuntime(options);

        foreach (var backendKey in ResolveBackendKeys(options))
        {
            var backend = _registry.Create(backendKey, _services);
            runtime.AddBackend(backend);
        }

        return runtime;
    }

    /// <summary>
    /// Python backup runtime kurar.
    ///
    /// Python artık normal authority değildir.
    /// Sadece açıkça PythonBackup seçildiğinde bu runtime döner.
    /// </summary>
    public ISensorRuntime BuildPythonBackup(SensorRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.PythonBackupEnabled)
            throw new InvalidOperationException("PythonBackup modu istendi ancak PythonBackupEnabled=false.");

        return new PythonBackupSensorRuntime();
    }

    /// <summary>
    /// Sensör runtime tamamen kapalı mod.
    /// </summary>
    public ISensorRuntime BuildDisabled()
    {
        return new DisabledSensorRuntime();
    }

    /// <summary>
    /// Options'tan hangi backend'lerin açılacağını çözer.
    ///
    /// Bu anahtarların registry içinde kayıtlı olması gerekir.
    /// Örn:
    /// - sim_imu
    /// - sim_gps
    /// - sim_lidar
    /// </summary>
    private static IReadOnlyList<string> ResolveBackendKeys(SensorRuntimeOptions options)
    {
        var keys = new List<string>();

        if (!options.EnableDefaultSimSensors)
            return keys;

        if (options.EnableImu)
            keys.Add("sim_imu");

        if (options.EnableGps)
            keys.Add("sim_gps");

        if (options.EnableLidar)
            keys.Add("sim_lidar");

        return keys;
    }
}