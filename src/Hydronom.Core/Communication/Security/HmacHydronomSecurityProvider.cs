using System.Security.Cryptography;
using System.Text;
using Hydronom.Core.Communication.Abstractions;
using Hydronom.Core.Communication.Envelope;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Security;

public sealed class HmacHydronomSecurityProvider : IHydronomSecurityProvider
{
    private readonly byte[] _secretKey;
    private readonly AntiReplayWindow _antiReplayWindow;

    public HmacHydronomSecurityProvider(
        string secretKey,
        AntiReplayWindow? antiReplayWindow = null)
        : this(Encoding.UTF8.GetBytes(secretKey), antiReplayWindow)
    {
    }

    public HmacHydronomSecurityProvider(
        byte[] secretKey,
        AntiReplayWindow? antiReplayWindow = null)
    {
        if (secretKey.Length < 16)
        {
            throw new ArgumentException(
                "HMAC secret key en az 16 byte olmalı.",
                nameof(secretKey));
        }

        _secretKey = secretKey.ToArray();
        _antiReplayWindow = antiReplayWindow ?? new AntiReplayWindow();
    }

    public string ProviderName => "hydronom-hmac-sha256-v1";

    public CommunicationEnvelope Protect(
        CommunicationEnvelope envelope,
        HydronomSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Level == HydronomSecurityLevel.None ||
            profile.Level == HydronomSecurityLevel.CrcOnly)
        {
            return envelope;
        }

        var tag = ComputeTag(envelope);

        return envelope with
        {
            Flags = envelope.Flags | HydronomMessageFlags.IsSigned,
            SecurityTag = tag
        };
    }

    public HydronomSecurityResult Verify(
        CommunicationEnvelope envelope,
        HydronomSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.RequireFreshTimestamp || profile.RequireMonotonicSequence)
        {
            var replayResult = _antiReplayWindow.CheckAndRemember(
                envelope.SourceId,
                envelope.Sequence,
                envelope.TimestampUnixMs,
                profile);

            if (!replayResult.Accepted)
            {
                return replayResult;
            }
        }

        if (profile.Level == HydronomSecurityLevel.None ||
            profile.Level == HydronomSecurityLevel.CrcOnly)
        {
            return HydronomSecurityResult.Accept(envelope.SourceId, envelope.Sequence);
        }

        if (envelope.SecurityTag is null || envelope.SecurityTag.Length == 0)
        {
            return HydronomSecurityResult.Reject(
                envelope.SourceId,
                envelope.Sequence,
                "SECURITY_TAG_MISSING",
                "Mesaj HMAC etiketi taşımıyor.");
        }

        var expected = ComputeTag(envelope);

        if (!CryptographicOperations.FixedTimeEquals(expected, envelope.SecurityTag))
        {
            return HydronomSecurityResult.Reject(
                envelope.SourceId,
                envelope.Sequence,
                "SECURITY_TAG_INVALID",
                "Mesaj HMAC doğrulamasından geçemedi.");
        }

        return HydronomSecurityResult.Accept(envelope.SourceId, envelope.Sequence);
    }

    private byte[] ComputeTag(CommunicationEnvelope envelope)
    {
        using var hmac = new HMACSHA256(_secretKey);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(envelope.Protocol ?? "HYDRONOM");
        writer.Write(envelope.Version);
        writer.Write((ushort)envelope.Type);
        writer.Write((byte)envelope.Priority);

        var flagsWithoutSigned = envelope.Flags & ~HydronomMessageFlags.IsSigned;
        writer.Write((ushort)flagsWithoutSigned);

        writer.Write(envelope.Sequence);
        writer.Write(envelope.TimestampUnixMs);

        writer.Write(envelope.SessionId ?? "");
        writer.Write(envelope.SourceId ?? "");
        writer.Write(envelope.TargetId ?? "");
        writer.Write(envelope.VehicleId ?? "");
        writer.Write(envelope.CorrelationId ?? "");
        writer.Write(envelope.ContentType ?? "");

        writer.Write(envelope.Payload.Length);
        writer.Write(envelope.Payload);

        foreach (var item in envelope.Metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.Write(item.Key);
            writer.Write(item.Value);
        }

        writer.Flush();

        return hmac.ComputeHash(stream.ToArray());
    }
}