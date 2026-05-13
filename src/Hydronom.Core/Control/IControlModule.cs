using Hydronom.Core.Domain;

namespace Hydronom.Core.Control
{
    /// <summary>
    /// Platform bağımsız yüksek frekanslı controller.
    ///
    /// Decision:
    /// - ne yapılacağını söyler
    ///
    /// Control:
    /// - bunun fiziksel olarak nasıl uygulanacağını çözer
    /// </summary>
    public interface IControlModule
    {
        ControlOutput Update(
            ControlIntent intent,
            VehicleState state,
            double dt);
    }
}