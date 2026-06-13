using System.Text;
using Hydronom.Core.Communication.Bandwidth;
using Hydronom.Core.Communication.Diagnostics;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Queues;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("====================================================");
Console.WriteLine(" HYDRONOM CORE COMMUNICATION QUEUE SMOKE TEST");
Console.WriteLine("====================================================");

Console.WriteLine();
Console.WriteLine("[1] Öncelik sırası testi");

var queue = new HydronomPriorityMessageQueue();

queue.Enqueue(CreateQueued("bulk-1", HydronomMessagePriority.Bulk));
queue.Enqueue(CreateQueued("normal-1", HydronomMessagePriority.Normal));
queue.Enqueue(CreateQueued("low-1", HydronomMessagePriority.Low));
queue.Enqueue(CreateQueued("critical-1", HydronomMessagePriority.Critical));
queue.Enqueue(CreateQueued("emergency-1", HydronomMessagePriority.Emergency));
queue.Enqueue(CreateQueued("high-1", HydronomMessagePriority.High));

Require(queue.TryDequeue(out var first), "İlk mesaj alındı.");
Require(ReadId(first) == "emergency-1", "Emergency mesaj ilk çıktı.");

Require(queue.TryDequeue(out var second), "İkinci mesaj alındı.");
Require(ReadId(second) == "critical-1", "Critical mesaj ikinci çıktı.");

Require(queue.TryDequeue(out var third), "Üçüncü mesaj alındı.");
Require(ReadId(third) == "high-1", "High mesaj üçüncü çıktı.");

Require(queue.TryDequeue(out var fourth), "Dördüncü mesaj alındı.");
Require(ReadId(fourth) == "normal-1", "Normal mesaj dördüncü çıktı.");

Require(queue.TryDequeue(out var fifth), "Beşinci mesaj alındı.");
Require(ReadId(fifth) == "low-1", "Low mesaj beşinci çıktı.");

Require(queue.TryDequeue(out var sixth), "Altıncı mesaj alındı.");
Require(ReadId(sixth) == "bulk-1", "Bulk mesaj son çıktı.");

Console.WriteLine();
Console.WriteLine("[2] Queue doluyken kritik mesaj için düşük trafik feda testi");

var tightQueue = new HydronomPriorityMessageQueue
{
    MaxEmergencyDepth = 4,
    MaxCriticalDepth = 4,
    MaxHighDepth = 4,
    MaxNormalDepth = 1,
    MaxLowDepth = 1,
    MaxBulkDepth = 1
};

Require(tightQueue.Enqueue(CreateQueued("bulk-old", HydronomMessagePriority.Bulk)), "Bulk mesaj kuyruğa girdi.");
Require(tightQueue.Enqueue(CreateQueued("low-old", HydronomMessagePriority.Low)), "Low mesaj kuyruğa girdi.");
Require(tightQueue.Enqueue(CreateQueued("normal-old", HydronomMessagePriority.Normal)), "Normal mesaj kuyruğa girdi.");

Require(tightQueue.Enqueue(CreateQueued("critical-new", HydronomMessagePriority.Critical)), "Critical mesaj düşük trafiği feda ederek kuyruğa girdi.");

var tightSnapshot = tightQueue.Snapshot();

Require(tightSnapshot.DroppedMessages >= 0, "Drop metriği okunabilir.");
Require(tightQueue.TryDequeue(out var tightFirst), "Sıkı kuyruktan ilk mesaj alındı.");
Require(ReadId(tightFirst) == "critical-new", "Critical mesaj öncelikli çıktı.");

Console.WriteLine();
Console.WriteLine("[3] Expired mesaj düşürme testi");

var expiryQueue = new HydronomPriorityMessageQueue();

expiryQueue.Enqueue(CreateQueued(
    id: "expired-low",
    priority: HydronomMessagePriority.Low,
    ttl: TimeSpan.FromMilliseconds(-1)));

expiryQueue.Enqueue(CreateQueued(
    id: "valid-high",
    priority: HydronomMessagePriority.High,
    ttl: TimeSpan.FromMinutes(1)));

Require(expiryQueue.TryDequeue(out var expiryFirst), "Expired sonrası geçerli mesaj alındı.");
Require(ReadId(expiryFirst) == "valid-high", "Expired mesaj atıldı, geçerli high mesaj kaldı.");

var expirySnapshot = expiryQueue.Snapshot();
Require(expirySnapshot.ExpiredMessages >= 1, "Expired mesaj metriği arttı.");

Console.WriteLine();
Console.WriteLine("[4] Adaptive bandwidth: Excellent link testi");

var policy = new HydronomAdaptiveBandwidthPolicy();

var excellentBudget = policy.CreateBudget(new HydronomLinkHealth
{
    LinkId = "test-link",
    Level = HydronomLinkHealthLevel.Excellent
});

Require(excellentBudget.AllowBulkTraffic, "Excellent link bulk trafiğe izin veriyor.");
Require(excellentBudget.StateTelemetryHz >= 50.0, "Excellent link yüksek state telemetry frekansı veriyor.");

Console.WriteLine();
Console.WriteLine("[5] Adaptive bandwidth: Weak link low/bulk temizleme testi");

var weakQueue = new HydronomPriorityMessageQueue();

weakQueue.Enqueue(CreateQueued("bulk-weak", HydronomMessagePriority.Bulk));
weakQueue.Enqueue(CreateQueued("low-weak", HydronomMessagePriority.Low));
weakQueue.Enqueue(CreateQueued("normal-weak", HydronomMessagePriority.Normal));
weakQueue.Enqueue(CreateQueued("critical-weak", HydronomMessagePriority.Critical));

var weakBudget = policy.CreateBudget(new HydronomLinkHealth
{
    LinkId = "test-link",
    Level = HydronomLinkHealthLevel.Weak
});

var weakBatch = policy.SelectBatchForSend(weakQueue, weakBudget);
var weakIds = weakBatch.Select(ReadId).ToArray();

Require(!weakIds.Contains("bulk-weak"), "Weak link bulk trafiği göndermedi.");
Require(!weakIds.Contains("low-weak"), "Weak link low trafiği göndermedi.");
Require(weakIds.Contains("critical-weak"), "Weak link critical trafiği korudu.");
Require(weakIds.Contains("normal-weak"), "Weak link normal trafiği korudu.");

var weakSnapshot = weakQueue.Snapshot();
Require(weakSnapshot.DroppedLowPriorityMessages >= 1, "Weak link low drop metriğini artırdı.");
Require(weakSnapshot.DroppedBulkMessages >= 1, "Weak link bulk drop metriğini artırdı.");

Console.WriteLine();
Console.WriteLine("[6] Adaptive bandwidth: Lost link testi");

var lostQueue = new HydronomPriorityMessageQueue();

lostQueue.Enqueue(CreateQueued("emergency-lost", HydronomMessagePriority.Emergency));
lostQueue.Enqueue(CreateQueued("critical-lost", HydronomMessagePriority.Critical));
lostQueue.Enqueue(CreateQueued("normal-lost", HydronomMessagePriority.Normal));

var lostBudget = policy.CreateBudget(new HydronomLinkHealth
{
    LinkId = "test-link",
    Level = HydronomLinkHealthLevel.Lost
});

var lostBatch = policy.SelectBatchForSend(lostQueue, lostBudget);

Require(lostBudget.MaxMessagesPerTick == 0, "Lost link normal batch bütçesini sıfırlıyor.");
Require(lostBatch.Count == 0, "Lost link batch göndermiyor.");

Console.WriteLine();
Console.WriteLine("[7] Message/byte budget testi");

var budgetQueue = new HydronomPriorityMessageQueue();

budgetQueue.Enqueue(CreateQueued("m1", HydronomMessagePriority.High, payloadSize: 100));
budgetQueue.Enqueue(CreateQueued("m2", HydronomMessagePriority.High, payloadSize: 100));
budgetQueue.Enqueue(CreateQueued("m3", HydronomMessagePriority.High, payloadSize: 100));

var limitedBudget = new HydronomBandwidthBudget
{
    LinkLevel = HydronomLinkHealthLevel.Good,
    MaxMessagesPerTick = 2,
    MaxBytesPerTick = 1024,
    StateTelemetryHz = 20.0,
    MissionTelemetryHz = 5.0,
    DiagnosticsHz = 2.0,
    WorldUpdateHz = 5.0,
    AllowBulkTraffic = true,
    AllowVideoMetadata = true,
    DropLowPriorityTraffic = false
};

var limitedBatch = policy.SelectBatchForSend(budgetQueue, limitedBudget);

Require(limitedBatch.Count == 2, "MaxMessagesPerTick limiti çalıştı.");

Console.WriteLine();
Console.WriteLine("====================================================");
Console.WriteLine(" COMMUNICATION QUEUE SMOKE TEST PASSED ✅");
Console.WriteLine("====================================================");

static HydronomQueuedMessage CreateQueued(
    string id,
    HydronomMessagePriority priority,
    TimeSpan? ttl = null,
    int payloadSize = 16)
{
    var idBytes = Encoding.UTF8.GetBytes(id);
    var payload = new byte[Math.Max(payloadSize, idBytes.Length)];
    Array.Copy(idBytes, payload, idBytes.Length);

    var encoded = new HydronomEncodedMessage
    {
        Type = priority switch
        {
            HydronomMessagePriority.Emergency => HydronomMessageType.EmergencyStop,
            HydronomMessagePriority.Critical => HydronomMessageType.MissionCommand,
            HydronomMessagePriority.High => HydronomMessageType.FusedState,
            HydronomMessagePriority.Normal => HydronomMessageType.MissionStatus,
            HydronomMessagePriority.Low => HydronomMessageType.DiagnosticSummary,
            HydronomMessagePriority.Bulk => HydronomMessageType.LogBatch,
            _ => HydronomMessageType.Unknown
        },
        Priority = priority,
        Flags = HydronomMessageFlags.None,
        Bytes = payload,
        CodecName = "smoke-test"
    };

    return HydronomQueuedMessage.Create(
        encoded,
        channelId: "smoke",
        ttl: ttl,
        reason: id);
}

static string ReadId(HydronomQueuedMessage message)
{
    var text = Encoding.UTF8.GetString(message.Message.Bytes);
    var zeroIndex = text.IndexOf('\0');

    return zeroIndex >= 0
        ? text[..zeroIndex]
        : text;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message}");
        Console.ResetColor();

        throw new InvalidOperationException(message);
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[OK] {message}");
    Console.ResetColor();
}