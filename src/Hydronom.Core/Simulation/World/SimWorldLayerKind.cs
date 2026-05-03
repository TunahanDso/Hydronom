namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// Ops/Gateway/Ground Station tarafÄ±nda ayrÄ±ÅŸtÄ±rÄ±labilecek dÃ¼nya katmanÄ± tÃ¼rleri.
    /// </summary>
    public enum SimWorldLayerKind
    {
        Unknown = 0,

        BaseMap = 1,

        Obstacles = 10,
        DynamicObstacles = 11,

        MissionObjects = 20,
        Targets = 21,
        Waypoints = 22,
        Zones = 23,

        Environment = 30,
        Water = 31,
        Wind = 32,
        Current = 33,
        Weather = 34,
        Terrain = 35,

        SensorDebug = 50,
        LidarDebug = 51,
        SonarDebug = 52,
        CameraDebug = 53,

        PhysicsTruth = 70,
        Replay = 80,

        Custom = 1000
    }
}
