namespace Hydronom.GroundStation.Ack;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.TransportExecution;

/// <summary>
/// FleetCommand gönderimleri ile araçtan gelen FleetCommandResult mesajlarını eşleştiren korelasyon motorudur.
/// 
/// Bu sınıfın amacı:
/// - Komut gönderildiğinde CommandId → RouteExecutionRecord bağlantısını kaydetmek,
/// - Araçtan FleetCommandResult geldiğinde aynı CommandId üzerinden execution kaydını bulmak,
/// - TransportExecutionTracker'a gerçek ACK / failure sonucunu işlemek,
/// - Hydronom Ops için gerçek command delivery trace snapshot'ı üretmektir.
/// 
/// Böylece sistem artık "SendAsync başarılı oldu, o zaman ACK geldi" varsayımından çıkar;
/// gerçekten araçtan dönen result ile ACK correlation yapar.
/// </summary>
public sealed class CommandAckCorrelator
{
    private readonly Dictionary<string, CommandAckCorrelationRecord> _recordsByCommandId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CommandAckCorrelationRecord> _recordsByExecutionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    /// <summary>
    /// Kayıtlı korelasyon sayısı.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
                return _recordsByCommandId.Count;
        }
    }

    /// <summary>
    /// Gönderilen bir command ile route execution kaydını ilişkilendirir.
    /// </summary>
    public CommandAckCorrelationRecord? Track(
        FleetCommand command,
        HydronomEnvelope envelope,
        RouteExecutionRecord execution,
        TransportKind? transportKind = null,
        DateTimeOffset? nowUtc = null)
    {
        if (command is null || !command.IsValid)
            return null;

        if (envelope is null)
            return null;

        if (execution is null)
            return null;

        var selectedTransport =
            transportKind ??
            execution.SendResults.FirstOrDefault(x => x.Success || x.HasAck)?.TransportKind ??
            execution.CandidateTransports.FirstOrDefault();

        var record = new CommandAckCorrelationRecord
        {
            CommandId = command.CommandId,
            MessageId = envelope.MessageId,
            ExecutionId = execution.ExecutionId,
            SourceNodeId = command.SourceNodeId,
            TargetNodeId = command.TargetNodeId,
            TransportKind = selectedTransport,
            CreatedUtc = nowUtc ?? DateTimeOffset.UtcNow
        };

        lock (_sync)
        {
            _recordsByCommandId[record.CommandId] = record;
            _recordsByExecutionId[record.ExecutionId] = record;
        }

        return record;
    }

    /// <summary>
    /// FleetCommandResult ile daha önce track edilmiş command execution kaydını eşleştirir.
    /// </summary>
    public CommandAckCorrelationRecord? ApplyResult(
        FleetCommandResult result,
        DateTimeOffset? nowUtc = null)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.CommandId))
            return null;

        CommandAckCorrelationRecord? record;

        lock (_sync)
        {
            _recordsByCommandId.TryGetValue(result.CommandId, out record);
        }

        if (record is null)
            return null;

        record.ApplyResult(
            result.Status,
            result.ProcessingStage,
            result.Message,
            nowUtc ?? DateTimeOffset.UtcNow);

        return record;
    }

    /// <summary>
    /// CommandId ile korelasyon kaydı döndürür.
    /// </summary>
    public CommandAckCorrelationRecord? GetByCommandId(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
            return null;

        lock (_sync)
        {
            return _recordsByCommandId.TryGetValue(commandId, out var record)
                ? record
                : null;
        }
    }

    /// <summary>
    /// ExecutionId ile korelasyon kaydı döndürür.
    /// </summary>
    public CommandAckCorrelationRecord? GetByExecutionId(string executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId))
            return null;

        lock (_sync)
        {
            return _recordsByExecutionId.TryGetValue(executionId, out var record)
                ? record
                : null;
        }
    }

    /// <summary>
    /// ACK alınmış korelasyon kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetAckedSnapshot()
    {
        return GetSnapshot()
            .Where(x => x.IsAcked)
            .ToArray();
    }

    /// <summary>
    /// Pending korelasyon kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetPendingSnapshot()
    {
        return GetSnapshot()
            .Where(x => !x.IsAcked)
            .ToArray();
    }

    /// <summary>
    /// Başarısız korelasyon kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetFailedSnapshot()
    {
        return GetSnapshot()
            .Where(x => x.IsFailed)
            .ToArray();
    }

    /// <summary>
    /// Tüm korelasyon kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetSnapshot()
    {
        CommandAckCorrelationRecord[] records;

        lock (_sync)
        {
            records = _recordsByCommandId.Values.ToArray();
        }

        return records
            .OrderByDescending(x => x.LastResultUtc ?? x.CreatedUtc)
            .Select(ToSnapshot)
            .ToArray();
    }

    /// <summary>
    /// Süresi dolmuş pending korelasyon kayıtlarını failed/timeout gibi işaretlemek için kullanılabilir.
    /// 
    /// İlk fazda sadece snapshot tarafında sayım amaçlıdır; command tracker timeout akışını bozmaz.
    /// </summary>
    public int CountExpiredPending(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;

        lock (_sync)
        {
            return _recordsByCommandId.Values.Count(x =>
                !x.IsAcked &&
                now - x.CreatedUtc >= timeout);
        }
    }

    /// <summary>
    /// Kayıt modelini snapshot modeline dönüştürür.
    /// </summary>
    private static CommandAckCorrelationSnapshot ToSnapshot(CommandAckCorrelationRecord record)
    {
        return new CommandAckCorrelationSnapshot
        {
            CorrelationId = record.CorrelationId,
            CommandId = record.CommandId,
            MessageId = record.MessageId,
            ExecutionId = record.ExecutionId,
            SourceNodeId = record.SourceNodeId,
            TargetNodeId = record.TargetNodeId,
            TransportKind = record.TransportKind,
            CreatedUtc = record.CreatedUtc,
            AckReceivedUtc = record.AckReceivedUtc,
            LastResultUtc = record.LastResultUtc,
            LastStatus = record.LastStatus,
            LastProcessingStage = record.LastProcessingStage,
            LastMessage = record.LastMessage,
            IsAcked = record.IsAcked,
            IsCompleted = record.IsCompleted,
            IsSuccessful = record.IsSuccessful,
            IsFailed = record.IsFailed,
            AckLatencyMs = record.AckLatencyMs,
            LastResultLatencyMs = record.LastResultLatencyMs
        };
    }
}