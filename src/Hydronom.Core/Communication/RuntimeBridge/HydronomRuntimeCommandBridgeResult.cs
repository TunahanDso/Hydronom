using Hydronom.Core.Communication.Commands;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed record HydronomRuntimeCommandBridgeResult
{
    public bool Accepted { get; init; }

    public string Reason { get; init; } = "";

    public HydronomRuntimeCommandIntent? Intent { get; init; }

    public HydronomCommandFrame? SourceCommand { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public static HydronomRuntimeCommandBridgeResult Accept(
        HydronomCommandFrame sourceCommand,
        HydronomRuntimeCommandIntent intent)
    {
        ArgumentNullException.ThrowIfNull(sourceCommand);
        ArgumentNullException.ThrowIfNull(intent);

        return new HydronomRuntimeCommandBridgeResult
        {
            Accepted = true,
            Reason = "RUNTIME_COMMAND_INTENT_CREATED",
            SourceCommand = sourceCommand,
            Intent = intent
        };
    }

    public static HydronomRuntimeCommandBridgeResult Reject(
        HydronomCommandFrame sourceCommand,
        string reason,
        params string[] issues)
    {
        ArgumentNullException.ThrowIfNull(sourceCommand);

        return new HydronomRuntimeCommandBridgeResult
        {
            Accepted = false,
            Reason = reason,
            SourceCommand = sourceCommand,
            Issues = issues
        };
    }
}