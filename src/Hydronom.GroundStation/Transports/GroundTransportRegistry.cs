namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;

/// <summary>
/// Ground Station tarafÄ±nda kayÄ±tlÄ± transport instance'larÄ±nÄ± tutar.
/// 
/// Bu registry:
/// - TransportKind bazlÄ± transport seÃ§imi,
/// - baÄŸlÄ± transport'larÄ± bulma,
/// - mock/real transport geÃ§iÅŸi,
/// - ileride multi-instance transport desteÄŸi
/// iÃ§in temel yapÄ±dÄ±r.
/// </summary>
public sealed class GroundTransportRegistry
{
    private readonly List<ITransport> _transports = new();

    /// <summary>
    /// KayÄ±tlÄ± tÃ¼m transport'lar.
    /// </summary>
    public IReadOnlyList<ITransport> Transports => _transports.ToArray();

    /// <summary>
    /// Registry iÃ§indeki transport sayÄ±sÄ±.
    /// </summary>
    public int Count => _transports.Count;

    /// <summary>
    /// Yeni transport ekler.
    /// 
    /// AynÄ± isimde transport varsa ekleme yapmaz.
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
    /// Belirli isimde transport kaldÄ±rÄ±r.
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
    /// Belirli tÃ¼rdeki baÄŸlÄ± ilk transport'u dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public ITransport? GetConnectedTransport(TransportKind kind)
    {
        return _transports.FirstOrDefault(x =>
            x.Kind == kind &&
            x.IsConnected);
    }

    /// <summary>
    /// Belirli tÃ¼rdeki tÃ¼m baÄŸlÄ± transport'larÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<ITransport> GetConnectedTransports(TransportKind kind)
    {
        return _transports
            .Where(x => x.Kind == kind && x.IsConnected)
            .ToArray();
    }

    /// <summary>
    /// Verilen candidate listesine gÃ¶re ilk baÄŸlÄ± transport'u dÃ¶ndÃ¼rÃ¼r.
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
    /// Verilen candidate listesine gÃ¶re baÄŸlÄ± transport listesini dÃ¶ndÃ¼rÃ¼r.
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
    /// TÃ¼m transport'larÄ± baÄŸlar.
    /// </summary>
    public async Task ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var transport in _transports)
            await transport.ConnectAsync(cancellationToken);
    }

    /// <summary>
    /// TÃ¼m transport'larÄ± kapatÄ±r.
    /// </summary>
    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var transport in _transports)
            await transport.DisconnectAsync(cancellationToken);
    }
}
