namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Basit ama gÃ¼venli 6xN motor etki matrisi (B).
    /// Her satÄ±r bir ekseni temsil eder: Fx, Fy, Fz, Tx, Ty, Tz.
    /// </summary>
    public class Matrix6xN
    {
        private readonly double[,] _data;

        public int Rows => 6;
        public int Cols => _data.GetLength(1);

        public Matrix6xN(double[,] data)
        {
            if (data.GetLength(0) != 6)
                throw new ArgumentException("Matrix6xN: ilk boyut 6 olmalÄ±dÄ±r.");

            _data = data;
        }

        /// <summary>
        /// B * u â†’ Vec6 dÃ¶ndÃ¼rÃ¼r.
        /// </summary>
        public Vec6 Multiply(double[] u)
        {
            if (u == null) throw new ArgumentNullException(nameof(u));
            if (u.Length != Cols)
                throw new ArgumentException($"Beklenen vektÃ¶r boyutu: {Cols}, gelen: {u.Length}");

            double[] r = new double[6];

            for (int i = 0; i < 6; i++)
                for (int j = 0; j < Cols; j++)
                    r[i] += _data[i, j] * u[j];

            return new Vec6(r);
        }
    }

    /// <summary>
    /// 6 boyutlu immutable vektÃ¶r: (Fx, Fy, Fz, Tx, Ty, Tz).
    /// </summary>
    public readonly struct Vec6
    {
        private readonly double[] _v; // her zaman uzunluk 6

        public double this[int i] => _v[i];

        public double Fx => _v[0];
        public double Fy => _v[1];
        public double Fz => _v[2];
        public double Tx => _v[3];
        public double Ty => _v[4];
        public double Tz => _v[5];

        public Vec6(double[] v)
        {
            if (v == null)
                throw new ArgumentNullException(nameof(v));

            if (v.Length != 6)
                throw new ArgumentException("Vec6: dizi uzunluÄŸu 6 olmalÄ±dÄ±r.");

            // Derin kopya â†’ immutable davranÄ±ÅŸ
            _v = (double[])v.Clone();
        }

        public Vec6(double fx, double fy, double fz, double tx, double ty, double tz)
        {
            _v = new double[6] { fx, fy, fz, tx, ty, tz };
        }

        public override string ToString()
            => $"Fx={Fx:F3}, Fy={Fy:F3}, Fz={Fz:F3}, Tx={Tx:F3}, Ty={Ty:F3}, Tz={Tz:F3}";
    }
}

