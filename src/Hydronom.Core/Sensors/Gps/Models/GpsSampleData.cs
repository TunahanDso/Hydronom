namespace Hydronom.Core.Sensors.Gps.Models
{
    /// <summary>
    /// GPS/GNSS konum verisi.
    ///
    /// Lat/Lon global konum iÃ§in, X/Y lokal projeksiyon iÃ§in kullanÄ±labilir.
    /// BazÄ± platformlarda global GPS olmayabilir; bu durumda position capability baÅŸka sensÃ¶rlerden Ã¼retilebilir.
    /// </summary>
    public readonly record struct GpsSampleData(
        double? Latitude,
        double? Longitude,
        double? AltitudeMeters,
        double? X,
        double? Y,
        double? Z,
        double? SpeedMps,
        double? CourseDeg,
        double? Hdop,
        int FixType,
        int Satellites
    )
    {
        public static GpsSampleData Empty => new(
            Latitude: null,
            Longitude: null,
            AltitudeMeters: null,
            X: null,
            Y: null,
            Z: null,
            SpeedMps: null,
            CourseDeg: null,
            Hdop: null,
            FixType: 0,
            Satellites: 0
        );

        public bool HasGlobalFix =>
            Latitude.HasValue &&
            Longitude.HasValue &&
            FixType > 0;

        public bool HasLocalPosition =>
            X.HasValue &&
            Y.HasValue;

        public bool IsFinite =>
            IsNullableFinite(Latitude) &&
            IsNullableFinite(Longitude) &&
            IsNullableFinite(AltitudeMeters) &&
            IsNullableFinite(X) &&
            IsNullableFinite(Y) &&
            IsNullableFinite(Z) &&
            IsNullableFinite(SpeedMps) &&
            IsNullableFinite(CourseDeg) &&
            IsNullableFinite(Hdop);

        public GpsSampleData Sanitized()
        {
            return new GpsSampleData(
                Latitude: SafeNullable(Latitude),
                Longitude: SafeNullable(Longitude),
                AltitudeMeters: SafeNullable(AltitudeMeters),
                X: SafeNullable(X),
                Y: SafeNullable(Y),
                Z: SafeNullable(Z),
                SpeedMps: SafeNullableNonNegative(SpeedMps),
                CourseDeg: SafeNullable(CourseDeg),
                Hdop: SafeNullableNonNegative(Hdop),
                FixType: FixType < 0 ? 0 : FixType,
                Satellites: Satellites < 0 ? 0 : Satellites
            );
        }

        private static bool IsNullableFinite(double? value)
        {
            return !value.HasValue || double.IsFinite(value.Value);
        }

        private static double? SafeNullable(double? value)
        {
            if (!value.HasValue)
                return null;

            return double.IsFinite(value.Value) ? value.Value : null;
        }

        private static double? SafeNullableNonNegative(double? value)
        {
            if (!value.HasValue || !double.IsFinite(value.Value))
                return null;

            return value.Value < 0.0 ? 0.0 : value.Value;
        }
    }
}


