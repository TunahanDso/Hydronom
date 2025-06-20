namespace Hydronom.Core.Modules.TaskModule
{
    public enum TaskType
    {
        Idle,
        Explore,
        CollectData,
        ReturnToBase
    }

    public class Task
    {
        public TaskType Type { get; set; }
        public string Description { get; set; }

        public Task(TaskType type, string description)
        {
            Type = type;
            Description = description;
        }
    }

    public class TaskManager
    {
         public void Run()
        {
            Console.WriteLine("🔧 Task assigned.");
        }
        private Task? currentTask;

        public TaskManager()
        {
            currentTask = new Task(TaskType.Idle, "System is idle.");
        }

        public void AssignTask(Task task)
        {
            currentTask = task;
            Console.WriteLine($"🔧 Task assigned: {task.Description}");
        }

        public Task? GetCurrentTask()
        {
            return currentTask;
        }

        public void ResetTask()
        {
            currentTask = new Task(TaskType.Idle, "System is idle.");
            Console.WriteLine("🔁 Task reset to Idle.");
        }
    }
}
