using System;
using System.Runtime.InteropServices;
using Hydronom.Core.Domain;

partial class Program
{
    /// <summary>
    /// Native sensor bridge.
    ///
    /// hydro_sensors native Ã§ekirdeÄŸi varsa runtime ona tick gÃ¶nderebilir.
    /// DLL yoksa sistem sessizce managed runtime ile Ã§alÄ±ÅŸmaya devam eder.
    /// </summary>
    private static class NativeSensors
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct HsFusedStateNative
        {
            public double pos_x;
            public double pos_y;
            public double pos_z;

            public double vel_x;
            public double vel_y;
            public double vel_z;

            public double yaw_deg;
            public double pitch_deg;
            public double roll_deg;

            public int has_fix;
            public double quality;
            public ulong timestamp;
        }

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_init();

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_shutdown();

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_tick(
            double dt_seconds,
            ref HsFusedStateNative state_input,
            double cmd_throttle,
            double cmd_rudder
        );

        [DllImport("hydro_sensors", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_get_fused_state(out HsFusedStateNative out_state);

        private static bool _available;

        internal static bool IsAvailable => _available;

        internal static void TryInit()
        {
            try
            {
                hs_init();
                _available = true;
                Console.WriteLine("[NATIVE] hydro_sensors Ã§ekirdeÄŸi yÃ¼klendi (C sensÃ¶r mimarisi aktif).");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[NATIVE] hydro_sensors.dll bulunamadÄ±, native sensÃ¶r Ã§ekirdeÄŸi pasif kalacak.");
                _available = false;
            }
            catch (EntryPointNotFoundException ex)
            {
                Console.WriteLine($"[NATIVE] hydro_sensors entrypoint bulunamadÄ±: {ex.Message}");
                _available = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_init baÅŸarÄ±sÄ±z: {ex.Message}");
                _available = false;
            }
        }

        internal static void TryShutdown()
        {
            if (!_available)
                return;

            try
            {
                hs_shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_shutdown hata: {ex.Message}");
            }
            finally
            {
                _available = false;
            }
        }

        internal static void TickIfAvailable(
            double dt,
            VehicleState state,
            double cmdThrottle,
            double cmdRudder)
        {
            if (!_available)
                return;

            if (!double.IsFinite(dt) || dt <= 0.0)
                dt = 0.01;

            var nativeState = new HsFusedStateNative
            {
                pos_x = Safe(state.Position.X),
                pos_y = Safe(state.Position.Y),
                pos_z = Safe(state.Position.Z),

                vel_x = Safe(state.LinearVelocity.X),
                vel_y = Safe(state.LinearVelocity.Y),
                vel_z = Safe(state.LinearVelocity.Z),

                yaw_deg = Safe(state.Orientation.YawDeg),
                pitch_deg = Safe(state.Orientation.PitchDeg),
                roll_deg = Safe(state.Orientation.RollDeg),

                has_fix = 1,
                quality = 1.0,
                timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            try
            {
                hs_tick(
                    dt,
                    ref nativeState,
                    Safe(cmdThrottle),
                    Safe(cmdRudder)
                );

                hs_get_fused_state(out _);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_tick Ã§aÄŸrÄ±sÄ±nda hata: {ex.Message}");
            }
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }
}
