using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Abstractions;

public interface IHydronomMessageCompressor
{
    string CompressorName { get; }

    CommunicationEnvelope Compress(CommunicationEnvelope envelope);

    CommunicationEnvelope Decompress(CommunicationEnvelope envelope);
}