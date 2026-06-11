using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Runtime.Planning;
using Hydronom.Runtime.Actuators;
using Hydronom.AI.Orchestration;
using Hydronom.Core.Control;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Modules;
using Hydronom.Core.Modules.Control;
using Hydronom.Runtime.Scenarios.Runtime;
using Hydronom.Runtime.Scheduling;
using Hydronom.Runtime.Tuning;
using Hydronom.Runtime.Vehicles;
using Hydronom.Runtime.Twin;
using Hydronom.Runtime.World.Runtime;

partial class Program
{
    static async Task Main()
    {
        var config = BuildRuntimeConfiguration();
        ConfigureRuntimeCulture();

        NativeSensors.TryInit();

        var runtime = ReadRuntimeOptions(config);
        var physics = ReadPhysicsOptions(config);
        var externalPoseOptions = ReadExternalPoseOptions(config, runtime);

        PrintBootstrapSummary(runtime, physics, externalPoseOptions);

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var vehicleProfileBinding = VehicleProfileRuntimeBootstrapper.Bootstrap(
            config,
            AppContext.BaseDirectory);

        var activeVehicleContext = vehicleProfileBinding.ActiveContext;

        Console.WriteLine(vehicleProfileBinding.BuildSummary());
        var runtimeWorldModel = new RuntimeWorldModel();

        Process? pythonProc = null;
        if (ReadBool(config, "Python:AutoStart", false))
        {
            pythonProc = MaybeStartPythonSensorHub(config);
        }
        else
        {
            Console.WriteLine("[PY] Python sensor hub auto-start devre dışı veya belirtilmedi.");
        }

        var analysisImpl = CreateAnalysisModule(config);
        IAnalysisModule analysis = analysisImpl;

        var decision = CreateDecisionModule(config);
        var controlModule = new PlatformControlModule();

        ITaskManager tasks = CreateTaskManager(config);

        var planningHost = new RuntimePlanningHost(
            config,
            tasks,
            runtimeWorldModel);

        var planningCache = new RuntimePlanningCache();

        var feedback = CreateFeedbackRecorder(config);
        var motors = CreateMotorController(config);

        var actuatorSystem = CreateActuatorSystem(config, runtime, motors);
        var actuatorManager = actuatorSystem.Manager;
        var actuatorBus = actuatorSystem.Bus;

        var controlCapability = activeVehicleContext.HasProfile
            ? activeVehicleContext.CapabilityProfile
            : actuatorManager.CapabilityProfile;

        controlModule.SetCapabilityProfile(controlCapability);

        Console.WriteLine(
            activeVehicleContext.HasProfile
                ? $"[CFG] PlatformControl capability bind → vehicle-profile {controlModule.CapabilityProfile.Summary}"
                : $"[CFG] PlatformControl capability bind → actuator-manager {controlModule.CapabilityProfile.Summary}");

        var limiter = CreateSafetyLimiter(config);

        var state = VehicleState.Zero;
        var externalPoseState = new ExternalPoseState();

        IFrameSource frameSource;
        TcpJsonFrameSource? tcpFrameSource = null;
        ITwinPublisher? twinPublisher = null;
        RuntimeTelemetryRuntime? runtimeTelemetryRuntime = null;

        var sourceType = config["SensorSource:Type"] ?? "TcpJson";

        if (sourceType.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            frameSource = new NullFrameSource();
            Console.WriteLine("[SRC] NullFrameSource aktif.");
        }
        else if (sourceType.Equals("TcpJson", StringComparison.OrdinalIgnoreCase))
        {
            string host = config["SensorSource:TcpJson:Host"] ?? "127.0.0.1";
            int port = ReadInt(config, "SensorSource:TcpJson:Port", 5055);
            int fresh = ReadInt(config, "SensorSource:TcpJson:FreshMs", 300);

            tcpFrameSource = new TcpJsonFrameSource(host, port, TimeSpan.FromMilliseconds(fresh));
            frameSource = tcpFrameSource;

            _ = tcpFrameSource.StartAsync(cts.Token);

            Console.WriteLine($"[SRC] TcpJsonFrameSource started on {host}:{port} (fresh={fresh}ms).");

            if (IsRuntimeTelemetryEnabled(config))
            {
                runtimeTelemetryRuntime = CreateRuntimeTelemetryRuntime(
                    config,
                    tcpFrameSource.Server,
                    state,
                    runtimeWorldModel
                );
            }
            else
            {
                Console.WriteLine("[RT-TEL] Runtime telemetry summary disabled.");
            }

            bool twinEnabled = ReadBool(config, "Twin:Enabled", false);
            if (twinEnabled)
            {
                twinPublisher = new TcpTwinPublisher(tcpFrameSource.Server)
                {
                    ReferenceLatDeg = ReadDouble(config, "Twin:Gps:ReferenceLatDeg", 41.0224),
                    ReferenceLonDeg = ReadDouble(config, "Twin:Gps:ReferenceLonDeg", 28.8321),
                    ReferenceAltM = ReadDouble(config, "Twin:Gps:ReferenceAltM", 0.0),
                    GpsRateHz = ReadDouble(config, "Twin:Gps:RateHz", 5.0),
                    ImuRateHz = ReadDouble(config, "Twin:Imu:RateHz", 20.0),
                    GpsFix = ReadInt(config, "Twin:Gps:Fix", 3),
                    GpsHdop = ReadDouble(config, "Twin:Gps:Hdop", 0.7),
                    SourceName = config["Twin:SourceName"] ?? "csharp-twin"
                };

                Console.WriteLine(
                    $"[TWIN] Enabled → RefLat={((TcpTwinPublisher)twinPublisher).ReferenceLatDeg:F6} " +
                    $"RefLon={((TcpTwinPublisher)twinPublisher).ReferenceLonDeg:F6} " +
                    $"GpsHz={((TcpTwinPublisher)twinPublisher).GpsRateHz:F1} " +
                    $"ImuHz={((TcpTwinPublisher)twinPublisher).ImuRateHz:F1}"
                );
            }
            else
            {
                Console.WriteLine("[TWIN] Disabled.");
            }
        }
        else
        {
            throw new NotSupportedException($"Unknown SensorSource type: {sourceType}");
        }

        string cmdHost = config["Control:CommandServer:Host"] ?? "127.0.0.1";
        int cmdPort = ReadInt(config, "Control:CommandServer:Port", 5060);

        int tickMs = (int)Math.Round(ReadDouble(config, "Runtime:TickMs", 100));
        if (tickMs < 10)
            tickMs = 10;

        Console.WriteLine($"[CFG] Tick → {tickMs} ms | DevMode={runtime.DevMode} SimMode={runtime.SimMode}");

        var tuner = CreateInlineTuner(
            analysisImpl,
            limiter,
            tasks,
            getTickMs: () => tickMs,
            setTickMs: v => tickMs = v
        );

        var aiGateway = CreateAiGateway(config);

        var runtimeScenarioController = new RuntimeScenarioController(
            config,
            tasks,
            runtimeWorldModel,
            activeVehicleContext);

        await runtimeScenarioController.AutoStartFromConfigAsync(
            state,
            cts.Token);

        var geometryAuthority = CreateScenarioGeometryAuthority(
            config,
            runtimeWorldModel);

        RuntimeScenarioGeometrySnapshot lastGeometrySnapshot = RuntimeScenarioGeometrySnapshot.Empty;

        var cmdSrv = new CommandServer(
            cmdHost,
            cmdPort,
            tasks,
            tuner,
            actuatorManager,
            ai: aiGateway,
            scenarioController: runtimeScenarioController
        );

        _ = cmdSrv.StartAsync(cts.Token);

        Console.WriteLine($"Hydronom runtime started. Frames on {sourceType}, Commands on {cmdHost}:{cmdPort}. Ctrl+C → stop.");

        bool invertRudder = ReadBool(config, "Control:InvertRudder", false);
        int heartbeatTimeoutMs = ReadInt(config, "Control:HeartbeatTimeoutMs", 1500);
        int manualCommandTimeoutMs = ReadInt(config, "Control:ManualCommandTimeoutMs", 1000);
        var manualLimits = ReadManualControlLimits(config);

        var loopState = LoopRuntimeState.Create(tickMs);

        var analysisCache = new RuntimeAnalysisCache();
        var intentCache = new RuntimeControlIntentCache();
        var outputCache = new RuntimeControlOutputCache();

        FusedFrame? latestFrameForAnalysis = null;

        double currentDtSeconds = tickMs / 1000.0;

        var initialLimitReport = limiter.LimitAdvanced(DecisionCommand.Zero, currentDtSeconds);

        DecisionCommand lastDesiredCommand = DecisionCommand.Zero;
        DecisionCommand lastLimitedCommand = initialLimitReport.Output;
        LimitFlags lastLimitFlags = initialLimitReport.Flags;
        SafetyLimitReport lastLimitReport = initialLimitReport;

        Vec3 lastForceBody = Vec3.Zero;
        Vec3 lastTorqueBody = Vec3.Zero;
        ActuatorAllocationReport lastAllocationReport = actuatorManager.LastAllocationReport;

        string lastControlMode = "INIT";
        AdvancedDecisionReport lastDecisionReport = AdvancedDecisionReport.Empty;

        var runtimeScheduler = CreateRuntimeScheduler(config);

        runtimeScheduler.RegisterSync(
            RuntimeModuleKind.Analysis,
            "AdvancedAnalysis",
            RuntimeFrequencyProfile.FromHz(
                ReadSchedulerHz(config, "Runtime:Scheduler:AnalysisHz", 10.0),
                deadlineRatio: 0.80,
                maxCatchUpTicks: 0.0),
            _ =>
            {
                if (latestFrameForAnalysis is null)
                    return RuntimeModuleTickResult.Warn("NO_FRAME");

                var scheduledInsights = analysis.Analyze(latestFrameForAnalysis);
                var scheduledReport = analysisImpl.LastReport;

                analysisCache.Update(
                    scheduledInsights,
                    scheduledReport,
                    DateTime.UtcNow);

                return RuntimeModuleTickResult.Ok("ANALYSIS_UPDATED", producedOutput: true);
            });

        runtimeScheduler.RegisterSync(
            RuntimeModuleKind.TrajectoryGenerator,
            "RuntimePlanningHost",
            RuntimeFrequencyProfile.FromHz(
                ReadSchedulerHz(config, "Runtime:Scheduler:PlanningHz", 10.0),
                deadlineRatio: 0.80,
                maxCatchUpTicks: 0.0),
            _ =>
            {
                try
                {
                    var snapshot = planningHost.Tick(
                        state,
                        DateTime.UtcNow);

                    planningCache.Update(snapshot);

                    if (!snapshot.HasPlan)
                        return RuntimeModuleTickResult.Warn(snapshot.Summary);

                    return RuntimeModuleTickResult.Ok(
                        snapshot.Summary,
                        producedOutput: true);
                }
                catch (Exception ex)
                {
                    planningCache.SetError(ex.Message);

                    return RuntimeModuleTickResult.Fail(
                        $"PLANNING_ERROR:{ex.Message}");
                }
            });

        runtimeScheduler.RegisterSync(
            RuntimeModuleKind.Decision,
            "AdvancedDecisionIntent",
            RuntimeFrequencyProfile.FromHz(
                ReadSchedulerHz(config, "Runtime:Scheduler:DecisionHz", 10.0),
                deadlineRatio: 0.80,
                maxCatchUpTicks: 0.0),
            _ =>
            {
                var analysisSnapshot = analysisCache.Snapshot();
                var insights = analysisSnapshot.HasValue
                    ? analysisSnapshot.Insights
                    : Insights.Clear;

                var nowUtc = DateTime.UtcNow;
                var heartbeatAgeMs = (nowUtc - cmdSrv.LastHeartbeatUtc).TotalMilliseconds;
                var manualAgeMs = (nowUtc - cmdSrv.LastManualCommandUtc).TotalMilliseconds;

                bool heartbeatFresh = heartbeatAgeMs <= heartbeatTimeoutMs;
                bool manualFresh = manualAgeMs <= manualCommandTimeoutMs;

                if (cmdSrv.IsEmergencyStop)
                {
                    if (!loopState.EstopTaskCleared)
                    {
                        tasks.ClearTask();
                        loopState.EstopTaskCleared = true;
                    }

                    var report = AdvancedDecisionReport.Empty with
                    {
                        Reason = "ESTOP_INTENT"
                    };

                    intentCache.Update(
                        new ControlIntent(
                            Kind: ControlIntentKind.EmergencyStop,
                            TargetPosition: state.Position,
                            TargetHeadingDeg: state.Orientation.YawDeg,
                            DesiredForwardSpeedMps: 0.0,
                            DesiredDepthMeters: state.Position.Z,
                            DesiredAltitudeMeters: 0.0,
                            HoldHeading: true,
                            HoldDepth: true,
                            AllowReverse: false,
                            RiskLevel: 1.0,
                            Reason: "ESTOP_INTENT"),
                        report,
                        DateTime.UtcNow);

                    lastControlMode = "ESTOP";
                    lastDecisionReport = report;

                    return RuntimeModuleTickResult.Ok("ESTOP_INTENT", producedOutput: true);
                }

                if (cmdSrv.IsManualMode)
                {
                    loopState.EstopTaskCleared = false;

                    DecisionCommand manualCommand = DecisionCommand.Zero;
                    string reason;

                    if (cmdSrv.IsArmed && heartbeatFresh && manualFresh)
                    {
                        var md = cmdSrv.CurrentManualDrive;

                        manualCommand = new DecisionCommand(
                            fx: md.Surge * manualLimits.MaxFxN,
                            fy: md.Sway * manualLimits.MaxFyN,
                            fz: md.Heave * manualLimits.MaxFzN,
                            tx: md.Roll * manualLimits.MaxTxNm,
                            ty: md.Pitch * manualLimits.MaxTyNm,
                            tz: md.Yaw * manualLimits.MaxTzNm
                        );

                        reason = "MANUAL_DIRECT";
                    }
                    else
                    {
                        reason = cmdSrv.IsArmed ? "MANUAL_HOLD" : "DISARMED";
                    }

                    if (invertRudder)
                        manualCommand = manualCommand with { Tz = -manualCommand.Tz };

                    outputCache.Update(
                        new ControlOutput(
                            manualCommand,
                            "MANUAL",
                            reason),
                        DateTime.UtcNow);

                    var report = AdvancedDecisionReport.Empty with
                    {
                        Reason = reason
                    };

                    intentCache.Update(
                        new ControlIntent(
                            Kind: ControlIntentKind.Manual,
                            TargetPosition: state.Position,
                            TargetHeadingDeg: state.Orientation.YawDeg,
                            DesiredForwardSpeedMps: 0.0,
                            DesiredDepthMeters: state.Position.Z,
                            DesiredAltitudeMeters: 0.0,
                            HoldHeading: true,
                            HoldDepth: true,
                            AllowReverse: true,
                            RiskLevel: 0.0,
                            Reason: reason),
                        report,
                        DateTime.UtcNow);

                    lastControlMode = cmdSrv.IsArmed ? "MANUAL" : "DISARMED";
                    lastDecisionReport = report;

                    return RuntimeModuleTickResult.Ok(reason, producedOutput: true);
                }

                loopState.EstopTaskCleared = false;

                tasks.Update(insights, state);

                if (!cmdSrv.IsArmed)
                {
                    var report = AdvancedDecisionReport.Empty with
                    {
                        Reason = "DISARMED"
                    };

                    intentCache.Update(
                        ControlIntent.Idle,
                        report,
                        DateTime.UtcNow);

                    lastControlMode = "DISARMED";
                    lastDecisionReport = report;

                    return RuntimeModuleTickResult.Ok("DISARMED_INTENT", producedOutput: true);
                }

                if (decision is AdvancedDecision advancedDecision)
                {
                    var taskForDecision = tasks.CurrentTask;
                    var planningSnapshot = planningCache.Snapshot();
                    var geometrySnapshot = lastGeometrySnapshot.Sanitized();

                    var planningRiskScore = ComputePlanningRiskScore(planningSnapshot);
                    var finalRiskScore = ComputeWorldAwareRiskScore(
                        planningSnapshot,
                        geometrySnapshot);

                    var hardBlocked = IsWorldAwareHardBlocked(
                        planningSnapshot,
                        geometrySnapshot,
                        finalRiskScore);

                    var softBlocked = IsWorldAwareSoftBlocked(
                        planningSnapshot,
                        geometrySnapshot,
                        finalRiskScore);

                    var geometryHardBlocked =
                        geometrySnapshot.CollisionCandidate ||
                        geometrySnapshot.HardBlocked;

                    var worldAwareAdvice = BuildWorldAwareDecisionAdvice(
                        analysisImpl.LastOperationalContext.Advice,
                        planningSnapshot,
                        geometrySnapshot);

                    advancedDecision.UpdateAdvice(worldAwareAdvice);

                    /*
                     * Hard blocked / collision candidate plan veya geometry target olarak Decision'a verilmez.
                     *
                     * Ek kural:
                     * Scenario reach_wp görevlerinde local-detour, gerçek waypoint hedefini ezemez.
                     * Gate/slalom bölgesinde planner'ın local-detour üretmesi aracı kapı koridorundan
                     * çıkarıp duba dibine sürükleyebiliyor.
                     */
                    if (ShouldBindPlanLookAheadToDecision(
                            taskForDecision,
                            planningSnapshot,
                            geometrySnapshot,
                            finalRiskScore,
                            hardBlocked,
                            softBlocked))
                    {
                        var lookAhead = planningSnapshot.Trajectory.LookAheadPoint!;

                        var plannedTask = new TaskDefinition(
                            taskForDecision!.Name,
                            lookAhead.Position)
                        {
                            HoldOnArrive = false,
                            WaitSecondsPerPoint = taskForDecision.WaitSecondsPerPoint,
                            Loop = taskForDecision.Loop,
                            CompletionAuthority = taskForDecision.CompletionAuthority,
                            ExternalOwnerId = taskForDecision.ExternalOwnerId,
                            ExternalObjectiveId = taskForDecision.ExternalObjectiveId
                        };

                        taskForDecision = plannedTask;
                    }

                    var intent = advancedDecision.DecideIntent(
                        insights,
                        taskForDecision,
                        state,
                        currentDtSeconds);

                    if (geometryHardBlocked)
                    {
                        intent = BuildGeometryEscapeIntent(
                            state,
                            geometrySnapshot,
                            finalRiskScore,
                            planningRiskScore,
                            planningSnapshot);
                    }
                    else if (hardBlocked)
                    {
                        intent = new ControlIntent(
                            Kind: ControlIntentKind.HoldPosition,
                            TargetPosition: state.Position,
                            TargetHeadingDeg: state.Orientation.YawDeg,
                            DesiredForwardSpeedMps: 0.0,
                            DesiredDepthMeters: state.Position.Z,
                            DesiredAltitudeMeters: 0.0,
                            HoldHeading: true,
                            HoldDepth: true,
                            AllowReverse: false,
                            RiskLevel: 1.0,
                            Reason:
                                $"PLAN_HARD_COLLISION_GUARD " +
                                $"planRisk={planningRiskScore:F2} " +
                                $"finalRisk={finalRiskScore:F2} " +
                                $"geomRisk={geometrySnapshot.RiskScore:F2} " +
                                $"geom={geometrySnapshot.Summary} " +
                                $"plan={planningSnapshot.Summary} " +
                                $"local={planningSnapshot.LocalPath.Summary} " +
                                $"traj={planningSnapshot.Trajectory.Summary}");
                    }
                    else if (ShouldBindPlanReferenceToIntent(
                                 tasks.CurrentTask,
                                 planningSnapshot,
                                 geometrySnapshot,
                                 finalRiskScore,
                                 hardBlocked,
                                 softBlocked))
                    {
                        var reference = planningSnapshot.Trajectory.ToControlReference(intent.TargetPosition);

                        var referenceRisk = Math.Max(reference.RiskScore, finalRiskScore);

                        var desiredSpeed = reference.RequiresSlowMode || softBlocked
                            ? Math.Min(intent.DesiredForwardSpeedMps, reference.DesiredSpeedMps)
                            : reference.DesiredSpeedMps;

                        if (finalRiskScore >= 0.85)
                            desiredSpeed = Math.Min(desiredSpeed, 0.12);
                        else if (finalRiskScore >= 0.70)
                            desiredSpeed = Math.Min(desiredSpeed, 0.28);
                        else if (finalRiskScore >= 0.50)
                            desiredSpeed = Math.Min(desiredSpeed, 0.45);

                        intent = intent with
                        {
                            TargetPosition = reference.TargetPosition,
                            TargetHeadingDeg = reference.TargetHeadingDeg,
                            DesiredForwardSpeedMps = desiredSpeed,
                            RiskLevel = Math.Max(intent.RiskLevel, referenceRisk),
                            Reason =
                                $"{intent.Reason}|PLAN:{planningSnapshot.Trajectory.Summary}" +
                                $"|REF:{reference.Reason}" +
                                $"|PLAN_RISK:{planningRiskScore:F2}" +
                                $"|GEOM_RISK:{geometrySnapshot.RiskScore:F2}" +
                                $"|FINAL_RISK:{finalRiskScore:F2}" +
                                $"|SOFT_BLOCK:{softBlocked}" +
                                $"|GEOM:{geometrySnapshot.Summary}"
                        };
                    }
                    else if (planningSnapshot.HasPlan &&
                             planningSnapshot.IsValid &&
                             planningSnapshot.AgeMs <= 500.0 &&
                             planningSnapshot.Trajectory.LookAheadPoint is not null)
                    {
                        /*
                         * Plan var ama referans bind edilmedi.
                         * Bu özellikle scenario reach_wp + local-detour durumunda bilinçli yapılır.
                         * Planner hedefi ezemez; sadece risk/speed bilgisini intent'e işleriz.
                         */
                        var desiredSpeed = intent.DesiredForwardSpeedMps;

                        if (finalRiskScore >= 0.85)
                            desiredSpeed = Math.Min(desiredSpeed, 0.12);
                        else if (finalRiskScore >= 0.70)
                            desiredSpeed = Math.Min(desiredSpeed, 0.28);
                        else if (finalRiskScore >= 0.50)
                            desiredSpeed = Math.Min(desiredSpeed, 0.45);

                        /*
                         * Soft risk var ama hard collision yoksa aracı tamamen öldürme.
                         */
                        if (!hardBlocked &&
                            finalRiskScore < 0.85 &&
                            desiredSpeed > 0.0)
                        {
                            desiredSpeed = Math.Max(desiredSpeed, 0.18);
                        }

                        intent = intent with
                        {
                            DesiredForwardSpeedMps = desiredSpeed,
                            RiskLevel = Math.Max(intent.RiskLevel, finalRiskScore),
                            Reason =
                                $"{intent.Reason}" +
                                $"|PLAN_REFERENCE_REJECTED_KEEP_TASK_TARGET" +
                                $"|PLAN:{planningSnapshot.Trajectory.Summary}" +
                                $"|LOOKAHEAD:{planningSnapshot.Trajectory.LookAheadPoint.Id}" +
                                $"|PLAN_RISK:{planningRiskScore:F2}" +
                                $"|GEOM_RISK:{geometrySnapshot.RiskScore:F2}" +
                                $"|FINAL_RISK:{finalRiskScore:F2}" +
                                $"|SOFT_BLOCK:{softBlocked}" +
                                $"|GEOM:{geometrySnapshot.Summary}"
                        };
                    }
                    else
                    {
                        intent = intent with
                        {
                            RiskLevel = Math.Max(intent.RiskLevel, finalRiskScore),
                            Reason =
                                $"{intent.Reason}" +
                                $"|NO_VALID_PLAN_RISK:{planningRiskScore:F2}" +
                                $"|GEOM_RISK:{geometrySnapshot.RiskScore:F2}" +
                                $"|FINAL_RISK:{finalRiskScore:F2}" +
                                $"|GEOM:{geometrySnapshot.Summary}"
                        };
                    }

                    var report = advancedDecision.LastDecisionReport;

                    intentCache.Update(
                        intent,
                        report,
                        DateTime.UtcNow);

                    lastControlMode = "AUTO";
                    lastDecisionReport = report;

                    return RuntimeModuleTickResult.Ok(
                        hardBlocked
                            ? "INTENT_WORLD_GEOMETRY_GUARD"
                            : "INTENT_UPDATED_WITH_WORLD_GEOMETRY",
                        producedOutput: true);
                }

                var fallbackReport = AdvancedDecisionReport.Empty with
                {
                    Reason = "DECISION_MODULE_HAS_NO_INTENT_API"
                };

                intentCache.Update(
                    ControlIntent.Idle,
                    fallbackReport,
                    DateTime.UtcNow);

                lastControlMode = "AUTO";
                lastDecisionReport = fallbackReport;

                return RuntimeModuleTickResult.Warn("DECISION_MODULE_HAS_NO_INTENT_API");
            });

        runtimeScheduler.RegisterSync(
            RuntimeModuleKind.Control,
            "PlatformControl",
            RuntimeFrequencyProfile.FromHz(
                ReadSchedulerHz(config, "Runtime:Scheduler:ControlHz", 50.0),
                deadlineRatio: 0.80,
                maxCatchUpTicks: 0.0),
            _ =>
            {
                if (cmdSrv.IsManualMode)
                {
                    return RuntimeModuleTickResult.Ok("MANUAL_OUTPUT_PASSTHROUGH");
                }

                var intentSnapshot = intentCache.Snapshot();

                var intent = intentSnapshot.HasValue
                    ? intentSnapshot.Intent
                    : ControlIntent.Idle;

                controlModule.SetCapabilityProfile(controlCapability);

                var controlOutput = controlModule.Update(
                    intent,
                    state,
                    currentDtSeconds);

                outputCache.Update(
                    controlOutput,
                    DateTime.UtcNow);

                return RuntimeModuleTickResult.Ok("CONTROL_OUTPUT_UPDATED", producedOutput: true);
            });

        runtimeScheduler.RegisterSync(
            RuntimeModuleKind.ActuatorCommand,
            "ActuatorCommand",
            RuntimeFrequencyProfile.FromHz(
                ReadSchedulerHz(config, "Runtime:Scheduler:ActuatorCommandHz", 100.0),
                deadlineRatio: 0.80,
                maxCatchUpTicks: 0.0),
            _ =>
            {
                var outputSnapshot = outputCache.Snapshot();

                var desiredCommand = outputSnapshot.HasValue
                    ? outputSnapshot.Output.Command
                    : DecisionCommand.Zero;

                if (outputSnapshot.HasValue && outputSnapshot.AgeMs > 500.0)
                    desiredCommand = DecisionCommand.Zero;

                var limitReport = limiter.LimitAdvanced(
                    desiredCommand,
                    currentDtSeconds);

                var limitedCommand = limitReport.Output;

                actuatorBus.Apply(limitedCommand);

                lastDesiredCommand = desiredCommand;
                lastLimitedCommand = limitedCommand;
                lastLimitReport = limitReport;
                lastLimitFlags = limitReport.Flags;

                lastForceBody = actuatorManager.VehicleState.LinearForce;
                lastTorqueBody = actuatorManager.VehicleState.AngularTorque;
                lastAllocationReport = actuatorManager.LastAllocationReport;

                return RuntimeModuleTickResult.Ok("ACTUATOR_COMMAND_APPLIED", producedOutput: true);
            });

        runtimeScheduler.RegisterSync(
            RuntimeModuleKind.Heartbeat,
            "HeartbeatDiagnostics",
            RuntimeFrequencyProfile.FromHz(
                ReadSchedulerHz(config, "Runtime:Scheduler:HeartbeatHz", 1.0),
                deadlineRatio: 0.80,
                maxCatchUpTicks: 0.0),
            _ =>
            {
                EmitSchedulerSnapshot(
                    runtimeScheduler.Snapshot(),
                    runtime.LogVerbose);

                return RuntimeModuleTickResult.Ok("HEARTBEAT_DIAGNOSTICS");
            });

        try
        {
            while (!cts.IsCancellationRequested)
            {
                long loopStartTicks = Stopwatch.GetTimestamp();

                if (loopState.NextLoopTicks == 0)
                    loopState.NextLoopTicks = loopStartTicks + loopState.PeriodTicks;

                if (loopState.TickIndex == 0)
                {
                    currentDtSeconds = tickMs / 1000.0;
                }
                else
                {
                    currentDtSeconds = StopwatchTicksToSeconds(loopStartTicks - (loopState.NextLoopTicks - loopState.PeriodTicks));
                    currentDtSeconds = NormalizeLoopDt(currentDtSeconds, tickMs);
                }

                bool externalApplied = TryApplyExternalPose(
                    frameSource,
                    runtime,
                    externalPoseOptions,
                    ref externalPoseState,
                    ref state,
                    loopState.TickIndex
                );

                var frameToUse = BuildRuntimeFrame(
                    frameSource,
                    state,
                    tasks,
                    runtimeWorldModel,
                    runtime.DevMode,
                    runtime.LogVerbose,
                    externalApplied,
                    out _,
                    out _
                );

                latestFrameForAnalysis = frameToUse;

                lastGeometrySnapshot = UpdateScenarioGeometrySnapshot(
                    geometryAuthority,
                    planningCache,
                    tasks,
                    state,
                    loopState.TickIndex);

                await runtimeScheduler.TickDueAsync(cts.Token);

                var analysisSnapshot = analysisCache.Snapshot();

                var insights = analysisSnapshot.HasValue
                    ? analysisSnapshot.Insights
                    : Insights.Clear;

                var analysisReport = analysisSnapshot.HasValue
                    ? analysisSnapshot.Report
                    : AdvancedAnalysisReport.Empty;

                var intentSnapshotForDiagnostics = intentCache.Snapshot();

                var decisionReport = intentSnapshotForDiagnostics.HasValue
                    ? intentSnapshotForDiagnostics.DecisionReport
                    : lastDecisionReport;

                var planningSnapshotForDiagnostics = planningCache.Snapshot();
                var planningTelemetry = RuntimePlanningTelemetrySnapshot.FromPlanningSnapshot(
                    planningSnapshotForDiagnostics);

                LogTaskChangeIfNeeded(tasks, ref loopState);

                var targetTelemetry = BuildTargetTelemetrySnapshot(tasks, state);

                var diagnostics = new RuntimeDiagnosticsSnapshot(
                    ControlMode: lastControlMode,
                    TargetTelemetry: targetTelemetry,
                    PlanningTelemetry: planningTelemetry,
                    AnalysisReport: analysisReport,
                    DecisionReport: decisionReport,
                    LimitReport: lastLimitReport,
                    AllocationReport: lastAllocationReport,
                    LimitFlags: lastLimitFlags
                );

                if (ShouldEmitLoopLog(runtime, loopState.TickIndex))
                {
                    EmitLoopDiagnostics(
                        runtime,
                        diagnostics,
                        state,
                        insights,
                        lastDesiredCommand,
                        lastLimitedCommand
                    );
                }

                state = IntegrateSyntheticStateIfNeeded(
                    state,
                    lastForceBody,
                    lastTorqueBody,
                    currentDtSeconds,
                    physics,
                    runtime,
                    externalApplied,
                    ref loopState
                );

                TickScenarioRuntimeWithGeometryGuard(
                    runtimeScenarioController,
                    state,
                    loopState.TickIndex,
                    lastGeometrySnapshot);

                await TryPublishOpsTelemetryFramesAsync(
                    tcpFrameSource,
                    runtimeScenarioController,
                    actuatorManager,
                    state,
                    loopState.TickIndex,
                    cts.Token
                );

                if (runtime.EnableNativeTick)
                {
                    NativeSensors.TickIfAvailable(
                        currentDtSeconds,
                        state,
                        lastLimitedCommand.Throttle01,
                        lastLimitedCommand.RudderNeg1To1
                    );
                }

                if (twinPublisher is not null)
                {
                    try
                    {
                        await twinPublisher.PublishAsync(state, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TWIN] publish error: {ex.Message}");
                    }
                }

                await TryTickRuntimeTelemetryPipelineAsync(
                    runtimeTelemetryRuntime,
                    runtime,
                    state,
                    loopState.TickIndex,
                    cts.Token
                );

                EmitVerboseModuleReports(runtime, state, diagnostics);

                feedback.Record(new FeedbackRecord(
                    timestampUtc: DateTime.UtcNow,
                    frame: frameToUse,
                    insights: insights,
                    command: lastLimitedCommand,
                    state: state,
                    forceBody: lastForceBody,
                    torqueBody: lastTorqueBody
                ));

                loopState.TickIndex++;

                EmitHeartbeat(
                    runtime,
                    loopState.TickIndex,
                    tickMs,
                    currentDtSeconds,
                    state,
                    cmdSrv,
                    actuatorManager,
                    diagnostics,
                    ComputeFrameAgeMs(frameSource)
                );

                loopState.PeriodTicks = ComputePeriodTicks(tickMs);
                loopState.NextLoopTicks += loopState.PeriodTicks;

                long nowTicks = Stopwatch.GetTimestamp();
                if (loopState.NextLoopTicks <= nowTicks)
                {
                    long lagTicks = nowTicks - loopState.NextLoopTicks;
                    long missedPeriods = Math.Max(1L, lagTicks / loopState.PeriodTicks + 1L);
                    loopState.NextLoopTicks += missedPeriods * loopState.PeriodTicks;

                    int elapsedMsNow = (int)StopwatchTicksToMs(nowTicks - loopStartTicks);
                    if (elapsedMsNow > tickMs + 5)
                        Console.WriteLine($"[WARN] Loop overrun: elapsed={elapsedMsNow}ms (tick={tickMs}ms)");
                }

                await HybridWaitUntilAsync(loopState.NextLoopTicks, cts.Token);
            }
        }
        finally
        {
            if (runtimeTelemetryRuntime is not null)
            {
                await runtimeTelemetryRuntime.DisposeAsync();
            }

            await ShutdownRuntimeAsync(frameSource, pythonProc, actuatorManager);
        }
    }

    private static bool ShouldBindPlanLookAheadToDecision(
        TaskDefinition? task,
        RuntimePlanningSnapshot planningSnapshot,
        RuntimeScenarioGeometrySnapshot geometrySnapshot,
        double finalRiskScore,
        bool hardBlocked,
        bool softBlocked)
    {
        if (hardBlocked)
            return false;

        if (task is null)
            return false;

        if (!planningSnapshot.HasPlan ||
            !planningSnapshot.IsValid ||
            planningSnapshot.AgeMs > 500.0 ||
            planningSnapshot.Trajectory.LookAheadPoint is null)
        {
            return false;
        }

        /*
         * Scenario reach_wp görevlerinde local-detour, gerçek görev hedefini ezemez.
         * Bu, WP2'ye gitmesi gereken aracın sağ dubanın altındaki detour'a kırılmasını engeller.
         */
        if (IsExternalReachWaypointTask(task) &&
            IsLocalDetourLookAhead(planningSnapshot))
        {
            return false;
        }

        if (IsExternalReachWaypointTask(task) &&
            (softBlocked || geometrySnapshot.SoftBlocked || finalRiskScore >= 0.50) &&
            IsLocalDetourLookAhead(planningSnapshot))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldBindPlanReferenceToIntent(
        TaskDefinition? task,
        RuntimePlanningSnapshot planningSnapshot,
        RuntimeScenarioGeometrySnapshot geometrySnapshot,
        double finalRiskScore,
        bool hardBlocked,
        bool softBlocked)
    {
        if (hardBlocked)
            return false;

        if (task is null)
            return false;

        if (!planningSnapshot.HasPlan ||
            !planningSnapshot.IsValid ||
            planningSnapshot.AgeMs > 500.0 ||
            planningSnapshot.Trajectory.LookAheadPoint is null)
        {
            return false;
        }

        /*
         * Scenario waypoint görevlerinde local-detour intent hedefini ezemez.
         * Bu kapı/slalomda "WP2'ye git" hedefinin "sağ duba altındaki detour'a git"e dönüşmesini engeller.
         */
        if (IsExternalReachWaypointTask(task) &&
            IsLocalDetourLookAhead(planningSnapshot))
        {
            return false;
        }

        if (IsExternalReachWaypointTask(task) &&
            (softBlocked || geometrySnapshot.SoftBlocked || finalRiskScore >= 0.50) &&
            IsLocalDetourLookAhead(planningSnapshot))
        {
            return false;
        }

        return true;
    }

    private static bool IsExternalReachWaypointTask(TaskDefinition? task)
    {
        if (task is null)
            return false;

        if (!task.IsExternallyCompleted)
            return false;

        if (!string.IsNullOrWhiteSpace(task.ExternalObjectiveId) &&
            task.ExternalObjectiveId.Contains("reach_wp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(task.Name) &&
            task.Name.Contains("reach_wp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsLocalDetourLookAhead(RuntimePlanningSnapshot planningSnapshot)
    {
        var id = planningSnapshot.Trajectory.LookAheadPoint?.Id;

        if (string.IsNullOrWhiteSpace(id))
            return false;

        return id.Contains("local-detour", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("detour", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("avoid", StringComparison.OrdinalIgnoreCase);
    }
}