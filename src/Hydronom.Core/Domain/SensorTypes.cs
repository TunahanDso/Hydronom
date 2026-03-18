using System;

namespace Hydronom.Core.Domain
{
    /// <summary>IMU verisi (m/s^2 ve rad/s; euler opsiyoneldir).</summary>
    public readonly record struct ImuData(
        DateTime Stamp,
        // İvme (m/s^2)
        double Ax, double Ay, double Az,
        // Açısal hız (rad/s)
        double Gx, double Gy, double Gz,
        // Opsiyonel euler (deg) – yoksa NaN bırak
        double RollDeg, double PitchDeg, double YawDeg
    );

    /// <summary>2D LiDAR taraması (ROS LaserScan ile uyumlu alan adları).</summary>
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

    /// <summary>Kamera karesi referansı (ham piksel taşımayız—referans taşır).</summary>
    public sealed record CameraFrameRef(
        DateTime Stamp,
        string FrameId,
        int Width,
        int Height,
        string Encoding,      // örn: "rgb8", "bgr8", "mono8"
        string BufferRef      // paylaşımlı bellek yolu / dosya yolu / handle
    );
}
