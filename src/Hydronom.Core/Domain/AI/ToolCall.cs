using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain.AI;

public sealed record ToolCall(
    string Id,
    string Name,
    IReadOnlyDictionary<string, object?> Args,
    DateTime TimestampUtc
)
{
    public static ToolCall Create(string name, IReadOnlyDictionary<string, object?> args)
        => new ToolCall(
            Id: Guid.NewGuid().ToString("N"),
            Name: name,
            Args: args,
            TimestampUtc: DateTime.UtcNow
        );
}
