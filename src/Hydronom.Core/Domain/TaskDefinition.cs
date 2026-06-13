using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Görevün genel sınıfı.
    ///
    /// Bu enum, görev semantiğini string/name üzerinden tahmin etmeyi bitirir.
    /// Task Manager artık "hold", "wait", "photo" gibi kelimeler aramaz.
    /// Görev niyeti yapısal olarak burada tanımlanır.
    /// </summary>
    public enum TaskKind
    {
        Unknown = 0,

        GoToWaypoint = 10,
        FollowRoute = 11,
        HoldStation = 12,
        Wait = 13,
        ReturnHome = 14,

        FlyThroughGate = 30,
        OrbitObject = 31,
        InspectObject = 32,
        CaptureImage = 33,
        CaptureVideo = 34,
        FollowPipe = 35,
        FollowLine = 36,
        SearchArea = 37,
        DropPayload = 38,

        Surface = 50,
        DiveToDepth = 51,
        MaintainDepth = 52,

        Rendezvous = 70,
        MaintainFormation = 71,
        CooperateWithFleet = 72,
        WaitForPartner = 73,
        RestoreFormation = 74,

        MonitorBattery = 90,
        MonitorGeofence = 91,
        MonitorCollisionRisk = 92,
        MonitorCommunication = 93,
        MonitorVehicleHealth = 94,

        EmergencyStop = 120,
        AbortMission = 121
    }

    /// <summary>
    /// Görev tamamlanma yetkisinin kimde olduğunu belirtir.
    ///
    /// TaskManager:
    /// - Normal görevlerde kullanılır.
    /// - TaskManager completion criteria sağlandığında görevi tamamlayabilir.
    ///
    /// ExternalScenario:
    /// - Scenario / parkur / yarış objective görevlerinde kullanılır.
    /// - TaskManager hedefe yaklaştı diye görevi erken temizlemez.
    /// - Objective gerçekten tamamlandı mı kararını dış sistem verir.
    ///
    /// ExternalFleet:
    /// - Fleet/cooperative görevlerde dış koordinasyon katmanı completion yetkisine sahip olabilir.
    ///
    /// ExternalOperator:
    /// - Manuel/operator onayı gereken görevlerde kullanılır.
    /// </summary>
    public enum TaskCompletionAuthority
    {
        TaskManager = 0,
        ExternalScenario = 1,
        ExternalFleet = 2,
        ExternalOperator = 3
    }

    public enum TaskPriority
    {
        Optional = 0,
        Low = 10,
        Normal = 20,
        MissionCritical = 40,
        SafetyCritical = 80,
        Emergency = 100
    }

    public enum TaskCompletionMode
    {
        None = 0,

        DistanceToTarget = 10,
        RouteCompleted = 11,
        HoldDurationSatisfied = 12,
        ExternalConfirmation = 13,

        GatePlaneCrossed = 30,
        OrbitAngleCompleted = 31,
        ImageCapturedAndValidated = 32,
        InspectionDataCollected = 33,
        SearchCoverageSatisfied = 34,
        PayloadReleased = 35,

        DepthReached = 50,
        Surfaced = 51,

        FleetConditionSatisfied = 70,
        GuardConditionTriggered = 90,
        OperatorConfirmed = 100
    }

    public enum TaskOrbitDirection
    {
        Auto = 0,
        Clockwise = 1,
        CounterClockwise = 2
    }

    public enum TaskFormationRole
    {
        None = 0,
        Leader = 1,
        Follower = 2,
        Support = 3,
        Observer = 4
    }

    public enum TaskFormationShape
    {
        None = 0,
        Convoy = 1,
        SideBySide = 2,
        SearchGrid = 3,
        Ring = 4,
        Custom = 100
    }

    /// <summary>
    /// Görev davranış bayrakları.
    ///
    /// Bunlar Task Manager'ın görev orkestrasyonu yapması içindir.
    /// Heading, force, trajectory veya obstacle-bypass üretmez.
    /// </summary>
    public sealed record TaskBehaviorFlags(
        bool IsStationKeeping = false,
        bool AllowsParallelExecution = false,
        bool CanGenerateSubtasks = false,
        bool RequiresStablePose = false,
        bool RequiresTargetObject = false,
        bool RequiresSensorConfirmation = false,
        bool MayPausePrimaryTask = false,
        bool IsSafetyCritical = false,
        bool IsGuardTask = false,
        bool IsFleetTask = false)
    {
        public static TaskBehaviorFlags Default { get; } = new();

        public static TaskBehaviorFlags ForKind(TaskKind kind)
        {
            return kind switch
            {
                TaskKind.HoldStation => new(
                    IsStationKeeping: true,
                    RequiresStablePose: true),

                TaskKind.Wait => new(
                    IsStationKeeping: true),

                TaskKind.CaptureImage => new(
                    AllowsParallelExecution: true,
                    CanGenerateSubtasks: true,
                    RequiresStablePose: true,
                    RequiresTargetObject: true,
                    RequiresSensorConfirmation: true),

                TaskKind.CaptureVideo => new(
                    AllowsParallelExecution: true,
                    RequiresTargetObject: false,
                    RequiresSensorConfirmation: true),

                TaskKind.InspectObject => new(
                    CanGenerateSubtasks: true,
                    RequiresStablePose: true,
                    RequiresTargetObject: true,
                    RequiresSensorConfirmation: true),

                TaskKind.OrbitObject => new(
                    CanGenerateSubtasks: true,
                    RequiresTargetObject: true,
                    RequiresSensorConfirmation: true),

                TaskKind.FlyThroughGate => new(
                    CanGenerateSubtasks: true,
                    RequiresTargetObject: true,
                    RequiresSensorConfirmation: true),

                TaskKind.SearchArea => new(
                    AllowsParallelExecution: false,
                    CanGenerateSubtasks: true,
                    RequiresSensorConfirmation: true),

                TaskKind.FollowPipe or TaskKind.FollowLine => new(
                    CanGenerateSubtasks: true,
                    RequiresSensorConfirmation: true),

                TaskKind.Rendezvous or
                TaskKind.MaintainFormation or
                TaskKind.CooperateWithFleet or
                TaskKind.WaitForPartner or
                TaskKind.RestoreFormation => new(
                    AllowsParallelExecution: true,
                    CanGenerateSubtasks: true,
                    MayPausePrimaryTask: true,
                    IsFleetTask: true),

                TaskKind.MonitorBattery or
                TaskKind.MonitorGeofence or
                TaskKind.MonitorCollisionRisk or
                TaskKind.MonitorCommunication or
                TaskKind.MonitorVehicleHealth => new(
                    AllowsParallelExecution: true,
                    IsSafetyCritical: true,
                    IsGuardTask: true),

                TaskKind.EmergencyStop or
                TaskKind.AbortMission => new(
                    MayPausePrimaryTask: true,
                    IsSafetyCritical: true,
                    IsGuardTask: true),

                TaskKind.Surface or
                TaskKind.DiveToDepth or
                TaskKind.MaintainDepth => new(
                    CanGenerateSubtasks: true,
                    RequiresStablePose: false),

                _ => Default
            };
        }
    }

    /// <summary>
    /// Görevün ne zaman tamamlanmış sayılacağını tanımlar.
    ///
    /// Bu yapı Task Manager'a completion kuralı verir.
    /// Sürüş hedefi, heading veya kuvvet üretmez.
    /// </summary>
    public sealed record TaskCompletionCriteria
    {
        public TaskCompletionMode Mode { get; init; } = TaskCompletionMode.DistanceToTarget;

        public double AcceptanceRadiusM { get; init; } = 0.75;

        public double RequiredHoldSeconds { get; init; } = 0.0;

        public double RequiredOrbitTurns { get; init; } = 1.0;

        public double RequiredSearchCoverage01 { get; init; } = 1.0;

        public double RequiredImageQuality01 { get; init; } = 0.70;

        public int RequiredValidSensorFrames { get; init; } = 1;

        public bool RequiresExternalAck { get; init; } = false;

        public static TaskCompletionCriteria Default { get; } = new();

        public static TaskCompletionCriteria ForKind(TaskKind kind)
        {
            return kind switch
            {
                TaskKind.GoToWaypoint => new()
                {
                    Mode = TaskCompletionMode.DistanceToTarget,
                    AcceptanceRadiusM = 0.75
                },

                TaskKind.FollowRoute => new()
                {
                    Mode = TaskCompletionMode.RouteCompleted,
                    AcceptanceRadiusM = 0.75
                },

                TaskKind.HoldStation => new()
                {
                    Mode = TaskCompletionMode.HoldDurationSatisfied,
                    AcceptanceRadiusM = 0.75,
                    RequiredHoldSeconds = 3.0
                },

                TaskKind.Wait => new()
                {
                    Mode = TaskCompletionMode.HoldDurationSatisfied,
                    AcceptanceRadiusM = 9999.0,
                    RequiredHoldSeconds = 1.0
                },

                TaskKind.FlyThroughGate => new()
                {
                    Mode = TaskCompletionMode.GatePlaneCrossed,
                    RequiresExternalAck = true
                },

                TaskKind.OrbitObject => new()
                {
                    Mode = TaskCompletionMode.OrbitAngleCompleted,
                    RequiredOrbitTurns = 1.0,
                    AcceptanceRadiusM = 1.5
                },

                TaskKind.CaptureImage => new()
                {
                    Mode = TaskCompletionMode.ImageCapturedAndValidated,
                    RequiredImageQuality01 = 0.70,
                    RequiredValidSensorFrames = 1
                },

                TaskKind.InspectObject => new()
                {
                    Mode = TaskCompletionMode.InspectionDataCollected,
                    RequiredValidSensorFrames = 5
                },

                TaskKind.SearchArea => new()
                {
                    Mode = TaskCompletionMode.SearchCoverageSatisfied,
                    RequiredSearchCoverage01 = 0.90
                },

                TaskKind.DropPayload => new()
                {
                    Mode = TaskCompletionMode.PayloadReleased,
                    RequiresExternalAck = true
                },

                TaskKind.Surface => new()
                {
                    Mode = TaskCompletionMode.Surfaced
                },

                TaskKind.DiveToDepth or TaskKind.MaintainDepth => new()
                {
                    Mode = TaskCompletionMode.DepthReached,
                    AcceptanceRadiusM = 0.25
                },

                TaskKind.Rendezvous or
                TaskKind.MaintainFormation or
                TaskKind.CooperateWithFleet or
                TaskKind.WaitForPartner or
                TaskKind.RestoreFormation => new()
                {
                    Mode = TaskCompletionMode.FleetConditionSatisfied,
                    RequiresExternalAck = true
                },

                TaskKind.MonitorBattery or
                TaskKind.MonitorGeofence or
                TaskKind.MonitorCollisionRisk or
                TaskKind.MonitorCommunication or
                TaskKind.MonitorVehicleHealth => new()
                {
                    Mode = TaskCompletionMode.GuardConditionTriggered
                },

                TaskKind.EmergencyStop or TaskKind.AbortMission => new()
                {
                    Mode = TaskCompletionMode.ExternalConfirmation,
                    RequiresExternalAck = true
                },

                _ => Default
            };
        }

        public TaskCompletionCriteria Sanitized()
        {
            return this with
            {
                AcceptanceRadiusM = SanitizePositive(AcceptanceRadiusM, 0.75, 0.01),
                RequiredHoldSeconds = SanitizeNonNegative(RequiredHoldSeconds),
                RequiredOrbitTurns = SanitizePositive(RequiredOrbitTurns, 1.0, 0.01),
                RequiredSearchCoverage01 = Clamp01(RequiredSearchCoverage01),
                RequiredImageQuality01 = Clamp01(RequiredImageQuality01),
                RequiredValidSensorFrames = Math.Max(0, RequiredValidSensorFrames)
            };
        }

        private static double SanitizePositive(double value, double fallback, double min)
        {
            if (!double.IsFinite(value))
                return Math.Max(min, fallback);

            return Math.Max(min, value);
        }

        private static double SanitizeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Max(0.0, value);
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Görev zaman politikası.
    /// Global Task Manager timeout ayarlarıyla birlikte kullanılabilir.
    /// </summary>
    public sealed record TaskTimingPolicy
    {
        public double? TimeoutSeconds { get; init; }
        public double? MaxNoProgressSeconds { get; init; }
        public double? MaxObstacleHoldSeconds { get; init; }
        public double? StartDelaySeconds { get; init; }

        public static TaskTimingPolicy Default { get; } = new();

        public TaskTimingPolicy Sanitized()
        {
            return this with
            {
                TimeoutSeconds = SanitizeNullablePositive(TimeoutSeconds),
                MaxNoProgressSeconds = SanitizeNullablePositive(MaxNoProgressSeconds),
                MaxObstacleHoldSeconds = SanitizeNullablePositive(MaxObstacleHoldSeconds),
                StartDelaySeconds = SanitizeNullableNonNegative(StartDelaySeconds)
            };
        }

        private static double? SanitizeNullablePositive(double? value)
        {
            if (value is null)
                return null;

            if (!double.IsFinite(value.Value))
                return null;

            return Math.Max(0.001, value.Value);
        }

        private static double? SanitizeNullableNonNegative(double? value)
        {
            if (value is null)
                return null;

            if (!double.IsFinite(value.Value))
                return null;

            return Math.Max(0.0, value.Value);
        }
    }

    /// <summary>
    /// Görevün mekansal parametreleri.
    ///
    /// Bu alanlar görev niyetini tanımlar.
    /// Planner/Trajectory bu niyetten güvenli yol üretir.
    /// TaskDefinition doğrudan sürüş referansı üretmez.
    /// </summary>
    public sealed record TaskSpatialPolicy
    {
        public double? PreferredApproachDistanceM { get; init; }
        public double? MinApproachDistanceM { get; init; }
        public double? MaxApproachDistanceM { get; init; }

        public double? OrbitRadiusM { get; init; }
        public TaskOrbitDirection OrbitDirection { get; init; } = TaskOrbitDirection.Auto;
        public double? OrbitSpeedLimitMps { get; init; }

        public double? DesiredDepthM { get; init; }
        public double? DepthToleranceM { get; init; }

        public double? MaxSpeedMps { get; init; }

        public Vec3? AreaCenter { get; init; }
        public double? AreaRadiusM { get; init; }
        public List<Vec3> AreaPolygon { get; init; } = new();

        public static TaskSpatialPolicy Default { get; } = new();

        public TaskSpatialPolicy Sanitized()
        {
            return this with
            {
                PreferredApproachDistanceM = SanitizeNullablePositive(PreferredApproachDistanceM),
                MinApproachDistanceM = SanitizeNullableNonNegative(MinApproachDistanceM),
                MaxApproachDistanceM = SanitizeNullablePositive(MaxApproachDistanceM),
                OrbitRadiusM = SanitizeNullablePositive(OrbitRadiusM),
                OrbitSpeedLimitMps = SanitizeNullablePositive(OrbitSpeedLimitMps),
                DesiredDepthM = SanitizeNullableNonNegative(DesiredDepthM),
                DepthToleranceM = SanitizeNullablePositive(DepthToleranceM),
                MaxSpeedMps = SanitizeNullablePositive(MaxSpeedMps),
                AreaRadiusM = SanitizeNullablePositive(AreaRadiusM)
            };
        }

        private static double? SanitizeNullablePositive(double? value)
        {
            if (value is null)
                return null;

            if (!double.IsFinite(value.Value))
                return null;

            return Math.Max(0.001, value.Value);
        }

        private static double? SanitizeNullableNonNegative(double? value)
        {
            if (value is null)
                return null;

            if (!double.IsFinite(value.Value))
                return null;

            return Math.Max(0.0, value.Value);
        }
    }

    /// <summary>
    /// Görevün sensör/algı beklentisi.
    /// </summary>
    public sealed record TaskSensorRequirement
    {
        public string? RequiredSensorMode { get; init; }

        public string? RequiredTargetClass { get; init; }

        public double MinConfidence01 { get; init; } = 0.50;

        public int RequiredConsecutiveFrames { get; init; } = 1;

        public bool AllowSimulatedSensor { get; init; } = true;

        public bool AllowRealSensor { get; init; } = true;

        public static TaskSensorRequirement None { get; } = new();

        public TaskSensorRequirement Sanitized()
        {
            return this with
            {
                MinConfidence01 = Clamp01(MinConfidence01),
                RequiredConsecutiveFrames = Math.Max(0, RequiredConsecutiveFrames)
            };
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Fleet / çok araçlı görev parametreleri.
    /// </summary>
    public sealed record TaskFleetRequirement
    {
        public string? PartnerVehicleId { get; init; }

        public TaskFormationRole Role { get; init; } = TaskFormationRole.None;

        public TaskFormationShape Formation { get; init; } = TaskFormationShape.None;

        public double DesiredSeparationM { get; init; } = 3.0;

        public double CommunicationTimeoutSeconds { get; init; } = 5.0;

        public string? SharedObjectiveId { get; init; }

        public static TaskFleetRequirement None { get; } = new();

        public TaskFleetRequirement Sanitized()
        {
            return this with
            {
                DesiredSeparationM = SanitizePositive(DesiredSeparationM, 3.0, 0.1),
                CommunicationTimeoutSeconds = SanitizePositive(CommunicationTimeoutSeconds, 5.0, 0.1)
            };
        }

        private static double SanitizePositive(double value, double fallback, double min)
        {
            if (!double.IsFinite(value))
                return Math.Max(min, fallback);

            return Math.Max(min, value);
        }
    }

    /// <summary>
    /// Görev tanımı.
    ///
    /// Bu model yalnızca "ne yapılacağını" tanımlar.
    /// "Nasıl sürüleceğini" Planner + Trajectory + Controller çözer.
    ///
    /// Geriye dönük uyumluluk korunmuştur:
    /// - Name
    /// - Target
    /// - Waypoints
    /// - HoldOnArrive
    /// - WaitSecondsPerPoint
    /// - Loop
    /// - CompletionAuthority
    /// - ExternalOwnerId
    /// - ExternalObjectiveId
    /// </summary>
    public record TaskDefinition(string Name, Vec3? Target)
    {
        public TaskKind Kind { get; set; } =
            Target is null ? TaskKind.Unknown : TaskKind.GoToWaypoint;

        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        public List<Vec3> Waypoints { get; set; } = new();

        public bool HoldOnArrive { get; set; } = false;

        public double WaitSecondsPerPoint { get; set; } = 0.0;

        public bool Loop { get; set; } = false;

        public TaskBehaviorFlags Behavior { get; set; } = TaskBehaviorFlags.Default;

        public TaskCompletionCriteria Completion { get; set; } = TaskCompletionCriteria.Default;

        public TaskTimingPolicy Timing { get; set; } = TaskTimingPolicy.Default;

        public TaskSpatialPolicy Spatial { get; set; } = TaskSpatialPolicy.Default;

        public TaskSensorRequirement SensorRequirement { get; set; } = TaskSensorRequirement.None;

        public TaskFleetRequirement FleetRequirement { get; set; } = TaskFleetRequirement.None;

        /// <summary>
        /// World Model / Perception tarafındaki hedef nesne kimliği.
        /// Örnek: buoy_3, gate_1, pipe_track_2.
        /// </summary>
        public string? TargetObjectId { get; set; }

        public string? TargetObjectKind { get; set; }

        public string? TargetObjectRole { get; set; }

        public TaskCompletionAuthority CompletionAuthority { get; set; } =
            TaskCompletionAuthority.TaskManager;

        public string? ExternalOwnerId { get; set; }

        public string? ExternalObjectiveId { get; set; }

        public Dictionary<string, string> Tags { get; set; } = new();

        public bool IsExternallyCompleted =>
            CompletionAuthority != TaskCompletionAuthority.TaskManager;

        public bool HasTarget =>
            Target is not null ||
            Waypoints.Count > 0 ||
            !string.IsNullOrWhiteSpace(TargetObjectId);

        public bool IsStationKeeping =>
            Kind is TaskKind.HoldStation or TaskKind.Wait or TaskKind.MaintainDepth ||
            Behavior.IsStationKeeping ||
            HoldOnArrive;

        public bool AllowsParallelExecution =>
            Behavior.AllowsParallelExecution;

        public bool CanGenerateSubtasks =>
            Behavior.CanGenerateSubtasks;

        public bool IsGuardTask =>
            Behavior.IsGuardTask;

        public bool IsFleetTask =>
            Behavior.IsFleetTask;

        public TaskDefinition Normalize()
        {
            if (Kind == TaskKind.Unknown)
            {
                if (Waypoints.Count > 0)
                    Kind = TaskKind.FollowRoute;
                else if (Target is not null)
                    Kind = TaskKind.GoToWaypoint;
            }

            if (Behavior == TaskBehaviorFlags.Default)
                Behavior = TaskBehaviorFlags.ForKind(Kind);

            if (Completion == TaskCompletionCriteria.Default)
                Completion = TaskCompletionCriteria.ForKind(Kind);

            Completion = Completion.Sanitized();
            Timing = Timing.Sanitized();
            Spatial = Spatial.Sanitized();
            SensorRequirement = SensorRequirement.Sanitized();
            FleetRequirement = FleetRequirement.Sanitized();

            WaitSecondsPerPoint = SanitizeNonNegative(WaitSecondsPerPoint);

            return this;
        }

        public static TaskDefinition GoTo(string name, Vec3 target, bool holdOnArrive = false)
        {
            var task = new TaskDefinition(name, target)
            {
                Kind = TaskKind.GoToWaypoint,
                HoldOnArrive = holdOnArrive,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.GoToWaypoint),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.GoToWaypoint)
            };

            return task.Normalize();
        }

        public static TaskDefinition ScenarioGoTo(
            string name,
            Vec3 target,
            string scenarioId,
            string objectiveId,
            bool holdOnArrive = false)
        {
            var task = new TaskDefinition(name, target)
            {
                Kind = TaskKind.GoToWaypoint,
                HoldOnArrive = holdOnArrive,
                CompletionAuthority = TaskCompletionAuthority.ExternalScenario,
                ExternalOwnerId = scenarioId,
                ExternalObjectiveId = objectiveId,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.GoToWaypoint),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.GoToWaypoint) with
                {
                    RequiresExternalAck = true
                }
            };

            return task.Normalize();
        }

        public static TaskDefinition Route(
            string name,
            IEnumerable<Vec3> waypoints,
            bool loop = false,
            bool holdOnArrive = false,
            double waitSecondsPerPoint = 0.0)
        {
            if (waypoints is null)
                throw new ArgumentNullException(nameof(waypoints));

            var task = new TaskDefinition(name, null)
            {
                Kind = TaskKind.FollowRoute,
                Loop = loop,
                HoldOnArrive = holdOnArrive,
                WaitSecondsPerPoint = waitSecondsPerPoint,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.FollowRoute),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.FollowRoute)
            };

            task.Waypoints.AddRange(waypoints);
            return task.Normalize();
        }

        public static TaskDefinition HoldStation(
            string name,
            Vec3 target,
            double holdSeconds,
            double acceptanceRadiusM = 0.75)
        {
            var task = new TaskDefinition(name, target)
            {
                Kind = TaskKind.HoldStation,
                HoldOnArrive = true,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.HoldStation),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.HoldStation) with
                {
                    RequiredHoldSeconds = holdSeconds,
                    AcceptanceRadiusM = acceptanceRadiusM
                }
            };

            return task.Normalize();
        }

        public static TaskDefinition CaptureImage(
            string name,
            string targetObjectId,
            double minQuality01 = 0.70)
        {
            var task = new TaskDefinition(name, null)
            {
                Kind = TaskKind.CaptureImage,
                TargetObjectId = targetObjectId,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.CaptureImage),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.CaptureImage) with
                {
                    RequiredImageQuality01 = minQuality01
                }
            };

            return task.Normalize();
        }

        public static TaskDefinition OrbitObject(
            string name,
            string targetObjectId,
            double radiusM,
            TaskOrbitDirection direction = TaskOrbitDirection.Auto,
            double requiredTurns = 1.0)
        {
            var task = new TaskDefinition(name, null)
            {
                Kind = TaskKind.OrbitObject,
                TargetObjectId = targetObjectId,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.OrbitObject),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.OrbitObject) with
                {
                    RequiredOrbitTurns = requiredTurns
                },
                Spatial = TaskSpatialPolicy.Default with
                {
                    OrbitRadiusM = radiusM,
                    OrbitDirection = direction
                }
            };

            return task.Normalize();
        }

        public static TaskDefinition MaintainFormation(
            string name,
            string partnerVehicleId,
            TaskFormationRole role,
            TaskFormationShape formation,
            double desiredSeparationM,
            string? sharedObjectiveId = null)
        {
            var task = new TaskDefinition(name, null)
            {
                Kind = TaskKind.MaintainFormation,
                Behavior = TaskBehaviorFlags.ForKind(TaskKind.MaintainFormation),
                Completion = TaskCompletionCriteria.ForKind(TaskKind.MaintainFormation),
                FleetRequirement = new TaskFleetRequirement
                {
                    PartnerVehicleId = partnerVehicleId,
                    Role = role,
                    Formation = formation,
                    DesiredSeparationM = desiredSeparationM,
                    SharedObjectiveId = sharedObjectiveId
                }
            };

            return task.Normalize();
        }

        public TaskDefinition WithExternalScenarioCompletion(
            string scenarioId,
            string objectiveId)
        {
            CompletionAuthority = TaskCompletionAuthority.ExternalScenario;
            ExternalOwnerId = scenarioId;
            ExternalObjectiveId = objectiveId;
            Completion = Completion with
            {
                RequiresExternalAck = true
            };

            return Normalize();
        }

        public TaskDefinition WithExternalFleetCompletion(
            string fleetOperationId,
            string objectiveId)
        {
            CompletionAuthority = TaskCompletionAuthority.ExternalFleet;
            ExternalOwnerId = fleetOperationId;
            ExternalObjectiveId = objectiveId;
            Completion = Completion with
            {
                RequiresExternalAck = true
            };

            return Normalize();
        }

        public TaskDefinition WithTargetObject(
            string targetObjectId,
            string? kind = null,
            string? role = null)
        {
            TargetObjectId = targetObjectId;
            TargetObjectKind = kind;
            TargetObjectRole = role;
            return Normalize();
        }

        public TaskDefinition WithPriority(TaskPriority priority)
        {
            Priority = priority;
            return this;
        }

        public TaskDefinition WithTag(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key))
                Tags[key.Trim()] = value ?? string.Empty;

            return this;
        }

        private static double SanitizeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Max(0.0, value);
        }
    }
}