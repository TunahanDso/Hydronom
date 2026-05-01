namespace Hydronom.GroundStation.Commanding;

using Hydronom.Core.Fleet;

/// <summary>
/// Ground Station tarafında gönderilen komutları ve araçlardan dönen sonuçları takip eder.
/// 
/// CommandTracker'ın amacı:
/// - FleetCommand kayıtlarını tutmak,
/// - FleetCommandResult geldiğinde ilgili komut kaydını güncellemek,
/// - Pending / completed / failed komutları ayırmak,
/// - Timeout olan komutları işaretlemek,
/// - Hydronom Ops tarafına command history sağlayabilmektir.
/// 
/// Bu sınıf, Fleet & Ground Station mimarisinde operatör kontrolünün izlenebilir olması için
/// temel bir yapı taşıdır.
/// </summary>
public sealed class CommandTracker
{
    /// <summary>
    /// CommandId -> CommandRecord eşlemesini tutar.
    /// </summary>
    private readonly Dictionary<string, CommandRecord> _records = new();

    /// <summary>
    /// Tracker erişimlerini thread-safe tutmak için kullanılan lock objesi.
    /// 
    /// İleride aynı anda:
    /// - OperatorCommandCenter,
    /// - CommunicationRouter,
    /// - GroundMessageDispatcher,
    /// - Ops Gateway,
    /// - ReplayRecorder
    /// bu tracker'a erişebilir.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// Kayıtlı toplam komut sayısını döndürür.
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
    /// Komut geçersizse false döner.
    /// Aynı CommandId daha önce varsa mevcut kayıt ezilir.
    /// 
    /// Not:
    /// İlk fazda overwrite davranışı kabul edilebilir.
    /// İleride aynı CommandId tekrar gelirse reject/ignore politikası eklenebilir.
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
    /// Bir FleetCommandResult sonucunu ilgili komut kaydına uygular.
    /// 
    /// Eğer CommandId tracker içinde bulunursa kayıt güncellenir ve true döner.
    /// Bulunamazsa false döner.
    /// 
    /// Bu davranış bilinçli:
    /// Ground Station bilmediği bir komut sonucunu takip etmemelidir.
    /// İleride unknown result kayıtları ayrı bir diagnostics/event log'a alınabilir.
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
    /// CommandId ile kayıtlı komutu bulmaya çalışır.
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
    /// Tüm komut geçmişinin snapshot kopyasını döndürür.
    /// 
    /// En yeni komutlar önce gelecek şekilde sıralanır.
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
    /// Henüz sonuç bekleyen komutların snapshot listesini döndürür.
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
    /// Tamamlanmış komutların snapshot listesini döndürür.
    /// 
    /// Completed olmak başarılı olmak anlamına gelmez.
    /// SafetyBlocked veya Failed gibi kayıtlar da completed kabul edilir.
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
    /// Başarısız sonuçlanan komutların snapshot listesini döndürür.
    /// 
    /// Henüz sonuç almamış pending komutlar bu listeye dahil edilmez.
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
    /// Belirtilen süreden daha uzun süredir cevap bekleyen komutları expired olarak işaretler.
    /// 
    /// Örnek:
    /// timeout = TimeSpan.FromSeconds(3)
    /// 
    /// Eğer bir MissionCommand 3 saniye boyunca sonuç dönmezse Expired olur.
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
    /// CommandId ile bir komut kaydını siler.
    /// 
    /// Test, cleanup veya sınırlı command history tutma senaryolarında kullanılabilir.
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
    /// Tüm komut geçmişini temizler.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _records.Clear();
        }
    }
}