using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// Ham sensör füzyonu çıktısı (FusedFrame) üzerinden anlık çevresel içgörüleri üretir.
    /// Not: FusedFrame hedef, engeller vb. içerir; 6DoF durum bilgisi Decision tarafında VehicleState ile gelir.
    /// </summary>
    public interface IAnalysisModule
    {
        Insights Analyze(FusedFrame frame);
    }

    /// <summary>
    /// Analiz + görev + aracın 6DoF durumu (VehicleState) + dışarıdan ölçülen dt → karar (DecisionCommand).
    /// 
    /// DecisionCommand:
    ///   - Fx, Fy, Fz : gövde ekseninde kuvvet bileşenleri
    ///   - Tx, Ty, Tz : gövde ekseninde tork bileşenleri
    /// 
    /// dt:
    ///   - Kontrol döngüsünün gerçek çevrim süresidir.
    ///   - Runtime tarafından Stopwatch benzeri tekil bir zaman kaynağından ölçülüp verilir.
    ///   - Decision modülü kendi içinde zaman ölçmez; deterministik zaman akışı üst katmandan gelir.
    /// 
    /// Geriye dönük uyum:
    ///   - Throttle01  → Fx
    ///   - RudderNeg1To1 → Tz
    /// Eski planar kullanıcılar hâlâ sadece throttle/rudder set edebilir.
    /// </summary>
    public interface IDecisionModule
    {
        DecisionCommand Decide(Insights insights, TaskDefinition? task, VehicleState state, double dt);
    }

    /// <summary>
    /// Telemetri/diagnostik kaydı.
    /// 6DoF uyumludur: FeedbackRecord.State içinde pos(x,y,z), rpy(roll,pitch,yaw), vel(vx,vy,vz) vb. bulunur.
    /// Ayrıca ForceBody/TorqueBody ile body-frame kuvvet/tork izlenebilir.
    /// </summary>
    public interface IFeedbackRecorder
    {
        void Record(FeedbackRecord record);
    }

    /// <summary>
    /// Alınan DecisionCommand'i düşük seviyeli iticilere/ESC'lere uygular.
    /// 
    /// - Command, 6DoF body-frame wrench (Fx,Fy,Fz,Tx,Ty,Tz) olarak yorumlanabilir.
    /// - Legacy durumda yalnızca Throttle01/RudderNeg1To1 (Fx/Tz) kullanılabilir.
    /// - Uygulama asenkron olabilir; iptal için CancellationToken desteklenir.
    /// </summary>
    public interface IMotorController
    {
        Task ApplyAsync(DecisionCommand command, CancellationToken ct = default);
    }
}