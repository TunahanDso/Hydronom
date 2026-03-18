using System;
using System.Runtime.InteropServices;
using Hydronom.Core.Domain;   // VehicleState için
using Hydronom.Core.Modules;  // DecisionCommand kullanırsan lazım olabilir

namespace Hydronom.Runtime.Native
{
    /// <summary>
    /// C tarafındaki hydro_sensors.dll çekirdeği ile köprü.
    /// - Runtime başlarken hs_init çağırılır.
    /// - Her tick'te hs_tick ile C çekirdeğine state + komut gönderilir.
    /// - İstenirse hs_get_fused_state / hs_get_health / hs_pop_event ile durum okunur.
    /// </summary>
    internal static class NativeSensorsBridge
    {
        private const string DllName = "hydro_sensors.dll";

        private static bool _initTried;
        private static bool _available;
        private static bool _permanentlyDisabled;

        // DLL ile çağrı sırasında küçük hatalarda runtime'ın çökmesini istemiyoruz.
        // Hata durumda bu flag set edilir ve sonraki çağrılar sessizce yok sayılır.
        private static int _consecutiveErrors;

        // Native fused state'i geri okurken kullanmak için cache
        private static HsFusedState _lastNativeState;
        private static HsHealth _lastHealth;

        // ---------------------------------------------------------------------
        // Native enum ve struct karşılıkları
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

            // C tarafındaki reserved alanları şimdilik yönetmiyoruz,
            // ama struct boyutunu doğru yansıtmak için burada bırakabiliriz.
            // İleride gerekirse doldururuz.
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
        // P/Invoke tanımları
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
        // Dışarı açık API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Runtime açılırken bir kez çağrılmalı.
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

                Console.WriteLine("[NATIVE] hydro_sensors çekirdeği yüklendi (C sensör mimarisi aktif).");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[NATIVE] hydro_sensors.dll bulunamadı, native sensör çekirdeği pasif kalacak.");
                _available = false;
                _permanentlyDisabled = true;
            }
            catch (EntryPointNotFoundException ex)
            {
                Console.WriteLine($"[NATIVE] hydro_sensors.dll içinde beklenen semboller bulunamadı: {ex.Message}");
                _available = false;
                _permanentlyDisabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NATIVE] hydro_sensors init hatası: {ex.Message}");
                _available = false;
                _permanentlyDisabled = true;
            }
        }

        /// <summary>
        /// Runtime kapanırken çağrılmalı (Program.cs finally bloğunda).
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
                // Kapanışta hata olursa kritik değil, sessiz geçiyoruz.
            }
        }

        /// <summary>
        /// Her kontrol tick'inde çağrılacak köprü:
        /// - C# fiziğinin ürettiği VehicleState'i HsFusedState'e çevirir.
        /// - Karar modülünden gelen throttle / rudder ile birlikte hs_tick'e gönderir.
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

                Console.WriteLine($"[NATIVE] hs_tick hatası: {ex.Message}");

                if (_consecutiveErrors >= 5)
                {
                    Console.WriteLine("[NATIVE] Çok sayıda hata aldı, native çekirdek devre dışı bırakılıyor.");
                    _available = false;
                    _permanentlyDisabled = true;
                }
            }
        }

        /// <summary>
        /// Native fused state'i okur. Başarılıysa true döner.
        /// Şimdilik sadece telemetri / debug için; istersek state override da yapabiliriz.
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
                Console.WriteLine($"[NATIVE] hs_get_fused_state hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Native health snapshot'ını okur.
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
                Console.WriteLine($"[NATIVE] hs_get_health hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Event halkasından bir event çeker, varsa metin olarak döndürür.
        /// Yoksa false döner.
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
                Console.WriteLine($"[NATIVE] hs_pop_event hatası: {ex.Message}");
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // VehicleState ↔ HsFusedState dönüştürücüler
        // ---------------------------------------------------------------------

        private static HsFusedState FromVehicleState(VehicleState state)
        {
            var nowMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var hs = new HsFusedState
            {
                StructSize = (uint)Marshal.SizeOf<HsFusedState>(),
                Version = 1,
                Seq = 0, // C tarafı zaten kendi seq'ini artırıyor

                PosX = state.Position.X,
                PosY = state.Position.Y,
                PosZ = state.Position.Z,

                VelX = state.LinearVelocity.X,
                VelY = state.LinearVelocity.Y,
                VelZ = state.LinearVelocity.Z,

                YawDeg = state.Orientation.YawDeg,
                PitchDeg = state.Orientation.PitchDeg,
                RollDeg = state.Orientation.RollDeg,

                // Hız büyüklüğünü C tarafı da hesaplıyor ama burada da doldurmak sorun değil
                SpeedMps = Math.Sqrt(
                    state.LinearVelocity.X * state.LinearVelocity.X +
                    state.LinearVelocity.Y * state.LinearVelocity.Y +
                    state.LinearVelocity.Z * state.LinearVelocity.Z),

                HasFix = 1,        // C# fiziği state veriyorsa "fix var" kabul ediyoruz
                Quality = 0.9,     // Şimdilik sabit kalite

                Timestamp = nowMs
            };

            return hs;
        }

        private static VehicleState ToVehicleState(HsFusedState hs)
        {
            // Şimdilik sadece pozisyon + yaw'ı kullanıyoruz.
            // İstersek roll/pitch ve hızları da aktarabiliriz.
            var pos = new Vec3(hs.PosX, hs.PosY, hs.PosZ);
            var ori = new Orientation(
                rollDeg: hs.RollDeg,
                pitchDeg: hs.PitchDeg,
                yawDeg: hs.YawDeg);

            // Var olan state'in geri kalan alanlarını doldurmak için minimum bir şablon.
            // Program.cs tarafında bunu ister doğrudan, ister override mantığıyla kullanabilirsin.
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
