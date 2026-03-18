using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Buses
{
    /// <summary>
    /// Birden fazla IActuator’ü aynı anda çağıran çoklayıcı.
    /// </summary>
    public class ActuatorBus : IActuatorBus
    {
        private readonly List<IActuator> _actuators = new();
        public DecisionCommand? LastApplied { get; private set; }
        public event Action<DecisionCommand>? Applied;

        public ActuatorBus(IEnumerable<IActuator>? initial = null)
        {
            if (initial != null) _actuators.AddRange(initial);
        }

        /// <summary>Dinamik olarak actuator eklemek için.</summary>
        public void Add(IActuator actuator) => _actuators.Add(actuator);

        public void Apply(DecisionCommand cmd)
        {
            LastApplied = cmd;

            foreach (var a in _actuators)
            {
                try { a.Apply(cmd); }
                catch (Exception ex)
                {
                    // Tek bir actuator fail ederse diğerlerini koru
                    Console.WriteLine($"[ActuatorBus] Apply error: {ex.Message}");
                }
            }

            Applied?.Invoke(cmd);
        }
    }
}
