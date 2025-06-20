using System;

namespace Hydronom.Core.Modules.TaskModule
{
    public class TaskManager
    {
        public TaskManager() { }

        public TaskItem CreateTask(string description)
        {
            var task = new TaskItem(description);
            Console.WriteLine($"🔧 Task created: {description}");
            return task;
        }

        public void AssignTask()
        {
            Console.WriteLine("🔧 Task assigned.");
        }
    }
}
