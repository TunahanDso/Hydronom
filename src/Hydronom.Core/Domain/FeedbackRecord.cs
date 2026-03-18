using System;

namespace Hydronom.Core.Domain
{
    /// <summary>Geri bildirim/telemetri kaydı (6DoF + kuvvet/tork).</summary>
    public record FeedbackRecord
    {
        public DateTime       TimestampUtc { get; init; }
        public FusedFrame     Frame        { get; init; }
        public Insights       Insights     { get; init; }
        public DecisionCommand Command     { get; init; }
        public VehicleState   State        { get; init; }

        // Ek: gövde ekseninde kuvvet/tork (ActuatorManager'dan)
        public Vec3           ForceBody    { get; init; }
        public Vec3           TorqueBody   { get; init; }

        // Tam kurucu
        public FeedbackRecord(
            DateTime timestampUtc,
            FusedFrame frame,
            Insights insights,
            DecisionCommand command,
            VehicleState state,
            Vec3 forceBody,
            Vec3 torqueBody)
        {
            TimestampUtc = timestampUtc;
            Frame        = frame;
            Insights     = insights;
            Command      = command;
            State        = state;
            ForceBody    = forceBody;
            TorqueBody   = torqueBody;
        }

        // Geriye dönük uyum: Force/Torque verilmezse 0 kabul edilir
        public FeedbackRecord(
            DateTime timestampUtc,
            FusedFrame frame,
            Insights insights,
            DecisionCommand command,
            VehicleState state)
            : this(timestampUtc, frame, insights, command, state, new Vec3(0, 0, 0), new Vec3(0, 0, 0)) { }

        // Geriye dönük uyum: State de yoksa Frame’den üret
        public FeedbackRecord(
            DateTime timestampUtc,
            FusedFrame frame,
            Insights insights,
            DecisionCommand command)
            : this(timestampUtc, frame, insights, command,
                   SynthesizeStateFromFrame(frame),
                   new Vec3(0, 0, 0),
                   new Vec3(0, 0, 0)) { }

        private static VehicleState SynthesizeStateFromFrame(FusedFrame frame)
        {
            var vs = VehicleState.Zero;
            return vs with
            {
                Position    = new Vec3(frame.Position.X, frame.Position.Y, vs.Position.Z),
                Orientation = new Orientation(vs.Orientation.RollDeg, vs.Orientation.PitchDeg, frame.HeadingDeg)
            };
        }
    }
}
