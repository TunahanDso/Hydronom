using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Clients;
using Hydronom.AI.Orchestration;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Interfaces.AI;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Actuators;
using Hydronom.Runtime.AI;
using Hydronom.Runtime.AI.Tools;
using Hydronom.Runtime.Buses;
using Hydronom.Runtime.Tuning;
using Hydronom.Runtime.Twin;
using Microsoft.Extensions.Configuration;

// ThrusterDesc adını Runtime.Actuators içindeki tipe sabitle
using ThrusterDesc = Hydronom.Runtime.Actuators.ThrusterDesc;

partial class Program
{
    static async Task Main()
    {
        // ---------------------------------------------------------------------
        // CONFIG
        // ---------------------------------------------------------------------
        var cb = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var cfgDir = Path.Combine(AppContext.BaseDirectory, "Configs");
        if (Directory.Exists(cfgDir))
        {
            foreach (var f in Directory.EnumerateFiles(cfgDir, "*.json", SearchOption.TopDirectoryOnly))
                cb.AddJsonFile(Path.Combine("Configs", Path.GetFileName(f)), optional: true, reloadOnChange: true);
        }

        var config = cb.Build();

        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------------
        // NATIVE
        // ---------------------------------------------------------------------
        NativeSensors.TryInit();

        // ---------------------------------------------------------------------
        // MODE FLAGS
        // ---------------------------------------------------------------------
        bool devMode = bool.TryParse(config["Runtime:DevMode"], out var dv) && dv;

        var simModeStr = config["Simulation:Mode"] ?? "";
        bool simMode = simModeStr.Equals("Sim", StringComparison.OrdinalIgnoreCase)
                    || simModeStr.Equals("Hybrid", StringComparison.OrdinalIgnoreCase);

        bool allowExternalPoseOverrideInSim =
            bool.TryParse(config["Simulation:AllowExternalPoseOverride"], out var aepo) && aepo;

        bool useSyntheticStateWhenNoExternal =
            !bool.TryParse(config["Runtime:UseSyntheticStateWhenNoExternal"], out var uss) || uss;

        bool enableNativeTick =
            !bool.TryParse(config["Runtime:NativeSensors:EnableTick"], out var ent) || ent;

        var logMode = config["Logging:Mode"] ?? "Compact";
        bool logVerbose = logMode.Equals("Verbose", StringComparison.OrdinalIgnoreCase);

        int loopLogEvery = ReadInt(config, "Logging:LoopEvery", logVerbose ? 1 : 5);
        if (loopLogEvery < 1) loopLogEvery = 1;

        int heartbeatEvery = ReadInt(config, "Logging:HeartbeatEvery", 10);
        if (heartbeatEvery < 1) heartbeatEvery = 10;

        Console.WriteLine($"[CFG] Logging → Mode={logMode}, LoopEvery={loopLogEvery}, HeartbeatEvery={heartbeatEvery}");
        Console.WriteLine($"[CFG] Modes → Dev={devMode} Sim={simMode} AllowExtInSim={allowExternalPoseOverrideInSim} SyntheticState={useSyntheticStateWhenNoExternal}");
        Console.WriteLine("[CFG] Obstacle Policy → Runtime obstacle üretmez. Obstacle yalnızca Python/TcpJson fresh frame'den alınır.");

        // ---------------------------------------------------------------------
        // CANCEL
        // ---------------------------------------------------------------------
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // ---------------------------------------------------------------------
        // PYTHON HUB
        // ---------------------------------------------------------------------
        Process? pythonProc = null;

        // ---------------------------------------------------------------------
        // MODULES
        // ---------------------------------------------------------------------
        var aheadM = ReadDouble(config, "Analysis:AheadDistanceM", 12.0);
        var fovDeg = ReadDouble(config, "Analysis:HalfFovDeg", 45.0);

        var analysisImpl = new BaselineAnalysis(aheadM, fovDeg);
        IAnalysisModule analysis = analysisImpl;
        Console.WriteLine($"[CFG] Analysis → Ahead={aheadM:F1} m, HalfFov={fovDeg:F0}°");

        IDecisionModule decision = new AdvancedDecision();
        ITaskManager tasks = new AdvancedTaskManager();
        IFeedbackRecorder feedback = new ConsoleFeedbackRecorder();
        IMotorController motors = new MockMotorController();

        // ---------------------------------------------------------------------
        // INITIAL STATE
        // ---------------------------------------------------------------------
        var state = VehicleState.Zero;

        double massKg = ReadDouble(config, "Physics:MassKg", 25.0);
        var inertia = new Vec3(
            ReadDouble(config, "Physics:Inertia:Ix", 0.2),
            ReadDouble(config, "Physics:Inertia:Iy", 0.2),
            ReadDouble(config, "Physics:Inertia:Iz", 0.1)
        );

        double linDragX = ReadDouble(config, "Physics:Drag:Linear:X", 2.0);
        double linDragY = ReadDouble(config, "Physics:Drag:Linear:Y", 6.0);
        double linDragZ = ReadDouble(config, "Physics:Drag:Linear:Z", 6.0);

        double quadDragX = ReadDouble(config, "Physics:Drag:Quadratic:X", 0.25);
        double quadDragY = ReadDouble(config, "Physics:Drag:Quadratic:Y", 0.8);
        double quadDragZ = ReadDouble(config, "Physics:Drag:Quadratic:Z", 0.8);

        double angLinDragX = ReadDouble(config, "Physics:AngularDrag:Linear:X", 0.50);
        double angLinDragY = ReadDouble(config, "Physics:AngularDrag:Linear:Y", 0.50);
        double angLinDragZ = ReadDouble(config, "Physics:AngularDrag:Linear:Z", 1.80);

        double angQuadDragX = ReadDouble(config, "Physics:AngularDrag:Quadratic:X", 0.02);
        double angQuadDragY = ReadDouble(config, "Physics:AngularDrag:Quadratic:Y", 0.02);
        double angQuadDragZ = ReadDouble(config, "Physics:AngularDrag:Quadratic:Z", 0.18);

        double maxSyntheticLinearSpeed = ReadDouble(config, "Physics:MaxLinearSpeedMps", 8.0);
        double maxSyntheticAngularSpeedDeg = ReadDouble(config, "Physics:MaxAngularSpeedDegPerSec", 220.0);

        Console.WriteLine(
            $"[CFG] Physics → Mass={massKg:F2}kg Inertia=({inertia.X:F2},{inertia.Y:F2},{inertia.Z:F2}) " +
            $"LinDrag=({linDragX:F2},{linDragY:F2},{linDragZ:F2}) QuadDrag=({quadDragX:F2},{quadDragY:F2},{quadDragZ:F2})"
        );

        Console.WriteLine(
            $"[CFG] AngularDrag → Lin=({angLinDragX:F2},{angLinDragY:F2},{angLinDragZ:F2}) " +
            $"Quad=({angQuadDragX:F2},{angQuadDragY:F2},{angQuadDragZ:F2}) " +
            $"MaxLinSpeed={maxSyntheticLinearSpeed:F2}m/s MaxAngSpeed={maxSyntheticAngularSpeedDeg:F1}deg/s"
        );

        // ---------------------------------------------------------------------
        // EXTERNAL POSE RECONCILIATION
        // ---------------------------------------------------------------------
        double externalVelBlend = ReadDouble(config, "SensorSource:ExternalPose:VelocityBlend", 0.65);
        externalVelBlend = Math.Clamp(externalVelBlend, 0.0, 1.0);

        double externalYawRateBlend = ReadDouble(config, "SensorSource:ExternalPose:YawRateBlend", 0.70);
        externalYawRateBlend = Math.Clamp(externalYawRateBlend, 0.0, 1.0);

        bool resetVelOnExternalTeleport =
            !bool.TryParse(config["SensorSource:ExternalPose:ResetVelocityOnTeleport"], out var rvot) || rvot;

        double externalTeleportDistanceM = ReadDouble(config, "SensorSource:ExternalPose:TeleportDistanceM", 2.5);
        double externalTeleportYawDeg = ReadDouble(config, "SensorSource:ExternalPose:TeleportYawDeg", 35.0);

        bool hasPrevExternalPose = false;
        double prevExternalX = 0.0;
        double prevExternalY = 0.0;
        double prevExternalYawDeg = 0.0;
        DateTime prevExternalUtc = DateTime.MinValue;

        Console.WriteLine(
            $"[CFG] ExternalPose → VelBlend={externalVelBlend:F2} YawRateBlend={externalYawRateBlend:F2} " +
            $"ResetOnTeleport={resetVelOnExternalTeleport} TeleportDist={externalTeleportDistanceM:F2}m TeleportYaw={externalTeleportYawDeg:F1}°"
        );

        // ---------------------------------------------------------------------
        // SERIAL / ACTUATOR
        // ---------------------------------------------------------------------
        var serialPortName = config["Actuator:Serial:Port"];
        if (string.IsNullOrWhiteSpace(serialPortName))
            serialPortName = null;

        bool disableSerial = devMode || simMode;
        if (disableSerial)
            serialPortName = null;

        var serialBaud = ReadInt(config, "Actuator:Serial:Baud", 115200);
        Console.WriteLine($"[CFG] Actuator Serial → {(serialPortName ?? "<disabled>")} @ {serialBaud}");

        ThrusterDesc[]? thrusterDescs = null;

        try
        {
            thrusterDescs = config.GetSection("Thrusters").Get<ThrusterDesc[]>();
        }
        catch
        {
            thrusterDescs = null;
        }

        if (thrusterDescs == null || thrusterDescs.Length == 0)
        {
            var fromCfg = TryLoadThrustersFromChannelProfiles(config);
            if (fromCfg.Length > 0)
                thrusterDescs = fromCfg;
        }

        if (thrusterDescs == null || thrusterDescs.Length == 0)
        {
            try
            {
                thrusterDescs = config.GetSection("Actuator:Thrusters").Get<ThrusterDesc[]>();
                if (thrusterDescs != null && thrusterDescs.Length > 0)
                    Console.WriteLine($"[CFG] Thrusters loaded from legacy 'Actuator:Thrusters' ({thrusterDescs.Length} ch).");
            }
            catch
            {
                thrusterDescs = null;
            }
        }

        var actuatorManager = new ActuatorManager(thrusterDescs, motors, serialPortName, serialBaud);
        var actuatorBus = new ActuatorBus(new IActuator[] { actuatorManager });

        Console.WriteLine($"[CFG] Actuator Authority → {actuatorManager.AuthorityProfile}");

        // ---------------------------------------------------------------------
        // SAFETY LIMITER
        // ---------------------------------------------------------------------
        var limiter = new SafetyLimiter(
            throttleRatePerSec: ReadDouble(config, "Control:Limiter:ThrottleRatePerSec", 40.0),
            rudderRatePerSec: ReadDouble(config, "Control:Limiter:RudderRatePerSec", 15.0)
        );

        limiter.SetAxisRates(
            fx: ReadNullableDouble(config, "Control:Limiter:AxisRates:Fx"),
            fy: ReadNullableDouble(config, "Control:Limiter:AxisRates:Fy"),
            fz: ReadNullableDouble(config, "Control:Limiter:AxisRates:Fz"),
            tx: ReadNullableDouble(config, "Control:Limiter:AxisRates:Tx"),
            ty: ReadNullableDouble(config, "Control:Limiter:AxisRates:Ty"),
            tz: ReadNullableDouble(config, "Control:Limiter:AxisRates:Tz")
        );

        limiter.SetAxisDeadbands(
            fx: ReadNullableDouble(config, "Control:Limiter:Deadbands:Fx"),
            fy: ReadNullableDouble(config, "Control:Limiter:Deadbands:Fy"),
            fz: ReadNullableDouble(config, "Control:Limiter:Deadbands:Fz"),
            tx: ReadNullableDouble(config, "Control:Limiter:Deadbands:Tx"),
            ty: ReadNullableDouble(config, "Control:Limiter:Deadbands:Ty"),
            tz: ReadNullableDouble(config, "Control:Limiter:Deadbands:Tz")
        );

        limiter.SetTurnAssist(
            enabled: ReadNullableBool(config, "Control:Limiter:TurnAssist:Enabled"),
            minFxWhenTurning: ReadNullableDouble(config, "Control:Limiter:TurnAssist:MinFxWhenTurning"),
            turnTzAbsThreshold: ReadNullableDouble(config, "Control:Limiter:TurnAssist:TurnTzAbsThreshold")
        );

        Console.WriteLine($"[CFG] Limiter → FxRate={limiter.ThrottleRatePerSec:F2} TzRate={limiter.RudderRatePerSec:F2}");

        // ---------------------------------------------------------------------
        // FRAME SOURCE
        // ---------------------------------------------------------------------
        IFrameSource frameSource;
        TcpJsonFrameSource? tcpFrameSource = null;
        ITwinPublisher? twinPublisher = null;

        var sourceType = config["SensorSource:Type"] ?? "TcpJson";

        bool preferExternalCfg = !bool.TryParse(config["SensorSource:PreferExternal"], out var pex) || pex;

        bool preferExternal = preferExternalCfg;
        if (simMode && !allowExternalPoseOverrideInSim)
        {
            preferExternal = false;
            Console.WriteLine("[CFG] PreferExternal → Sim/Hybrid mod: DISABLED (Simulation:AllowExternalPoseOverride=false).");
        }
        else
        {
            Console.WriteLine($"[CFG] PreferExternal → {preferExternal} (cfg={preferExternalCfg}, simMode={simMode}, allowInSim={allowExternalPoseOverrideInSim})");
        }

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

            bool twinEnabled = bool.TryParse(config["Twin:Enabled"], out var twinEn) && twinEn;
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

        // ---------------------------------------------------------------------
        // COMMAND SERVER + AI
        // ---------------------------------------------------------------------
        string cmdHost = config["Control:CommandServer:Host"] ?? "127.0.0.1";
        int cmdPort = ReadInt(config, "Control:CommandServer:Port", 5060);

        int tickMs = (int)Math.Round(ReadDouble(config, "Runtime:TickMs", 100));
        if (tickMs < 10) tickMs = 10;

        Console.WriteLine($"[CFG] Tick → {tickMs} ms | DevMode={devMode} SimMode={simMode}");

        var analysisImplRef = analysisImpl;

        var tuner = new InlineTuner(
            getThrRate: () => limiter.ThrottleRatePerSec,
            setThrRate: v => limiter.SetRates(v, null),
            getRudRate: () => limiter.RudderRatePerSec,
            setRudRate: v => limiter.SetRates(null, v),

            getAhead: () => analysisImplRef.AheadDistanceM,
            setAhead: v => analysisImplRef.SetParameters(v, null),
            getFov: () => analysisImplRef.HalfFovDeg,
            setFov: v => analysisImplRef.SetParameters(null, v),

            getTickMs: () => tickMs,
            setTickMs: v =>
            {
                var val = Math.Max(10, v);
                tickMs = val;
                Console.WriteLine($"[TUNE] Tick set to {val} ms");
            },

            getTaskActive: () => tasks.CurrentTask is not null
        );

        var toolRegistry = new ToolRegistry();
        toolRegistry.RegisterRange(new IAiTool[]
        {
            new TimeNowTool(),
            new RuntimeStatusTool(),
            new TelemetrySnapshotTool()
        });

        var aiProvider = (config["AI:Provider"] ?? "fake").Trim();
        var llamaEndpoint = config["AI:Llama:Endpoint"] ?? config["AI:Endpoint"];

        IAiClient aiClient;
        if (aiProvider.Equals("llama", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(llamaEndpoint))
            {
                Console.WriteLine("[AI] UYARI: AI:Provider=llama seçilmiş ama endpoint boş. FakeAiClient fallback kullanılacak.");
                aiClient = new FakeAiClient();
            }
            else
            {
                var timeoutSeconds = ReadInt(config, "AI:Llama:TimeoutSeconds", 45);
                if (timeoutSeconds < 5) timeoutSeconds = 5;

                var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                };

                aiClient = new LlamaJsonClient(httpClient, llamaEndpoint);
                Console.WriteLine($"[AI] Provider=LLaMA Endpoint={llamaEndpoint} Timeout={timeoutSeconds}s");
            }
        }
        else
        {
            aiClient = new FakeAiClient();
            Console.WriteLine("[AI] Provider=FakeAiClient");
        }

        Console.WriteLine($"[AI] Tools → {string.Join(", ", toolRegistry.GetAllToolNames())}");

        var aiGateway = new AiGateway(aiClient, toolRegistry);

        var cmdSrv = new CommandServer(
            cmdHost,
            cmdPort,
            tasks,
            tuner,
            actuatorManager,
            ai: aiGateway
        );

        _ = cmdSrv.StartAsync(cts.Token);

        Console.WriteLine($"Hydronom runtime started. Frames on {sourceType}, Commands on {cmdHost}:{cmdPort}. Ctrl+C → stop.");

        // ---------------------------------------------------------------------
        // OBSTACLE POLICY
        // ---------------------------------------------------------------------
        bool invertRudder = bool.TryParse(config["Control:InvertRudder"], out var ir) && ir;

        double manualMaxFx = ReadDouble(config, "Control:Manual:MaxFxN", 24.0);
        double manualMaxFy = ReadDouble(config, "Control:Manual:MaxFyN", 12.0);
        double manualMaxFz = ReadDouble(config, "Control:Manual:MaxFzN", 35.0);
        double manualMaxTx = ReadDouble(config, "Control:Manual:MaxTxNm", 5.0);
        double manualMaxTy = ReadDouble(config, "Control:Manual:MaxTyNm", 7.0);
        double manualMaxTz = ReadDouble(config, "Control:Manual:MaxTzNm", 8.0);

        int heartbeatTimeoutMs = ReadInt(config, "Control:HeartbeatTimeoutMs", 1500);
        int manualCommandTimeoutMs = ReadInt(config, "Control:ManualCommandTimeoutMs", 1000);

        long tickIndex = 0;
        long lastExternalLogTick = 0;
        bool loggedSyntheticStateNotice = false;
        bool estopTaskCleared = false;
        string? lastTaskSignature = null;

        long periodTicks = Math.Max(1L, (long)Math.Round(Stopwatch.Frequency * (tickMs / 1000.0)));
        long nextLoopTicks = Stopwatch.GetTimestamp();

        // ---------------------------------------------------------------------
        // MAIN LOOP
        // ---------------------------------------------------------------------
        try
        {
            while (!cts.IsCancellationRequested)
            {
                long loopStartTicks = Stopwatch.GetTimestamp();

                if (nextLoopTicks == 0)
                    nextLoopTicks = loopStartTicks + periodTicks;

                double dtMeasured;
                if (tickIndex == 0)
                {
                    dtMeasured = tickMs / 1000.0;
                }
                else
                {
                    dtMeasured = (loopStartTicks - (nextLoopTicks - periodTicks)) / (double)Stopwatch.Frequency;
                    if (dtMeasured <= 1e-4 || dtMeasured > 1.0)
                        dtMeasured = tickMs / 1000.0;
                }

                // -----------------------------------------------------------------
                // 0) En son frame
                // -----------------------------------------------------------------
                FusedFrame? latestFrame;
                bool hasFreshFrame = frameSource.TryGetLatestFrame(out latestFrame) && latestFrame is not null;

                // -----------------------------------------------------------------
                // 1) External pose override
                // -----------------------------------------------------------------
                bool externalApplied = false;
                if (!simMode || allowExternalPoseOverrideInSim)
                {
                    if (frameSource is IExternalPoseProvider extProv &&
                        preferExternal &&
                        extProv.TryGetLatestExternal(out var extPose) &&
                        extPose.AgeMs <= extProv.FreshMs)
                    {
                        double newX = extPose.X;
                        double newY = extPose.Y;
                        double newYawDeg = extPose.HeadingDeg;

                        Vec3 linearVelocityToApply = state.LinearVelocity;
                        Vec3 angularVelocityToApply = state.AngularVelocity;

                        if (hasPrevExternalPose)
                        {
                            double extDt = (DateTime.UtcNow - prevExternalUtc).TotalSeconds;
                            if (extDt > 1e-4 && extDt < 1.0)
                            {
                                double dxExt = newX - prevExternalX;
                                double dyExt = newY - prevExternalY;
                                double distExt = Math.Sqrt(dxExt * dxExt + dyExt * dyExt);

                                double dyawExt = NormalizeAngleDeg(newYawDeg - prevExternalYawDeg);

                                bool teleported =
                                    distExt > externalTeleportDistanceM ||
                                    Math.Abs(dyawExt) > externalTeleportYawDeg;

                                if (teleported && resetVelOnExternalTeleport)
                                {
                                    linearVelocityToApply = new Vec3(0.0, 0.0, state.LinearVelocity.Z);
                                    angularVelocityToApply = new Vec3(
                                        state.AngularVelocity.X,
                                        state.AngularVelocity.Y,
                                        0.0
                                    );
                                }
                                else
                                {
                                    double vxEst = dxExt / extDt;
                                    double vyEst = dyExt / extDt;
                                    double yawRateEstDeg = dyawExt / extDt;

                                    linearVelocityToApply = new Vec3(
                                        Lerp(state.LinearVelocity.X, vxEst, externalVelBlend),
                                        Lerp(state.LinearVelocity.Y, vyEst, externalVelBlend),
                                        state.LinearVelocity.Z
                                    );

                                    angularVelocityToApply = new Vec3(
                                        state.AngularVelocity.X,
                                        state.AngularVelocity.Y,
                                        Lerp(state.AngularVelocity.Z, yawRateEstDeg, externalYawRateBlend)
                                    );
                                }
                            }
                        }

                        state = state.WithExternalPose(
                            x: newX,
                            y: newY,
                            z: state.Position.Z,
                            yawDeg: newYawDeg,
                            rollDeg: state.Orientation.RollDeg,
                            pitchDeg: state.Orientation.PitchDeg,
                            linearVelocity: linearVelocityToApply,
                            angularVelocity: angularVelocityToApply
                        );

                        prevExternalX = newX;
                        prevExternalY = newY;
                        prevExternalYawDeg = newYawDeg;
                        prevExternalUtc = DateTime.UtcNow;
                        hasPrevExternalPose = true;

                        externalApplied = true;
                    }
                }
                else
                {
                    if (preferExternalCfg && (tickIndex - lastExternalLogTick) >= 50)
                    {
                        Console.WriteLine("[SRC] ExternalPose override blocked (Sim/Hybrid). Set Simulation:AllowExternalPoseOverride=true to enable.");
                        lastExternalLogTick = tickIndex;
                    }
                }

                // -----------------------------------------------------------------
                // 2) Hedefin 2D izdüşümü
                // -----------------------------------------------------------------
                Vec2? target2D = null;
                if (tasks.CurrentTask?.Target is Vec3 tg3Target)
                    target2D = new Vec2(tg3Target.X, tg3Target.Y);

                // -----------------------------------------------------------------
                // 3) Kullanılacak frame
                // -----------------------------------------------------------------
                FusedFrame frameToUse;

                if (hasFreshFrame && latestFrame is not null)
                {
                    var obstaclesFromPython = latestFrame.Obstacles is not null
                        ? new List<Obstacle>(latestFrame.Obstacles)
                        : new List<Obstacle>();

                    frameToUse = new FusedFrame(
                        TimestampUtc: latestFrame.TimestampUtc,
                        Position: new Vec2(state.Position.X, state.Position.Y),
                        HeadingDeg: state.Orientation.YawDeg,
                        Obstacles: obstaclesFromPython,
                        Target: target2D
                    );

                    if (devMode && logVerbose)
                    {
                        string tstr = tasks.CurrentTask?.Target is Vec3 tg3Log
                            ? $"({tg3Log.X:F1},{tg3Log.Y:F1},{tg3Log.Z:F1})"
                            : "none";

                        var obsCount = obstaclesFromPython.Count;
                        Console.WriteLine($"[SRC] fresh frame: obs={obsCount}, target={tstr}, extApplied={externalApplied}");
                    }
                }
                else
                {
                    frameToUse = new FusedFrame(
                        TimestampUtc: DateTime.UtcNow,
                        Position: new Vec2(state.Position.X, state.Position.Y),
                        HeadingDeg: state.Orientation.YawDeg,
                        Obstacles: new List<Obstacle>(),
                        Target: target2D
                    );

                    if (devMode && logVerbose)
                    {
                        Console.WriteLine("[SRC] no fresh frame: obstacle source empty (runtime fallback disabled)");
                    }
                }

                var insights = analysis.Analyze(frameToUse);

                // -----------------------------------------------------------------
                // 4) Mode / decision selection
                // -----------------------------------------------------------------
                var nowUtc = DateTime.UtcNow;
                var heartbeatAgeMs = (nowUtc - cmdSrv.LastHeartbeatUtc).TotalMilliseconds;
                var manualAgeMs = (nowUtc - cmdSrv.LastManualCommandUtc).TotalMilliseconds;

                bool heartbeatFresh = heartbeatAgeMs <= heartbeatTimeoutMs;
                bool manualFresh = manualAgeMs <= manualCommandTimeoutMs;

                DecisionCommand cmdDesired;
                string controlMode;

                if (cmdSrv.IsEmergencyStop)
                {
                    if (!estopTaskCleared)
                    {
                        tasks.ClearTask();
                        estopTaskCleared = true;
                    }

                    cmdDesired = DecisionCommand.Zero;
                    controlMode = "ESTOP";
                }
                else if (cmdSrv.IsManualMode)
                {
                    estopTaskCleared = false;

                    if (cmdSrv.IsArmed && heartbeatFresh && manualFresh)
                    {
                        var md = cmdSrv.CurrentManualDrive;

                        cmdDesired = new DecisionCommand(
                            fx: md.Surge * manualMaxFx,
                            fy: md.Sway * manualMaxFy,
                            fz: md.Heave * manualMaxFz,
                            tx: md.Roll * manualMaxTx,
                            ty: md.Pitch * manualMaxTy,
                            tz: md.Yaw * manualMaxTz
                        );

                        controlMode = "MANUAL";
                    }
                    else
                    {
                        cmdDesired = DecisionCommand.Zero;
                        controlMode = cmdSrv.IsArmed ? "MANUAL-HOLD" : "DISARMED";
                    }
                }
                else
                {
                    estopTaskCleared = false;

                    tasks.Update(insights, state);

                    if (cmdSrv.IsArmed)
                    {
                        cmdDesired = decision.Decide(insights, tasks.CurrentTask, state, dtMeasured);
                        controlMode = "AUTO";
                    }
                    else
                    {
                        cmdDesired = DecisionCommand.Zero;
                        controlMode = "DISARMED";
                    }
                }

                if (invertRudder)
                    cmdDesired = cmdDesired with { Tz = -cmdDesired.Tz };

                // -----------------------------------------------------------------
                // 5) Telemetry / debug values
                // -----------------------------------------------------------------
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

                // -----------------------------------------------------------------
                // 5.5) Görev değişim logları
                // -----------------------------------------------------------------
                var taskSignature = BuildTaskSignature(tasks.CurrentTask);
                if (!string.Equals(lastTaskSignature, taskSignature, StringComparison.Ordinal))
                {
                    LogTaskState(tasks.CurrentTask);
                    lastTaskSignature = taskSignature;
                }

                var taskInfoInline = DescribeTaskInline(tasks.CurrentTask);

                // -----------------------------------------------------------------
                // 6) Limit
                // -----------------------------------------------------------------
                var (cmd, limFlags) = limiter.Limit(cmdDesired, dtMeasured);

                // -----------------------------------------------------------------
                // 7) Log (seyrek)
                // -----------------------------------------------------------------
                bool emitLoopLog = logVerbose || (tickIndex % loopLogEvery == 0);

                if (emitLoopLog)
                {
                    if (logVerbose)
                    {
                        Console.WriteLine(
                            $"[{DateTime.UtcNow:O}] " +
                            $"mode={controlMode} " +
                            $"task={taskInfoInline} " +
                            $"pos=({state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}) " +
                            $"rpy=({state.Orientation.RollDeg:F1},{state.Orientation.PitchDeg:F1},{state.Orientation.YawDeg:F1}) " +
                            $"angVel=({state.AngularVelocity.X:F1},{state.AngularVelocity.Y:F1},{state.AngularVelocity.Z:F1}) " +
                            $"obsAhead={(insights.HasObstacleAhead ? "True" : "False")} " +
                            $"cmd(Fx={cmd.Fx:F2}, Fy={cmd.Fy:F2}, Fz={cmd.Fz:F2}, Tx={cmd.Tx:F2}, Ty={cmd.Ty:F2}, Tz={cmd.Tz:F2})"
                        );

                        Console.WriteLine(
                            $"[CTL] mode={controlMode} " +
                            $"task={taskInfoInline} " +
                            $"dist={(double.IsNaN(distToTarget) ? -1 : distToTarget):F1}m " +
                            $"dHead={(double.IsNaN(deltaHeadDeg) ? 0 : deltaHeadDeg):F1}° " +
                            $"pre(Fx={cmdDesired.Fx:F2},Fy={cmdDesired.Fy:F2},Fz={cmdDesired.Fz:F2},Tz={cmdDesired.Tz:F2}) -> " +
                            $"post(Fx={cmd.Fx:F2},Fy={cmd.Fy:F2},Fz={cmd.Fz:F2},Tz={cmd.Tz:F2}) lim={limFlags}"
                        );
                    }
                    else
                    {
                        Console.WriteLine(
                            $"[LOOP] mode={controlMode} " +
                            $"task={taskInfoInline} " +
                            $"pos=({state.Position.X:F2},{state.Position.Y:F2}) " +
                            $"yaw={state.Orientation.YawDeg:F1}° " +
                            $"yawRate={state.AngularVelocity.Z:F1}°/s " +
                            $"dist={(double.IsNaN(distToTarget) ? -1 : distToTarget):F1}m " +
                            $"dHead={(double.IsNaN(deltaHeadDeg) ? 0 : deltaHeadDeg):F1}° " +
                            $"Fx={cmd.Fx:F2} Fy={cmd.Fy:F2} Fz={cmd.Fz:F2} Tz={cmd.Tz:F2} " +
                            $"obsAhead={(insights.HasObstacleAhead ? "True" : "False")} lim={limFlags}"
                        );
                    }
                }

                // -----------------------------------------------------------------
                // 8) Actuation
                // -----------------------------------------------------------------
                actuatorBus.Apply(cmd);

                Vec3 forceBody = actuatorManager.VehicleState.LinearForce;
                Vec3 torqueBody = actuatorManager.VehicleState.AngularTorque;

                // -----------------------------------------------------------------
                // 9) Synthetic state / physics
                // -----------------------------------------------------------------
                bool shouldIntegrateSyntheticState = useSyntheticStateWhenNoExternal && !externalApplied;

                if (shouldIntegrateSyntheticState)
                {
                    if (!loggedSyntheticStateNotice)
                    {
                        Console.WriteLine("[STATE] Synthetic state integration aktif (karar/rota testi için iç fizik yürütülüyor).");
                        loggedSyntheticStateNotice = true;
                    }

                    var withForces = state.ClearForces();
                    withForces = withForces with
                    {
                        LinearForce = state.Orientation.BodyToWorld(forceBody),
                        AngularTorque = torqueBody
                    };

                    state = withForces.IntegrateMarine(
                        dt: dtMeasured,
                        mass: massKg,
                        inertia: inertia,
                        linearDragBody: new Vec3(linDragX, linDragY, linDragZ),
                        quadraticDragBody: new Vec3(quadDragX, quadDragY, quadDragZ),
                        angularLinearDragBody: new Vec3(angLinDragX, angLinDragY, angLinDragZ),
                        angularQuadraticDragBody: new Vec3(angQuadDragX, angQuadDragY, angQuadDragZ),
                        maxLinearSpeed: maxSyntheticLinearSpeed,
                        maxAngularSpeedDeg: maxSyntheticAngularSpeedDeg
                    );
                }

                // -----------------------------------------------------------------
                // 10) Native tick
                // -----------------------------------------------------------------
                if (enableNativeTick)
                {
                    NativeSensors.TickIfAvailable(
                        dtMeasured,
                        state,
                        cmd.Throttle01,
                        cmd.RudderNeg1To1
                    );
                }

                // -----------------------------------------------------------------
                // 10.5) Twin publish
                // -----------------------------------------------------------------
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

                // -----------------------------------------------------------------
                // 11) Extra verbose logs
                // -----------------------------------------------------------------
                if (logVerbose)
                {
                    Console.WriteLine(
                        $"[STATE] pos=({state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}) " +
                        $"rpy=({state.Orientation.RollDeg:F1},{state.Orientation.PitchDeg:F1},{state.Orientation.YawDeg:F1}) " +
                        $"vel=({state.LinearVelocity.X:F2},{state.LinearVelocity.Y:F2},{state.LinearVelocity.Z:F2}) " +
                        $"angVel=({state.AngularVelocity.X:F1},{state.AngularVelocity.Y:F1},{state.AngularVelocity.Z:F1})"
                    );

                    Console.WriteLine($"[DBG] mode={controlMode} task={taskInfoInline}");
                }

                // -----------------------------------------------------------------
                // 12) Feedback
                // -----------------------------------------------------------------
                feedback.Record(new FeedbackRecord(
                    timestampUtc: DateTime.UtcNow,
                    frame: frameToUse,
                    insights: insights,
                    command: cmd,
                    state: state,
                    forceBody: forceBody,
                    torqueBody: torqueBody
                ));

                // -----------------------------------------------------------------
                // 13) Heartbeat
                // -----------------------------------------------------------------
                tickIndex++;

                if (tickIndex % heartbeatEvery == 0)
                {
                    double frameAgeMs = double.NaN;
                    if (frameSource.TryGetLatestFrame(out var lastFrame) && lastFrame is not null)
                        frameAgeMs = (DateTime.UtcNow - lastFrame.TimestampUtc).TotalMilliseconds;

                    Console.WriteLine(
                        $"[HEARTBEAT] tick={tickIndex} mode={controlMode} task={taskInfoInline} " +
                        $"dtTarget={tickMs}ms dtMeasured={dtMeasured * 1000.0:F0}ms " +
                        $"pos=({state.Position.X:F2},{state.Position.Y:F2}) yaw={state.Orientation.YawDeg:F1}° yawRate={state.AngularVelocity.Z:F1}°/s " +
                        $"frameAgeMs={(double.IsNaN(frameAgeMs) ? -1 : frameAgeMs):F0} " +
                        $"armed={cmdSrv.IsArmed} estop={cmdSrv.IsEmergencyStop} manual={cmdSrv.IsManualMode} " +
                        $"serial={(actuatorManager.IsSerialOpen ? "open" : "closed")} lim={limFlags}"
                    );
                }

                // -----------------------------------------------------------------
                // 14) Deadline advance + hybrid wait
                // -----------------------------------------------------------------
                nextLoopTicks += periodTicks;

                long nowTicks = Stopwatch.GetTimestamp();
                if (nextLoopTicks <= nowTicks)
                {
                    long lagTicks = nowTicks - nextLoopTicks;
                    long missedPeriods = Math.Max(1L, lagTicks / periodTicks + 1L);
                    nextLoopTicks += missedPeriods * periodTicks;

                    int elapsedMsNow = (int)((nowTicks - loopStartTicks) * 1000.0 / Stopwatch.Frequency);
                    if (elapsedMsNow > tickMs + 5)
                    {
                        Console.WriteLine($"[WARN] Loop overrun: elapsed={elapsedMsNow}ms (tick={tickMs}ms)");
                    }
                }

                await HybridWaitUntilAsync(nextLoopTicks, cts.Token);
            }
        }
        finally
        {
            NativeSensors.TryShutdown();

            if (frameSource is IAsyncDisposable disp)
                await disp.DisposeAsync();

            if (pythonProc is not null)
            {
                try
                {
                    if (!pythonProc.HasExited)
                    {
                        Console.WriteLine("[PY] python main.py sonlandırılıyor...");
#if NET6_0_OR_GREATER
                        pythonProc.Kill(entireProcessTree: true);
#else
                        pythonProc.Kill();
#endif
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PY] python süreci sonlandırılırken hata: {ex.Message}");
                }
                finally
                {
                    pythonProc.Dispose();
                }
            }

            Console.WriteLine("Hydronom runtime stopped.");
        }
    }

    // -------------------------------------------------------------------------
    // Sensörsüz test için Null FrameSource
    // -------------------------------------------------------------------------
    private sealed class NullFrameSource : IFrameSource
    {
        public bool TryGetLatestFrame(out FusedFrame? frame)
        {
            frame = null;
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Yardımcılar
    // -------------------------------------------------------------------------
    private static double ReadDouble(IConfiguration config, string key, double fallback)
    {
        var s = config[key];
        if (string.IsNullOrWhiteSpace(s)) return fallback;

        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var v))
            return v;

        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out v))
            return v;

        return fallback;
    }

    private static double? ReadNullableDouble(IConfiguration config, string key)
    {
        var s = config[key];
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var v))
            return v;

        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out v))
            return v;

        return null;
    }

    private static bool? ReadNullableBool(IConfiguration config, string key)
    {
        var s = config[key];
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (bool.TryParse(s, out var v)) return v;
        return null;
    }

    private static int ReadInt(IConfiguration config, string key, int fallback)
    {
        var s = config[key];
        if (string.IsNullOrWhiteSpace(s)) return fallback;

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return v;

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out v))
            return v;

        return fallback;
    }

    private static double NormalizeAngleDeg(double deg)
    {
        while (deg > 180) deg -= 360;
        while (deg < -180) deg += 360;
        return deg;
    }

    private static double Lerp(double a, double b, double t)
    {
        if (t <= 0.0) return a;
        if (t >= 1.0) return b;
        return a + (b - a) * t;
    }

    private static async Task HybridWaitUntilAsync(long targetTicks, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            long now = Stopwatch.GetTimestamp();
            long remainingTicks = targetTicks - now;
            if (remainingTicks <= 0)
                return;

            double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;

            if (remainingMs > 2.0)
            {
                int delayMs = Math.Max(1, (int)Math.Floor(remainingMs - 1.0));
                await Task.Delay(delayMs, ct);
            }
            else if (remainingMs > 0.25)
            {
                Thread.SpinWait(200);
            }
            else
            {
                Thread.SpinWait(50);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Görev log yardımcıları
    // -------------------------------------------------------------------------
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

    private static string? TryReadStringProperty(object obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop is null) continue;

                var value = prop.GetValue(obj);
                var text = value?.ToString()?.Trim();

                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            catch
            {
                // Yut
            }
        }

        return null;
    }

    private static string? TryReadTargetString(object obj)
    {
        try
        {
            var prop = obj.GetType().GetProperty("Target", BindingFlags.Instance | BindingFlags.Public);
            if (prop is null)
                return null;

            var value = prop.GetValue(obj);
            if (value is null)
                return null;

            if (value is Vec3 v3)
                return $"({v3.X:F1},{v3.Y:F1},{v3.Z:F1})";

            return value.ToString();
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Thruster config loader
    // -------------------------------------------------------------------------
    private static ThrusterDesc[] TryLoadThrustersFromChannelProfiles(IConfiguration config)
    {
        var baseDir = AppContext.BaseDirectory;
        var configsDir = Path.Combine(baseDir, "Configs");

        try
        {
            var discoveredPath = Path.Combine(configsDir, "actuators.discovered.json");
            if (File.Exists(discoveredPath))
            {
                var json = File.ReadAllText(discoveredPath);
                var arr = System.Text.Json.JsonSerializer.Deserialize<ThrusterDesc[]>(json);
                if (arr != null && arr.Length > 0)
                {
                    Console.WriteLine($"[CFG] Thrusters loaded from Configs/actuators.discovered.json ({arr.Length} ch).");
                    return arr;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to load Configs/actuators.discovered.json: {ex.Message}");
        }

        try
        {
            var geomPath = Path.Combine(configsDir, "thrusters.geometry.json");
            if (File.Exists(geomPath))
            {
                var json = File.ReadAllText(geomPath);
                var arr = System.Text.Json.JsonSerializer.Deserialize<ThrusterDesc[]>(json);
                if (arr != null && arr.Length > 0)
                {
                    Console.WriteLine($"[CFG] Thrusters loaded from Configs/thrusters.geometry.json ({arr.Length} ch).");
                    return arr;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to load Configs/thrusters.geometry.json: {ex.Message}");
        }

        try
        {
            var outDir = config["AutoDiscovery:OutputDirectory"] ?? "Configs/AutoDiscovery";
            var file = config["AutoDiscovery:ChannelProfileFile"] ?? "channel_profiles.json";
            var fullPath = Path.Combine(baseDir, outDir, file);

            if (!File.Exists(fullPath))
                return Array.Empty<ThrusterDesc>();

            Console.WriteLine("[CFG] Skipping AutoDiscovery: ChannelProfileSet type not defined here.");
            return Array.Empty<ThrusterDesc>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to load AutoDiscovery ChannelProfileSet: {ex.Message}");
            return Array.Empty<ThrusterDesc>();
        }
    }

    // -------------------------------------------------------------------------
    // Motors → Thrusters geri uyum
    // -------------------------------------------------------------------------
    private static ThrusterDesc[] MapMotorsToThrusters(MotorDesc[] motors)
    {
        double halfX = 0.5, halfY = 0.5;

        Vec3 PosForId(string id, int index, int count)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                var u = id.Trim().ToUpperInvariant();
                if (u == "FL") return new Vec3(-halfX, +halfY, 0);
                if (u == "FR") return new Vec3(+halfX, +halfY, 0);
                if (u == "RL") return new Vec3(-halfX, -halfY, 0);
                if (u == "RR") return new Vec3(+halfX, -halfY, 0);
            }

            bool top = index < (count + 1) / 2;
            double y = top ? +halfY : -halfY;
            int col = top ? index : index - (count + 1) / 2;
            double x = (col % 2 == 0) ? -halfX : +halfX;
            return new Vec3(x, y, 0);
        }

        return motors.Select((m, i) =>
        {
            var pos = PosForId(m.Id ?? "", i, motors.Length);
            var dir = new Vec3(1, 0, 0);
            return new ThrusterDesc(m.Id ?? $"CH{m.Channel}", m.Channel, pos, dir, Reversed: false);
        }).ToArray();
    }

    // -------------------------------------------------------------------------
    // Python çalışma klasörü çözümleyici
    // -------------------------------------------------------------------------
    private static string? ResolvePythonWorkDir(IConfiguration config)
    {
        var envDir = Environment.GetEnvironmentVariable("HYDRONOM_PYTHON_DIR");
        if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir))
        {
            Console.WriteLine($"[PY] HYDRONOM_PYTHON_DIR ile python klasörü bulundu: {envDir}");
            return envDir;
        }

        var cfgDir = config["Python:WorkingDir"];
        if (!string.IsNullOrWhiteSpace(cfgDir) && Directory.Exists(cfgDir))
        {
            Console.WriteLine($"[PY] Python:WorkingDir ile python klasörü bulundu: {cfgDir}");
            return cfgDir;
        }

        var exeDir = AppContext.BaseDirectory;
        var cand1 = Path.Combine(exeDir, "python");
        var cand2 = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "..", "python"));

        if (Directory.Exists(cand1))
        {
            Console.WriteLine($"[PY] python klasörü (cand1) bulundu: {cand1}");
            return cand1;
        }

        if (Directory.Exists(cand2))
        {
            Console.WriteLine($"[PY] python klasörü (cand2) bulundu: {cand2}");
            return cand2;
        }

        Console.WriteLine("[PY] python çalışma klasörü bulunamadı.");
        Console.WriteLine($"[PY]  - denenen 1: {cand1}");
        Console.WriteLine($"[PY]  - denenen 2: {cand2}");
        Console.WriteLine("[PY] HYDRONOM_PYTHON_DIR veya Python:WorkingDir ile yol verebilirsin.");
        return null;
    }

    // -------------------------------------------------------------------------
    // Python main.py otomatik başlatıcı
    // -------------------------------------------------------------------------
    private static Process? MaybeStartPythonSensorHub(IConfiguration config)
    {
        var autoStr = config["Python:AutoStart"];
        bool autoStart = !bool.TryParse(autoStr, out var b) || b;
        if (!autoStart)
        {
            Console.WriteLine("[PY] Python sensor hub auto-start devre dışı (Python:AutoStart=false).");
            return null;
        }

        var exe = config["Python:Executable"];
        if (string.IsNullOrWhiteSpace(exe))
            exe = OperatingSystem.IsWindows() ? "py" : "python3";

        var script = config["Python:Script"] ?? "main.py";
        var workDir = ResolvePythonWorkDir(config);
        if (workDir is null)
            return null;

        var scriptPath = Path.Combine(workDir, script);
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"[PY] Python script bulunamadı: {scriptPath}");
            return null;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = script,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Environment["HYDRONOM_MODE"] = "runtime";
        psi.Environment["HYDRONOM_TCP_HOST"] = "127.0.0.1";
        psi.Environment["HYDRONOM_TCP_PORT"] = config["SensorSource:TcpJson:Port"] ?? "5055";
        psi.Environment["PYTHONUTF8"] = "1";

        try
        {
            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var line = e.Data;
                    if (line.Length > 400)
                        line = line.Substring(0, 400) + "...";
                    Console.WriteLine("[PY] " + line);
                }
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var line = e.Data;
                    if (line.Length > 400)
                        line = line.Substring(0, 400) + "...";
                    Console.WriteLine("[PY-ERR] " + line);
                }
            };

            if (!proc.Start())
            {
                Console.WriteLine("[PY] python main.py başlatılamadı (Start=false).");
                return null;
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            Console.WriteLine($"[PY] Python sensor hub başlatıldı (PID={proc.Id}) dir={workDir}");
            return proc;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PY] python main.py başlatılırken hata: {ex.Message}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Native sensor bridge
    // -------------------------------------------------------------------------
    private static class NativeSensors
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct HsFusedStateNative
        {
            public double pos_x;
            public double pos_y;
            public double pos_z;

            public double vel_x;
            public double vel_y;
            public double vel_z;

            public double yaw_deg;
            public double pitch_deg;
            public double roll_deg;

            public int has_fix;
            public double quality;
            public ulong timestamp;
        }

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_init();

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_shutdown();

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_tick(
            double dt_seconds,
            ref HsFusedStateNative state_input,
            double cmd_throttle,
            double cmd_rudder
        );

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_get_fused_state(out HsFusedStateNative out_state);

        private static bool _available;

        internal static void TryInit()
        {
            try
            {
                hs_init();
                _available = true;
                Console.WriteLine("[NATIVE] hydro_sensors çekirdeği yüklendi (C sensör mimarisi aktif).");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[NATIVE] hydro_sensors.dll bulunamadı, native sensör çekirdeği pasif kalacak.");
                _available = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_init başarısız: {ex.Message}");
                _available = false;
            }
        }

        internal static void TryShutdown()
        {
            if (!_available) return;

            try
            {
                hs_shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_shutdown hata: {ex.Message}");
            }
        }

        internal static void TickIfAvailable(double dt, VehicleState state, double cmdThrottle, double cmdRudder)
        {
            if (!_available) return;

            var nativeState = new HsFusedStateNative
            {
                pos_x = state.Position.X,
                pos_y = state.Position.Y,
                pos_z = state.Position.Z,
                vel_x = state.LinearVelocity.X,
                vel_y = state.LinearVelocity.Y,
                vel_z = state.LinearVelocity.Z,
                yaw_deg = state.Orientation.YawDeg,
                pitch_deg = state.Orientation.PitchDeg,
                roll_deg = state.Orientation.RollDeg,
                has_fix = 1,
                quality = 1.0,
                timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            try
            {
                hs_tick(dt, ref nativeState, cmdThrottle, cmdRudder);

                HsFusedStateNative fused;
                hs_get_fused_state(out fused);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_tick çağrısında hata: {ex.Message}");
            }
        }
    }
}