using System;
using System.Collections.Generic;
using Hydronom.Core.Domain.AI;

namespace Hydronom.AI.Orchestration;

public static class AiContextBuilder
{
    // Doğrudan AiMessage içindeki Factory metotları kullanıyoruz
    public static AiMessage System(string text)
        => AiMessage.System(text);

    public static AiMessage User(string text)
        => AiMessage.User(text);

    public static AiMessage Assistant(string text)
        => AiMessage.Assistant(text);

    public static IReadOnlyList<AiMessage> Trim(IReadOnlyList<AiMessage> msgs, int max = 40)
    {
        if (msgs.Count <= max) return msgs;
        var start = Math.Max(0, msgs.Count - max);
        var list = new List<AiMessage>(max);
        for (int i = start; i < msgs.Count; i++)
            list.Add(msgs[i]);
        return list;
    }
}