using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules;

public class MockMotorController : IMotorController
{
    public async Task ApplyAsync(DecisionCommand command, CancellationToken ct = default)
    {
        // Gerçek donanım yok; sadece gecikme simüle edelim
        await Task.Delay(10, ct);
    }
}

