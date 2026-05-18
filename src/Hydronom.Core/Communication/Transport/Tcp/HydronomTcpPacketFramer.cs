using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Hydronom.Core.Communication.Transport.Tcp;

public static class HydronomTcpPacketFramer
{
    private const uint Magic = 0x48595450u; // HYTP
    private const ushort Version = 1;

    private const int MaxHeaderBytes = ushort.MaxValue;
    private const int MaxPayloadBytes = 32 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async ValueTask WritePacketAsync(
        NetworkStream stream,
        HydronomTransportPacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(packet);

        cancellationToken.ThrowIfCancellationRequested();

        var header = new TcpPacketHeaderDto
        {
            TransportId = packet.TransportId,
            SourceId = packet.SourceId,
            TargetId = packet.TargetId,
            ChannelId = packet.ChannelId,
            Sequence = packet.Sequence,
            CreatedAtUnixMs = packet.CreatedAt.ToUnixTimeMilliseconds()
        };

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions);
        var payloadBytes = packet.Bytes ?? Array.Empty<byte>();

        if (headerBytes.Length > MaxHeaderBytes)
        {
            throw new InvalidDataException($"TCP packet header çok büyük. HeaderBytes={headerBytes.Length}");
        }

        if (payloadBytes.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException($"TCP packet payload çok büyük. PayloadBytes={payloadBytes.Length}");
        }

        var prefix = new byte[16];

        WriteUInt32LittleEndian(prefix.AsSpan(0, 4), Magic);
        WriteUInt16LittleEndian(prefix.AsSpan(4, 2), Version);
        WriteUInt16LittleEndian(prefix.AsSpan(6, 2), (ushort)headerBytes.Length);
        WriteInt32LittleEndian(prefix.AsSpan(8, 4), payloadBytes.Length);
        WriteInt32LittleEndian(prefix.AsSpan(12, 4), 0);

        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

        if (payloadBytes.Length > 0)
        {
            await stream.WriteAsync(payloadBytes, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<HydronomTransportPacket?> ReadPacketAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        cancellationToken.ThrowIfCancellationRequested();

        var prefix = await ReadExactOrNullAsync(
                stream,
                16,
                cancellationToken)
            .ConfigureAwait(false);

        if (prefix is null)
        {
            return null;
        }

        var magic = ReadUInt32LittleEndian(prefix.AsSpan(0, 4));

        if (magic != Magic)
        {
            throw new InvalidDataException($"TCP packet magic hatalı. Magic=0x{magic:X8}");
        }

        var version = ReadUInt16LittleEndian(prefix.AsSpan(4, 2));

        if (version != Version)
        {
            throw new InvalidDataException($"TCP packet versiyonu desteklenmiyor. Version={version}");
        }

        var headerLength = ReadUInt16LittleEndian(prefix.AsSpan(6, 2));
        var payloadLength = ReadInt32LittleEndian(prefix.AsSpan(8, 4));

        if (headerLength <= 0 || headerLength > MaxHeaderBytes)
        {
            throw new InvalidDataException($"TCP packet header uzunluğu geçersiz. HeaderLength={headerLength}");
        }

        if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
        {
            throw new InvalidDataException($"TCP packet payload uzunluğu geçersiz. PayloadLength={payloadLength}");
        }

        var headerBytes = await ReadExactOrNullAsync(
                stream,
                headerLength,
                cancellationToken)
            .ConfigureAwait(false);

        if (headerBytes is null)
        {
            return null;
        }

        var header = JsonSerializer.Deserialize<TcpPacketHeaderDto>(
            headerBytes,
            JsonOptions) ?? throw new InvalidDataException("TCP packet header çözümlenemedi.");

        var payloadBytes = payloadLength > 0
            ? await ReadExactOrNullAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false)
            : Array.Empty<byte>();

        if (payloadBytes is null)
        {
            return null;
        }

        var createdAt = header.CreatedAtUnixMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(header.CreatedAtUnixMs)
            : DateTimeOffset.UtcNow;

        return new HydronomTransportPacket
        {
            TransportId = header.TransportId ?? "",
            SourceId = header.SourceId ?? "",
            TargetId = header.TargetId ?? "",
            ChannelId = string.IsNullOrWhiteSpace(header.ChannelId) ? "default" : header.ChannelId,
            Sequence = header.Sequence,
            CreatedAt = createdAt,
            Bytes = payloadBytes
        };
    }

    private static async ValueTask<byte[]?> ReadExactOrNullAsync(
        NetworkStream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await stream.ReadAsync(
                    buffer.AsMemory(offset, length - offset),
                    cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                return offset == 0
                    ? null
                    : throw new EndOfStreamException("TCP stream beklenenden erken kapandı.");
            }

            offset += read;
        }

        return buffer;
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

    private sealed class TcpPacketHeaderDto
    {
        public string? TransportId { get; set; }

        public string? SourceId { get; set; }

        public string? TargetId { get; set; }

        public string? ChannelId { get; set; }

        public ulong Sequence { get; set; }

        public long CreatedAtUnixMs { get; set; }
    }
}