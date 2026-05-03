using System;

namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// Hydronom sensÃ¶r kimlik modeli.
    ///
    /// AynÄ± tÃ¼rden birden fazla sensÃ¶r olduÄŸunda SourceId ile ayrÄ±m yapÄ±lÄ±r:
    /// - imu0
    /// - imu1
    /// - gps0
    /// - lidar_front
    /// - camera_bow
    /// - dvl0
    /// </summary>
    public readonly record struct SensorIdentity(
        string SensorId,
        string SourceId,
        SensorDataKind DataKind,
        string DisplayName,
        string FrameId,
        SensorFrameConvention FrameConvention
    )
    {
        public static SensorIdentity Create(
            string sensorId,
            string sourceId,
            SensorDataKind dataKind,
            string frameId,
            string displayName = ""
        )
        {
            var id = Normalize(sensorId, sourceId);

            return new SensorIdentity(
                SensorId: id,
                SourceId: Normalize(sourceId, id),
                DataKind: dataKind,
                DisplayName: string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim(),
                FrameId: Normalize(frameId, "base_link"),
                FrameConvention: GuessConvention(frameId)
            ).Sanitized();
        }

        public SensorIdentity Sanitized()
        {
            return new SensorIdentity(
                SensorId: Normalize(SensorId, "sensor"),
                SourceId: Normalize(SourceId, SensorId),
                DataKind: DataKind,
                DisplayName: Normalize(DisplayName, SensorId),
                FrameId: Normalize(FrameId, "base_link"),
                FrameConvention: FrameConvention == SensorFrameConvention.Unknown
                    ? GuessConvention(FrameId)
                    : FrameConvention
            );
        }

        private static SensorFrameConvention GuessConvention(string? frameId)
        {
            if (string.IsNullOrWhiteSpace(frameId))
                return SensorFrameConvention.BaseLink;

            var frame = frameId.Trim().ToLowerInvariant();

            if (frame == "map")
                return SensorFrameConvention.Map;

            if (frame == "world")
                return SensorFrameConvention.World;

            if (frame == "base_link" || frame == "body")
                return SensorFrameConvention.BaseLink;

            if (frame.Contains("imu"))
                return SensorFrameConvention.ImuFrame;

            if (frame.Contains("gps"))
                return SensorFrameConvention.GpsFrame;

            if (frame.Contains("lidar"))
                return SensorFrameConvention.LidarFrame;

            if (frame.Contains("camera"))
                return SensorFrameConvention.CameraOptical;

            return SensorFrameConvention.SensorFrame;
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

