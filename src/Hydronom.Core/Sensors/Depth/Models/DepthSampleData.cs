namespace Hydronom.Core.Sensors.Depth.Models
{
    /// <summary>
    /// Derinlik / basÄ±nÃ§ / irtifa tarzÄ± tek eksenli Ã§evresel Ã¶lÃ§Ã¼m verisi.
    ///
    /// Su altÄ± araÃ§larÄ±nda depth sensor, hava araÃ§larÄ±nda altimeter/barometer,
    /// kara araÃ§larÄ±nda rangefinder gibi sensÃ¶rlerle kullanÄ±labilir.
    /// </summary>
    public readonly record struct DepthSampleData(
        double DepthMeters,
        double? PressureKPa,
        double? AltitudeMeters,
        double? TemperatureC,
        bool Valid
    )
    {
        public static DepthSampleData Empty => new(
            DepthMeters: 0.0,
            PressureKPa: null,
            AltitudeMeters: null,
            TemperatureC: null,
            Valid: false
        );

        public bool IsFinite =>
            double.IsFinite(DepthMeters) &&
            IsNullableFinite(PressureKPa) &&
            IsNullableFinite(AltitudeMeters) &&
            IsNullableFinite(TemperatureC);

        public DepthSampleData Sanitized()
        {
            return new DepthSampleData(
                DepthMeters: Safe(DepthMeters),
                PressureKPa: SafeNullable(PressureKPa),
                AltitudeMeters: SafeNullable(AltitudeMeters),
                TemperatureC: SafeNullable(TemperatureC),
                Valid: Valid && IsFinite
            );
        }

        private static bool IsNullableFinite(double? value)
        {
            return !value.HasValue || double.IsFinite(value.Value);
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double? SafeNullable(double? value)
        {
            if (!value.HasValue)
                return null;

            return double.IsFinite(value.Value) ? value.Value : null;
        }
    }
}


