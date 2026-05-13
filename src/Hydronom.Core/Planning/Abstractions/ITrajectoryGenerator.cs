using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Abstractions
{
    /// <summary>
    /// Soyut path'i controller'ın takip edebileceği hareket referanslarına çevirir.
    /// </summary>
    public interface ITrajectoryGenerator
    {
        TrajectoryPlan GenerateTrajectory(
            PlanningContext context,
            PlannedPath path);
    }
}