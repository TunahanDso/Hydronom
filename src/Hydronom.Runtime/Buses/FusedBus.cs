using System;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// Fused veriler iÃ§in olay tetikleyen SensorBus tÃ¼revi.
    /// </summary>
    public class FusedBus : SensorBus
    {
        /// <summary>Yeni fused veri geldiÄŸinde tetiklenir.</summary>
        public event Action<object>? FusedUpdated;

        /// <summary>Fused yayÄ±n + olay.</summary>
        public void PublishFused<T>(T data, DateTime stamp) where T : class
        {
            Publish(data, stamp);
            FusedUpdated?.Invoke(data!);
        }
    }
}

