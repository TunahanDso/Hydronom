namespace Hydronom.Core.Communication.Security;

public sealed record HydronomSecurityResult
{
    public bool Accepted { get; init; }

    public string Reason { get; init; } = "";

    public string SourceId { get; init; } = "";

    public ulong Sequence { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public static HydronomSecurityResult Accept(string sourceId, ulong sequence)
    {
        return new HydronomSecurityResult
        {
            Accepted = true,
            Reason = "ACCEPTED",
            SourceId = sourceId,
            Sequence = sequence
        };
    }

    public static HydronomSecurityResult Reject(
        string sourceId,
        ulong sequence,
        string reason,
        params string[] issues)
    {
        return new HydronomSecurityResult
        {
            Accepted = false,
            Reason = reason,
            SourceId = sourceId,
            Sequence = sequence,
            Issues = issues
        };
    }
}