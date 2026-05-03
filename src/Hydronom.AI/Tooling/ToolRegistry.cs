using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain.AI;
using Hydronom.Core.Interfaces.AI;

namespace Hydronom.AI.Tooling;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry(IEnumerable<IAiTool> tools)
    {
        foreach (var t in tools)
            Register(t);
    }

    public void Register(IAiTool tool)
    {
        var name = tool.Spec?.Name;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("ToolSpec.Name boÅŸ olamaz.");

        _tools[name] = tool;
    }

    public IReadOnlyList<ToolSpec> ListSpecs()
        => _tools.Values.Select(t => t.Spec).ToList();

    public bool TryGet(string name, out IAiTool tool)
        => _tools.TryGetValue(name, out tool!);
}
