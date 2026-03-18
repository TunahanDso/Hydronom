// File: Hydronom.Core/Bridges/PcaBridge.cs

using System;
using System.Device.I2c;
using System.Threading;
using System.Collections.Generic;

namespace Hydronom.Core.Bridges
{
    /// <summary>
    /// PCA9685 16-kanallı PWM sürücü köprüsü (I2C).
    /// Endüstriyel özellikler: Failsafe (Auto-Stop), osilatör kalibrasyonu, I2C bus tarama.
    /// Geliştirme ortamında donanım yoksa otomatik olarak simülasyon moduna düşer.
    /// </summary>
    public class PcaBridge : IDisposable
    {
        // --- REGISTER MAP (PCA9685) ---
        private const byte MODE1 = 0x00;
        private const byte MODE2 = 0x01;
        private const byte PRESCALE = 0xFE;
        
        // Kanal başına 4 register (ON_L, ON_H, OFF_L, OFF_H)
        private const byte LED0_ON_L = 0x06; 
        private const byte ALL_LED_ON_L = 0xFA;

        private const int CHANNEL_COUNT = 16;

        // MODE1 Bitleri
        private const byte MODE1_RESTART = 0x80;
        private const byte MODE1_SLEEP   = 0x10;
        private const byte MODE1_AI      = 0x20; // Auto-Increment

        // --- YAPILANDIRMA ---
        private readonly int _busId;
        private int _address; // Tarama sonucu değişebilir
        private bool _simulation;
        
        private I2cDevice? _device;
        private bool _disposed;
        
        private double _frequency = 50.0; 
        private readonly object _lock = new();

        // Ucuz modüllerdeki kristal hatasını düzeltmek için katsayı (örn. 1.05)
        public double OscillatorCorrection { get; set; } = 1.0;

        // Durum takibi (Replay/Log için)
        private readonly ushort[] _lastValues = new ushort[CHANNEL_COUNT];

        // ESC/Servo sınırları (Konfigüre edilebilir)
        public double MinPulseUs { get; set; } = 1000;
        public double MaxPulseUs { get; set; } = 2000;

        /// <summary>
        /// Dışarıdan bridge’in sim modda olup olmadığını görebilmek için.
        /// </summary>
        public bool IsSimulation => _simulation;

        /// <summary>
        /// Donanımdaki toplam kanal sayısı (PCA9685 -> 16).
        /// </summary>
        public int ChannelCount => CHANNEL_COUNT;

        /// <summary>
        /// Yeni bir PCA9685 sürücüsü oluşturur.
        /// </summary>
        /// <param name="busId">I2C veri yolu ID (Raspberry Pi için genelde 1)</param>
        /// <param name="address">Hedef adres (0x40 varsayılan). -1 verilirse tarama yapar.</param>
        /// <param name="simulation">True ise donanıma hiç bağlanmadan simülasyon modunda çalışır.</param>
        public PcaBridge(int busId = 1, int address = 0x40, bool simulation = false)
        {
            _busId = busId;
            _address = address;
            _simulation = simulation;

            if (_simulation)
            {
                Console.WriteLine($"[PCA] Simulation Mode Started (Virtual Bus {_busId})");
                return;
            }

            try
            {
                // Eğer adres belirsizse (-1) veya belirtilen adreste yoksa tara
                if (_address == -1 || !PingAddress(_address))
                {
                    Console.WriteLine($"[PCA] Target address 0x{_address:X2} not responding. Scanning bus...");
                    int found = ScanForPca9685();
                    if (found != -1)
                    {
                        _address = found;
                        Console.WriteLine($"[PCA] Auto-detected PCA9685 at 0x{_address:X2}");
                    }
                    else
                    {
                        throw new Exception("No PCA9685 device found on I2C bus.");
                    }
                }

                var settings = new I2cConnectionSettings(_busId, _address);
                _device = I2cDevice.Create(settings);
                
                InitializeHardware();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PCA] Hardware Init Failed: {ex.Message}. Switching to Simulation.");
                _simulation = true; // Fallback to simulation to prevent crash
            }
        }

        private void InitializeHardware()
        {
            // 1. Reset
            WriteRegister(MODE1, MODE1_AI); 
            // 2. Frekansı ayarla (Varsayılan 50Hz)
            SetPwmFrequency(_frequency);
            // 3. Başlangıçta tüm motorları sustur (Safety)
            ResetAll();
        }

        /// <summary>
        /// Belirtilen adrese ping atar (basit okuma denemesi).
        /// </summary>
        private bool PingAddress(int addr)
        {
            try
            {
                using var dev = I2cDevice.Create(new I2cConnectionSettings(_busId, addr));
                dev.ReadByte(); // Hata vermezse cihaz vardır
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// I2C hattını tarayarak PCA9685 olabilecek adresleri (0x40-0x7F) arar.
        /// </summary>
        private int ScanForPca9685()
        {
            // PCA9685 standart aralığı: 0x40 - 0x7F
            for (int addr = 0x40; addr < 0x80; addr++)
            {
                if (PingAddress(addr)) return addr;
            }
            return -1;
        }

        public void SetPwmFrequency(double hz)
        {
            if (hz < 1 || hz > 1500) throw new ArgumentOutOfRangeException(nameof(hz));

            if (_simulation)
            {
                _frequency = hz;
                Console.WriteLine($"[PCA] [SIM] Freq set to {hz:F1} Hz");
                return;
            }

            lock (_lock)
            {
                _frequency = hz;
                // Formül: prescale = round(osc_clock / (4096 * rate)) - 1
                // OscillatorCorrection: Ucuz modüllerdeki kristal sapmasını düzeltir.
                double oscClock = 25_000_000.0 * OscillatorCorrection;
                var prescaleVal = (oscClock / (4096.0 * hz)) - 1.0;
                byte prescale = (byte)Math.Round(prescaleVal);

                // Frekans değiştirmek için SLEEP modu gerekir
                byte oldMode = ReadRegister(MODE1);
                byte newMode = (byte)((oldMode & 0x7F) | MODE1_SLEEP); // Sleep bit 1 yap

                WriteRegister(MODE1, newMode);        // Uyu
                WriteRegister(PRESCALE, prescale);    // Hızı ayarla
                WriteRegister(MODE1, oldMode);        // Uyan
                
                Thread.Sleep(5); // Osilatörün stabilize olması için bekle
                
                WriteRegister(MODE1, (byte)(oldMode | MODE1_RESTART)); // Restart
                
                Console.WriteLine($"[PCA] Freq set to {hz:F1} Hz (Pre={prescale}, Corr={OscillatorCorrection:F2})");
            }
        }

        /// <summary>
        /// Belirli bir kanala Duty Cycle (0.0 - 1.0) uygular.
        /// </summary>
        public void SetDuty(int channel, double duty)
        {
            if (channel < 0 || channel >= CHANNEL_COUNT) return;

            duty = Math.Clamp(duty, 0.0, 1.0);
            
            // 12-bit çözünürlük (0..4095)
            // ON zamanını 0'da sabitliyoruz, sadece OFF zamanını kaydırıyoruz.
            ushort on = 0; 
            ushort off = (ushort)Math.Round(duty * 4095.0);

            // Eğer tam 0 veya tam 1 ise özel durum (Register 12. bit)
            if (duty == 0.0) { on = 0; off = 4096; } // Tamamen kapalı
            else if (duty == 1.0) { on = 4096; off = 0; } // Tamamen açık

            // Cache kontrolü (I2C trafiğini azaltmak için)
            if (_lastValues[channel] == off && duty != 0 && duty != 1) return;

            if (_simulation)
            {
                _lastValues[channel] = off;
                // Çok sık log basmamak için sadece değişimde veya debug modda basılabilir
                // Console.WriteLine($"[PCA] [SIM] CH{channel} -> {duty:P1}");
                return;
            }

            lock (_lock)
            {
                // 4 byte'lık blok yazma (Auto-Increment sayesinde tek seferde)
                // Register: LEDn_ON_L (0x06 + 4*ch)
                byte baseReg = (byte)(LED0_ON_L + (4 * channel));
                Span<byte> data = stackalloc byte[5];
                
                data[0] = baseReg;
                data[1] = (byte)(on & 0xFF);
                data[2] = (byte)(on >> 8);
                data[3] = (byte)(off & 0xFF);
                data[4] = (byte)(off >> 8);

                try
                {
                    _device?.Write(data);
                    _lastValues[channel] = off;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PCA] I2C Write Error CH{channel}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ESC/Servo için mikrosaniye cinsinden darbe genişliği ayarlar.
        /// </summary>
        public void SetPulseUs(int channel, double us)
        {
            // Periyot süresi (µs) = 1_000_000 / Freq
            double periodUs = 1_000_000.0 / _frequency;
            double duty = us / periodUs;
            SetDuty(channel, duty);
        }

        /// <summary>
        /// ACİL DURDURMA: Tüm kanalları 0 (kapalı) konumuna getirir.
        /// </summary>
        public void ResetAll()
        {
            if (_simulation)
            {
                Array.Clear(_lastValues, 0, _lastValues.Length);
                Console.WriteLine("[PCA] [SIM] All channels reset.");
                return;
            }

            lock (_lock)
            {
                // ALL_LED registerlarını kullanarak tek seferde tüm kanalları kapat
                // ON=0, OFF=4096 (Tamamen Kapalı biti)
                Span<byte> data = stackalloc byte[5];
                data[0] = ALL_LED_ON_L;
                data[1] = 0;
                data[2] = 0;
                data[3] = 0x00;
                data[4] = 0x10; // Bit 4 (Full OFF) set edilir

                try
                {
                    _device?.Write(data);
                    Array.Fill(_lastValues, (ushort)4096);
                    Console.WriteLine("[PCA] Hardware Failsafe Triggered (All Stop)");
                }
                catch
                {
                    // best-effort
                }
            }
        }

        private void WriteRegister(byte reg, byte value)
        {
            if (_simulation) return;
            Span<byte> data = stackalloc byte[2] { reg, value };
            _device?.Write(data);
        }

        private byte ReadRegister(byte reg)
        {
            if (_simulation) return 0;
            _device?.WriteByte(reg);
            return _device?.ReadByte() ?? 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // KRİTİK: Nesne yok edilirken donanımı güvenli moda al.
            try
            {
                ResetAll(); 
            }
            catch
            {
                // ignore
            }

            _device?.Dispose();
            _device = null;
            Console.WriteLine("[PCA] Bridge Disposed.");
        }
    }
}
