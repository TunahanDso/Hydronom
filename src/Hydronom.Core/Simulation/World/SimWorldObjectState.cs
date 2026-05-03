namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// DÃ¼nya nesnesinin aktiflik ve gÃ¶rev durumu.
    /// </summary>
    public enum SimWorldObjectState
    {
        Unknown = 0,

        Active = 10,
        Inactive = 11,
        Hidden = 12,

        Detected = 30,
        Tracked = 31,
        Lost = 32,

        Completed = 50,
        Failed = 51,

        Disabled = 100
    }
}
