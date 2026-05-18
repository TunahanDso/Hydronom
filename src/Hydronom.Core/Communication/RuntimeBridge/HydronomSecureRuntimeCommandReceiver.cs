using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.Security;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed class HydronomSecureRuntimeCommandReceiver
{
    private readonly HydronomSecureCommandReceiver _secureCommandReceiver;
    private readonly HydronomRuntimeCommandBridge _runtimeCommandBridge;

    public HydronomSecureRuntimeCommandReceiver(
        HydronomSecureCommandReceiver secureCommandReceiver,
        HydronomRuntimeCommandBridge runtimeCommandBridge)
    {
        _secureCommandReceiver = secureCommandReceiver
            ?? throw new ArgumentNullException(nameof(secureCommandReceiver));

        _runtimeCommandBridge = runtimeCommandBridge
            ?? throw new ArgumentNullException(nameof(runtimeCommandBridge));
    }

    public HydronomSecureRuntimeCommandReceiver(
        string hmacSecretKey,
        HydronomCommandAuthorityPolicy authorityPolicy,
        HydronomSecurityProfile? securityProfile = null,
        bool enableSecurity = true,
        string sessionId = "")
        : this(
            new HydronomSecureCommandReceiver(
                hmacSecretKey,
                authorityPolicy,
                securityProfile,
                enableSecurity,
                sessionId),
            new HydronomRuntimeCommandBridge())
    {
    }

    public HydronomSecureRuntimeCommandReceiveResult Receive(
        byte[] packetBytes)
    {
        ArgumentNullException.ThrowIfNull(packetBytes);

        var secureResult = _secureCommandReceiver.Receive(packetBytes);

        if (!secureResult.Accepted)
        {
            return HydronomSecureRuntimeCommandReceiveResult
                .RejectFromSecureCommand(secureResult);
        }

        if (secureResult.Command is null)
        {
            return HydronomSecureRuntimeCommandReceiveResult
                .RejectFromSecureCommand(
                    secureResult with
                    {
                        Status = HydronomSecureCommandReceiveStatus.CommandInvalid,
                        Reason = "SECURE_COMMAND_ACCEPTED_BUT_COMMAND_MISSING"
                    });
        }

        var bridgeResult = _runtimeCommandBridge.Convert(
            secureResult.Command);

        if (!bridgeResult.Accepted)
        {
            return HydronomSecureRuntimeCommandReceiveResult
                .RejectRuntimeBridge(
                    secureResult,
                    bridgeResult);
        }

        return HydronomSecureRuntimeCommandReceiveResult
            .Accept(
                secureResult,
                bridgeResult);
    }
}