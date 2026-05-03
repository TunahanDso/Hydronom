using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Domain;
using HydronomOps.Gateway.Infrastructure.Serialization;

namespace HydronomOps.Gateway.Infrastructure.Broadcast;

/// <summary>
/// Gateway iÃ§indeki tÃ¼m yayÄ±n akÄ±ÅŸÄ±nÄ± yÃ¶netir.
/// TCP tarafÄ±ndan toplanan ve map edilen mesajlarÄ± tÃ¼m aktif WebSocket istemcilerine yollar.
/// </summary>
public sealed class GatewayBroadcastService
{
    private readonly GatewayWebSocketConnectionManager _connectionManager;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.SerializerOptions;

    public GatewayBroadcastService(GatewayWebSocketConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Tek bir envelope mesajÄ±nÄ± tÃ¼m baÄŸlÄ± istemcilere yayÄ±nlar.
    /// </summary>
    public async Task BroadcastAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        await BroadcastRawJsonAsync(json, cancellationToken);
    }

    /// <summary>
    /// Log yayÄ±nÄ± iÃ§in uyumluluk metodu.
    /// </summary>
    public Task BroadcastLogAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return BroadcastAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// AraÃ§ telemetrisi yayÄ±nÄ± iÃ§in uyumluluk metodu.
    /// </summary>
    public Task BroadcastVehicleTelemetryAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return BroadcastAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// GÃ¶rev durumu yayÄ±nÄ± iÃ§in uyumluluk metodu.
    /// </summary>
    public Task BroadcastMissionStateAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return BroadcastAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// SensÃ¶r durumu yayÄ±nÄ± iÃ§in uyumluluk metodu.
    /// </summary>
    public Task BroadcastSensorStateAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return BroadcastAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// AktÃ¼atÃ¶r durumu yayÄ±nÄ± iÃ§in uyumluluk metodu.
    /// </summary>
    public Task BroadcastActuatorStateAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return BroadcastAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// Diagnostik durumu yayÄ±nÄ± iÃ§in uyumluluk metodu.
    /// </summary>
    public Task BroadcastDiagnosticsStateAsync(
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return BroadcastAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// Ham JSON iÃ§eriÄŸini tÃ¼m baÄŸlÄ± istemcilere yayÄ±nlar.
    /// </summary>
    public async Task BroadcastRawJsonAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var sockets = _connectionManager.GetAllSockets();
        if (sockets.Count == 0)
        {
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(buffer);

        var failedConnections = new ConcurrentBag<Guid>();
        var tasks = new List<Task>(sockets.Count);

        foreach (var connection in sockets)
        {
            tasks.Add(SendToClientSafeAsync(connection, segment, failedConnections, cancellationToken));
        }

        await Task.WhenAll(tasks);

        foreach (var connectionId in failedConnections)
        {
            await _connectionManager.RemoveConnectionAsync(connectionId, cancellationToken);
        }
    }

    /// <summary>
    /// Sadece belirli bir istemciye mesaj gÃ¶nderir.
    /// </summary>
    public async Task<bool> SendToClientAsync(
        Guid connectionId,
        GatewayEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_connectionManager.TryGetConnection(connectionId, out var connection))
        {
            return false;
        }

        if (connection.Socket.State != WebSocketState.Open)
        {
            await _connectionManager.RemoveConnectionAsync(connectionId, cancellationToken);
            return false;
        }

        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);

        try
        {
            await connection.Socket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            connection.MarkSent();
            return true;
        }
        catch
        {
            await _connectionManager.RemoveConnectionAsync(connectionId, cancellationToken);
            return false;
        }
    }

    private static async Task SendToClientSafeAsync(
        GatewayClientConnection connection,
        ArraySegment<byte> payload,
        ConcurrentBag<Guid> failedConnections,
        CancellationToken cancellationToken)
    {
        if (connection.Socket.State != WebSocketState.Open)
        {
            failedConnections.Add(connection.Id);
            return;
        }

        try
        {
            await connection.Socket.SendAsync(
                payload,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            connection.MarkSent();
        }
        catch
        {
            failedConnections.Add(connection.Id);
        }
    }
}
