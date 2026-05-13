using Hydronom.Core.Domain;

namespace Hydronom.Core.Control
{
    /// <summary>
    /// Controller katmanının ürettiği fiziksel çıktı.
    ///
    /// Bu artık:
    /// - wrench
    /// - force/moment
    /// - actuator-ready command
    ///
    /// katmanıdır.
    /// </summary>
    public sealed record ControlOutput(
        DecisionCommand Command,
        string Mode,
        string Reason
    )
    {
        public static ControlOutput Zero { get; } =
            new(
                DecisionCommand.Zero,
                "IDLE",
                "ZERO_OUTPUT");
    }
}