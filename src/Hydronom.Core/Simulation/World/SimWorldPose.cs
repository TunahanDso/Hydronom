using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±ndaki bir nesnenin pozisyon ve yÃ¶nelim bilgisi.
    ///
    /// Bu model dÃ¼nya nesneleri, gÃ¶rev objeleri, 3D engeller, hedefler,
    /// sensÃ¶r referanslarÄ± ve Ops 3D Ã§izimleri iÃ§in ortak pose sÃ¶zleÅŸmesidir.
    /// </summary>
    public readonly record struct SimWorldPose(
        SimVector3 Position,
        SimQuaternion Rotation,
        string FrameId
    )
    {
        public static SimWorldPose Zero => new(
            Position: SimVector3.Zero,
            Rotation: SimQuaternion.Identity,
            FrameId: "world"
        );

        public bool IsFinite =>
            Position.IsFinite &&
            Rotation.IsFinite;

        public SimWorldPose Sanitized()
        {
            return new SimWorldPose(
                Position: Position.Sanitized(),
                Rotation: Rotation.Sanitized(),
                FrameId: string.IsNullOrWhiteSpace(FrameId) ? "world" : FrameId.Trim()
            );
        }

        public SimWorldPose WithPosition(SimVector3 position)
        {
            return this with
            {
                Position = position.Sanitized()
            };
        }

        public SimWorldPose WithRotation(SimQuaternion rotation)
        {
            return this with
            {
                Rotation = rotation.Sanitized()
            };
        }
    }
}
