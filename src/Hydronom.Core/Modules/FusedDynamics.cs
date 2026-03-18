using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Motor komutlarından 6D kuvvet/tork çıktısını (body-frame) hesaplar.
    /// 
    /// _B matrisi:
    ///   [ Fx ]
    ///   [ Fy ]
    ///   [ Fz ] = B * u
    ///   [ Tx ]
    ///   [ Ty ]
    ///   [ Tz ]
    ///
    /// Buradaki çıktı gövde eksenlerindedir (Fb, Tb).
    /// PhysicsIntegrator, bu body-frame wrench'i dünya eksenine döndürüp
    /// VehicleState üzerinde entegrasyon yapar.
    ///
    /// Not:
    ///  - B genellikle thruster geometri + MaxThrustN ölçeklemesinden gelir.
    ///  - u vektörü tipik olarak -1..+1 aralığındaki motor komutlarını temsil eder.
    /// </summary>
    public class FusedDynamics
    {
        private readonly Matrix6xN _B;

        /// <summary>İçte tutulan 6×N matrix (read-only referans).</summary>
        public Matrix6xN B => _B;

        public FusedDynamics(Matrix6xN B)
        {
            _B = B ?? throw new ArgumentNullException(nameof(B));
            if (_B.Rows != 6)
                throw new ArgumentException("FusedDynamics: B matrisi 6 satırlı olmalı (Fx,Fy,Fz,Tx,Ty,Tz).", nameof(B));
        }

        /// <summary>
        /// Motor komutlarından 6D kuvvet/tork hesaplar (body-frame).
        /// 
        /// Beklenen:
        ///  - throttles uzunluğu B ile uyumlu olmalıdır (çağıran taraf sorumluluğu).
        ///  - Her eleman kabaca -1..+1 aralığındadır (gerekirse burada clamp edilir).
        /// </summary>
        public Vec6 ComputeForces(double[] throttles)
        {
            if (throttles is null)
                throw new ArgumentNullException(nameof(throttles));

            // Savunmacı programlama: NaN/Inf ve aralık dışı değerleri bastır.
            var u = new double[throttles.Length];
            for (int i = 0; i < throttles.Length; i++)
            {
                var v = throttles[i];

                if (double.IsNaN(v) || double.IsInfinity(v))
                    v = 0.0;

                // Motor komutlarını -1..+1 aralığında tut
                v = Math.Clamp(v, -1.0, 1.0);

                u[i] = v;
            }

            // B * u → Vec6 (Fx,Fy,Fz,Tx,Ty,Tz) body-frame
            return _B.Multiply(u);
        }
    }
}
