using System.Text;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Pipeline;
using Hydronom.Core.Communication.Telemetry;
using Hydronom.Core.Communication.Transport;
using Hydronom.Core.Communication.Transport.SerialRf;

internal static class Program
{
    private const string PayloadText = "text";
    private const string PayloadPipeline = "pipeline";
    private const string PayloadAuto = "auto";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var mode = GetArg(args, "--mode", "receive").Trim().ToLowerInvariant();
        var portName = GetArg(args, "--port", "");
        var baudText = GetArg(args, "--baud", "57600");
        var message = GetArg(args, "--message", "HYDRONOM_SERIAL_RF_PACKET_TEST");
        var countText = GetArg(args, "--count", "1");
        var intervalText = GetArg(args, "--interval-ms", "500");
        var payloadMode = GetArg(args, "--payload", PayloadText).Trim().ToLowerInvariant();
        var quietText = GetArg(args, "--quiet", "false");

        if (string.IsNullOrWhiteSpace(portName))
        {
            Console.WriteLine("Kullanim:");
            Console.WriteLine("  receive text    : dotnet run -- --mode receive --port COM9 --payload text");
            Console.WriteLine("  send text       : dotnet run -- --mode send --port COM5 --payload text --message TEST --count 5");
            Console.WriteLine("  receive pipeline: dotnet run -- --mode receive --port COM9 --payload pipeline");
            Console.WriteLine("  send pipeline   : dotnet run -- --mode send --port COM5 --payload pipeline --count 100");
            Console.WriteLine();
            Console.WriteLine("Not:");
            Console.WriteLine("  --payload auto receiver tarafinda once pipeline decode dener, olmazsa text gibi basar.");
            return 2;
        }

        if (payloadMode is not PayloadText and not PayloadPipeline and not PayloadAuto)
        {
            Console.WriteLine($"Gecersiz --payload: {payloadMode}");
            Console.WriteLine("Gecerli degerler: text, pipeline, auto");
            return 2;
        }

        var baudRate = int.TryParse(baudText, out var parsedBaud)
            ? parsedBaud
            : 57_600;

        var count = int.TryParse(countText, out var parsedCount)
            ? Math.Max(1, parsedCount)
            : 1;

        var intervalMs = int.TryParse(intervalText, out var parsedInterval)
            ? Math.Max(0, parsedInterval)
            : 500;

        var quiet = bool.TryParse(quietText, out var parsedQuiet) && parsedQuiet;

        var options = SerialRfHydronomTransportOptions.SiK433(
                portName: portName,
                transportId: $"serial-rf-smoke-{portName}")
            with
            {
                BaudRate = baudRate,
                SourceId = mode == "send" ? "ground-smoke" : "vehicle-smoke",
                TargetId = mode == "send" ? "vehicle-smoke" : "ground-smoke",
                ChannelId = payloadMode == PayloadPipeline
                    ? "serial-rf-pipeline-smoke"
                    : "serial-rf-smoke",
                ReadTimeoutMs = 250,
                WriteTimeoutMs = 1000,
                ReceiveChannelCapacity = 512,
                MaxPayloadBytes = 8 * 1024,
                DropInvalidFrames = true
            };

        await using var transport = new SerialRfHydronomPacketTransport(options);

        Console.WriteLine("======================================================");
        Console.WriteLine("Hydronom Serial RF Smoke Test");
        Console.WriteLine("======================================================");
        Console.WriteLine($"Mode        : {mode}");
        Console.WriteLine($"Payload     : {payloadMode}");
        Console.WriteLine($"Port        : {portName}");
        Console.WriteLine($"Baud        : {baudRate}");
        Console.WriteLine($"Count       : {count}");
        Console.WriteLine($"IntervalMs  : {intervalMs}");
        Console.WriteLine($"Transport   : {transport.TransportId}");
        Console.WriteLine("======================================================");

        await transport.StartAsync();

        try
        {
            if (mode == "send")
            {
                await RunSenderAsync(
                    transport,
                    payloadMode == PayloadAuto ? PayloadPipeline : payloadMode,
                    message,
                    count,
                    intervalMs,
                    quiet);
            }
            else if (mode == "receive")
            {
                await RunReceiverAsync(
                    transport,
                    payloadMode,
                    quiet);
            }
            else
            {
                Console.WriteLine($"Bilinmeyen mode: {mode}");
                return 2;
            }

            return 0;
        }
        finally
        {
            var stats = transport.SnapshotStats();

            Console.WriteLine();
            Console.WriteLine("======================================================");
            Console.WriteLine("Transport Stats");
            Console.WriteLine("======================================================");
            Console.WriteLine($"Running       : {stats.IsRunning}");
            Console.WriteLine($"SentPackets   : {stats.SentPackets}");
            Console.WriteLine($"Received      : {stats.ReceivedPackets}");
            Console.WriteLine($"SentBytes     : {stats.SentBytes}");
            Console.WriteLine($"ReceivedBytes : {stats.ReceivedBytes}");
            Console.WriteLine($"Dropped       : {stats.DroppedPackets}");
            Console.WriteLine($"PendingRx     : {stats.PendingReceivePackets}");
            Console.WriteLine("======================================================");

            await transport.StopAsync();
        }
    }

    private static async Task RunSenderAsync(
        SerialRfHydronomPacketTransport transport,
        string payloadMode,
        string message,
        int count,
        int intervalMs,
        bool quiet)
    {
        Console.WriteLine("Sender basladi.");

        var pipelineOptions = CreatePipelineOptions(
            sourceId: "ground-pipeline-smoke",
            targetId: "vehicle-pipeline-smoke");

        var pipeline = new HydronomCommunicationPipeline(pipelineOptions);

        var sent = 0;
        var skipped = 0;
        var totalTransportBytes = 0;
        var totalPipelinePayloadBytes = 0;
        var totalPipelineEncodedBytes = 0;

        var startedAt = DateTimeOffset.UtcNow;

        for (var i = 1; i <= count; i++)
        {
            HydronomTransportPacket? packet;

            if (payloadMode == PayloadPipeline)
            {
                var frame = BuildTelemetryFrame((ulong)i);

                var outgoing = pipeline.BuildOutgoingTelemetryPacket(
                    currentFrame: frame,
                    priority: i % 10 == 0
                        ? HydronomMessagePriority.Critical
                        : HydronomMessagePriority.High,
                    extraFlags: i == 1
                        ? HydronomMessageFlags.RequiresAck | HydronomMessageFlags.IsSnapshot
                        : HydronomMessageFlags.None,
                    correlationId: $"serial-rf-pipeline-smoke-{i}");

                if (!outgoing.ShouldSend)
                {
                    skipped++;

                    if (!quiet)
                    {
                        Console.WriteLine($">> SKIP pipeline seq={i} reason={outgoing.Reason}");
                    }

                    continue;
                }

                totalPipelinePayloadBytes += outgoing.PayloadBytes;
                totalPipelineEncodedBytes += outgoing.PacketBytes;

                packet = HydronomTransportPacket.Create(
                    bytes: outgoing.EncodedMessage.Bytes,
                    sourceId: "ground-smoke",
                    targetId: "vehicle-smoke",
                    transportId: transport.TransportId,
                    channelId: "serial-rf-pipeline-smoke",
                    sequence: (ulong)i);

                if (!quiet)
                {
                    Console.WriteLine(
                        $">> Pipeline packet sent seq={i} transportBytes={packet.SizeBytes} encodedBytes={outgoing.PacketBytes} payloadBytes={outgoing.PayloadBytes} reason={outgoing.Reason} flags={outgoing.EncodedMessage.Flags}");
                }
            }
            else
            {
                var text = $"{message} #{i} utc={DateTimeOffset.UtcNow:O}";
                var bytes = Encoding.UTF8.GetBytes(text);

                packet = HydronomTransportPacket.Create(
                    bytes: bytes,
                    sourceId: "ground-smoke",
                    targetId: "vehicle-smoke",
                    transportId: transport.TransportId,
                    channelId: "serial-rf-smoke",
                    sequence: (ulong)i);

                if (!quiet)
                {
                    Console.WriteLine($">> Text packet sent seq={packet.Sequence} bytes={packet.SizeBytes} text={text}");
                }
            }

            await transport.SendAsync(packet);

            sent++;
            totalTransportBytes += packet.SizeBytes;

            if (quiet && (i % 50 == 0 || i == count))
            {
                Console.WriteLine($">> progress sent={sent}/{count} skipped={skipped}");
            }

            if (intervalMs > 0)
            {
                await Task.Delay(intervalMs);
            }
        }

        await Task.Delay(1000);

        var duration = DateTimeOffset.UtcNow - startedAt;

        Console.WriteLine();
        Console.WriteLine("======================================================");
        Console.WriteLine("Sender Summary");
        Console.WriteLine("======================================================");
        Console.WriteLine($"PayloadMode           : {payloadMode}");
        Console.WriteLine($"Sent                  : {sent}");
        Console.WriteLine($"Skipped               : {skipped}");
        Console.WriteLine($"TransportBytes        : {totalTransportBytes}");
        Console.WriteLine($"PipelinePayloadBytes  : {totalPipelinePayloadBytes}");
        Console.WriteLine($"PipelineEncodedBytes  : {totalPipelineEncodedBytes}");
        Console.WriteLine($"DurationSeconds       : {duration.TotalSeconds:F2}");

        if (duration.TotalSeconds > 0)
        {
            Console.WriteLine($"PacketsPerSecond      : {sent / duration.TotalSeconds:F2}");
            Console.WriteLine($"BytesPerSecond        : {totalTransportBytes / duration.TotalSeconds:F2}");
        }

        Console.WriteLine("======================================================");
    }

    private static async Task RunReceiverAsync(
        SerialRfHydronomPacketTransport transport,
        string payloadMode,
        bool quiet)
    {
        Console.WriteLine("Receiver basladi. Ctrl+C ile kapat.");

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var pipelineOptions = CreatePipelineOptions(
            sourceId: "ground-pipeline-smoke",
            targetId: "vehicle-pipeline-smoke");

        var pipeline = new HydronomCommunicationPipeline(pipelineOptions);
        var analyzer = new ReceiveAnalyzer(payloadMode);

        try
        {
            await foreach (var packet in transport.ReceiveAllAsync(cts.Token))
            {
                analyzer.RecordTransportPacket(packet);

                var handledAsPipeline = false;

                if (payloadMode is PayloadPipeline or PayloadAuto)
                {
                    var incoming = pipeline.ReadIncomingTelemetryPacket(packet.Bytes);

                    if (incoming.Accepted && incoming.TelemetryFrame is not null)
                    {
                        handledAsPipeline = true;
                        analyzer.RecordPipelineAccepted(incoming);

                        if (!quiet)
                        {
                            PrintPipelinePacket(packet, incoming);
                        }
                    }
                    else
                    {
                        analyzer.RecordPipelineRejected(incoming);

                        if (payloadMode == PayloadPipeline)
                        {
                            if (!quiet)
                            {
                                PrintPipelineReject(packet, incoming);
                            }

                            continue;
                        }
                    }
                }

                if (payloadMode == PayloadText ||
                    (payloadMode == PayloadAuto && !handledAsPipeline))
                {
                    analyzer.RecordTextAccepted();

                    if (!quiet)
                    {
                        PrintTextPacket(packet);
                    }
                }

                if (quiet && analyzer.ReceivedPackets % 50 == 0)
                {
                    Console.WriteLine(
                        $"RX progress received={analyzer.ReceivedPackets} missing={analyzer.MissingSequences} pipelineOk={analyzer.PipelineAccepted} pipelineRejected={analyzer.PipelineRejected}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Receiver durduruldu.");
        }
        finally
        {
            analyzer.PrintSummary();
        }
    }

    private static void PrintTextPacket(HydronomTransportPacket packet)
    {
        var text = Encoding.UTF8.GetString(packet.Bytes);

        Console.WriteLine();
        Console.WriteLine("<< Text packet received");
        Console.WriteLine($"   Transport : {packet.TransportId}");
        Console.WriteLine($"   Source    : {packet.SourceId}");
        Console.WriteLine($"   Target    : {packet.TargetId}");
        Console.WriteLine($"   Channel   : {packet.ChannelId}");
        Console.WriteLine($"   Sequence  : {packet.Sequence}");
        Console.WriteLine($"   Size      : {packet.SizeBytes}");
        Console.WriteLine($"   CreatedAt : {packet.CreatedAt:O}");
        Console.WriteLine($"   Text      : {text}");
    }

    private static void PrintPipelinePacket(
        HydronomTransportPacket packet,
        HydronomIncomingPacket incoming)
    {
        var frame = incoming.TelemetryFrame!;
        var envelope = incoming.Envelope;

        Console.WriteLine();
        Console.WriteLine("<< Pipeline packet received");
        Console.WriteLine($"   TransportSeq : {packet.Sequence}");
        Console.WriteLine($"   TransportSize: {packet.SizeBytes}");
        Console.WriteLine($"   Accepted     : {incoming.Accepted}");
        Console.WriteLine($"   Reason       : {incoming.Reason}");
        Console.WriteLine($"   Security     : {incoming.SecurityResult?.Accepted}");
        Console.WriteLine($"   Vehicle      : {frame.VehicleId}");
        Console.WriteLine($"   FrameSeq     : {frame.Sequence}");
        Console.WriteLine($"   FieldMask    : {frame.FieldMask}");
        Console.WriteLine($"   Position     : X={frame.PositionXM:F2}, Y={frame.PositionYM:F2}, Z={frame.PositionZM:F2}");
        Console.WriteLine($"   Speed/Risk   : speed={frame.SpeedMps:F2}, risk={frame.RiskScore01:F3}");
        Console.WriteLine($"   Battery      : {frame.BatteryVoltageV:F2} V, {frame.BatteryPercent:F1}%");

        if (envelope is not null)
        {
            Console.WriteLine($"   EnvelopeFlags: {envelope.Flags}");
            Console.WriteLine($"   ContentType  : {envelope.ContentType}");
            Console.WriteLine($"   SecurityTag  : {(envelope.SecurityTag is { Length: > 0 } ? envelope.SecurityTag.Length + " bytes" : "none")}");
        }
    }

    private static void PrintPipelineReject(
        HydronomTransportPacket packet,
        HydronomIncomingPacket incoming)
    {
        Console.WriteLine();
        Console.WriteLine("<< Pipeline packet rejected");
        Console.WriteLine($"   TransportSeq : {packet.Sequence}");
        Console.WriteLine($"   TransportSize: {packet.SizeBytes}");
        Console.WriteLine($"   Accepted     : {incoming.Accepted}");
        Console.WriteLine($"   Reason       : {incoming.Reason}");
        Console.WriteLine($"   Security     : {incoming.SecurityResult?.Accepted}");
    }

    private static HydronomCommunicationPipelineOptions CreatePipelineOptions(
        string sourceId,
        string targetId)
    {
        return HydronomCommunicationPipelineOptions.Race with
        {
            SourceId = sourceId,
            TargetId = targetId,
            SessionId = "serial-rf-pipeline-smoke-session",
            HmacSecretKey = "hydronom-serial-rf-pipeline-smoke-secret-key",
            EnableDeltaTelemetry = true,
            EnableSecurity = true,
            RequireTelemetryChange = false
        };
    }

    private static CompactTelemetryFrame BuildTelemetryFrame(ulong sequence)
    {
        var t = (double)sequence;

        var full = CompactTelemetryFrame.Full(
            vehicleId: "HYDRONOM-RF-PIPELINE-001",
            sequence: sequence);

        return full with
        {
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),

            PositionXM = 12.0 + t * 0.07,
            PositionYM = 8.0 + Math.Sin(t * 0.12) * 1.5,
            PositionZM = -0.2 + Math.Cos(t * 0.08) * 0.05,

            RollRad = 0.01 * Math.Sin(t * 0.1),
            PitchRad = -0.02 * Math.Cos(t * 0.1),
            YawRad = 1.0 + t * 0.01,

            VelocityXMps = 0.45 + 0.002 * t,
            VelocityYMps = 0.03 * Math.Sin(t * 0.2),
            VelocityZMps = 0.0,

            AngularVelocityXRadps = 0.001 + 0.0001 * t,
            AngularVelocityYRadps = -0.002 + 0.0001 * t,
            AngularVelocityZRadps = 0.03 + 0.0002 * t,

            SpeedMps = 0.45 + 0.003 * t,
            HeadingErrorRad = 0.25 / (t + 1.0),
            DistanceToTargetM = Math.Max(0.0, 80.0 - t * 0.25),

            BatteryVoltageV = 15.8 - t * 0.001,
            BatteryPercent = Math.Max(0.0, 92.0 - t * 0.02),

            MissionProgress01 = Math.Min(1.0, t / 500.0),
            RiskScore01 = Math.Min(1.0, 0.05 + Math.Abs(Math.Sin(t * 0.08)) * 0.2),

            ForceXN = 8.0 + t * 0.02,
            ForceYN = 0.5 * Math.Sin(t * 0.2),
            ForceZN = 0.1 * Math.Cos(t * 0.1),

            TorqueXNm = 0.01 * Math.Sin(t * 0.1),
            TorqueYNm = 0.01 * Math.Cos(t * 0.1),
            TorqueZNm = 0.2 + 0.001 * t
        };
    }

    private static string GetArg(string[] args, string name, string fallback)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return fallback;
    }

    private sealed class ReceiveAnalyzer
    {
        private readonly string _payloadMode;
        private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
        private readonly HashSet<ulong> _seenSequences = new();

        private ulong? _firstSequence;
        private ulong? _lastSequence;
        private ulong _highestSequence;
        private ulong _longestGap;

        private long _receivedBytes;

        public ReceiveAnalyzer(string payloadMode)
        {
            _payloadMode = payloadMode;
        }

        public int ReceivedPackets { get; private set; }

        public int DuplicatePackets { get; private set; }

        public int OutOfOrderPackets { get; private set; }

        public ulong MissingSequences { get; private set; }

        public int PipelineAccepted { get; private set; }

        public int PipelineRejected { get; private set; }

        public int TextAccepted { get; private set; }

        public void RecordTransportPacket(HydronomTransportPacket packet)
        {
            ReceivedPackets++;
            _receivedBytes += packet.SizeBytes;

            if (_firstSequence is null)
            {
                _firstSequence = packet.Sequence;
                _highestSequence = packet.Sequence;
            }

            if (_seenSequences.Contains(packet.Sequence))
            {
                DuplicatePackets++;
            }
            else
            {
                _seenSequences.Add(packet.Sequence);
            }

            if (_lastSequence is not null && packet.Sequence < _lastSequence.Value)
            {
                OutOfOrderPackets++;
            }

            if (packet.Sequence > _highestSequence + 1)
            {
                var gap = packet.Sequence - _highestSequence - 1;
                MissingSequences += gap;

                if (gap > _longestGap)
                {
                    _longestGap = gap;
                }
            }

            if (packet.Sequence > _highestSequence)
            {
                _highestSequence = packet.Sequence;
            }

            _lastSequence = packet.Sequence;
        }

        public void RecordPipelineAccepted(HydronomIncomingPacket _)
        {
            PipelineAccepted++;
        }

        public void RecordPipelineRejected(HydronomIncomingPacket _)
        {
            PipelineRejected++;
        }

        public void RecordTextAccepted()
        {
            TextAccepted++;
        }

        public void PrintSummary()
        {
            var duration = DateTimeOffset.UtcNow - _startedAt;
            var observedRange = _firstSequence is null
                ? 0
                : _highestSequence - _firstSequence.Value + 1;

            var estimatedLossPercent = observedRange > 0
                ? MissingSequences * 100.0 / observedRange
                : 0.0;

            Console.WriteLine();
            Console.WriteLine("======================================================");
            Console.WriteLine("Receiver Analysis");
            Console.WriteLine("======================================================");
            Console.WriteLine($"PayloadMode       : {_payloadMode}");
            Console.WriteLine($"ReceivedPackets   : {ReceivedPackets}");
            Console.WriteLine($"FirstSequence     : {(_firstSequence?.ToString() ?? "N/A")}");
            Console.WriteLine($"LastSequence      : {(_lastSequence?.ToString() ?? "N/A")}");
            Console.WriteLine($"HighestSequence   : {(_firstSequence is null ? "N/A" : _highestSequence.ToString())}");
            Console.WriteLine($"ObservedRange     : {observedRange}");
            Console.WriteLine($"MissingSequences  : {MissingSequences}");
            Console.WriteLine($"DuplicatePackets  : {DuplicatePackets}");
            Console.WriteLine($"OutOfOrderPackets : {OutOfOrderPackets}");
            Console.WriteLine($"LongestGap        : {_longestGap}");
            Console.WriteLine($"EstimatedLoss     : {estimatedLossPercent:F2}%");
            Console.WriteLine($"PipelineAccepted  : {PipelineAccepted}");
            Console.WriteLine($"PipelineRejected  : {PipelineRejected}");
            Console.WriteLine($"TextAccepted      : {TextAccepted}");
            Console.WriteLine($"ReceivedBytes     : {_receivedBytes}");
            Console.WriteLine($"DurationSeconds   : {duration.TotalSeconds:F2}");

            if (duration.TotalSeconds > 0)
            {
                Console.WriteLine($"PacketsPerSecond  : {ReceivedPackets / duration.TotalSeconds:F2}");
                Console.WriteLine($"BytesPerSecond    : {_receivedBytes / duration.TotalSeconds:F2}");
            }

            Console.WriteLine("======================================================");
        }
    }
}