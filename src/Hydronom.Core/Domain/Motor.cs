using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Motor konfigürasyon açıklaması (descriptor).
    /// Id: "FL","FR","RL","RR" gibi etiket.
    /// Channel: fiziksel kanal/indeks (bridge/arduino için).
    /// Direction: 1 veya -1 (ters yönlü motorlar için).
    /// Neutral: nötr değer (ör. servo için ortalama) 0..1 aralığında.
    /// </summary>
    public record MotorDesc(string Id, int Channel = 0, int Direction = 1, double Neutral = 0.0);

    /// <summary>
    /// Runtime içinde kullanılan Motor nesnesi.
    /// - Current: son uygulanmış normalleştirilmiş değer (0..1).
    /// - Set(): güvenli clamp ve atama yapar.
    /// - MapToByte(...): Current'i Arduino/ESC/servo'ya gönderebileceğiniz 0..max aralığına çevirir.
    /// - MapToByteWithDirection(...): Direction bilgisine göre invert uygulayan yardımcı.
    /// 
    /// Not: Bu sınıf sadece küçük yardımcı dönüşümler sağlar. Gerçek donanım
    /// mapping'ini (ör. servo 50 Hz vs. PWM frekansı, ESC trigger vs. PWM hatları)
    /// bridge/arduino tarafında karar verin.
    /// </summary>
    public class Motor
    {
        public string Id { get; }
        public int Channel { get; }
        public int Direction { get; } = 1;
        public double Neutral { get; } = 0.0;

        // Son uygulanan normalleştirilmiş değer (0..1)
        public double Current { get; private set; }

        public Motor(MotorDesc desc)
        {
            if (desc is null) throw new ArgumentNullException(nameof(desc));
            Id = desc.Id ?? throw new ArgumentNullException(nameof(desc.Id));
            Channel = desc.Channel;
            Direction = desc.Direction;
            Neutral = Math.Max(0.0, Math.Min(1.0, desc.Neutral)); // güvenli neutral
            Current = Neutral;
        }

        /// <summary>
        /// Motor değerini güvenli şekilde ayarlar (0..1 aralığı).
        /// NaN kontrolü yapılır. Burada Current(0..1) tutulur — 
        /// donanım mapping'i için MapToByte* metodlarını kullanın.
        /// </summary>
        public void Set(double v)
        {
            var v2 = double.IsNaN(v) ? Neutral : Math.Max(0.0, Math.Min(1.0, v));
            Current = v2;
        }

        /// <summary>
        /// Current (0..1) değerini 0..max aralığına çevirir.
        /// Eğer centered = false ise: 0 -> 0, 1 -> max
        /// Eğer centered = true ise: Current'teki Neutral değeri -> max/2,
        /// Current > Neutral ise üst yarıya, Current < Neutral ise alt yarıya map edilir.
        /// (Servo/ruyja benzeri durumlar için kullanılabilir.)
        /// </summary>
        /// <param name="max">Çıktı aralığının üst limiti (ör. 255).</param>
        /// <param name="centered">Nötr noktasını merkeze taşı (ör. servo/ruyja için).</param>
        /// <returns>0..max aralığında int değer</returns>
        public int MapToByte(int max = 255, bool centered = false)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));

            // Basit 0..1 -> 0..max
            if (!centered)
            {
                var val = (int)Math.Round(Current * max);
                return Math.Max(0, Math.Min(max, val));
            }

            // centered mapping: Neutral -> max/2
            // Current==Neutral => mid, Current==1 => top, Current==0 => bottom
            var mid = max / 2.0;

            // avoid division by zero if Neutral==1 or Neutral==0
            double upperSpan = 1.0 - Neutral;
            double lowerSpan = Neutral - 0.0;

            if (upperSpan <= 1e-9 && lowerSpan <= 1e-9)
            {
                // degenerate: neutral is only value
                return (int)Math.Round(mid);
            }

            double outVal;
            if (Current >= Neutral)
            {
                // map Neutral..1 -> mid..max
                if (upperSpan <= 1e-9) outVal = mid;
                else outVal = mid + (Current - Neutral) / upperSpan * (max - mid);
            }
            else
            {
                // map 0..Neutral -> 0..mid
                if (lowerSpan <= 1e-9) outVal = mid;
                else outVal = (Current / Neutral) * mid; // Current/Neutral in 0..1
            }

            var iv = (int)Math.Round(outVal);
            return Math.Max(0, Math.Min(max, iv));
        }

        /// <summary>
        /// MapToByte sonucunu Direction bilgisine göre invert eder.
        /// - Eğer Direction == 1: çıktıyı olduğu gibi döner.
        /// - Eğer Direction == -1: invert uygulanır (centered ise terslemeyi merkeze göre yapar).
        /// 
        /// Bu fonksiyon, motor fiziksel olarak ters takıldıysa yazılım tarafında düzeltme amaçlıdır.
        /// </summary>
        public int MapToByteWithDirection(int max = 255, bool centered = false)
        {
            var b = MapToByte(max, centered);

            if (Direction >= 0) return b;

            // invert: centered veya non-centered diferansiyel invert uygula
            if (!centered)
            {
                // non-centered: 0..max -> invert around midpoint of range -> max - b
                var inv = max - b;
                return Math.Max(0, Math.Min(max, inv));
            }
            else
            {
                // centered: invert around mid
                var mid = max / 2.0;
                var inv = (int)Math.Round(mid - (b - mid));
                return Math.Max(0, Math.Min(max, inv));
            }
        }

        /// <summary>
        /// Kısa açıklama: MapToByte / MapToByteWithDirection metodlarını kullanarak
        /// bridge/arduino'ya gönderilecek integer sinyal elde edebilirsiniz.
        /// Örnek:
        ///   var pwm = motor.MapToByteWithDirection(255, centered:false);
        ///   ser.WriteLine($"SET:{motor.Channel},{pwm}");
        /// 
        /// Not: Gerçek ESC/servo gereksinimleri farklı olabilir (ör. PWM frekansı,
        /// servo pulse width). Bu metodlar yalnızca 0..max integer mapping sağlar.
        /// </summary>
    }
}

