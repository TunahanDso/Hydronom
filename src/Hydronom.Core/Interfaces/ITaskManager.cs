using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Görev yönetimi arabirimi.
    /// </summary>
    public interface ITaskManager
    {
        /// <summary>Aktif görev (yoksa null).</summary>
        TaskDefinition? CurrentTask { get; }

        /// <summary>Yeni görevi ayarla.</summary>
        void SetTask(TaskDefinition task);

        /// <summary>
        /// Analiz çıktısı ve (varsa) araç durumu ile görev durumunu güncelle.
        /// Örn: hedefe varış kontrolü (state gerekir).
        /// </summary>
        void Update(Insights insights, VehicleState? state = null);

        /// <summary>Aktif görevi temizle (Stop vb.).</summary>
        void ClearTask();
    }
}
