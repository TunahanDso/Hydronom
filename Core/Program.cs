
using System;
using Hydronom.Core.Modules.TaskModule;
using Hydronom.Core.Modules.AnalysisModule;
using Hydronom.Core.Modules.DecisionModule;
using Hydronom.Core.Modules.ControlModule;
using Hydronom.Core.Modules.FeedbackModule;

namespace Hydronom.Core
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🔵 Hydronom Autonomous System Starting...");

            var taskManager = new TaskManager();
            taskManager.Run();

            var analysisManager = new AnalysisManager();
            analysisManager.Run();

            var decisionManager = new DecisionManager();
            decisionManager.Run();

            var controlManager = new ControlManager();
            controlManager.Run();

            var feedbackManager = new FeedbackManager();
            feedbackManager.Run();

            Console.WriteLine("✅ All modules initialized successfully.");
        }
    }
}
