using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Abstractions
{
    /// <summary>
    /// Görev hedefini ve dünya bağlamını kullanarak genel rota üretir.
    ///
    /// Global planner:
    /// - görev sırasını,
    /// - ana geçiş hattını,
    /// - hedefe giden kaba yolu
    /// belirler.
    /// </summary>
    public interface IGlobalPlanner
    {
        PlannedPath PlanGlobal(PlanningContext context);
    }
}