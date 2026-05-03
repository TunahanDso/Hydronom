using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Motor komutlarÄ±ndan 6D kuvvet/tork Ã§Ä±ktÄ±sÄ±nÄ± (body-frame) hesaplar.
    /// 
    /// _B matrisi:
    ///   [ Fx ]
    ///   [ Fy ]
    ///   [ Fz ] = B * u
    ///   [ Tx ]
    ///   [ Ty ]
    ///   [ Tz ]
    ///
    /// Buradaki Ã§Ä±ktÄ± gÃ¶vde eksenlerindedir (Fb, Tb).
    /// PhysicsIntegrator, bu body-frame wrench'i dÃ¼nya eksenine dÃ¶ndÃ¼rÃ¼p
    /// VehicleState Ã¼zerinde entegrasyon yapar.
    ///
    /// Not:
    ///  - B genellikle thruster geometri + MaxThrustN Ã¶lÃ§eklemesinden gelir.
    ///  - u vektÃ¶rÃ¼ tipik olarak -1..+1 aralÄ±ÄŸÄ±ndaki motor komutlarÄ±nÄ± temsil eder.
    /// </summary>
    public class FusedDynamics
    {
        private readonly Matrix6xN _B;

        /// <summary>Ä°Ã§te tutulan 6Ã—N matrix (read-only referans).</summary>
        public Matrix6xN B => _B;

        public FusedDynamics(Matrix6xN B)
        {
            _B = B ?? throw new ArgumentNullException(nameof(B));
            if (_B.Rows != 6)
                throw new ArgumentException("FusedDynamics: B matrisi 6 satÄ±rlÄ± olmalÄ± (Fx,Fy,Fz,Tx,Ty,Tz).", nameof(B));
        }

        /// <summary>
        /// Motor komutlarÄ±ndan 6D kuvvet/tork hesaplar (body-frame).
        /// 
        /// Beklenen:
        ///  - throttles uzunluÄŸu B ile uyumlu olmalÄ±dÄ±r (Ã§aÄŸÄ±ran taraf sorumluluÄŸu).
        ///  - Her eleman kabaca -1..+1 aralÄ±ÄŸÄ±ndadÄ±r (gerekirse burada clamp edilir).
        /// </summary>
        public Vec6 ComputeForces(double[] throttles)
        {
            if (throttles is null)
                throw new ArgumentNullException(nameof(throttles));

            // SavunmacÄ± programlama: NaN/Inf ve aralÄ±k dÄ±ÅŸÄ± deÄŸerleri bastÄ±r.
            var u = new double[throttles.Length];
            for (int i = 0; i < throttles.Length; i++)
            {
                var v = throttles[i];

                if (double.IsNaN(v) || double.IsInfinity(v))
                    v = 0.0;

                // Motor komutlarÄ±nÄ± -1..+1 aralÄ±ÄŸÄ±nda tut
                v = Math.Clamp(v, -1.0, 1.0);

                u[i] = v;
            }

            // B * u â†’ Vec6 (Fx,Fy,Fz,Tx,Ty,Tz) body-frame
            return _B.Multiply(u);
        }
    }
}

