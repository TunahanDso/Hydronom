using System;
using System.Diagnostics;
using Hydronom.Core.Domain;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Actuators;

partial class Program
{
    /// <summary>
    /// Runtime mod / logging / simülasyon seçenekleri.
    /// Program.cs içinde dağınık duran bool/int/string ayarlarını tek paket halinde taşır.
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
    /// 6-DoF synthetic physics entegrasyonu için kullanılan fizik parametreleri.
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
    /// External pose reconciliation ayarları.
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
    /// External pose geçmişi.
    /// Runtime frame'lerinden gelen pose bilgisinden velocity/yaw-rate türetmek için kullanılır.
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
    /// Runtime ana döngüsünde taşınan mutable durum.
    /// Büyük Program.cs içinde dağınık duran flag/counter/state değerlerini gruplar.
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
    /// Runtime'ın bir döngüde ürettiği karar/komut bağlamı.
    /// Log, limiter, actuator ve feedback aşamaları bu paketi kullanabilir.
    /// </summary>
    private readonly record struct ControlSelectionResult(
        DecisionCommand DesiredCommand,
        string ControlMode,
        AdvancedDecisionReport DecisionReport,
        bool EstopTaskCleared
    );

    /// <summary>
    /// Bir döngüde loglanacak hedef/telemetry yardımcı değerleri.
    /// </summary>
    private readonly record struct TargetTelemetrySnapshot(
        double DistanceToTargetM,
        double DeltaHeadingDeg,
        string TaskInfoInline,
        AdvancedTaskReport TaskReport
    );

    /// <summary>
    /// Loop log / heartbeat için ortak telemetry paketi.
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