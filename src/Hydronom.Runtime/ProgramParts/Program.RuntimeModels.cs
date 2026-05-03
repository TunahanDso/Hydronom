using System;
using System.Diagnostics;
using Hydronom.Core.Domain;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Actuators;

partial class Program
{
    /// <summary>
    /// Runtime mod / logging / simÃ¼lasyon seÃ§enekleri.
    /// Program.cs iÃ§inde daÄŸÄ±nÄ±k duran bool/int/string ayarlarÄ±nÄ± tek paket halinde taÅŸÄ±r.
    /// </summary>
    private readonly record struct RuntimeOptions(
        bool DevMode,
        bool SimMode,
        bool AllowExternalPoseOverrideInSim,
        bool UseSyntheticStateWhenNoExternal,
        bool EnableNativeTick,
        string LogMode,
        bool LogVerbose,
        int LoopLogEvery,
        int HeartbeatEvery
    );

    /// <summary>
    /// 6-DoF synthetic physics entegrasyonu iÃ§in kullanÄ±lan fizik parametreleri.
    /// </summary>
    private readonly record struct PhysicsOptions(
        double MassKg,
        Vec3 Inertia,
        Vec3 LinearDragBody,
        Vec3 QuadraticDragBody,
        Vec3 AngularLinearDragBody,
        Vec3 AngularQuadraticDragBody,
        double MaxSyntheticLinearSpeed,
        double MaxSyntheticAngularSpeedDeg
    );

    /// <summary>
    /// External pose reconciliation ayarlarÄ±.
    /// </summary>
    private readonly record struct ExternalPoseOptions(
        bool PreferExternalConfig,
        bool PreferExternalEffective,
        double VelocityBlend,
        double YawRateBlend,
        bool ResetVelocityOnTeleport,
        double TeleportDistanceM,
        double TeleportYawDeg
    );

    /// <summary>
    /// External pose geÃ§miÅŸi.
    /// Runtime frame'lerinden gelen pose bilgisinden velocity/yaw-rate tÃ¼retmek iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    private struct ExternalPoseState
    {
        public bool HasPrevious;
        public double PreviousX;
        public double PreviousY;
        public double PreviousYawDeg;
        public DateTime PreviousUtc;
        public long LastBlockedLogTick;
    }

    /// <summary>
    /// Runtime ana dÃ¶ngÃ¼sÃ¼nde taÅŸÄ±nan mutable durum.
    /// BÃ¼yÃ¼k Program.cs iÃ§inde daÄŸÄ±nÄ±k duran flag/counter/state deÄŸerlerini gruplar.
    /// </summary>
    private struct LoopRuntimeState
    {
        public long TickIndex;
        public long PeriodTicks;
        public long NextLoopTicks;
        public bool LoggedSyntheticStateNotice;
        public bool EstopTaskCleared;
        public string? LastTaskSignature;

        public static LoopRuntimeState Create(int tickMs)
        {
            return new LoopRuntimeState
            {
                TickIndex = 0,
                PeriodTicks = ComputePeriodTicks(tickMs),
                NextLoopTicks = Stopwatch.GetTimestamp(),
                LoggedSyntheticStateNotice = false,
                EstopTaskCleared = false,
                LastTaskSignature = null
            };
        }
    }

    /// <summary>
    /// Runtime'Ä±n bir dÃ¶ngÃ¼de Ã¼rettiÄŸi karar/komut baÄŸlamÄ±.
    /// Log, limiter, actuator ve feedback aÅŸamalarÄ± bu paketi kullanabilir.
    /// </summary>
    private readonly record struct ControlSelectionResult(
        DecisionCommand DesiredCommand,
        string ControlMode,
        AdvancedDecisionReport DecisionReport,
        bool EstopTaskCleared
    );

    /// <summary>
    /// Bir dÃ¶ngÃ¼de loglanacak hedef/telemetry yardÄ±mcÄ± deÄŸerleri.
    /// </summary>
    private readonly record struct TargetTelemetrySnapshot(
        double DistanceToTargetM,
        double DeltaHeadingDeg,
        string TaskInfoInline,
        AdvancedTaskReport TaskReport
    );

    /// <summary>
    /// Loop log / heartbeat iÃ§in ortak telemetry paketi.
    /// </summary>
    private readonly record struct RuntimeDiagnosticsSnapshot(
        string ControlMode,
        TargetTelemetrySnapshot TargetTelemetry,
        AdvancedAnalysisReport AnalysisReport,
        AdvancedDecisionReport DecisionReport,
        SafetyLimitReport LimitReport,
        ActuatorAllocationReport AllocationReport,
        LimitFlags LimitFlags
    );
}
