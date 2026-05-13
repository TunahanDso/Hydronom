using System.Text.Json;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private void ProcessRuntimeTelemetrySummary(JsonElement root)
    {
        /*
         * RuntimeTelemetrySummary artık VehicleTelemetry kaynağı değildir.
         *
         * Sebep:
         * - RuntimeTelemetry gerçek hareket telemetrisini taşır.
         * - RuntimeTelemetrySummary daha genel sağlık/fusion özetidir.
         * - Summary frame'i velocity, yaw-rate, hedef ve görev türetilmiş değerlerini
         *   sıfır/null basabildiği için RuntimeTelemetry'yi ezmemelidir.
         *
         * Bu yüzden burada sadece diagnostics ve runtime sensor summary güncellenir.
         */

        var diagnostics = _mapper.MapDiagnosticsStateFromRuntimeSummary(root);
        var sensor = _mapper.MapSensorStateFromRuntimeSummary(root);

        _stateStore.SetDiagnosticsState(diagnostics);
        _stateStore.SetRuntimeSensorState(sensor);
        _stateStore.TouchRuntimeMessage("RuntimeTelemetrySummary");
    }
}