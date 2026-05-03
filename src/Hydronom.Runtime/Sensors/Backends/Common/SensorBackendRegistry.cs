using Hydronom.Core.Sensors.Common.Abstractions;

namespace Hydronom.Runtime.Sensors.Backends.Common;

/// <summary>
/// Sensör backend kayıt defteri.
///
/// Bu sınıf sensörleri çalıştırmaz.
/// Sadece hangi backend'in hangi anahtar ile üretileceğini bilir.
///
/// Örnek:
/// - sim_imu
/// - sim_gps
/// - sim_lidar
/// - replay_gps
/// - real_gps_nmea
/// </summary>
public sealed class SensorBackendRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider?, ISensorBackend>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kayıtlı backend anahtarları.
    /// Diagnostics/debug çıktıları için kullanılabilir.
    /// </summary>
    public IReadOnlyList<string> RegisteredKeys =>
        _factories.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Registry boş mu?
    /// </summary>
    public bool IsEmpty => _factories.Count == 0;

    /// <summary>
    /// Yeni backend factory kaydeder.
    ///
    /// replaceExisting=false ise aynı key ikinci kez kaydedilemez.
    /// Bu, yanlışlıkla backend override edilmesini engeller.
    /// </summary>
    public SensorBackendRegistry Register(
        string key,
        Func<IServiceProvider?, ISensorBackend> factory,
        bool replaceExisting = false)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sensör backend anahtarı boş olamaz.", nameof(key));

        ArgumentNullException.ThrowIfNull(factory);

        var normalizedKey = NormalizeKey(key);

        if (_factories.ContainsKey(normalizedKey) && !replaceExisting)
        {
            throw new InvalidOperationException(
                $"'{normalizedKey}' isimli sensör backend zaten kayıtlı. " +
                "Üzerine yazmak için replaceExisting=true kullanılmalıdır.");
        }

        _factories[normalizedKey] = factory;
        return this;
    }

    /// <summary>
    /// Belirli bir backend kayıtlı mı?
    /// </summary>
    public bool Contains(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _factories.ContainsKey(NormalizeKey(key));
    }

    /// <summary>
    /// Tek bir backend oluşturur.
    /// </summary>
    public ISensorBackend Create(string key, IServiceProvider? services = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sensör backend anahtarı boş olamaz.", nameof(key));

        var normalizedKey = NormalizeKey(key);

        if (!_factories.TryGetValue(normalizedKey, out var factory))
        {
            var known = _factories.Count == 0
                ? "kayıtlı backend yok"
                : string.Join(", ", RegisteredKeys);

            throw new InvalidOperationException(
                $"'{normalizedKey}' isimli sensör backend bulunamadı. Kayıtlı backend'ler: {known}");
        }

        return factory(services);
    }

    /// <summary>
    /// Birden fazla backend oluşturur.
    /// Config/options üzerinden gelen backend listesi burada çözülebilir.
    /// </summary>
    public IReadOnlyList<ISensorBackend> CreateMany(
        IEnumerable<string> keys,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var result = new List<ISensorBackend>();

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result.Add(Create(key, services));
        }

        return result;
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}