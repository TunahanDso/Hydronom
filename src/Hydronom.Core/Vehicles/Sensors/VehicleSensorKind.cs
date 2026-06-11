namespace Hydronom.Core.Vehicles.Sensors
{
    /// <summary>
    /// Vehicle Profile içindeki sensör ailelerini tanımlar.
    ///
    /// Bu enum; simülasyon, görev uyumluluğu, ground station ve safety tarafında
    /// "araçta hangi algılama kabiliyetleri var?" sorusunu cevaplamak için kullanılır.
    /// </summary>
    public enum VehicleSensorKind
    {
        Unknown = 0,

        Imu = 10,
        Magnetometer = 11,
        Gyroscope = 12,
        Accelerometer = 13,

        Gps = 20,
        Gnss = 21,
        RtkGps = 22,

        DepthSensor = 30,
        PressureSensor = 31,
        Altimeter = 32,

        Camera = 40,
        StereoCamera = 41,
        ThermalCamera = 42,

        Lidar = 50,
        Sonar = 51,
        ImagingSonar = 52,
        Dvl = 53,

        LeakSensor = 60,
        BatteryMonitor = 61,
        CurrentSensor = 62,
        VoltageSensor = 63,
        TemperatureSensor = 64,

        Encoder = 70,
        LimitSwitch = 71,

        Microphone = 80,
        Hydrophone = 81,

        Custom = 1000
    }
}