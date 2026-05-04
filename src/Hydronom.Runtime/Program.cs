using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Orchestration;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Scenarios.Runtime;
using Hydronom.Runtime.Tuning;
using Hydronom.Runtime.Twin;

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
        ITaskManager tasks = CreateTaskManager(config);
        var feedback = CreateFeedbackRecorder(config);
        var motors = CreateMotorController(config);

        var actuatorSystem = CreateActuatorSystem(config, runtime, motors);
        var actuatorManager = actuatorSystem.Manager;
        var actuatorBus = actuatorSystem.Bus;

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
                    state
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
            tasks);

        await runtimeScenarioController.AutoStartFromConfigAsync(
            state,
            cts.Token);

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

        try
        {
            while (!cts.IsCancellationRequested)
            {
                long loopStartTicks = Stopwatch.GetTimestamp();

                if (loopState.NextLoopTicks == 0)
                    loopState.NextLoopTicks = loopStartTicks + loopState.PeriodTicks;

                double dtMeasured;
                if (loopState.TickIndex == 0)
                {
                    dtMeasured = tickMs / 1000.0;
                }
                else
                {
                    dtMeasured = StopwatchTicksToSeconds(loopStartTicks - (loopState.NextLoopTicks - loopState.PeriodTicks));
                    dtMeasured = NormalizeLoopDt(dtMeasured, tickMs);
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
                    runtime.DevMode,
                    runtime.LogVerbose,
                    externalApplied,
                    out _,
                    out _
                );

                var insights = analysis.Analyze(frameToUse);
                var analysisReport = analysisImpl.LastReport;

                /*
                * Analysis → Decision Advice Bridge
                *
                * AdvancedAnalysis, IAnalysisModule sözleşmesini bozmadan dışarıya hâlâ Insights döndürür.
                * Ancak içeride LastOperationalContext üzerinden daha zengin bir operasyonel karar tavsiyesi üretir.
                *
                * Bu köprü:
                * - obstacle/sector riskinden türetilen throttle/yaw/slow-mode/coast/safe-heading tavsiyesini
                * - AdvancedDecision içine aktarır.
                *
                * Böylece karar modülü sadece hedef geometrisine göre değil,
                * analiz katmanının operasyonel risk değerlendirmesine göre de davranabilir.
                */
                if (decision is AdvancedDecision advancedDecision)
                {
                    advancedDecision.UpdateAdvice(analysisImpl.LastOperationalContext.Advice);
                }

                var control = SelectControlCommand(
                    cmdSrv,
                    decision,
                    tasks,
                    insights,
                    state,
                    dtMeasured,
                    invertRudder,
                    loopState.EstopTaskCleared,
                    heartbeatTimeoutMs,
                    manualCommandTimeoutMs,
                    manualLimits
                );

                loopState.EstopTaskCleared = control.EstopTaskCleared;

                LogTaskChangeIfNeeded(tasks, ref loopState);

                var targetTelemetry = BuildTargetTelemetrySnapshot(tasks, state);

                var limitReport = limiter.LimitAdvanced(control.DesiredCommand, dtMeasured);
                var cmd = limitReport.Output;
                var limFlags = limitReport.Flags;

                actuatorBus.Apply(cmd);

                Vec3 forceBody = actuatorManager.VehicleState.LinearForce;
                Vec3 torqueBody = actuatorManager.VehicleState.AngularTorque;
                var allocationReport = actuatorManager.LastAllocationReport;

                var diagnostics = new RuntimeDiagnosticsSnapshot(
                    ControlMode: control.ControlMode,
                    TargetTelemetry: targetTelemetry,
                    AnalysisReport: analysisReport,
                    DecisionReport: control.DecisionReport,
                    LimitReport: limitReport,
                    AllocationReport: allocationReport,
                    LimitFlags: limFlags
                );

                if (ShouldEmitLoopLog(runtime, loopState.TickIndex))
                {
                    EmitLoopDiagnostics(
                        runtime,
                        diagnostics,
                        state,
                        insights,
                        control.DesiredCommand,
                        cmd
                    );
                }

                state = IntegrateSyntheticStateIfNeeded(
                    state,
                    forceBody,
                    torqueBody,
                    dtMeasured,
                    physics,
                    runtime,
                    externalApplied,
                    ref loopState
                );

                runtimeScenarioController.Tick(state, loopState.TickIndex);

                if (runtime.EnableNativeTick)
                {
                    NativeSensors.TickIfAvailable(
                        dtMeasured,
                        state,
                        cmd.Throttle01,
                        cmd.RudderNeg1To1
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
                    command: cmd,
                    state: state,
                    forceBody: forceBody,
                    torqueBody: torqueBody
                ));

                loopState.TickIndex++;

                EmitHeartbeat(
                    runtime,
                    loopState.TickIndex,
                    tickMs,
                    dtMeasured,
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

    private sealed class NullFrameSource : IFrameSource
    {
        public bool TryGetLatestFrame(out FusedFrame? frame)
        {
            frame = null;
            return false;
        }
    }
}