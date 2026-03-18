using System.Collections.Concurrent;
using System.Net.WebSockets;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Infrastructure.Broadcast;

/// <summary>
/// Aktif websocket istemcilerini thread-safe biçimde yönetir.
/// </summary>
public sealed class GatewayWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<Guid, GatewayClientConnection> _connections = new();

    /// <summary>
    /// Yeni bağlantı ekler.
    /// </summary>
    public Task<GatewayClientConnection> AddAsync(
        WebSocket socket,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var connection = new GatewayClientConnection
        {
            Id = Guid.NewGuid(),
            Socket = socket,
            ConnectedAtUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow,
            LastSentUtc = null,
            RemoteIp = string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp
        };

        _connections[connection.Id] = connection;
        return Task.FromResult(connection);
    }

    /// <summary>
    /// Remote IP vermeden çağrılan eski kullanım için uyumluluk metodu.
    /// </summary>
    public Task<GatewayClientConnection> AddAsync(
        WebSocket socket,
        CancellationToken cancellationToken = default)
    {
        return AddAsync(socket, remoteIp: null, cancellationToken);
    }

    /// <summary>
    /// Eski çağrılar için senkron ekleme uyumluluğu.
    /// </summary>
    public GatewayClientConnection AddConnection(WebSocket socket, string? remoteIp = null)
    {
        return AddAsync(socket, remoteIp).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Bağlantı sayısı.
    /// </summary>
    public int Count => _connections.Count;

    /// <summary>
    /// Tüm bağlantıları döner.
    /// </summary>
    public IReadOnlyList<GatewayClientConnection> GetAllConnections()
    {
        return _connections.Values.ToList();
    }

    /// <summary>
    /// Eski isimlendirme için uyumluluk metodu.
    /// </summary>
    public IReadOnlyList<GatewayClientConnection> GetAllSockets()
    {
        return GetAllConnections();
    }

    /// <summary>
    /// Canlı bağlantıları döner.
    /// </summary>
    public IReadOnlyList<GatewayClientConnection> GetAliveConnections()
    {
        return _connections.Values.Where(x => x.IsAlive).ToList();
    }

    /// <summary>
    /// Belirli bağlantıyı bulur.
    /// </summary>
    public bool TryGet(Guid connectionId, out GatewayClientConnection? connection)
    {
        var ok = _connections.TryGetValue(connectionId, out var found);
        connection = found;
        return ok;
    }

    /// <summary>
    /// Eski isimlendirme için uyumluluk metodu.
    /// </summary>
    public bool TryGetConnection(Guid connectionId, out GatewayClientConnection connection)
    {
        var ok = _connections.TryGetValue(connectionId, out var found);
        connection = found!;
        return ok;
    }

    /// <summary>
    /// Bağlantı sayısını döner.
    /// </summary>
    public int GetConnectionCount()
    {
        return _connections.Count;
    }

    /// <summary>
    /// Son görülme zamanını günceller.
    /// </summary>
    public bool MarkSeen(Guid connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return false;
        }

        connection.LastSeenUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Tek bağlantıyı kapatır ve siler.
    /// </summary>
    public async Task RemoveAsync(
        Guid connectionId,
        WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
        string closeDescription = "Connection closed",
        CancellationToken cancellationToken = default)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
        {
            return;
        }

        try
        {
            if (connection.Socket.State == WebSocketState.Open ||
                connection.Socket.State == WebSocketState.CloseReceived)
            {
                await connection.Socket.CloseAsync(
                    closeStatus,
                    closeDescription,
                    cancellationToken);
            }
        }
        catch
        {
        }
        finally
        {
            connection.Socket.Dispose();
        }
    }

    /// <summary>
    /// Eski çağrılar için uyumluluk.
    /// </summary>
    public Task RemoveConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        return RemoveAsync(connectionId, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Ölü bağlantıları temizler.
    /// </summary>
    public async Task RemoveDeadConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var deadIds = _connections
            .Where(x =>
                x.Value.Socket.State == WebSocketState.Aborted ||
                x.Value.Socket.State == WebSocketState.Closed ||
                x.Value.Socket.State == WebSocketState.CloseSent)
            .Select(x => x.Key)
            .ToList();

        foreach (var id in deadIds)
        {
            await RemoveAsync(id, cancellationToken: cancellationToken);
        }
    }
}