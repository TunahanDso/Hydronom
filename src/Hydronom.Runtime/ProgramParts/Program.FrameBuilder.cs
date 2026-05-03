using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Modules;

partial class Program
{
    /// <summary>
    /// Runtime dÃ¶ngÃ¼sÃ¼nde kullanÄ±lacak frame bilgisini Ã¼retir.
    ///
    /// Ä°lke:
    /// - Runtime kendi obstacle Ã¼retmez.
    /// - Obstacle yalnÄ±zca IFrameSource Ã¼zerinden gelen fresh frame'den alÄ±nÄ±r.
    /// - Position ve heading ise runtime'Ä±n gÃ¼ncel VehicleState deÄŸerinden alÄ±nÄ±r.
    /// - Target, aktif task hedefinden 2D izdÃ¼ÅŸÃ¼m olarak frame'e eklenir.
    /// </summary>
    private static FusedFrame BuildRuntimeFrame(
        IFrameSource frameSource,
        VehicleState state,
        ITaskManager tasks,
        bool devMode,
        bool logVerbose,
        bool externalApplied,
        out bool hasFreshFrame,
        out FusedFrame? latestFrame)
    {
        hasFreshFrame = frameSource.TryGetLatestFrame(out latestFrame) && latestFrame is not null;

        Vec2? target2D = null;
        if (tasks.CurrentTask?.Target is Vec3 tg3Target)
            target2D = new Vec2(tg3Target.X, tg3Target.Y);

        if (hasFreshFrame && latestFrame is not null)
        {
            var obstaclesFromPython = latestFrame.Obstacles is not null
                ? new List<Obstacle>(latestFrame.Obstacles)
                : new List<Obstacle>();

            var frameToUse = new FusedFrame(
                TimestampUtc: latestFrame.TimestampUtc,
                Position: new Vec2(state.Position.X, state.Position.Y),
                HeadingDeg: state.Orientation.YawDeg,
                Obstacles: obstaclesFromPython,
                Target: target2D
            );

            if (devMode && logVerbose)
            {
                string targetText = tasks.CurrentTask?.Target is Vec3 tg3Log
                    ? $"({tg3Log.X:F1},{tg3Log.Y:F1},{tg3Log.Z:F1})"
                    : "none";

                Console.WriteLine(
                    $"[SRC] fresh frame: obs={obstaclesFromPython.Count}, " +
                    $"target={targetText}, extApplied={externalApplied}"
                );
            }

            return frameToUse;
        }

        if (devMode && logVerbose)
            Console.WriteLine("[SRC] no fresh frame: obstacle source empty (runtime fallback disabled)");

        return new FusedFrame(
            TimestampUtc: DateTime.UtcNow,
            Position: new Vec2(state.Position.X, state.Position.Y),
            HeadingDeg: state.Orientation.YawDeg,
            Obstacles: new List<Obstacle>(),
            Target: target2D
        );
    }

    /// <summary>
    /// Aktif task'a gÃ¶re hedef telemetri bilgisini Ã¼retir.
    /// Log ve heartbeat tarafÄ±nda kullanÄ±lÄ±r.
    /// </summary>
    private static TargetTelemetrySnapshot BuildTargetTelemetrySnapshot(
        ITaskManager tasks,
        VehicleState state)
    {
        double distToTarget = double.NaN;
        double deltaHeadDeg = double.NaN;

        if (tasks.CurrentTask?.Target is Vec3 tg3Telemetry)
        {
            var dxT = tg3Telemetry.X - state.Position.X;
            var dyT = tg3Telemetry.Y - state.Position.Y;
            var dzT = tg3Telemetry.Z - state.Position.Z;

            distToTarget = Math.Sqrt(dxT * dxT + dyT * dyT + dzT * dzT);

            var targetHeadingDeg = Math.Atan2(dyT, dxT) * 180.0 / Math.PI;
            deltaHeadDeg = NormalizeAngleDeg(targetHeadingDeg - state.Orientation.YawDeg);
        }

        var taskInfoInline = DescribeTaskInline(tasks.CurrentTask);

        AdvancedTaskReport taskReport = AdvancedTaskReport.Empty;
        if (tasks is AdvancedTaskManager advancedTaskManager)
            taskReport = advancedTaskManager.LastReport;

        return new TargetTelemetrySnapshot(
            DistanceToTargetM: distToTarget,
            DeltaHeadingDeg: deltaHeadDeg,
            TaskInfoInline: taskInfoInline,
            TaskReport: taskReport
        );
    }

    /// <summary>
    /// Task deÄŸiÅŸimini algÄ±lar ve sadece deÄŸiÅŸtiÄŸinde detaylÄ± task logu basar.
    /// </summary>
    private static void LogTaskChangeIfNeeded(
        ITaskManager tasks,
        ref LoopRuntimeState loopState)
    {
        var taskSignature = BuildTaskSignature(tasks.CurrentTask);

        if (!string.Equals(loopState.LastTaskSignature, taskSignature, StringComparison.Ordinal))
        {
            LogTaskState(tasks.CurrentTask);
            loopState.LastTaskSignature = taskSignature;
        }
    }
}
