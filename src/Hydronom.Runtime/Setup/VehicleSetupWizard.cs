using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hydronom.Core.Domain;
using Hydronom.Runtime.Actuators;

// Bu dosyada ThrusterDesc adÄ± Hydronom.Runtime.Actuators tipine iÅŸaret etsin:
using ThrusterDesc = Hydronom.Runtime.Actuators.ThrusterDesc;

namespace Hydronom.Runtime.Setup
{
    /// <summary>
    /// AraÃ§ kurulum sihirbazÄ±.
    ///
    /// GÃ¶revleri:
    /// - DÃ¼z ThrusterDesc[] JSON formatÄ±nÄ± okuyabilir.
    /// - Yeni root-object thrusters.geometry.json formatÄ±nÄ± okuyabilir:
    ///   { SchemaVersion, FrameId, GeometryPolicy, Thrusters: [...] }
    /// - Eski Motors tanÄ±mlarÄ±ndan gÃ¼venli varsayÄ±lan thruster geometrisi Ã¼retebilir.
    /// - Tek yÃ¶nlÃ¼ / Ã§ift yÃ¶nlÃ¼ ESC kabiliyetini CanReverse Ã¼zerinden taÅŸÄ±r.
    /// - Reversed ve CanReverse deÄŸerlerini kaybetmeden dosyaya yazabilir.
    ///
    /// Not:
    /// Reversed = motor yÃ¶n kalibrasyonu.
    /// CanReverse = motor/ESC fiziksel olarak negatif komutu destekliyor mu?
    /// </summary>
    public static class VehicleSetupWizard
    {
        private const string DefaultSchemaVersion = "1.1.0";
        private const string DefaultFrameId = "base_link";

        /// <summary>
        /// JSON metninden Thruster tanÄ±mlarÄ±nÄ± yÃ¼kler; hatada null dÃ¶ner.
        ///
        /// Desteklenen formatlar:
        /// 1) ThrusterDesc[]
        /// 2) { SchemaVersion, FrameId, GeometryPolicy, Thrusters: [...] }
        /// </summary>
        public static ThrusterDesc[]? TryLoadThrustersFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var options = CreateJsonOptions();

            try
            {
                var root = JsonSerializer.Deserialize<ThrusterGeometryDocument>(json, options);

                if (root?.Thrusters is not null && root.Thrusters.Length > 0)
                    return SanitizeThrusters(root.Thrusters);
            }
            catch
            {
                // Root-object format deÄŸilse array formatÄ±nÄ± deneyeceÄŸiz.
            }

            try
            {
                var arr = JsonSerializer.Deserialize<ThrusterDesc[]>(json, options);

                if (arr is not null && arr.Length > 0)
                    return SanitizeThrusters(arr);

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Dosyadan Thruster tanÄ±mlarÄ±nÄ± yÃ¼kler; yoksa/null dÃ¶ner.
        /// </summary>
        public static ThrusterDesc[]? TryLoadThrustersFromFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                return TryLoadThrustersFromJson(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Eski MotorDesc listesinden varsayÄ±lan bir Thruster listesi Ã¼retir.
        ///
        /// VarsayÄ±lan gÃ¼venli davranÄ±ÅŸ:
        /// - Reversed=false
        /// - CanReverse=false
        ///
        /// EÄŸer Ã§ift yÃ¶nlÃ¼ ESC kullanÄ±lacaksa defaultCanReverse=true gÃ¶nderilebilir.
        /// </summary>
        public static ThrusterDesc[] MapMotorsToThrusters(
            IReadOnlyList<MotorDesc> motors,
            bool defaultCanReverse = false,
            bool defaultReversed = false)
        {
            if (motors is null || motors.Count == 0)
                return Array.Empty<ThrusterDesc>();

            // Basit gÃ¶vde yarÄ± boyutlarÄ± (m): X = ileri/geri, Y = iskele/sancak
            const double halfX = 0.5;
            const double halfY = 0.5;

            Vec3 PosForId(string? id, int index, int count)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var u = id.Trim().ToUpperInvariant();

                    // Hydronom yÃ¼zey aracÄ± dÃ¼zenine yakÄ±n yorum:
                    // FL / FR Ã¶nde, RL / RR arkada.
                    if (u is "FL" or "H_FL")
                        return new Vec3(+halfX, +halfY, 0);

                    if (u is "FR" or "H_FR")
                        return new Vec3(+halfX, -halfY, 0);

                    if (u is "RL" or "H_RL")
                        return new Vec3(-halfX, +halfY, 0);

                    if (u is "RR" or "H_RR")
                        return new Vec3(-halfX, -halfY, 0);
                }

                // Bilinmiyorsa: kanal sÄ±rasÄ±na gÃ¶re dikdÃ¶rtgen daÄŸÄ±tÄ±m.
                bool front = index < (count + 1) / 2;
                double x = front ? +halfX : -halfX;

                int local = front ? index : index - (count + 1) / 2;
                double y = local % 2 == 0 ? +halfY : -halfY;

                return new Vec3(x, y, 0);
            }

            Vec3 DirForId(string? id)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var u = id.Trim().ToUpperInvariant();

                    // Senin mevcut thrusters.geometry.json dÃ¼zenin:
                    // H_FL: +Y
                    // H_FR: -Y
                    // H_RL/H_RR: +X
                    if (u is "FL" or "H_FL")
                        return new Vec3(0, +1, 0);

                    if (u is "FR" or "H_FR")
                        return new Vec3(0, -1, 0);

                    if (u is "RL" or "H_RL" or "RR" or "H_RR")
                        return new Vec3(+1, 0, 0);
                }

                return new Vec3(+1, 0, 0);
            }

            var list = new List<ThrusterDesc>(motors.Count);

            for (int i = 0; i < motors.Count; i++)
            {
                var m = motors[i];

                var id = string.IsNullOrWhiteSpace(m.Id)
                    ? $"CH{m.Channel}"
                    : m.Id.Trim();

                var pos = PosForId(id, i, motors.Count);
                var dir = DirForId(id);

                list.Add(new ThrusterDesc(
                    Id: id,
                    Channel: m.Channel,
                    Position: pos,
                    ForceDir: dir,
                    Reversed: defaultReversed,
                    CanReverse: defaultCanReverse
                ));
            }

            return SanitizeThrusters(list);
        }

        /// <summary>
        /// Thruster listesini dÃ¼z array JSON olarak diske kaydeder.
        /// Geriye dÃ¶nÃ¼k uyumluluk iÃ§in korunmuÅŸtur.
        /// </summary>
        public static void SaveThrustersToFile(
            string path,
            IReadOnlyList<ThrusterDesc> thrusters,
            bool indented = true)
        {
            SaveThrustersArrayToFile(path, thrusters, indented);
        }

        /// <summary>
        /// Thruster listesini dÃ¼z ThrusterDesc[] JSON olarak kaydeder.
        /// </summary>
        public static void SaveThrustersArrayToFile(
            string path,
            IReadOnlyList<ThrusterDesc> thrusters,
            bool indented = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path boÅŸ olamaz.", nameof(path));

            var clean = SanitizeThrusters(thrusters);
            EnsureDirectory(path);

            var json = JsonSerializer.Serialize(
                clean,
                new JsonSerializerOptions
                {
                    WriteIndented = indented
                });

            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Thruster listesini yeni thrusters.geometry.json root formatÄ±nda kaydeder.
        /// Ã–nerilen format budur.
        /// </summary>
        public static void SaveThrusterGeometryToFile(
            string path,
            IReadOnlyList<ThrusterDesc> thrusters,
            bool indented = true,
            string schemaVersion = DefaultSchemaVersion,
            string frameId = DefaultFrameId)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path boÅŸ olamaz.", nameof(path));

            var clean = SanitizeThrusters(thrusters);
            EnsureDirectory(path);

            var document = new ThrusterGeometryDocument
            {
                SchemaVersion = string.IsNullOrWhiteSpace(schemaVersion)
                    ? DefaultSchemaVersion
                    : schemaVersion,

                FrameId = string.IsNullOrWhiteSpace(frameId)
                    ? DefaultFrameId
                    : frameId,

                GeometryPolicy = ThrusterGeometryPolicy.DefaultExplicit(),
                Thrusters = clean
            };

            var json = JsonSerializer.Serialize(
                document,
                new JsonSerializerOptions
                {
                    WriteIndented = indented
                });

            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Configlerden veya wizard'dan gelen thruster listesini gÃ¼venli hale getirir.
        /// </summary>
        public static ThrusterDesc[] SanitizeThrusters(IReadOnlyList<ThrusterDesc> thrusters)
        {
            if (thrusters is null || thrusters.Count == 0)
                return Array.Empty<ThrusterDesc>();

            return thrusters
                .Where(t => t.Channel >= 0)
                .GroupBy(t => t.Channel)
                .Select(g => g.First())
                .OrderBy(t => t.Channel)
                .Select(t =>
                {
                    var id = string.IsNullOrWhiteSpace(t.Id)
                        ? $"CH{t.Channel}"
                        : t.Id.Trim();

                    var pos = IsFinite(t.Position)
                        ? t.Position
                        : Vec3.Zero;

                    var dir = IsFinite(t.ForceDir) && t.ForceDir.Length > 1e-9
                        ? t.ForceDir.Normalize()
                        : new Vec3(+1, 0, 0);

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

        private static JsonSerializerOptions CreateJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        private static void EnsureDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }

        private static bool IsFinite(Vec3 v)
        {
            return
                double.IsFinite(v.X) &&
                double.IsFinite(v.Y) &&
                double.IsFinite(v.Z);
        }

        /// <summary>
        /// Yeni thrusters.geometry.json root modeli.
        /// </summary>
        private sealed class ThrusterGeometryDocument
        {
            public string SchemaVersion { get; set; } = DefaultSchemaVersion;
            public string FrameId { get; set; } = DefaultFrameId;
            public ThrusterGeometryPolicy GeometryPolicy { get; set; } = ThrusterGeometryPolicy.DefaultExplicit();
            public ThrusterDesc[] Thrusters { get; set; } = Array.Empty<ThrusterDesc>();
        }

        private sealed class ThrusterGeometryPolicy
        {
            public string Mode { get; set; } = "Explicit";
            public FallbackLayoutConfig FallbackLayout { get; set; } = FallbackLayoutConfig.Default();

            public static ThrusterGeometryPolicy DefaultExplicit()
            {
                return new ThrusterGeometryPolicy
                {
                    Mode = "Explicit",
                    FallbackLayout = FallbackLayoutConfig.Default()
                };
            }
        }

        private sealed class FallbackLayoutConfig
        {
            public string Type { get; set; } = "Ring";
            public double RadiusM { get; set; } = 0.5;
            public VectorJson ThrustAxis { get; set; } = new() { x = 1.0, y = 0.0, z = 0.0 };

            public static FallbackLayoutConfig Default()
            {
                return new FallbackLayoutConfig
                {
                    Type = "Ring",
                    RadiusM = 0.5,
                    ThrustAxis = new VectorJson { x = 1.0, y = 0.0, z = 0.0 }
                };
            }
        }

        private sealed class VectorJson
        {
            public double x { get; set; }
            public double y { get; set; }
            public double z { get; set; }
        }
    }
}
