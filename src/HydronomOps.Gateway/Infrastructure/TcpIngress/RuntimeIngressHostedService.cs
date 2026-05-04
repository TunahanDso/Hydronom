using HydronomOps.Gateway.Configuration;
using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Infrastructure.Broadcast;
using HydronomOps.Gateway.Services.State;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

/// <summary>
/// Runtime TCP istemcisini arka planda çalıştırır, gelen frame'leri parse eder
/// ve gateway tarafına yayın tetiklerini iletir.
/// </summary>
public sealed class RuntimeIngressHostedService : BackgroundService
{
    private readonly RuntimeTcpClientService _tcpClientService;
    private readonly RuntimeFrameParser _frameParser;
    private readonly GatewayBroadcastService _broadcastService;
    private readonly IGatewayStateStore _stateStore;
    private readonly RuntimeTcpOptions _options;
    private readonly ILogger<RuntimeIngressHostedService> _logger;

    private long _sequence;

    public RuntimeIngressHostedService(
        RuntimeTcpClientService tcpClientService,
        RuntimeFrameParser frameParser,
        GatewayBroadcastService broadcastService,
        IGatewayStateStore stateStore,
        IOptions<RuntimeTcpOptions> options,
        ILogger<RuntimeIngressHostedService> logger)
    {
        _tcpClientService = tcpClientService;
        _frameParser = frameParser;
        _broadcastService = broadcastService;
        _stateStore = stateStore;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Arka plan ana döngüsü.
    /// Runtime bağlantısını canlı tutar, satırları işler ve hata durumunda tekrar dener.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Runtime ingress başlatıldı. Host={Host}, Port={Port}",
            _options.Host,
            _options.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIngressLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime ingress döngüsünde hata oluştu.");

                var log = new GatewayLogDto
                {
                    Level = "error",
                    Category = "runtime-ingress",
                    Message = $"Runtime ingress hatası: {ex.Message}",
                    TimestampUtc = DateTime.UtcNow
                };

                _stateStore.AddLog(log);

                try
                {
                    await _broadcastService.BroadcastLogAsync(
                        CreateEnvelope(GatewayMessageType.GatewayLog, log),
                        stoppingToken);
                }
                catch
                {
                    // Yayın katmanı da düşerse ana retry mekanizmasını bozma.
                }

                await DelaySafeAsync(_options.ReconnectDelayMs, stoppingToken);
            }
        }

        _logger.LogInformation("Runtime ingress durduruldu.");
    }

    /// <summary>
    /// Tek bir bağlantı oturumunu yürütür.
    /// </summary>
    private async Task RunIngressLoopAsync(CancellationToken stoppingToken)
    {
        await _tcpClientService.ConnectAsync(stoppingToken);
        _stateStore.SetRuntimeConnected(true);

        var connectedLog = new GatewayLogDto
        {
            Level = "info",
            Category = "runtime-ingress",
            Message = $"Runtime bağlantısı kuruldu: {_options.Host}:{_options.Port}",
            TimestampUtc = DateTime.UtcNow
        };

        _stateStore.AddLog(connectedLog);

        await _broadcastService.BroadcastLogAsync(
            CreateEnvelope(GatewayMessageType.GatewayLog, connectedLog),
            stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var line = await _tcpClientService.ReadLineAsync(stoppingToken);

                if (line is null)
                {
                    throw new IOException("Runtime bağlantısı kapandı.");
                }

                _frameParser.ProcessLine(line);
                await BroadcastCurrentStateAsync(stoppingToken);
            }
        }
        finally
        {
            _stateStore.SetRuntimeConnected(false);
            await _tcpClientService.DisconnectAsync(stoppingToken);

            var closedLog = new GatewayLogDto
            {
                Level = "warn",
                Category = "runtime-ingress",
                Message = "Runtime bağlantısı kapandı.",
                TimestampUtc = DateTime.UtcNow
            };

            _stateStore.AddLog(closedLog);

            try
            {
                await _broadcastService.BroadcastLogAsync(
                    CreateEnvelope(GatewayMessageType.GatewayLog, closedLog),
                    stoppingToken);
            }
            catch
            {
                // Kapanış anındaki broadcast hatası kritik değil.
            }
        }
    }

    /// <summary>
    /// Güncel store snapshot'ını websocket istemcilerine iter.
    /// </summary>
    private async Task BroadcastCurrentStateAsync(CancellationToken cancellationToken)
    {
        var snapshot = _stateStore.GetCurrent();

        if (snapshot.VehicleTelemetry is not null)
        {
            await _broadcastService.BroadcastVehicleTelemetryAsync(
                CreateEnvelope(GatewayMessageType.VehicleTelemetry, snapshot.VehicleTelemetry, snapshot.VehicleId),
                cancellationToken);
        }

        if (snapshot.MissionState is not null)
        {
            await _broadcastService.BroadcastMissionStateAsync(
                CreateEnvelope(GatewayMessageType.MissionState, snapshot.MissionState, snapshot.VehicleId),
                cancellationToken);
        }

        if (snapshot.SensorState is not null)
        {
            /*
             * SensorState burada ana operasyonel sensör özetidir.
             * C# Primary akışta RuntimeSensorSummary taşır.
             * TwinImu/TwinGps gibi debug sensörler DebugSensorState alanında tutulur
             * ve ana SensorState yayınına karıştırılmaz.
             */
            await _broadcastService.BroadcastSensorStateAsync(
                CreateEnvelope(GatewayMessageType.SensorState, snapshot.SensorState, snapshot.VehicleId),
                cancellationToken);
        }

        if (snapshot.ActuatorState is not null)
        {
            await _broadcastService.BroadcastActuatorStateAsync(
                CreateEnvelope(GatewayMessageType.ActuatorState, snapshot.ActuatorState, snapshot.VehicleId),
                cancellationToken);
        }

        if (snapshot.DiagnosticsState is not null)
        {
            await _broadcastService.BroadcastDiagnosticsStateAsync(
                CreateEnvelope(GatewayMessageType.DiagnosticsState, snapshot.DiagnosticsState, snapshot.VehicleId),
                cancellationToken);
        }
    }

    /// <summary>
    /// Ortak envelope oluşturur.
    /// </summary>
    private GatewayEnvelope CreateEnvelope(string type, object payload, string? vehicleId = null)
    {
        return new GatewayEnvelope
        {
            Type = type,
            TimestampUtc = DateTime.UtcNow,
            VehicleId = string.IsNullOrWhiteSpace(vehicleId) ? "hydronom-main" : vehicleId,
            Source = "gateway",
            Sequence = Interlocked.Increment(ref _sequence),
            Payload = payload
        };
    }

    /// <summary>
    /// İptal dostu gecikme uygular.
    /// </summary>
    private static Task DelaySafeAsync(int delayMs, CancellationToken cancellationToken)
    {
        if (delayMs <= 0)
        {
            delayMs = 1000;
        }

        return Task.Delay(delayMs, cancellationToken);
    }
}