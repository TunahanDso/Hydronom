using System.Text.Json;
using Hydronom.Core.Communication.Abstractions;
using Hydronom.Core.Communication.Envelope;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Codecs;

public sealed class JsonHydronomCodec : IHydronomCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string CodecName => "hydronom-json-v1";

    public string ContentType => "application/vnd.hydronom.envelope+json";

    public HydronomEncodedMessage Encode(CommunicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var jsonEnvelope = new JsonEnvelopeDto
        {
            Protocol = envelope.Protocol,
            Version = envelope.Version,
            Type = envelope.Type,
            Priority = envelope.Priority,
            Flags = envelope.Flags,
            Sequence = envelope.Sequence,
            TimestampUnixMs = envelope.TimestampUnixMs,
            SessionId = envelope.SessionId,
            SourceId = envelope.SourceId,
            TargetId = envelope.TargetId,
            VehicleId = envelope.VehicleId,
            CorrelationId = envelope.CorrelationId,
            ContentType = envelope.ContentType,
            Payload = envelope.Payload,
            SecurityTag = envelope.SecurityTag,
            Metadata = envelope.Metadata.ToDictionary(x => x.Key, x => x.Value)
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(jsonEnvelope, Options);

        return new HydronomEncodedMessage
        {
            Type = envelope.Type,
            Priority = envelope.Priority,
            Flags = envelope.Flags,
            Bytes = bytes,
            CodecName = CodecName
        };
    }

    public CommunicationEnvelope Decode(HydronomEncodedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Bytes.Length == 0)
        {
            throw new InvalidDataException("JSON mesajı boş olamaz.");
        }

        var dto = JsonSerializer.Deserialize<JsonEnvelopeDto>(message.Bytes, Options)
            ?? throw new InvalidDataException("JSON envelope çözümlenemedi.");

        return new CommunicationEnvelope
        {
            Protocol = string.IsNullOrWhiteSpace(dto.Protocol) ? "HYDRONOM" : dto.Protocol,
            Version = dto.Version,
            Type = dto.Type,
            Priority = dto.Priority,
            Flags = dto.Flags,
            Sequence = dto.Sequence,
            TimestampUnixMs = dto.TimestampUnixMs,
            SessionId = dto.SessionId ?? "",
            SourceId = dto.SourceId ?? "",
            TargetId = dto.TargetId ?? "",
            VehicleId = dto.VehicleId ?? "",
            CorrelationId = dto.CorrelationId ?? "",
            ContentType = dto.ContentType ?? "application/octet-stream",
            Payload = dto.Payload ?? Array.Empty<byte>(),
            SecurityTag = dto.SecurityTag,
            Metadata = dto.Metadata ?? new Dictionary<string, string>()
        };
    }

    private sealed class JsonEnvelopeDto
    {
        public string Protocol { get; set; } = "HYDRONOM";

        public ushort Version { get; set; } = 1;

        public HydronomMessageType Type { get; set; }

        public HydronomMessagePriority Priority { get; set; }

        public HydronomMessageFlags Flags { get; set; }

        public ulong Sequence { get; set; }

        public long TimestampUnixMs { get; set; }

        public string? SessionId { get; set; }

        public string? SourceId { get; set; }

        public string? TargetId { get; set; }

        public string? VehicleId { get; set; }

        public string? CorrelationId { get; set; }

        public string? ContentType { get; set; }

        public byte[]? Payload { get; set; }

        public byte[]? SecurityTag { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }
    }
}