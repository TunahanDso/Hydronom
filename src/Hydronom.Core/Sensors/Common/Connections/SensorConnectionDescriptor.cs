using System;
using System.Collections.Generic;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Connections
{
    /// <summary>
    /// Bir sensöre nasıl bağlanılacağını tanımlayan platform-bağımsız bağlantı modeli.
    ///
    /// Bu sınıf sensörün markasını bilmez.
    /// Sadece "nereden ve nasıl erişilir?" sorusunu cevaplar.
    ///
    /// Örnek:
    /// - Serial COM7 115200
    /// - Usb VID/PID
    /// - TCP 192.168.1.10:9000
    /// - Simulation channel
    /// - Replay file path
    /// </summary>
    public sealed record SensorConnectionDescriptor
    {
        public SensorConnectionType Type { get; init; } = SensorConnectionType.Unknown;

        public string PortName { get; init; } = string.Empty;

        public int? BaudRate { get; init; }

        public string Host { get; init; } = string.Empty;

        public int? Port { get; init; }

        public string DevicePath { get; init; } = string.Empty;

        public string UsbVendorId { get; init; } = string.Empty;

        public string UsbProductId { get; init; } = string.Empty;

        public string BusId { get; init; } = string.Empty;

        public int? I2cAddress { get; init; }

        public int? SpiChipSelect { get; init; }

        public string ReplayPath { get; init; } = string.Empty;

        public string SimulationChannel { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsAuto =>
            Type == SensorConnectionType.Unknown ||
            IsAutoText(PortName) ||
            IsAutoText(DevicePath) ||
            IsAutoText(Host);

        public bool HasUsableEndpoint =>
            Type switch
            {
                SensorConnectionType.Serial or SensorConnectionType.UsbSerial or SensorConnectionType.Uart
                    => !string.IsNullOrWhiteSpace(PortName) || !string.IsNullOrWhiteSpace(DevicePath),

                SensorConnectionType.NetworkTcp or SensorConnectionType.NetworkUdp or SensorConnectionType.NetworkHttp or SensorConnectionType.NetworkRtsp
                    => !string.IsNullOrWhiteSpace(Host) && Port.HasValue,

                SensorConnectionType.I2c
                    => !string.IsNullOrWhiteSpace(BusId) && I2cAddress.HasValue,

                SensorConnectionType.Spi
                    => !string.IsNullOrWhiteSpace(BusId) && SpiChipSelect.HasValue,

                SensorConnectionType.FileReplay
                    => !string.IsNullOrWhiteSpace(ReplayPath),

                SensorConnectionType.Simulation
                    => true,

                SensorConnectionType.NativeLibrary
                    => !string.IsNullOrWhiteSpace(DevicePath),

                SensorConnectionType.Usb or SensorConnectionType.Csi
                    => !string.IsNullOrWhiteSpace(DevicePath) ||
                       !string.IsNullOrWhiteSpace(UsbVendorId) ||
                       !string.IsNullOrWhiteSpace(UsbProductId),

                _ => false
            };

        public static SensorConnectionDescriptor Auto(SensorConnectionType type = SensorConnectionType.Unknown)
        {
            return new SensorConnectionDescriptor
            {
                Type = type,
                PortName = "auto",
                DevicePath = "auto"
            };
        }

        public static SensorConnectionDescriptor Serial(string portName, int? baudRate = null)
        {
            return new SensorConnectionDescriptor
            {
                Type = SensorConnectionType.Serial,
                PortName = portName?.Trim() ?? string.Empty,
                BaudRate = baudRate
            }.Sanitized();
        }

        public static SensorConnectionDescriptor UsbSerial(string portName, int? baudRate = null)
        {
            return new SensorConnectionDescriptor
            {
                Type = SensorConnectionType.UsbSerial,
                PortName = portName?.Trim() ?? string.Empty,
                BaudRate = baudRate
            }.Sanitized();
        }

        public static SensorConnectionDescriptor Simulation(string channel = "default")
        {
            return new SensorConnectionDescriptor
            {
                Type = SensorConnectionType.Simulation,
                SimulationChannel = string.IsNullOrWhiteSpace(channel) ? "default" : channel.Trim()
            }.Sanitized();
        }

        public static SensorConnectionDescriptor Replay(string path)
        {
            return new SensorConnectionDescriptor
            {
                Type = SensorConnectionType.FileReplay,
                ReplayPath = path?.Trim() ?? string.Empty
            }.Sanitized();
        }

        public static SensorConnectionDescriptor NetworkTcp(string host, int port)
        {
            return new SensorConnectionDescriptor
            {
                Type = SensorConnectionType.NetworkTcp,
                Host = host?.Trim() ?? string.Empty,
                Port = port
            }.Sanitized();
        }

        public SensorConnectionDescriptor Sanitized()
        {
            return this with
            {
                PortName = Normalize(PortName),
                Host = Normalize(Host),
                DevicePath = Normalize(DevicePath),
                UsbVendorId = Normalize(UsbVendorId),
                UsbProductId = Normalize(UsbProductId),
                BusId = Normalize(BusId),
                ReplayPath = Normalize(ReplayPath),
                SimulationChannel = string.IsNullOrWhiteSpace(SimulationChannel)
                    ? string.Empty
                    : SimulationChannel.Trim(),
                Metadata = SanitizeMetadata(Metadata)
            };
        }

        public override string ToString()
        {
            var clean = Sanitized();

            return clean.Type switch
            {
                SensorConnectionType.Serial or SensorConnectionType.UsbSerial or SensorConnectionType.Uart
                    => $"{clean.Type}:{clean.PortName}@{clean.BaudRate?.ToString() ?? "auto"}",

                SensorConnectionType.NetworkTcp or SensorConnectionType.NetworkUdp or SensorConnectionType.NetworkHttp or SensorConnectionType.NetworkRtsp
                    => $"{clean.Type}:{clean.Host}:{clean.Port?.ToString() ?? "auto"}",

                SensorConnectionType.I2c
                    => $"{clean.Type}:{clean.BusId}:0x{clean.I2cAddress.GetValueOrDefault():X2}",

                SensorConnectionType.Spi
                    => $"{clean.Type}:{clean.BusId}:cs{clean.SpiChipSelect?.ToString() ?? "auto"}",

                SensorConnectionType.FileReplay
                    => $"{clean.Type}:{clean.ReplayPath}",

                SensorConnectionType.Simulation
                    => $"{clean.Type}:{clean.SimulationChannel}",

                _ => clean.Type.ToString()
            };
        }

        private static bool IsAutoText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim();

            return text.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("*", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static IReadOnlyDictionary<string, string> SanitizeMetadata(
            IReadOnlyDictionary<string, string>? metadata)
        {
            if (metadata is null || metadata.Count == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                result[pair.Key.Trim()] = pair.Value?.Trim() ?? string.Empty;
            }

            return result;
        }
    }
}