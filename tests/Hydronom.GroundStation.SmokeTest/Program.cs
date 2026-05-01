using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.Coordination;
using Hydronom.GroundStation.Routing;
using Hydronom.GroundStation.Telemetry;
using Hydronom.GroundStation.WorldModel;
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
    linkHealthSnapshot);

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
    lostSnapshot);

Console.WriteLine();
Console.WriteLine("    Operation snapshot after link loss simulation:");
Console.WriteLine($"    Overall health       : {lostLinkOperationSnapshot.OverallHealth}");
Console.WriteLine($"    Summary              : {lostLinkOperationSnapshot.Summary}");
Console.WriteLine($"    Link health summary  : {lostLinkOperationSnapshot.LinkHealthSummary}");
Console.WriteLine($"    Good links           : {lostLinkOperationSnapshot.GoodLinkCount}");
Console.WriteLine($"    Degraded links       : {lostLinkOperationSnapshot.DegradedLinkCount}");
Console.WriteLine($"    Critical links       : {lostLinkOperationSnapshot.CriticalLinkCount}");
Console.WriteLine($"    Lost links           : {lostLinkOperationSnapshot.LostLinkCount}");
Console.WriteLine($"    Has warnings         : {lostLinkOperationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues  : {lostLinkOperationSnapshot.HasCriticalIssues}");
Console.WriteLine();

Console.WriteLine("[18] Mark stale nodes offline test:");

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
Console.WriteLine($"    Has warnings         : {offlineOperationSnapshot.HasWarnings}");
Console.WriteLine($"    Has critical issues  : {offlineOperationSnapshot.HasCriticalIssues}");

Console.WriteLine();
Console.WriteLine("=== Smoke test completed ===");