using Hydronom.Core.Communication.Security;

namespace Hydronom.Core.Communication.Commands;

public sealed class HydronomSecureCommandReceiver
{
    private readonly HydronomSecureCommandPipeline _pipeline;
    private readonly HydronomCommandAuthorityValidator _authorityValidator;

    public HydronomSecureCommandReceiver(
        HydronomSecureCommandPipeline pipeline,
        HydronomCommandAuthorityValidator authorityValidator)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _authorityValidator = authorityValidator ?? throw new ArgumentNullException(nameof(authorityValidator));
    }

    public HydronomSecureCommandReceiver(
        string hmacSecretKey,
        HydronomCommandAuthorityPolicy authorityPolicy,
        HydronomSecurityProfile? securityProfile = null,
        bool enableSecurity = true,
        string sessionId = "")
        : this(
            new HydronomSecureCommandPipeline(
                hmacSecretKey,
                securityProfile,
                enableSecurity,
                sessionId),
            new HydronomCommandAuthorityValidator(authorityPolicy))
    {
    }

    public HydronomSecureCommandReceiveResult Receive(byte[] packetBytes)
    {
        ArgumentNullException.ThrowIfNull(packetBytes);

        var packetResult = _pipeline.ReadIncomingCommandPacket(packetBytes);

        if (packetResult.Accepted)
        {
            if (packetResult.Command is null || packetResult.Envelope is null)
            {
                return HydronomSecureCommandReceiveResult.RejectCommandInvalid(
                    packetResult with
                    {
                        Reason = "COMMAND_PACKET_ACCEPTED_BUT_MISSING_COMMAND_OR_ENVELOPE"
                    });
            }

            var authorityDecision = _authorityValidator.Validate(packetResult.Command);

            if (!authorityDecision.Allowed)
            {
                return HydronomSecureCommandReceiveResult.RejectAuthority(
                    packetResult,
                    authorityDecision);
            }

            return HydronomSecureCommandReceiveResult.Accept(
                packetResult.Command,
                packetResult.Envelope,
                packetResult.SecurityResult,
                authorityDecision);
        }

        if (packetResult.Reason.StartsWith("BINARY_DECODE_FAILED", StringComparison.Ordinal))
        {
            return HydronomSecureCommandReceiveResult.RejectDecode(
                packetResult.Reason);
        }

        if (packetResult.SecurityResult is { Accepted: false })
        {
            return HydronomSecureCommandReceiveResult.RejectSecurity(packetResult);
        }

        if (packetResult.Reason.StartsWith("COMMAND_ENVELOPE_INVALID", StringComparison.Ordinal))
        {
            return HydronomSecureCommandReceiveResult.RejectCommandInvalid(packetResult);
        }

        return HydronomSecureCommandReceiveResult.RejectCommandInvalid(packetResult);
    }
}