namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planner'ın hangi operasyonel modda çalıştığını belirtir.
    /// Bu değer hem planner davranışını hem de Ops açıklamalarını etkiler.
    /// </summary>
    public enum PlanningMode
    {
        Idle = 0,

        /// <summary>
        /// Açık ortamda hedefe güvenli ve verimli ilerleme.
        /// </summary>
        Navigate = 1,

        /// <summary>
        /// Dar geçit, duba arası, koridor, gate veya slalom benzeri görev.
        /// </summary>
        Corridor = 2,

        /// <summary>
        /// Engel veya risk nedeniyle lokal kaçınma davranışı.
        /// </summary>
        Avoidance = 3,

        /// <summary>
        /// Hedef çevresinde yavaşlama, hizalanma ve capture fazı.
        /// </summary>
        Arrival = 4,

        /// <summary>
        /// Konum veya heading koruma.
        /// </summary>
        Hold = 5,

        /// <summary>
        /// Güvenlik nedeniyle plan üretimini durdurma veya sıfır hareket planı.
        /// </summary>
        SafetyStop = 6
    }
}