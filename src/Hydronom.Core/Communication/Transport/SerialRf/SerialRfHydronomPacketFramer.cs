using System.IO.Ports;
using System.Text;

namespace Hydronom.Core.Communication.Transport.SerialRf;

public static class SerialRfHydronomPacketFramer
{
    private const uint Magic = 0x46525948u; // HYRF, little-endian: 48 59 52 46
    private const ushort Version = 1;

    private const int PrefixBytes = 16;

    public static void WritePacket(
        SerialPort port,
        HydronomTransportPacket packet,
        SerialRfHydronomTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(options);

        var headerBytes = BuildHeader(packet);
        var payloadBytes = packet.Bytes ?? Array.Empty<byte>();

        if (headerBytes.Length > options.MaxHeaderBytes)
        {
            throw new InvalidDataException(
                $"Serial RF header cok buyuk. HeaderBytes={headerBytes.Length}, Max={options.MaxHeaderBytes}");
        }

        if (payloadBytes.Length > options.MaxPayloadBytes)
        {
            throw new InvalidDataException(
                $"Serial RF payload cok buyuk. PayloadBytes={payloadBytes.Length}, Max={options.MaxPayloadBytes}");
        }

        var frameBytes = new byte[PrefixBytes + headerBytes.Length + payloadBytes.Length];

        WriteUInt32LittleEndian(frameBytes.AsSpan(0, 4), Magic);
        WriteUInt16LittleEndian(frameBytes.AsSpan(4, 2), Version);
        WriteUInt16LittleEndian(frameBytes.AsSpan(6, 2), (ushort)headerBytes.Length);
        WriteInt32LittleEndian(frameBytes.AsSpan(8, 4), payloadBytes.Length);

        headerBytes.CopyTo(frameBytes.AsSpan(PrefixBytes));

        if (payloadBytes.Length > 0)
        {
            payloadBytes.CopyTo(frameBytes.AsSpan(PrefixBytes + headerBytes.Length));
        }

        var crc = ComputeCrc32(frameBytes.AsSpan(PrefixBytes));
        WriteUInt32LittleEndian(frameBytes.AsSpan(12, 4), crc);

        port.Write(frameBytes, 0, frameBytes.Length);
    }

    public static HydronomTransportPacket? TryReadPacket(
        SerialPort port,
        SerialRfHydronomTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        var prefix = ReadPrefixWithMagicOrNull(port, cancellationToken);

        if (prefix is null)
        {
            return null;
        }

        var version = ReadUInt16LittleEndian(prefix.AsSpan(4, 2));

        if (version != Version)
        {
            throw new InvalidDataException(
                $"Serial RF frame versiyonu desteklenmiyor. Version={version}");
        }

        var headerLength = ReadUInt16LittleEndian(prefix.AsSpan(6, 2));
        var payloadLength = ReadInt32LittleEndian(prefix.AsSpan(8, 4));
        var expectedCrc = ReadUInt32LittleEndian(prefix.AsSpan(12, 4));

        if (headerLength <= 0 || headerLength > options.MaxHeaderBytes)
        {
            throw new InvalidDataException(
                $"Serial RF header uzunlugu gecersiz. HeaderLength={headerLength}");
        }

        if (payloadLength < 0 || payloadLength > options.MaxPayloadBytes)
        {
            throw new InvalidDataException(
                $"Serial RF payload uzunlugu gecersiz. PayloadLength={payloadLength}");
        }

        var bodyLength = headerLength + payloadLength;
        var body = ReadExactOrNull(port, bodyLength, cancellationToken);

        if (body is null)
        {
            return null;
        }

        var actualCrc = ComputeCrc32(body);

        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException(
                $"Serial RF CRC hatali. Expected=0x{expectedCrc:X8}, Actual=0x{actualCrc:X8}");
        }

        var headerBytes = body.AsSpan(0, headerLength).ToArray();
        var payloadBytes = payloadLength > 0
            ? body.AsSpan(headerLength, payloadLength).ToArray()
            : Array.Empty<byte>();

        return ParseHeader(headerBytes) with
        {
            Bytes = payloadBytes
        };
    }

    private static byte[] BuildHeader(HydronomTransportPacket packet)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true);

        writer.Write(packet.Sequence);
        writer.Write(packet.CreatedAt.ToUnixTimeMilliseconds());

        WriteTinyString(writer, packet.TransportId);
        WriteTinyString(writer, packet.SourceId);
        WriteTinyString(writer, packet.TargetId);
        WriteTinyString(writer, packet.ChannelId);

        writer.Flush();
        return memory.ToArray();
    }

    private static HydronomTransportPacket ParseHeader(byte[] headerBytes)
    {
        using var memory = new MemoryStream(headerBytes);
        using var reader = new BinaryReader(memory, Encoding.UTF8, leaveOpen: false);

        var sequence = reader.ReadUInt64();
        var createdAtUnixMs = reader.ReadInt64();

        var transportId = ReadTinyString(reader);
        var sourceId = ReadTinyString(reader);
        var targetId = ReadTinyString(reader);
        var channelId = ReadTinyString(reader);

        var createdAt = createdAtUnixMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(createdAtUnixMs)
            : DateTimeOffset.UtcNow;

        return new HydronomTransportPacket
        {
            TransportId = transportId,
            SourceId = sourceId,
            TargetId = targetId,
            ChannelId = string.IsNullOrWhiteSpace(channelId) ? "serial-rf" : channelId,
            Sequence = sequence,
            CreatedAt = createdAt,
            Bytes = Array.Empty<byte>()
        };
    }

    private static void WriteTinyString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");

        if (bytes.Length > byte.MaxValue)
        {
            throw new InvalidDataException(
                $"Serial RF header string cok uzun. Length={bytes.Length}");
        }

        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadTinyString(BinaryReader reader)
    {
        var length = reader.ReadByte();

        if (length == 0)
        {
            return "";
        }

        var bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Serial RF header string beklenenden erken bitti.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[]? ReadPrefixWithMagicOrNull(
        SerialPort port,
        CancellationToken cancellationToken)
    {
        var window = new byte[4];
        var filled = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var next = ReadByteOrTimeout(port, cancellationToken);

            if (next < 0)
            {
                return null;
            }

            if (filled < 4)
            {
                window[filled] = (byte)next;
                filled++;
            }
            else
            {
                window[0] = window[1];
                window[1] = window[2];
                window[2] = window[3];
                window[3] = (byte)next;
            }

            if (filled < 4)
            {
                continue;
            }

            if (window[0] == (byte)'H' &&
                window[1] == (byte)'Y' &&
                window[2] == (byte)'R' &&
                window[3] == (byte)'F')
            {
                var prefix = new byte[PrefixBytes];
                window.CopyTo(prefix.AsSpan(0, 4));

                var rest = ReadExactOrNull(
                    port,
                    PrefixBytes - 4,
                    cancellationToken);

                if (rest is null)
                {
                    return null;
                }

                rest.CopyTo(prefix.AsSpan(4));
                return prefix;
            }
        }

        return null;
    }

    private static byte[]? ReadExactOrNull(
        SerialPort port,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length && !cancellationToken.IsCancellationRequested)
        {
            var next = ReadByteOrTimeout(port, cancellationToken);

            if (next < 0)
            {
                return offset == 0 ? null : throw new EndOfStreamException(
                    "Serial RF frame beklenenden erken kesildi.");
            }

            buffer[offset] = (byte)next;
            offset++;
        }

        return offset == length ? buffer : null;
    }

    private static int ReadByteOrTimeout(
        SerialPort port,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                return port.ReadByte();
            }
            catch (TimeoutException)
            {
                return -1;
            }
        }

        return -1;
    }

    private static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
    }

    private static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
    }

    private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source)
    {
        return (uint)(source[0] |
                      (source[1] << 8) |
                      (source[2] << 16) |
                      (source[3] << 24));
    }

    private static void WriteInt32LittleEndian(Span<byte> destination, int value)
    {
        WriteUInt32LittleEndian(destination, unchecked((uint)value));
    }

    private static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
    {
        return unchecked((int)ReadUInt32LittleEndian(source));
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFF_FFFFu;

        foreach (var b in bytes)
        {
            crc ^= b;

            for (var i = 0; i < 8; i++)
            {
                var mask = 0u - (crc & 1u);
                crc = (crc >> 1) ^ (0xEDB8_8320u & mask);
            }
        }

        return ~crc;
    }
}