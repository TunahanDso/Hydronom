using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Runtime.Sensors.Supervision;

namespace Hydronom.Runtime.Sensors.Runtime;

/// <summary>
/// Hydronom'un ana C# sensör runtime'ı.
/// Backend'leri açar, okur, health/capability toplar.
/// </summary>
public sealed class CSharpSensorRuntime : ISensorRuntime
{
    private readonly List<ISensorBackend> _backends = new();
    private readonly SensorRuntimeOptions _options;
    private readonly SensorSupervisor _supervisor = new();

    private bool _isRunning;

    /// <summary>
    /// Runtime'a kayıtlı backend sayısı.
    ///
    /// Bu bilgi özellikle auto-wiring testlerinde önemlidir.
    /// Örneğin CSharpPrimary + default sim sensörler açıkken beklenen başlangıç değeri genelde 2'dir:
    /// - sim_imu
    /// - sim_gps
    /// </summary>
    public int BackendCount => _backends.Count;

    /// <summary>
    /// Runtime içinde en az bir backend kayıtlı mı?
    ///
    /// CSharpPrimary modda bu değer false ise selector/builder/registry hattında bağlantı eksik olabilir.
    /// </summary>
    public bool HasBackends => _backends.Count > 0;

    public CSharpSensorRuntime(SensorRuntimeOptions? options = null)
    {
        _options = options ?? SensorRuntimeOptions.Default();
    }

    public SensorRuntimeMode Mode => SensorRuntimeMode.CSharpPrimary;

    public bool IsRunning => _isRunning;

    public SensorCapabilitySet Capabilities
    {
        get
        {
            var set = SensorCapabilitySet.Empty;

            foreach (var backend in _backends)
            {
                foreach (var capability in backend.Capabilities.Sanitized().Capabilities)
                    set = set.AddOrUpdate(capability);
            }

            return set.Sanitized();
        }
    }

    public void AddBackend(ISensorBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backends.Add(backend);
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        return StartInternalAsync(cancellationToken);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var backend in _backends)
        {
            try
            {
                await backend.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Kapatma sırasında tek backend hatası runtime'ı kilitlememeli.
            }
        }

        _isRunning = false;
    }

    public ValueTask<IReadOnlyList<SensorSample>> ReadBatchAsync(CancellationToken cancellationToken = default)
    {
        return ReadBatchInternalAsync(cancellationToken);
    }

    public SensorRuntimeHealth GetHealth()
    {
        var health = _backends
            .Select(x => x.GetHealthSnapshot())
            .ToArray();

        return _supervisor.Evaluate(Mode, health);
    }

    private async ValueTask StartInternalAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
            return;

        foreach (var backend in _backends)
            await backend.OpenAsync(cancellationToken).ConfigureAwait(false);

        _isRunning = true;
    }

    private async ValueTask<IReadOnlyList<SensorSample>> ReadBatchInternalAsync(CancellationToken cancellationToken)
    {
        var samples = new List<SensorSample>();

        if (!_isRunning)
            return samples;

        foreach (var backend in _backends)
        {
            try
            {
                var sample = await backend.ReadAsync(cancellationToken).ConfigureAwait(false);

                if (sample.HasValue && sample.Value.IsValid)
                    samples.Add(sample.Value.Sanitized());
            }
            catch
            {
                // İlk geçişte backend hatasını sample'a çevirmiyoruz.
                // Health snapshot zaten backend durumunu açıklayacak.
            }
        }

        return samples;
    }
}