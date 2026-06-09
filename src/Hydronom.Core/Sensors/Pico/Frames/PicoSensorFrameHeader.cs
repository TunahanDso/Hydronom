using System;
using Hydronom.Core.Sensors.Pico.Protocol;

namespace Hydronom.Core.Sensors.Pico.Frames;

/// <summary>
/// Pico raw frame başlığıdır.
/// Bu model, payload çözülmeden önce frame'in kimlik, zaman, kanal ve bütünlük bilgisini taşır.
/// </summary>
public readonly record struct PicoSensorFrameHeader(
    PicoSensorProtocolVersion ProtocolVersion,
    PicoSensorPacketKind PacketKind,
    PicoSensorChannelKind ChannelKind,
    byte DeviceIndex,
    byte ChannelIndex,
    string DeviceId,
    string SensorId,
    long Sequence,
    long CaptureTimestampUnixMicros,
    int PayloadLength,
    PicoSensorChecksumKind ChecksumKind,
    uint ChecksumValue,
    DateTime HeaderReceivedUtc
)
{
    public static PicoSensorFrameHeader Empty => new(
        ProtocolVersion: PicoSensorProtocolVersion.Unknown,
        PacketKind: PicoSensorPacketKind.Unknown,
        ChannelKind: PicoSensorChannelKind.Unknown,
        DeviceIndex: 0,
        ChannelIndex: 0,
        DeviceId: "",
        SensorId: "",
        Sequence: 0,
        CaptureTimestampUnixMicros: 0,
        PayloadLength: 0,
        ChecksumKind: PicoSensorChecksumKind.None,
        ChecksumValue: 0,
        HeaderReceivedUtc: DateTime.UtcNow
    );

    public bool IsKnownProtocol => ProtocolVersion.IsCompatibleWith(PicoSensorProtocolVersion.Current);

    public bool HasPayload => PayloadLength > 0;

    public bool IsValid =>
        IsKnownProtocol &&
        PacketKind != PicoSensorPacketKind.Unknown &&
        ChannelKind != PicoSensorChannelKind.Unknown &&
        Sequence >= 0 &&
        PayloadLength >= 0;

    public DateTime CaptureUtc => UnixMicrosToUtc(CaptureTimestampUnixMicros);

    public string EffectiveDeviceId => Normalize(DeviceId, $"pico{DeviceIndex}");

    public string EffectiveSensorId => Normalize(
        SensorId,
        ChannelKind.DefaultSensorId(ChannelIndex)
    );

    public PicoSensorFrameHeader Sanitized()
    {
        var safeVersion = ProtocolVersion.Sanitized();
        var safeDeviceId = Normalize(DeviceId, $"pico{DeviceIndex}");
        var safeSensorId = Normalize(SensorId, ChannelKind.DefaultSensorId(ChannelIndex));

        return new PicoSensorFrameHeader(
            ProtocolVersion: safeVersion,
            PacketKind: PacketKind,
            ChannelKind: ChannelKind,
            DeviceIndex: DeviceIndex,
            ChannelIndex: ChannelIndex,
            DeviceId: safeDeviceId,
            SensorId: safeSensorId,
            Sequence: Sequence < 0 ? 0 : Sequence,
            CaptureTimestampUnixMicros: CaptureTimestampUnixMicros < 0 ? 0 : CaptureTimestampUnixMicros,
            PayloadLength: PayloadLength < 0 ? 0 : PayloadLength,
            ChecksumKind: ChecksumKind,
            ChecksumValue: ChecksumValue,
            HeaderReceivedUtc: HeaderReceivedUtc == default ? DateTime.UtcNow : HeaderReceivedUtc
        );
    }

    private static DateTime UnixMicrosToUtc(long unixMicros)
    {
        if (unixMicros <= 0)
        {
            return DateTime.UtcNow;
        }

        try
        {
            var ticks = checked(unixMicros * 10);
            return DateTime.UnixEpoch.AddTicks(ticks);
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}