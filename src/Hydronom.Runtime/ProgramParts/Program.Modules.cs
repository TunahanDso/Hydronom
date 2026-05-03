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
    /// Runtime analiz modÃ¼lÃ¼nÃ¼ oluÅŸturur.
    /// Åimdilik varsayÄ±lan olarak AdvancedAnalysis kullanÄ±lÄ±r.
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
            $"[CFG] Analysis â†’ AdvancedAnalysis Ahead={analysis.AheadDistanceM:F1} m, " +
            $"HalfFov={analysis.HalfFovDeg:F0}Â°, Sectors={analysis.SectorCount}"
        );

        return analysis;
    }

    /// <summary>
    /// Karar modÃ¼lÃ¼nÃ¼ oluÅŸturur.
    /// </summary>
    private static IDecisionModule CreateDecisionModule(IConfiguration config)
    {
        var type = ReadString(config, "Decision:Type", "AdvancedDecision");

        if (!type.Equals("AdvancedDecision", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"[CFG] Decision:Type={type} desteklenmiyor, AdvancedDecision kullanÄ±lacak.");

        Console.WriteLine("[CFG] Decision â†’ AdvancedDecision");
        return new AdvancedDecision();
    }

    /// <summary>
    /// GÃ¶rev yÃ¶neticisini oluÅŸturur.
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
            "[CFG] TaskManager â†’ AdvancedTaskManager " +
            $"arrive={manager.LastReport.EffectiveArrivalThresholdM:F2}m"
        );

        return manager;
    }

    /// <summary>
    /// Feedback recorder oluÅŸturur.
    /// </summary>
    private static IFeedbackRecorder CreateFeedbackRecorder(IConfiguration config)
    {
        var type = ReadString(config, "Feedback:Type", "Console");

        if (!type.Equals("Console", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"[CFG] Feedback:Type={type} desteklenmiyor, ConsoleFeedbackRecorder kullanÄ±lacak.");

        Console.WriteLine("[CFG] Feedback â†’ ConsoleFeedbackRecorder");
        return new ConsoleFeedbackRecorder();
    }

    /// <summary>
    /// Motor controller oluÅŸturur.
    /// Åimdilik mock controller kullanÄ±lÄ±r.
    /// </summary>
    private static IMotorController CreateMotorController(IConfiguration config)
    {
        var type = ReadString(config, "MotorController:Type", "Mock");

        if (!type.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"[CFG] MotorController:Type={type} desteklenmiyor, MockMotorController kullanÄ±lacak.");

        Console.WriteLine("[CFG] MotorController â†’ MockMotorController");
        return new MockMotorController();
    }

    /// <summary>
    /// ActuatorManager ve ActuatorBus oluÅŸturur.
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

        Console.WriteLine($"[CFG] Actuator Serial â†’ {(serialPortName ?? "<disabled>")} @ {serialBaud}");

        var thrusterDescs = LoadThrusterDescriptions(config);
        var actuatorManager = new ActuatorManager(thrusterDescs, motors, serialPortName, serialBaud);
        var actuatorBus = new ActuatorBus(new IActuator[] { actuatorManager });

        Console.WriteLine($"[CFG] Actuator Authority â†’ {actuatorManager.AuthorityProfile}");

        return (actuatorManager, actuatorBus);
    }

    /// <summary>
    /// SafetyLimiter oluÅŸturur ve config'teki eksen bazlÄ± ayarlarÄ± uygular.
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

        Console.WriteLine($"[CFG] Limiter â†’ FxRate={limiter.ThrottleRatePerSec:F2} TzRate={limiter.RudderRatePerSec:F2}");

        return limiter;
    }

    /// <summary>
    /// Inline tuner oluÅŸturur.
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
    /// AI tool registry oluÅŸturur.
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

        Console.WriteLine($"[AI] Tools â†’ {string.Join(", ", toolRegistry.GetAllToolNames())}");

        return toolRegistry;
    }

    /// <summary>
    /// AI client oluÅŸturur.
    /// Config'e gÃ¶re LLaMA veya fake client seÃ§er.
    /// </summary>
    private static IAiClient CreateAiClient(IConfiguration config)
    {
        var aiProvider = (config["AI:Provider"] ?? "fake").Trim();
        var llamaEndpoint = config["AI:Llama:Endpoint"] ?? config["AI:Endpoint"];

        if (aiProvider.Equals("llama", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(llamaEndpoint))
            {
                Console.WriteLine("[AI] UYARI: AI:Provider=llama seÃ§ilmiÅŸ ama endpoint boÅŸ. FakeAiClient fallback kullanÄ±lacak.");
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
    /// AI gateway oluÅŸturur.
    /// </summary>
    private static AiGateway CreateAiGateway(IConfiguration config)
    {
        var registry = CreateToolRegistry();
        var client = CreateAiClient(config);

        return new AiGateway(client, registry);
    }
}
