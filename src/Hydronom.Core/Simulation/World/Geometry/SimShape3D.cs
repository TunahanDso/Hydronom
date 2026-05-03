namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 3D dÃ¼nya ÅŸekilleri iÃ§in ortak arayÃ¼z.
    ///
    /// Ops 3D tactical view, 3D engeller, hacimsel bÃ¶lgeler, sualtÄ± hedefleri ve
    /// hava/yer simÃ¼lasyon nesneleri bu arayÃ¼zden tÃ¼reyen modellerle temsil edilir.
    /// </summary>
    public interface SimShape3D
    {
        SimShapeKind Kind { get; }

        SimVector3 Center { get; }

        bool IsFinite { get; }

        SimShape3D Sanitized();

        bool Contains(SimVector3 point);

        SimBox GetBoundingBox();
    }
}
