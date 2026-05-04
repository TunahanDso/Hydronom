using Hydronom.Runtime.Scenarios.Execution;
using Hydronom.Runtime.Scenarios.Telemetry;
using Hydronom.Runtime.Telemetry;

namespace Hydronom.Runtime.Scenarios.Replay;

/// <summary>
/// ScenarioExecutionResult içindeki timeline telemetry özetlerini sırayla publish eder.
/// Bu sınıf Digital Proving Ground koşusunu Gateway/Ops tarafına replay/canlı akış gibi taşımak için kullanılır.
/// </summary>
public sealed class ScenarioTelemetryReplayPublisher
{
    private readonly ScenarioExecutionTelemetryProjector _projector;
    private readonly IRuntimeTelemetryPublisher _publisher;

    public ScenarioTelemetryReplayPublisher(
        ScenarioExecutionTelemetryProjector projector,
        IRuntimeTelemetryPublisher publisher)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// Scenario execution timeline'ını RuntimeTelemetrySummary frame'lerine dönüştürür
    /// ve publisher üzerinden sırayla yayınlar.
    /// </summary>
    public async Task<ScenarioTelemetryReplayPublishResult> PublishTimelineAsync(
        ScenarioExecutionResult executionResult,
        ScenarioTelemetryReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        var safeOptions = (options ?? new ScenarioTelemetryReplayOptions()).Sanitized();
        var timeline = _projector.ProjectTimeline(executionResult);

        if (timeline.Count == 0)
        {
            return ScenarioTelemetryReplayPublishResult.NotPublished(
                scenarioId: executionResult.ScenarioId,
                runId: executionResult.RunId,
                reason: "timeline_empty");
        }

        var startedUtc = DateTime.UtcNow;
        var publishedCount = 0;
        var skippedCount = 0;

        var stride = Math.Max(safeOptions.FrameStride, 1);

        for (var i = 0; i < timeline.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (i % stride != 0)
            {
                skippedCount++;
                continue;
            }

            await _publisher
                .PublishAsync(timeline[i], cancellationToken)
                .ConfigureAwait(false);

            publishedCount++;

            if (safeOptions.DelayBetweenFramesMs > 0 && i < timeline.Count - 1)
            {
                await Task
                    .Delay(safeOptions.DelayBetweenFramesMs, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (safeOptions.PublishFinalSummary)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var finalSummary = _projector.ProjectFinalSummary(executionResult);

            await _publisher
                .PublishAsync(finalSummary, cancellationToken)
                .ConfigureAwait(false);

            publishedCount++;
        }

        var finishedUtc = DateTime.UtcNow;

        return new ScenarioTelemetryReplayPublishResult
        {
            Published = publishedCount > 0,
            ScenarioId = executionResult.ScenarioId,
            RunId = executionResult.RunId,
            StartedUtc = startedUtc,
            FinishedUtc = finishedUtc,
            TimelineFrameCount = timeline.Count,
            PublishedFrameCount = publishedCount,
            SkippedFrameCount = skippedCount,
            DelayBetweenFramesMs = safeOptions.DelayBetweenFramesMs,
            FrameStride = safeOptions.FrameStride,
            PublishedFinalSummary = safeOptions.PublishFinalSummary,
            Reason = publishedCount > 0 ? "published" : "not_published",
            Summary =
                $"Scenario telemetry replay published: scenario={executionResult.ScenarioId}, " +
                $"run={executionResult.RunId}, frames={publishedCount}/{timeline.Count}, " +
                $"stride={safeOptions.FrameStride}, delayMs={safeOptions.DelayBetweenFramesMs}"
        };
    }
}

/// <summary>
/// ScenarioTelemetryReplayPublisher çalışma ayarlarıdır.
/// </summary>
public sealed record ScenarioTelemetryReplayOptions
{
    /// <summary>
    /// Frame'ler arasında bekleme süresi.
    /// 0 ise mümkün olduğunca hızlı yayınlanır.
    /// </summary>
    public int DelayBetweenFramesMs { get; init; } = 20;

    /// <summary>
    /// Kaç frame'de bir yayın yapılacağını belirtir.
    /// 1 ise her frame yayınlanır, 2 ise iki frame'de bir yayınlanır.
    /// </summary>
    public int FrameStride { get; init; } = 1;

    /// <summary>
    /// Timeline bittikten sonra ayrıca final summary yayınlansın mı?
    /// </summary>
    public bool PublishFinalSummary { get; init; } = true;

    /// <summary>
    /// Geçersiz değerleri güvenli hale getirir.
    /// </summary>
    public ScenarioTelemetryReplayOptions Sanitized()
    {
        return this with
        {
            DelayBetweenFramesMs = DelayBetweenFramesMs < 0 ? 0 : DelayBetweenFramesMs,
            FrameStride = FrameStride <= 0 ? 1 : FrameStride
        };
    }
}

/// <summary>
/// Scenario telemetry replay publish sonucudur.
/// </summary>
public sealed record ScenarioTelemetryReplayPublishResult
{
    public bool Published { get; init; }

    public string ScenarioId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

    public DateTime FinishedUtc { get; init; } = DateTime.UtcNow;

    public int TimelineFrameCount { get; init; }

    public int PublishedFrameCount { get; init; }

    public int SkippedFrameCount { get; init; }

    public int DelayBetweenFramesMs { get; init; }

    public int FrameStride { get; init; }

    public bool PublishedFinalSummary { get; init; }

    public string Reason { get; init; } = "unknown";

    public string Summary { get; init; } = string.Empty;

    public static ScenarioTelemetryReplayPublishResult NotPublished(
        string scenarioId,
        string runId,
        string reason)
    {
        return new ScenarioTelemetryReplayPublishResult
        {
            Published = false,
            ScenarioId = string.IsNullOrWhiteSpace(scenarioId) ? "unknown_scenario" : scenarioId.Trim(),
            RunId = string.IsNullOrWhiteSpace(runId) ? "unknown_run" : runId.Trim(),
            StartedUtc = DateTime.UtcNow,
            FinishedUtc = DateTime.UtcNow,
            TimelineFrameCount = 0,
            PublishedFrameCount = 0,
            SkippedFrameCount = 0,
            DelayBetweenFramesMs = 0,
            FrameStride = 1,
            PublishedFinalSummary = false,
            Reason = string.IsNullOrWhiteSpace(reason) ? "not_published" : reason.Trim(),
            Summary = $"Scenario telemetry replay not published. reason={reason}"
        };
    }
}