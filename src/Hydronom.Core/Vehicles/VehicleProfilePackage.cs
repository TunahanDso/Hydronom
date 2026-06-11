namespace Hydronom.Core.Vehicles
{
    /// <summary>
    /// Diskten okunan ham Vehicle Profile Package temsilidir.
    ///
    /// Bu model özellikle loader/registry tarafında kullanılır.
    /// İçindeki JSON blokları daha sonra typed profile modellerine dönüştürülür.
    /// </summary>
    public sealed record VehicleProfilePackage(
        string RootDirectory,
        VehicleProfileManifest Manifest,
        string? IdentityJson,
        string? PhysicalJson,
        string? BuoyancyJson,
        string? HydrodynamicsJson,
        string? ActuationJson,
        string? SensorsJson,
        string? ControlJson,
        string? SimulationJson,
        string? CommunicationJson,
        string? TetherJson,
        string? SafetyJson)
    {
        public bool HasIdentity => !string.IsNullOrWhiteSpace(IdentityJson);
        public bool HasPhysical => !string.IsNullOrWhiteSpace(PhysicalJson);
        public bool HasActuation => !string.IsNullOrWhiteSpace(ActuationJson);
        public bool HasSimulation => !string.IsNullOrWhiteSpace(SimulationJson);
        public bool HasSafety => !string.IsNullOrWhiteSpace(SafetyJson);
    }
}