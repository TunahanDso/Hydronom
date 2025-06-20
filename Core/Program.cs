
using System;
using Hydronom.Core.Modules.TaskModule;
using Hydronom.Core.Modules.DecisionModule;
using Hydronom.Core.Modules.ControlModule;
using Hydronom.Core.Modules.AnalysisModule;
using Hydronom.Core.Modules.FeedbackModule;

namespace Hydronom.Core
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🔵 Hydronom Autonomous System Starting...");

            // Modülleri başlat
            var taskManager = new TaskManager();
            var decisionManager = new DecisionManager();
            var controlManager = new ControlManager();
            var analysisManager = new AnalysisManager();
            var feedbackManager = new FeedbackManager();

            // Basit test çalıştırmaları (ileride görev döngüsüne dönüştürülecek)
            taskManager.AssignTask();
            analysisManager.Analyze();
            decisionManager.Evaluate();
            controlManager.MoveForward();
            feedbackManager.Log();

            Console.WriteLine("✅ All modules initialized successfully.");
        }
    }
}
