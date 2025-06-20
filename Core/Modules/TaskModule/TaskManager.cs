using System;
using System.Collections.Generic;

namespace Hydronom.Core.Modules.TaskModule
{
    public class Task
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }

        public Task(string type)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            CreatedAt = DateTime.Now;
        }

        public override string ToString()
        {
            return $"📌 Task Created: {Type} (ID: {Id}, Time: {CreatedAt})";
        }
    }

    public class TaskManager
    {
        private List<Task> taskList;

        public TaskManager()
        {
            taskList = new List<Task>();
        }

        public Task CreateTask(string type)
        {
            var newTask = new Task(type);
            taskList.Add(newTask);
            Console.WriteLine(newTask);
            return newTask;
        }

        public void ListTasks()
        {
            Console.WriteLine("📋 Task List:");
            foreach (var task in taskList)
                Console.WriteLine($"- {task.Type} (ID: {task.Id})");
        }
    }
}
