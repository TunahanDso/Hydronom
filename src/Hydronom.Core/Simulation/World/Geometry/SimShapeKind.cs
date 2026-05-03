namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nda kullanÄ±labilecek temel geometri tÃ¼rleri.
    /// </summary>
    public enum SimShapeKind
    {
        Unknown = 0,

        Point = 1,

        Circle = 10,
        Rectangle = 11,
        Polygon = 12,

        Box = 30,
        Sphere = 31,
        Cylinder = 32,
        Mesh = 33,

        Line = 50,
        Polyline = 51,

        Custom = 100
    }
}
