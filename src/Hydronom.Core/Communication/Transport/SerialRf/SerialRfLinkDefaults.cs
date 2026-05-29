namespace Hydronom.Core.Communication.Transport.SerialRf;

public static class SerialRfLinkDefaults
{
    public const string TransportKind = "serial-rf";

    public const string SiK433Kind = "serial-rf-sik-433";

    public const string LoRa868Kind = "serial-rf-lora-868";

    public const string UsbTetherKind = "serial-usb-tether";

    public const int SiK433DefaultBaudRate = 57_600;

    public const int LoRa868DefaultBaudRate = 57_600;

    public const int SiK433RecommendedMaxPayloadBytes = 8 * 1024;

    public const int LoRa868RecommendedMaxPayloadBytes = 4 * 1024;

    public const bool AllowsVideo = false;

    public const bool AllowsCompactTelemetry = true;

    public const bool AllowsCommands = true;

    public const bool AllowsAcknowledgements = true;

    public const bool AllowsEmergency = true;
}