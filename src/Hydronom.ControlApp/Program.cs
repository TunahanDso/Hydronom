using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Hydronom ControlApp (Persistent TCP Client) - v5.0");
        Console.WriteLine("------------------------------------------------");

        Console.Write("Runtime HOST (örn: 127.0.0.1): ");
        var host = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        Console.Write("Runtime PORT (örn: 5060): ");
        var portText = Console.ReadLine()?.Trim();
        if (!int.TryParse(portText, out var port)) port = 5060;

        using var client = new TcpClient();
        Console.WriteLine($"Bağlanılıyor: {host}:{port} ...");

        try
        {
            await client.ConnectAsync(host, port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Bağlantı başarısız: {ex.Message}");
            return;
        }

        using NetworkStream stream = client.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        using var reader = new StreamReader(stream, new UTF8Encoding(false));

        PrintHelp();

        // Hoş geldin mesajı
        stream.ReadTimeout = 500;
        try
        {
            if (stream.DataAvailable)
            {
                var hello = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(hello))
                    Console.WriteLine($"[recv] {hello}");
            }
        }
        catch
        {
        }
        finally
        {
            stream.ReadTimeout = -1;
        }

        while (true)
        {
            Console.Write("> ");
            var input = (Console.ReadLine() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Bağlantı kapatılıyor...");
                break;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("?", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            string? jsonLine = BuildJson(input);
            if (jsonLine is null)
            {
                Console.WriteLine("Komut anlaşılmadı. 'help' yaz.");
                continue;
            }

            try
            {
                await writer.WriteLineAsync(jsonLine);
                Console.WriteLine($"[sent] {jsonLine}");

                stream.ReadTimeout = 1500;
                var resp = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(resp))
                    Console.WriteLine($"[recv] {resp}");

                // Bazı komutlarda ikinci satır da gelebilir (örn status / ai ack kombinasyonu)
                await DrainExtraLinesAsync(reader, stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Gönderim/okuma hatası: {ex.Message}");
                Console.WriteLine("Bağlantı kopmuş olabilir.");
                break;
            }
            finally
            {
                stream.ReadTimeout = -1;
            }
        }

        Console.WriteLine("Çıkılıyor.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Komutlar:");
        Console.WriteLine("  goto X Y [Z]               → hedef görev");
        Console.WriteLine("  goto hold X Y [Z]          → hold hedef görev");
        Console.WriteLine("  stop                       → görevi temizle");
        Console.WriteLine("  arm                        → sistemi arm et");
        Console.WriteLine("  disarm                     → sistemi disarm et");
        Console.WriteLine("  estop                      → emergency stop");
        Console.WriteLine("  clear-estop                → emergency stop temizle");
        Console.WriteLine("  manual on                  → manual mode aç");
        Console.WriteLine("  manual off                 → manual mode kapat");
        Console.WriteLine("  drive surge <v> yaw <v>    → manuel sürüş");
        Console.WriteLine("  drive surge <v> sway <v> heave <v> roll <v> pitch <v> yaw <v>");
        Console.WriteLine("  mstop                      → manual drive sıfırla");
        Console.WriteLine("  heartbeat                  → heartbeat gönder");
        Console.WriteLine("  lim thr <val>              → limiter throttle rate");
        Console.WriteLine("  lim rud <val>              → limiter rudder rate");
        Console.WriteLine("  ana ahead <m>              → analysis ahead");
        Console.WriteLine("  ana fov <deg>              → analysis fov");
        Console.WriteLine("  tick <ms>                  → runtime tick");
        Console.WriteLine("  status                     → durum iste");
        Console.WriteLine("  serial <port> [baud]       → runtime serial ayarla");
        Console.WriteLine("  serial off                 → serial kapat");
        Console.WriteLine("  ai <goal text>             → AI suggest");
        Console.WriteLine("  help                       → yardım");
        Console.WriteLine("  exit                       → çık");
        Console.WriteLine();
    }

    private static async Task DrainExtraLinesAsync(StreamReader reader, NetworkStream stream)
    {
        try
        {
            stream.ReadTimeout = 150;
            while (stream.DataAvailable)
            {
                var extra = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(extra))
                    break;

                Console.WriteLine($"[recv] {extra}");
            }
        }
        catch
        {
        }
    }

    private static string? BuildJson(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        // -----------------------------------------------------------------
        // GOTO / GOTO HOLD
        // -----------------------------------------------------------------
        if (parts[0].Equals("goto", StringComparison.OrdinalIgnoreCase))
        {
            bool hold = false;
            var nums = new List<double>();

            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Equals("hold", StringComparison.OrdinalIgnoreCase))
                {
                    hold = true;
                    continue;
                }

                if (double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    nums.Add(v);
            }

            double x, y, z = 0.0;

            if (nums.Count >= 3)
            {
                x = nums[0];
                y = nums[1];
                z = nums[2];
            }
            else if (nums.Count >= 2)
            {
                x = nums[0];
                y = nums[1];
            }
            else
            {
                Console.Write("Target X: ");
                double.TryParse(Console.ReadLine(), NumberStyles.Float, CultureInfo.InvariantCulture, out x);

                Console.Write("Target Y: ");
                double.TryParse(Console.ReadLine(), NumberStyles.Float, CultureInfo.InvariantCulture, out y);

                Console.Write("Target Z (opsiyonel): ");
                var zStr = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(zStr))
                    double.TryParse(zStr, NumberStyles.Float, CultureInfo.InvariantCulture, out z);
            }

            return
                "{" +
                "\"Type\":\"GoToPoint\"," +
                "\"Target\":{" +
                $"\"X\":{x.ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Y\":{y.ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Z\":{z.ToString("G", CultureInfo.InvariantCulture)}" +
                "}" +
                "}";
        }

        // -----------------------------------------------------------------
        // STOP / ARM / DISARM / ESTOP / HEARTBEAT
        // -----------------------------------------------------------------
        if (parts.Length == 1 && parts[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"Stop\"}";

        if (parts.Length == 1 && parts[0].Equals("arm", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"Arm\"}";

        if (parts.Length == 1 && parts[0].Equals("disarm", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"Disarm\"}";

        if (parts.Length == 1 && (
                parts[0].Equals("estop", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("e-stop", StringComparison.OrdinalIgnoreCase)))
            return "{\"Type\":\"EmergencyStop\"}";

        if (parts.Length == 1 && parts[0].Equals("clear-estop", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"ClearEmergencyStop\"}";

        if (parts.Length == 1 && parts[0].Equals("heartbeat", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"Heartbeat\"}";

        // -----------------------------------------------------------------
        // MANUAL MODE
        // -----------------------------------------------------------------
        if (parts.Length == 2 &&
            parts[0].Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            if (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                return "{\"Type\":\"SetManualMode\",\"Enabled\":true}";

            if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                return "{\"Type\":\"SetManualMode\",\"Enabled\":false}";

            return null;
        }

        if (parts.Length == 1 && parts[0].Equals("mstop", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"ManualStop\"}";

        // -----------------------------------------------------------------
        // DRIVE
        // Örnek:
        // drive surge 0.4 yaw 0.2
        // drive surge 0.3 sway 0.1 heave 0 yaw -0.2
        // -----------------------------------------------------------------
        if (parts[0].Equals("drive", StringComparison.OrdinalIgnoreCase))
        {
            double? surge = null, sway = null, heave = null, roll = null, pitch = null, yaw = null;

            for (int i = 1; i < parts.Length - 1; i += 2)
            {
                var key = parts[i].ToLowerInvariant();
                if (!double.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    return null;

                switch (key)
                {
                    case "surge": surge = val; break;
                    case "sway": sway = val; break;
                    case "heave": heave = val; break;
                    case "roll": roll = val; break;
                    case "pitch": pitch = val; break;
                    case "yaw": yaw = val; break;
                    default: return null;
                }
            }

            return
                "{" +
                "\"Type\":\"ManualDrive\"," +
                "\"Manual\":{" +
                $"\"Surge\":{(surge ?? 0.0).ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Sway\":{(sway ?? 0.0).ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Heave\":{(heave ?? 0.0).ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Roll\":{(roll ?? 0.0).ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Pitch\":{(pitch ?? 0.0).ToString("G", CultureInfo.InvariantCulture)}," +
                $"\"Yaw\":{(yaw ?? 0.0).ToString("G", CultureInfo.InvariantCulture)}" +
                "}" +
                "}";
        }

        // -----------------------------------------------------------------
        // LIMITER
        // -----------------------------------------------------------------
        if (parts.Length == 3 && parts[0].Equals("lim", StringComparison.OrdinalIgnoreCase))
        {
            if (parts[1].Equals("thr", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var thr))
            {
                return $"{{\"Type\":\"SetLimiter\",\"Limiter\":{{\"ThrottleRatePerSec\":{thr.ToString("G", CultureInfo.InvariantCulture)}}}}}";
            }

            if (parts[1].Equals("rud", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var rud))
            {
                return $"{{\"Type\":\"SetLimiter\",\"Limiter\":{{\"RudderRatePerSec\":{rud.ToString("G", CultureInfo.InvariantCulture)}}}}}";
            }

            return null;
        }

        // -----------------------------------------------------------------
        // ANALYSIS
        // -----------------------------------------------------------------
        if (parts.Length == 3 && parts[0].Equals("ana", StringComparison.OrdinalIgnoreCase))
        {
            if (parts[1].Equals("ahead", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
            {
                return $"{{\"Type\":\"SetAnalysis\",\"Analysis\":{{\"AheadDistanceM\":{m.ToString("G", CultureInfo.InvariantCulture)}}}}}";
            }

            if (parts[1].Equals("fov", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fov))
            {
                return $"{{\"Type\":\"SetAnalysis\",\"Analysis\":{{\"HalfFovDeg\":{fov.ToString("G", CultureInfo.InvariantCulture)}}}}}";
            }

            return null;
        }

        // -----------------------------------------------------------------
        // TICK
        // -----------------------------------------------------------------
        if (parts.Length == 2 && parts[0].Equals("tick", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(parts[1], out var ms))
                return $"{{\"Type\":\"SetTick\",\"TickMs\":{ms}}}";
            return null;
        }

        // -----------------------------------------------------------------
        // STATUS
        // -----------------------------------------------------------------
        if (parts.Length == 1 && parts[0].Equals("status", StringComparison.OrdinalIgnoreCase))
            return "{\"Type\":\"GetStatus\"}";

        // -----------------------------------------------------------------
        // SERIAL
        // -----------------------------------------------------------------
        if (parts.Length >= 2 && parts[0].Equals("serial", StringComparison.OrdinalIgnoreCase))
        {
            var arg = parts[1].Trim();
            int baud = 115200;
            if (parts.Length >= 3) int.TryParse(parts[2], out baud);

            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("disable", StringComparison.OrdinalIgnoreCase))
            {
                return $"{{\"Type\":\"SetSerial\",\"Port\":null,\"Baud\":{baud}}}";
            }

            var portEscaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{{\"Type\":\"SetSerial\",\"Port\":\"{portEscaped}\",\"Baud\":{baud}}}";
        }

        // -----------------------------------------------------------------
        // AI
        // -----------------------------------------------------------------
        if (parts.Length >= 2 && parts[0].Equals("ai", StringComparison.OrdinalIgnoreCase))
        {
            var goal = input.Substring(input.IndexOf(' ') + 1).Trim();
            var safeGoal = goal.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{{\"Type\":\"AiSuggest\",\"Goal\":\"{safeGoal}\"}}";
        }

        return null;
    }
}
