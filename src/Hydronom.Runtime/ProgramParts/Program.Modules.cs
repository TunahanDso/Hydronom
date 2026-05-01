using System;
using System.Net.Http;
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
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Runtime analiz modülünü oluşturur.
    /// Şimdilik varsayılan olarak AdvancedAnalysis kullanılır.
    /// </summary>
    private static AdvancedAnalysis CreateAnalysisModule(IConfiguration config)
    {
        var aheadM = ReadDouble(config, "Analysis:AheadDistanceM", 12.0);
        var fovDeg = ReadDouble(config, "Analysis:HalfFovDeg", 45.0);

        var analysis = new AdvancedAnalysis(
            aheadDistanceM: aheadM,
            halfFovDeg: fovDeg,
            sectorCount: ReadInt(config, "Analysis:Advanced:SectorCount", 31),
            safetyMarginM: ReadDouble(config, "Analysis:Advanced:SafetyMarginM", 0.80),
            dangerDistanceM: ReadDouble(config, "Analysis:Advanced:DangerDistanceM", 4.0),
            sideWindowDeg: ReadDouble(config, "Analysis:Advanced:SideWindowDeg", 70.0),
            frontWeight: ReadDouble(config, "Analysis:Advanced:FrontWeight", 1.35),
            sizeWeight: ReadDouble(config, "Analysis:Advanced:SizeWeight", 0.90),
            centerBiasWeight: ReadDouble(config, "Analysis:Advanced:CenterBiasWeight", 0.10),
            frontCriticalRiskThreshold: ReadDouble(config, "Analysis:Advanced:FrontCriticalRiskThreshold", 1.15)
        );

        Console.WriteLine(
            $"[CFG] Analysis → AdvancedAnalysis Ahead={analysis.AheadDistanceM:F1} m, " +
            $"HalfFov={analysis.HalfFovDeg:F0}°, Sectors={analysis.SectorCount}"
        );

        return analysis;
    }

    /// <summary>
    /// Karar modülünü oluşturur.
    /// </summary>
    private static IDecisionModule CreateDecisionModule(IConfiguration config)
    {
        var type = ReadString(config, "Decision:Type", "AdvancedDecision");

        if (!type.Equals("AdvancedDecision", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"[CFG] Decision:Type={type} desteklenmiyor, AdvancedDecision kullanılacak.");

        Console.WriteLine("[CFG] Decision → AdvancedDecision");
        return new AdvancedDecision();
    }

    /// <summary>
    /// Görev yöneticisini oluşturur.
    /// </summary>
    private static AdvancedTaskManager CreateTaskManager(IConfiguration config)
    {
        var manager = new AdvancedTaskManager(
            arriveThresholdM: ReadDouble(config, "Task:ArriveThresholdM", 0.75),
            maxTaskDurationSeconds: ReadDouble(config, "Task:MaxTaskDurationSeconds", 600.0),
            maxNoProgressSeconds: ReadDouble(config, "Task:MaxNoProgressSeconds", 60.0),
            maxObstacleHoldSeconds: ReadDouble(config, "Task:MaxObstacleHoldSeconds", 120.0),
            minProgressDeltaM: ReadDouble(config, "Task:MinProgressDeltaM", 0.25),
            dynamicAcceptanceTauSeconds: ReadDouble(config, "Task:DynamicAcceptanceTauSeconds", 0.60),
            maxArrivalThresholdM: ReadDouble(config, "Task:MaxArrivalThresholdM", 3.00)
        );

        Console.WriteLine(
            "[CFG] TaskManager → AdvancedTaskManager " +
            $"arrive={manager.LastReport.EffectiveArrivalThresholdM:F2}m"
        );

        return manager;
    }

    /// <summary>
    /// Feedback recorder oluşturur.
    /// </summary>
    private static IFeedbackRecorder CreateFeedbackRecorder(IConfiguration config)
    {
        var type = ReadString(config, "Feedback:Type", "Console");

        if (!type.Equals("Console", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"[CFG] Feedback:Type={type} desteklenmiyor, ConsoleFeedbackRecorder kullanılacak.");

        Console.WriteLine("[CFG] Feedback → ConsoleFeedbackRecorder");
        return new ConsoleFeedbackRecorder();
    }

    /// <summary>
    /// Motor controller oluşturur.
    /// Şimdilik mock controller kullanılır.
    /// </summary>
    private static IMotorController CreateMotorController(IConfiguration config)
    {
        var type = ReadString(config, "MotorController:Type", "Mock");

        if (!type.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"[CFG] MotorController:Type={type} desteklenmiyor, MockMotorController kullanılacak.");

        Console.WriteLine("[CFG] MotorController → MockMotorController");
        return new MockMotorController();
    }

    /// <summary>
    /// ActuatorManager ve ActuatorBus oluşturur.
    /// </summary>
    private static (ActuatorManager Manager, ActuatorBus Bus) CreateActuatorSystem(
        IConfiguration config,
        RuntimeOptions runtime,
        IMotorController motors)
    {
        var serialPortName = config["Actuator:Serial:Port"];
        if (string.IsNullOrWhiteSpace(serialPortName))
            serialPortName = null;

        bool disableSerial = runtime.DevMode || runtime.SimMode;
        if (disableSerial)
            serialPortName = null;

        var serialBaud = ReadInt(config, "Actuator:Serial:Baud", 115200);

        Console.WriteLine($"[CFG] Actuator Serial → {(serialPortName ?? "<disabled>")} @ {serialBaud}");

        var thrusterDescs = LoadThrusterDescriptions(config);
        var actuatorManager = new ActuatorManager(thrusterDescs, motors, serialPortName, serialBaud);
        var actuatorBus = new ActuatorBus(new IActuator[] { actuatorManager });

        Console.WriteLine($"[CFG] Actuator Authority → {actuatorManager.AuthorityProfile}");

        return (actuatorManager, actuatorBus);
    }

    /// <summary>
    /// SafetyLimiter oluşturur ve config'teki eksen bazlı ayarları uygular.
    /// </summary>
    private static SafetyLimiter CreateSafetyLimiter(IConfiguration config)
    {
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

        limiter.SetAxisAbsoluteLimits(
            enabled: ReadBool(config, "Control:Limiter:AbsoluteLimits:Enabled", false),
            fx: ReadNullableDouble(config, "Control:Limiter:AbsoluteLimits:Fx"),
            fy: ReadNullableDouble(config, "Control:Limiter:AbsoluteLimits:Fy"),
            fz: ReadNullableDouble(config, "Control:Limiter:AbsoluteLimits:Fz"),
            tx: ReadNullableDouble(config, "Control:Limiter:AbsoluteLimits:Tx"),
            ty: ReadNullableDouble(config, "Control:Limiter:AbsoluteLimits:Ty"),
            tz: ReadNullableDouble(config, "Control:Limiter:AbsoluteLimits:Tz")
        );

        limiter.SetTurnAssist(
            enabled: ReadNullableBool(config, "Control:Limiter:TurnAssist:Enabled"),
            minFxWhenTurning: ReadNullableDouble(config, "Control:Limiter:TurnAssist:MinFxWhenTurning"),
            turnTzAbsThreshold: ReadNullableDouble(config, "Control:Limiter:TurnAssist:TurnTzAbsThreshold")
        );

        Console.WriteLine($"[CFG] Limiter → FxRate={limiter.ThrottleRatePerSec:F2} TzRate={limiter.RudderRatePerSec:F2}");

        return limiter;
    }

    /// <summary>
    /// Inline tuner oluşturur.
    /// </summary>
    private static InlineTuner CreateInlineTuner(
        AdvancedAnalysis analysis,
        SafetyLimiter limiter,
        ITaskManager tasks,
        Func<int> getTickMs,
        Action<int> setTickMs)
    {
        return new InlineTuner(
            getThrRate: () => limiter.ThrottleRatePerSec,
            setThrRate: v => limiter.SetRates(v, null),
            getRudRate: () => limiter.RudderRatePerSec,
            setRudRate: v => limiter.SetRates(null, v),

            getAhead: () => analysis.AheadDistanceM,
            setAhead: v => analysis.SetParameters(aheadDistanceM: v),
            getFov: () => analysis.HalfFovDeg,
            setFov: v => analysis.SetParameters(halfFovDeg: v),

            getTickMs: getTickMs,
            setTickMs: v =>
            {
                var val = Math.Max(10, v);
                setTickMs(val);
                Console.WriteLine($"[TUNE] Tick set to {val} ms");
            },

            getTaskActive: () => tasks.CurrentTask is not null
        );
    }

    /// <summary>
    /// AI tool registry oluşturur.
    /// </summary>
    private static ToolRegistry CreateToolRegistry()
    {
        var toolRegistry = new ToolRegistry();

        toolRegistry.RegisterRange(new IAiTool[]
        {
            new TimeNowTool(),
            new RuntimeStatusTool(),
            new TelemetrySnapshotTool()
        });

        Console.WriteLine($"[AI] Tools → {string.Join(", ", toolRegistry.GetAllToolNames())}");

        return toolRegistry;
    }

    /// <summary>
    /// AI client oluşturur.
    /// Config'e göre LLaMA veya fake client seçer.
    /// </summary>
    private static IAiClient CreateAiClient(IConfiguration config)
    {
        var aiProvider = (config["AI:Provider"] ?? "fake").Trim();
        var llamaEndpoint = config["AI:Llama:Endpoint"] ?? config["AI:Endpoint"];

        if (aiProvider.Equals("llama", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(llamaEndpoint))
            {
                Console.WriteLine("[AI] UYARI: AI:Provider=llama seçilmiş ama endpoint boş. FakeAiClient fallback kullanılacak.");
                return new FakeAiClient();
            }

            var timeoutSeconds = ReadInt(config, "AI:Llama:TimeoutSeconds", 45);
            if (timeoutSeconds < 5)
                timeoutSeconds = 5;

            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            Console.WriteLine($"[AI] Provider=LLaMA Endpoint={llamaEndpoint} Timeout={timeoutSeconds}s");
            return new LlamaJsonClient(httpClient, llamaEndpoint);
        }

        Console.WriteLine("[AI] Provider=FakeAiClient");
        return new FakeAiClient();
    }

    /// <summary>
    /// AI gateway oluşturur.
    /// </summary>
    private static AiGateway CreateAiGateway(IConfiguration config)
    {
        var registry = CreateToolRegistry();
        var client = CreateAiClient(config);

        return new AiGateway(client, registry);
    }
}