namespace Hydronom.Core.Communication.Envelope;

public sealed record HydronomEncodedMessage
{
    public HydronomMessageType Type { get; init; }

    public HydronomMessagePriority Priority { get; init; }

    public HydronomMessageFlags Flags { get; init; }

    public byte[] Bytes { get; init; } = Array.Empty<byte>();

    public string CodecName { get; init; } = "";

    public int SizeBytes => Bytes.Length;

    public static HydronomEncodedMessage Empty { get; } = new();
}