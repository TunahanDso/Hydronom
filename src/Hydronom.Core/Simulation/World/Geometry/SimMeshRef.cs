namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 3D mesh referansÄ±.
    ///
    /// Bu model gerÃ§ek mesh verisini taÅŸÄ±maz.
    /// Ops/Gateway tarafÄ±na hangi 3D modelin Ã§izileceÄŸini tarif eder.
    ///
    /// Ã–rnek:
    /// - buoy_red.glb
    /// - dock_platform.glb
    /// - underwater_pipe.glb
    /// - obstacle_rock_01.glb
    /// </summary>
    public readonly record struct SimMeshRef(
        string MeshId,
        string Uri,
        SimVector3 LocalScale,
        SimBox Bounds
    ) : SimShape3D
    {
        public SimShapeKind Kind => SimShapeKind.Mesh;

        public SimVector3 Center => Bounds.Center;

        public bool IsFinite =>
            LocalScale.IsFinite &&
            Bounds.IsFinite;

        public SimShape3D Sanitized()
        {
            var safeScale = LocalScale.Sanitized();

            return new SimMeshRef(
                MeshId: Normalize(MeshId, "mesh_unknown"),
                Uri: Normalize(Uri, ""),
                LocalScale: new SimVector3(
                    SafePositive(safeScale.X, 1.0),
                    SafePositive(safeScale.Y, 1.0),
                    SafePositive(safeScale.Z, 1.0)
                ),
                Bounds: Bounds.SanitizedBox()
            );
        }

        public SimMeshRef SanitizedMeshRef()
        {
            return (SimMeshRef)Sanitized();
        }

        public bool Contains(SimVector3 point)
        {
            return Bounds.Contains(point);
        }

        public SimBox GetBoundingBox()
        {
            return Bounds.SanitizedBox();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }
    }
}
