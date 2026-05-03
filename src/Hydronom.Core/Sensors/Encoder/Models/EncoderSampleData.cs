namespace Hydronom.Core.Sensors.Encoder.Models
{
    /// <summary>
    /// Encoder / wheel odometry / motor encoder sample verisi.
    ///
    /// Kara araÃ§larÄ±, motor health, odometry ve propulsion feedback iÃ§in kullanÄ±labilir.
    /// </summary>
    public readonly record struct EncoderSampleData(
        string ChannelId,
        long Ticks,
        double? PositionRad,
        double? VelocityRadSec,
        double? Rpm,
        double? DistanceMeters,
        bool DirectionKnown
    )
    {
        public static EncoderSampleData Empty => new(
            ChannelId: "encoder0",
            Ticks: 0,
            PositionRad: null,
            VelocityRadSec: null,
            Rpm: null,
            DistanceMeters: null,
            DirectionKnown: false
        );

        public EncoderSampleData Sanitized()
        {
            return new EncoderSampleData(
                ChannelId: string.IsNullOrWhiteSpace(ChannelId) ? "encoder0" : ChannelId.Trim(),
                Ticks: Ticks,
                PositionRad: SafeNullable(PositionRad),
                VelocityRadSec: SafeNullable(VelocityRadSec),
                Rpm: SafeNullable(Rpm),
                DistanceMeters: SafeNullable(DistanceMeters),
                DirectionKnown: DirectionKnown
            );
        }

        private static double? SafeNullable(double? value)
        {
            if (!value.HasValue)
                return null;

            return double.IsFinite(value.Value) ? value.Value : null;
        }
    }
}


