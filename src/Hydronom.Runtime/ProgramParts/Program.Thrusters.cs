using System;
using System.IO;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Actuators;
using Microsoft.Extensions.Configuration;

// ThrusterDesc adını Runtime.Actuators içindeki tipe sabitle
using ThrusterDesc = Hydronom.Runtime.Actuators.ThrusterDesc;

partial class Program
{
    /// <summary>
    /// Thruster konfigürasyonunu farklı kaynaklardan sırayla yükler.
    ///
    /// Öncelik:
    /// 1. Thrusters section
    /// 2. Configs/actuators.discovered.json
    /// 3. Configs/thrusters.geometry.json
    /// 4. Actuator:Thrusters legacy section
    /// 5. AutoDiscovery çıktısı placeholder
    /// </summary>
    private static ThrusterDesc[] LoadThrusterDescriptions(IConfiguration config)
    {
        ThrusterDesc[]? thrusterDescs = null;

        try
        {
            thrusterDescs = config.GetSection("Thrusters").Get<ThrusterDesc[]>();

            if (thrusterDescs is not null && thrusterDescs.Length > 0)
            {
                Console.WriteLine($"[CFG] Thrusters loaded from 'Thrusters' section ({thrusterDescs.Length} ch).");
                return SanitizeThrusterDescriptions(thrusterDescs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to read 'Thrusters' section: {ex.Message}");
        }

        var fromFiles = TryLoadThrustersFromChannelProfiles(config);
        if (fromFiles.Length > 0)
            return SanitizeThrusterDescriptions(fromFiles);

        try
        {
            thrusterDescs = config.GetSection("Actuator:Thrusters").Get<ThrusterDesc[]>();

            if (thrusterDescs is not null && thrusterDescs.Length > 0)
            {
                Console.WriteLine($"[CFG] Thrusters loaded from legacy 'Actuator:Thrusters' ({thrusterDescs.Length} ch).");
                return SanitizeThrusterDescriptions(thrusterDescs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to read legacy 'Actuator:Thrusters': {ex.Message}");
        }

        return Array.Empty<ThrusterDesc>();
    }

    /// <summary>
    /// Thruster config loader.
    /// Configs altındaki keşif/geometri dosyalarını dener.
    /// </summary>
    private static ThrusterDesc[] TryLoadThrustersFromChannelProfiles(IConfiguration config)
    {
        var baseDir = AppContext.BaseDirectory;
        var configsDir = Path.Combine(baseDir, "Configs");

        try
        {
            var discoveredPath = Path.Combine(configsDir, "actuators.discovered.json");

            if (File.Exists(discoveredPath))
            {
                var json = File.ReadAllText(discoveredPath);
                var arr = System.Text.Json.JsonSerializer.Deserialize<ThrusterDesc[]>(json);

                if (arr is not null && arr.Length > 0)
                {
                    Console.WriteLine($"[CFG] Thrusters loaded from Configs/actuators.discovered.json ({arr.Length} ch).");
                    return arr;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to load Configs/actuators.discovered.json: {ex.Message}");
        }

        try
        {
            var geomPath = Path.Combine(configsDir, "thrusters.geometry.json");

            if (File.Exists(geomPath))
            {
                var json = File.ReadAllText(geomPath);
                var arr = System.Text.Json.JsonSerializer.Deserialize<ThrusterDesc[]>(json);

                if (arr is not null && arr.Length > 0)
                {
                    Console.WriteLine($"[CFG] Thrusters loaded from Configs/thrusters.geometry.json ({arr.Length} ch).");
                    return arr;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to load Configs/thrusters.geometry.json: {ex.Message}");
        }

        try
        {
            var outDir = config["AutoDiscovery:OutputDirectory"] ?? "Configs/AutoDiscovery";
            var file = config["AutoDiscovery:ChannelProfileFile"] ?? "channel_profiles.json";
            var fullPath = Path.Combine(baseDir, outDir, file);

            if (!File.Exists(fullPath))
                return Array.Empty<ThrusterDesc>();

            // Bu alan bilinçli olarak placeholder bırakıldı.
            // AutoDiscovery ChannelProfileSet tipi runtime tarafında doğrudan tanımlı değilse
            // geometri dönüşümü ayrı bir adapter ile yapılmalı.
            Console.WriteLine("[CFG] Skipping AutoDiscovery: ChannelProfileSet type not defined here.");
            return Array.Empty<ThrusterDesc>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CFG] Failed to load AutoDiscovery ChannelProfileSet: {ex.Message}");
            return Array.Empty<ThrusterDesc>();
        }
    }

    /// <summary>
    /// Config kaynaklarından gelen thruster tanımlarını güvenli hale getirir.
    /// Bozuk channel/id/force direction gibi değerlerde mümkün olduğunca güvenli fallback uygular.
    /// </summary>
    private static ThrusterDesc[] SanitizeThrusterDescriptions(ThrusterDesc[] input)
    {
        return input
            .Where(t => t.Channel >= 0)
            .GroupBy(t => t.Channel)
            .Select(g => g.First())
            .OrderBy(t => t.Channel)
            .Select((t, index) =>
            {
                var id = string.IsNullOrWhiteSpace(t.Id)
                    ? $"CH{t.Channel}"
                    : t.Id.Trim();

                var dir = t.ForceDir;
                if (!IsFinite(dir) || dir.Length < 1e-9)
                    dir = new Vec3(1, 0, 0);

                var pos = t.Position;
                if (!IsFinite(pos))
                    pos = Vec3.Zero;

                return new ThrusterDesc(
                    Id: id,
                    Channel: t.Channel,
                    Position: pos,
                    ForceDir: dir,
                    Reversed: t.Reversed
                );
            })
            .ToArray();
    }

    /// <summary>
    /// Motors → Thrusters geri uyum dönüştürücüsü.
    /// Eski motor config yapısını basit ileri-itki thruster geometrisine çevirir.
    /// </summary>
    private static ThrusterDesc[] MapMotorsToThrusters(MotorDesc[] motors)
    {
        double halfX = 0.5;
        double halfY = 0.5;

        Vec3 PosForId(string id, int index, int count)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                var u = id.Trim().ToUpperInvariant();

                if (u == "FL") return new Vec3(-halfX, +halfY, 0);
                if (u == "FR") return new Vec3(+halfX, +halfY, 0);
                if (u == "RL") return new Vec3(-halfX, -halfY, 0);
                if (u == "RR") return new Vec3(+halfX, -halfY, 0);
            }

            bool top = index < (count + 1) / 2;
            double y = top ? +halfY : -halfY;
            int col = top ? index : index - (count + 1) / 2;
            double x = (col % 2 == 0) ? -halfX : +halfX;

            return new Vec3(x, y, 0);
        }

        return motors
            .Select((m, i) =>
            {
                var pos = PosForId(m.Id ?? string.Empty, i, motors.Length);
                var dir = new Vec3(1, 0, 0);

                return new ThrusterDesc(
                    Id: m.Id ?? $"CH{m.Channel}",
                    Channel: m.Channel,
                    Position: pos,
                    ForceDir: dir,
                    Reversed: false
                );
            })
            .ToArray();
    }

    private static bool IsFinite(Vec3 v)
    {
        return
            double.IsFinite(v.X) &&
            double.IsFinite(v.Y) &&
            double.IsFinite(v.Z);
    }
}
