using System;
using System.Reflection;
using Hydronom.Core.Domain;

partial class Program
{
    /// <summary>
    /// Görev değişimlerinde görevin okunabilir durumunu konsola basar.
    /// Reflection kullanmasının sebebi farklı task tiplerini ortak şekilde loglayabilmektir.
    /// </summary>
    private static void LogTaskState(object? task)
    {
        Console.WriteLine("[TASK] --------------------------------------------------");

        if (task is null)
        {
            Console.WriteLine("[TASK] none");
            Console.WriteLine("[TASK] --------------------------------------------------");
            return;
        }

        Console.WriteLine($"[TASK] type   : {task.GetType().Name}");

        var title = TryReadStringProperty(task, "Title", "Name", "TaskName", "Label");
        if (!string.IsNullOrWhiteSpace(title))
            Console.WriteLine($"[TASK] title  : {title}");

        var mode = TryReadStringProperty(task, "Mode", "State", "Kind", "Type");
        if (!string.IsNullOrWhiteSpace(mode))
            Console.WriteLine($"[TASK] mode   : {mode}");

        var target = TryReadTargetString(task);
        if (!string.IsNullOrWhiteSpace(target))
            Console.WriteLine($"[TASK] target : {target}");

        var summary = task.ToString();
        if (!string.IsNullOrWhiteSpace(summary) && summary != task.GetType().ToString())
            Console.WriteLine($"[TASK] desc   : {summary}");

        Console.WriteLine("[TASK] --------------------------------------------------");
    }

    /// <summary>
    /// Görevi tek satırlık log formatında açıklar.
    /// </summary>
    private static string DescribeTaskInline(object? task)
    {
        if (task is null)
            return "none";

        var title = TryReadStringProperty(task, "Title", "Name", "TaskName", "Label");
        var target = TryReadTargetString(task);

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(target))
            return $"{title} {target}";

        if (!string.IsNullOrWhiteSpace(title))
            return title;

        if (!string.IsNullOrWhiteSpace(target))
            return target;

        return task.GetType().Name;
    }

    /// <summary>
    /// Görev değişimi olup olmadığını anlamak için kararlı imza üretir.
    /// </summary>
    private static string BuildTaskSignature(object? task)
    {
        if (task is null)
            return "none";

        return string.Join("|",
            task.GetType().FullName ?? task.GetType().Name,
            TryReadStringProperty(task, "Title", "Name", "TaskName", "Label") ?? string.Empty,
            TryReadStringProperty(task, "Mode", "State", "Kind", "Type") ?? string.Empty,
            TryReadTargetString(task) ?? string.Empty,
            task.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Farklı task tiplerinden ortak isim alanlarını okumaya çalışır.
    /// </summary>
    private static string? TryReadStringProperty(object obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var prop = obj
                    .GetType()
                    .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

                if (prop is null)
                    continue;

                var value = prop.GetValue(obj);
                var text = value?.ToString()?.Trim();

                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            catch
            {
                // Görev loglama runtime davranışını bozmamalı.
            }
        }

        return null;
    }

    /// <summary>
    /// Task.Target property değerini okunabilir metne çevirir.
    /// </summary>
    private static string? TryReadTargetString(object obj)
    {
        try
        {
            var prop = obj
                .GetType()
                .GetProperty("Target", BindingFlags.Instance | BindingFlags.Public);

            if (prop is null)
                return null;

            var value = prop.GetValue(obj);
            if (value is null)
                return null;

            if (value is Vec3 v3)
                return $"({v3.X:F1},{v3.Y:F1},{v3.Z:F1})";

            if (value is Vec2 v2)
                return $"({v2.X:F1},{v2.Y:F1})";

            return value.ToString();
        }
        catch
        {
            return null;
        }
    }
}