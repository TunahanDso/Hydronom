using System;
using System.Collections.Generic;

namespace Hydronom.Core.Modules.TaskModule
{
    public class Task
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public DateTime Timestamp { get; set; }

        public Task(int id, string type)
        {
            Id = id;
            Type = type;
            Timestamp = DateTime.Now;
        }
    }

    public class TaskManager
    {
        private readonly List<Task> _tasks = new();

        public TaskManager() { }

        public Task CreateTask(int id, string type)
        {
            var task = new Task(id, type);
            _tasks.Add(task);
            Console.WriteLine($"🔧 Created task {task.Id} of type {task.Type} at {task.Timestamp}.");
            return task;
        }

        public void ListTasks()
        {
            Console.WriteLine("📋 Task list:");
            foreach (var task in _tasks)
            {
                Console.WriteLine($"- {task.Id}: {task.Type} at {task.Timestamp}");
            }
        }
    }
}
