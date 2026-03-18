using System;
using System.Collections.Concurrent;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// Son bilinen sensör/fused örneklerini thread-safe tutar.
    /// Adapters Publish eder; Core TryGetLast ile tüketir.
    /// </summary>
    public class SensorBus : ISensorBus
    {
        private readonly ConcurrentDictionary<Type, object> _last  = new();
        private readonly ConcurrentDictionary<Type, DateTime> _stamp = new();

        /// <summary>
        /// Adapter’lar için yayın (tip güvenli).
        /// T hem class hem struct olabilir; boxing doğal olarak gerçekleşir.
        /// </summary>
        public void Publish<T>(T data, DateTime stamp)
        {
            _last[typeof(T)] = data!;
            _stamp[typeof(T)] = stamp;
        }

        /// <summary>
        /// Son bilinen veriyi döndürür (varsa true).
        /// T hem referans tip hem değer tipi olabilir.
        /// </summary>
        public bool TryGetLast<T>(out T data)
        {
            if (_last.TryGetValue(typeof(T), out var obj) && obj is T t)
            {
                data = t;
                return true;
            }

            data = default!;
            return false;
        }

        /// <summary>
        /// İlgili tipin son zaman damgasını döndürür (yoksa null).
        /// </summary>
        public DateTime? LastStampOf<T>()
        {
            return _stamp.TryGetValue(typeof(T), out var ts) ? ts : (DateTime?)null;
        }
    }
}
