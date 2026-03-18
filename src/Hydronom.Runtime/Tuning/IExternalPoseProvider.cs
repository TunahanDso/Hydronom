// Hydronom.Runtime/Buses/IExternalPoseProvider.cs
using System;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// Dış poz kaynağı (ExternalState) sağlayıcısı.
    /// </summary>
    public interface IExternalPoseProvider
    {
        /// <summary>Son dış pozu döndürür (varsa).</summary>
        bool TryGetLatestExternal(out ExternalPose pose);

        /// <summary>Tazelik penceresi (ms). Bu süre içinde gelen poz "fresh" kabul edilir.</summary>
        int FreshMs { get; }

        /// <summary>Dış durumu tercih et (Capability vb. ile değiştirilebilir).</summary>
        bool PreferExternal { get; }
    }

    /// <summary>Dış poz veri taşıyıcısı.</summary>
    public readonly record struct ExternalPose(
        double X, double Y, double Z,
        double HeadingDeg, double YawRate,
        DateTime TimestampUtc
    )
    {
        public double AgeMs => (DateTime.UtcNow - TimestampUtc).TotalMilliseconds;
    }
}
