using Hydronom.Core.Communication.Security;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Commands;

public sealed record HydronomSecureCommandReceiveResult
{
    public HydronomSecureCommandReceiveStatus Status { get; init; } =
        HydronomSecureCommandReceiveStatus.Unknown;

    public bool Accepted => Status == HydronomSecureCommandReceiveStatus.Accepted;

    public string Reason { get; init; } = "";

    public HydronomCommandFrame? Command { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public HydronomSecurityResult? SecurityResult { get; init; }

    public HydronomCommandAuthorityDecision? AuthorityDecision { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public bool IsDecodeRejected => Status == HydronomSecureCommandReceiveStatus.DecodeRejected;

    public bool IsSecurityRejected => Status == HydronomSecureCommandReceiveStatus.SecurityRejected;

    public bool IsCommandInvalid => Status == HydronomSecureCommandReceiveStatus.CommandInvalid;

    public bool IsAuthorityRejected => Status == HydronomSecureCommandReceiveStatus.AuthorityRejected;

    public static HydronomSecureCommandReceiveResult Accept(
        HydronomCommandFrame command,
        CommunicationEnvelope envelope,
        HydronomSecurityResult? securityResult,
        HydronomCommandAuthorityDecision authorityDecision)
    {
        return new HydronomSecureCommandReceiveResult
        {
            Status = HydronomSecureCommandReceiveStatus.Accepted,
            Reason = "SECURE_COMMAND_ACCEPTED",
            Command = command,
            Envelope = envelope,
            SecurityResult = securityResult,
            AuthorityDecision = authorityDecision
        };
    }

    public static HydronomSecureCommandReceiveResult RejectDecode(
        string reason,
        IReadOnlyList<string>? issues = null)
    {
        return new HydronomSecureCommandReceiveResult
        {
            Status = HydronomSecureCommandReceiveStatus.DecodeRejected,
            Reason = reason,
            Issues = issues ?? Array.Empty<string>()
        };
    }

    public static HydronomSecureCommandReceiveResult RejectSecurity(
        HydronomCommandPacketResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        return new HydronomSecureCommandReceiveResult
        {
            Status = HydronomSecureCommandReceiveStatus.SecurityRejected,
            Reason = packetResult.Reason,
            Envelope = packetResult.Envelope,
            SecurityResult = packetResult.SecurityResult,
            Issues = packetResult.SecurityResult?.Issues ?? Array.Empty<string>()
        };
    }

    public static HydronomSecureCommandReceiveResult RejectCommandInvalid(
        HydronomCommandPacketResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        return new HydronomSecureCommandReceiveResult
        {
            Status = HydronomSecureCommandReceiveStatus.CommandInvalid,
            Reason = packetResult.Reason,
            Command = packetResult.Command,
            Envelope = packetResult.Envelope,
            SecurityResult = packetResult.SecurityResult
        };
    }

    public static HydronomSecureCommandReceiveResult RejectAuthority(
        HydronomCommandPacketResult packetResult,
        HydronomCommandAuthorityDecision authorityDecision)
    {
        ArgumentNullException.ThrowIfNull(packetResult);
        ArgumentNullException.ThrowIfNull(authorityDecision);

        return new HydronomSecureCommandReceiveResult
        {
            Status = HydronomSecureCommandReceiveStatus.AuthorityRejected,
            Reason = authorityDecision.Reason,
            Command = packetResult.Command,
            Envelope = packetResult.Envelope,
            SecurityResult = packetResult.SecurityResult,
            AuthorityDecision = authorityDecision,
            Issues = authorityDecision.Issues
        };
    }
}