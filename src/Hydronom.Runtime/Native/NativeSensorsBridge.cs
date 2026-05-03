using System;
using System.Runtime.InteropServices;
using Hydronom.Core.Domain;   // VehicleState iÃ§in
using Hydronom.Core.Modules;  // DecisionCommand kullanÄ±rsan lazÄ±m olabilir

namespace Hydronom.Runtime.Native
{
    /// <summary>
    /// C tarafÄ±ndaki hydro_sensors.dll Ã§ekirdeÄŸi ile kÃ¶prÃ¼.
    /// - Runtime baÅŸlarken hs_init Ã§aÄŸÄ±rÄ±lÄ±r.
    /// - Her tick'te hs_tick ile C Ã§ekirdeÄŸine state + komut gÃ¶nderilir.
    /// - Ä°stenirse hs_get_fused_state / hs_get_health / hs_pop_event ile durum okunur.
    /// </summary>
    internal static class NativeSensorsBridge
    {
        private const string DllName = "hydro_sensors.dll";

        private static bool _initTried;
        private static bool _available;
        private static bool _permanentlyDisabled;

        // DLL ile Ã§aÄŸrÄ± sÄ±rasÄ±nda kÃ¼Ã§Ã¼k hatalarda runtime'Ä±n Ã§Ã¶kmesini istemiyoruz.
        // Hata durumda bu flag set edilir ve sonraki Ã§aÄŸrÄ±lar sessizce yok sayÄ±lÄ±r.
        private static int _consecutiveErrors;

        // Native fused state'i geri okurken kullanmak iÃ§in cache
        private static HsFusedState _lastNativeState;
        private static HsHealth _lastHealth;

        // ---------------------------------------------------------------------
        // Native enum ve struct karÅŸÄ±lÄ±klarÄ±
        // ---------------------------------------------------------------------

        private enum HsHealthStatus : int
        {
            Unknown = 0,
            Ok = 1,
            Warn = 2,
            Error = 3
        }

        private enum HsEventType : int
        {
            None = 0,
            SensorError = 1,
            SensorWarn = 2,
            FusionReset = 3,
            Custom = 100
        }

        private enum HsEventSeverity : int
        {
            Info = 0,
            Warn = 1,
            Error = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HsFusedState
        {
            public uint StructSize;
            public uint Version;

            public ulong Seq;

            public double PosX;
            public double PosY;
            public double PosZ;

            public double VelX;
            public double VelY;
            public double VelZ;

            public double YawDeg;
            public double PitchDeg;
            public double RollDeg;

            public double SpeedMps;

            public int HasFix;
            public double Quality;

            public ulong Timestamp;

            // C tarafÄ±ndaki reserved alanlarÄ± ÅŸimdilik yÃ¶netmiyoruz,
            // ama struct boyutunu doÄŸru yansÄ±tmak iÃ§in burada bÄ±rakabiliriz.
            // Ä°leride gerekirse doldururuz.
            // public uint ReservedU32_0, ReservedU32_1, ReservedU32_2, ReservedU32_3;
            // public double ReservedF64_0, ReservedF64_1, ReservedF64_2, ReservedF64_3;
            // public double ReservedF64_4, ReservedF64_5, ReservedF64_6, ReservedF64_7;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct HsHealth
        {
            public HsHealthStatus Status;
            public ulong Timestamp;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Message;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct HsEvent
        {
            public HsEventType Type;
            public HsEventSeverity Severity;
            public ulong Timestamp;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Message;
        }

        // ---------------------------------------------------------------------
        // P/Invoke tanÄ±mlarÄ±
        // ---------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_init();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_tick(
            double dtSeconds,
            ref HsFusedState stateInput,
            double cmdThrottle,
            double cmdRudder);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_get_fused_state(out HsFusedState outState);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_get_health(out HsHealth outHealth);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hs_pop_event(out HsEvent outEvent);

        // ---------------------------------------------------------------------
        // DÄ±ÅŸarÄ± aÃ§Ä±k API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Runtime aÃ§Ä±lÄ±rken bir kez Ã§aÄŸrÄ±lmalÄ±.
        /// hydro_sensors.dll yoksa sistemi bozmadan devam eder.
        /// </summary>
        internal static void Initialize()
        {
            if (_initTried)
                return;

            _initTried = true;

            try
            {
                hs_init();
                _available = true;
                _consecutiveErrors = 0;

                Console.WriteLine("[NATIVE] hydro_sensors Ã§ekirdeÄŸi yÃ¼klendi (C sensÃ¶r mimarisi aktif).");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[NATIVE] hydro_sensors.dll bulunamadÄ±, native sensÃ¶r Ã§ekirdeÄŸi pasif kalacak.");
                _available = false;
                _permanentlyDisabled = true;
            }
            catch (EntryPointNotFoundException ex)
            {
                Console.WriteLine($"[NATIVE] hydro_sensors.dll iÃ§inde beklenen semboller bulunamadÄ±: {ex.Message}");
                _available = false;
                _permanentlyDisabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hydro_sensors init hatasÄ±: {ex.Message}");
                _available = false;
                _permanentlyDisabled = true;
            }
        }

        /// <summary>
        /// Runtime kapanÄ±rken Ã§aÄŸrÄ±lmalÄ± (Program.cs finally bloÄŸunda).
        /// </summary>
        internal static void Shutdown()
        {
            if (!_available || _permanentlyDisabled)
                return;

            try
            {
                hs_shutdown();
            }
            catch
            {
                // KapanÄ±ÅŸta hata olursa kritik deÄŸil, sessiz geÃ§iyoruz.
            }
        }

        /// <summary>
        /// Her kontrol tick'inde Ã§aÄŸrÄ±lacak kÃ¶prÃ¼:
        /// - C# fiziÄŸinin Ã¼rettiÄŸi VehicleState'i HsFusedState'e Ã§evirir.
        /// - Karar modÃ¼lÃ¼nden gelen throttle / rudder ile birlikte hs_tick'e gÃ¶nderir.
        /// </summary>
        internal static void Tick(double dtSeconds, VehicleState state, double throttle01, double rudderNeg1To1)
        {
            if (!_available || _permanentlyDisabled)
                return;

            var nativeState = FromVehicleState(state);

            try
            {
                hs_tick(dtSeconds, ref nativeState, throttle01, rudderNeg1To1);
                _consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;

                Console.WriteLine($"[NATIVE] hs_tick hatasÄ±: {ex.Message}");

                if (_consecutiveErrors >= 5)
                {
                    Console.WriteLine("[NATIVE] Ã‡ok sayÄ±da hata aldÄ±, native Ã§ekirdek devre dÄ±ÅŸÄ± bÄ±rakÄ±lÄ±yor.");
                    _available = false;
                    _permanentlyDisabled = true;
                }
            }
        }

        /// <summary>
        /// Native fused state'i okur. BaÅŸarÄ±lÄ±ysa true dÃ¶ner.
        /// Åimdilik sadece telemetri / debug iÃ§in; istersek state override da yapabiliriz.
        /// </summary>
        internal static bool TryGetFusedState(out VehicleState fusedState)
        {
            fusedState = default;

            if (!_available || _permanentlyDisabled)
                return false;

            try
            {
                hs_get_fused_state(out _lastNativeState);

                if (_lastNativeState.HasFix == 0)
                    return false;

                fusedState = ToVehicleState(_lastNativeState);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_get_fused_state hatasÄ±: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Native health snapshot'Ä±nÄ± okur.
        /// </summary>
        internal static bool TryGetHealth(out string statusText)
        {
            statusText = string.Empty;

            if (!_available || _permanentlyDisabled)
                return false;

            try
            {
                hs_get_health(out _lastHealth);

                var status = _lastHealth.Status;
                var msg = _lastHealth.Message ?? string.Empty;

                statusText = $"status={status} msg={msg}";
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_get_health hatasÄ±: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Event halkasÄ±ndan bir event Ã§eker, varsa metin olarak dÃ¶ndÃ¼rÃ¼r.
        /// Yoksa false dÃ¶ner.
        /// </summary>
        internal static bool TryPopEvent(out string eventText)
        {
            eventText = string.Empty;

            if (!_available || _permanentlyDisabled)
                return false;

            try
            {
                HsEvent ev;
                hs_pop_event(out ev);

                if (ev.Type == HsEventType.None)
                    return false;

                var msg = ev.Message ?? string.Empty;
                eventText = $"type={ev.Type} sev={ev.Severity} msg={msg}";
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hs_pop_event hatasÄ±: {ex.Message}");
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // VehicleState â†” HsFusedState dÃ¶nÃ¼ÅŸtÃ¼rÃ¼cÃ¼ler
        // ---------------------------------------------------------------------

        private static HsFusedState FromVehicleState(VehicleState state)
        {
            var nowMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var hs = new HsFusedState
            {
                StructSize = (uint)Marshal.SizeOf<HsFusedState>(),
                Version = 1,
                Seq = 0, // C tarafÄ± zaten kendi seq'ini artÄ±rÄ±yor

                PosX = state.Position.X,
                PosY = state.Position.Y,
                PosZ = state.Position.Z,

                VelX = state.LinearVelocity.X,
                VelY = state.LinearVelocity.Y,
                VelZ = state.LinearVelocity.Z,

                YawDeg = state.Orientation.YawDeg,
                PitchDeg = state.Orientation.PitchDeg,
                RollDeg = state.Orientation.RollDeg,

                // HÄ±z bÃ¼yÃ¼klÃ¼ÄŸÃ¼nÃ¼ C tarafÄ± da hesaplÄ±yor ama burada da doldurmak sorun deÄŸil
                SpeedMps = Math.Sqrt(
                    state.LinearVelocity.X * state.LinearVelocity.X +
                    state.LinearVelocity.Y * state.LinearVelocity.Y +
                    state.LinearVelocity.Z * state.LinearVelocity.Z),

                HasFix = 1,        // C# fiziÄŸi state veriyorsa "fix var" kabul ediyoruz
                Quality = 0.9,     // Åimdilik sabit kalite

                Timestamp = nowMs
            };

            return hs;
        }

        private static VehicleState ToVehicleState(HsFusedState hs)
        {
            // Åimdilik sadece pozisyon + yaw'Ä± kullanÄ±yoruz.
            // Ä°stersek roll/pitch ve hÄ±zlarÄ± da aktarabiliriz.
            var pos = new Vec3(hs.PosX, hs.PosY, hs.PosZ);
            var ori = new Orientation(
                rollDeg: hs.RollDeg,
                pitchDeg: hs.PitchDeg,
                yawDeg: hs.YawDeg);

            // Var olan state'in geri kalan alanlarÄ±nÄ± doldurmak iÃ§in minimum bir ÅŸablon.
            // Program.cs tarafÄ±nda bunu ister doÄŸrudan, ister override mantÄ±ÄŸÄ±yla kullanabilirsin.
            var state = VehicleState.Zero with
            {
                Position = pos,
                Orientation = ori,

                LinearVelocity = new Vec3(hs.VelX, hs.VelY, hs.VelZ)
            };

            return state;
        }
    }
}

