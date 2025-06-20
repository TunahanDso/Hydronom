using System;

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
        public string Id { get; }
        public TaskType Type { get; }
        public DateTime CreatedAt { get; }

        public Task(TaskType type)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            CreatedAt = DateTime.Now;
        }

        public override string ToString()
        {
            return $"\ud83d\udccc Task Created: {Type} (ID: {Id}, Time: {CreatedAt})";
        }
    }
}
