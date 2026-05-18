using System.Text;

namespace Hydronom.Core.Communication.Commands;

public sealed class HydronomCommandBinaryCodec
{
    public const string ContentType = "application/vnd.hydronom.command+binary";

    private const byte Magic0 = (byte)'H';
    private const byte Magic1 = (byte)'C';
    private const ushort Version = 1;

    private const int MaxStringBytes = 64 * 1024;
    private const int MaxMapEntries = 4096;

    public byte[] Encode(HydronomCommandFrame command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic0);
        writer.Write(Magic1);
        writer.Write(Version);

        WriteString(writer, command.CommandId);
        writer.Write((ushort)command.Kind);
        writer.Write((byte)command.Authority);

        WriteString(writer, command.SourceId);
        WriteString(writer, command.TargetId);
        WriteString(writer, command.VehicleId);
        WriteString(writer, command.OperatorId);

        writer.Write(command.Sequence);
        writer.Write(command.TimestampUnixMs);

        writer.Write(command.RequiresAck);
        writer.Write(command.SafetyCritical);

        WriteString(writer, command.Reason);

        WriteStringMap(writer, command.Parameters);

        // RawPayload bilinçli olarak yazılmıyor.
        // Ana komut protokolü artık typed/known alanlar üzerinden binary taşınır.
        // İleride gerekiyorsa buraya versiyonlu extension block eklenebilir.

        writer.Flush();
        return stream.ToArray();
    }

    public HydronomCommandFrame Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new InvalidDataException("Command binary payload boş.");
        }

        using var stream = new MemoryStream(payload.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();

        if (magic0 != Magic0 || magic1 != Magic1)
        {
            throw new InvalidDataException("Command binary magic değeri geçersiz.");
        }

        var version = reader.ReadUInt16();

        if (version != Version)
        {
            throw new InvalidDataException(
                $"Desteklenmeyen command binary version. Version={version}");
        }

        var commandId = ReadString(reader);
        var kind = (HydronomCommandKind)reader.ReadUInt16();
        var authority = (HydronomCommandAuthority)reader.ReadByte();

        var sourceId = ReadString(reader);
        var targetId = ReadString(reader);
        var vehicleId = ReadString(reader);
        var operatorId = ReadString(reader);

        var sequence = reader.ReadUInt64();
        var timestampUnixMs = reader.ReadInt64();

        var requiresAck = reader.ReadBoolean();
        var safetyCritical = reader.ReadBoolean();

        var reason = ReadString(reader);
        var parameters = ReadStringMap(reader);

        EnsureFullyConsumed(stream);

        return new HydronomCommandFrame
        {
            CommandId = commandId,
            Kind = kind,
            Authority = authority,
            SourceId = sourceId,
            TargetId = targetId,
            VehicleId = vehicleId,
            OperatorId = operatorId,
            Sequence = sequence,
            TimestampUnixMs = timestampUnixMs,
            RequiresAck = requiresAck,
            SafetyCritical = safetyCritical,
            Reason = reason,
            Parameters = parameters,

            // RawPayload ana binary haberleşmede taşınmıyor.
            RawPayload = null
        };
    }

    public HydronomCommandFrame Decode(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Decode(payload.AsSpan());
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
                $"Command parameter sayısı çok yüksek. Count={values.Count}, Max={MaxMapEntries}");
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
                $"Command parameter count geçersiz. Count={count}, Max={MaxMapEntries}");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < count; i++)
        {
            var key = ReadString(reader);
            var value = ReadString(reader);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException("Command parameter key boş olamaz.");
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
                $"Command binary payload sonunda beklenmeyen veri var. Remaining={stream.Length - stream.Position}");
        }
    }
}