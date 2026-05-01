using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.Coordination;
using Hydronom.GroundStation.Routing;
using Hydronom.GroundStation.Telemetry;
using Hydronom.GroundStation.TransportExecution;
using Hydronom.GroundStation.WorldModel;
using Hydronom.GroundStation.Transports;
using Hydronom.Runtime.Fleet;

Console.WriteLine("=== Hydronom Ground Station Smoke Test ===");
Console.WriteLine();

var ground = new GroundStationEngine();

var alphaIdentity = new NodeIdentity
{
    NodeId = "VEHICLE-ALPHA-001",
    DisplayName = "Alpha",
    NodeType = "Vehicle",
    VehicleType = "SurfaceVessel",
    Role = "Leader",
    SoftwareVersion = "fleet-v1-dev",
    HardwareProfile = "Simulation",
    IsSimulation = true
};

var alphaAgent = new VehicleFleetAgent(alphaIdentity);

var heartbeatEnvelope = alphaAgent.CreateHeartbeatEnvelope(
    mode: "Autonomous",
    health: "OK",
    batteryPercent: 82,
    activeMissionId: "MISSION-SMOKE-001",
    missionState: "Running",
    latitude: 41.025,
    longitude: 29.015,
    headingDeg: 72.5,
    speedMps: 1.35,
    availableTransports: new[]
    {
        TransportKind.Tcp,
        TransportKind.WebSocket,
        TransportKind.Mock
    },
    capabilities: new[]
    {
        new VehicleCapability
        {
            Name = "navigation",
            Description = "Autonomous navigation capability",
            IsEnabled = true,
            Health = "OK",
            IsSimulated = true
        },
        new VehicleCapability
        {
            Name = "mapping",
            Description = "Simulated mapping capability",
            IsEnabled = true,
            Health = "OK",
            IsSimulated = true
        },
        new VehicleCapability
        {
            Name = "fleet_heartbeat",
            Description = "Can announce itself to Ground Station",
            IsEnabled = true,
            Health = "OK",
            IsSimulated = true,
            RelatedTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.Mock
            }
        }
    },
    metadata: new Dictionary<string, string>
    {
        ["runtimeHz"] = "20",
        ["source"] = "smoke_test",
        ["note"] = "First Fleet & Ground Station heartbeat test"
    });

Console.WriteLine("[1] VehicleFleetAgent produced heartbeat envelope:");
Console.WriteLine($"    MessageType : {heartbeatEnvelope.MessageType}");
Console.WriteLine($"    Source      : {heartbeatEnvelope.SourceNodeId}");
Console.WriteLine($"    Target      : {heartbeatEnvelope.TargetNodeId}");
Console.WriteLine($"    Priority    : {heartbeatEnvelope.Priority}");
Console.WriteLine();

var handled = ground.HandleEnvelope(heartbeatEnvelope);

Console.WriteLine("[2] GroundStationEngine handled heartbeat envelope:");
Console.WriteLine($"    Handled     : {handled}");
Console.WriteLine();

var snapshot = ground.GetFleetSnapshot();

Console.WriteLine("[3] FleetRegistry snapshot:");
Console.WriteLine($"    Node count  : {snapshot.Count}");
Console.WriteLine();

foreach (var node in snapshot)
{
    Console.WriteLine($"    NodeId      : {node.Identity.NodeId}");
    Console.WriteLine($"    Name        : {node.Identity.DisplayName}");
    Console.WriteLine($"    Type        : {node.Identity.VehicleType}");
    Console.WriteLine($"    Role        : {node.Identity.Role}");
    Console.WriteLine($"    Online      : {node.IsOnline}");
    Console.WriteLine($"    Health      : {node.Health}");
    Console.WriteLine($"    Battery     : {node.BatteryPercent}%");
    Console.WriteLine($"    Mission     : {node.ActiveMissionId} / {node.MissionState}");
    Console.WriteLine($"    Position    : {node.Latitude}, {node.Longitude}");
    Console.WriteLine($"    Heading     : {node.HeadingDeg} deg");
    Console.WriteLine($"    Speed       : {node.SpeedMps} m/s");
    Console.WriteLine($"    Transports  : {string.Join(", ", node.AvailableTransports)}");
    Console.WriteLine($"    Capabilities: {string.Join(", ", node.Capabilities.Select(x => x.Name))}");
    Console.WriteLine();
}

Console.WriteLine("[4] Tracked FleetCommand + multi-stage FleetCommandResult test:");

var command = new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "AssignMission",
    AuthorityLevel = "MissionCommand",
    Priority = MessagePriority.High,
    Args = new Dictionary<string, string>
    {
        ["missionId"] = "MISSION-SMOKE-001",
        ["areaId"] = "TEST-AREA-A"
    },
    IsOperatorIssued = true,
    RequiresResult = true,
    Metadata = new Dictionary<string, string>
    {
        ["operatorName"] = "Tunahan",
        ["sourceScreen"] = "SmokeTest"
    }
};

var commandEnvelope = ground.CreateTrackedCommandEnvelope(command);

Console.WriteLine("    Ground produced tracked command envelope:");
Console.WriteLine($"    CommandId   : {command.CommandId}");
Console.WriteLine($"    Tracked     : {commandEnvelope is not null}");
Console.WriteLine($"    MessageType : {commandEnvelope?.MessageType}");
Console.WriteLine($"    Source      : {commandEnvelope?.SourceNodeId}");
Console.WriteLine($"    Target      : {commandEnvelope?.TargetNodeId}");
Console.WriteLine($"    Priority    : {commandEnvelope?.Priority}");
Console.WriteLine($"    TrackerCount: {ground.CommandTracker.Count}");

var acceptedResult = new FleetCommandResult
{
    CommandId = command.CommandId,
    SourceNodeId = "VEHICLE-ALPHA-001",
    TargetNodeId = "GROUND-001",
    Status = "Accepted",
    Success = true,
    Message = "Mission command accepted by simulated vehicle.",
    ProcessingStage = "DecisionAccepted",
    Metadata = new Dictionary<string, string>
    {
        ["latencyMs"] = "18",
        ["safetyGate"] = "passed",
        ["runtimeMode"] = "Autonomous"
    }
};

var acceptedEnvelope = HydronomEnvelopeFactory.CreateCommandResult(acceptedResult);
var acceptedHandled = ground.HandleEnvelope(acceptedEnvelope);

Console.WriteLine();
Console.WriteLine("    Vehicle returned first command result envelope:");
Console.WriteLine($"    ResultId    : {acceptedResult.ResultId}");
Console.WriteLine($"    MessageType : {acceptedEnvelope.MessageType}");
Console.WriteLine($"    Source      : {acceptedEnvelope.SourceNodeId}");
Console.WriteLine($"    Target      : {acceptedEnvelope.TargetNodeId}");
Console.WriteLine($"    Status      : {acceptedResult.Status}");
Console.WriteLine($"    Success     : {acceptedResult.Success}");
Console.WriteLine($"    Handled     : {acceptedHandled}");

var historyAfterAccepted = ground.GetCommandHistorySnapshot();

Console.WriteLine();
Console.WriteLine("    Command history after Accepted:");
foreach (var record in historyAfterAccepted)
{
    Console.WriteLine($"    CommandId   : {record.Command.CommandId}");
    Console.WriteLine($"    Type        : {record.Command.CommandType}");
    Console.WriteLine($"    HasResult   : {record.HasResult}");
    Console.WriteLine($"    IsPending   : {record.IsPending}");
    Console.WriteLine($"    IsCompleted : {record.IsCompleted}");
    Console.WriteLine($"    IsSuccessful: {record.IsSuccessful}");
    Console.WriteLine($"    LastStatus  : {record.LastResult?.Status}");
    Console.WriteLine($"    LastStage   : {record.LastResult?.ProcessingStage}");
}

var appliedResult = new FleetCommandResult
{
    CommandId = command.CommandId,
    SourceNodeId = "VEHICLE-ALPHA-001",
    TargetNodeId = "GROUND-001",
    Status = "Applied",
    Success = true,
    Message = "Mission command applied by simulated vehicle.",
    ProcessingStage = "ActuationApplied",
    Metadata = new Dictionary<string, string>
    {
        ["latencyMs"] = "42",
        ["safetyGate"] = "passed",
        ["runtimeMode"] = "Autonomous",
        ["actuation"] = "simulated"
    }
};

var appliedEnvelope = HydronomEnvelopeFactory.CreateCommandResult(appliedResult);
var appliedHandled = ground.HandleEnvelope(appliedEnvelope);

Console.WriteLine();
Console.WriteLine("    Vehicle returned final command result envelope:");
Console.WriteLine($"    ResultId    : {appliedResult.ResultId}");
Console.WriteLine($"    MessageType : {appliedEnvelope.MessageType}");
Console.WriteLine($"    Source      : {appliedEnvelope.SourceNodeId}");
Console.WriteLine($"    Target      : {appliedEnvelope.TargetNodeId}");
Console.WriteLine($"    Status      : {appliedResult.Status}");
Console.WriteLine($"    Success     : {appliedResult.Success}");
Console.WriteLine($"    Handled     : {appliedHandled}");
Console.WriteLine();

var commandHistory = ground.GetCommandHistorySnapshot();

Console.WriteLine("    Command history after Applied:");
Console.WriteLine($"    Count       : {commandHistory.Count}");

foreach (var record in commandHistory)
{
    Console.WriteLine($"    CommandId   : {record.Command.CommandId}");
    Console.WriteLine($"    Type        : {record.Command.CommandType}");
    Console.WriteLine($"    Target      : {record.Command.TargetNodeId}");
    Console.WriteLine($"    HasResult   : {record.HasResult}");
    Console.WriteLine($"    IsPending   : {record.IsPending}");
    Console.WriteLine($"    IsCompleted : {record.IsCompleted}");
    Console.WriteLine($"    IsSuccessful: {record.IsSuccessful}");
    Console.WriteLine($"    LastStatus  : {record.LastResult?.Status}");
    Console.WriteLine($"    LastStage   : {record.LastResult?.ProcessingStage}");
}

Console.WriteLine();

Console.WriteLine("[5] Command timeout test:");

var timeoutCommand = new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "SetTarget",
    AuthorityLevel = "MissionCommand",
    Priority = MessagePriority.High,
    Args = new Dictionary<string, string>
    {
        ["lat"] = "41.030",
        ["lon"] = "29.020"
    },
    IsOperatorIssued = true,
    RequiresResult = true
};

var timeoutEnvelope = ground.CreateTrackedCommandEnvelope(timeoutCommand);

Console.WriteLine($"    Timeout command tracked: {timeoutEnvelope is not null}");
Console.WriteLine($"    Pending before timeout : {ground.GetPendingCommandSnapshot().Count}");

var expiredCommands = ground.MarkExpiredCommands(
    timeout: TimeSpan.FromMilliseconds(1),
    nowUtc: DateTimeOffset.UtcNow.AddSeconds(10));

Console.WriteLine($"    Expired changed        : {expiredCommands}");
Console.WriteLine($"    Pending after timeout  : {ground.GetPendingCommandSnapshot().Count}");

var failedCommands = ground.CommandTracker.GetFailedCommands();

Console.WriteLine($"    Failed command count   : {failedCommands.Count}");

Console.WriteLine();

Console.WriteLine("[6] Transport routing policy test:");

var routingPolicy = new TransportRoutingPolicy();

var heartbeatRoute = routingPolicy.Decide(heartbeatEnvelope);

Console.WriteLine("    Heartbeat route:");
Console.WriteLine($"    MessageType : {heartbeatRoute.MessageType}");
Console.WriteLine($"    Reason      : {heartbeatRoute.Reason}");
Console.WriteLine($"    Primary     : {string.Join(", ", heartbeatRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback    : {string.Join(", ", heartbeatRoute.FallbackTransports)}");
Console.WriteLine($"    Ack         : {heartbeatRoute.RequiresAck}");
Console.WriteLine($"    Broadcast   : {heartbeatRoute.BroadcastAllAvailableLinks}");
Console.WriteLine($"    Valid       : {heartbeatRoute.IsValid}");
Console.WriteLine();

var normalCommandRoute = routingPolicy.Decide(commandEnvelope!);

Console.WriteLine("    FleetCommand route:");
Console.WriteLine($"    MessageType : {normalCommandRoute.MessageType}");
Console.WriteLine($"    Reason      : {normalCommandRoute.Reason}");
Console.WriteLine($"    Primary     : {string.Join(", ", normalCommandRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback    : {string.Join(", ", normalCommandRoute.FallbackTransports)}");
Console.WriteLine($"    Ack         : {normalCommandRoute.RequiresAck}");
Console.WriteLine($"    Broadcast   : {normalCommandRoute.BroadcastAllAvailableLinks}");
Console.WriteLine($"    Valid       : {normalCommandRoute.IsValid}");
Console.WriteLine();

var emergencyCommand = new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "BROADCAST",
    CommandType = "EmergencyStop",
    AuthorityLevel = "EmergencyCommand",
    Priority = MessagePriority.Emergency,
    Args = new Dictionary<string, string>
    {
        ["reason"] = "smoke_test_emergency_route"
    },
    IsOperatorIssued = true,
    RequiresResult = true
};

var emergencyEnvelope = HydronomEnvelopeFactory.CreateCommand(emergencyCommand);
var emergencyRoute = routingPolicy.Decide(emergencyEnvelope);

Console.WriteLine("    EmergencyStop route:");
Console.WriteLine($"    MessageType : {emergencyRoute.MessageType}");
Console.WriteLine($"    Reason      : {emergencyRoute.Reason}");
Console.WriteLine($"    Primary     : {string.Join(", ", emergencyRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback    : {string.Join(", ", emergencyRoute.FallbackTransports)}");
Console.WriteLine($"    Ack         : {emergencyRoute.RequiresAck}");
Console.WriteLine($"    Broadcast   : {emergencyRoute.BroadcastAllAvailableLinks}");
Console.WriteLine($"    MaxLatency  : {emergencyRoute.MaxLatency}");
Console.WriteLine($"    Valid       : {emergencyRoute.IsValid}");
Console.WriteLine();

Console.WriteLine("    Available transport filter test:");

var transportFilter = new AvailableTransportFilter();

var alphaAvailableTransports = snapshot
    .First(x => x.Identity.NodeId == "VEHICLE-ALPHA-001")
    .AvailableTransports;

var filteredCommandRoute = transportFilter.Filter(
    normalCommandRoute,
    alphaAvailableTransports);

Console.WriteLine("    Filtered FleetCommand route for Alpha:");
Console.WriteLine($"    Available   : {string.Join(", ", alphaAvailableTransports)}");
Console.WriteLine($"    Primary     : {string.Join(", ", filteredCommandRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback    : {string.Join(", ", filteredCommandRoute.FallbackTransports)}");
Console.WriteLine($"    Applicable  : {transportFilter.IsApplicable(filteredCommandRoute)}");
Console.WriteLine();

var filteredEmergencyRoute = transportFilter.Filter(
    emergencyRoute,
    alphaAvailableTransports);

Console.WriteLine("    Filtered EmergencyStop route for Alpha:");
Console.WriteLine($"    Available   : {string.Join(", ", alphaAvailableTransports)}");
Console.WriteLine($"    Primary     : {string.Join(", ", filteredEmergencyRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback    : {string.Join(", ", filteredEmergencyRoute.FallbackTransports)}");
Console.WriteLine($"    Broadcast   : {filteredEmergencyRoute.BroadcastAllAvailableLinks}");
Console.WriteLine($"    Applicable  : {transportFilter.IsApplicable(filteredEmergencyRoute)}");
Console.WriteLine();

Console.WriteLine("[7] Adaptive telemetry profile test:");

var telemetrySelector = new AdaptiveTelemetryProfileSelector();

var alphaTelemetryProfile = telemetrySelector.Select(alphaAvailableTransports);

Console.WriteLine("    Alpha telemetry profile:");
Console.WriteLine($"    Available   : {string.Join(", ", alphaAvailableTransports)}");
Console.WriteLine($"    Profile     : {alphaTelemetryProfile}");
Console.WriteLine($"    Explanation : {telemetrySelector.Explain(alphaTelemetryProfile)}");
Console.WriteLine();

var loraOnlyTransports = new[]
{
    TransportKind.LoRa
};

var loraTelemetryProfile = telemetrySelector.Select(loraOnlyTransports);

Console.WriteLine("    LoRa-only telemetry profile:");
Console.WriteLine($"    Available   : {string.Join(", ", loraOnlyTransports)}");
Console.WriteLine($"    Profile     : {loraTelemetryProfile}");
Console.WriteLine($"    Explanation : {telemetrySelector.Explain(loraTelemetryProfile)}");
Console.WriteLine();

var rfOnlyTransports = new[]
{
    TransportKind.RfModem
};

var rfTelemetryProfile = telemetrySelector.Select(rfOnlyTransports);

Console.WriteLine("    RF-only telemetry profile:");
Console.WriteLine($"    Available   : {string.Join(", ", rfOnlyTransports)}");
Console.WriteLine($"    Profile     : {rfTelemetryProfile}");
Console.WriteLine($"    Explanation : {telemetrySelector.Explain(rfTelemetryProfile)}");
Console.WriteLine();

Console.WriteLine("[8] Ground world model test:");

var obstacle = new GroundWorldObject
{
    ObjectId = "OBS-SMOKE-001",
    Kind = WorldObjectKind.Obstacle,
    Name = "Simulated obstacle",
    SourceNodeId = "VEHICLE-ALPHA-001",
    ContributorNodeIds = new[]
    {
        "VEHICLE-ALPHA-001"
    },
    Latitude = 41.026,
    Longitude = 29.016,
    X = 12.5,
    Y = 4.2,
    RadiusMeters = 1.8,
    Confidence = 0.72,
    Metadata = new Dictionary<string, string>
    {
        ["sensor"] = "sim_lidar",
        ["sourceFrame"] = "smoke_test",
        ["severity"] = "medium"
    }
};

var target = new GroundWorldObject
{
    ObjectId = "TARGET-SMOKE-001",
    Kind = WorldObjectKind.Target,
    Name = "Simulated target buoy",
    SourceNodeId = "VEHICLE-ALPHA-001",
    ContributorNodeIds = new[]
    {
        "VEHICLE-ALPHA-001"
    },
    Latitude = 41.027,
    Longitude = 29.017,
    X = 18.0,
    Y = 7.5,
    RadiusMeters = 0.8,
    Confidence = 0.84,
    Metadata = new Dictionary<string, string>
    {
        ["sensor"] = "sim_camera",
        ["class"] = "buoy",
        ["sourceFrame"] = "smoke_test"
    }
};

var obstacleAdded = ground.UpsertWorldObject(obstacle);
var targetAdded = ground.UpsertWorldObject(target);

Console.WriteLine($"    Obstacle added        : {obstacleAdded}");
Console.WriteLine($"    Target added          : {targetAdded}");
Console.WriteLine($"    World count           : {ground.WorldModel.Count}");
Console.WriteLine($"    Active world count    : {ground.WorldModel.ActiveCount}");
Console.WriteLine($"    Active obstacles      : {ground.GetActiveObstacles().Count}");
Console.WriteLine($"    Active targets        : {ground.GetActiveTargets().Count}");

var contributionAdded = ground.WorldModel.AddContribution(
    objectId: "OBS-SMOKE-001",
    nodeId: "VEHICLE-BETA-001");

Console.WriteLine($"    Beta contribution     : {contributionAdded}");

var worldSnapshot = ground.GetWorldSnapshot();

foreach (var worldObject in worldSnapshot)
{
    Console.WriteLine($"    ObjectId              : {worldObject.ObjectId}");
    Console.WriteLine($"    Kind                  : {worldObject.Kind}");
    Console.WriteLine($"    Name                  : {worldObject.Name}");
    Console.WriteLine($"    Source                : {worldObject.SourceNodeId}");
    Console.WriteLine($"    Contributors          : {string.Join(", ", worldObject.ContributorNodeIds)}");
    Console.WriteLine($"    Position              : {worldObject.Latitude}, {worldObject.Longitude}");
    Console.WriteLine($"    Local XY              : {worldObject.X}, {worldObject.Y}");
    Console.WriteLine($"    Confidence            : {worldObject.Confidence}");
    Console.WriteLine($"    Active                : {worldObject.IsActive}");
}

var deactivatedWorldObjects = ground.DeactivateStaleWorldObjects(
    maxAge: TimeSpan.FromMilliseconds(1),
    nowUtc: DateTimeOffset.UtcNow.AddSeconds(10));

Console.WriteLine($"    Stale world deactivated: {deactivatedWorldObjects}");
Console.WriteLine($"    Active world count now : {ground.WorldModel.ActiveCount}");
Console.WriteLine();

Console.WriteLine("[9] Mission allocator test:");

var mappingMission = new MissionRequest
{
    MissionId = "MISSION-ALLOC-MAP-001",
    Name = "Map test area",
    MissionType = "Mapping",
    RequiredCapabilities = new[]
    {
        "navigation",
        "mapping"
    },
    PreferredCapabilities = new[]
    {
        "fleet_heartbeat"
    },
    AllowedVehicleTypes = new[]
    {
        "SurfaceVessel"
    },
    Priority = 3,
    TargetLatitude = 41.028,
    TargetLongitude = 29.018,
    Metadata = new Dictionary<string, string>
    {
        ["source"] = "smoke_test",
        ["areaId"] = "TEST-AREA-A"
    }
};

var allocation = ground.AllocateMission(mappingMission);

Console.WriteLine("    Mapping mission allocation:");
Console.WriteLine($"    MissionId       : {allocation.MissionId}");
Console.WriteLine($"    Success         : {allocation.Success}");
Console.WriteLine($"    SelectedNodeId  : {allocation.SelectedNodeId}");
Console.WriteLine($"    SelectedName    : {allocation.SelectedDisplayName}");
Console.WriteLine($"    Score           : {allocation.Score}");
Console.WriteLine($"    Reason          : {allocation.Reason}");
Console.WriteLine($"    Candidates      : {string.Join(", ", allocation.CandidateNodeIds)}");

var impossibleMission = new MissionRequest
{
    MissionId = "MISSION-ALLOC-SUB-001",
    Name = "Submarine inspection",
    MissionType = "InspectTarget",
    RequiredCapabilities = new[]
    {
        "navigation",
        "sonar"
    },
    AllowedVehicleTypes = new[]
    {
        "Submarine"
    },
    Priority = 5,
    RelatedWorldObjectId = "TARGET-SMOKE-001",
    Metadata = new Dictionary<string, string>
    {
        ["source"] = "smoke_test",
        ["reason"] = "intentional_rejection_case"
    }
};

var failedAllocation = ground.AllocateMission(impossibleMission);

Console.WriteLine();
Console.WriteLine("    Impossible mission allocation:");
Console.WriteLine($"    MissionId       : {failedAllocation.MissionId}");
Console.WriteLine($"    Success         : {failedAllocation.Success}");
Console.WriteLine($"    Reason          : {failedAllocation.Reason}");
Console.WriteLine($"    Rejected count  : {failedAllocation.RejectedNodeReasons.Count}");

foreach (var pair in failedAllocation.RejectedNodeReasons)
{
    Console.WriteLine($"    Rejected         : {pair.Key} -> {pair.Value}");
}

Console.WriteLine();

Console.WriteLine("[10] Fleet coordinator test:");

var coordinationMission = new MissionRequest
{
    MissionId = "MISSION-COORD-MAP-001",
    Name = "Coordinate mapping mission",
    MissionType = "Mapping",
    RequiredCapabilities = new[]
    {
        "navigation",
        "mapping"
    },
    PreferredCapabilities = new[]
    {
        "fleet_heartbeat"
    },
    AllowedVehicleTypes = new[]
    {
        "SurfaceVessel"
    },
    Priority = 4,
    TargetLatitude = 41.029,
    TargetLongitude = 29.019,
    RelatedWorldObjectId = "TARGET-SMOKE-001",
    Metadata = new Dictionary<string, string>
    {
        ["source"] = "smoke_test",
        ["areaId"] = "TEST-AREA-B",
        ["operator"] = "Tunahan"
    }
};

var coordination = ground.CoordinateMission(coordinationMission);

Console.WriteLine("    Fleet coordination result:");
Console.WriteLine($"    Success         : {coordination.Success}");
Console.WriteLine($"    Reason          : {coordination.Reason}");
Console.WriteLine($"    SelectedNodeId  : {coordination.Allocation?.SelectedNodeId}");
Console.WriteLine($"    CommandType     : {coordination.Command?.CommandType}");
Console.WriteLine($"    CommandId       : {coordination.Command?.CommandId}");
Console.WriteLine($"    TargetNodeId    : {coordination.Command?.TargetNodeId}");
Console.WriteLine($"    EnvelopeType    : {coordination.Envelope?.MessageType}");
Console.WriteLine($"    EnvelopeSource  : {coordination.Envelope?.SourceNodeId}");
Console.WriteLine($"    EnvelopeTarget  : {coordination.Envelope?.TargetNodeId}");
Console.WriteLine($"    TrackerCount    : {ground.CommandTracker.Count}");

if (coordination.Command is not null)
{
    Console.WriteLine("    Command args:");
    foreach (var pair in coordination.Command.Args)
    {
        Console.WriteLine($"    - {pair.Key}: {pair.Value}");
    }
}

var coordinatorFailed = ground.CoordinateMission(impossibleMission);

Console.WriteLine();
Console.WriteLine("    Failed fleet coordination result:");
Console.WriteLine($"    Success         : {coordinatorFailed.Success}");
Console.WriteLine($"    Reason          : {coordinatorFailed.Reason}");
Console.WriteLine($"    HasCommand      : {coordinatorFailed.Command is not null}");
Console.WriteLine($"    HasEnvelope     : {coordinatorFailed.Envelope is not null}");
Console.WriteLine();

Console.WriteLine("[11] Communication router test:");

CommunicationRouteResult? coordinatedRoute = null;

if (coordination.Envelope is not null)
{
    coordinatedRoute = ground.RouteEnvelope(coordination.Envelope);

    Console.WriteLine("    Coordinated mission envelope route:");
    Console.WriteLine($"    CanRoute        : {coordinatedRoute.CanRoute}");
    Console.WriteLine($"    Reason          : {coordinatedRoute.Reason}");
    Console.WriteLine($"    MessageType     : {coordinatedRoute.MessageType}");
    Console.WriteLine($"    Source          : {coordinatedRoute.SourceNodeId}");
    Console.WriteLine($"    Target          : {coordinatedRoute.TargetNodeId}");
    Console.WriteLine($"    TargetKnown     : {coordinatedRoute.TargetKnown}");
    Console.WriteLine($"    TargetAvailable : {string.Join(", ", coordinatedRoute.TargetAvailableTransports)}");
    Console.WriteLine($"    Primary         : {string.Join(", ", coordinatedRoute.PrimaryTransports)}");
    Console.WriteLine($"    Fallback        : {string.Join(", ", coordinatedRoute.FallbackTransports)}");
    Console.WriteLine($"    Ack             : {coordinatedRoute.RequiresAck}");
    Console.WriteLine($"    Broadcast       : {coordinatedRoute.BroadcastAllAvailableLinks}");
}

var emergencyRouteResult = ground.RouteEnvelope(emergencyEnvelope);

Console.WriteLine();
Console.WriteLine("    Emergency broadcast route:");
Console.WriteLine($"    CanRoute        : {emergencyRouteResult.CanRoute}");
Console.WriteLine($"    Reason          : {emergencyRouteResult.Reason}");
Console.WriteLine($"    MessageType     : {emergencyRouteResult.MessageType}");
Console.WriteLine($"    Source          : {emergencyRouteResult.SourceNodeId}");
Console.WriteLine($"    Target          : {emergencyRouteResult.TargetNodeId}");
Console.WriteLine($"    TargetKnown     : {emergencyRouteResult.TargetKnown}");
Console.WriteLine($"    TargetAvailable : {string.Join(", ", emergencyRouteResult.TargetAvailableTransports)}");
Console.WriteLine($"    Primary         : {string.Join(", ", emergencyRouteResult.PrimaryTransports)}");
Console.WriteLine($"    Fallback        : {string.Join(", ", emergencyRouteResult.FallbackTransports)}");
Console.WriteLine($"    Ack             : {emergencyRouteResult.RequiresAck}");
Console.WriteLine($"    Broadcast       : {emergencyRouteResult.BroadcastAllAvailableLinks}");

var unknownTargetCommand = new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-UNKNOWN-001",
    CommandType = "AssignMission",
    AuthorityLevel = "MissionCommand",
    Priority = MessagePriority.High,
    Args = new Dictionary<string, string>
    {
        ["missionId"] = "MISSION-UNKNOWN-TARGET"
    },
    IsOperatorIssued = true,
    RequiresResult = true
};

var unknownTargetEnvelope = HydronomEnvelopeFactory.CreateCommand(unknownTargetCommand);
var unknownTargetRoute = ground.RouteEnvelope(unknownTargetEnvelope);

Console.WriteLine();
Console.WriteLine("    Unknown target route:");
Console.WriteLine($"    CanRoute        : {unknownTargetRoute.CanRoute}");
Console.WriteLine($"    Reason          : {unknownTargetRoute.Reason}");
Console.WriteLine($"    Target          : {unknownTargetRoute.TargetNodeId}");
Console.WriteLine($"    TargetKnown     : {unknownTargetRoute.TargetKnown}");
Console.WriteLine($"    Primary         : {string.Join(", ", unknownTargetRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback        : {string.Join(", ", unknownTargetRoute.FallbackTransports)}");
Console.WriteLine();

Console.WriteLine("[12] Telemetry route planner test:");

if (coordinatedRoute is not null)
{
    var coordinatedTelemetryPlan = ground.PlanTelemetryForRoute(coordinatedRoute);

    Console.WriteLine("    Coordinated mission telemetry plan:");
    Console.WriteLine($"    CanRoute        : {coordinatedTelemetryPlan.CanRoute}");
    Console.WriteLine($"    MessageType     : {coordinatedTelemetryPlan.MessageType}");
    Console.WriteLine($"    Target          : {coordinatedTelemetryPlan.TargetNodeId}");
    Console.WriteLine($"    Profile         : {coordinatedTelemetryPlan.Profile}");
    Console.WriteLine($"    RouteReason     : {coordinatedTelemetryPlan.RouteReason}");
    Console.WriteLine($"    ProfileReason   : {coordinatedTelemetryPlan.ProfileReason}");
    Console.WriteLine($"    Primary         : {string.Join(", ", coordinatedTelemetryPlan.PrimaryTransports)}");
    Console.WriteLine($"    Fallback        : {string.Join(", ", coordinatedTelemetryPlan.FallbackTransports)}");
}

var emergencyTelemetryPlan = ground.PlanTelemetryForRoute(emergencyRouteResult);

Console.WriteLine();
Console.WriteLine("    Emergency telemetry plan:");
Console.WriteLine($"    CanRoute        : {emergencyTelemetryPlan.CanRoute}");
Console.WriteLine($"    MessageType     : {emergencyTelemetryPlan.MessageType}");
Console.WriteLine($"    Target          : {emergencyTelemetryPlan.TargetNodeId}");
Console.WriteLine($"    Profile         : {emergencyTelemetryPlan.Profile}");
Console.WriteLine($"    RouteReason     : {emergencyTelemetryPlan.RouteReason}");
Console.WriteLine($"    ProfileReason   : {emergencyTelemetryPlan.ProfileReason}");
Console.WriteLine($"    Primary         : {string.Join(", ", emergencyTelemetryPlan.PrimaryTransports)}");
Console.WriteLine($"    Fallback        : {string.Join(", ", emergencyTelemetryPlan.FallbackTransports)}");

var unknownTargetTelemetryPlan = ground.PlanTelemetryForRoute(unknownTargetRoute);

Console.WriteLine();
Console.WriteLine("    Unknown target telemetry plan:");
Console.WriteLine($"    CanRoute        : {unknownTargetTelemetryPlan.CanRoute}");
Console.WriteLine($"    MessageType     : {unknownTargetTelemetryPlan.MessageType}");
Console.WriteLine($"    Target          : {unknownTargetTelemetryPlan.TargetNodeId}");
Console.WriteLine($"    Profile         : {unknownTargetTelemetryPlan.Profile}");
Console.WriteLine($"    RouteReason     : {unknownTargetTelemetryPlan.RouteReason}");
Console.WriteLine($"    ProfileReason   : {unknownTargetTelemetryPlan.ProfileReason}");
Console.WriteLine($"    Primary         : {string.Join(", ", unknownTargetTelemetryPlan.PrimaryTransports)}");
Console.WriteLine($"    Fallback        : {string.Join(", ", unknownTargetTelemetryPlan.FallbackTransports)}");

var oneShotTelemetryPlan = ground.CoordinateMissionRouteAndPlanTelemetry(new MissionRequest
{
    MissionId = "MISSION-ONE-SHOT-TELEMETRY-001",
    Name = "One shot telemetry planning mission",
    MissionType = "Mapping",
    RequiredCapabilities = new[]
    {
        "navigation",
        "mapping"
    },
    PreferredCapabilities = new[]
    {
        "fleet_heartbeat"
    },
    AllowedVehicleTypes = new[]
    {
        "SurfaceVessel"
    },
    Priority = 2,
    TargetLatitude = 41.031,
    TargetLongitude = 29.021,
    Metadata = new Dictionary<string, string>
    {
        ["source"] = "smoke_test",
        ["mode"] = "one_shot"
    }
});

Console.WriteLine();
Console.WriteLine("    One-shot mission route telemetry plan:");
Console.WriteLine($"    Plan exists      : {oneShotTelemetryPlan is not null}");
Console.WriteLine($"    CanRoute        : {oneShotTelemetryPlan?.CanRoute}");
Console.WriteLine($"    Profile         : {oneShotTelemetryPlan?.Profile}");
Console.WriteLine($"    Primary         : {string.Join(", ", oneShotTelemetryPlan?.PrimaryTransports ?? Array.Empty<TransportKind>())}");
Console.WriteLine($"    Fallback        : {string.Join(", ", oneShotTelemetryPlan?.FallbackTransports ?? Array.Empty<TransportKind>())}");
Console.WriteLine();

Console.WriteLine("[13] Ground diagnostics snapshot test:");

var operationSnapshot = ground.CreateOperationSnapshot();

Console.WriteLine("    Operation snapshot:");
Console.WriteLine($"    Overall health       : {operationSnapshot.OverallHealth}");
Console.WriteLine($"    Summary              : {operationSnapshot.Summary}");
Console.WriteLine($"    Total nodes          : {operationSnapshot.TotalNodeCount}");
Console.WriteLine($"    Online nodes         : {operationSnapshot.OnlineNodeCount}");
Console.WriteLine($"    Offline nodes        : {operationSnapshot.OfflineNodeCount}");
Console.WriteLine($"    Healthy nodes        : {operationSnapshot.HealthyNodeCount}");
Console.WriteLine($"    Warning nodes        : {operationSnapshot.WarningNodeCount}");
Console.WriteLine($"    Critical nodes       : {operationSnapshot.CriticalNodeCount}");
Console.WriteLine($"    Average battery      : {operationSnapshot.AverageBatteryPercent}%");
Console.WriteLine($"    Total commands       : {operationSnapshot.TotalCommandCount}");
Console.WriteLine($"    Pending commands     : {operationSnapshot.PendingCommandCount}");
Console.WriteLine($"    Completed commands   : {operationSnapshot.CompletedCommandCount}");
Console.WriteLine($"    Successful commands  : {operationSnapshot.SuccessfulCommandCount}");
Console.WriteLine($"    Failed commands      : {operationSnapshot.FailedCommandCount}");
Console.WriteLine($"    Total world objects  : {operationSnapshot.TotalWorldObjectCount}");
Console.WriteLine($"    Active world objects : {operationSnapshot.ActiveWorldObjectCount}");
Console.WriteLine($"    Active obstacles     : {operationSnapshot.ActiveObstacleCount}");
Console.WriteLine($"    Active targets       : {operationSnapshot.ActiveTargetCount}");
Console.WriteLine($"    Active no-go zones   : {operationSnapshot.ActiveNoGoZoneCount}");
Console.WriteLine($"    Link vehicles        : {operationSnapshot.LinkVehicleCount}");
Console.WriteLine($"    Total links          : {operationSnapshot.TotalLinkCount}");
Console.WriteLine($"    Link summary         : {operationSnapshot.LinkHealthSummary}");
Console.WriteLine($"    Route executions     : {operationSnapshot.TotalRouteExecutionCount}");
Console.WriteLine($"    Route summary        : {operationSnapshot.RouteExecutionSummary}");
Console.WriteLine($"    Has warnings         : {operationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues  : {operationSnapshot.HasCriticalIssues}");
Console.WriteLine();

Console.WriteLine("[14] Link health tracker test:");

var linkTestTime = DateTime.UtcNow;

ground.MarkLinkSeen(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Tcp,
    nowUtc: linkTestTime);

ground.RecordLinkSend(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Tcp,
    nowUtc: linkTestTime.AddMilliseconds(2));

ground.RecordLinkRouteSuccess(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Tcp,
    latencyMs: 24,
    nowUtc: linkTestTime.AddMilliseconds(24));

ground.RecordLinkAck(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Tcp,
    latencyMs: 26,
    nowUtc: linkTestTime.AddMilliseconds(26));

ground.MarkLinkSeen(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.WebSocket,
    nowUtc: linkTestTime);

ground.RecordLinkSend(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.WebSocket,
    nowUtc: linkTestTime.AddMilliseconds(4));

ground.RecordLinkRouteSuccess(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.WebSocket,
    latencyMs: 86,
    nowUtc: linkTestTime.AddMilliseconds(86));

ground.MarkLinkSeen(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Mock,
    nowUtc: linkTestTime);

ground.RecordLinkSend(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Mock,
    nowUtc: linkTestTime.AddMilliseconds(1));

ground.RecordLinkTimeout(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Mock,
    nowUtc: linkTestTime.AddMilliseconds(500));

ground.RecordEstimatedLinkPacketLoss(
    vehicleId: "VEHICLE-ALPHA-001",
    transportKind: TransportKind.Mock,
    lostPacketCount: 2,
    nowUtc: linkTestTime.AddMilliseconds(600));

var linkHealthSnapshot = ground.GetLinkHealthSnapshot(linkTestTime.AddSeconds(1));

Console.WriteLine($"    Link vehicle count    : {linkHealthSnapshot.Count}");

foreach (var vehicleLink in linkHealthSnapshot)
{
    Console.WriteLine($"    Vehicle               : {vehicleLink.VehicleId}");
    Console.WriteLine($"    Overall status        : {vehicleLink.OverallStatus}");
    Console.WriteLine($"    Overall quality       : {vehicleLink.OverallQualityScore:0.##}");

    foreach (var link in vehicleLink.Links)
    {
        Console.WriteLine($"    - Transport           : {link.TransportKind}");
        Console.WriteLine($"      Status              : {link.Status}");
        Console.WriteLine($"      Quality             : {link.QualityScore:0.##}");
        Console.WriteLine($"      SuccessRate         : {link.SuccessRate:0.##}");
        Console.WriteLine($"      FailureRate         : {link.FailureRate:0.##}");
        Console.WriteLine($"      TimeoutRate         : {link.TimeoutRate:0.##}");
        Console.WriteLine($"      LastLatencyMs       : {link.LastLatencyMs}");
        Console.WriteLine($"      AvgLatencyMs        : {link.AverageLatencyMs}");
        Console.WriteLine($"      Sent/Success/Fail   : {link.SentCount}/{link.SuccessCount}/{link.FailureCount}");
        Console.WriteLine($"      Ack/Timeout/Loss    : {link.AckCount}/{link.TimeoutCount}/{link.LostPacketEstimateCount}");
    }
}

var bestLink = ground.GetBestAvailableLink("VEHICLE-ALPHA-001");
var availableLinks = ground.GetAvailableLinks("VEHICLE-ALPHA-001");

Console.WriteLine();
Console.WriteLine("    Best available link:");
Console.WriteLine($"    Exists                : {bestLink is not null}");
Console.WriteLine($"    Transport             : {bestLink?.TransportKind}");
Console.WriteLine($"    Status                : {bestLink?.Status}");
Console.WriteLine($"    Quality               : {bestLink?.QualityScore:0.##}");
Console.WriteLine($"    Available link count  : {availableLinks.Count}");
Console.WriteLine();

Console.WriteLine("[15] Link health diagnostics snapshot test:");

var linkAwareOperationSnapshot = ground.DiagnosticsEngine.CreateSnapshot(
    ground.GetFleetSnapshot(),
    ground.GetCommandHistorySnapshot(),
    ground.WorldModel,
    linkHealthSnapshot,
    ground.GetRouteExecutionSnapshot());

Console.WriteLine("    Link-aware operation snapshot:");
Console.WriteLine($"    Overall health            : {linkAwareOperationSnapshot.OverallHealth}");
Console.WriteLine($"    Summary                   : {linkAwareOperationSnapshot.Summary}");
Console.WriteLine($"    Link health summary       : {linkAwareOperationSnapshot.LinkHealthSummary}");
Console.WriteLine($"    Link vehicle count        : {linkAwareOperationSnapshot.LinkVehicleCount}");
Console.WriteLine($"    Total link count          : {linkAwareOperationSnapshot.TotalLinkCount}");
Console.WriteLine($"    Good links                : {linkAwareOperationSnapshot.GoodLinkCount}");
Console.WriteLine($"    Degraded links            : {linkAwareOperationSnapshot.DegradedLinkCount}");
Console.WriteLine($"    Critical links            : {linkAwareOperationSnapshot.CriticalLinkCount}");
Console.WriteLine($"    Lost links                : {linkAwareOperationSnapshot.LostLinkCount}");
Console.WriteLine($"    Unknown links             : {linkAwareOperationSnapshot.UnknownLinkCount}");
Console.WriteLine($"    Avg vehicle link quality  : {linkAwareOperationSnapshot.AverageVehicleLinkQualityScore}");
Console.WriteLine($"    Avg transport quality     : {linkAwareOperationSnapshot.AverageTransportLinkQualityScore}");
Console.WriteLine($"    Worst vehicle quality     : {linkAwareOperationSnapshot.WorstVehicleLinkQualityScore}");
Console.WriteLine($"    Worst transport quality   : {linkAwareOperationSnapshot.WorstTransportLinkQualityScore}");
Console.WriteLine($"    Route summary             : {linkAwareOperationSnapshot.RouteExecutionSummary}");
Console.WriteLine();

Console.WriteLine("[16] Link-aware routing preparation test:");

if (coordination.Envelope is not null)
{
    var linkAwareRoute = ground.CommunicationRouter.Route(
        coordination.Envelope,
        ground.GetFleetSnapshot(),
        linkAvailabilityFilter: (vehicleId, transportKind) =>
            ground.GetAvailableLinks(vehicleId)
                .Any(link => link.TransportKind == transportKind));

    Console.WriteLine("    Link-aware coordinated route:");
    Console.WriteLine($"    CanRoute        : {linkAwareRoute.CanRoute}");
    Console.WriteLine($"    Reason          : {linkAwareRoute.Reason}");
    Console.WriteLine($"    Target          : {linkAwareRoute.TargetNodeId}");
    Console.WriteLine($"    TargetKnown     : {linkAwareRoute.TargetKnown}");
    Console.WriteLine($"    TargetAvailable : {string.Join(", ", linkAwareRoute.TargetAvailableTransports)}");
    Console.WriteLine($"    Primary         : {string.Join(", ", linkAwareRoute.PrimaryTransports)}");
    Console.WriteLine($"    Fallback        : {string.Join(", ", linkAwareRoute.FallbackTransports)}");
    Console.WriteLine($"    Ack             : {linkAwareRoute.RequiresAck}");
}

var linkAwareEmergencyRoute = ground.CommunicationRouter.Route(
    emergencyEnvelope,
    ground.GetFleetSnapshot(),
    linkAvailabilityFilter: (vehicleId, transportKind) =>
        ground.GetAvailableLinks(vehicleId)
            .Any(link => link.TransportKind == transportKind));

Console.WriteLine();
Console.WriteLine("    Link-aware emergency broadcast route:");
Console.WriteLine($"    CanRoute        : {linkAwareEmergencyRoute.CanRoute}");
Console.WriteLine($"    Reason          : {linkAwareEmergencyRoute.Reason}");
Console.WriteLine($"    Target          : {linkAwareEmergencyRoute.TargetNodeId}");
Console.WriteLine($"    TargetKnown     : {linkAwareEmergencyRoute.TargetKnown}");
Console.WriteLine($"    TargetAvailable : {string.Join(", ", linkAwareEmergencyRoute.TargetAvailableTransports)}");
Console.WriteLine($"    Primary         : {string.Join(", ", linkAwareEmergencyRoute.PrimaryTransports)}");
Console.WriteLine($"    Fallback        : {string.Join(", ", linkAwareEmergencyRoute.FallbackTransports)}");
Console.WriteLine($"    Broadcast       : {linkAwareEmergencyRoute.BroadcastAllAvailableLinks}");
Console.WriteLine();

Console.WriteLine("[17] Link loss simulation test:");

var linkLossTime = linkTestTime.AddSeconds(90);
var lostSnapshot = ground.GetLinkHealthSnapshot(linkLossTime);

Console.WriteLine($"    Snapshot time offset : +90s");

foreach (var vehicleLink in lostSnapshot)
{
    Console.WriteLine($"    Vehicle             : {vehicleLink.VehicleId}");
    Console.WriteLine($"    Overall status      : {vehicleLink.OverallStatus}");
    Console.WriteLine($"    Overall quality     : {vehicleLink.OverallQualityScore:0.##}");

    foreach (var link in vehicleLink.Links)
    {
        Console.WriteLine($"    - {link.TransportKind}: {link.Status}, quality={link.QualityScore:0.##}");
    }
}

var lostLinkOperationSnapshot = ground.DiagnosticsEngine.CreateSnapshot(
    ground.GetFleetSnapshot(),
    ground.GetCommandHistorySnapshot(),
    ground.WorldModel,
    lostSnapshot,
    ground.GetRouteExecutionSnapshot());

Console.WriteLine();
Console.WriteLine("    Operation snapshot after link loss simulation:");
Console.WriteLine($"    Overall health       : {lostLinkOperationSnapshot.OverallHealth}");
Console.WriteLine($"    Summary              : {lostLinkOperationSnapshot.Summary}");
Console.WriteLine($"    Link health summary  : {lostLinkOperationSnapshot.LinkHealthSummary}");
Console.WriteLine($"    Route summary        : {lostLinkOperationSnapshot.RouteExecutionSummary}");
Console.WriteLine($"    Good links           : {lostLinkOperationSnapshot.GoodLinkCount}");
Console.WriteLine($"    Degraded links       : {lostLinkOperationSnapshot.DegradedLinkCount}");
Console.WriteLine($"    Critical links       : {lostLinkOperationSnapshot.CriticalLinkCount}");
Console.WriteLine($"    Lost links           : {lostLinkOperationSnapshot.LostLinkCount}");
Console.WriteLine($"    Has warnings         : {lostLinkOperationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues  : {lostLinkOperationSnapshot.HasCriticalIssues}");
Console.WriteLine();

Console.WriteLine("[18] Transport execution tracker ACK success test:");

if (coordination.Envelope is not null)
{
    var ackExecution = ground.BeginRouteExecutionWithLinkHealth(
        coordination.Envelope,
        nowUtc: DateTimeOffset.UtcNow);

    var sendStart = DateTimeOffset.UtcNow;
    var sendCompleted = sendStart.AddMilliseconds(31);

    var sendAttemptRecorded = ground.RecordTransportSendAttempt(
        ackExecution.ExecutionId,
        TransportKind.Tcp,
        sendStart);

    var ackRecorded = ground.RecordTransportAcked(
        ackExecution.ExecutionId,
        TransportKind.Tcp,
        sendStart,
        sendCompleted,
        latencyMs: 31,
        reason: "Simulated ACK for coordinated mission command.");

    Console.WriteLine($"    ExecutionId        : {ackExecution.ExecutionId}");
    Console.WriteLine($"    CanRoute           : {ackExecution.RouteResult.CanRoute}");
    Console.WriteLine($"    Candidate          : {string.Join(", ", ackExecution.CandidateTransports)}");
    Console.WriteLine($"    Send attempt       : {sendAttemptRecorded}");
    Console.WriteLine($"    ACK recorded       : {ackRecorded}");
    Console.WriteLine($"    IsCompleted        : {ackExecution.IsCompleted}");
    Console.WriteLine($"    HasSuccess         : {ackExecution.HasSuccess}");
    Console.WriteLine($"    HasAck             : {ackExecution.HasAck}");
    Console.WriteLine($"    LastStatus         : {ackExecution.LastStatus}");
    Console.WriteLine($"    BestLatencyMs      : {ackExecution.BestLatencyMs}");
}

Console.WriteLine();

Console.WriteLine("[19] Transport execution timeout / failure test:");

var transportTimeoutEnvelope = HydronomEnvelopeFactory.CreateCommand(new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "SetSpeed",
    AuthorityLevel = "ControlCommand",
    Priority = MessagePriority.High,
    Args = new Dictionary<string, string>
    {
        ["speedMps"] = "0.75"
    },
    IsOperatorIssued = true,
    RequiresResult = true
});

var timeoutExecution = ground.BeginRouteExecutionWithLinkHealth(
    transportTimeoutEnvelope,
    nowUtc: DateTimeOffset.UtcNow);

var timeoutStart = DateTimeOffset.UtcNow;
var timeoutEnd = timeoutStart.AddMilliseconds(900);

var timeoutSendAttemptRecorded = ground.RecordTransportSendAttempt(
    timeoutExecution.ExecutionId,
    TransportKind.Mock,
    timeoutStart);

var timeoutRecorded = ground.RecordTransportTimeout(
    timeoutExecution.ExecutionId,
    TransportKind.Mock,
    timeoutStart,
    timeoutEnd,
    reason: "Simulated transport timeout on Mock link.");

Console.WriteLine("    Timeout execution:");
Console.WriteLine($"    ExecutionId        : {timeoutExecution.ExecutionId}");
Console.WriteLine($"    CanRoute           : {timeoutExecution.RouteResult.CanRoute}");
Console.WriteLine($"    Candidate          : {string.Join(", ", timeoutExecution.CandidateTransports)}");
Console.WriteLine($"    Send attempt       : {timeoutSendAttemptRecorded}");
Console.WriteLine($"    Timeout recorded   : {timeoutRecorded}");
Console.WriteLine($"    IsCompleted        : {timeoutExecution.IsCompleted}");
Console.WriteLine($"    HasTimeout         : {timeoutExecution.HasTimeout}");
Console.WriteLine($"    HasFailure         : {timeoutExecution.HasFailure}");
Console.WriteLine($"    LastStatus         : {timeoutExecution.LastStatus}");
Console.WriteLine();

var transportFailureEnvelope = HydronomEnvelopeFactory.CreateCommand(new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "RequestLongTelemetry",
    AuthorityLevel = "MissionCommand",
    Priority = MessagePriority.Normal,
    Args = new Dictionary<string, string>
    {
        ["profile"] = "Full"
    },
    IsOperatorIssued = true,
    RequiresResult = false
});

var failureExecution = ground.BeginRouteExecution(
    transportFailureEnvelope,
    nowUtc: DateTimeOffset.UtcNow);

var failureStart = DateTimeOffset.UtcNow;
var failureEnd = failureStart.AddMilliseconds(140);

var failureRecorded = ground.RecordTransportFailure(
    failureExecution.ExecutionId,
    TransportKind.WebSocket,
    TransportSendStatus.Failed,
    failureStart,
    failureEnd,
    reason: "Simulated transport send failure.",
    errorMessage: "Mock WebSocket send error.");

Console.WriteLine("    Failure execution:");
Console.WriteLine($"    ExecutionId        : {failureExecution.ExecutionId}");
Console.WriteLine($"    CanRoute           : {failureExecution.RouteResult.CanRoute}");
Console.WriteLine($"    Candidate          : {string.Join(", ", failureExecution.CandidateTransports)}");
Console.WriteLine($"    Failure recorded   : {failureRecorded}");
Console.WriteLine($"    IsCompleted        : {failureExecution.IsCompleted}");
Console.WriteLine($"    HasFailure         : {failureExecution.HasFailure}");
Console.WriteLine($"    LastStatus         : {failureExecution.LastStatus}");
Console.WriteLine();

Console.WriteLine("[20] Route unavailable execution test:");

var routeUnavailableExecution = ground.BeginRouteExecution(
    unknownTargetEnvelope,
    nowUtc: DateTimeOffset.UtcNow);

Console.WriteLine($"    ExecutionId        : {routeUnavailableExecution.ExecutionId}");
Console.WriteLine($"    CanRoute           : {routeUnavailableExecution.RouteResult.CanRoute}");
Console.WriteLine($"    IsCompleted        : {routeUnavailableExecution.IsCompleted}");
Console.WriteLine($"    HasFailure         : {routeUnavailableExecution.HasFailure}");
Console.WriteLine($"    LastStatus         : {routeUnavailableExecution.LastStatus}");
Console.WriteLine($"    SendResults        : {routeUnavailableExecution.SendResults.Count}");
Console.WriteLine();

Console.WriteLine("[21] Route execution diagnostics snapshot test:");

var routeExecutionSnapshot = ground.GetRouteExecutionSnapshot();

Console.WriteLine($"    Route execution count     : {routeExecutionSnapshot.Count}");

foreach (var execution in routeExecutionSnapshot)
{
    Console.WriteLine($"    ExecutionId               : {execution.ExecutionId}");
    Console.WriteLine($"    MessageType               : {execution.MessageType}");
    Console.WriteLine($"    Target                    : {execution.TargetNodeId}");
    Console.WriteLine($"    CanRoute                  : {execution.CanRoute}");
    Console.WriteLine($"    IsCompleted               : {execution.IsCompleted}");
    Console.WriteLine($"    HasSuccess                : {execution.HasSuccess}");
    Console.WriteLine($"    HasAck                    : {execution.HasAck}");
    Console.WriteLine($"    HasTimeout                : {execution.HasTimeout}");
    Console.WriteLine($"    HasFailure                : {execution.HasFailure}");
    Console.WriteLine($"    LastStatus                : {execution.LastStatus}");
    Console.WriteLine($"    BestLatencyMs             : {execution.BestLatencyMs}");
    Console.WriteLine($"    CandidateTransports       : {string.Join(", ", execution.CandidateTransports)}");
    Console.WriteLine($"    SendResultCount           : {execution.SendResults.Count}");
}

var routeAwareOperationSnapshot = ground.CreateOperationSnapshot();

Console.WriteLine();
Console.WriteLine("    Route-aware operation snapshot:");
Console.WriteLine($"    Overall health            : {routeAwareOperationSnapshot.OverallHealth}");
Console.WriteLine($"    Summary                   : {routeAwareOperationSnapshot.Summary}");
Console.WriteLine($"    Route summary             : {routeAwareOperationSnapshot.RouteExecutionSummary}");
Console.WriteLine($"    Total route executions    : {routeAwareOperationSnapshot.TotalRouteExecutionCount}");
Console.WriteLine($"    Pending route executions  : {routeAwareOperationSnapshot.PendingRouteExecutionCount}");
Console.WriteLine($"    Completed route executions: {routeAwareOperationSnapshot.CompletedRouteExecutionCount}");
Console.WriteLine($"    Successful route execs    : {routeAwareOperationSnapshot.SuccessfulRouteExecutionCount}");
Console.WriteLine($"    Acked route execs         : {routeAwareOperationSnapshot.AckedRouteExecutionCount}");
Console.WriteLine($"    Timeout route execs       : {routeAwareOperationSnapshot.TimeoutRouteExecutionCount}");
Console.WriteLine($"    Failed route execs        : {routeAwareOperationSnapshot.FailedRouteExecutionCount}");
Console.WriteLine($"    Unroutable route execs    : {routeAwareOperationSnapshot.RouteUnavailableExecutionCount}");
Console.WriteLine($"    Total send results        : {routeAwareOperationSnapshot.TotalTransportSendResultCount}");
Console.WriteLine($"    Successful send results   : {routeAwareOperationSnapshot.SuccessfulTransportSendResultCount}");
Console.WriteLine($"    ACK send results          : {routeAwareOperationSnapshot.AckedTransportSendResultCount}");
Console.WriteLine($"    Timeout send results      : {routeAwareOperationSnapshot.TimeoutTransportSendResultCount}");
Console.WriteLine($"    Failed send results       : {routeAwareOperationSnapshot.FailedTransportSendResultCount}");
Console.WriteLine($"    Avg route latency ms      : {routeAwareOperationSnapshot.AverageRouteExecutionLatencyMs}");
Console.WriteLine($"    Best route latency ms     : {routeAwareOperationSnapshot.BestRouteExecutionLatencyMs}");
Console.WriteLine($"    Worst route latency ms    : {routeAwareOperationSnapshot.WorstRouteExecutionLatencyMs}");
Console.WriteLine($"    Has warnings              : {routeAwareOperationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues       : {routeAwareOperationSnapshot.HasCriticalIssues}");
Console.WriteLine();
Console.WriteLine("[22] TransportManager mock send success test:");

var mockTcpTransport = new MockGroundTransport(
    name: "mock-tcp-main",
    kind: TransportKind.Tcp,
    isConnected: true)
{
    SimulatedSendDelay = TimeSpan.FromMilliseconds(12)
};

var mockWebSocketTransport = new MockGroundTransport(
    name: "mock-websocket-ops",
    kind: TransportKind.WebSocket,
    isConnected: true)
{
    SimulatedSendDelay = TimeSpan.FromMilliseconds(18)
};

var mockFailTransport = new MockGroundTransport(
    name: "mock-fail-link",
    kind: TransportKind.Mock,
    isConnected: true)
{
    SimulatedSendDelay = TimeSpan.FromMilliseconds(5),
    FailOnSend = true
};

var tcpRegistered = ground.RegisterTransport(mockTcpTransport);
var wsRegistered = ground.RegisterTransport(mockWebSocketTransport);
var failRegistered = ground.RegisterTransport(mockFailTransport);

Console.WriteLine($"    TCP registered       : {tcpRegistered}");
Console.WriteLine($"    WebSocket registered : {wsRegistered}");
Console.WriteLine($"    Fail registered      : {failRegistered}");
Console.WriteLine($"    Registry count       : {ground.TransportRegistry.Count}");

var managerCommand = new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "SetHeading",
    AuthorityLevel = "ControlCommand",
    Priority = MessagePriority.High,
    Args = new Dictionary<string, string>
    {
        ["headingDeg"] = "90"
    },
    IsOperatorIssued = true,
    RequiresResult = true
};

var managerExecution = await ground.SendTrackedCommandAsync(
    managerCommand,
    useLinkHealthRouting: false,
    treatSuccessfulSendAsAckWhenRequired: true,
    sendTimeout: TimeSpan.FromSeconds(1),
    tryFallbacks: true);

Console.WriteLine("    SendTrackedCommandAsync result:");
Console.WriteLine($"    Execution exists     : {managerExecution is not null}");
Console.WriteLine($"    ExecutionId          : {managerExecution?.ExecutionId}");
Console.WriteLine($"    CanRoute             : {managerExecution?.RouteResult.CanRoute}");
Console.WriteLine($"    IsCompleted          : {managerExecution?.IsCompleted}");
Console.WriteLine($"    HasSuccess           : {managerExecution?.HasSuccess}");
Console.WriteLine($"    HasAck               : {managerExecution?.HasAck}");
Console.WriteLine($"    LastStatus           : {managerExecution?.LastStatus}");
Console.WriteLine($"    BestLatencyMs        : {managerExecution?.BestLatencyMs}");
Console.WriteLine($"    Mock TCP sent count  : {mockTcpTransport.SentCount}");
Console.WriteLine();

Console.WriteLine("[23] TransportManager mock broadcast send test:");

var broadcastExecution = await ground.SendEnvelopeAsync(
    emergencyEnvelope,
    useLinkHealthRouting: false,
    treatSuccessfulSendAsAckWhenRequired: true,
    sendTimeout: TimeSpan.FromSeconds(1),
    tryFallbacks: true,
    sendToAllForBroadcast: true);

Console.WriteLine("    Broadcast SendEnvelopeAsync result:");
Console.WriteLine($"    ExecutionId          : {broadcastExecution.ExecutionId}");
Console.WriteLine($"    CanRoute             : {broadcastExecution.RouteResult.CanRoute}");
Console.WriteLine($"    IsCompleted          : {broadcastExecution.IsCompleted}");
Console.WriteLine($"    HasSuccess           : {broadcastExecution.HasSuccess}");
Console.WriteLine($"    HasAck               : {broadcastExecution.HasAck}");
Console.WriteLine($"    LastStatus           : {broadcastExecution.LastStatus}");
Console.WriteLine($"    Send result count    : {broadcastExecution.SendResults.Count}");
Console.WriteLine($"    Mock TCP sent count  : {mockTcpTransport.SentCount}");
Console.WriteLine($"    Mock WS sent count   : {mockWebSocketTransport.SentCount}");
Console.WriteLine();

Console.WriteLine("[24] TransportManager mock timeout / failure / unavailable test:");

var slowMockTransport = new MockGroundTransport(
    name: "mock-slow-tcp",
    kind: TransportKind.Tcp,
    isConnected: true)
{
    SimulatedSendDelay = TimeSpan.FromMilliseconds(250)
};

var slowGround = new GroundStationEngine();
slowGround.HandleEnvelope(heartbeatEnvelope);
slowGround.RegisterTransport(slowMockTransport);

var slowCommand = new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "SlowCommand",
    AuthorityLevel = "ControlCommand",
    Priority = MessagePriority.High,
    Args = new Dictionary<string, string>
    {
        ["mode"] = "timeout_test"
    },
    IsOperatorIssued = true,
    RequiresResult = true
};

var slowExecution = await slowGround.SendTrackedCommandAsync(
    slowCommand,
    useLinkHealthRouting: false,
    treatSuccessfulSendAsAckWhenRequired: true,
    sendTimeout: TimeSpan.FromMilliseconds(10),
    tryFallbacks: false);

Console.WriteLine("    Timeout through TransportManager:");
Console.WriteLine($"    Execution exists     : {slowExecution is not null}");
Console.WriteLine($"    ExecutionId          : {slowExecution?.ExecutionId}");
Console.WriteLine($"    CanRoute             : {slowExecution?.RouteResult.CanRoute}");
Console.WriteLine($"    IsCompleted          : {slowExecution?.IsCompleted}");
Console.WriteLine($"    HasTimeout           : {slowExecution?.HasTimeout}");
Console.WriteLine($"    HasFailure           : {slowExecution?.HasFailure}");
Console.WriteLine($"    LastStatus           : {slowExecution?.LastStatus}");
Console.WriteLine();

var failEnvelope = HydronomEnvelopeFactory.CreateCommand(new FleetCommand
{
    SourceNodeId = "GROUND-001",
    TargetNodeId = "VEHICLE-ALPHA-001",
    CommandType = "MockFailCommand",
    AuthorityLevel = "ControlCommand",
    Priority = MessagePriority.Normal,
    Args = new Dictionary<string, string>
    {
        ["mode"] = "failure_test"
    },
    IsOperatorIssued = true,
    RequiresResult = false
});

var failRoute = ground.CommunicationRouter.Route(
    failEnvelope,
    ground.GetFleetSnapshot(),
    linkAvailabilityFilter: (_, transportKind) => transportKind == TransportKind.Mock);

var failRequest = new GroundTransportSendRequest
{
    Envelope = failEnvelope,
    UseLinkHealthRouting = false,
    TreatSuccessfulSendAsAckWhenRequired = false,
    SendTimeout = TimeSpan.FromSeconds(1),
    TryFallbacks = false,
    SendToAllForBroadcast = false,
    Reason = "Smoke test failure through Mock transport."
};

var failManagerExecution = await ground.TransportManager.SendAsync(
    failRequest,
    failRoute);

Console.WriteLine("    Failure through TransportManager:");
Console.WriteLine($"    ExecutionId          : {failManagerExecution.ExecutionId}");
Console.WriteLine($"    CanRoute             : {failManagerExecution.RouteResult.CanRoute}");
Console.WriteLine($"    IsCompleted          : {failManagerExecution.IsCompleted}");
Console.WriteLine($"    HasFailure           : {failManagerExecution.HasFailure}");
Console.WriteLine($"    LastStatus           : {failManagerExecution.LastStatus}");
Console.WriteLine($"    Send result count    : {failManagerExecution.SendResults.Count}");
Console.WriteLine();

var noTransportGround = new GroundStationEngine();
noTransportGround.HandleEnvelope(heartbeatEnvelope);

var noTransportExecution = await noTransportGround.SendEnvelopeAsync(
    commandEnvelope!,
    useLinkHealthRouting: false,
    treatSuccessfulSendAsAckWhenRequired: true,
    sendTimeout: TimeSpan.FromMilliseconds(50),
    tryFallbacks: true);

Console.WriteLine("    Link unavailable through TransportManager:");
Console.WriteLine($"    ExecutionId          : {noTransportExecution.ExecutionId}");
Console.WriteLine($"    CanRoute             : {noTransportExecution.RouteResult.CanRoute}");
Console.WriteLine($"    IsCompleted          : {noTransportExecution.IsCompleted}");
Console.WriteLine($"    HasFailure           : {noTransportExecution.HasFailure}");
Console.WriteLine($"    LastStatus           : {noTransportExecution.LastStatus}");
Console.WriteLine($"    Send result count    : {noTransportExecution.SendResults.Count}");
Console.WriteLine();

Console.WriteLine("[25] Transport registry / manager diagnostics test:");

var transportManagerSnapshot = ground.GetRouteExecutionSnapshot();
var transportManagerOperationSnapshot = ground.CreateOperationSnapshot();

Console.WriteLine($"    Registry count              : {ground.TransportRegistry.Count}");
Console.WriteLine($"    Route execution count       : {transportManagerSnapshot.Count}");
Console.WriteLine($"    Total route executions      : {transportManagerOperationSnapshot.TotalRouteExecutionCount}");
Console.WriteLine($"    Successful route executions : {transportManagerOperationSnapshot.SuccessfulRouteExecutionCount}");
Console.WriteLine($"    Acked route executions      : {transportManagerOperationSnapshot.AckedRouteExecutionCount}");
Console.WriteLine($"    Timeout route executions    : {transportManagerOperationSnapshot.TimeoutRouteExecutionCount}");
Console.WriteLine($"    Failed route executions     : {transportManagerOperationSnapshot.FailedRouteExecutionCount}");
Console.WriteLine($"    Total send results          : {transportManagerOperationSnapshot.TotalTransportSendResultCount}");
Console.WriteLine($"    Successful send results     : {transportManagerOperationSnapshot.SuccessfulTransportSendResultCount}");
Console.WriteLine($"    ACK send results            : {transportManagerOperationSnapshot.AckedTransportSendResultCount}");
Console.WriteLine($"    Timeout send results        : {transportManagerOperationSnapshot.TimeoutTransportSendResultCount}");
Console.WriteLine($"    Failed send results         : {transportManagerOperationSnapshot.FailedTransportSendResultCount}");
Console.WriteLine($"    Route summary               : {transportManagerOperationSnapshot.RouteExecutionSummary}");
Console.WriteLine($"    Link summary                : {transportManagerOperationSnapshot.LinkHealthSummary}");
Console.WriteLine($"    Has warnings                : {transportManagerOperationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues         : {transportManagerOperationSnapshot.HasCriticalIssues}");
Console.WriteLine();
Console.WriteLine("[26] Mark stale nodes offline test:");

var changed = ground.MarkStaleNodesOffline(
    timeout: TimeSpan.FromMilliseconds(1),
    nowUtc: DateTimeOffset.UtcNow.AddSeconds(10));

Console.WriteLine($"    Offline changed: {changed}");

var afterOffline = ground.GetFleetSnapshot();

foreach (var node in afterOffline)
{
    Console.WriteLine($"    {node.Identity.NodeId} online: {node.IsOnline}");
}

var offlineOperationSnapshot = ground.CreateOperationSnapshot();

Console.WriteLine();
Console.WriteLine("    Operation snapshot after stale node check:");
Console.WriteLine($"    Overall health       : {offlineOperationSnapshot.OverallHealth}");
Console.WriteLine($"    Summary              : {offlineOperationSnapshot.Summary}");
Console.WriteLine($"    Online nodes         : {offlineOperationSnapshot.OnlineNodeCount}");
Console.WriteLine($"    Offline nodes        : {offlineOperationSnapshot.OfflineNodeCount}");
Console.WriteLine($"    Link vehicles        : {offlineOperationSnapshot.LinkVehicleCount}");
Console.WriteLine($"    Total links          : {offlineOperationSnapshot.TotalLinkCount}");
Console.WriteLine($"    Link summary         : {offlineOperationSnapshot.LinkHealthSummary}");
Console.WriteLine($"    Route executions     : {offlineOperationSnapshot.TotalRouteExecutionCount}");
Console.WriteLine($"    Route summary        : {offlineOperationSnapshot.RouteExecutionSummary}");
Console.WriteLine($"    Has warnings         : {offlineOperationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues  : {offlineOperationSnapshot.HasCriticalIssues}");

Console.WriteLine();
Console.WriteLine("=== Smoke test completed ===");