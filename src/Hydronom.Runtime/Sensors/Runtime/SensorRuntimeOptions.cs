using Hydronom.Core.Sensors.Gps.Models;
using Hydronom.Core.Sensors.Imu.Models;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors;

namespace Hydronom.Runtime.Sensors.Runtime;

/// <summary>
/// Hydronom sensÃ¶r runtime ayarlarÄ±.
/// 
/// Bu sÄ±nÄ±f, sensÃ¶r tarafÄ±nda ana Ã§alÄ±ÅŸma modunu belirler.
/// Bundan sonra varsayÄ±lan hedef CSharpPrimary modudur.
/// Python tarafÄ± yalnÄ±zca legacy backup / fallback olarak korunur.
/// </summary>
public sealed class SensorRuntimeOptions
{
    /// <summary>
    /// Ana sensÃ¶r runtime modu.
    /// 
    /// CSharpPrimary:
    /// Normal mod. SensÃ¶r, fÃ¼zyon ve state estimation C# tarafÄ±nda Ã§alÄ±ÅŸÄ±r.
    /// 
    /// PythonBackup:
    /// Eski Python pipeline yedek sistem olarak kullanÄ±lÄ±r.
    /// 
    /// CompareOnly:
    /// C# ve Python Ã§Ä±ktÄ±larÄ± karÅŸÄ±laÅŸtÄ±rÄ±lÄ±r; Python authority sahibi olmaz.
    /// 
    /// Disabled:
    /// SensÃ¶r runtime devre dÄ±ÅŸÄ±dÄ±r.
    /// </summary>
    public SensorRuntimeMode Mode { get; set; } = SensorRuntimeMode.CSharpPrimary;

    /// <summary>
    /// Python backup sisteminin kullanÄ±lmasÄ±na izin verilip verilmediÄŸi.
    /// Bu deÄŸer true olsa bile Python normal modda ana state'i yÃ¶netemez.
    /// </summary>
    public bool PythonBackupEnabled { get; set; } = true;

    /// <summary>
    /// Normal modda Python'Ä±n authoritative state kaynaÄŸÄ± olmasÄ±na izin verilmez.
    /// Bu deÄŸer gÃ¼venlik iÃ§in varsayÄ±lan olarak false kalmalÄ±dÄ±r.
    /// </summary>
    public bool AllowPythonAuthority { get; set; } = false;

    /// <summary>
    /// CompareOnly modunda Python Ã§Ä±ktÄ±larÄ± C# Ã§Ä±ktÄ±larÄ± ile karÅŸÄ±laÅŸtÄ±rÄ±lÄ±r.
    /// Bu mod test/debug amaÃ§lÄ±dÄ±r.
    /// </summary>
    public bool CompareWithPython { get; set; } = false;

    /// <summary>
    /// SensÃ¶rlerden okuma yapÄ±lÄ±rken hedef dÃ¶ngÃ¼ frekansÄ±.
    /// Bu deÄŸer tÃ¼m sensÃ¶rlerin gerÃ§ek hÄ±zÄ± deÄŸildir; runtime host'un ana poll hedefidir.
    /// </summary>
    public double RuntimeRateHz { get; set; } = 20.0;

    /// <summary>
    /// SensÃ¶r Ã¶rneÄŸi bu sÃ¼reden eskiyse stale kabul edilir.
    /// </summary>
    public double StaleSampleMs { get; set; } = 750.0;

    /// <summary>
    /// ArdÄ±ÅŸÄ±k hata sayÄ±sÄ± bu eÅŸiÄŸi geÃ§erse sensÃ¶r failing/critical durumuna yaklaÅŸÄ±r.
    /// </summary>
    public int ConsecutiveFailureWarningThreshold { get; set; } = 3;

    /// <summary>
    /// ArdÄ±ÅŸÄ±k hata sayÄ±sÄ± bu eÅŸiÄŸi geÃ§erse sensÃ¶r critical kabul edilebilir.
    /// </summary>
    public int ConsecutiveFailureCriticalThreshold { get; set; } = 8;

    /// <summary>
    /// CSharpPrimary modunda sim sensÃ¶rlerin otomatik oluÅŸturulmasÄ±na izin verir.
    /// Ä°lk geÃ§iÅŸ aÅŸamasÄ±nda IMU/GPS sim sensÃ¶rleri iÃ§in kullanacaÄŸÄ±z.
    /// </summary>
    public bool EnableDefaultSimSensors { get; set; } = true;

    /// <summary>
    /// Runtime iÃ§inde IMU sensÃ¶rÃ¼nÃ¼ etkinleÅŸtirir.
    /// </summary>
    public bool EnableImu { get; set; } = true;

    /// <summary>
    /// Runtime iÃ§inde GPS sensÃ¶rÃ¼nÃ¼ etkinleÅŸtirir.
    /// </summary>
    public bool EnableGps { get; set; } = true;

    /// <summary>
    /// Runtime iÃ§inde LiDAR sensÃ¶rÃ¼nÃ¼ etkinleÅŸtirir.
    /// Ä°lk paketlerde false kalabilir.
    /// </summary>
    public bool EnableLidar { get; set; } = false;

    /// <summary>
    /// Runtime iÃ§inde kamera sensÃ¶rÃ¼nÃ¼ etkinleÅŸtirir.
    /// Kamera C# tarafÄ±na ileriki paketlerde taÅŸÄ±nacak.
    /// </summary>
    public bool EnableCamera { get; set; } = false;

    /// <summary>
    /// VarsayÄ±lan ayar nesnesi Ã¼retir.
    /// </summary>
    public static SensorRuntimeOptions Default()
    {
        return new SensorRuntimeOptions();
    }
}

