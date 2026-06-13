using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.AI.Diagnostics;
using Hydronom.AI.Planning.Validation;
using Hydronom.AI.Policies;
using Hydronom.AI.Safety;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Orchestration;

public sealed class AiRuntimeOrchestrator
{
    private readonly AiOrchestrator _orchestrator;
    private readonly AiPlanValidator _validator;
    private readonly AiSafetyGate _safetyGate;

    public AiDiagnosticsSnapshot LastDiagnostics { get; private set; } = AiDiagnosticsSnapshot.Disabled();

    public AiRuntimeOrchestrator(IAiClient client)
        : this(client, new AiPlanValidator(), new AiSafetyGate())
    {
    }

    public AiRuntimeOrchestrator(
        IAiClient client,
        AiPlanValidator validator,
        AiSafetyGate safetyGate)
    {
        ArgumentNullException.ThrowIfNull(client);

        _orchestrator = new AiOrchestrator(client);
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _safetyGate = safetyGate ?? throw new ArgumentNullException(nameof(safetyGate));
    }

    public Task<AiRuntimePlanResult> SuggestPlanAsync(
        IReadOnlyList<AiMessage> context,
        IReadOnlyList<ToolSpec> tools,
        SafetyPolicy? safetyPolicy,
        AiAuthorityPolicy? authorityPolicy,
        string provider,
        CancellationToken ct)
    {
        return RunPlanPipelineAsync(
            context,
            tools,
            safetyPolicy,
            authorityPolicy,
            provider,
            operationKind: "MissionPlanSuggestion",
            ct);
    }

    public Task<AiRuntimePlanResult> AnalyzeRuntimeAsync(
        string runtimeSnapshot,
        string? missionState,
        string? vehicleState,
        string? sensorState,
        string? actuatorState,
        string? safetyState,
        string provider,
        CancellationToken ct)
    {
        var context = BuildOperationAssistantContext(
            task: "Runtime durumunu analiz et. Operatöre sistemin genel sağlığını, riskleri ve dikkat edilmesi gereken noktaları açıkla.",
            runtimeSnapshot,
            missionState,
            vehicleState,
            sensorState,
            actuatorState,
            safetyState);

        return RunPlanPipelineAsync(
            context,
            Array.Empty<ToolSpec>(),
            SafetyPolicy.DefaultSuggest(),
            AiAuthorityPolicy.SuggestOnly(),
            provider,
            operationKind: "RuntimeAnalysis",
            ct);
    }

    public Task<AiRuntimePlanResult> SummarizeMissionAsync(
        string missionState,
        string? recentEvents,
        string? performanceState,
        string? safetyState,
        string provider,
        CancellationToken ct)
    {
        var context = BuildOperationAssistantContext(
            task: "Görev durumunu özetle. Ne yapıldı, ne kaldı, riskler neler ve operatör neye bakmalı açıkla.",
            runtimeSnapshot: performanceState,
            missionState: missionState,
            vehicleState: null,
            sensorState: recentEvents,
            actuatorState: null,
            safetyState: safetyState);

        return RunPlanPipelineAsync(
            context,
            Array.Empty<ToolSpec>(),
            SafetyPolicy.DefaultSuggest(),
            AiAuthorityPolicy.SuggestOnly(),
            provider,
            operationKind: "MissionSummary",
            ct);
    }

    public Task<AiRuntimePlanResult> ExplainFaultAsync(
        string faultOrWarning,
        string? runtimeSnapshot,
        string? sensorState,
        string? actuatorState,
        string? safetyState,
        string provider,
        CancellationToken ct)
    {
        var context = BuildOperationAssistantContext(
            task: "Arıza/uyarı açıklaması yap. Muhtemel sebebi, etkisini, güvenli kontrol adımlarını ve operatör önerisini üret.",
            runtimeSnapshot,
            missionState: "Fault/Warning: " + faultOrWarning,
            vehicleState: null,
            sensorState,
            actuatorState,
            safetyState);

        return RunPlanPipelineAsync(
            context,
            Array.Empty<ToolSpec>(),
            SafetyPolicy.DefaultSuggest(),
            AiAuthorityPolicy.SuggestOnly(),
            provider,
            operationKind: "FaultExplanation",
            ct);
    }

    public Task<AiRuntimePlanResult> AssessRiskAsync(
        string missionRequest,
        string? vehicleState,
        string? worldState,
        string? sensorState,
        string? safetyState,
        string provider,
        CancellationToken ct)
    {
        var context = BuildOperationAssistantContext(
            task: "Görev risk değerlendirmesi yap. Görev uygulanabilir mi, hangi koşullarda riskli, hangi onaylar gerekir açıkla.",
            runtimeSnapshot: worldState,
            missionState: "Mission request: " + missionRequest,
            vehicleState,
            sensorState,
            actuatorState: null,
            safetyState);

        return RunPlanPipelineAsync(
            context,
            Array.Empty<ToolSpec>(),
            SafetyPolicy.DefaultSuggest(),
            AiAuthorityPolicy.SuggestOnly(),
            provider,
            operationKind: "RiskAssessment",
            ct);
    }

    private async Task<AiRuntimePlanResult> RunPlanPipelineAsync(
        IReadOnlyList<AiMessage> context,
        IReadOnlyList<ToolSpec> tools,
        SafetyPolicy? safetyPolicy,
        AiAuthorityPolicy? authorityPolicy,
        string provider,
        string operationKind,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        safetyPolicy ??= SafetyPolicy.DefaultSuggest();
        authorityPolicy ??= AiAuthorityPolicy.SuggestOnly();

        try
        {
            var plan = await _orchestrator
                .PlanAsync(context, tools, safetyPolicy, ct)
                .ConfigureAwait(false);

            var validation = _validator.Validate(plan, safetyPolicy, authorityPolicy);
            var decision = _safetyGate.Evaluate(plan, validation, safetyPolicy, authorityPolicy);

            sw.Stop();

            LastDiagnostics = AiDiagnosticsSnapshot.FromPlan(
                provider,
                $"{safetyPolicy.Mode}:{operationKind}",
                plan,
                decision,
                sw.Elapsed.TotalMilliseconds);

            return new AiRuntimePlanResult(
                Plan: plan,
                Validation: validation,
                Safety: decision,
                Diagnostics: LastDiagnostics,
                OperationKind: operationKind);
        }
        catch (Exception ex)
        {
            sw.Stop();

            LastDiagnostics = AiDiagnosticsSnapshot.FromError(
                provider,
                $"{safetyPolicy.Mode}:{operationKind}",
                ex,
                sw.Elapsed.TotalMilliseconds);

            throw;
        }
    }

    private static IReadOnlyList<AiMessage> BuildOperationAssistantContext(
        string task,
        string? runtimeSnapshot,
        string? missionState,
        string? vehicleState,
        string? sensorState,
        string? actuatorState,
        string? safetyState)
    {
        var messages = new List<AiMessage>
        {
            AiMessage.System(
                "Sen HydronomAI operasyon asistanısın. " +
                "Araç üstü gerçek zamanlı kontrolcü değilsin; yer istasyonunda operatöre analiz, özet ve öneri üretirsin. " +
                "Doğrudan motor, actuator, PWM, ESC, thrust, rudder veya servo komutu üretmezsin. " +
                "SafetyGate, EmergencyStop, authority ve operatör onayını asla bypass etmezsin. " +
                "Cevabını MissionPlan JSON formatında üret: goal alanı kısa özet, steps alanı ise uygulanabilir analiz/öneri maddeleri olsun."
            ),
            AiMessage.User("Goal: " + task)
        };

        AddContext(messages, "Runtime snapshot", runtimeSnapshot);
        AddContext(messages, "Mission state", missionState);
        AddContext(messages, "Vehicle state", vehicleState);
        AddContext(messages, "Sensor state", sensorState);
        AddContext(messages, "Actuator state", actuatorState);
        AddContext(messages, "Safety state", safetyState);

        return messages;
    }

    private static void AddContext(List<AiMessage> messages, string title, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        messages.Add(AiMessage.User($"{title}: {value.Trim()}"));
    }
}

public sealed record AiRuntimePlanResult(
    MissionPlan Plan,
    AiPlanValidationResult Validation,
    AiSafetyDecision Safety,
    AiDiagnosticsSnapshot Diagnostics,
    string OperationKind = "MissionPlanSuggestion"
);