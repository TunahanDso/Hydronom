using System;
using System.Collections.Concurrent;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// Son bilinen sensÃ¶r/fused Ã¶rneklerini thread-safe tutar.
    /// Adapters Publish eder; Core TryGetLast ile tÃ¼ketir.
    /// </summary>
    public class SensorBus : ISensorBus
    {
        private readonly ConcurrentDictionary<Type, object> _last  = new();
        private readonly ConcurrentDictionary<Type, DateTime> _stamp = new();

        /// <summary>
        /// Adapterâ€™lar iÃ§in yayÄ±n (tip gÃ¼venli).
        /// T hem class hem struct olabilir; boxing doÄŸal olarak gerÃ§ekleÅŸir.
        /// </summary>
        public void Publish<T>(T data, DateTime stamp)
        {
            _last[typeof(T)] = data!;
            _stamp[typeof(T)] = stamp;
        }

        /// <summary>
        /// Son bilinen veriyi dÃ¶ndÃ¼rÃ¼r (varsa true).
        /// T hem referans tip hem deÄŸer tipi olabilir.
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
        /// Ä°lgili tipin son zaman damgasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r (yoksa null).
        /// </summary>
        public DateTime? LastStampOf<T>()
        {
            return _stamp.TryGetValue(typeof(T), out var ts) ? ts : (DateTime?)null;
        }
    }
}

