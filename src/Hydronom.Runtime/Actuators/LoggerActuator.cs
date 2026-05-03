using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>Komutu sadece loglar (debug/test iÃ§in).</summary>
    public class LoggerActuator : IActuator
    {
        public void Apply(DecisionCommand cmd)
        {
            System.Console.WriteLine($"[LoggerActuator] throttle={cmd.Throttle01:F2} rudder={cmd.RudderNeg1To1:F2}");
        }
    }
}

