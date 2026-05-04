using Hydronom.Core.Scenarios.Judging;
using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.Scenarios.Reports;
using Hydronom.Core.Scenarios.Runtime;

namespace Hydronom.Runtime.Testing.Scenarios;

/// <summary>
/// Senaryo dosyalarını ve runtime araç durumunu kullanarak Digital Proving Ground
/// test koşusu çalıştıran yardımcı sınıftır.
/// </summary>
public sealed class RuntimeScenarioTestRunner
{
    private readonly IScenarioJudge _judge;

    public RuntimeScenarioTestRunner()
        : this(new DefaultScenarioJudge())
    {
    }

    public RuntimeScenarioTestRunner(IScenarioJudge judge)
    {
        _judge = judge ?? throw new ArgumentNullException(nameof(judge));
    }

    /// <summary>
    /// Tek tick/final değerlendirme için senaryo koşusu çalıştırır.
    /// Bu method şimdilik temel test runner'dır; ileride gerçek runtime loop, physics,
    /// sensor simulation ve telemetry timeline ile genişletilecektir.
    /// </summary>
    public ScenarioRunReport RunSingleEvaluation(
        ScenarioDefinition scenario,
        RuntimeScenarioVehicleState vehicleState,
        RuntimeScenarioTestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(vehicleState);

        options ??= new RuntimeScenarioTestOptions();

        var now = options.TimestampUtc ?? DateTime.UtcNow;
        var startedUtc = options.StartedUtc ?? now;

        var runState = BuildRunState(scenario, vehicleState, options, startedUtc, now);
        var context = BuildJudgeContext(scenario, runState, vehicleState, options, now);

        var judgeResult = _judge.Evaluate(context);

        var finalRunState = runState with
        {
            Status = ResolveRunStatus(judgeResult),
            IsCompleted = judgeResult.IsSuccess,
            IsFailed = judgeResult.IsFailure,
            IsRunning = judgeResult.IsRunning,
            IsAborted = string.Equals(judgeResult.Status, ScenarioJudgeStatus.Aborted, StringComparison.OrdinalIgnoreCase),
            IsTimedOut = string.Equals(judgeResult.Status, ScenarioJudgeStatus.Timeout, StringComparison.OrdinalIgnoreCase),
            FinishedUtc = judgeResult.IsSuccess || judgeResult.IsFailure ? now : runState.FinishedUtc,
            Score = judgeResult.Score,
            Penalty = judgeResult.Penalty,
            CompletionRatio = judgeResult.CompletionRatio,
            TotalObjectiveCount = judgeResult.TotalObjectiveCount,
            CompletedObjectiveCount = judgeResult.CompletedObjectiveCount,
            FailedObjectiveCount = judgeResult.FailedObjectiveCount,
            CurrentObjectiveId = judgeResult.CurrentObjectiveId ?? runState.CurrentObjectiveId,
            CurrentObjectiveTitle = judgeResult.CurrentObjectiveTitle ?? runState.CurrentObjectiveTitle,
            FailureReason = judgeResult.FailureReason,
            Summary = judgeResult.Summary
        };

        return ScenarioRunReportBuilder.Build(
            scenario,
            finalRunState,
            judgeResult);
    }

    /// <summary>
    /// Çoklu araç state örneği üzerinden ardışık değerlendirme çalıştırır.
    /// Bu method ileride replay/timeline testleri için temel oluşturur.
    /// </summary>
    public ScenarioRunReport RunTimelineEvaluation(
        ScenarioDefinition scenario,
        IReadOnlyList<RuntimeScenarioVehicleState> timeline,
        RuntimeScenarioTestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(timeline);

        if (timeline.Count == 0)
        {
            throw new ArgumentException("Scenario timeline boş olamaz.", nameof(timeline));
        }

        options ??= new RuntimeScenarioTestOptions();

        var runId = string.IsNullOrWhiteSpace(options.RunId)
            ? Guid.NewGuid().ToString("N")
            : options.RunId;

        var startedUtc = options.StartedUtc ?? timeline[0].TimestampUtc ?? DateTime.UtcNow;

        var previousEvents = new List<ScenarioJudgeEvent>();
        var previousViolations = new List<ScenarioJudgeViolation>();

        ScenarioRunState? lastRunState = null;
        ScenarioJudgeResult? lastJudgeResult = null;

        for (var i = 0; i < timeline.Count; i++)
        {
            var vehicleState = timeline[i];
            var now = vehicleState.TimestampUtc ?? DateTime.UtcNow;

            var stepOptions = options with
            {
                RunId = runId,
                StartedUtc = startedUtc,
                TimestampUtc = now,
                PreviousEvents = previousEvents,
                PreviousViolations = previousViolations
            };

            var runState = BuildRunState(scenario, vehicleState, stepOptions, startedUtc, now);
            var context = BuildJudgeContext(scenario, runState, vehicleState, stepOptions, now);

            var judgeResult = _judge.Evaluate(context);

            previousEvents = judgeResult.Events.ToList();
            previousViolations = judgeResult.Violations.ToList();

            lastRunState = runState with
            {
                Status = ResolveRunStatus(judgeResult),
                IsCompleted = judgeResult.IsSuccess,
                IsFailed = judgeResult.IsFailure,
                IsRunning = judgeResult.IsRunning,
                IsAborted = string.Equals(judgeResult.Status, ScenarioJudgeStatus.Aborted, StringComparison.OrdinalIgnoreCase),
                IsTimedOut = string.Equals(judgeResult.Status, ScenarioJudgeStatus.Timeout, StringComparison.OrdinalIgnoreCase),
                FinishedUtc = judgeResult.IsSuccess || judgeResult.IsFailure ? now : runState.FinishedUtc,
                Score = judgeResult.Score,
                Penalty = judgeResult.Penalty,
                CompletionRatio = judgeResult.CompletionRatio,
                TotalObjectiveCount = judgeResult.TotalObjectiveCount,
                CompletedObjectiveCount = judgeResult.CompletedObjectiveCount,
                FailedObjectiveCount = judgeResult.FailedObjectiveCount,
                CurrentObjectiveId = judgeResult.CurrentObjectiveId ?? runState.CurrentObjectiveId,
                CurrentObjectiveTitle = judgeResult.CurrentObjectiveTitle ?? runState.CurrentObjectiveTitle,
                FailureReason = judgeResult.FailureReason,
                Summary = judgeResult.Summary
            };

            lastJudgeResult = judgeResult;

            if (judgeResult.IsSuccess || judgeResult.IsFailure)
            {
                break;
            }
        }

        if (lastRunState is null || lastJudgeResult is null)
        {
            throw new InvalidOperationException("Scenario timeline değerlendirmesi sonuç üretemedi.");
        }

        return ScenarioRunReportBuilder.Build(
            scenario,
            lastRunState,
            lastJudgeResult);
    }

    private static ScenarioRunState BuildRunState(
        ScenarioDefinition scenario,
        RuntimeScenarioVehicleState vehicleState,
        RuntimeScenarioTestOptions options,
        DateTime startedUtc,
        DateTime now)
    {
        var elapsedSeconds = options.ElapsedSecondsOverride ??
                             Math.Max((now - startedUtc).TotalSeconds, 0.0);

        var activeObjective = ResolveActiveObjective(scenario, options.CurrentObjectiveId);

        return new ScenarioRunState
        {
            ScenarioId = scenario.Id,
            RunId = string.IsNullOrWhiteSpace(options.RunId)
                ? Guid.NewGuid().ToString("N")
                : options.RunId,
            VehicleId = string.IsNullOrWhiteSpace(vehicleState.VehicleId)
                ? scenario.VehicleId
                : vehicleState.VehicleId,
            VehiclePlatform = scenario.VehiclePlatform,

            Status = ScenarioRunStatus.Running,
            IsStarted = true,
            IsRunning = true,
            StartedUtc = startedUtc,
            LastUpdatedUtc = now,
            ElapsedSeconds = elapsedSeconds,
            TimeLimitSeconds = scenario.TimeLimitSeconds,
            IsTimedOut = scenario.HasTimeLimit && elapsedSeconds > scenario.TimeLimitSeconds,

            CurrentObjectiveId = activeObjective?.Id,
            CurrentObjectiveTitle = activeObjective?.Title,
            CurrentObjectiveOrder = activeObjective?.Order ?? 0,
            NextObjectiveId = ResolveNextObjectiveId(scenario, activeObjective),

            TotalObjectiveCount = scenario.Objectives.Count,

            VehicleX = vehicleState.X,
            VehicleY = vehicleState.Y,
            VehicleZ = vehicleState.Z,
            VehicleRollDeg = vehicleState.RollDeg,
            VehiclePitchDeg = vehicleState.PitchDeg,
            VehicleYawDeg = vehicleState.YawDeg,
            VehicleVx = vehicleState.Vx,
            VehicleVy = vehicleState.Vy,
            VehicleVz = vehicleState.Vz,

            DistanceToCurrentObjectiveMeters = null,
            HorizontalDistanceToCurrentObjectiveMeters = null,
            VerticalDistanceToCurrentObjectiveMeters = null,

            CollisionCount = options.ReportedCollisionObjectIds.Count,
            NoGoZoneViolationCount = options.ReportedNoGoZoneObjectIds.Count,
            DegradedEventCount = options.ActiveFaultIds.Count,
            SafetyInterventionCount = options.SafetyLimiterActive ? 1 : 0,

            Summary = $"Running scenario={scenario.Id}, vehicle=({vehicleState.X:F2},{vehicleState.Y:F2},{vehicleState.Z:F2})",

            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["run.elapsedSeconds"] = elapsedSeconds,
                ["vehicle.x"] = vehicleState.X,
                ["vehicle.y"] = vehicleState.Y,
                ["vehicle.z"] = vehicleState.Z,
                ["vehicle.yawDeg"] = vehicleState.YawDeg,
                ["vehicle.speed3dMps"] = CalculateSpeed3D(vehicleState.Vx, vehicleState.Vy, vehicleState.Vz),
                ["vehicle.speedHorizontalMps"] = CalculateSpeed2D(vehicleState.Vx, vehicleState.Vy)
            },

            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["runner"] = nameof(RuntimeScenarioTestRunner),
                ["scenario.id"] = scenario.Id,
                ["scenario.family"] = scenario.ScenarioFamily,
                ["vehicle.platform"] = scenario.VehiclePlatform
            }
        };
    }

    private static ScenarioJudgeContext BuildJudgeContext(
        ScenarioDefinition scenario,
        ScenarioRunState runState,
        RuntimeScenarioVehicleState vehicleState,
        RuntimeScenarioTestOptions options,
        DateTime now)
    {
        return new ScenarioJudgeContext
        {
            Scenario = scenario,
            RunState = runState,
            TimestampUtc = now,

            VehicleX = vehicleState.X,
            VehicleY = vehicleState.Y,
            VehicleZ = vehicleState.Z,
            VehicleRollDeg = vehicleState.RollDeg,
            VehiclePitchDeg = vehicleState.PitchDeg,
            VehicleYawDeg = vehicleState.YawDeg,
            VehicleVx = vehicleState.Vx,
            VehicleVy = vehicleState.Vy,
            VehicleVz = vehicleState.Vz,
            VehicleYawRateDegPerSecond = vehicleState.YawRateDegPerSecond,

            VehicleRadiusMeters = options.VehicleRadiusMeters,
            VehicleVerticalToleranceMeters = options.VehicleVerticalToleranceMeters,

            StateConfidence = options.StateConfidence,
            FusionConfidence = options.FusionConfidence,

            GpsHealthy = options.GpsHealthy,
            ImuHealthy = options.ImuHealthy,
            ObstacleSensorHealthy = options.ObstacleSensorHealthy,

            IsDegradedMode = options.IsDegradedMode,
            SafetyLimiterActive = options.SafetyLimiterActive,
            EmergencyStopActive = options.EmergencyStopActive,

            ReportedCollisionObjectIds = options.ReportedCollisionObjectIds,
            ReportedNoGoZoneObjectIds = options.ReportedNoGoZoneObjectIds,
            ActiveFaultIds = options.ActiveFaultIds,

            PreviousEvents = options.PreviousEvents,
            PreviousViolations = options.PreviousViolations,

            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["stateConfidence"] = options.StateConfidence,
                ["fusionConfidence"] = options.FusionConfidence,
                ["vehicle.radiusMeters"] = options.VehicleRadiusMeters,
                ["vehicle.verticalToleranceMeters"] = options.VehicleVerticalToleranceMeters
            },

            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["runner"] = nameof(RuntimeScenarioTestRunner)
            }
        };
    }

    private static ScenarioMissionObjectiveDefinition? ResolveActiveObjective(
        ScenarioDefinition scenario,
        string? preferredObjectiveId)
    {
        if (!string.IsNullOrWhiteSpace(preferredObjectiveId))
        {
            var preferred = scenario.Objectives.FirstOrDefault(x =>
                string.Equals(x.Id, preferredObjectiveId, StringComparison.OrdinalIgnoreCase));

            if (preferred is not null)
            {
                return preferred;
            }
        }

        return scenario.Objectives
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? ResolveNextObjectiveId(
        ScenarioDefinition scenario,
        ScenarioMissionObjectiveDefinition? activeObjective)
    {
        if (activeObjective is null)
        {
            return null;
        }

        return scenario.Objectives
            .Where(x => x.Order > activeObjective.Order)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?.Id;
    }

    private static string ResolveRunStatus(ScenarioJudgeResult judgeResult)
    {
        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Success, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Completed;
        }

        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Timeout, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Timeout;
        }

        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Aborted, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Aborted;
        }

        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Failed, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Failed;
        }

        return ScenarioRunStatus.Running;
    }

    private static double CalculateSpeed2D(double vx, double vy)
    {
        return Math.Sqrt((vx * vx) + (vy * vy));
    }

    private static double CalculateSpeed3D(double vx, double vy, double vz)
    {
        return Math.Sqrt((vx * vx) + (vy * vy) + (vz * vz));
    }
}

/// <summary>
/// Runtime senaryo test runner için araç state girdisidir.
/// </summary>
public sealed record RuntimeScenarioVehicleState
{
    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; init; } = "hydronom-main";

    /// <summary>
    /// Araç state zamanı.
    /// </summary>
    public DateTime? TimestampUtc { get; init; }

    /// <summary>
    /// Araç X konumu.
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Araç Y konumu.
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Araç Z konumu.
    /// Denizaltı için derinlik, VTOL için irtifa ekseni olarak yorumlanabilir.
    /// </summary>
    public double Z { get; init; }

    /// <summary>
    /// Araç roll açısı.
    /// </summary>
    public double RollDeg { get; init; }

    /// <summary>
    /// Araç pitch açısı.
    /// </summary>
    public double PitchDeg { get; init; }

    /// <summary>
    /// Araç yaw/heading açısı.
    /// </summary>
    public double YawDeg { get; init; }

    /// <summary>
    /// Araç X hızı.
    /// </summary>
    public double Vx { get; init; }

    /// <summary>
    /// Araç Y hızı.
    /// </summary>
    public double Vy { get; init; }

    /// <summary>
    /// Araç Z hızı.
    /// </summary>
    public double Vz { get; init; }

    /// <summary>
    /// Araç yaw rate değeri.
    /// </summary>
    public double YawRateDegPerSecond { get; init; }
}

/// <summary>
/// Runtime senaryo test runner ayarlarıdır.
/// </summary>
public sealed record RuntimeScenarioTestOptions
{
    /// <summary>
    /// Koşu kimliği.
    /// Boşsa otomatik oluşturulur.
    /// </summary>
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Koşu başlangıç zamanı.
    /// </summary>
    public DateTime? StartedUtc { get; init; }

    /// <summary>
    /// Bu değerlendirme tick zamanı.
    /// </summary>
    public DateTime? TimestampUtc { get; init; }

    /// <summary>
    /// Geçen süreyi elle override etmek için kullanılır.
    /// </summary>
    public double? ElapsedSecondsOverride { get; init; }

    /// <summary>
    /// Aktif objective kimliği.
    /// Boşsa ilk objective aktif kabul edilir.
    /// </summary>
    public string? CurrentObjectiveId { get; init; }

    /// <summary>
    /// Araç yaklaşık yarıçapı.
    /// </summary>
    public double VehicleRadiusMeters { get; init; } = 0.5;

    /// <summary>
    /// Araç dikey toleransı.
    /// Denizaltı/VTOL gibi 3D görevlerde Z kontrolünü yumuşatmak için kullanılır.
    /// </summary>
    public double VehicleVerticalToleranceMeters { get; init; } = 0.5;

    /// <summary>
    /// State confidence.
    /// </summary>
    public double StateConfidence { get; init; } = 1.0;

    /// <summary>
    /// Fusion confidence.
    /// </summary>
    public double FusionConfidence { get; init; } = 1.0;

    /// <summary>
    /// GPS sağlıklı mı?
    /// </summary>
    public bool GpsHealthy { get; init; } = true;

    /// <summary>
    /// IMU sağlıklı mı?
    /// </summary>
    public bool ImuHealthy { get; init; } = true;

    /// <summary>
    /// Obstacle sensor sağlıklı mı?
    /// </summary>
    public bool ObstacleSensorHealthy { get; init; } = true;

    /// <summary>
    /// Sistem degraded modda mı?
    /// </summary>
    public bool IsDegradedMode { get; init; }

    /// <summary>
    /// Safety limiter aktif mi?
    /// </summary>
    public bool SafetyLimiterActive { get; init; }

    /// <summary>
    /// Emergency stop aktif mi?
    /// </summary>
    public bool EmergencyStopActive { get; init; }

    /// <summary>
    /// Bildirilen çarpışma obje kimlikleri.
    /// </summary>
    public IReadOnlyList<string> ReportedCollisionObjectIds { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Bildirilen no-go zone ihlal obje kimlikleri.
    /// </summary>
    public IReadOnlyList<string> ReportedNoGoZoneObjectIds { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Aktif fault kimlikleri.
    /// </summary>
    public IReadOnlyList<string> ActiveFaultIds { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Önceki judge event kayıtları.
    /// Timeline koşularında olay tekrarlarını önlemek için kullanılır.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeEvent> PreviousEvents { get; init; }
        = Array.Empty<ScenarioJudgeEvent>();

    /// <summary>
    /// Önceki judge violation kayıtları.
    /// Timeline koşularında ihlal tekrarlarını önlemek için kullanılır.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeViolation> PreviousViolations { get; init; }
        = Array.Empty<ScenarioJudgeViolation>();
}