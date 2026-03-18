using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// Mevcut IMotorController'ı IActuatorBus ekosistemine bağlayan adaptör.
    /// Not: Apply senkron imza; içeride async'i fire-and-forget çalıştırıyoruz.
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
            // Motor denetimi async olduğu için beklemeyelim; hata motor tarafında loglanır.
            _ = Task.Run(() => _motor.ApplyAsync(cmd, _ct), _ct);
        }
    }
}
