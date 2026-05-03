using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HydronomOps.Gateway.Infrastructure.Broadcast;
using HydronomOps.Gateway.Infrastructure.Serialization;

namespace HydronomOps.Gateway.Endpoints;

/// <summary>
/// WebSocket endpoint tanÄ±mÄ±.
/// TarayÄ±cÄ± istemcileri gateway yayÄ±nlarÄ±nÄ± bu uÃ§tan dinler.
/// </summary>
public static class WebSocketEndpointExtensions
{
    /// <summary>
    /// Program.cs ile uyumlu ana map metodu.
    /// </summary>
    public static IEndpointRouteBuilder MapWebSocketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGatewayWebSocketEndpoint();
    }

    /// <summary>
    /// /ws endpoint'ini mapler.
    /// </summary>
    public static IEndpointRouteBuilder MapGatewayWebSocketEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket request bekleniyor.");
                return;
            }

            var connectionManager = context.RequestServices.GetRequiredService<GatewayWebSocketConnectionManager>();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("HydronomOps.Gateway.WebSocket");

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connection = await connectionManager.AddAsync(
                socket,
                remoteIp: context.Connection.RemoteIpAddress?.ToString(),
                cancellationToken: context.RequestAborted);

            logger.LogInformation(
                "WebSocket client connected. ConnectionId={ConnectionId}, Total={Total}",
                connection.Id,
                connectionManager.Count);

            try
            {
                await ReceiveLoopAsync(socket, connectionManager, connection.Id, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException ex)
            {
                logger.LogWarning(ex, "WebSocket baÄŸlantÄ±sÄ±nda hata oluÅŸtu. ConnectionId={ConnectionId}", connection.Id);
            }
            finally
            {
                await connectionManager.RemoveAsync(connection.Id, cancellationToken: context.RequestAborted);

                logger.LogInformation(
                    "WebSocket client disconnected. ConnectionId={ConnectionId}, Total={Total}",
                    connection.Id,
                    connectionManager.Count);
            }
        });

        return endpoints;
    }

    /// <summary>
    /// Ä°stemciden gelen mesajlarÄ± okuyup baÄŸlantÄ±yÄ± canlÄ± tutar.
    /// </summary>
    private static async Task ReceiveLoopAsync(
        WebSocket socket,
        GatewayWebSocketConnectionManager connectionManager,
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested &&
               socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "closing",
                    cancellationToken);
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            connectionManager.MarkSeen(connectionId);

            var message = await ReadTextMessageAsync(socket, buffer, result, cancellationToken);
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (string.Equals(message, "ping", StringComparison.OrdinalIgnoreCase))
            {
                var pongBytes = Encoding.UTF8.GetBytes("pong");
                await socket.SendAsync(
                    pongBytes,
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
                continue;
            }

            if (TryHandleJsonPing(message, out var responseJson))
            {
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await socket.SendAsync(
                    responseBytes,
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// ParÃ§alÄ± gelen text frame'i tek string olarak toplar.
    /// </summary>
    private static async Task<string> ReadTextMessageAsync(
        WebSocket socket,
        byte[] buffer,
        WebSocketReceiveResult firstResult,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();

        ms.Write(buffer, 0, firstResult.Count);

        var result = firstResult;
        while (!result.EndOfMessage)
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return string.Empty;
            }

            ms.Write(buffer, 0, result.Count);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// JSON tabanlÄ± ping isteÄŸini iÅŸler.
    /// </summary>
    private static bool TryHandleJsonPing(string message, out string responseJson)
    {
        responseJson = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(message, JsonDefaults.DocumentOptions);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                return false;
            }

            var type = typeProp.GetString();
            if (!string.Equals(type, "ping", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = new
            {
                type = "pong",
                timestampUtc = DateTime.UtcNow
            };

            responseJson = JsonSerializer.Serialize(payload, JsonDefaults.SerializerOptions);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
