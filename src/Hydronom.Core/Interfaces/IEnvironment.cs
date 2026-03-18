using System.Collections.Generic;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Çevresel veriler:
    /// - Akıntı alanı (2D yatay bileşen)
    /// - Engeller (2D projeksiyon)
    /// - Kablo hattı / rota vb.
    ///
    /// Not:
    ///   - Bu arayüz, 6DoF mimaride bile "planar" (XY) çevre bilgisini temsil eder.
    ///   - Su altı / hava araçlarında, bu bilgiler genelde yüzey/harita düzlemine
    ///     projeksiyon olarak kullanılır; dikey yapı (Z) ayrı modellerle ele alınır.
    ///
    /// Unity adaptörü doldurabilir; gerçek dünyada çoğu zaman boş/unknown kalabilir.
    /// </summary>
    public interface IEnvironment
    {
        /// <summary>
        /// Verilen 2D konumdaki su akış vektörü (m/s, XY düzleminde).
        /// Gerçek dünyada akıntı bilgisi yoksa (0,0) dönebilir.
        /// 
        /// 6DoF bağlam:
        ///   - Bu vektör, body/world frame dönüşümleriyle 3D dinamiğe
        ///     dahil edilebilir; burada sadece yatay bileşen taşınır.
        /// </summary>
        Vec2 FlowAt(Vec2 position);

        /// <summary>
        /// Şu an bilinen engellerin 2D projeksiyon listesi.
        /// - Position: dünya XY düzleminde merkez.
        /// - RadiusM : güvenlik yarıçapı (m).
        ///
        /// Not:
        ///   - Su altı / hava aracı için bu, tipik olarak bir "plan view"
        ///     (üstten görünüm) engel haritasıdır.
        ///   - Z boyutu (yükseklik/derinlik) farklı bir modelde tutulabilir.
        /// </summary>
        IReadOnlyList<Obstacle> Obstacles { get; }

        /// <summary>
        /// Kablo hattı, referans rota veya saha içi sabit bir geometri varsa,
        /// örneklenmiş 2D noktalar halinde temsil edilir.
        ///
        /// Örn:
        ///   - ROV için deniz altı kablosu güzergahı
        ///   - Yüzey aracı için sahil hattı / iskele kenarı
        ///
        /// Gerçek sistemde çoğu zaman boş (0 uzunlukta liste) olabilir.
        /// </summary>
        IReadOnlyList<Vec2> CablePath { get; }
    }
}
