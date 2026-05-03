using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules;

public sealed class ConsoleFeedbackRecorder : IFeedbackRecorder
{
    public void Record(FeedbackRecord r)
    {
        var p = r.State.Position;
        var o = r.State.Orientation;
        var v = r.State.LinearVelocity;

        Console.WriteLine(
            $"[{r.TimestampUtc:O}] " +
            $"pos=({p.X:F2},{p.Y:F2},{p.Z:F2}) " +
            $"rpy=({o.RollDeg:F1},{o.PitchDeg:F1},{o.YawDeg:F1}) " +
            $"vel=({v.X:F2},{v.Y:F2},{v.Z:F2}) " +
            $"obsAhead={(r.Insights.HasObstacleAhead ? "True" : "False")} " +
            $"cmd(thr={r.Command.Throttle01:F2}, rud={r.Command.RudderNeg1To1:+0.00;-0.00;0.00})"
        );

        // Kuvvet/tork (gÃ¶vde ekseni)
        Console.WriteLine(
            $"[FORCE] Fb=({r.ForceBody.X:F2},{r.ForceBody.Y:F2},{r.ForceBody.Z:F2}) " +
            $"Tb=({r.TorqueBody.X:F2},{r.TorqueBody.Y:F2},{r.TorqueBody.Z:F2})"
        );
    }
}

