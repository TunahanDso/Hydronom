using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Actuators;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Tuning;

partial class Program
{
    /// <summary>
    /// Ana loop logunun basÄ±lÄ±p basÄ±lmayacaÄŸÄ±nÄ± belirler.
    /// Verbose modda her dÃ¶ngÃ¼de, compact modda belirli aralÄ±klarla log basÄ±lÄ±r.
    /// </summary>
    private static bool ShouldEmitLoopLog(RuntimeOptions runtime, long tickIndex)
    {
        return runtime.LogVerbose || tickIndex % runtime.LoopLogEvery == 0;
    }

    /// <summary>
    /// LOOP / CTL loglarÄ±nÄ± basar.
    /// </summary>
    private static void EmitLoopDiagnostics(
        RuntimeOptions runtime,
        RuntimeDiagnosticsSnapshot diagnostics,
        VehicleState state,
        Insights insights,
        DecisionCommand desiredCommand,
        DecisionCommand limitedCommand)
    {
        if (runtime.LogVerbose)
        {
            EmitVerboseLoopDiagnostics(
                diagnostics,
                state,
                insights,
                desiredCommand,
                limitedCommand
            );

            return;
        }

        EmitCompactLoopDiagnostics(
            diagnostics,
            state,
            insights,
            limitedCommand
        );
    }

    /// <summary>
    /// Compact loop logu.
    /// Tek satÄ±rda operasyonel durum verir.
    /// </summary>
    private static void EmitCompactLoopDiagnostics(
        RuntimeDiagnosticsSnapshot diagnostics,
        VehicleState state,
        Insights insights,
        DecisionCommand limitedCommand)
    {
        var target = diagnostics.TargetTelemetry;
        var decision = diagnostics.DecisionReport;
        var analysis = diagnostics.AnalysisReport;
        var limit = diagnostics.LimitReport;

        Console.WriteLine(
            $"[LOOP] mode={diagnostics.ControlMode} " +
            $"decision={decision.Mode} reason={decision.Reason} " +
            $"task={target.TaskInfoInline} " +
            $"taskPhase={target.TaskReport.Phase} " +
            $"taskProg={target.TaskReport.ProgressPercent:F1}% " +
            $"wp={FormatWaypoint(target.TaskReport)} " +
            $"accept={target.TaskReport.EffectiveArrivalThresholdM:F2}m " +
            $"queue={target.TaskReport.QueuedTaskCount} " +
            $"pos=({state.Position.X:F2},{state.Position.Y:F2}) " +
            $"yaw={state.Orientation.YawDeg:F1}Â° " +
            $"yawRate={state.AngularVelocity.Z:F1}Â°/s " +
            $"dist={(double.IsNaN(target.DistanceToTargetM) ? -1 : target.DistanceToTargetM):F1}m " +
            $"dHead={(double.IsNaN(target.DeltaHeadingDeg) ? 0 : target.DeltaHeadingDeg):F1}Â° " +
            $"decDHead={decision.HeadingErrorDeg:F1}Â° " +
            $"vFwd={decision.ForwardSpeedMps:F2}m/s " +
            $"Fx={limitedCommand.Fx:F2} Fy={limitedCommand.Fy:F2} Fz={limitedCommand.Fz:F2} Tz={limitedCommand.Tz:F2} " +
            $"obsAhead={(insights.HasObstacleAhead ? "True" : "False")} " +
            $"risk={analysis.FrontRiskScore:F2} avoid={analysis.SuggestedSide} " +
            $"thr={decision.ThrottleNorm:F2} rud={decision.RudderNorm:F2} " +
            $"lim={diagnostics.LimitFlags} limited={limit.WasLimited}"
        );
    }

    /// <summary>
    /// Verbose loop logu.
    /// AyrÄ±ntÄ±lÄ± state + control bilgisi verir.
    /// </summary>
    private static void EmitVerboseLoopDiagnostics(
        RuntimeDiagnosticsSnapshot diagnostics,
        VehicleState state,
        Insights insights,
        DecisionCommand desiredCommand,
        DecisionCommand limitedCommand)
    {
        var target = diagnostics.TargetTelemetry;
        var decision = diagnostics.DecisionReport;
        var analysis = diagnostics.AnalysisReport;
        var limit = diagnostics.LimitReport;

        Console.WriteLine(
            $"[{DateTime.UtcNow:O}] " +
            $"mode={diagnostics.ControlMode} " +
            $"decision={decision.Mode} reason={decision.Reason} " +
            $"task={target.TaskInfoInline} " +
            $"taskPhase={target.TaskReport.Phase} taskReason={target.TaskReport.Reason} " +
            $"taskProg={target.TaskReport.ProgressPercent:F1}% wp={FormatWaypoint(target.TaskReport)} " +
            $"accept={target.TaskReport.EffectiveArrivalThresholdM:F2}m queue={target.TaskReport.QueuedTaskCount} " +
            $"pos=({state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}) " +
            $"rpy=({state.Orientation.RollDeg:F1},{state.Orientation.PitchDeg:F1},{state.Orientation.YawDeg:F1}) " +
            $"angVel=({state.AngularVelocity.X:F1},{state.AngularVelocity.Y:F1},{state.AngularVelocity.Z:F1}) " +
            $"obsAhead={(insights.HasObstacleAhead ? "True" : "False")} " +
            $"risk={analysis.FrontRiskScore:F2} avoid={analysis.SuggestedSide} " +
            $"thr={decision.ThrottleNorm:F2} rud={decision.RudderNorm:F2} " +
            $"cmd(Fx={limitedCommand.Fx:F2}, Fy={limitedCommand.Fy:F2}, Fz={limitedCommand.Fz:F2}, " +
            $"Tx={limitedCommand.Tx:F2}, Ty={limitedCommand.Ty:F2}, Tz={limitedCommand.Tz:F2})"
        );

        Console.WriteLine(
            $"[CTL] mode={diagnostics.ControlMode} " +
            $"decision={decision.Mode} reason={decision.Reason} " +
            $"task={target.TaskInfoInline} " +
            $"dist={(double.IsNaN(target.DistanceToTargetM) ? -1 : target.DistanceToTargetM):F1}m " +
            $"dHead={(double.IsNaN(target.DeltaHeadingDeg) ? 0 : target.DeltaHeadingDeg):F1}Â° " +
            $"decDHead={decision.HeadingErrorDeg:F1}Â° " +
            $"vFwd={decision.ForwardSpeedMps:F2}m/s " +
            $"pre(Fx={desiredCommand.Fx:F2},Fy={desiredCommand.Fy:F2},Fz={desiredCommand.Fz:F2},Tz={desiredCommand.Tz:F2}) -> " +
            $"post(Fx={limitedCommand.Fx:F2},Fy={limitedCommand.Fy:F2},Fz={limitedCommand.Fz:F2},Tz={limitedCommand.Tz:F2}) " +
            $"lim={diagnostics.LimitFlags} limited={limit.WasLimited}"
        );
    }

    /// <summary>
    /// Verbose modda state ve aÃ§Ä±klanabilir modÃ¼l raporlarÄ±nÄ± basar.
    /// </summary>
    private static void EmitVerboseModuleReports(
        RuntimeOptions runtime,
        VehicleState state,
        RuntimeDiagnosticsSnapshot diagnostics)
    {
        if (!runtime.LogVerbose)
            return;

        Console.WriteLine(
            $"[STATE] pos=({state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}) " +
            $"rpy=({state.Orientation.RollDeg:F1},{state.Orientation.PitchDeg:F1},{state.Orientation.YawDeg:F1}) " +
            $"vel=({state.LinearVelocity.X:F2},{state.LinearVelocity.Y:F2},{state.LinearVelocity.Z:F2}) " +
            $"angVel=({state.AngularVelocity.X:F1},{state.AngularVelocity.Y:F1},{state.AngularVelocity.Z:F1})"
        );

        Console.WriteLine(
            $"[TASK] {diagnostics.TargetTelemetry.TaskReport} | " +
            $"[ANALYSIS] {diagnostics.AnalysisReport} | " +
            $"[DECISION] {diagnostics.DecisionReport} | " +
            $"[LIMIT] {diagnostics.LimitReport} | " +
            $"[ALLOC] {diagnostics.AllocationReport}"
        );
    }

    /// <summary>
    /// Heartbeat logunu basar.
    /// </summary>
    private static void EmitHeartbeat(
        RuntimeOptions runtime,
        long tickIndex,
        int tickMs,
        double dtMeasured,
        VehicleState state,
        CommandServer cmdSrv,
        ActuatorManager actuatorManager,
        RuntimeDiagnosticsSnapshot diagnostics,
        double frameAgeMs)
    {
        if (tickIndex % runtime.HeartbeatEvery != 0)
            return;

        var target = diagnostics.TargetTelemetry;
        var task = target.TaskReport;
        var analysis = diagnostics.AnalysisReport;
        var decision = diagnostics.DecisionReport;
        var allocation = diagnostics.AllocationReport;

        Console.WriteLine(
            $"[HEARTBEAT] tick={tickIndex} " +
            $"mode={diagnostics.ControlMode} " +
            $"decision={decision.Mode} reason={decision.Reason} " +
            $"task={target.TaskInfoInline} " +
            $"taskPhase={task.Phase} taskProg={task.ProgressPercent:F1}% " +
            $"wp={FormatWaypoint(task)} accept={task.EffectiveArrivalThresholdM:F2}m " +
            $"queue={task.QueuedTaskCount} taskReason={task.Reason} " +
            $"dtTarget={tickMs}ms dtMeasured={dtMeasured * 1000.0:F0}ms " +
            $"pos=({state.Position.X:F2},{state.Position.Y:F2}) " +
            $"yaw={state.Orientation.YawDeg:F1}Â° " +
            $"yawRate={state.AngularVelocity.Z:F1}Â°/s " +
            $"frameAgeMs={(double.IsNaN(frameAgeMs) ? -1 : frameAgeMs):F0} " +
            $"armed={cmdSrv.IsArmed} estop={cmdSrv.IsEmergencyStop} manual={cmdSrv.IsManualMode} " +
            $"serial={(actuatorManager.IsSerialOpen ? "open" : "closed")} " +
            $"risk={analysis.FrontRiskScore:F2} avoid={analysis.SuggestedSide} " +
            $"decDHead={decision.HeadingErrorDeg:F1}Â° vFwd={decision.ForwardSpeedMps:F2}m/s " +
            $"thr={decision.ThrottleNorm:F2} rud={decision.RudderNorm:F2} " +
            $"lim={diagnostics.LimitFlags} " +
            $"alloc={allocation.Reason} allocErr={allocation.NormalizedError:F2}"
        );
    }

    /// <summary>
    /// Frame age bilgisini hesaplar.
    /// </summary>
    private static double ComputeFrameAgeMs(IFrameSource frameSource)
    {
        if (frameSource.TryGetLatestFrame(out var lastFrame) && lastFrame is not null)
            return (DateTime.UtcNow - lastFrame.TimestampUtc).TotalMilliseconds;

        return double.NaN;
    }

    private static string FormatWaypoint(AdvancedTaskReport taskReport)
    {
        if (taskReport.WaypointCount <= 0 || taskReport.CurrentWaypointIndex < 0)
            return "n/a";

        return $"{taskReport.CurrentWaypointIndex + 1}/{taskReport.WaypointCount}";
    }
}
