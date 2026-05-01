using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation;
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

Console.WriteLine("[4] FleetCommandResult dispatch test:");

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

var commandEnvelope = HydronomEnvelopeFactory.CreateCommand(command);

Console.WriteLine("    Ground produced command envelope:");
Console.WriteLine($"    CommandId   : {command.CommandId}");
Console.WriteLine($"    MessageType : {commandEnvelope.MessageType}");
Console.WriteLine($"    Source      : {commandEnvelope.SourceNodeId}");
Console.WriteLine($"    Target      : {commandEnvelope.TargetNodeId}");
Console.WriteLine($"    Priority    : {commandEnvelope.Priority}");

var commandResult = new FleetCommandResult
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

var commandResultEnvelope = HydronomEnvelopeFactory.CreateCommandResult(commandResult);
var commandResultHandled = ground.HandleEnvelope(commandResultEnvelope);

Console.WriteLine();
Console.WriteLine("    Vehicle returned command result envelope:");
Console.WriteLine($"    ResultId    : {commandResult.ResultId}");
Console.WriteLine($"    MessageType : {commandResultEnvelope.MessageType}");
Console.WriteLine($"    Source      : {commandResultEnvelope.SourceNodeId}");
Console.WriteLine($"    Target      : {commandResultEnvelope.TargetNodeId}");
Console.WriteLine($"    Status      : {commandResult.Status}");
Console.WriteLine($"    Success     : {commandResult.Success}");
Console.WriteLine($"    Handled     : {commandResultHandled}");
Console.WriteLine();

Console.WriteLine("[5] Mark stale nodes offline test:");

var changed = ground.MarkStaleNodesOffline(
    timeout: TimeSpan.FromMilliseconds(1),
    nowUtc: DateTimeOffset.UtcNow.AddSeconds(10));

Console.WriteLine($"    Offline changed: {changed}");

var afterOffline = ground.GetFleetSnapshot();

foreach (var node in afterOffline)
{
    Console.WriteLine($"    {node.Identity.NodeId} online: {node.IsOnline}");
}

Console.WriteLine();
Console.WriteLine("=== Smoke test completed ===");