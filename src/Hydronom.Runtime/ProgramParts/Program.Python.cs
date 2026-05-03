using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Python sensor hub Ã§alÄ±ÅŸma klasÃ¶rÃ¼nÃ¼ Ã§Ã¶zer.
    ///
    /// Ã–ncelik:
    /// 1. HYDRONOM_PYTHON_DIR environment variable
    /// 2. Python:WorkingDir config deÄŸeri
    /// 3. Runtime output altÄ±ndaki python klasÃ¶rÃ¼
    /// 4. Repo kÃ¶kÃ¼ne gÃ¶re ../python tahmini
    /// </summary>
    private static string? ResolvePythonWorkDir(IConfiguration config)
    {
        var envDir = Environment.GetEnvironmentVariable("HYDRONOM_PYTHON_DIR");
        if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir))
        {
            Console.WriteLine($"[PY] HYDRONOM_PYTHON_DIR ile python klasÃ¶rÃ¼ bulundu: {envDir}");
            return envDir;
        }

        var cfgDir = config["Python:WorkingDir"];
        if (!string.IsNullOrWhiteSpace(cfgDir) && Directory.Exists(cfgDir))
        {
            Console.WriteLine($"[PY] Python:WorkingDir ile python klasÃ¶rÃ¼ bulundu: {cfgDir}");
            return cfgDir;
        }

        var exeDir = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(exeDir, "python"),
            Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "..", "python")),
            Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "python")),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "python")),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "python"))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                Console.WriteLine($"[PY] python klasÃ¶rÃ¼ bulundu: {candidate}");
                return candidate;
            }
        }

        Console.WriteLine("[PY] python Ã§alÄ±ÅŸma klasÃ¶rÃ¼ bulunamadÄ±.");

        foreach (var candidate in candidates)
            Console.WriteLine($"[PY]  - denenen: {candidate}");

        Console.WriteLine("[PY] HYDRONOM_PYTHON_DIR veya Python:WorkingDir ile yol verebilirsin.");
        return null;
    }

    /// <summary>
    /// Python main.py otomatik baÅŸlatÄ±cÄ±.
    ///
    /// Python:AutoStart=false ise hiÃ§bir ÅŸey baÅŸlatmaz.
    /// Runtime tarafÄ±nÄ±n Python hub'Ä± ayrÄ±ca terminalden Ã§alÄ±ÅŸtÄ±rdÄ±ÄŸÄ± senaryolarda
    /// config Ã¼zerinden kapatÄ±lmasÄ± Ã¶nerilir.
    /// </summary>
    private static Process? MaybeStartPythonSensorHub(IConfiguration config)
    {
        var autoStr = config["Python:AutoStart"];
        bool autoStart = !bool.TryParse(autoStr, out var b) || b;

        if (!autoStart)
        {
            Console.WriteLine("[PY] Python sensor hub auto-start devre dÄ±ÅŸÄ± (Python:AutoStart=false).");
            return null;
        }

        var exe = config["Python:Executable"];
        if (string.IsNullOrWhiteSpace(exe))
            exe = OperatingSystem.IsWindows() ? "py" : "python3";

        var script = config["Python:Script"] ?? "main.py";
        var workDir = ResolvePythonWorkDir(config);

        if (workDir is null)
            return null;

        var scriptPath = Path.Combine(workDir, script);
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"[PY] Python script bulunamadÄ±: {scriptPath}");
            return null;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = script,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Environment["HYDRONOM_MODE"] = config["Python:Env:HYDRONOM_MODE"] ?? "runtime";
        psi.Environment["HYDRONOM_TCP_HOST"] = config["SensorSource:TcpJson:Host"] ?? "127.0.0.1";
        psi.Environment["HYDRONOM_TCP_PORT"] = config["SensorSource:TcpJson:Port"] ?? "5055";
        psi.Environment["PYTHONUTF8"] = "1";

        ApplyOptionalPythonEnv(config, psi, "HYDRONOM_IMU_BACKEND");
        ApplyOptionalPythonEnv(config, psi, "HYDRONOM_GPS_BACKEND");
        ApplyOptionalPythonEnv(config, psi, "HYDRONOM_LIDAR_BACKEND");
        ApplyOptionalPythonEnv(config, psi, "HYDRONOM_CAMERA_BACKEND");
        ApplyOptionalPythonEnv(config, psi, "HYDRONOM_DISABLE_CAMERA");
        ApplyOptionalPythonEnv(config, psi, "HYDRONOM_FORCE_NDJSON");

        try
        {
            var proc = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine("[PY] " + TrimForConsole(e.Data, 400));
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine("[PY-ERR] " + TrimForConsole(e.Data, 400));
            };

            if (!proc.Start())
            {
                Console.WriteLine("[PY] python main.py baÅŸlatÄ±lamadÄ± (Start=false).");
                return null;
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            Console.WriteLine($"[PY] Python sensor hub baÅŸlatÄ±ldÄ± (PID={proc.Id}) dir={workDir}");
            return proc;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PY] python main.py baÅŸlatÄ±lÄ±rken hata: {ex.Message}");
            return null;
        }
    }

    private static void ApplyOptionalPythonEnv(
        IConfiguration config,
        ProcessStartInfo psi,
        string name)
    {
        var configured = config[$"Python:Env:{name}"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            psi.Environment[name] = configured;
            return;
        }

        var existing = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(existing))
            psi.Environment[name] = existing;
    }

    private static string TrimForConsole(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (maxLength <= 0 || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength) + "...";
    }
}
