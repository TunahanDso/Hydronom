using System;
using Hydronom.Core.Modules.TaskModule;
using Hydronom.Core.Modules.ControlModule;

namespace Hydronom.Core.Modules.DecisionModule
{
    public class DecisionManager
    {
        public DecisionManager() { }

        public ControlMode Evaluate(TaskItem task)
        {
            Console.WriteLine("🤖 Evaluating task...");
            if (task.Description.ToLower().Contains("manual"))
            {
                Console.WriteLine("🤖 Manual control required.");
                return ControlMode.Manual;
            }
            Console.WriteLine("🤖 Autonomous control selected.");
            return ControlMode.Autonomous;
        }

        public void Evaluate()
        {
            Console.WriteLine("🤖 Decision evaluated.");
        }
    }
}
