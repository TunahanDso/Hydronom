namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;

/// <summary>
/// Ground Station tarafında kayıtlı transport instance'larını tutar.
/// 
/// Bu registry:
/// - TransportKind bazlı transport seçimi,
/// - bağlı transport'ları bulma,
/// - mock/real transport geçişi,
/// - ileride multi-instance transport desteği
/// için temel yapıdır.
/// </summary>
public sealed class GroundTransportRegistry
{
    private readonly List<ITransport> _transports = new();

    /// <summary>
    /// Kayıtlı tüm transport'lar.
    /// </summary>
    public IReadOnlyList<ITransport> Transports => _transports.ToArray();

    /// <summary>
    /// Registry içindeki transport sayısı.
    /// </summary>
    public int Count => _transports.Count;

    /// <summary>
    /// Yeni transport ekler.
    /// 
    /// Aynı isimde transport varsa ekleme yapmaz.
    /// </summary>
    public bool Add(ITransport transport)
    {
        if (transport is null)
            return false;

        if (_transports.Any(x =>
                string.Equals(x.Name, transport.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _transports.Add(transport);
        return true;
    }

    /// <summary>
    /// Belirli isimde transport kaldırır.
    /// </summary>
    public bool RemoveByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var existing = _transports.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            return false;

        _transports.Remove(existing);
        return true;
    }

    /// <summary>
    /// Belirli türdeki bağlı ilk transport'u döndürür.
    /// </summary>
    public ITransport? GetConnectedTransport(TransportKind kind)
    {
        return _transports.FirstOrDefault(x =>
            x.Kind == kind &&
            x.IsConnected);
    }

    /// <summary>
    /// Belirli türdeki tüm bağlı transport'ları döndürür.
    /// </summary>
    public IReadOnlyList<ITransport> GetConnectedTransports(TransportKind kind)
    {
        return _transports
            .Where(x => x.Kind == kind && x.IsConnected)
            .ToArray();
    }

    /// <summary>
    /// Verilen candidate listesine göre ilk bağlı transport'u döndürür.
    /// </summary>
    public ITransport? FindFirstConnected(IReadOnlyList<TransportKind> candidateKinds)
    {
        if (candidateKinds is null || candidateKinds.Count == 0)
            return null;

        foreach (var kind in candidateKinds)
        {
            var transport = GetConnectedTransport(kind);

            if (transport is not null)
                return transport;
        }

        return null;
    }

    /// <summary>
    /// Verilen candidate listesine göre bağlı transport listesini döndürür.
    /// </summary>
    public IReadOnlyList<ITransport> FindConnected(IReadOnlyList<TransportKind> candidateKinds)
    {
        if (candidateKinds is null || candidateKinds.Count == 0)
            return Array.Empty<ITransport>();

        var result = new List<ITransport>();

        foreach (var kind in candidateKinds)
            result.AddRange(GetConnectedTransports(kind));

        return result
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    /// <summary>
    /// Tüm transport'ları bağlar.
    /// </summary>
    public async Task ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var transport in _transports)
            await transport.ConnectAsync(cancellationToken);
    }

    /// <summary>
    /// Tüm transport'ları kapatır.
    /// </summary>
    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var transport in _transports)
            await transport.DisconnectAsync(cancellationToken);
    }
}