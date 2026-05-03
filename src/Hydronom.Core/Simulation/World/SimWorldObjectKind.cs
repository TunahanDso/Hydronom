namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nda temsil edilebilecek nesne tÃ¼rleri.
    /// </summary>
    public enum SimWorldObjectKind
    {
        Unknown = 0,

        Generic = 1,

        StaticObstacle = 10,
        DynamicObstacle = 11,
        Terrain = 12,
        Structure = 13,
        Dock = 14,
        Wall = 15,

        Buoy = 30,
        Gate = 31,
        Target = 32,
        Waypoint = 33,
        Marker = 34,

        Vehicle = 50,
        GhostVehicle = 51,
        ReplayGhost = 52,

        NoGoZone = 70,
        InspectionZone = 71,
        OperationArea = 72,
        SafeZone = 73,

        WaterSurface = 90,
        CurrentField = 91,
        WindField = 92,

        Custom = 1000
    }
}
