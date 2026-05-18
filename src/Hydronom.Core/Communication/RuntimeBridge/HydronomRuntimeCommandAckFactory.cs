namespace Hydronom.Core.Communication.RuntimeBridge;

public static class HydronomRuntimeCommandAckFactory
{
    public static HydronomRuntimeCommandAck FromReceiveResult(
        HydronomSecureRuntimeCommandReceiveResult receiveResult)
    {
        ArgumentNullException.ThrowIfNull(receiveResult);

        if (receiveResult.Accepted && receiveResult.Intent is not null)
        {
            return HydronomRuntimeCommandAck.Create(
                receiveResult.Intent,
                HydronomRuntimeCommandAckStatus.Accepted,
                "SECURE_RUNTIME_COMMAND_ACCEPTED");
        }

        var status = receiveResult.Status switch
        {
            HydronomSecureRuntimeCommandStatus.DecodeRejected =>
                HydronomRuntimeCommandAckStatus.RejectedByDecode,

            HydronomSecureRuntimeCommandStatus.SecurityRejected =>
                HydronomRuntimeCommandAckStatus.RejectedBySecurity,

            HydronomSecureRuntimeCommandStatus.CommandInvalid =>
                HydronomRuntimeCommandAckStatus.Rejected,

            HydronomSecureRuntimeCommandStatus.AuthorityRejected =>
                HydronomRuntimeCommandAckStatus.RejectedByAuthority,

            HydronomSecureRuntimeCommandStatus.RuntimeBridgeRejected =>
                HydronomRuntimeCommandAckStatus.RejectedByRuntimeBridge,

            _ =>
                HydronomRuntimeCommandAckStatus.Rejected
        };

        return HydronomRuntimeCommandAck.CreateRejectedFromCommand(
            receiveResult.Command,
            status,
            string.IsNullOrWhiteSpace(receiveResult.Reason)
                ? "SECURE_RUNTIME_COMMAND_REJECTED"
                : receiveResult.Reason,
            receiveResult.Issues);
    }

    public static HydronomRuntimeCommandAck QueuedForSafetyGate(
        HydronomRuntimeCommandIntent intent,
        string reason = "QUEUED_FOR_SAFETY_GATE")
    {
        return HydronomRuntimeCommandAck.Create(
            intent,
            HydronomRuntimeCommandAckStatus.QueuedForSafetyGate,
            reason);
    }

    public static HydronomRuntimeCommandAck QueuedForExecution(
        HydronomRuntimeCommandIntent intent,
        string reason = "QUEUED_FOR_EXECUTION")
    {
        return HydronomRuntimeCommandAck.Create(
            intent,
            HydronomRuntimeCommandAckStatus.QueuedForExecution,
            reason);
    }

    public static HydronomRuntimeCommandAck Applied(
        HydronomRuntimeCommandIntent intent,
        string reason = "COMMAND_APPLIED")
    {
        return HydronomRuntimeCommandAck.Create(
            intent,
            HydronomRuntimeCommandAckStatus.Applied,
            reason);
    }

    public static HydronomRuntimeCommandAck RejectedBySafetyGate(
        HydronomRuntimeCommandIntent intent,
        string reason,
        params string[] issues)
    {
        return HydronomRuntimeCommandAck.Create(
            intent,
            HydronomRuntimeCommandAckStatus.RejectedBySafetyGate,
            string.IsNullOrWhiteSpace(reason)
                ? "REJECTED_BY_SAFETY_GATE"
                : reason,
            issues);
    }

    public static HydronomRuntimeCommandAck Failed(
        HydronomRuntimeCommandIntent intent,
        string reason,
        params string[] issues)
    {
        return HydronomRuntimeCommandAck.Create(
            intent,
            HydronomRuntimeCommandAckStatus.Failed,
            string.IsNullOrWhiteSpace(reason)
                ? "COMMAND_FAILED"
                : reason,
            issues);
    }

    public static HydronomRuntimeCommandAck Timeout(
        HydronomRuntimeCommandIntent intent,
        string reason = "COMMAND_TIMEOUT")
    {
        return HydronomRuntimeCommandAck.Create(
            intent,
            HydronomRuntimeCommandAckStatus.Timeout,
            reason);
    }
}