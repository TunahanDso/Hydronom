namespace Hydronom.GroundStation.Commanding;

using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafÄ±nda gÃ¶nderilen komutlarÄ± ve araÃ§lardan dÃ¶nen sonuÃ§larÄ± takip eder.
/// 
/// CommandTracker'Ä±n amacÄ±:
/// - FleetCommand kayÄ±tlarÄ±nÄ± tutmak,
/// - FleetCommandResult geldiÄŸinde ilgili komut kaydÄ±nÄ± gÃ¼ncellemek,
/// - Pending / completed / failed komutlarÄ± ayÄ±rmak,
/// - Timeout olan komutlarÄ± iÅŸaretlemek,
/// - Hydronom Ops tarafÄ±na command history saÄŸlayabilmektir.
/// 
/// Bu sÄ±nÄ±f, Fleet & Ground Station mimarisinde operatÃ¶r kontrolÃ¼nÃ¼n izlenebilir olmasÄ± iÃ§in
/// temel bir yapÄ± taÅŸÄ±dÄ±r.
/// </summary>
public sealed class CommandTracker
{
    /// <summary>
    /// CommandId -> CommandRecord eÅŸlemesini tutar.
    /// </summary>
    private readonly Dictionary<string, CommandRecord> _records = new();

    /// <summary>
    /// Tracker eriÅŸimlerini thread-safe tutmak iÃ§in kullanÄ±lan lock objesi.
    /// 
    /// Ä°leride aynÄ± anda:
    /// - OperatorCommandCenter,
    /// - CommunicationRouter,
    /// - GroundMessageDispatcher,
    /// - Ops Gateway,
    /// - ReplayRecorder
    /// bu tracker'a eriÅŸebilir.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// KayÄ±tlÄ± toplam komut sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _records.Count;
            }
        }
    }

    /// <summary>
    /// Yeni bir FleetCommand kaydeder.
    /// 
    /// Komut geÃ§ersizse false dÃ¶ner.
    /// AynÄ± CommandId daha Ã¶nce varsa mevcut kayÄ±t ezilir.
    /// 
    /// Not:
    /// Ä°lk fazda overwrite davranÄ±ÅŸÄ± kabul edilebilir.
    /// Ä°leride aynÄ± CommandId tekrar gelirse reject/ignore politikasÄ± eklenebilir.
    /// </summary>
    public bool TrackCommand(FleetCommand command)
    {
        if (command is null || !command.IsValid)
            return false;

        var record = new CommandRecord
        {
            Command = command,
            CreatedUtc = DateTimeOffset.UtcNow,
            IsCompleted = !command.RequiresResult
        };

        lock (_sync)
        {
            _records[command.CommandId] = record;
        }

        return true;
    }

    /// <summary>
    /// Bir FleetCommandResult sonucunu ilgili komut kaydÄ±na uygular.
    /// 
    /// EÄŸer CommandId tracker iÃ§inde bulunursa kayÄ±t gÃ¼ncellenir ve true dÃ¶ner.
    /// Bulunamazsa false dÃ¶ner.
    /// 
    /// Bu davranÄ±ÅŸ bilinÃ§li:
    /// Ground Station bilmediÄŸi bir komut sonucunu takip etmemelidir.
    /// Ä°leride unknown result kayÄ±tlarÄ± ayrÄ± bir diagnostics/event log'a alÄ±nabilir.
    /// </summary>
    public bool ApplyResult(FleetCommandResult result)
    {
        if (result is null || !result.IsValid)
            return false;

        lock (_sync)
        {
            if (!_records.TryGetValue(result.CommandId, out var existing))
                return false;

            _records[result.CommandId] = existing.WithResult(result);
            return true;
        }
    }

    /// <summary>
    /// CommandId ile kayÄ±tlÄ± komutu bulmaya Ã§alÄ±ÅŸÄ±r.
    /// </summary>
    public bool TryGet(string commandId, out CommandRecord? record)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            record = null;
            return false;
        }

        lock (_sync)
        {
            return _records.TryGetValue(commandId, out record);
        }
    }

    /// <summary>
    /// TÃ¼m komut geÃ§miÅŸinin snapshot kopyasÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En yeni komutlar Ã¶nce gelecek ÅŸekilde sÄ±ralanÄ±r.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetSnapshot()
    {
        lock (_sync)
        {
            return _records.Values
                .OrderByDescending(x => x.CreatedUtc)
                .ToArray();
        }
    }

    /// <summary>
    /// HenÃ¼z sonuÃ§ bekleyen komutlarÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetPendingCommands()
    {
        lock (_sync)
        {
            return _records.Values
                .Where(x => x.IsPending)
                .OrderByDescending(x => x.CreatedUtc)
                .ToArray();
        }
    }

    /// <summary>
    /// TamamlanmÄ±ÅŸ komutlarÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// Completed olmak baÅŸarÄ±lÄ± olmak anlamÄ±na gelmez.
    /// SafetyBlocked veya Failed gibi kayÄ±tlar da completed kabul edilir.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetCompletedCommands()
    {
        lock (_sync)
        {
            return _records.Values
                .Where(x => x.IsCompleted)
                .OrderByDescending(x => x.CreatedUtc)
                .ToArray();
        }
    }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z sonuÃ§lanan komutlarÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// HenÃ¼z sonuÃ§ almamÄ±ÅŸ pending komutlar bu listeye dahil edilmez.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetFailedCommands()
    {
        lock (_sync)
        {
            return _records.Values
                .Where(x => x.HasResult && !x.IsSuccessful)
                .OrderByDescending(x => x.CreatedUtc)
                .ToArray();
        }
    }

    /// <summary>
    /// Belirtilen sÃ¼reden daha uzun sÃ¼redir cevap bekleyen komutlarÄ± expired olarak iÅŸaretler.
    /// 
    /// Ã–rnek:
    /// timeout = TimeSpan.FromSeconds(3)
    /// 
    /// EÄŸer bir MissionCommand 3 saniye boyunca sonuÃ§ dÃ¶nmezse Expired olur.
    /// </summary>
    public int MarkExpiredCommands(TimeSpan timeout, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var changed = 0;

        lock (_sync)
        {
            foreach (var pair in _records.ToArray())
            {
                var record = pair.Value;

                if (!record.IsPending)
                    continue;

                var age = now - record.CreatedUtc;

                if (age <= timeout)
                    continue;

                _records[pair.Key] = record.MarkExpired();
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// CommandId ile bir komut kaydÄ±nÄ± siler.
    /// 
    /// Test, cleanup veya sÄ±nÄ±rlÄ± command history tutma senaryolarÄ±nda kullanÄ±labilir.
    /// </summary>
    public bool Remove(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
            return false;

        lock (_sync)
        {
            return _records.Remove(commandId);
        }
    }

    /// <summary>
    /// TÃ¼m komut geÃ§miÅŸini temizler.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _records.Clear();
        }
    }
}
