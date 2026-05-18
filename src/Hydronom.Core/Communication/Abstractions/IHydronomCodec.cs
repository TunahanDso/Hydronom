using Hydronom.Core.Communication.Envelope;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Abstractions;

public interface IHydronomCodec
{
    string CodecName { get; }

    string ContentType { get; }

    HydronomEncodedMessage Encode(CommunicationEnvelope envelope);

    CommunicationEnvelope Decode(HydronomEncodedMessage message);
}