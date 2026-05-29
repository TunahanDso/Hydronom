using System.IO.Ports;

namespace Hydronom.Core.Communication.Transport.SerialRf;

public sealed record SerialRfHydronomTransportOptions
{
    public string TransportId { get; init; } = "serial-rf";

    public string PortName { get; init; } = "COM1";

    public int BaudRate { get; init; } = 57_600;

    public Parity Parity { get; init; } = Parity.None;

    public int DataBits { get; init; } = 8;

    public StopBits StopBits { get; init; } = StopBits.One;

    public bool DtrEnable { get; init; } = true;

    public bool RtsEnable { get; init; } = true;

    public int ReadTimeoutMs { get; init; } = 250;

    public int WriteTimeoutMs { get; init; } = 1000;

    public int ReceiveChannelCapacity { get; init; } = 512;

    public int MaxHeaderBytes { get; init; } = 512;

    public int MaxPayloadBytes { get; init; } = 16 * 1024;

    public string SourceId { get; init; } = "hydronom-node";

    public string TargetId { get; init; } = "hydronom-peer";

    public string ChannelId { get; init; } = "serial-rf";

    public bool DropInvalidFrames { get; init; } = true;

    public static SerialRfHydronomTransportOptions SiK433(
        string portName,
        string transportId = "serial-rf-sik-433")
    {
        return new SerialRfHydronomTransportOptions
        {
            TransportId = transportId,
            PortName = portName,
            BaudRate = 57_600,
            ChannelId = "sik-433",
            MaxPayloadBytes = 8 * 1024,
            ReceiveChannelCapacity = 512
        };
    }

    public static SerialRfHydronomTransportOptions LoRa868(
        string portName,
        string transportId = "serial-rf-lora-868")
    {
        return new SerialRfHydronomTransportOptions
        {
            TransportId = transportId,
            PortName = portName,
            BaudRate = 57_600,
            ChannelId = "lora-868",
            MaxPayloadBytes = 4 * 1024,
            ReceiveChannelCapacity = 256
        };
    }
}