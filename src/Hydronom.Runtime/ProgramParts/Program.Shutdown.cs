using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Actuators;

partial class Program
{
    /// <summary>
    /// Runtime kapanış temizliğini merkezi olarak yapar.
    ///
    /// Kapanış sırası:
    /// 1. Native sensor bridge kapatılır.
    /// 2. Frame source async dispose edilir.
    /// 3. Python sensor hub süreci varsa sonlandırılır.
    /// 4. ActuatorManager dispose edilir.
    /// </summary>
    private static async Task ShutdownRuntimeAsync(
        IFrameSource frameSource,
        Process? pythonProc,
        ActuatorManager actuatorManager)
    {
        NativeSensors.TryShutdown();

        if (frameSource is IAsyncDisposable disp)
        {
            try
            {
                await disp.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SHUTDOWN] frame source dispose hata: {ex.Message}");
            }
        }

        ShutdownPythonProcess(pythonProc);

        try
        {
            actuatorManager.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SHUTDOWN] actuator manager dispose hata: {ex.Message}");
        }

        Console.WriteLine("Hydronom runtime stopped.");
    }

    /// <summary>
    /// Python sensor hub sürecini güvenli şekilde sonlandırır.
    /// </summary>
    private static void ShutdownPythonProcess(Process? pythonProc)
    {
        if (pythonProc is null)
            return;

        try
        {
            if (!pythonProc.HasExited)
            {
                Console.WriteLine("[PY] python main.py sonlandırılıyor...");

#if NET6_0_OR_GREATER
                pythonProc.Kill(entireProcessTree: true);
#else
                pythonProc.Kill();
#endif
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PY] python süreci sonlandırılırken hata: {ex.Message}");
        }
        finally
        {
            try
            {
                pythonProc.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PY] python process dispose hata: {ex.Message}");
            }
        }
    }
}