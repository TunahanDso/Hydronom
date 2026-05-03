// Hydronom.Core/Domain/ThrusterDesc.cs
namespace Hydronom.Core.Domain
{
    /// <summary>
    /// AraÃ§taki bir itki Ã¼nitesinin betimi (6DoF iÃ§in gerekli tÃ¼m bilgiler).
    /// - Id: MantÄ±ksal isim (CH0, CH1â€¦)
    /// - Channel: PWM/bridge kanalÄ±
    /// - Position: GÃ¶vde koordinatÄ±nda konum (metre)
    /// - ForceDir: Ä°tki yÃ¶nÃ¼ (DAÄ°MA normalize edilir)
    /// - Reversed: YazÄ±lÄ±msal yÃ¶n kalibrasyonu. true ise motor komut iÅŸareti ters Ã§evrilir.
    /// - CanReverse: Motor/ESC fiziksel olarak negatif komutu destekliyor mu?
    /// </summary>
    public readonly record struct ThrusterDesc(
        string Id,
        int Channel,
        Vec3 Position,
        Vec3 ForceDir,
        bool Reversed = false,
        bool CanReverse = false
    )
    {
        public ThrusterDesc Normalize() =>
            new ThrusterDesc(
                Id,
                Channel,
                Position,
                ForceDir.Normalize(),
                Reversed,
                CanReverse
            );
    }
}
