using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace Hydronom.Runtime.PicoUsbHardwareSmokeTest;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var options = HardwareSmokeOptions.Parse(args);

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        try
        {
            PrintHeader(options);

            var ports = ResolveCandidatePorts(options);

            if (ports.Count == 0)
            {
                return Finish(
                    pass: !options.RequireHardware,
                    message: options.RequireHardware
                        ? "No serial ports were found after filtering and --require-hardware was set."
                        : "No serial ports were found after filtering. Skipping hardware smoke test."
                );
            }

            Console.WriteLine("Candidate ports:");
            foreach (var port in ports)
            {
                Console.WriteLine($"- {port}");
            }

            Console.WriteLine();

            if (options.ListOnly)
            {
                return Finish(
                    pass: true,
                    message: "Port list completed. Probe was not executed because --list-only was set."
                );
            }

            var results = new List<HardwareProbeResult>();
            var bestResult = HardwareProbeResult.None;

            foreach (var port in ports)
            {
                var result = await ProbePortAsync(port, options);

                results.Add(result);
                PrintProbeResult(result);

                if (result.Score > bestResult.Score)
                {
                    bestResult = result;
                }

                if (result.Pass)
                {
                    PrintFinalSummary(results, bestResult);

                    return Finish(
                        pass: true,
                        message: $"Hydronom Pico-compatible device detected on {result.PortName}."
                    );
                }
            }

            PrintFinalSummary(results, bestResult);

            if (options.RequireHardware)
            {
                return Finish(
                    pass: false,
                    message: bestResult.PortName.Length == 0
                        ? "Hardware was required, but no candidate port could be probed."
                        : $"Hardware was required, but no Hydronom Pico-compatible response was detected. Best candidate: {bestResult.PortName}."
                );
            }

            return Finish(
                pass: true,
                message: "No Hydronom Pico-compatible hardware response detected. Test skipped because --require-hardware was not set."
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Pico USB hardware smoke test failed with an unexpected error.");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PrintHeader(HardwareSmokeOptions options)
    {
        Console.WriteLine("Hydronom Pico USB Hardware Smoke Test");
        Console.WriteLine("-------------------------------------");
        Console.WriteLine($"Mode             : {(options.Auto ? "auto" : "manual")}");
        Console.WriteLine($"Port             : {options.PortName}");
        Console.WriteLine($"Baud             : {options.BaudRate}");
        Console.WriteLine($"Timeout          : {options.TimeoutMs} ms");
        Console.WriteLine($"Startup delay    : {options.StartupDelayMs} ms");
        Console.WriteLine($"Max open time    : {options.MaxPortOpenMs} ms");
        Console.WriteLine($"Require hardware : {options.RequireHardware}");
        Console.WriteLine($"List only        : {options.ListOnly}");
        Console.WriteLine($"Skip ports       : {FormatPorts(options.SkipPorts)}");
        Console.WriteLine();

        /*
         * Test kapsamı:
         * - Rastgele her Pico'yu kabul etmez.
         * - Hydronom Pico/MCU sensör protokolünü konuşan cihazı arar.
         * - İlk sürümde handshake/ASCII probe odaklıdır.
         * - Binary frame parser olgunlaştığında PicoUsbFrameDecoder ile daha sıkı doğrulama yapılacak.
         *
         * Önemli:
         * Bu test CI/build testi değildir; gerçek USB donanım teşhis testidir.
         * Donanım zorunlu olsun isteniyorsa --require-hardware kullanılmalıdır.
         */
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Hydronom Pico USB Hardware Smoke Test");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project .\\tests\\Hydronom.Runtime.PicoUsbHardwareSmokeTest\\Hydronom.Runtime.PicoUsbHardwareSmokeTest.csproj -- --auto");
        Console.WriteLine("  dotnet run --project .\\tests\\Hydronom.Runtime.PicoUsbHardwareSmokeTest\\Hydronom.Runtime.PicoUsbHardwareSmokeTest.csproj -- --auto --require-hardware");
        Console.WriteLine("  dotnet run --project .\\tests\\Hydronom.Runtime.PicoUsbHardwareSmokeTest\\Hydronom.Runtime.PicoUsbHardwareSmokeTest.csproj -- --port COM7 --require-hardware");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --auto                         Auto-scan serial ports.");
        Console.WriteLine("  --port COM7                    Probe one specific port.");
        Console.WriteLine("  --baud 115200                  Serial baudrate. Default: 115200.");
        Console.WriteLine("  --timeout-ms 3000              Read/probe timeout per port. Default: 3000.");
        Console.WriteLine("  --startup-delay-ms 1200        Delay after opening port. Default: 1200.");
        Console.WriteLine("  --max-open-ms 2500             Maximum time allowed for SerialPort.Open. Default: 2500.");
        Console.WriteLine("  --skip-port COM13              Skip one or more ports. Supports comma-separated values.");
        Console.WriteLine("  --skip-port COM13,COM14        Skip multiple ports.");
        Console.WriteLine("  --list-only                    List candidate ports and exit without probing.");
        Console.WriteLine("  --require-hardware             Return FAIL when no Hydronom Pico-compatible device is detected.");
        Console.WriteLine("  --help                         Show this help.");
    }

    private static IReadOnlyList<string> ResolveCandidatePorts(HardwareSmokeOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PortName))
        {
            var manualPort = options.PortName.Trim();

            if (options.SkipPorts.Contains(manualPort))
            {
                return Array.Empty<string>();
            }

            return new[] { manualPort };
        }

        if (!options.Auto)
        {
            return Array.Empty<string>();
        }

        try
        {
            return SerialPort
                .GetPortNames()
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => !options.SkipPorts.Contains(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static async Task<HardwareProbeResult> ProbePortAsync(
        string portName,
        HardwareSmokeOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var receivedText = new StringBuilder();
        var receivedBytes = new List<byte>();

        SerialPort? port = null;

        try
        {
            var openResult = await TryOpenPortAsync(portName, options);

            if (!openResult.Opened || openResult.Port is null)
            {
                stopwatch.Stop();

                return new HardwareProbeResult(
                    PortName: portName,
                    Opened: false,
                    ReceivedBytes: 0,
                    ReceivedTextPreview: "",
                    Pass: false,
                    Score: 0,
                    ElapsedMs: stopwatch.Elapsed.TotalMilliseconds,
                    Message: openResult.Message
                );
            }

            port = openResult.Port;

            /*
             * Bazı USB CDC cihazları port açıldıktan hemen sonra reset atabilir.
             * Kısa bekleme, Pico/MCU'nun ayağa kalkmasına zaman verir.
             */
            await Task.Delay(options.StartupDelayMs);

            SafeDiscardBuffers(port);

            SafeWriteLine(port, "HYDRONOM_PICO_HELLO");
            SafeWriteLine(port, "HYDRONOM_SENSOR_HELLO");
            SafeWriteLine(port, "?");

            var deadline = DateTime.UtcNow.AddMilliseconds(options.TimeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);

                var available = SafeBytesToRead(port);

                if (available <= 0)
                {
                    continue;
                }

                var buffer = new byte[Math.Min(available, 512)];
                var read = SafeRead(port, buffer);

                if (read <= 0)
                {
                    continue;
                }

                for (var i = 0; i < read; i++)
                {
                    receivedBytes.Add(buffer[i]);
                }

                receivedText.Append(Encoding.ASCII.GetString(buffer, 0, read));

                if (LooksLikeHydronomPicoResponse(receivedText.ToString()))
                {
                    stopwatch.Stop();

                    return new HardwareProbeResult(
                        PortName: portName,
                        Opened: true,
                        ReceivedBytes: receivedBytes.Count,
                        ReceivedTextPreview: Preview(receivedText.ToString()),
                        Pass: true,
                        Score: 100,
                        ElapsedMs: stopwatch.Elapsed.TotalMilliseconds,
                        Message: "Hydronom Pico handshake-like response detected."
                    );
                }
            }

            stopwatch.Stop();

            var score = receivedBytes.Count > 0 ? 40 : 10;

            return new HardwareProbeResult(
                PortName: portName,
                Opened: true,
                ReceivedBytes: receivedBytes.Count,
                ReceivedTextPreview: Preview(receivedText.ToString()),
                Pass: false,
                Score: score,
                ElapsedMs: stopwatch.Elapsed.TotalMilliseconds,
                Message: receivedBytes.Count > 0
                    ? "Port opened and data was received, but Hydronom Pico handshake was not detected."
                    : "Port opened, but no data was received."
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HardwareProbeResult(
                PortName: portName,
                Opened: port?.IsOpen == true,
                ReceivedBytes: receivedBytes.Count,
                ReceivedTextPreview: Preview(receivedText.ToString()),
                Pass: false,
                Score: 0,
                ElapsedMs: stopwatch.Elapsed.TotalMilliseconds,
                Message: ex.Message
            );
        }
        finally
        {
            SafeClose(port);
        }
    }

    private static async Task<SerialOpenResult> TryOpenPortAsync(
        string portName,
        HardwareSmokeOptions options)
    {
        var port = CreateSerialPort(portName, options);

        var openTask = Task.Run(() =>
        {
            port.Open();
        });

        var timeoutTask = Task.Delay(options.MaxPortOpenMs);
        var completed = await Task.WhenAny(openTask, timeoutTask);

        if (completed != openTask)
        {
            ObserveEventually(openTask);
            SafeClose(port);

            return new SerialOpenResult(
                Opened: false,
                Port: null,
                Message: $"SerialPort.Open timed out after {options.MaxPortOpenMs} ms."
            );
        }

        try
        {
            await openTask;

            return new SerialOpenResult(
                Opened: true,
                Port: port,
                Message: "Port opened."
            );
        }
        catch (Exception ex)
        {
            SafeClose(port);

            return new SerialOpenResult(
                Opened: false,
                Port: null,
                Message: ex.Message
            );
        }
    }

    private static SerialPort CreateSerialPort(string portName, HardwareSmokeOptions options)
    {
        return new SerialPort(portName, options.BaudRate)
        {
            ReadTimeout = Math.Max(100, options.TimeoutMs),
            WriteTimeout = Math.Max(100, options.TimeoutMs),
            DtrEnable = true,
            RtsEnable = true,
            NewLine = "\n",
            Encoding = Encoding.ASCII
        };
    }

    private static void ObserveEventually(Task task)
    {
        _ = task.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
            },
            TaskContinuationOptions.OnlyOnFaulted
        );
    }

    private static void SafeWriteLine(SerialPort port, string text)
    {
        try
        {
            port.WriteLine(text);
        }
        catch
        {
            // Bazı cihazlar yazmaya kapalı olabilir. Probe okumaya devam eder.
        }
    }

    private static void SafeDiscardBuffers(SerialPort port)
    {
        try
        {
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
        }
        catch
        {
            // Bazı sürücüler buffer temizlemeyi desteklemeyebilir.
        }
    }

    private static int SafeBytesToRead(SerialPort port)
    {
        try
        {
            return port.BytesToRead;
        }
        catch
        {
            return 0;
        }
    }

    private static int SafeRead(SerialPort port, byte[] buffer)
    {
        try
        {
            return port.Read(buffer, 0, buffer.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static void SafeClose(SerialPort? port)
    {
        if (port is null)
        {
            return;
        }

        try
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }
        catch
        {
            // Kapanış hatası smoke test sonucunu gölgelememeli.
        }

        try
        {
            port.Dispose();
        }
        catch
        {
            // Dispose hatası smoke test sonucunu gölgelememeli.
        }
    }

    private static bool LooksLikeHydronomPicoResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToUpperInvariant();

        return normalized.Contains("HYDRONOM_PICO", StringComparison.Ordinal) ||
               normalized.Contains("HYDRONOM_SENSOR", StringComparison.Ordinal) ||
               normalized.Contains("PICO_SENSOR", StringComparison.Ordinal) ||
               normalized.Contains("PICO_USB", StringComparison.Ordinal) ||
               normalized.Contains("HYDRO_SENSOR", StringComparison.Ordinal) ||
               normalized.Contains("HYDRONOM_PICO_OK", StringComparison.Ordinal) ||
               normalized.Contains("HYDRONOM_SENSOR_OK", StringComparison.Ordinal);
    }

    private static void PrintProbeResult(HardwareProbeResult result)
    {
        Console.WriteLine($"Port: {result.PortName}");
        Console.WriteLine($"  Opened        : {result.Opened}");
        Console.WriteLine($"  Pass          : {result.Pass}");
        Console.WriteLine($"  Score         : {result.Score}");
        Console.WriteLine($"  Received bytes: {result.ReceivedBytes}");
        Console.WriteLine($"  Elapsed       : {result.ElapsedMs:0.0} ms");
        Console.WriteLine($"  Message       : {result.Message}");

        if (!string.IsNullOrWhiteSpace(result.ReceivedTextPreview))
        {
            Console.WriteLine("  Preview:");
            Console.WriteLine($"  {result.ReceivedTextPreview}");
        }

        Console.WriteLine();
    }

    private static void PrintFinalSummary(
        IReadOnlyList<HardwareProbeResult> results,
        HardwareProbeResult bestResult)
    {
        Console.WriteLine("Final summary");
        Console.WriteLine("-------------");
        Console.WriteLine($"Ports probed       : {results.Count}");
        Console.WriteLine($"Opened ports       : {results.Count(x => x.Opened)}");
        Console.WriteLine($"Ports with data    : {results.Count(x => x.ReceivedBytes > 0)}");
        Console.WriteLine($"Passing ports      : {results.Count(x => x.Pass)}");

        if (!string.IsNullOrWhiteSpace(bestResult.PortName))
        {
            Console.WriteLine($"Best candidate     : {bestResult.PortName}");
            Console.WriteLine($"Best score         : {bestResult.Score}");
            Console.WriteLine($"Best message       : {bestResult.Message}");
        }

        Console.WriteLine();
    }

    private static int Finish(bool pass, string message)
    {
        Console.WriteLine(message);
        Console.WriteLine(pass ? "Result: PASS" : "Result: FAIL");

        return pass ? 0 : 1;
    }

    private static string Preview(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var clean = text
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Trim();

        if (clean.Length <= 240)
        {
            return clean;
        }

        return clean[..240] + "...";
    }

    private static string FormatPorts(IReadOnlySet<string> ports)
    {
        if (ports.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", ports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record HardwareSmokeOptions(
        bool Auto,
        string PortName,
        int BaudRate,
        int TimeoutMs,
        int StartupDelayMs,
        int MaxPortOpenMs,
        bool RequireHardware,
        bool ListOnly,
        bool ShowHelp,
        IReadOnlySet<string> SkipPorts
    )
    {
        public static HardwareSmokeOptions Parse(string[] args)
        {
            var auto = args.Any(x => x.Equals("--auto", StringComparison.OrdinalIgnoreCase));
            var requireHardware = args.Any(x => x.Equals("--require-hardware", StringComparison.OrdinalIgnoreCase));
            var listOnly = args.Any(x => x.Equals("--list-only", StringComparison.OrdinalIgnoreCase));
            var showHelp = args.Any(x =>
                x.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("/?", StringComparison.OrdinalIgnoreCase)
            );

            var port = ReadOption(args, "--port", "");
            var baudText = ReadOption(args, "--baud", "115200");
            var timeoutText = ReadOption(args, "--timeout-ms", "3000");
            var startupDelayText = ReadOption(args, "--startup-delay-ms", "1200");
            var maxOpenText = ReadOption(args, "--max-open-ms", "2500");
            var skipPorts = ReadMultiOption(args, "--skip-port");

            return new HardwareSmokeOptions(
                Auto: auto || string.IsNullOrWhiteSpace(port),
                PortName: port,
                BaudRate: ParseInt(baudText, 115200, min: 1200),
                TimeoutMs: ParseInt(timeoutText, 3000, min: 250),
                StartupDelayMs: ParseInt(startupDelayText, 1200, min: 0),
                MaxPortOpenMs: ParseInt(maxOpenText, 2500, min: 250),
                RequireHardware: requireHardware,
                ListOnly: listOnly,
                ShowHelp: showHelp,
                SkipPorts: ParseSkipPorts(skipPorts)
            );
        }

        private static IReadOnlySet<string> ParseSkipPorts(IEnumerable<string> values)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                foreach (var port in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(port))
                    {
                        result.Add(port.Trim());
                    }
                }
            }

            return result;
        }

        private static string ReadOption(string[] args, string name, string fallback)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    return fallback;
                }

                return args[i + 1];
            }

            return fallback;
        }

        private static IReadOnlyList<string> ReadMultiOption(string[] args, string name)
        {
            var result = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    continue;
                }

                result.Add(args[i + 1]);
            }

            return result;
        }

        private static int ParseInt(string text, int fallback, int min)
        {
            if (!int.TryParse(text, out var value))
            {
                return fallback;
            }

            return value < min ? fallback : value;
        }
    }

    private sealed record SerialOpenResult(
        bool Opened,
        SerialPort? Port,
        string Message
    );

    private sealed record HardwareProbeResult(
        string PortName,
        bool Opened,
        int ReceivedBytes,
        string ReceivedTextPreview,
        bool Pass,
        int Score,
        double ElapsedMs,
        string Message
    )
    {
        public static HardwareProbeResult None => new(
            PortName: "",
            Opened: false,
            ReceivedBytes: 0,
            ReceivedTextPreview: "",
            Pass: false,
            Score: -1,
            ElapsedMs: 0.0,
            Message: "No probe result."
        );
    }
}