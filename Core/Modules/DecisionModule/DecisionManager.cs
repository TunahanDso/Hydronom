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
                "AreaScan" => "AutonomousControl",
                "Docking" => "ManualControl",
                "ObstacleAvoidance" => "AutonomousEmergency",
                _ => "Idle"
            };

            Console.WriteLine($"🤖 Decision: Switching to {mode} for task '{task.Type}'");
        }
    }
}

