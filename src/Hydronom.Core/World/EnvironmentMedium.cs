namespace Hydronom.Core.World
{
    /// <summary>
    /// Simülasyon dünyasında aracın içinde bulunduğu temel ortam türü.
    /// Bu bilgi fizik, sensör simülasyonu, planlama ve güvenlik katmanları tarafından kullanılabilir.
    /// </summary>
    public enum EnvironmentMedium
    {
        Unknown = 0,

        /// <summary>Hava ortamı. VTOL, uçak, yüzey üstü hareket ve su dışına çıkma durumları için.</summary>
        Air = 1,

        /// <summary>Su ortamı. USV yüzey/sualtı, UUV ve sualtı roketi gibi araçlar için.</summary>
        Water = 2,

        /// <summary>Katı zemin veya temas yüzeyi. Kara, havuz tabanı, deniz tabanı, rampa ve iskele için.</summary>
        Solid = 3,

        /// <summary>Birden fazla ortamın aynı anda etkili olduğu geçiş bölgesi.</summary>
        Mixed = 4
    }
}
