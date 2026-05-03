namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 2D dÃ¼nya ÅŸekilleri iÃ§in ortak arayÃ¼z.
    ///
    /// No-go zone, inspection area, waypoint bÃ¶lgesi ve 2D mission control katmanlarÄ±
    /// bu arayÃ¼zden tÃ¼reyen modellerle temsil edilir.
    /// </summary>
    public interface SimShape2D
    {
        SimShapeKind Kind { get; }

        SimVector2 Center { get; }

        bool IsFinite { get; }

        SimShape2D Sanitized();

        bool Contains(SimVector2 point);

        SimRectangle GetBoundingRectangle();
    }
}
