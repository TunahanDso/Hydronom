using System.Collections.Concurrent;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Infrastructure.Serialization;
using HydronomOps.Gateway.Services.Mapping;
using HydronomOps.Gateway.Services.State;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private readonly IGatewayStateStore _stateStore;
    private readonly RuntimeToGatewayMapper _mapper;

    private readonly object _twinGate = new();

    private readonly ConcurrentDictionary<string, int> _debugLogCounts = new();
    private const int MaxDebugLogsPerType = 5;

    private DateTime _lastImuTimestampUtc = DateTime.UtcNow;
    private DateTime _lastGpsTimestampUtc = DateTime.UtcNow;
    private bool _hasLastGpsSample;

    private double _lastX;
    private double _lastY;
    private double _lastZ;
    private double _lastRollDeg;
    private double _lastPitchDeg;
    private double _lastYawDeg;
    private double _lastHeadingDeg;
    private double _lastRollRateDeg;
    private double _lastPitchRateDeg;
    private double _lastYawRateDeg;

    private bool _gpsOriginInitialized;
    private double _originLatDeg;
    private double _originLonDeg;

    public RuntimeFrameParser(
        IGatewayStateStore stateStore,
        RuntimeToGatewayMapper mapper)
    {
        _stateStore = stateStore;
        _mapper = mapper;
    }

    public void ProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        try
        {
            using var document = JsonDocument.Parse(line, JsonDefaults.DocumentOptions);
            var root = document.RootElement;

            var type = ReadType(root);
            if (string.IsNullOrWhiteSpace(type))
            {
                _stateStore.AddLog(new GatewayLogDto
                {
                    Level = "debug",
                    Category = "parser",
                    Message = "Type alanı olmayan runtime satırı alındı.",
                    Detail = TrimForLog(line),
                    TimestampUtc = DateTime.UtcNow
                });

                return;
            }

            LogRawSampleIfNeeded(type, line);

            switch (type)
            {
                case "FusedState":
                    ProcessFusedState(root);
                    break;

                case "ExternalState":
                    ProcessExternalState(root);
                    break;

                case "Sample":
                    ProcessSample(root);
                    break;

                case "Health":
                    ProcessHealth(root);
                    break;

                case "RuntimeTelemetrySummary":
                case "RuntimeSummary":
                    ProcessRuntimeTelemetrySummary(root);
                    break;

                // 🔥 KRİTİK: YENİ TELEMETRY PIPELINE
                case "RuntimeTelemetry":
                    ProcessRuntimeTelemetry(root);
                    break;

                case "RuntimeMissionState":
                    ProcessRuntimeMissionState(root);
                    break;

                case "RuntimeActuatorState":
                    ProcessRuntimeActuatorState(root);
                    break;

                case "RuntimeWorldObjects":
                    ProcessRuntimeWorldObjects(root);
                    break;

                case "Event":
                    ProcessEvent(root);
                    break;

                case "Capability":
                    ProcessCapability(root);
                    break;

                case "TwinImu":
                    ProcessTwinImu(root);
                    break;

                case "TwinGps":
                    ProcessTwinGps(root);
                    break;

                case "StreamSubscribe":
                    _stateStore.TouchRuntimeMessage("StreamSubscribe");
                    break;

                default:
                    _stateStore.AddLog(new GatewayLogDto
                    {
                        Level = "debug",
                        Category = "parser",
                        Message = $"Bilinmeyen mesaj tipi alındı: {type}",
                        Detail = TrimForLog(line),
                        TimestampUtc = DateTime.UtcNow
                    });

                    _stateStore.TouchRuntimeMessage(type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _stateStore.AddLog(new GatewayLogDto
            {
                Level = "error",
                Category = "parser",
                Message = $"Frame parse hatası: {ex.Message}",
                Detail = TrimForLog(line),
                TimestampUtc = DateTime.UtcNow
            });
        }
    }

    // 🔥 EN KRİTİK PARÇA (SENİN BUG'IN BURADAYDI)
    private void ProcessRuntimeTelemetry(JsonElement root)
    {
        var vehicle = _mapper.MapVehicleTelemetryFromRuntimeTelemetry(root);

        _stateStore.SetVehicleTelemetry(vehicle);

        _stateStore.TouchRuntimeMessage("RuntimeTelemetry");
    }

    private void MarkPythonConnected()
    {
        _stateStore.SetPythonConnected(true);
    }
}