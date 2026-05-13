using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Modules;
using Hydronom.Runtime.World.Runtime;

partial class Program
{
    private const double RuntimeWorldObstacleMaxDistanceMeters = 60.0;
    private const double RuntimeWorldObstacleSafetyMarginMeters = 0.35;
    private const double RuntimeWorldObstacleMinRadiusMeters = 0.25;
    private const double RuntimeWorldObstacleMergeDistanceMeters = 0.35;

    /// <summary>
    /// Runtime döngüsünde kullanılacak frame bilgisini üretir.
    ///
    /// İlke:
    /// - Öncelik varsa IFrameSource üzerinden gelen fresh obstacle bilgisindedir.
    /// - C# Primary / scenario simülasyonunda RuntimeWorldModel içindeki aktif engeller de frame'e eklenir.
    /// - Position ve heading runtime'ın güncel VehicleState değerinden alınır.
    /// - Target, aktif task hedefinden 2D izdüşüm olarak frame'e eklenir.
    ///
    /// Not:
    /// - Bu köprü özellikle Parkur-2 için gereklidir.
    /// - Sim LiDAR/world tarafı OK görünse bile ana analiz döngüsü FusedFrame okuduğu için,
    ///   world engelleri burada FusedFrame.Obstacles içine aktarılır.
    /// </summary>
    private static FusedFrame BuildRuntimeFrame(
        IFrameSource frameSource,
        VehicleState state,
        ITaskManager tasks,
        RuntimeWorldModel? runtimeWorldModel,
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

        var obstacles = new List<Obstacle>();

        if (hasFreshFrame && latestFrame is not null && latestFrame.Obstacles is not null)
        {
            obstacles.AddRange(latestFrame.Obstacles);
        }

        var runtimeWorldObstacleCount = AddRuntimeWorldObstacles(
            runtimeWorldModel,
            state,
            obstacles);

        obstacles = DeduplicateRuntimeFrameObstacles(
            obstacles,
            RuntimeWorldObstacleMergeDistanceMeters);

        var timestampUtc = hasFreshFrame && latestFrame is not null
            ? latestFrame.TimestampUtc
            : DateTime.UtcNow;

        var frameToUse = new FusedFrame(
            TimestampUtc: timestampUtc,
            Position: new Vec2(state.Position.X, state.Position.Y),
            HeadingDeg: state.Orientation.YawDeg,
            Obstacles: obstacles,
            Target: target2D
        );

        if (devMode && logVerbose)
        {
            string targetText = tasks.CurrentTask?.Target is Vec3 tg3Log
                ? $"({tg3Log.X:F1},{tg3Log.Y:F1},{tg3Log.Z:F1})"
                : "none";

            Console.WriteLine(
                $"[SRC] frame: fresh={hasFreshFrame}, obs={obstacles.Count}, " +
                $"worldObs={runtimeWorldObstacleCount}, target={targetText}, extApplied={externalApplied}"
            );
        }

        return frameToUse;
    }

    /// <summary>
    /// RuntimeWorldModel içindeki aktif engelleri analiz frame'ine aktarır.
    /// Bu metot fiziksel collision çözmez; sadece analiz/karar modülünün engeli görmesini sağlar.
    /// </summary>
    private static int AddRuntimeWorldObstacles(
        RuntimeWorldModel? runtimeWorldModel,
        VehicleState state,
        List<Obstacle> output)
    {
        if (runtimeWorldModel is null)
            return 0;

        var activeObjects = runtimeWorldModel.ActiveObjects();

        if (activeObjects.Count == 0)
            return 0;

        var added = 0;
        var vehicleX = state.Position.X;
        var vehicleY = state.Position.Y;

        foreach (var obj in activeObjects)
        {
            if (!obj.IsBlocking && !obj.IsObstacleLike)
                continue;

            var dx = obj.X - vehicleX;
            var dy = obj.Y - vehicleY;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (!double.IsFinite(distance) || distance > RuntimeWorldObstacleMaxDistanceMeters)
                continue;

            var radius = ResolveRuntimeWorldObstacleRadius(obj.Radius, obj.Width, obj.Height);

            output.Add(new Obstacle(
                Position: new Vec2(obj.X, obj.Y),
                RadiusM: radius));

            added++;
        }

        return added;
    }

    /// <summary>
    /// World object boyutlarından analiz için güvenli obstacle yarıçapı üretir.
    /// Duba gibi küçük nesnelerde safety margin eklenir ki araç dubanın içinden geçmesin.
    /// </summary>
    private static double ResolveRuntimeWorldObstacleRadius(
        double radius,
        double width,
        double height)
    {
        var sizeBasedRadius = Math.Max(
            Math.Max(radius, width > 0.0 ? width * 0.5 : 0.0),
            height > 0.0 ? height * 0.5 : 0.0);

        if (!double.IsFinite(sizeBasedRadius) || sizeBasedRadius <= 0.0)
            sizeBasedRadius = RuntimeWorldObstacleMinRadiusMeters;

        sizeBasedRadius = Math.Max(sizeBasedRadius, RuntimeWorldObstacleMinRadiusMeters);

        return sizeBasedRadius + RuntimeWorldObstacleSafetyMarginMeters;
    }

    /// <summary>
    /// Aynı engelin hem Python/TCP frame'den hem de RuntimeWorldModel'dan gelmesi durumunda çift sayımı azaltır.
    /// </summary>
    private static List<Obstacle> DeduplicateRuntimeFrameObstacles(
        List<Obstacle> src,
        double mergeDistance)
    {
        if (src.Count <= 1)
            return src;

        var result = new List<Obstacle>();

        foreach (var obstacle in src)
        {
            var merged = false;

            for (var i = 0; i < result.Count; i++)
            {
                var existing = result[i];

                var dx = obstacle.Position.X - existing.Position.X;
                var dy = obstacle.Position.Y - existing.Position.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > mergeDistance)
                    continue;

                var mergedX = (obstacle.Position.X + existing.Position.X) * 0.5;
                var mergedY = (obstacle.Position.Y + existing.Position.Y) * 0.5;
                var mergedRadius = Math.Max(obstacle.RadiusM, existing.RadiusM);

                result[i] = new Obstacle(
                    Position: new Vec2(mergedX, mergedY),
                    RadiusM: mergedRadius);

                merged = true;
                break;
            }

            if (!merged)
                result.Add(obstacle);
        }

        return result;
    }

    /// <summary>
    /// Aktif task'a göre hedef telemetri bilgisini üretir.
    /// Log ve heartbeat tarafında kullanılır.
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
    /// Task değişimini algılar ve sadece değiştiğinde detaylı task logu basar.
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