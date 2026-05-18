namespace Hydronom.Core.Communication.Commands;

public sealed record HydronomCommandAuthorityDecision
{
    public bool Allowed { get; init; }

    public string Reason { get; init; } = "";

    public HydronomCommandKind Kind { get; init; } = HydronomCommandKind.Unknown;

    public HydronomCommandAuthority Authority { get; init; } = HydronomCommandAuthority.Unknown;

    public string SourceId { get; init; } = "";

    public string TargetId { get; init; } = "";

    public string VehicleId { get; init; } = "";

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public static HydronomCommandAuthorityDecision Allow(
        HydronomCommandFrame command,
        string reason = "AUTHORITY_ALLOWED")
    {
        ArgumentNullException.ThrowIfNull(command);

        return new HydronomCommandAuthorityDecision
        {
            Allowed = true,
            Reason = reason,
            Kind = command.Kind,
            Authority = command.Authority,
            SourceId = command.SourceId,
            TargetId = command.TargetId,
            VehicleId = command.VehicleId
        };
    }

    public static HydronomCommandAuthorityDecision Reject(
        HydronomCommandFrame command,
        string reason,
        params string[] issues)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new HydronomCommandAuthorityDecision
        {
            Allowed = false,
            Reason = reason,
            Kind = command.Kind,
            Authority = command.Authority,
            SourceId = command.SourceId,
            TargetId = command.TargetId,
            VehicleId = command.VehicleId,
            Issues = issues
        };
    }
}