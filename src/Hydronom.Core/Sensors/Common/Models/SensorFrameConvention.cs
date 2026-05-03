namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// SensÃ¶r verisinin hangi frame convention ile yorumlanacaÄŸÄ±nÄ± belirtir.
    ///
    /// Bu deÄŸer ileride frame transform, calibration ve sensor fusion iÃ§in Ã¶nemlidir.
    /// </summary>
    public enum SensorFrameConvention
    {
        Unknown = 0,

        World = 10,
        Map = 11,
        LocalNed = 12,
        LocalEnu = 13,

        Body = 30,
        BaseLink = 31,

        SensorFrame = 50,
        CameraOptical = 51,
        LidarFrame = 52,
        ImuFrame = 53,
        GpsFrame = 54,

        Custom = 1000
    }
}

