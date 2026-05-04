using System;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// ActuatorManager allocation / control effectiveness / solver bölümü.
    ///
    /// Bu partial dosya şunlardan sorumludur:
    /// - Thruster geometrisinden 6xM B matrisi üretmek
    /// - Sağlıklı thruster maskesine göre solver cache oluşturmak
    /// - Ridge least-squares ile istenen wrench için thruster çözümü üretmek
    /// - CanReverse=false thruster'lar için negatif komutu çözüm aşamasında engellemek
    /// - Control authority profilini hesaplamak
    /// - Hedef/gerçek wrench farkını raporlamak
    ///
    /// Frame sözleşmesi:
    /// - B matrisi body frame'dedir.
    /// - RequestedForceBody ve RequestedTorqueBody body frame'dedir.
    /// - Thruster ForceDir ve Position body frame'dedir.
    /// </summary>
    public sealed partial class ActuatorManager
    {
        /// <summary>
        /// DecisionCommand içinden 6 elemanlı hedef wrench vektörü üretir.
        ///
        /// Sıra:
        /// [Fx, Fy, Fz, Tx, Ty, Tz]
        ///
        /// TorqueWeight yalnızca solver hedefinde moment eksenlerinin sayısal ağırlığını
        /// değiştirmek için kullanılır. Rapor tarafında gerçek istenen tork korunur.
        /// </summary>
        private double[] BuildRequestedWrenchVector(DecisionCommand cmd)
        {
            return new double[6]
            {
                SanitizeScalar(cmd.Fx),
                SanitizeScalar(cmd.Fy),
                SanitizeScalar(cmd.Fz),
                SanitizeScalar(cmd.Tx) * TorqueWeight,
                SanitizeScalar(cmd.Ty) * TorqueWeight,
                SanitizeScalar(cmd.Tz) * TorqueWeight
            };
        }

        /// <summary>
        /// DecisionCommand için açıklanabilir allocation işlemi yapar.
        ///
        /// Bu metot thruster Current değerlerini doğrudan değiştirmez.
        /// Sadece solver çıktısı ve rapor üretir.
        ///
        /// v2:
        /// - Solver artık tek yönlü thruster'ların negatif çözüm almasına izin vermez.
        /// - Unconstrained ridge çözümden sonra active-set benzeri bir düzeltme yapılır.
        /// - CanReverse=false ve çözüm negatifse ilgili thruster 0'a sabitlenir,
        ///   kalan aktif thruster'larla hedef wrench yeniden çözülür.
        /// - Apply tarafındaki reverse clamp artık yalnızca safety-net olarak kalır.
        /// </summary>
        private AllocationSolveResult SolveAllocation(DecisionCommand cmd)
        {
            var requestedForceBody = new Vec3(
                SanitizeScalar(cmd.Fx),
                SanitizeScalar(cmd.Fy),
                SanitizeScalar(cmd.Fz)
            );

            var requestedTorqueBody = new Vec3(
                SanitizeScalar(cmd.Tx),
                SanitizeScalar(cmd.Ty),
                SanitizeScalar(cmd.Tz)
            );

            double[] requestedWrench = BuildRequestedWrenchVector(cmd);

            SolverCache solver;
            bool[] canReverse;
            bool[] healthy;

            lock (_stateLock)
            {
                solver = _solverCache;
                canReverse = _thrusters.Select(t => t.CanReverse).ToArray();
                healthy = _thrusters.Select(t => t.IsHealthy).ToArray();
            }

            double[] raw = SolveWithCapabilityBounds(
                solver,
                requestedWrench,
                canReverse,
                healthy
            );

            if (raw.Length == 0 && _thrusters.Count > 0)
                raw = new double[_thrusters.Count];

            return new AllocationSolveResult(
                RawSolution: raw,
                RequestedForceBody: requestedForceBody,
                RequestedTorqueBody: requestedTorqueBody,
                SolverWasEmpty: solver.IsEmpty
            );
        }

        /// <summary>
        /// Uygulanan thruster çıkışlarından gerçek body-frame wrench hesaplar.
        /// </summary>
        private (Vec3 forceBody, Vec3 torqueBody) ComputeAchievedWrench_NoLock()
        {
            Vec3 totalFBody = Vec3.Zero;
            Vec3 totalTBody = Vec3.Zero;

            foreach (var t in _thrusters)
            {
                if (!t.IsHealthy)
                    continue;

                Vec3 force = t.ForceDir * (t.Current * MaxThrustN);
                Vec3 torque = Vec3.Cross(t.Position, force);

                totalFBody += force;
                totalTBody += torque;
            }

            return (totalFBody, totalTBody);
        }

        /// <summary>
        /// Allocation raporu üretir.
        /// Hedef wrench ile uygulanmış thruster current'larından hesaplanan gerçek wrench'i karşılaştırır.
        /// </summary>
        private ActuatorAllocationReport BuildAllocationReport_NoLock(
            AllocationSolveResult solve,
            Vec3 achievedForceBody,
            Vec3 achievedTorqueBody,
            bool hadSaturation,
            int activeThrusterCount,
            double saturationRatio,
            int reverseClampCount = 0)
        {
            int healthyCount = _thrusters.Count(t => t.IsHealthy);
            bool hadUnhealthy = healthyCount < _thrusters.Count;

            Vec3 forceError = solve.RequestedForceBody - achievedForceBody;
            Vec3 torqueError = solve.RequestedTorqueBody - achievedTorqueBody;

            double requestedNorm = WrenchNorm(solve.RequestedForceBody, solve.RequestedTorqueBody);
            double errorNorm = WrenchNorm(forceError, torqueError);

            double normalizedError = requestedNorm <= 1e-9
                ? errorNorm
                : errorNorm / requestedNorm;

            bool hadReverseClamp = reverseClampCount > 0;

            bool authorityLimited =
                solve.SolverWasEmpty ||
                healthyCount == 0 ||
                hadSaturation ||
                hadReverseClamp ||
                normalizedError > 0.25;

            string reason = BuildAllocationReason(
                solve.SolverWasEmpty,
                healthyCount,
                hadSaturation,
                normalizedError,
                reverseClampCount
            );

            return new ActuatorAllocationReport(
                Success: !solve.SolverWasEmpty && healthyCount > 0,
                Reason: reason,
                RequestedForceBody: solve.RequestedForceBody,
                RequestedTorqueBody: solve.RequestedTorqueBody,
                AchievedForceBody: achievedForceBody,
                AchievedTorqueBody: achievedTorqueBody,
                ForceErrorBody: forceError,
                TorqueErrorBody: torqueError,
                NormalizedError: normalizedError,
                SaturationRatio: saturationRatio,
                ActiveThrusterCount: activeThrusterCount,
                HealthyThrusterCount: healthyCount,
                HadSaturation: hadSaturation,
                HadUnhealthyThruster: hadUnhealthy,
                AuthorityLimited: authorityLimited,
                ReverseClampCount: reverseClampCount
            );
        }

        /// <summary>
        /// Geometriye bağlı sabit 6xM B matrisi üretir.
        ///
        /// Her kolon bir thruster'ın normalize +1 komutunda üreteceği body-frame wrench'tir.
        /// CanReverse=false olan thruster için kolon yine +1 yönünü temsil eder.
        /// Ancak negatif komut artık yalnızca Apply aşamasında değil, solver aşamasında da engellenir.
        /// </summary>
        private double[,] BuildThrusterMatrixFromGeometry()
        {
            int m = _thrusters.Count;
            double[,] b = new double[6, m];

            for (int j = 0; j < m; j++)
            {
                var t = _thrusters[j];

                Vec3 dir = t.ForceDir.Normalize();
                Vec3 r = t.Position;
                Vec3 torquePerUnit = Vec3.Cross(r, dir);

                b[0, j] = dir.X * MaxThrustN;
                b[1, j] = dir.Y * MaxThrustN;
                b[2, j] = dir.Z * MaxThrustN;

                b[3, j] = torquePerUnit.X * MaxThrustN;
                b[4, j] = torquePerUnit.Y * MaxThrustN;
                b[5, j] = torquePerUnit.Z * MaxThrustN;
            }

            return b;
        }

        /// <summary>
        /// Sağlık durumuna göre effective B matrisi ve Ridge LS cache'i üretir.
        ///
        /// Not:
        /// CanReverse=false sınırı cache seviyesinde B kolonunu silmez.
        /// Çünkü tek yönlü thruster pozitif yönde hâlâ otorite sağlar.
        /// Negatif komut sınırı SolveWithCapabilityBounds içinde active-set mantığıyla uygulanır.
        /// </summary>
        private void RebuildSolverCache_NoLockRequired()
        {
            lock (_stateLock)
            {
                int rows = _baseB.GetLength(0);
                int cols = _baseB.GetLength(1);

                if (rows == 0 || cols == 0)
                {
                    _solverCache = SolverCache.Empty;
                    return;
                }

                double[,] bEff = new double[rows, cols];

                for (int j = 0; j < cols; j++)
                {
                    double gain = _thrusters[j].IsHealthy ? 1.0 : 0.0;

                    for (int i = 0; i < rows; i++)
                        bEff[i, j] = _baseB[i, j] * gain;
                }

                double[] colScale = ComputeColumnScales(bEff);
                double[,] bs = ScaleColumns(bEff, colScale);

                double[,] a = BuildRegularizedNormalMatrix(bs, SolverLambda);
                double[,] aInv = InvertMatrix(a);

                _solverCache = new SolverCache(
                    B: bEff,
                    Bs: bs,
                    ColScale: colScale,
                    AInv: aInv,
                    ActiveMask: _thrusters.Select(t => t.IsHealthy).ToArray()
                );
            }
        }

        /// <summary>
        /// Mevcut geometri ve sağlıklı thruster'lara göre eksen otoritesi hesaplar.
        ///
        /// CanReverse=true:
        ///   +1 komut kolon yönünde, -1 komut kolonun ters yönünde otorite sağlar.
        ///
        /// CanReverse=false:
        ///   yalnızca +1 komut yönü geçerli kabul edilir.
        ///   Böylece tek yönlü ESC kullanılan araçlarda negatif eksen otoritesi yanlış şişirilmez.
        /// </summary>
        private void RecomputeAuthorityProfile_NoLockRequired()
        {
            lock (_stateLock)
            {
                if (_thrusters.Count == 0)
                {
                    _authorityProfile = ControlAuthorityProfile.Empty;
                    return;
                }

                int rows = _baseB.GetLength(0);
                int cols = _baseB.GetLength(1);

                AxisAuthority[] axes = new AxisAuthority[rows];

                for (int i = 0; i < rows; i++)
                {
                    double pos = 0.0;
                    double neg = 0.0;

                    for (int j = 0; j < cols; j++)
                    {
                        var thruster = _thrusters[j];

                        if (!thruster.IsHealthy)
                            continue;

                        double v = _baseB[i, j];

                        if (Math.Abs(v) <= 1e-12)
                            continue;

                        if (v > 0.0)
                        {
                            pos += v;

                            if (thruster.CanReverse)
                                neg += v;
                        }
                        else
                        {
                            neg += -v;

                            if (thruster.CanReverse)
                                pos += -v;
                        }
                    }

                    axes[i] = new AxisAuthority(pos, neg);
                }

                _authorityProfile = new ControlAuthorityProfile(
                    Fx: axes[0],
                    Fy: axes[1],
                    Fz: axes[2],
                    Tx: axes[3],
                    Ty: axes[4],
                    Tz: axes[5]
                );
            }
        }

        /// <summary>
        /// CanReverse-aware allocation solver.
        ///
        /// Akış:
        /// 1. Sağlıklı thruster maskesiyle ridge LS çözümü alınır.
        /// 2. CanReverse=false thruster'lardan negatif çıkanlar 0'a sabitlenir.
        /// 3. Kalan aktif thruster'larla hedef tekrar çözülür.
        /// 4. Bu işlem negatif tek yönlü ihlal kalmayana kadar veya iterasyon limiti dolana kadar sürer.
        /// 5. Son çözüm fiziksel sınırlarla sanitize edilir.
        ///
        /// Bu tam QP/NNLS değildir; ancak eski "çöz sonra clamp et" davranışından çok daha doğrudur.
        /// Amaç reverse clamp'i normal operasyonda neredeyse sıfıra indirmektir.
        /// </summary>
        private static double[] SolveWithCapabilityBounds(
            SolverCache cache,
            double[] targetWrench,
            bool[] canReverse,
            bool[] healthy)
        {
            if (cache.IsEmpty)
                return Array.Empty<double>();

            int cols = cache.B.GetLength(1);

            if (cols <= 0)
                return Array.Empty<double>();

            bool[] active = new bool[cols];

            for (int j = 0; j < cols; j++)
            {
                bool cacheActive = j < cache.ActiveMask.Length && cache.ActiveMask[j];
                bool isHealthy = j < healthy.Length && healthy[j];

                active[j] = cacheActive && isHealthy;
            }

            double[] solution = SolveWithMask(cache.B, targetWrench, active);

            int maxIterations = Math.Max(1, cols + 1);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool changed = false;

                for (int j = 0; j < cols; j++)
                {
                    bool supportsReverse = j < canReverse.Length && canReverse[j];

                    if (!active[j])
                        continue;

                    if (supportsReverse)
                        continue;

                    if (j < solution.Length && solution[j] < -1e-7)
                    {
                        /*
                         * Tek yönlü thruster negatif çözüm istiyor.
                         * Bu thruster'ı bu allocation için 0'a sabitleyip
                         * kalan thruster'larla hedefi tekrar çözüyoruz.
                         */
                        active[j] = false;
                        changed = true;
                    }
                }

                if (!changed)
                    break;

                solution = SolveWithMask(cache.B, targetWrench, active);
            }

            if (solution.Length != cols)
                Array.Resize(ref solution, cols);

            for (int j = 0; j < cols; j++)
            {
                bool supportsReverse = j < canReverse.Length && canReverse[j];

                if (!active[j])
                {
                    solution[j] = 0.0;
                    continue;
                }

                if (!double.IsFinite(solution[j]))
                    solution[j] = 0.0;

                solution[j] = supportsReverse
                    ? Math.Clamp(solution[j], -1.0, 1.0)
                    : Math.Clamp(solution[j], 0.0, 1.0);
            }

            return solution;
        }

        /// <summary>
        /// Verilen active mask için ridge LS çözümü üretir.
        /// Pasif kolonlar B içinde sıfırlanır; sonuç dizisinde de 0 kalır.
        /// </summary>
        private static double[] SolveWithMask(
            double[,] baseB,
            double[] targetWrench,
            bool[] activeMask)
        {
            int rows = baseB.GetLength(0);
            int cols = baseB.GetLength(1);

            if (rows == 0 || cols == 0)
                return Array.Empty<double>();

            double[,] bEff = new double[rows, cols];

            for (int j = 0; j < cols; j++)
            {
                double gain = j < activeMask.Length && activeMask[j] ? 1.0 : 0.0;

                for (int i = 0; i < rows; i++)
                    bEff[i, j] = baseB[i, j] * gain;
            }

            double[] colScale = ComputeColumnScales(bEff);
            double[,] bs = ScaleColumns(bEff, colScale);

            double[,] a = BuildRegularizedNormalMatrix(bs, SolverLambda);
            double[,] aInv;

            try
            {
                aInv = InvertMatrix(a);
            }
            catch
            {
                return new double[cols];
            }

            double[] solution = SolveWithCache(
                new SolverCache(
                    B: bEff,
                    Bs: bs,
                    ColScale: colScale,
                    AInv: aInv,
                    ActiveMask: activeMask.ToArray()
                ),
                targetWrench
            );

            if (solution.Length != cols)
                Array.Resize(ref solution, cols);

            for (int j = 0; j < cols; j++)
            {
                if (j >= activeMask.Length || !activeMask[j])
                    solution[j] = 0.0;
            }

            return solution;
        }

        /// <summary>
        /// Ridge least-squares çözümü.
        ///
        /// Kullanılan form:
        /// u = S^-1 * (Bs^T Bs + λI)^-1 * Bs^T * target
        ///
        /// Bu fonksiyon tek başına unconstrained çözüm üretir.
        /// CanReverse / one-way sınırları SolveWithCapabilityBounds içinde uygulanır.
        /// </summary>
        private static double[] SolveWithCache(SolverCache cache, double[] targetWrench)
        {
            if (cache.IsEmpty)
                return Array.Empty<double>();

            int rows = cache.Bs.GetLength(0);
            int cols = cache.Bs.GetLength(1);

            double[] b = new double[cols];

            for (int i = 0; i < cols; i++)
            {
                double sum = 0.0;

                for (int k = 0; k < rows; k++)
                    sum += cache.Bs[k, i] * targetWrench[k];

                b[i] = sum;
            }

            double[] w = Multiply(cache.AInv, b);
            double[] u = new double[cols];

            for (int j = 0; j < cols; j++)
            {
                double scale = cache.ColScale[j];

                if (Math.Abs(scale) < 1e-12)
                    scale = 1.0;

                u[j] = w[j] / scale;

                if (!cache.ActiveMask[j])
                    u[j] = 0.0;

                if (!double.IsFinite(u[j]))
                    u[j] = 0.0;
            }

            return u;
        }

        private static double[] ComputeColumnScales(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            double[] colScale = new double[cols];
            const double minNorm = 1e-6;

            for (int j = 0; j < cols; j++)
            {
                double norm = 0.0;

                for (int i = 0; i < rows; i++)
                {
                    double v = matrix[i, j];
                    norm += v * v;
                }

                norm = Math.Sqrt(norm);

                if (!double.IsFinite(norm) || norm < minNorm)
                    norm = 1.0;

                colScale[j] = norm;
            }

            return colScale;
        }

        private static double[,] ScaleColumns(double[,] matrix, double[] colScale)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            double[,] scaled = new double[rows, cols];

            for (int j = 0; j < cols; j++)
            {
                double s = Math.Abs(colScale[j]) < 1e-12 ? 1.0 : colScale[j];

                for (int i = 0; i < rows; i++)
                    scaled[i, j] = matrix[i, j] / s;
            }

            return scaled;
        }

        private static double[,] BuildRegularizedNormalMatrix(double[,] bs, double lambda)
        {
            int rows = bs.GetLength(0);
            int cols = bs.GetLength(1);

            double[,] a = new double[cols, cols];

            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double sum = 0.0;

                    for (int k = 0; k < rows; k++)
                        sum += bs[k, i] * bs[k, j];

                    if (i == j)
                        sum += Math.Max(0.0, lambda);

                    a[i, j] = sum;
                }
            }

            return a;
        }

        private static double[] Multiply(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            if (cols != vector.Length)
                throw new InvalidOperationException("Matrix/vector boyutu uyumsuz.");

            double[] result = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double sum = 0.0;

                for (int j = 0; j < cols; j++)
                    sum += matrix[i, j] * vector[j];

                result[i] = sum;
            }

            return result;
        }

        private static double[,] InvertMatrix(double[,] matrix)
        {
            int n = matrix.GetLength(0);

            if (n != matrix.GetLength(1))
                throw new InvalidOperationException("Sadece kare matris terslenebilir.");

            double[,] a = new double[n, n * 2];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    a[i, j] = matrix[i, j];

                a[i, n + i] = 1.0;
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double max = Math.Abs(a[col, col]);

                for (int row = col + 1; row < n; row++)
                {
                    double v = Math.Abs(a[row, col]);

                    if (v > max)
                    {
                        max = v;
                        pivot = row;
                    }
                }

                if (max < 1e-12)
                    throw new InvalidOperationException("Matris terslenemiyor; geometri veya lambda kontrol edilmeli.");

                if (pivot != col)
                {
                    for (int j = 0; j < n * 2; j++)
                    {
                        double tmp = a[col, j];
                        a[col, j] = a[pivot, j];
                        a[pivot, j] = tmp;
                    }
                }

                double diag = a[col, col];

                for (int j = 0; j < n * 2; j++)
                    a[col, j] /= diag;

                for (int row = 0; row < n; row++)
                {
                    if (row == col)
                        continue;

                    double factor = a[row, col];

                    if (Math.Abs(factor) < 1e-15)
                        continue;

                    for (int j = 0; j < n * 2; j++)
                        a[row, j] -= factor * a[col, j];
                }
            }

            double[,] inv = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    inv[i, j] = a[i, n + j];
            }

            return inv;
        }

        private static string BuildAllocationReason(
            bool solverWasEmpty,
            int healthyCount,
            bool hadSaturation,
            double normalizedError,
            int reverseClampCount = 0)
        {
            if (solverWasEmpty)
                return "SOLVER_EMPTY";

            if (healthyCount <= 0)
                return "NO_HEALTHY_THRUSTERS";

            if (reverseClampCount > 0 && normalizedError > 0.25)
                return "REVERSE_CLAMP_HIGH_ERROR";

            if (reverseClampCount > 0)
                return "REVERSE_CLAMPED";

            if (hadSaturation && normalizedError > 0.25)
                return "SATURATED_HIGH_ERROR";

            if (hadSaturation)
                return "SATURATED";

            if (normalizedError > 0.50)
                return "HIGH_WRENCH_ERROR";

            if (normalizedError > 0.25)
                return "MEDIUM_WRENCH_ERROR";

            return "OK";
        }

        private static double WrenchNorm(Vec3 force, Vec3 torque)
        {
            double f =
                force.X * force.X +
                force.Y * force.Y +
                force.Z * force.Z;

            double t =
                torque.X * torque.X +
                torque.Y * torque.Y +
                torque.Z * torque.Z;

            double s = f + t;

            if (s <= 0.0 || !double.IsFinite(s))
                return 0.0;

            return Math.Sqrt(s);
        }

        private static double SanitizeScalar(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private readonly record struct AllocationSolveResult(
            double[] RawSolution,
            Vec3 RequestedForceBody,
            Vec3 RequestedTorqueBody,
            bool SolverWasEmpty
        );
    }
}