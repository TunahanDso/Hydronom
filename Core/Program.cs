using System;
using Hydronom.Core.Modules.TaskModule;
using Hydronom.Core.Modules.DecisionModule;

namespace Hydronom.Core
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🔵 Hydronom Autonomous System Starting...");

            var taskManager = new TaskManager();
            var task = taskManager.CreateTask(TaskType.Docking);

            var decisionManager = new DecisionManager();
            decisionManager.Evaluate(task);

            Console.WriteLine("✅ All modules initialized successfully.");
        }
    }
}
