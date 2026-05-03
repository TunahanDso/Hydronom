namespace Hydronom.GroundStation.Ack;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.TransportExecution;

/// <summary>
/// FleetCommand gÃ¶nderimleri ile araÃ§tan gelen FleetCommandResult mesajlarÄ±nÄ± eÅŸleÅŸtiren korelasyon motorudur.
/// 
/// Bu sÄ±nÄ±fÄ±n amacÄ±:
/// - Komut gÃ¶nderildiÄŸinde CommandId â†’ RouteExecutionRecord baÄŸlantÄ±sÄ±nÄ± kaydetmek,
/// - AraÃ§tan FleetCommandResult geldiÄŸinde aynÄ± CommandId Ã¼zerinden execution kaydÄ±nÄ± bulmak,
/// - TransportExecutionTracker'a gerÃ§ek ACK / failure sonucunu iÅŸlemek,
/// - Hydronom Ops iÃ§in gerÃ§ek command delivery trace snapshot'Ä± Ã¼retmektir.
/// 
/// BÃ¶ylece sistem artÄ±k "SendAsync baÅŸarÄ±lÄ± oldu, o zaman ACK geldi" varsayÄ±mÄ±ndan Ã§Ä±kar;
/// gerÃ§ekten araÃ§tan dÃ¶nen result ile ACK correlation yapar.
/// </summary>
public sealed class CommandAckCorrelator
{
    private readonly Dictionary<string, CommandAckCorrelationRecord> _recordsByCommandId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CommandAckCorrelationRecord> _recordsByExecutionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    /// <summary>
    /// KayÄ±tlÄ± korelasyon sayÄ±sÄ±.
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
    /// GÃ¶nderilen bir command ile route execution kaydÄ±nÄ± iliÅŸkilendirir.
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
    /// FleetCommandResult ile daha Ã¶nce track edilmiÅŸ command execution kaydÄ±nÄ± eÅŸleÅŸtirir.
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
    /// CommandId ile korelasyon kaydÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// ExecutionId ile korelasyon kaydÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// ACK alÄ±nmÄ±ÅŸ korelasyon kayÄ±tlarÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetAckedSnapshot()
    {
        return GetSnapshot()
            .Where(x => x.IsAcked)
            .ToArray();
    }

    /// <summary>
    /// Pending korelasyon kayÄ±tlarÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetPendingSnapshot()
    {
        return GetSnapshot()
            .Where(x => !x.IsAcked)
            .ToArray();
    }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z korelasyon kayÄ±tlarÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandAckCorrelationSnapshot> GetFailedSnapshot()
    {
        return GetSnapshot()
            .Where(x => x.IsFailed)
            .ToArray();
    }

    /// <summary>
    /// TÃ¼m korelasyon kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
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
    /// SÃ¼resi dolmuÅŸ pending korelasyon kayÄ±tlarÄ±nÄ± failed/timeout gibi iÅŸaretlemek iÃ§in kullanÄ±labilir.
    /// 
    /// Ä°lk fazda sadece snapshot tarafÄ±nda sayÄ±m amaÃ§lÄ±dÄ±r; command tracker timeout akÄ±ÅŸÄ±nÄ± bozmaz.
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
    /// KayÄ±t modelini snapshot modeline dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r.
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
