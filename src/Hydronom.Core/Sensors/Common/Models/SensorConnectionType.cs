namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// Sensörün fiziksel/mantıksal bağlantı tipini belirtir.
    ///
    /// Bu enum sensör markasını/modelini değil, bağlantı yolunu anlatır.
    /// Örnek:
    /// - RPLIDAR: çoğunlukla Serial/UsbSerial
    /// - LDRobot D500: çoğunlukla Serial/UsbSerial
    /// - GPS M8N: Serial/Uart
    /// - Kamera: Usb/Csi/Network
    /// </summary>
    public enum SensorConnectionType
    {
        Unknown = 0,

        Serial = 10,
        UsbSerial = 11,
        Uart = 12,

        I2c = 20,
        Spi = 21,
        Can = 22,

        Usb = 30,
        Csi = 31,

        NetworkTcp = 40,
        NetworkUdp = 41,
        NetworkHttp = 42,
        NetworkRtsp = 43,

        NativeLibrary = 60,
        FileReplay = 70,
        Simulation = 80,

        Custom = 1000
    }
}