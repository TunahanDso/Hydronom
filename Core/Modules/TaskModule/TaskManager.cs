using System;
using System.Collections.Generic;

namespace Hydronom.Core.Modules.TaskModule
{

    public class TaskManager
    {
        private List<Task> taskList;

        public TaskManager()
        {
            taskList = new List<Task>();
        }

        public Task CreateTask(TaskType type)
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
