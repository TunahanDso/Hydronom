namespace Hydronom.Core.Sensors.Pico.Protocol;

/// <summary>
/// Pico sensör protokolünün sürüm bilgisidir.
/// Firmware ile Hydronom C# runtime arasında protokol uyumluluğu kontrolü için kullanılır.
/// </summary>
public readonly record struct PicoSensorProtocolVersion(
    byte Major,
    byte Minor,
    byte Patch
)
{
    public static PicoSensorProtocolVersion Unknown => new(0, 0, 0);

    public static PicoSensorProtocolVersion Current => new(1, 0, 0);

    public bool IsKnown => Major > 0;

    public bool IsCompatibleWith(PicoSensorProtocolVersion other)
    {
        var safeOther = other.Sanitized();

        if (!IsKnown || !safeOther.IsKnown)
        {
            return false;
        }

        return Major == safeOther.Major;
    }

    public PicoSensorProtocolVersion Sanitized()
    {
        return new PicoSensorProtocolVersion(Major, Minor, Patch);
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}";
    }
}