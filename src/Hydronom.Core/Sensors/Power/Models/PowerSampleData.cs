namespace Hydronom.Core.Sensors.Power.Models
{
    /// <summary>
    /// GÃ¼Ã§/batarya/akÄ±m/voltaj saÄŸlÄ±k sample verisi.
    ///
    /// Advanced Analysis, Safety, Power/Health ve Ops diagnostics iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct PowerSampleData(
        double? BatteryPercent,
        double? Voltage,
        double? CurrentAmp,
        double? PowerWatt,
        double? EstimatedRemainingMinutes,
        double? TemperatureC,
        bool Critical
    )
    {
        public static PowerSampleData Empty => new(
            BatteryPercent: null,
            Voltage: null,
            CurrentAmp: null,
            PowerWatt: null,
            EstimatedRemainingMinutes: null,
            TemperatureC: null,
            Critical: false
        );

        public PowerSampleData Sanitized()
        {
            return new PowerSampleData(
                BatteryPercent: ClampNullable(BatteryPercent, 0.0, 100.0),
                Voltage: SafeNullableNonNegative(Voltage),
                CurrentAmp: SafeNullable(CurrentAmp),
                PowerWatt: SafeNullable(PowerWatt),
                EstimatedRemainingMinutes: SafeNullableNonNegative(EstimatedRemainingMinutes),
                TemperatureC: SafeNullable(TemperatureC),
                Critical: Critical
            );
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

        private static double? ClampNullable(double? value, double min, double max)
        {
            if (!value.HasValue || !double.IsFinite(value.Value))
                return null;

            if (value.Value < min)
                return min;

            if (value.Value > max)
                return max;

            return value.Value;
        }
    }
}


