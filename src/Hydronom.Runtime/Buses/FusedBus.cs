using System;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// Fused veriler için olay tetikleyen SensorBus türevi.
    /// </summary>
    public class FusedBus : SensorBus
    {
        /// <summary>Yeni fused veri geldiğinde tetiklenir.</summary>
        public event Action<object>? FusedUpdated;

        /// <summary>Fused yayın + olay.</summary>
        public void PublishFused<T>(T data, DateTime stamp) where T : class
        {
            Publish(data, stamp);
            FusedUpdated?.Invoke(data!);
        }
    }
}
