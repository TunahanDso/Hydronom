namespace Hydronom.Core.Vehicles
{
    /// <summary>
    /// Hydronom'un desteklediği ana araç/platform ailelerini tanımlar.
    ///
    /// Bu enum doğrudan şu kararları etkiler:
    /// - Hangi physics modeli çalışacak?
    /// - Hangi kontrol kısıtları uygulanacak?
    /// - Hangi sensörler beklenir?
    /// - Araç hangi görevlerle uyumludur?
    /// </summary>
    public enum VehiclePlatformKind
    {
        Unknown = 0,

        SurfaceVessel = 10,
        UnderwaterVehicle = 20,
        MiniRov = 21,

        GroundVehicle = 30,
        AerialVehicle = 40,

        IndustrialMachine = 50,

        Hybrid = 100,
        Custom = 1000
    }
}