using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    public enum TaskPhase { None, Active, Arrived, Aborted }

    public class SimpleTaskManager : ITaskManager
    {
        public TaskDefinition? CurrentTask { get; private set; }
        public TaskPhase Phase { get; private set; } = TaskPhase.None;

        private readonly double _arriveThresholdM;
        private bool _holdOnArrive = false; // goto hold → true, normal goto → false

        public SimpleTaskManager(double arriveThresholdM = 0.75)
        {
            _arriveThresholdM = Math.Max(0, arriveThresholdM);
        }

        public void SetTask(TaskDefinition task)
        {
            CurrentTask = task;
            Phase = TaskPhase.Active;
            _holdOnArrive = InferHoldFromName(task?.Name);
        }

        // İmza ITaskManager ile aynı: VehicleState opsiyonel
        public void Update(Insights insights, VehicleState? state = null)
        {
            if (CurrentTask is null)
            {
                Phase = TaskPhase.None;
                return;
            }

            // Sadece GoTo çeşitleri için varış kontrolü
            // **[KRİTİK DÜZELTME]**: Target'ın Vec3? olduğunu varsayarak 3D mesafeyi hesapla.
            // Bu, Vec3'e yükseltilen TaskDefinition ile uyumluluğu sağlar.
            if (IsGoTo(CurrentTask) && CurrentTask.Target is Vec3 target3d && state is VehicleState s)
            {
                // Vec3 hedefi mevcutsa, 3D mesafe hesaplanır.
                var dx = target3d.X - s.Position.X;
                var dy = target3d.Y - s.Position.Y;
                var dz = target3d.Z - s.Position.Z; // Z mesafesi dahil edildi

                var dist3D = Math.Sqrt(dx * dx + dy * dy + dz * dz); // 3D mesafesi
                
                if (dist3D <= _arriveThresholdM)
                {
                    if (_holdOnArrive)
                    {
                        // goto hold → görevi tut, fazı "Arrived" yap
                        Phase = TaskPhase.Arrived;
                        return;
                    }
                    else
                    {
                        // normal goto → varışta görevi temizle
                        ClearTask();
                        return;
                    }
                }

                Phase = TaskPhase.Active;
            }
            // Eski TaskDefinition'dan (Vec2 Target) gelen verileri işlemek için artık bu bloka gerek yoktur,
            // çünkü TaskDefinition.Target'ı Vec3? olarak güncelledik. 
            // Eğer sistem hala Vec2 hedefi alsaydı, bu hedef Target'a Vec3(X, Y, 0) olarak atanmalıydı.
            // Bu nedenle, hata veren eski Vec2 desen eşleştirmesi KALDIRILDI.
            /*
            else if (IsGoTo(CurrentTask) && CurrentTask.Target is Vec2 target2d && state is VehicleState s2)
            {
                // ... (Bu blok kaldırılmıştır)
            }
            */
            else
            {
                // Diğer görev tipleri veya hedef türü GoTo'ya uygun değil: default aktif
                Phase = TaskPhase.Active;
            }

            // TODO: zaman aşımı, iptal şartları, engel kaynaklı bekleme vb.
        }

        public void ClearTask()
        {
            CurrentTask = null;
            Phase = TaskPhase.None;
            _holdOnArrive = false;
        }

        // --- Yardımcılar ---

        private static bool IsGoTo(TaskDefinition task)
        {
            var n = (task?.Name ?? string.Empty).Trim().ToLowerInvariant();
            // ControlApp’ten gelebilecek yaygın biçimler:
            // "goto", "go to", "gotopoint", "go to point", "gotohold", "goto hold", "gotopoint hold", vb.
            return n.Contains("goto") || n.Contains("go to") || n.Contains("gotopoint");
        }

        private static bool InferHoldFromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.Trim().ToLowerInvariant();
            // "goto hold" / "gotohold" / "gotopoint hold" vb. yakala
            return n.Contains("hold");
        }
    }
}
