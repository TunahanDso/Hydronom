namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// DÃ¼nya nesnelerine eklenebilecek esnek etiket modeli.
    ///
    /// Ã–rnekler:
    /// - obstacle
    /// - no_go
    /// - target
    /// - dock
    /// - buoy
    /// - underwater
    /// - visible_in_ops
    /// </summary>
    public readonly record struct SimWorldTag(
        string Key,
        string Value
    )
    {
        public static SimWorldTag Create(string key, string value = "true")
        {
            return new SimWorldTag(
                Key: Normalize(key, "tag"),
                Value: Normalize(value, "true")
            );
        }

        public SimWorldTag Sanitized()
        {
            return new SimWorldTag(
                Key: Normalize(Key, "tag"),
                Value: Normalize(Value, "true")
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
