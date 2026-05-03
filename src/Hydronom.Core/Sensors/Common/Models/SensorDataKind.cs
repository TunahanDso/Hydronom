namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// SensorSample iÃ§indeki verinin genel tÃ¼rÃ¼.
    ///
    /// Fusion, state estimation, diagnostics ve telemetry katmanlarÄ±
    /// sample'Ä±n hangi sensÃ¶r ailesinden geldiÄŸini bu enum Ã¼zerinden anlayabilir.
    /// </summary>
    public enum SensorDataKind
    {
        Unknown = 0,

        Imu = 10,
        Ahrs = 11,
        Gyroscope = 12,
        Accelerometer = 13,
        Magnetometer = 14,

        Gps = 30,
        Gnss = 31,
        RtkGps = 32,
        AcousticPositioning = 33,
        UwbPositioning = 34,

        Lidar = 50,
        Sonar = 51,
        Radar = 52,
        Rangefinder = 53,

        Camera = 70,
        DepthCamera = 71,
        ThermalCamera = 72,
        VisionDetection = 73,

        Depth = 90,
        Pressure = 91,
        Dvl = 92,
        WaterSpeed = 93,
        AirSpeed = 94,

        Encoder = 110,
        WheelEncoder = 111,
        MotorEncoder = 112,
        RudderAngle = 113,
        SailAngle = 114,
        SteeringAngle = 115,

        Battery = 130,
        Power = 131,
        MotorHealth = 132,
        ComputeHealth = 133,
        Leak = 134,

        Environment = 150,
        Wind = 151,
        Weather = 152,
        WaterQuality = 153,

        Perception = 170,
        SemanticObject = 171,
        AcousticEvent = 172,

        Custom = 1000
    }
}

