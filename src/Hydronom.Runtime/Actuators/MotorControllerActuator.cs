using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// Mevcut IMotorController'Ä± IActuatorBus ekosistemine baÄŸlayan adaptÃ¶r.
    /// Not: Apply senkron imza; iÃ§eride async'i fire-and-forget Ã§alÄ±ÅŸtÄ±rÄ±yoruz.
    /// </summary>
    public class MotorControllerActuator : IActuator
    {
        private readonly IMotorController _motor;
        private readonly CancellationToken _ct;

        public MotorControllerActuator(IMotorController motor, CancellationToken ct = default)
        {
            _motor = motor;
            _ct = ct;
        }

        public void Apply(DecisionCommand cmd)
        {
            // Motor denetimi async olduÄŸu iÃ§in beklemeyelim; hata motor tarafÄ±nda loglanÄ±r.
            _ = Task.Run(() => _motor.ApplyAsync(cmd, _ct), _ct);
        }
    }
}

