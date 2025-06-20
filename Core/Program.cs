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

            // Görev oluştur ve değerlendir
            var task = taskManager.CreateTask("Navigate to waypoint");
            var selectedMode = decisionManager.Evaluate(task);
            Console.WriteLine($"Selected Control Mode: {selectedMode}");
            controlManager.ApplyControlMode(selectedMode);

            // Diğer modülleri çalıştır
            analysisManager.Analyze();
            feedbackManager.Log();

            Console.WriteLine("✅ All modules initialized successfully.");
        }
    }
}
