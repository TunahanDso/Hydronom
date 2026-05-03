using System;
using System.IO;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Actuators;
using Microsoft.Extensions.Configuration;

// ThrusterDesc adÄ±nÄ± Runtime.Actuators iÃ§indeki tipe sabitle
using ThrusterDesc = Hydronom.Runtime.Actuators.ThrusterDesc;

partial class Program
{
    /// <summary>
    /// Thruster konfigÃ¼rasyonunu farklÄ± kaynaklardan sÄ±rayla yÃ¼kler.
    ///
    /// Ã–ncelik:
    /// 1. Thrusters section
    /// 2. Configs/actuators.discovered.json
    /// 3. Configs/thrusters.geometry.json
    /// 4. Actuator:Thrusters legacy section
    /// 5. AutoDiscovery Ã§Ä±ktÄ±sÄ± placeholder
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
    /// Configs altÄ±ndaki keÅŸif/geometri dosyalarÄ±nÄ± dener.
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
                var arr = DeserializeThrusterConfig(json);

                if (arr.Length > 0)
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
                var arr = DeserializeThrusterConfig(json);

                if (arr.Length > 0)
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

            // Bu alan bilinÃ§li olarak placeholder bÄ±rakÄ±ldÄ±.
            // AutoDiscovery ChannelProfileSet tipi runtime tarafÄ±nda doÄŸrudan tanÄ±mlÄ± deÄŸilse
            // geometri dÃ¶nÃ¼ÅŸÃ¼mÃ¼ ayrÄ± bir adapter ile yapÄ±lmalÄ±.
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
    /// Hem dÃ¼z ThrusterDesc[] JSON formatÄ±nÄ± hem de
    /// { SchemaVersion, FrameId, GeometryPolicy, Thrusters: [...] } formatÄ±nÄ± destekler.
    /// </summary>
    private static ThrusterDesc[] DeserializeThrusterConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ThrusterDesc>();

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var geometry = System.Text.Json.JsonSerializer.Deserialize<ThrusterGeometryConfig>(json, options);

            if (geometry?.Thrusters is not null && geometry.Thrusters.Length > 0)
                return geometry.Thrusters;
        }
        catch
        {
            // Root-object format deÄŸilse aÅŸaÄŸÄ±da array formatÄ±nÄ± deneyeceÄŸiz.
        }

        try
        {
            var arr = System.Text.Json.JsonSerializer.Deserialize<ThrusterDesc[]>(json, options);
            return arr ?? Array.Empty<ThrusterDesc>();
        }
        catch
        {
            return Array.Empty<ThrusterDesc>();
        }
    }

    /// <summary>
    /// thrusters.geometry.json root modeli.
    /// Burada SchemaVersion, FrameId ve GeometryPolicy ÅŸu an runtime iÃ§in yalnÄ±zca taÅŸÄ±yÄ±cÄ± metadÄ±r.
    /// AsÄ±l actuator modeli Thrusters dizisinden oluÅŸturulur.
    /// </summary>
    private sealed class ThrusterGeometryConfig
    {
        public string? SchemaVersion { get; set; }
        public string? FrameId { get; set; }
        public object? GeometryPolicy { get; set; }
        public ThrusterDesc[]? Thrusters { get; set; }
    }

    /// <summary>
    /// Config kaynaklarÄ±ndan gelen thruster tanÄ±mlarÄ±nÄ± gÃ¼venli hale getirir.
    /// Bozuk channel/id/force direction gibi deÄŸerlerde mÃ¼mkÃ¼n olduÄŸunca gÃ¼venli fallback uygular.
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
                    Reversed: t.Reversed,
                    CanReverse: t.CanReverse
                );
            })
            .ToArray();
    }

    /// <summary>
    /// Motors â†’ Thrusters geri uyum dÃ¶nÃ¼ÅŸtÃ¼rÃ¼cÃ¼sÃ¼.
    /// Eski motor config yapÄ±sÄ±nÄ± basit ileri-itki thruster geometrisine Ã§evirir.
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
                    Reversed: false,
                    CanReverse: false
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
