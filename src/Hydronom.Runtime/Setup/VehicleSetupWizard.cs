using System;
using System.Collections.Generic;
using System.Text.Json;
using Hydronom.Core.Domain;              // Vec3
using Hydronom.Runtime.Actuators;        // MotorDesc, vb.
// Bu dosyada ThrusterDesc adı Hydronom.Runtime.Actuators tipine işaret etsin:
using ThrusterDesc = Hydronom.Runtime.Actuators.ThrusterDesc;

namespace Hydronom.Runtime.Setup
{
    /// <summary>
    /// Basit araç kurulum sihirbazı:
    /// - JSON'dan Thrusters yükle (dosya veya ham JSON).
    /// - Bulamazsa eski "Motors" tanımından varsayılan geometriyle Thrusters üret.
    /// - Gerekirse listeyi JSON’a kaydet.
    /// </summary>
    public static class VehicleSetupWizard
    {
        /// <summary>JSON metninden Thruster tanımlarını yükler; hatada null döner.</summary>
        public static ThrusterDesc[]? TryLoadThrustersFromJson(string json)
        {
            try { return JsonSerializer.Deserialize<ThrusterDesc[]>(json); }
            catch { return null; }
        }

        /// <summary>Dosyadan Thruster tanımlarını yükler; yoksa/null döner.</summary>
        public static ThrusterDesc[]? TryLoadThrustersFromFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                    return null;

                var json = System.IO.File.ReadAllText(path);
                return TryLoadThrustersFromJson(json);
            }
            catch { return null; }
        }

        /// <summary>
        /// Eski MotorDesc listesinden (FL/FR/RL/RR adlarına bakarak) varsayılan bir Thruster listesi üretir.
        /// </summary>
        public static ThrusterDesc[] MapMotorsToThrusters(IReadOnlyList<MotorDesc> motors)
        {
            // Basit gövde yarı boyutları (m): X = ileri/geri, Y = iskele/sancak
            const double halfX = 0.5;
            const double halfY = 0.5;

            Vec3 PosForId(string? id, int index, int count)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var u = id.Trim().ToUpperInvariant();
                    // Klasik quad adlandırması
                    if (u == "FL") return new Vec3(-halfX, +halfY, 0);
                    if (u == "FR") return new Vec3(+halfX, +halfY, 0);
                    if (u == "RL") return new Vec3(-halfX, -halfY, 0);
                    if (u == "RR") return new Vec3(+halfX, -halfY, 0);
                }

                // Bilinmiyorsa: sıraya göre üst sıra +Y, alt sıra -Y; X sola/sağa dağıt
                bool top = index < (count + 1) / 2;
                double y = top ? +halfY : -halfY;
                int col = top ? index : index - (count + 1) / 2;
                double x = (col % 2 == 0) ? -halfX : +halfX;
                return new Vec3(x, y, 0);
            }

            var list = new List<ThrusterDesc>(motors.Count);
            for (int i = 0; i < motors.Count; i++)
            {
                var m = motors[i];
                var pos = PosForId(m.Id, i, motors.Count);
                var dir = new Vec3(1, 0, 0); // ileri yön: +X
                list.Add(new ThrusterDesc(m.Id ?? $"CH{m.Channel}", m.Channel, pos, dir, Reversed: false));
            }
            return list.ToArray();
        }

        /// <summary>Thruster listesini güzel JSON olarak diske kaydet (UTF8, üstüne yazar).</summary>
        public static void SaveThrustersToFile(string path, IReadOnlyList<ThrusterDesc> thrusters, bool indented = true)
        {
            var json = JsonSerializer.Serialize(thrusters, new JsonSerializerOptions { WriteIndented = indented });
            System.IO.File.WriteAllText(path, json);
        }
    }
}
