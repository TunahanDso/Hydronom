namespace Hydronom.Runtime.Telemetry;

/// <summary>
/// Runtime telemetry özetlerini dış tüketicilere yayınlayan ortak sözleşme.
/// Normal C# Primary akışta bu sensör verisini runtime'a almak için değil,
/// runtime'ın kendi operasyonel özetini Gateway/Ops tarafına taşımak için kullanılır.
/// </summary>
public interface IRuntimeTelemetryPublisher
{
    Task PublishAsync(RuntimeTelemetrySummary summary, CancellationToken cancellationToken = default);
}