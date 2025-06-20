using System;
using Hydronom.Core.Modules.TaskModule;

namespace Hydronom.Core.Modules.DecisionModule
{
    public class DecisionManager
    {
        public DecisionManager()
        {
            // Decision manager initialized
        }

        public void Evaluate(Task task)
        {
            string mode = task.Type switch
            {
                TaskType.AreaScan => "AutonomousControl",
                TaskType.Docking => "ManualControl",
                _ => "Idle"
            };

            Console.WriteLine($"🤖 Decision: Switching to {mode} for task '{task.Type}'");
        }
    }
}

