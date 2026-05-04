using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Tuning;
using Hydronom.Runtime.Actuators;
using Hydronom.Runtime.AI;
using Hydronom.Runtime.Scenarios.Runtime;
using Hydronom.Core.Domain.AI;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class CommandServer
{
    private readonly string _host;
    private readonly int _port;
    private readonly ITaskManager _taskManager;
    private readonly ITuningSink? _tuning;
    private readonly ActuatorManager? _actuator;
    private readonly AiGateway? _ai;
    private readonly RuntimeScenarioController? _scenarioController;

    private TcpListener? _listener;

    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
    private int _clientIdSeq = 0;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = false
    };

    private volatile bool _armed;
    private volatile bool _emergencyStop;
    private volatile bool _manualMode;

    private long _lastHeartbeatUtcTicks;
    private long _lastCommandUtcTicks;
    private long _lastManualCommandUtcTicks;

    private ManualDriveState _manualDrive = ManualDriveState.Zero;

    public bool IsArmed => _armed;
    public bool IsEmergencyStop => _emergencyStop;
    public bool IsManualMode => _manualMode;

    public DateTime LastHeartbeatUtc => TicksToUtc(_lastHeartbeatUtcTicks);
    public DateTime LastCommandUtc => TicksToUtc(_lastCommandUtcTicks);
    public DateTime LastManualCommandUtc => TicksToUtc(_lastManualCommandUtcTicks);

    public ManualDriveState CurrentManualDrive => _manualDrive;

    public CommandServer(
        string host,
        int port,
        ITaskManager taskManager,
        ITuningSink? tuning = null,
        ActuatorManager? actuator = null,
        AiGateway? ai = null,
        RuntimeScenarioController? scenarioController = null)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host boş olamaz.", nameof(host));
        if (port <= 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port 1-65535 aralığında olmalıdır.");

        _host = host.Trim();
        _port = port;
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _tuning = tuning;
        _actuator = actuator;
        _ai = ai;
        _scenarioController = scenarioController;

        var now = DateTime.UtcNow.Ticks;
        _lastHeartbeatUtcTicks = now;
        _lastCommandUtcTicks = now;
        _lastManualCommandUtcTicks = now;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var ip = ParseIpAddress(_host);
        _listener = new TcpListener(ip, _port);
        _listener.Start();
        Console.WriteLine($"[CMD] Listening on {ip}:{_port} ...");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                client.NoDelay = true;

                var id = Interlocked.Increment(ref _clientIdSeq);
                _clients[id] = client;

                Console.WriteLine($"[CMD] Client#{id} connected. ActiveClients={_clients.Count}");
                _ = Task.Run(() => HandleClientAsync(id, client, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            try { _listener.Stop(); } catch { }
            Console.WriteLine("[CMD] Listener stopped.");

            foreach (var kv in _clients)
            {
                try { kv.Value.Close(); } catch { }
            }

            _clients.Clear();
        }
    }

    private async Task HandleClientAsync(int clientId, TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
        {
            try
            {
                var hello = new HelloDto
                {
                    hello = "Hydronom CommandServer",
                    version = 7,
                    features = new[]
                    {
                        "6DoF",
                        "AI",
                        "ArmDisarm",
                        "EmergencyStop",
                        "Heartbeat",
                        "ManualDrive",
                        "Status",
                        "RuntimeScenario"
                    }
                };

                await SafeWriteAsync(writer, JsonSerializer.Serialize(hello, JsonOpts) + "\n").ConfigureAwait(false);

                while (!ct.IsCancellationRequested)
                {
                    if (!client.Connected)
                        break;

                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync().ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (line is null)
                        break;

                    line = line.Trim();
                    if (line.Length == 0)
                        continue;

                    Console.WriteLine($"[CMD] Client#{clientId} → {line}");

                    CommandDto? cmd;
                    try
                    {
                        cmd = JsonSerializer.Deserialize<CommandDto>(line, JsonOpts);
                    }
                    catch (JsonException jex)
                    {
                        Console.WriteLine($"[CMD] JSON deserialize error: {jex.Message}");
                        await SendAckAsync(writer, ok: false, msg: "Invalid JSON").ConfigureAwait(false);
                        continue;
                    }

                    if (cmd is null)
                    {
                        await SendAckAsync(writer, ok: false, msg: "Invalid command payload").ConfigureAwait(false);
                        continue;
                    }

                    TouchCommand();

                    var type = (cmd.Type ?? string.Empty).Trim();

                    try
                    {
                        if (type.Equals("Ping", StringComparison.OrdinalIgnoreCase))
                        {
                            await SendAckAsync(writer, ok: true, msg: "Pong").ConfigureAwait(false);
                        }
                        else if (type.Equals("Heartbeat", StringComparison.OrdinalIgnoreCase))
                        {
                            TouchHeartbeat();
                            await SendAckAsync(writer, ok: true, msg: "Heartbeat accepted").ConfigureAwait(false);
                        }
                        else if (type.Equals("Arm", StringComparison.OrdinalIgnoreCase))
                        {
                            _armed = true;
                            _emergencyStop = false;
                            Console.WriteLine("[CMD] System armed.");
                            await SendAckAsync(writer, ok: true, msg: "Armed").ConfigureAwait(false);
                        }
                        else if (type.Equals("Disarm", StringComparison.OrdinalIgnoreCase))
                        {
                            _armed = false;
                            _manualMode = false;
                            _manualDrive = ManualDriveState.Zero;
                            Console.WriteLine("[CMD] System disarmed.");
                            await SendAckAsync(writer, ok: true, msg: "Disarmed").ConfigureAwait(false);
                        }
                        else if (type.Equals("EmergencyStop", StringComparison.OrdinalIgnoreCase)
                              || type.Equals("EStop", StringComparison.OrdinalIgnoreCase))
                        {
                            _emergencyStop = true;
                            _armed = false;
                            _manualMode = false;
                            _manualDrive = ManualDriveState.Zero;
                            _scenarioController?.StopScenario($"Emergency stop by client#{clientId}");
                            _taskManager.ClearTask();

                            Console.WriteLine("[CMD] Emergency stop activated. Task cleared.");
                            await SendAckAsync(writer, ok: true, msg: "EmergencyStop active").ConfigureAwait(false);
                        }
                        else if (type.Equals("ClearEmergencyStop", StringComparison.OrdinalIgnoreCase)
                              || type.Equals("ResetEStop", StringComparison.OrdinalIgnoreCase))
                        {
                            _emergencyStop = false;
                            Console.WriteLine("[CMD] Emergency stop cleared.");
                            await SendAckAsync(writer, ok: true, msg: "EmergencyStop cleared").ConfigureAwait(false);
                        }
                        else if (type.Equals("SetManualMode", StringComparison.OrdinalIgnoreCase))
                        {
                            bool enabled = cmd.Enabled ?? false;
                            _manualMode = enabled;

                            if (!enabled)
                                _manualDrive = ManualDriveState.Zero;

                            Console.WriteLine($"[CMD] Manual mode → {enabled}");
                            await SendAckAsync(writer, ok: true, msg: $"ManualMode={(enabled ? "On" : "Off")}").ConfigureAwait(false);
                        }
                        else if (type.Equals("ManualDrive", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_emergencyStop)
                            {
                                await SendAckAsync(writer, ok: false, msg: "EmergencyStop active").ConfigureAwait(false);
                                continue;
                            }

                            if (!_armed)
                            {
                                await SendAckAsync(writer, ok: false, msg: "System not armed").ConfigureAwait(false);
                                continue;
                            }

                            _manualMode = true;

                            var md = cmd.Manual ?? new ManualDriveDto();

                            _manualDrive = new ManualDriveState(
                                Surge: ClampNormalized(md.Surge ?? 0.0),
                                Sway: ClampNormalized(md.Sway ?? 0.0),
                                Heave: ClampNormalized(md.Heave ?? 0.0),
                                Roll: ClampNormalized(md.Roll ?? 0.0),
                                Pitch: ClampNormalized(md.Pitch ?? 0.0),
                                Yaw: ClampNormalized(md.Yaw ?? 0.0)
                            );

                            TouchManualCommand();

                            Console.WriteLine(
                                $"[CMD] ManualDrive set → " +
                                $"surge={_manualDrive.Surge:F2}, sway={_manualDrive.Sway:F2}, heave={_manualDrive.Heave:F2}, " +
                                $"roll={_manualDrive.Roll:F2}, pitch={_manualDrive.Pitch:F2}, yaw={_manualDrive.Yaw:F2}"
                            );

                            await SendAckAsync(writer, ok: true, msg: "ManualDrive accepted").ConfigureAwait(false);
                        }
                        else if (type.Equals("ManualStop", StringComparison.OrdinalIgnoreCase))
                        {
                            _manualDrive = ManualDriveState.Zero;
                            TouchManualCommand();
                            Console.WriteLine("[CMD] ManualDrive zeroed.");
                            await SendAckAsync(writer, ok: true, msg: "ManualDrive zeroed").ConfigureAwait(false);
                        }
                        else if (type.Equals("GoToPoint", StringComparison.OrdinalIgnoreCase) && cmd.Target != null)
                        {
                            if (_emergencyStop)
                            {
                                await SendAckAsync(writer, ok: false, msg: "EmergencyStop active").ConfigureAwait(false);
                                continue;
                            }

                            _scenarioController?.StopScenario($"GoToPoint overrides scenario by client#{clientId}");

                            var target3d = new Vec3(cmd.Target.X, cmd.Target.Y, cmd.Target.Z);
                            _manualMode = false;
                            _manualDrive = ManualDriveState.Zero;
                            _taskManager.SetTask(new TaskDefinition("GoToPoint", target3d));

                            Console.WriteLine($"[CMD] Task set → GoToPoint {target3d.X:F1},{target3d.Y:F1},{target3d.Z:F1}");
                            await SendAckAsync(
                                writer,
                                ok: true,
                                msg: $"Task=GoToPoint X={target3d.X:F1} Y={target3d.Y:F1} Z={target3d.Z:F1}"
                            ).ConfigureAwait(false);
                        }
                        else if (type.Equals("StartScenario", StringComparison.OrdinalIgnoreCase)
                              || type.Equals("RunScenario", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_scenarioController is null)
                            {
                                await SendAckAsync(writer, ok: false, msg: "Scenario controller not available").ConfigureAwait(false);
                                continue;
                            }

                            if (_emergencyStop)
                            {
                                await SendAckAsync(writer, ok: false, msg: "EmergencyStop active").ConfigureAwait(false);
                                continue;
                            }

                            _manualMode = false;
                            _manualDrive = ManualDriveState.Zero;

                            var scenarioPath = cmd.Scenario?.Path ?? cmd.ScenarioPath;

                            var snapshot = await _scenarioController.StartScenarioAsync(
                                scenarioPath,
                                requestedBy: $"client#{clientId}",
                                cancellationToken: ct).ConfigureAwait(false);

                            var lineOut = JsonSerializer.Serialize(new ScenarioStatusResultDto
                            {
                                ok = snapshot.IsRunning,
                                type = "ScenarioStatus",
                                scenario = snapshot
                            }, JsonOpts);

                            await SafeWriteAsync(writer, lineOut + "\n").ConfigureAwait(false);

                            await SendAckAsync(
                                writer,
                                ok: snapshot.IsRunning,
                                msg: snapshot.Message ?? $"Scenario state={snapshot.State}"
                            ).ConfigureAwait(false);
                        }
                        else if (type.Equals("StopScenario", StringComparison.OrdinalIgnoreCase)
                              || type.Equals("AbortScenario", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_scenarioController is null)
                            {
                                await SendAckAsync(writer, ok: false, msg: "Scenario controller not available").ConfigureAwait(false);
                                continue;
                            }

                            var snapshot = _scenarioController.StopScenario($"Stopped by client#{clientId}");

                            var lineOut = JsonSerializer.Serialize(new ScenarioStatusResultDto
                            {
                                ok = true,
                                type = "ScenarioStatus",
                                scenario = snapshot
                            }, JsonOpts);

                            await SafeWriteAsync(writer, lineOut + "\n").ConfigureAwait(false);
                            await SendAckAsync(writer, ok: true, msg: snapshot.Message ?? "Scenario stopped").ConfigureAwait(false);
                        }
                        else if (type.Equals("GetScenarioStatus", StringComparison.OrdinalIgnoreCase)
                              || type.Equals("ScenarioStatus", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_scenarioController is null)
                            {
                                await SendAckAsync(writer, ok: false, msg: "Scenario controller not available").ConfigureAwait(false);
                                continue;
                            }

                            var snapshot = _scenarioController.GetSnapshot();

                            var lineOut = JsonSerializer.Serialize(new ScenarioStatusResultDto
                            {
                                ok = true,
                                type = "ScenarioStatus",
                                scenario = snapshot
                            }, JsonOpts);

                            await SafeWriteAsync(writer, lineOut + "\n").ConfigureAwait(false);
                        }
                        else if (type.Equals("Stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _scenarioController?.StopScenario($"STOP by client#{clientId}");
                            _taskManager.ClearTask();
                            _manualDrive = ManualDriveState.Zero;
                            Console.WriteLine("[CMD] Task cleared (STOP).");
                            await SendAckAsync(writer, ok: true, msg: "Stopped").ConfigureAwait(false);
                        }
                        else if (type.Equals("AiSuggest", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_ai is null)
                            {
                                await SendAckAsync(writer, ok: false, msg: "AI not enabled on server").ConfigureAwait(false);
                            }
                            else
                            {
                                ct.ThrowIfCancellationRequested();

                                var goal = (cmd.Goal ?? string.Empty).Trim();
                                if (string.IsNullOrWhiteSpace(goal))
                                    goal = "No goal provided";

                                var context = BuildAiContext(goal);
                                var plan = await _ai.SuggestPlanAsync(goal, context, ct).ConfigureAwait(false);

                                var payload = new AiPlanDto
                                {
                                    ok = true,
                                    type = "AiSuggestResult",
                                    planId = plan.Id,
                                    goal = plan.Goal,
                                    createdUtc = plan.CreatedUtc,
                                    steps = plan.Steps.Select(s => new AiPlanStepDto
                                    {
                                        index = s.Index,
                                        title = s.Title,
                                        description = s.Description,
                                        expectedTools = s.ExpectedTools?.ToArray() ?? Array.Empty<string>()
                                    }).ToArray()
                                };

                                LogAiPlan(plan);

                                var outLine = JsonSerializer.Serialize(payload, JsonOpts);
                                await writer.WriteLineAsync(outLine).ConfigureAwait(false);
                                await writer.FlushAsync().ConfigureAwait(false);

                                await SendAckAsync(
                                    writer,
                                    ok: true,
                                    msg: $"AiSuggestResult sent planId={payload.planId} steps={payload.steps.Length}"
                                ).ConfigureAwait(false);
                            }
                        }
                        else if (type.Equals("SetLimiter", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_tuning is null)
                            {
                                await SendAckAsync(writer, false, "Tuning not enabled on server").ConfigureAwait(false);
                            }
                            else
                            {
                                double? thr = cmd.Limiter?.ThrottleRatePerSec;
                                double? rud = cmd.Limiter?.RudderRatePerSec;

                                _tuning.SetLimiter(thr, rud);

                                await SendAckAsync(
                                    writer,
                                    true,
                                    msg: $"Limiter set thr={(thr?.ToString() ?? "-")} rud={(rud?.ToString() ?? "-")}"
                                ).ConfigureAwait(false);
                            }
                        }
                        else if (type.Equals("SetAnalysis", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_tuning is null)
                            {
                                await SendAckAsync(writer, false, "Tuning not enabled on server").ConfigureAwait(false);
                            }
                            else
                            {
                                double? ahead = cmd.Analysis?.AheadDistanceM;
                                double? fov = cmd.Analysis?.HalfFovDeg;

                                _tuning.SetAnalysis(ahead, fov);

                                await SendAckAsync(
                                    writer,
                                    true,
                                    msg: $"Analysis set ahead={(ahead?.ToString() ?? "-")} fov={(fov?.ToString() ?? "-")}"
                                ).ConfigureAwait(false);
                            }
                        }
                        else if (type.Equals("SetTick", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_tuning is null)
                            {
                                await SendAckAsync(writer, false, "Tuning not enabled on server").ConfigureAwait(false);
                            }
                            else
                            {
                                _tuning.SetTick(cmd.TickMs);
                                await SendAckAsync(writer, ok: true, msg: $"Tick set {(cmd.TickMs?.ToString() ?? "-")} ms").ConfigureAwait(false);
                            }
                        }
                        else if (type.Equals("GetStatus", StringComparison.OrdinalIgnoreCase)
                              || type.Equals("Status", StringComparison.OrdinalIgnoreCase))
                        {
                            var status = BuildStatusPayload();
                            var lineOut = JsonSerializer.Serialize(status, JsonOpts);
                            await SafeWriteAsync(writer, lineOut + "\n").ConfigureAwait(false);
                        }
                        else if (type.Equals("SetSerial", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_actuator is null)
                            {
                                await SendAckAsync(writer, false, "ActuatorManager not available on server").ConfigureAwait(false);
                            }
                            else
                            {
                                string? port = string.IsNullOrWhiteSpace(cmd.Port) ? null : cmd.Port.Trim();
                                int baud = cmd.Baud ?? 115200;

                                try
                                {
                                    _actuator.SetSerialPort(port, baud);
                                    await SendAckAsync(writer, ok: true, msg: $"Serial set to {(port ?? "<disabled>")}@{baud}").ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    await SendAckAsync(writer, ok: false, msg: "SetSerial failed: " + ex.Message).ConfigureAwait(false);
                                }
                            }
                        }
                        else
                        {
                            await SendAckAsync(writer, ok: false, msg: $"Unknown Type '{type}'").ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CMD] Handle error for Type={type}: {ex.Message}");
                        await SendAckAsync(writer, ok: false, msg: "Handle error: " + ex.Message).ConfigureAwait(false);
                    }
                }
            }
            catch (IOException)
            {
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                Console.WriteLine($"[CMD] Client#{clientId} disconnected. ActiveClients={_clients.Count}");
            }
        }
    }

    private StatusDto BuildStatusPayload()
    {
        return new StatusDto
        {
            ok = true,
            type = "Status",
            armed = _armed,
            emergencyStop = _emergencyStop,
            manualMode = _manualMode,
            currentTask = DescribeCurrentTask(),
            lastHeartbeatUtc = LastHeartbeatUtc,
            lastCommandUtc = LastCommandUtc,
            lastManualCommandUtc = LastManualCommandUtc,
            activeClientCount = _clients.Count,
            aiEnabled = _ai is not null,
            manual = new ManualDriveDto
            {
                Surge = _manualDrive.Surge,
                Sway = _manualDrive.Sway,
                Heave = _manualDrive.Heave,
                Roll = _manualDrive.Roll,
                Pitch = _manualDrive.Pitch,
                Yaw = _manualDrive.Yaw
            },
            serial = _actuator is null
                ? null
                : new SerialStatusDto
                {
                    port = _actuator.SerialPortName,
                    baud = _actuator.SerialBaud,
                    isOpen = _actuator.IsSerialOpen,
                    lastError = _actuator.LastSerialError
                },
            scenario = _scenarioController?.GetSnapshot()
        };
    }

    private IReadOnlyList<AiMessage> BuildAiContext(string goal)
    {
        var ctx = new List<AiMessage>
        {
            AiMessage.System("Hydronom runtime command context attached."),
            AiMessage.User($"Current runtime state: armed={_armed}, manualMode={_manualMode}, emergencyStop={_emergencyStop}."),
            AiMessage.User($"Current task: {DescribeCurrentTask()}"),
            AiMessage.User($"Manual drive: surge={_manualDrive.Surge:F2}, sway={_manualDrive.Sway:F2}, heave={_manualDrive.Heave:F2}, roll={_manualDrive.Roll:F2}, pitch={_manualDrive.Pitch:F2}, yaw={_manualDrive.Yaw:F2}."),
            AiMessage.User($"Client requested goal: {goal}")
        };

        return ctx;
    }

    private void LogAiPlan(MissionPlan plan)
    {
        Console.WriteLine("[AI] --------------------------------------------------");
        Console.WriteLine($"[AI] PlanId : {plan.Id}");
        Console.WriteLine($"[AI] Goal   : {plan.Goal}");
        Console.WriteLine($"[AI] Created: {plan.CreatedUtc:O}");
        Console.WriteLine($"[AI] Steps  : {plan.Steps.Count}");

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            var expectedTools = step.ExpectedTools is not null && step.ExpectedTools.Count > 0
                ? string.Join(", ", step.ExpectedTools)
                : "<none>";

            Console.WriteLine($"[AI][STEP {i}] Index : {step.Index}");
            Console.WriteLine($"[AI][STEP {i}] Title : {step.Title}");
            Console.WriteLine($"[AI][STEP {i}] Desc  : {step.Description}");
            Console.WriteLine($"[AI][STEP {i}] Tools : {expectedTools}");
        }

        Console.WriteLine("[AI] --------------------------------------------------");
    }

    private string DescribeCurrentTask()
    {
        var task = _taskManager.CurrentTask;
        if (task is null)
            return "none";

        var name = TryReadStringProperty(task, "Name", "Title", "TaskName", "Label")
                   ?? task.GetType().Name;

        var target = TryReadTargetString(task);

        if (!string.IsNullOrWhiteSpace(target))
            return $"{name} {target}";

        return name;
    }

    private static string? TryReadStringProperty(object obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop is null) continue;

                var value = prop.GetValue(obj);
                var text = value?.ToString()?.Trim();

                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? TryReadTargetString(object obj)
    {
        try
        {
            var prop = obj.GetType().GetProperty("Target", BindingFlags.Instance | BindingFlags.Public);
            if (prop is null)
                return null;

            var value = prop.GetValue(obj);
            if (value is null)
                return null;

            if (value is Vec3 v3)
                return $"({v3.X:F1},{v3.Y:F1},{v3.Z:F1})";

            return value.ToString();
        }
        catch
        {
            return null;
        }
    }

    private void TouchHeartbeat()
    {
        Interlocked.Exchange(ref _lastHeartbeatUtcTicks, DateTime.UtcNow.Ticks);
    }

    private void TouchCommand()
    {
        Interlocked.Exchange(ref _lastCommandUtcTicks, DateTime.UtcNow.Ticks);
    }

    private void TouchManualCommand()
    {
        Interlocked.Exchange(ref _lastManualCommandUtcTicks, DateTime.UtcNow.Ticks);
    }

    private static DateTime TicksToUtc(long ticks)
    {
        if (ticks <= 0) return DateTime.MinValue;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static async Task SendAckAsync(StreamWriter writer, bool ok, string msg)
    {
        var ack = JsonSerializer.Serialize(new AckDto { ok = ok, msg = msg }, JsonOpts);
        await SafeWriteAsync(writer, ack + "\n").ConfigureAwait(false);
    }

    private static async Task SafeWriteAsync(StreamWriter writer, string text)
    {
        try
        {
            await writer.WriteAsync(text).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static double ClampNormalized(double value)
    {
        if (value < -1.0) return -1.0;
        if (value > 1.0) return 1.0;
        return value;
    }

    private static IPAddress ParseIpAddress(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host == "*" || host == "0.0.0.0")
            return IPAddress.Any;

        if (host == "::")
            return IPAddress.IPv6Any;

        if (IPAddress.TryParse(host, out var ip))
            return ip;

        try
        {
            var entry = Dns.GetHostEntry(host);
            foreach (var a in entry.AddressList)
            {
                if (a.AddressFamily == AddressFamily.InterNetwork)
                    return a;
            }

            return entry.AddressList.First();
        }
        catch
        {
            return IPAddress.Any;
        }
    }

    private sealed class CommandDto
    {
        public string? Type { get; set; }
        public bool? Enabled { get; set; }
        public TargetDto? Target { get; set; }
        public ManualDriveDto? Manual { get; set; }
        public LimiterDto? Limiter { get; set; }
        public AnalysisDto? Analysis { get; set; }
        public int? TickMs { get; set; }
        public string? Port { get; set; }
        public int? Baud { get; set; }
        public string? Goal { get; set; }
        public string? ScenarioPath { get; set; }
        public ScenarioCommandDto? Scenario { get; set; }
    }

    private sealed class ScenarioCommandDto
    {
        public string? Path { get; set; }
    }

    private sealed class ScenarioStatusResultDto
    {
        public bool ok { get; set; }
        public string type { get; set; } = "ScenarioStatus";
        public RuntimeScenarioSnapshot? scenario { get; set; }
    }

    private sealed class TargetDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; } = 0.0;
    }

    public sealed class ManualDriveDto
    {
        public double? Surge { get; set; }
        public double? Sway { get; set; }
        public double? Heave { get; set; }
        public double? Roll { get; set; }
        public double? Pitch { get; set; }
        public double? Yaw { get; set; }
    }

    public readonly record struct ManualDriveState(
        double Surge,
        double Sway,
        double Heave,
        double Roll,
        double Pitch,
        double Yaw)
    {
        public static ManualDriveState Zero => new(0, 0, 0, 0, 0, 0);
    }

    private sealed class LimiterDto
    {
        public double? ThrottleRatePerSec { get; set; }
        public double? RudderRatePerSec { get; set; }
    }

    private sealed class AnalysisDto
    {
        public double? AheadDistanceM { get; set; }
        public double? HalfFovDeg { get; set; }
    }

    private sealed class AckDto
    {
        public bool ok { get; set; }
        public string msg { get; set; } = "";
    }

    private sealed class HelloDto
    {
        public string hello { get; set; } = "";
        public int version { get; set; }
        public string[] features { get; set; } = Array.Empty<string>();
    }

    private sealed class StatusDto
    {
        public bool ok { get; set; }
        public string type { get; set; } = "Status";
        public bool armed { get; set; }
        public bool emergencyStop { get; set; }
        public bool manualMode { get; set; }
        public string? currentTask { get; set; }
        public DateTime lastHeartbeatUtc { get; set; }
        public DateTime lastCommandUtc { get; set; }
        public DateTime lastManualCommandUtc { get; set; }
        public int activeClientCount { get; set; }
        public bool aiEnabled { get; set; }
        public ManualDriveDto? manual { get; set; }
        public SerialStatusDto? serial { get; set; }
        public RuntimeScenarioSnapshot? scenario { get; set; }
    }

    private sealed class SerialStatusDto
    {
        public string? port { get; set; }
        public int baud { get; set; }
        public bool isOpen { get; set; }
        public string? lastError { get; set; }
    }

    private sealed class AiPlanDto
    {
        public bool ok { get; set; }
        public string type { get; set; } = "AiSuggestResult";
        public string planId { get; set; } = "";
        public string goal { get; set; } = "";
        public DateTime createdUtc { get; set; }
        public AiPlanStepDto[] steps { get; set; } = Array.Empty<AiPlanStepDto>();
    }

    private sealed class AiPlanStepDto
    {
        public int index { get; set; }
        public string title { get; set; } = "";
        public string description { get; set; } = "";
        public string[] expectedTools { get; set; } = Array.Empty<string>();
    }
}