using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Sensors.Camera.Models
{
    /// <summary>
    /// Kamera veya vision pipeline sample verisi.
    ///
    /// Bu model ham gÃ¶rÃ¼ntÃ¼ taÅŸÄ±mak zorunda deÄŸildir.
    /// FrameUri, metadata veya detection listesi taÅŸÄ±yabilir.
    /// </summary>
    public readonly record struct CameraSampleData(
        int Width,
        int Height,
        string Format,
        string FrameUri,
        string JpegBase64,
        IReadOnlyList<string> Detections,
        double? ExposureMs,
        double? Fps
    )
    {
        public static CameraSampleData Empty => new(
            Width: 0,
            Height: 0,
            Format: "unknown",
            FrameUri: "",
            JpegBase64: "",
            Detections: Array.Empty<string>(),
            ExposureMs: null,
            Fps: null
        );

        public CameraSampleData Sanitized()
        {
            return new CameraSampleData(
                Width: Width < 0 ? 0 : Width,
                Height: Height < 0 ? 0 : Height,
                Format: Normalize(Format, "unknown"),
                FrameUri: FrameUri?.Trim() ?? "",
                JpegBase64: JpegBase64?.Trim() ?? "",
                Detections: NormalizeList(Detections),
                ExposureMs: SafeNullableNonNegative(ExposureMs),
                Fps: SafeNullableNonNegative(Fps)
            );
        }

        private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
                return Array.Empty<string>();

            return values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double? SafeNullableNonNegative(double? value)
        {
            if (!value.HasValue || !double.IsFinite(value.Value))
                return null;

            return value.Value < 0.0 ? 0.0 : value.Value;
        }
    }
}


