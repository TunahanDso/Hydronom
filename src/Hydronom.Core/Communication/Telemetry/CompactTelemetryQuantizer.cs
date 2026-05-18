namespace Hydronom.Core.Communication.Telemetry;

public static class CompactTelemetryQuantizer
{
    public static int MetersToCentimeters(double meters)
    {
        return ClampToInt32(RoundAwayFromZero(meters * 100.0));
    }

    public static double CentimetersToMeters(int centimeters)
    {
        return centimeters / 100.0;
    }

    public static short RadToMillirad(double radians)
    {
        return ClampToInt16(RoundAwayFromZero(radians * 1000.0));
    }

    public static double MilliradToRad(short millirad)
    {
        return millirad / 1000.0;
    }

    public static short MpsToCentimetersPerSecond(double metersPerSecond)
    {
        return ClampToInt16(RoundAwayFromZero(metersPerSecond * 100.0));
    }

    public static double CentimetersPerSecondToMps(short centimetersPerSecond)
    {
        return centimetersPerSecond / 100.0;
    }

    public static short RadpsToMilliradPerSecond(double radps)
    {
        return ClampToInt16(RoundAwayFromZero(radps * 1000.0));
    }

    public static double MilliradPerSecondToRadps(short milliradPerSecond)
    {
        return milliradPerSecond / 1000.0;
    }

    public static ushort VoltageToCentivolt(double voltage)
    {
        var value = RoundAwayFromZero(voltage * 100.0);
        return (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
    }

    public static double CentivoltToVoltage(ushort centivolt)
    {
        return centivolt / 100.0;
    }

    public static ushort Ratio01ToPermille(double ratio)
    {
        var value = RoundAwayFromZero(Math.Clamp(ratio, 0.0, 1.0) * 1000.0);
        return (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
    }

    public static double PermilleToRatio01(ushort permille)
    {
        return Math.Clamp(permille / 1000.0, 0.0, 1.0);
    }

    public static short ForceToDecinewton(double forceNewton)
    {
        return ClampToInt16(RoundAwayFromZero(forceNewton * 10.0));
    }

    public static double DecinewtonToForce(short decinewton)
    {
        return decinewton / 10.0;
    }

    public static short TorqueToCentinewtonMeter(double torqueNm)
    {
        return ClampToInt16(RoundAwayFromZero(torqueNm * 100.0));
    }

    public static double CentinewtonMeterToTorque(short centiNewtonMeter)
    {
        return centiNewtonMeter / 100.0;
    }

    private static double RoundAwayFromZero(double value)
    {
        return Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static short ClampToInt16(double value)
    {
        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }

    private static int ClampToInt32(double value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}