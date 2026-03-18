// Hydronom.Core/Domain/ThrusterDesc.cs
namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Araçtaki bir itki ünitesinin betimi (6DoF için gerekli tüm bilgiler).
    /// - Id: Mantıksal isim (CH0, CH1…)
    /// - Channel: PWM/bridge kanalı
    /// - Position: Gövde koordinatında konum (metre)
    /// - ForceDir: İtki yönü (DAİMA normalize edilir)
    /// </summary>
    public readonly record struct ThrusterDesc(
        string Id,
        int Channel,
        Vec3 Position,
        Vec3 ForceDir
    )
    {
        public ThrusterDesc Normalize() =>
            new ThrusterDesc(
                Id,
                Channel,
                Position,
                ForceDir.Normalize() 
            );
    }
}
