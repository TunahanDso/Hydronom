using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Infrastructure.Broadcast;
using HydronomOps.Gateway.Services.Health;
using HydronomOps.Gateway.Services.State;

namespace HydronomOps.Gateway.BackgroundJobs;

/// <summary>
/// Belirli aralÄ±klarla heartbeat yayÄ±nÄ± yapan background worker.
/// </summary>
public sealed class HeartbeatBroadcastWorker : BackgroundService
{
    private readonly GatewayBroadcastService _broadcastService;
    private readonly IGatewayHealthService _healthService;
    private readonly IGatewayStateStore _stateStore;
    private readonly ILogger<HeartbeatBroadcastWorker> _logger;

    private long _sequence;

    public HeartbeatBroadcastWorker(
        GatewayBroadcastService broadcastService,
        IGatewayHealthService healthService,
        IGatewayStateStore stateStore,
        ILogger<HeartbeatBroadcastWorker> logger)
    {
        _broadcastService = broadcastService;
        _healthService = healthService;
        _stateStore = stateStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat broadcast worker baÅŸlatÄ±ldÄ±.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var aggregate = _stateStore.GetCurrent();
                var heartbeat = _healthService.BuildHeartbeat(aggregate);

                var envelope = new GatewayEnvelope
                {
                    Type = GatewayMessageType.Heartbeat,
                    TimestampUtc = heartbeat.TimestampUtc,
                    VehicleId = aggregate.VehicleId,
                    Source = "gateway",
                    Sequence = Interlocked.Increment(ref _sequence),
                    Payload = heartbeat
                };

                await _broadcastService.BroadcastAsync(envelope, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Heartbeat yayÄ±nÄ± sÄ±rasÄ±nda hata oluÅŸtu.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Heartbeat broadcast worker durduruldu.");
    }
}
