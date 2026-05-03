// Hydronom.Runtime/Buses/IExternalPoseProvider.cs
using System;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// DÄ±ÅŸ poz kaynaÄŸÄ± (ExternalState) saÄŸlayÄ±cÄ±sÄ±.
    /// </summary>
    public interface IExternalPoseProvider
    {
        /// <summary>Son dÄ±ÅŸ pozu dÃ¶ndÃ¼rÃ¼r (varsa).</summary>
        bool TryGetLatestExternal(out ExternalPose pose);

        /// <summary>Tazelik penceresi (ms). Bu sÃ¼re iÃ§inde gelen poz "fresh" kabul edilir.</summary>
        int FreshMs { get; }

        /// <summary>DÄ±ÅŸ durumu tercih et (Capability vb. ile deÄŸiÅŸtirilebilir).</summary>
        bool PreferExternal { get; }
    }

    /// <summary>DÄ±ÅŸ poz veri taÅŸÄ±yÄ±cÄ±sÄ±.</summary>
    public readonly record struct ExternalPose(
        double X, double Y, double Z,
        double HeadingDeg, double YawRate,
        DateTime TimestampUtc
    )
    {
        public double AgeMs => (DateTime.UtcNow - TimestampUtc).TotalMilliseconds;
    }
}

