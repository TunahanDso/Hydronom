using System;
using Hydronom.Core.Modules.TaskModule;

namespace Hydronom.Core.Modules.DecisionModule
{
    public class DecisionManager
    {
        public DecisionManager() { }

        public void Evaluate(Task task)
        {
            string mode = task.Type switch
            {
                TaskType.AreaScan => "AutonomousControl",
                TaskType.Docking => "ManualControl",
                _ => "UnknownControl"
            };

            Console.WriteLine($"🤖 Decision evaluated. Activating {mode}.");
        }
    }
}
