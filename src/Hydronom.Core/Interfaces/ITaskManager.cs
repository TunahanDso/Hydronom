using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Modules;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Temel görev yönetimi arabirimi.
    ///
    /// Bu interface bilinçli olarak küçük tutulur:
    /// - Aktif görevi bilir.
    /// - Görev set eder.
    /// - Runtime tick içinde görev durumunu günceller.
    /// - Aktif görevi temizler.
    ///
    /// Gelişmiş mission orchestration için IMissionOrchestrator kullanılmalıdır.
    /// </summary>
    public interface ITaskManager
    {
        /// <summary>Aktif görev; yoksa null.</summary>
        TaskDefinition? CurrentTask { get; }

        /// <summary>Aktif görev fazı.</summary>
        TaskPhase Phase { get; }

        /// <summary>Yeni primary görevi ayarla.</summary>
        void SetTask(TaskDefinition task);

        /// <summary>
        /// Analiz çıktısı ve varsa araç durumu ile görev durumunu güncelle.
        ///
        /// Bu metot:
        /// - Görev progression durumunu günceller.
        /// - Arrival / timeout / no-progress gibi task-level durumları izler.
        ///
        /// Bu metot:
        /// - Heading üretmez.
        /// - Force / torque üretmez.
        /// - Planner yerine rota seçmez.
        /// - Controller hedefi üretmez.
        /// </summary>
        void Update(Insights insights, VehicleState? state = null);

        /// <summary>Aktif görevi temizle.</summary>
        void ClearTask();
    }

    /// <summary>
    /// Gelişmiş mission orchestration arabirimi.
    ///
    /// Bu interface AdvancedTaskManager'ın yeni sorumluluğunu temsil eder:
    /// - Primary task
    /// - Sequential queue
    /// - Generated subtask
    /// - Suspended primary
    /// - Parallel task
    /// - Guard task
    /// - Route-free task raporlama
    ///
    /// Bu interface de sürüş üretmez.
    /// Heading / force / trajectory / obstacle-bypass Planner + Trajectory + Controller hattının işidir.
    /// </summary>
    public interface IMissionOrchestrator : ITaskManager
    {
        /// <summary>Son açıklanabilir görev raporu.</summary>
        AdvancedTaskReport LastReport { get; }

        /// <summary>Son mission orchestration snapshot'ı.</summary>
        AdvancedTaskOrchestrationSnapshot LastOrchestration { get; }

        /// <summary>Son status / reason metni.</summary>
        string? LastStatusReason { get; }

        /// <summary>Aktif primary görevle beraber izlenen paralel görevler.</summary>
        IReadOnlyList<MissionTaskSlot> ParallelTasks { get; }

        /// <summary>Aktif guard görevleri.</summary>
        IReadOnlyList<MissionTaskSlot> GuardTasks { get; }

        /// <summary>Sıralı primary görev kuyruğunda bekleyen görev sayısı.</summary>
        int PendingSequentialTaskCount { get; }

        /// <summary>Bekleyen generated subtask sayısı.</summary>
        int PendingGeneratedSubtaskCount { get; }

        /// <summary>Generated subtask nedeniyle askıya alınmış primary görev sayısı.</summary>
        int SuspendedPrimaryTaskCount { get; }

        /// <summary>
        /// Görevi sıralı primary kuyruğa ekler.
        /// Aktif görev yoksa implementasyon görevi hemen başlatabilir.
        /// </summary>
        void EnqueueTask(TaskDefinition task);

        /// <summary>Birden fazla görevi sıralı primary kuyruğa ekler.</summary>
        void EnqueueTasks(IEnumerable<TaskDefinition> tasks);

        /// <summary>
        /// Primary görevle aynı anda izlenecek paralel görev ekler.
        /// Bu görev aracı sürmez; mission orchestration içinde takip edilir.
        /// </summary>
        MissionTaskSlot AddParallelTask(
            TaskDefinition task,
            string reason = "PARALLEL_TASK_ADDED");

        /// <summary>
        /// Güvenlik, haberleşme, enerji veya görev geçerliliği izleyicisi ekler.
        /// Guard görevleri doğrudan sürüş kararı vermez.
        /// </summary>
        MissionTaskSlot AddGuardTask(
            TaskDefinition task,
            string reason = "GUARD_TASK_ADDED");

        /// <summary>
        /// Sistem tarafından üretilen ara görev ekler.
        ///
        /// startImmediately=false:
        /// - Ara görev bekleyen generated subtask kuyruğuna alınır.
        ///
        /// startImmediately=true:
        /// - Aktif primary görev askıya alınabilir.
        /// - Ara görev hemen primary olarak başlatılabilir.
        ///
        /// Bu metot rota/heading/force üretmez.
        /// </summary>
        MissionTaskSlot GenerateSubtask(
            TaskDefinition task,
            bool startImmediately = false,
            string reason = "GENERATED_SUBTASK_ADDED");

        /// <summary>
        /// Bekleyen sıralı primary görev kuyruğunu temizler.
        /// Aktif görev etkilenmez.
        /// </summary>
        void ClearQueue();

        /// <summary>
        /// Bekleyen generated subtask kuyruğunu temizler.
        /// Aktif görev etkilenmez.
        /// </summary>
        void ClearGeneratedSubtasks();

        /// <summary>
        /// Askıya alınmış primary görevleri temizler.
        /// Aktif görev etkilenmez.
        /// </summary>
        void ClearSuspendedPrimaryTasks();

        /// <summary>
        /// Paralel görevleri temizler.
        /// Aktif primary görev etkilenmez.
        /// </summary>
        void ClearParallelTasks();

        /// <summary>
        /// Guard görevlerini temizler.
        /// Aktif primary görev etkilenmez.
        /// </summary>
        void ClearGuardTasks();

        /// <summary>
        /// Tüm mission orchestration durumunu temizler.
        /// Active primary, sequential queue, generated subtasks, suspended primary,
        /// parallel tasks ve guard tasks sıfırlanır.
        /// </summary>
        void ClearMission();
    }
}