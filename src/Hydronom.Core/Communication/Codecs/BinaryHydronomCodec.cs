using System.Text;
using Hydronom.Core.Communication.Abstractions;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Integrity;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Codecs;

public sealed class BinaryHydronomCodec : IHydronomCodec
{
    private const byte Magic0 = 0x48;
    private const byte Magic1 = 0x59;

    private const ushort CodecVersion = 1;

    public string CodecName => "hydronom-binary-envelope-v1";

    public string ContentType => "application/vnd.hydronom.envelope+binary";

    public HydronomEncodedMessage Encode(CommunicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var bodyStream = new MemoryStream();
        using var writer = new BinaryWriter(bodyStream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic0);
        writer.Write(Magic1);
        writer.Write(CodecVersion);

        writer.Write((ushort)envelope.Type);
        writer.Write((byte)envelope.Priority);
        writer.Write((ushort)envelope.Flags);

        writer.Write(envelope.Sequence);
        writer.Write(envelope.TimestampUnixMs);

        WriteString(writer, envelope.SessionId);
        WriteString(writer, envelope.SourceId);
        WriteString(writer, envelope.TargetId);
        WriteString(writer, envelope.VehicleId);
        WriteString(writer, envelope.CorrelationId);
        WriteString(writer, envelope.ContentType);

        writer.Write(envelope.Payload.Length);
        writer.Write(envelope.Payload);

        var securityTag = envelope.SecurityTag ?? Array.Empty<byte>();
        WriteBytes16(writer, securityTag);

        writer.Write(envelope.Metadata.Count);

        foreach (var item in envelope.Metadata)
        {
            WriteString(writer, item.Key);
            WriteString(writer, item.Value);
        }

        writer.Flush();

        var body = bodyStream.ToArray();
        var crc = HydronomCrc32.Compute(body);

        using var finalStream = new MemoryStream(body.Length + sizeof(uint));
        finalStream.Write(body, 0, body.Length);

        using var finalWriter = new BinaryWriter(finalStream, Encoding.UTF8, leaveOpen: true);
        finalWriter.Write(crc);
        finalWriter.Flush();

        return new HydronomEncodedMessage
        {
            Type = envelope.Type,
            Priority = envelope.Priority,
            Flags = envelope.Flags,
            Bytes = finalStream.ToArray(),
            CodecName = CodecName
        };
    }

    public CommunicationEnvelope Decode(HydronomEncodedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Bytes.Length < 16)
        {
            throw new InvalidDataException("Binary Hydronom mesajı çok kısa.");
        }

        var bodyLength = message.Bytes.Length - sizeof(uint);
        var body = message.Bytes.AsSpan(0, bodyLength);
        var expectedCrc = BitConverter.ToUInt32(message.Bytes, bodyLength);

        if (!HydronomCrc32.Verify(body, expectedCrc))
        {
            throw new InvalidDataException("Binary Hydronom mesajı CRC doğrulamasından geçemedi.");
        }

        using var stream = new MemoryStream(message.Bytes, 0, bodyLength, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();

        if (magic0 != Magic0 || magic1 != Magic1)
        {
            throw new InvalidDataException("Binary Hydronom magic header hatalı.");
        }

        var version = reader.ReadUInt16();

        if (version != CodecVersion)
        {
            throw new InvalidDataException($"Desteklenmeyen Binary Hydronom codec versiyonu: {version}");
        }

        var type = (HydronomMessageType)reader.ReadUInt16();
        var priority = (HydronomMessagePriority)reader.ReadByte();
        var flags = (HydronomMessageFlags)reader.ReadUInt16();

        var sequence = reader.ReadUInt64();
        var timestampUnixMs = reader.ReadInt64();

        var sessionId = ReadString(reader);
        var sourceId = ReadString(reader);
        var targetId = ReadString(reader);
        var vehicleId = ReadString(reader);
        var correlationId = ReadString(reader);
        var contentType = ReadString(reader);

        var payloadLength = reader.ReadInt32();

        if (payloadLength < 0 || payloadLength > stream.Length - stream.Position)
        {
            throw new InvalidDataException("Binary Hydronom payload uzunluğu geçersiz.");
        }

        var payload = reader.ReadBytes(payloadLength);
        var securityTag = ReadBytes16(reader);

        var metadataCount = reader.ReadInt32();

        if (metadataCount < 0 || metadataCount > 1024)
        {
            throw new InvalidDataException("Binary Hydronom metadata sayısı geçersiz.");
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < metadataCount; i++)
        {
            var key = ReadString(reader);
            var value = ReadString(reader);

            if (!string.IsNullOrWhiteSpace(key))
            {
                metadata[key] = value;
            }
        }

        return new CommunicationEnvelope
        {
            Protocol = "HYDRONOM",
            Version = version,
            Type = type,
            Priority = priority,
            Flags = flags,
            Sequence = sequence,
            TimestampUnixMs = timestampUnixMs,
            SessionId = sessionId,
            SourceId = sourceId,
            TargetId = targetId,
            VehicleId = vehicleId,
            CorrelationId = correlationId,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType,
            Payload = payload,
            SecurityTag = securityTag.Length == 0 ? null : securityTag,
            Metadata = metadata
        };
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");

        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidDataException("String alanı binary envelope için çok uzun.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadUInt16();

        if (length == 0)
        {
            return "";
        }

        var bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Binary Hydronom string alanı eksik okundu.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteBytes16(BinaryWriter writer, byte[] bytes)
    {
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidDataException("Byte alanı binary envelope için çok uzun.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static byte[] ReadBytes16(BinaryReader reader)
    {
        var length = reader.ReadUInt16();

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        var bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Binary Hydronom byte alanı eksik okundu.");
        }

        return bytes;
    }
}