using System.Text;
using Hydronom.Core.Communication.Commands;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed class HydronomRuntimeCommandAckBinaryCodec
{
    public const string ContentType = "application/vnd.hydronom.runtime-command-ack+binary";

    private const byte Magic0 = (byte)'H';
    private const byte Magic1 = (byte)'A';
    private const ushort Version = 1;

    private const int MaxStringBytes = 64 * 1024;
    private const int MaxListEntries = 4096;
    private const int MaxMapEntries = 4096;

    public byte[] Encode(HydronomRuntimeCommandAck ack)
    {
        ArgumentNullException.ThrowIfNull(ack);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic0);
        writer.Write(Magic1);
        writer.Write(Version);

        WriteString(writer, ack.AckId);
        writer.Write(ack.AckSequence);

        WriteString(writer, ack.CommandId);
        WriteString(writer, ack.IntentId);

        writer.Write((ushort)ack.CommandKind);
        writer.Write((ushort)ack.IntentKind);
        writer.Write((byte)ack.Status);

        WriteString(writer, ack.Reason);

        WriteString(writer, ack.SourceId);
        WriteString(writer, ack.TargetId);
        WriteString(writer, ack.VehicleId);
        WriteString(writer, ack.OperatorId);

        // Bu alan komutun orijinal sequence değeridir.
        // Envelope sequence değeri ACK için ayrıca AckSequence olmalıdır.
        writer.Write(ack.Sequence);

        writer.Write(ack.CommandTimestampUnixMs);
        writer.Write(ack.AckTimestampUnixMs);

        WriteStringList(writer, ack.Issues);
        WriteStringMap(writer, ack.Metadata);

        writer.Flush();
        return stream.ToArray();
    }

    public HydronomRuntimeCommandAck Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new InvalidDataException("Runtime command ACK binary payload boş.");
        }

        using var stream = new MemoryStream(payload.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();

        if (magic0 != Magic0 || magic1 != Magic1)
        {
            throw new InvalidDataException("Runtime command ACK binary magic değeri geçersiz.");
        }

        var version = reader.ReadUInt16();

        if (version != Version)
        {
            throw new InvalidDataException(
                $"Desteklenmeyen runtime command ACK binary version. Version={version}");
        }

        var ackId = ReadString(reader);
        var ackSequence = reader.ReadUInt64();

        var commandId = ReadString(reader);
        var intentId = ReadString(reader);

        var commandKind = (HydronomCommandKind)reader.ReadUInt16();
        var intentKind = (HydronomRuntimeCommandIntentKind)reader.ReadUInt16();
        var status = (HydronomRuntimeCommandAckStatus)reader.ReadByte();

        var reason = ReadString(reader);

        var sourceId = ReadString(reader);
        var targetId = ReadString(reader);
        var vehicleId = ReadString(reader);
        var operatorId = ReadString(reader);

        var sequence = reader.ReadUInt64();

        var commandTimestampUnixMs = reader.ReadInt64();
        var ackTimestampUnixMs = reader.ReadInt64();

        var issues = ReadStringList(reader);
        var metadata = ReadStringMap(reader);

        EnsureFullyConsumed(stream);

        return new HydronomRuntimeCommandAck
        {
            AckId = ackId,
            AckSequence = ackSequence,
            CommandId = commandId,
            IntentId = intentId,
            CommandKind = commandKind,
            IntentKind = intentKind,
            Status = status,
            Reason = reason,
            SourceId = sourceId,
            TargetId = targetId,
            VehicleId = vehicleId,
            OperatorId = operatorId,
            Sequence = sequence,
            CommandTimestampUnixMs = commandTimestampUnixMs,
            AckTimestampUnixMs = ackTimestampUnixMs,
            Issues = issues,
            Metadata = metadata
        };
    }

    public HydronomRuntimeCommandAck Decode(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Decode(payload.AsSpan());
    }

    private static void WriteStringList(
        BinaryWriter writer,
        IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            writer.Write(0);
            return;
        }

        if (values.Count > MaxListEntries)
        {
            throw new InvalidDataException(
                $"ACK issue sayısı çok yüksek. Count={values.Count}, Max={MaxListEntries}");
        }

        writer.Write(values.Count);

        foreach (var value in values)
        {
            WriteString(writer, value);
        }
    }

    private static IReadOnlyList<string> ReadStringList(BinaryReader reader)
    {
        var count = reader.ReadInt32();

        if (count < 0 || count > MaxListEntries)
        {
            throw new InvalidDataException(
                $"ACK issue count geçersiz. Count={count}, Max={MaxListEntries}");
        }

        if (count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[count];

        for (var i = 0; i < count; i++)
        {
            result[i] = ReadString(reader);
        }

        return result;
    }

    private static void WriteStringMap(
        BinaryWriter writer,
        IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            writer.Write(0);
            return;
        }

        if (values.Count > MaxMapEntries)
        {
            throw new InvalidDataException(
                $"ACK metadata sayısı çok yüksek. Count={values.Count}, Max={MaxMapEntries}");
        }

        var ordered = values
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();

        writer.Write(ordered.Length);

        foreach (var pair in ordered)
        {
            WriteString(writer, pair.Key);
            WriteString(writer, pair.Value);
        }
    }

    private static Dictionary<string, string> ReadStringMap(BinaryReader reader)
    {
        var count = reader.ReadInt32();

        if (count < 0 || count > MaxMapEntries)
        {
            throw new InvalidDataException(
                $"ACK metadata count geçersiz. Count={count}, Max={MaxMapEntries}");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < count; i++)
        {
            var key = ReadString(reader);
            var value = ReadString(reader);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException("ACK metadata key boş olamaz.");
            }

            result[key] = value;
        }

        return result;
    }

    private static void WriteString(BinaryWriter writer, string? value)
    {
        value ??= "";

        var bytes = Encoding.UTF8.GetBytes(value);

        if (bytes.Length > MaxStringBytes)
        {
            throw new InvalidDataException(
                $"String alanı çok büyük. Bytes={bytes.Length}, Max={MaxStringBytes}");
        }

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();

        if (length < 0 || length > MaxStringBytes)
        {
            throw new InvalidDataException(
                $"String uzunluğu geçersiz. Length={length}, Max={MaxStringBytes}");
        }

        var bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            throw new EndOfStreamException(
                $"String okunamadı. Expected={length}, Actual={bytes.Length}");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void EnsureFullyConsumed(Stream stream)
    {
        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException(
                $"Runtime command ACK binary payload sonunda beklenmeyen veri var. Remaining={stream.Length - stream.Position}");
        }
    }
}