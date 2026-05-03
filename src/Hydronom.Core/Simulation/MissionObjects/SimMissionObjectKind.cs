namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nda gÃ¶rev anlamÄ± taÅŸÄ±yan nesne tÃ¼rleri.
    ///
    /// Bu enum mission editor, Ops world layer, task planner ve Ã¶zel gÃ¶rev senaryolarÄ±
    /// tarafÄ±ndan ortak dil olarak kullanÄ±labilir.
    /// </summary>
    public enum SimMissionObjectKind
    {
        Unknown = 0,

        Generic = 1,

        Target = 10,
        Waypoint = 11,
        Dock = 12,
        Buoy = 13,
        Gate = 14,

        InspectionZone = 30,
        NoGoZone = 31,
        SafeZone = 32,
        OperationArea = 33,

        PickupPoint = 50,
        DropoffPoint = 51,

        SearchArea = 70,
        PatrolArea = 71,

        Custom = 1000
    }
}
