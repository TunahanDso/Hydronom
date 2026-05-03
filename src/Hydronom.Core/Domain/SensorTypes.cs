using System;

namespace Hydronom.Core.Domain
{
    /// <summary>IMU verisi (m/s^2 ve rad/s; euler opsiyoneldir).</summary>
    public readonly record struct ImuData(
        DateTime Stamp,
        // Ä°vme (m/s^2)
        double Ax, double Ay, double Az,
        // AÃ§Ä±sal hÄ±z (rad/s)
        double Gx, double Gy, double Gz,
        // Opsiyonel euler (deg) â€“ yoksa NaN bÄ±rak
        double RollDeg, double PitchDeg, double YawDeg
    );

    /// <summary>2D LiDAR taramasÄ± (ROS LaserScan ile uyumlu alan adlarÄ±).</summary>
    public sealed record LaserScanData(
        DateTime Stamp,
        string FrameId,
        double AngleMin,      // rad
        double AngleMax,      // rad
        double AngleIncrement,// rad
        double RangeMin,      // m
        double RangeMax,      // m
        float[] Ranges,       // m
        float[]? Intensities  // opsiyonel
    );

    /// <summary>Kamera karesi referansÄ± (ham piksel taÅŸÄ±mayÄ±zâ€”referans taÅŸÄ±r).</summary>
    public sealed record CameraFrameRef(
        DateTime Stamp,
        string FrameId,
        int Width,
        int Height,
        string Encoding,      // Ã¶rn: "rgb8", "bgr8", "mono8"
        string BufferRef      // paylaÅŸÄ±mlÄ± bellek yolu / dosya yolu / handle
    );
}

