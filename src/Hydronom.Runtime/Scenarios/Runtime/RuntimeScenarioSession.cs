using Hydronom.Core.Scenarios.Reports;
using Hydronom.Runtime.Scenarios.Mission;

namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Gerçek runtime scenario oturumunun canlı durumunu taşır.
/// Bu sınıf tek başına karar vermez; RuntimeScenarioObjectiveTracker/ExecutionHost tarafından yönetilir.
/// </summary>
public sealed class RuntimeScenarioSession
{
    private readonly object _gate = new();

    private readonly HashSet<string> _completedObjectiveIds =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<RuntimeScenarioTickResult> _recentTicks = new();

    private int _maxRecentTickCount = 64;

    public RuntimeScenarioSession(ScenarioMissionPlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        RunId = Guid.NewGuid().ToString("N");
        State = RuntimeScenarioSessionState.Created;
        CreatedUtc = DateTime.UtcNow;
        CurrentObjectiveId = plan.FirstTarget?.ObjectiveId;
    }

    /// <summary>
    /// Scenario mission plan.
    /// </summary>
    public ScenarioMissionPlan Plan { get; }

    /// <summary>
    /// Bu runtime koşusunun benzersiz kimliği.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Oturum oluşturulma zamanı.
    /// </summary>
    public DateTime CreatedUtc { get; }

    /// <summary>
    /// Oturumun başlatıldığı zaman.
    /// </summary>
    public DateTime? StartedUtc { get; private set; }

    /// <summary>
    /// Oturumun tamamlandığı/durduğu zaman.
    /// </summary>
    public DateTime? FinishedUtc { get; private set; }

    /// <summary>
    /// Oturum state'i.
    /// </summary>
    public RuntimeScenarioSessionState State { get; private set; }

    /// <summary>
    /// Aktif objective id.
    /// </summary>
    public string? CurrentObjectiveId { get; private set; }

    /// <summary>
    /// Son judge raporu.
    /// </summary>
    public ScenarioRunReport? LastReport { get; private set; }

    /// <summary>
    /// Son tick sonucu.
    /// </summary>
    public RuntimeScenarioTickResult? LastTick { get; private set; }

    /// <summary>
    /// Tamamlanan objective id listesi.
    /// </summary>
    public IReadOnlySet<string> CompletedObjectiveIds
    {
        get
        {
            lock (_gate)
            {
                return new HashSet<string>(_completedObjectiveIds, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Son tick sonuçları.
    /// </summary>
    public IReadOnlyList<RuntimeScenarioTickResult> RecentTicks
    {
        get
        {
            lock (_gate)
            {
                return _recentTicks.ToArray();
            }
        }
    }

    public bool IsActive =>
        State == RuntimeScenarioSessionState.Created ||
        State == RuntimeScenarioSessionState.Running ||
        State == RuntimeScenarioSessionState.Paused;

    public bool IsTerminal =>
        State == RuntimeScenarioSessionState.Completed ||
        State == RuntimeScenarioSessionState.Failed ||
        State == RuntimeScenarioSessionState.TimedOut ||
        State == RuntimeScenarioSessionState.Aborted;

    public int CompletedCount
    {
        get
        {
            lock (_gate)
            {
                return _completedObjectiveIds.Count;
            }
        }
    }

    public int TotalObjectiveCount => Plan.Targets.Count;

    public double CompletionRatio =>
        TotalObjectiveCount <= 0
            ? 0.0
            : Math.Clamp(CompletedCount / (double)TotalObjectiveCount, 0.0, 1.0);

    public TimeSpan Elapsed =>
        StartedUtc is null
            ? TimeSpan.Zero
            : ((FinishedUtc ?? DateTime.UtcNow) - StartedUtc.Value);

    public ScenarioMissionTarget? CurrentTarget =>
        string.IsNullOrWhiteSpace(CurrentObjectiveId)
            ? null
            : Plan.FindTargetByObjectiveId(CurrentObjectiveId);

    public ScenarioMissionTarget? NextTarget =>
        Plan.FindNextTarget(CompletedObjectiveIds);

    /// <summary>
    /// Oturumu başlatır.
    /// </summary>
    public void Start(DateTime? utcNow = null)
    {
        lock (_gate)
        {
            if (IsTerminal)
            {
                throw new InvalidOperationException($"Terminal scenario session tekrar başlatılamaz. State={State}");
            }

            var now = utcNow ?? DateTime.UtcNow;

            StartedUtc ??= now;
            FinishedUtc = null;
            State = RuntimeScenarioSessionState.Running;

            CurrentObjectiveId ??= Plan.FirstTarget?.ObjectiveId;
        }
    }

    /// <summary>
    /// Oturumu duraklatır.
    /// </summary>
    public void Pause()
    {
        lock (_gate)
        {
            if (State == RuntimeScenarioSessionState.Running)
            {
                State = RuntimeScenarioSessionState.Paused;
            }
        }
    }

    /// <summary>
    /// Duraklatılmış oturumu tekrar çalıştırır.
    /// </summary>
    public void Resume(DateTime? utcNow = null)
    {
        lock (_gate)
        {
            if (State == RuntimeScenarioSessionState.Paused)
            {
                State = RuntimeScenarioSessionState.Running;
                StartedUtc ??= utcNow ?? DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Objective tamamlandı olarak işaretlenir.
    /// </summary>
    public bool MarkObjectiveCompleted(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            return false;
        }

        lock (_gate)
        {
            return _completedObjectiveIds.Add(objectiveId.Trim());
        }
    }

    /// <summary>
    /// Aktif objective'i değiştirir.
    /// </summary>
    public void SetCurrentObjective(string? objectiveId)
    {
        lock (_gate)
        {
            CurrentObjectiveId = string.IsNullOrWhiteSpace(objectiveId)
                ? null
                : objectiveId.Trim();
        }
    }

    /// <summary>
    /// Son judge raporunu günceller.
    /// </summary>
    public void SetReport(ScenarioRunReport report)
    {
        lock (_gate)
        {
            LastReport = report;
        }
    }

    /// <summary>
    /// Tick sonucunu kaydeder.
    /// </summary>
    public void AddTick(RuntimeScenarioTickResult tick)
    {
        lock (_gate)
        {
            LastTick = tick;
            LastReport = tick.Report ?? LastReport;

            _recentTicks.Add(tick);

            while (_recentTicks.Count > _maxRecentTickCount)
            {
                _recentTicks.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Oturumu Completed olarak kapatır.
    /// </summary>
    public void Complete(DateTime? utcNow = null)
    {
        lock (_gate)
        {
            State = RuntimeScenarioSessionState.Completed;
            FinishedUtc = utcNow ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Oturumu Failed olarak kapatır.
    /// </summary>
    public void Fail(DateTime? utcNow = null)
    {
        lock (_gate)
        {
            State = RuntimeScenarioSessionState.Failed;
            FinishedUtc = utcNow ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Oturumu TimedOut olarak kapatır.
    /// </summary>
    public void Timeout(DateTime? utcNow = null)
    {
        lock (_gate)
        {
            State = RuntimeScenarioSessionState.TimedOut;
            FinishedUtc = utcNow ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Oturumu Aborted olarak kapatır.
    /// </summary>
    public void Abort(DateTime? utcNow = null)
    {
        lock (_gate)
        {
            State = RuntimeScenarioSessionState.Aborted;
            FinishedUtc = utcNow ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Recent tick history limitini ayarlar.
    /// </summary>
    public void SetRecentTickLimit(int maxRecentTickCount)
    {
        lock (_gate)
        {
            _maxRecentTickCount = maxRecentTickCount <= 0 ? 64 : maxRecentTickCount;

            while (_recentTicks.Count > _maxRecentTickCount)
            {
                _recentTicks.RemoveAt(0);
            }
        }
    }

    public string Summary =>
        $"RuntimeScenarioSession scenario={Plan.ScenarioId}, run={RunId}, state={State}, " +
        $"objective={CurrentObjectiveId ?? "none"}, completed={CompletedCount}/{TotalObjectiveCount}, elapsed={Elapsed.TotalSeconds:F1}s";

    public override string ToString()
    {
        return Summary;
    }
}