using Hydronom.Core.Communication.Security;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Abstractions;

public interface IHydronomSecurityProvider
{
    string ProviderName { get; }

    CommunicationEnvelope Protect(
        CommunicationEnvelope envelope,
        HydronomSecurityProfile profile);

    HydronomSecurityResult Verify(
        CommunicationEnvelope envelope,
        HydronomSecurityProfile profile);
}