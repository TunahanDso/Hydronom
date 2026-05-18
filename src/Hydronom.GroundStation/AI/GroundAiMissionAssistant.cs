using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Clients;
using Hydronom.AI.Clients.Local;
using Hydronom.AI.Orchestration;
using Hydronom.AI.Planning.Validation;
using Hydronom.AI.Policies;
using Hydronom.AI.Prompts.Mission;
using Hydronom.AI.Safety;
using Hydronom.Core.Domain.AI;

namespace Hydronom.GroundStation.AI;

public sealed class GroundAiMissionAssistant
{
    private readonly IAiClient _client;
    private readonly string _providerName;

    public GroundAiMissionAssistant(IAiClient client, string providerName = "custom")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _providerName = string.IsNullOrWhiteSpace(providerName) ? "custom" : providerName.Trim();
    }

    public static GroundAiMissionAssistant CreateFake()
        => new(new FakeAiClient(), "fake");

    public static GroundAiMissionAssistant CreateLocalLlama(
        HttpClient http,
        string endpointUrl = "http://localhost:11434/api/generate",
        string model = "qwen2.5:3b-instruct")
        => new(new LocalLlamaClient(http, endpointUrl, model), $"local-llama:{model}");

    public async Task<GroundAiMissionResult> CreateMissionPlanFromStoryAsync(
        string story,
        string? runtimeContext,
        string? vehicleContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(story))
            throw new ArgumentException("Görev hikayesi boş olamaz.", nameof(story));

        var context = MissionPlanningPromptBuilder.BuildGroundMissionContext(
            story,
            runtimeContext,
            vehicleContext);

        var orchestrator = CreateOrchestrator();

        var result = await orchestrator
            .SuggestPlanAsync(
                context,
                Array.Empty<ToolSpec>(),
                SafetyPolicy.DefaultSuggest(),
                AiAuthorityPolicy.SuggestOnly(),
                _providerName,
                ct)
            .ConfigureAwait(false);

        return ToGroundResult(
            input: story.Trim(),
            result,
            resultKind: GroundAiResultKind.MissionPlan);
    }

    public async Task<GroundAiMissionResult> AnalyzeRuntimeAsync(
        string runtimeSnapshot,
        string? missionState,
        string? vehicleState,
        string? sensorState,
        string? actuatorState,
        string? safetyState,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runtimeSnapshot))
            throw new ArgumentException("Runtime snapshot boş olamaz.", nameof(runtimeSnapshot));

        var orchestrator = CreateOrchestrator();

        var result = await orchestrator
            .AnalyzeRuntimeAsync(
                runtimeSnapshot,
                missionState,
                vehicleState,
                sensorState,
                actuatorState,
                safetyState,
                _providerName,
                ct)
            .ConfigureAwait(false);

        return ToGroundResult(
            input: runtimeSnapshot.Trim(),
            result,
            resultKind: GroundAiResultKind.RuntimeAnalysis);
    }

    public async Task<GroundAiMissionResult> SummarizeMissionAsync(
        string missionState,
        string? recentEvents,
        string? performanceState,
        string? safetyState,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(missionState))
            throw new ArgumentException("Mission state boş olamaz.", nameof(missionState));

        var orchestrator = CreateOrchestrator();

        var result = await orchestrator
            .SummarizeMissionAsync(
                missionState,
                recentEvents,
                performanceState,
                safetyState,
                _providerName,
                ct)
            .ConfigureAwait(false);

        return ToGroundResult(
            input: missionState.Trim(),
            result,
            resultKind: GroundAiResultKind.MissionSummary);
    }

    public async Task<GroundAiMissionResult> ExplainFaultAsync(
        string faultOrWarning,
        string? runtimeSnapshot,
        string? sensorState,
        string? actuatorState,
        string? safetyState,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(faultOrWarning))
            throw new ArgumentException("Fault/warning metni boş olamaz.", nameof(faultOrWarning));

        var orchestrator = CreateOrchestrator();

        var result = await orchestrator
            .ExplainFaultAsync(
                faultOrWarning,
                runtimeSnapshot,
                sensorState,
                actuatorState,
                safetyState,
                _providerName,
                ct)
            .ConfigureAwait(false);

        return ToGroundResult(
            input: faultOrWarning.Trim(),
            result,
            resultKind: GroundAiResultKind.FaultExplanation);
    }

    public async Task<GroundAiMissionResult> AssessRiskAsync(
        string missionRequest,
        string? vehicleState,
        string? worldState,
        string? sensorState,
        string? safetyState,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(missionRequest))
            throw new ArgumentException("Mission request boş olamaz.", nameof(missionRequest));

        var orchestrator = CreateOrchestrator();

        var result = await orchestrator
            .AssessRiskAsync(
                missionRequest,
                vehicleState,
                worldState,
                sensorState,
                safetyState,
                _providerName,
                ct)
            .ConfigureAwait(false);

        return ToGroundResult(
            input: missionRequest.Trim(),
            result,
            resultKind: GroundAiResultKind.RiskAssessment);
    }

    private AiRuntimeOrchestrator CreateOrchestrator()
        => new(_client);

    private GroundAiMissionResult ToGroundResult(
        string input,
        AiRuntimePlanResult result,
        GroundAiResultKind resultKind)
        => new(
            Input: input,
            Provider: _providerName,
            ResultKind: resultKind,
            OperationKind: result.OperationKind,
            Plan: result.Plan,
            Validation: result.Validation,
            Safety: result.Safety,
            RequiresHumanApproval: result.Safety.RequiresHumanApproval,
            LatencyMs: result.Diagnostics.LastLatencyMs
        );
}

public enum GroundAiResultKind
{
    MissionPlan = 0,
    RuntimeAnalysis = 1,
    MissionSummary = 2,
    FaultExplanation = 3,
    RiskAssessment = 4
}

public sealed record GroundAiMissionResult(
    string Input,
    string Provider,
    GroundAiResultKind ResultKind,
    string OperationKind,
    MissionPlan Plan,
    AiPlanValidationResult Validation,
    AiSafetyDecision Safety,
    bool RequiresHumanApproval,
    double LatencyMs
);