using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Pico.Frames;
using Hydronom.Core.Sensors.Pico.Protocol;

namespace Hydronom.Runtime.Sensors.Backends.PicoUsb;

/// <summary>
/// Pico raw frame -> Hydronom SensorSample dönüştürücüsü.
/// 
/// Bu sınıf firmware bilmez.
/// Sadece decode edilmiş Pico frame'i ortak SensorSample sözleşmesine map eder.
/// </summary>
public sealed class PicoSensorSampleMapper
{
    private readonly PicoUsbSensorBackendOptions _options;

    public PicoSensorSampleMapper(PicoUsbSensorBackendOptions? options = null)
    {
        _options = (options ?? PicoUsbSensorBackendOptions.Default()).Sanitized();
    }

    public SensorSample? TryMap(PicoRawSensorFrame frame)
    {
        var safeFrame = frame.Sanitized();

        if (!safeFrame.IsValid)
        {
            return null;
        }

        var header = safeFrame.Header.Sanitized();
        var dataKind = header.ChannelKind.ToSensorDataKind();

        if (dataKind == SensorDataKind.Unknown)
        {
            return null;
        }

        /*
         * İlk pakette typed payload parse etmiyoruz.
         * Bu yüzden Data alanına raw payload koymak yerine null üretmek de mümkündü.
         *
         * Fakat SensorSample.IsValid, Data null ise false olur.
         * Bu mapper sadece frame geçerliyse RawPicoPayload wrapper'ı ile sample üretir.
         * Typed IMU/GPS/LiDAR mapper bir sonraki pakette bu katmanın üstüne eklenecek.
         */
        var data = new RawPicoSensorPayload(
            DeviceId: header.EffectiveDeviceId,
            SensorId: header.EffectiveSensorId,
            ChannelKind: header.ChannelKind,
            PacketKind: header.PacketKind,
            Payload: safeFrame.Payload.ToArray()
        );

        var captureUtc = header.CaptureUtc;
        var receiveUtc = safeFrame.ReceiveUtc == default ? DateTime.UtcNow : safeFrame.ReceiveUtc;
        var publishUtc = DateTime.UtcNow;

        var timing = SensorTiming.FromCapture(
            captureUtc: captureUtc,
            receiveUtc: receiveUtc,
            publishUtc: publishUtc,
            targetRateHz: _options.TargetRateHz,
            effectiveRateHz: _options.TargetRateHz
        );

        var quality = SensorQuality
            .Good(
                backendKind: SensorBackendKind.PicoUsb,
                backendName: _options.BackendName,
                simulated: false,
                confidence: 0.80
            )
            .WithTiming(
                ageMs: timing.CaptureAgeMs,
                latencyMs: timing.ReceiveToPublishMs,
                targetRateHz: _options.TargetRateHz,
                effectiveRateHz: _options.TargetRateHz
            );

        var identity = SensorIdentity.Create(
            sensorId: header.EffectiveSensorId,
            sourceId: $"{_options.DeviceId}:{header.EffectiveSensorId}",
            dataKind: dataKind,
            frameId: header.ChannelKind.DefaultFrameId(),
            displayName: $"Pico {header.ChannelKind} {header.ChannelIndex}"
        );

        var source = SensorSourceInfo.PicoUsb(
            backendName: _options.BackendName,
            endpoint: _options.Connection.ToString()
        );

        return SensorSample
            .Create(
                sensor: identity,
                source: source,
                sequence: header.Sequence,
                dataKind: dataKind,
                data: data,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: safeFrame.ToTraceId()
            )
            .WithTags(
                "pico",
                "pico_usb",
                header.ChannelKind.ToString(),
                header.PacketKind.ToString()
            );
    }

    public readonly record struct RawPicoSensorPayload(
        string DeviceId,
        string SensorId,
        PicoSensorChannelKind ChannelKind,
        PicoSensorPacketKind PacketKind,
        IReadOnlyList<byte> Payload
    );
}