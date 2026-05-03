using System.Collections.Generic;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Ã‡evresel veriler:
    /// - AkÄ±ntÄ± alanÄ± (2D yatay bileÅŸen)
    /// - Engeller (2D projeksiyon)
    /// - Kablo hattÄ± / rota vb.
    ///
    /// Not:
    ///   - Bu arayÃ¼z, 6DoF mimaride bile "planar" (XY) Ã§evre bilgisini temsil eder.
    ///   - Su altÄ± / hava araÃ§larÄ±nda, bu bilgiler genelde yÃ¼zey/harita dÃ¼zlemine
    ///     projeksiyon olarak kullanÄ±lÄ±r; dikey yapÄ± (Z) ayrÄ± modellerle ele alÄ±nÄ±r.
    ///
    /// Unity adaptÃ¶rÃ¼ doldurabilir; gerÃ§ek dÃ¼nyada Ã§oÄŸu zaman boÅŸ/unknown kalabilir.
    /// </summary>
    public interface IEnvironment
    {
        /// <summary>
        /// Verilen 2D konumdaki su akÄ±ÅŸ vektÃ¶rÃ¼ (m/s, XY dÃ¼zleminde).
        /// GerÃ§ek dÃ¼nyada akÄ±ntÄ± bilgisi yoksa (0,0) dÃ¶nebilir.
        /// 
        /// 6DoF baÄŸlam:
        ///   - Bu vektÃ¶r, body/world frame dÃ¶nÃ¼ÅŸÃ¼mleriyle 3D dinamiÄŸe
        ///     dahil edilebilir; burada sadece yatay bileÅŸen taÅŸÄ±nÄ±r.
        /// </summary>
        Vec2 FlowAt(Vec2 position);

        /// <summary>
        /// Åu an bilinen engellerin 2D projeksiyon listesi.
        /// - Position: dÃ¼nya XY dÃ¼zleminde merkez.
        /// - RadiusM : gÃ¼venlik yarÄ±Ã§apÄ± (m).
        ///
        /// Not:
        ///   - Su altÄ± / hava aracÄ± iÃ§in bu, tipik olarak bir "plan view"
        ///     (Ã¼stten gÃ¶rÃ¼nÃ¼m) engel haritasÄ±dÄ±r.
        ///   - Z boyutu (yÃ¼kseklik/derinlik) farklÄ± bir modelde tutulabilir.
        /// </summary>
        IReadOnlyList<Obstacle> Obstacles { get; }

        /// <summary>
        /// Kablo hattÄ±, referans rota veya saha iÃ§i sabit bir geometri varsa,
        /// Ã¶rneklenmiÅŸ 2D noktalar halinde temsil edilir.
        ///
        /// Ã–rn:
        ///   - ROV iÃ§in deniz altÄ± kablosu gÃ¼zergahÄ±
        ///   - YÃ¼zey aracÄ± iÃ§in sahil hattÄ± / iskele kenarÄ±
        ///
        /// GerÃ§ek sistemde Ã§oÄŸu zaman boÅŸ (0 uzunlukta liste) olabilir.
        /// </summary>
        IReadOnlyList<Vec2> CablePath { get; }
    }
}

