using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Sensors.Pico.Protocol;

namespace Hydronom.Core.Sensors.Pico.Frames;

/// <summary>
/// Pico'dan gelen ham sensör frame'idir.
/// Bu model henüz IMU/GPS/LiDAR gibi typed SensorSample verisine çevrilmemiş düşük seviye frame'i temsil eder.
/// </summary>
public readonly record struct PicoRawSensorFrame(
    PicoSensorFrameHeader Header,
    IReadOnlyList<byte> Payload,
    PicoSensorFrameStatus Status,
    string Error,
    DateTime ReceiveUtc,
    DateTime DecodeUtc
)
{
    public static PicoRawSensorFrame Empty => new(
        Header: PicoSensorFrameHeader.Empty,
        Payload: Array.Empty<byte>(),
        Status: PicoSensorFrameStatus.Unknown,
        Error: "",
        ReceiveUtc: DateTime.UtcNow,
        DecodeUtc: DateTime.UtcNow
    );

    public bool IsValid =>
        Status == PicoSensorFrameStatus.Valid &&
        Header.IsValid &&
        Payload.Count == Header.PayloadLength;

    public bool HasPayload => Payload.Count > 0;

    public int PayloadLength => Payload.Count;

    public static PicoRawSensorFrame Create(
        PicoSensorFrameHeader header,
        IReadOnlyList<byte>? payload,
        PicoSensorFrameStatus status = PicoSensorFrameStatus.Valid,
        string error = "",
        DateTime? receiveUtc = null,
        DateTime? decodeUtc = null
    )
    {
        var safeHeader = header.Sanitized();
        var safePayload = NormalizePayload(payload);

        var effectiveStatus = status;

        if (effectiveStatus == PicoSensorFrameStatus.Valid && safeHeader.PayloadLength != safePayload.Count)
        {
            effectiveStatus = safePayload.Count == 0
                ? PicoSensorFrameStatus.EmptyPayload
                : PicoSensorFrameStatus.Incomplete;
        }

        return new PicoRawSensorFrame(
            Header: safeHeader,
            Payload: safePayload,
            Status: effectiveStatus,
            Error: Normalize(error, ""),
            ReceiveUtc: receiveUtc ?? DateTime.UtcNow,
            DecodeUtc: decodeUtc ?? receiveUtc ?? DateTime.UtcNow
        ).Sanitized();
    }

    public PicoRawSensorFrame WithStatus(PicoSensorFrameStatus status, string error = "")
    {
        return this with
        {
            Status = status,
            Error = Normalize(error, Error)
        };
    }

    public PicoRawSensorFrame Sanitized()
    {
        var receive = ReceiveUtc == default ? DateTime.UtcNow : ReceiveUtc;
        var decode = DecodeUtc == default ? receive : DecodeUtc;

        return new PicoRawSensorFrame(
            Header: Header.Sanitized(),
            Payload: NormalizePayload(Payload),
            Status: Status,
            Error: Normalize(Error, ""),
            ReceiveUtc: receive,
            DecodeUtc: decode
        );
    }

    public string ToTraceId()
    {
        var header = Header.Sanitized();
        return $"{header.EffectiveDeviceId}:{header.EffectiveSensorId}:{header.Sequence}";
    }

    private static IReadOnlyList<byte> NormalizePayload(IReadOnlyList<byte>? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return Array.Empty<byte>();
        }

        return payload.ToArray();
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}