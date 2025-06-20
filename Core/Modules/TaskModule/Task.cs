namespace Hydronom.Core.Modules.TaskModule
{
    public enum TaskType
    {
        AreaScan,
        Docking,
        Unknown
    }

    public class Task
    {
        public TaskType Type { get; }

        public Task(TaskType type)
        {
            Type = type;
        }
    }
}
