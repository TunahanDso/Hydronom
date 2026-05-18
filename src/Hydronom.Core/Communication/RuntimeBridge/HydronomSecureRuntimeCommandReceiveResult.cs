using Hydronom.Core.Communication.Commands;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed record HydronomSecureRuntimeCommandReceiveResult
{
    public HydronomSecureRuntimeCommandStatus Status { get; init; } =
        HydronomSecureRuntimeCommandStatus.Unknown;

    public bool Accepted => Status == HydronomSecureRuntimeCommandStatus.Accepted;

    public string Reason { get; init; } = "";

    public HydronomSecureCommandReceiveResult? SecureCommandResult { get; init; }

    public HydronomRuntimeCommandBridgeResult? BridgeResult { get; init; }

    public HydronomRuntimeCommandIntent? Intent { get; init; }

    public HydronomCommandFrame? Command { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public bool IsDecodeRejected => Status == HydronomSecureRuntimeCommandStatus.DecodeRejected;

    public bool IsSecurityRejected => Status == HydronomSecureRuntimeCommandStatus.SecurityRejected;

    public bool IsCommandInvalid => Status == HydronomSecureRuntimeCommandStatus.CommandInvalid;

    public bool IsAuthorityRejected => Status == HydronomSecureRuntimeCommandStatus.AuthorityRejected;

    public bool IsRuntimeBridgeRejected => Status == HydronomSecureRuntimeCommandStatus.RuntimeBridgeRejected;

    public static HydronomSecureRuntimeCommandReceiveResult Accept(
        HydronomSecureCommandReceiveResult secureCommandResult,
        HydronomRuntimeCommandBridgeResult bridgeResult)
    {
        ArgumentNullException.ThrowIfNull(secureCommandResult);
        ArgumentNullException.ThrowIfNull(bridgeResult);

        return new HydronomSecureRuntimeCommandReceiveResult
        {
            Status = HydronomSecureRuntimeCommandStatus.Accepted,
            Reason = "SECURE_RUNTIME_COMMAND_ACCEPTED",
            SecureCommandResult = secureCommandResult,
            BridgeResult = bridgeResult,
            Intent = bridgeResult.Intent,
            Command = secureCommandResult.Command,
            Envelope = secureCommandResult.Envelope
        };
    }

    public static HydronomSecureRuntimeCommandReceiveResult RejectFromSecureCommand(
        HydronomSecureCommandReceiveResult secureCommandResult)
    {
        ArgumentNullException.ThrowIfNull(secureCommandResult);

        var status = secureCommandResult.Status switch
        {
            HydronomSecureCommandReceiveStatus.DecodeRejected =>
                HydronomSecureRuntimeCommandStatus.DecodeRejected,

            HydronomSecureCommandReceiveStatus.SecurityRejected =>
                HydronomSecureRuntimeCommandStatus.SecurityRejected,

            HydronomSecureCommandReceiveStatus.CommandInvalid =>
                HydronomSecureRuntimeCommandStatus.CommandInvalid,

            HydronomSecureCommandReceiveStatus.AuthorityRejected =>
                HydronomSecureRuntimeCommandStatus.AuthorityRejected,

            _ =>
                HydronomSecureRuntimeCommandStatus.CommandInvalid
        };

        return new HydronomSecureRuntimeCommandReceiveResult
        {
            Status = status,
            Reason = secureCommandResult.Reason,
            SecureCommandResult = secureCommandResult,
            Command = secureCommandResult.Command,
            Envelope = secureCommandResult.Envelope,
            Issues = secureCommandResult.Issues
        };
    }

    public static HydronomSecureRuntimeCommandReceiveResult RejectRuntimeBridge(
        HydronomSecureCommandReceiveResult secureCommandResult,
        HydronomRuntimeCommandBridgeResult bridgeResult)
    {
        ArgumentNullException.ThrowIfNull(secureCommandResult);
        ArgumentNullException.ThrowIfNull(bridgeResult);

        return new HydronomSecureRuntimeCommandReceiveResult
        {
            Status = HydronomSecureRuntimeCommandStatus.RuntimeBridgeRejected,
            Reason = bridgeResult.Reason,
            SecureCommandResult = secureCommandResult,
            BridgeResult = bridgeResult,
            Command = secureCommandResult.Command,
            Envelope = secureCommandResult.Envelope,
            Issues = bridgeResult.Issues
        };
    }
}