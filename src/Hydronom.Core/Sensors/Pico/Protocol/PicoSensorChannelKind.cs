using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Pico.Protocol;

/// <summary>
/// Pico üzerinden gelen verinin hangi sensör kanalına ait olduğunu belirtir.
/// Bu enum fiziksel sensör markasını değil, Hydronom açısından veri ailesini anlatır.
/// </summary>
public enum PicoSensorChannelKind
{
    Unknown = 0,

    Imu = 10,
    Ahrs = 11,

    Gps = 20,
    Gnss = 21,

    Lidar = 30,
    Sonar = 40,

    Depth = 50,
    Pressure = 51,

    Power = 60,
    Battery = 61,

    Encoder = 70,
    MotorEncoder = 71,

    Wind = 80,
    Environment = 90,

    /// <summary>
    /// Kamera Pico hattına dahil değildir; bu kanal sadece tetik/senkron gibi düşük bant genişlikli yardımcı sinyaller içindir.
    /// </summary>
    CameraTrigger = 100,

    Custom = 1000
}

/// <summary>
/// Pico kanal türlerini Hydronom ortak sensör veri türlerine çevirir.
/// </summary>
public static class PicoSensorChannelKindExtensions
{
    public static SensorDataKind ToSensorDataKind(this PicoSensorChannelKind channel)
    {
        return channel switch
        {
            PicoSensorChannelKind.Imu => SensorDataKind.Imu,
            PicoSensorChannelKind.Ahrs => SensorDataKind.Ahrs,

            PicoSensorChannelKind.Gps => SensorDataKind.Gps,
            PicoSensorChannelKind.Gnss => SensorDataKind.Gnss,

            PicoSensorChannelKind.Lidar => SensorDataKind.Lidar,
            PicoSensorChannelKind.Sonar => SensorDataKind.Sonar,

            PicoSensorChannelKind.Depth => SensorDataKind.Depth,
            PicoSensorChannelKind.Pressure => SensorDataKind.Pressure,

            PicoSensorChannelKind.Power => SensorDataKind.Power,
            PicoSensorChannelKind.Battery => SensorDataKind.Battery,

            PicoSensorChannelKind.Encoder => SensorDataKind.Encoder,
            PicoSensorChannelKind.MotorEncoder => SensorDataKind.MotorEncoder,

            PicoSensorChannelKind.Wind => SensorDataKind.Wind,
            PicoSensorChannelKind.Environment => SensorDataKind.Environment,

            PicoSensorChannelKind.CameraTrigger => SensorDataKind.Camera,

            _ => SensorDataKind.Unknown
        };
    }

    public static string DefaultFrameId(this PicoSensorChannelKind channel)
    {
        return channel switch
        {
            PicoSensorChannelKind.Imu => "imu_link",
            PicoSensorChannelKind.Ahrs => "imu_link",

            PicoSensorChannelKind.Gps => "gps_link",
            PicoSensorChannelKind.Gnss => "gps_link",

            PicoSensorChannelKind.Lidar => "lidar_link",
            PicoSensorChannelKind.Sonar => "sonar_link",

            PicoSensorChannelKind.Depth => "depth_link",
            PicoSensorChannelKind.Pressure => "pressure_link",

            PicoSensorChannelKind.Power => "power_link",
            PicoSensorChannelKind.Battery => "battery_link",

            PicoSensorChannelKind.Encoder => "encoder_link",
            PicoSensorChannelKind.MotorEncoder => "motor_encoder_link",

            PicoSensorChannelKind.Wind => "wind_link",
            PicoSensorChannelKind.Environment => "environment_link",

            PicoSensorChannelKind.CameraTrigger => "camera_trigger_link",

            _ => "sensor_link"
        };
    }

    public static string DefaultSensorId(this PicoSensorChannelKind channel, byte channelIndex)
    {
        var prefix = channel switch
        {
            PicoSensorChannelKind.Imu => "imu",
            PicoSensorChannelKind.Ahrs => "ahrs",

            PicoSensorChannelKind.Gps => "gps",
            PicoSensorChannelKind.Gnss => "gnss",

            PicoSensorChannelKind.Lidar => "lidar",
            PicoSensorChannelKind.Sonar => "sonar",

            PicoSensorChannelKind.Depth => "depth",
            PicoSensorChannelKind.Pressure => "pressure",

            PicoSensorChannelKind.Power => "power",
            PicoSensorChannelKind.Battery => "battery",

            PicoSensorChannelKind.Encoder => "encoder",
            PicoSensorChannelKind.MotorEncoder => "motor_encoder",

            PicoSensorChannelKind.Wind => "wind",
            PicoSensorChannelKind.Environment => "environment",

            PicoSensorChannelKind.CameraTrigger => "camera_trigger",

            _ => "pico_sensor"
        };

        return $"{prefix}{channelIndex}";
    }
}