using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Abstractions
{
    /// <summary>
    /// Global planı yakın çevre, engel, corridor ve risk durumuna göre düzeltir.
    ///
    /// Local planner:
    /// - engel çevresinden geçiş,
    /// - güvenli corridor,
    /// - yavaşlama / replan ihtiyacı
    /// üretir.
    /// </summary>
    public interface ILocalPlanner
    {
        PlannedPath RefineLocal(
            PlanningContext context,
            PlannedPath globalPath);
    }
}