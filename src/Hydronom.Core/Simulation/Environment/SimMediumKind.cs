namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// AracÄ±n veya dÃ¼nya bÃ¶lgesinin Ã§alÄ±ÅŸtÄ±ÄŸÄ± ana ortam tÃ¼rÃ¼.
    ///
    /// Bu bilgi physics, sensor model, task compatibility ve safety kararlarÄ±nda kullanÄ±labilir.
    /// </summary>
    public enum SimMediumKind
    {
        Unknown = 0,

        Vacuum = 1,

        Air = 10,

        SurfaceWater = 20,
        Underwater = 21,

        Ground = 30,
        Indoor = 31,

        Hybrid = 50,

        Custom = 1000
    }
}
