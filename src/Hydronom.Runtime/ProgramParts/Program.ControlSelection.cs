using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Modules;

partial class Program
{
    /// <summary>
    /// Runtime döngüsünde kontrol modunu seçer ve desired command üretir.
    ///
    /// Karar sırası:
    /// 1. Emergency stop
    /// 2. Manual mode
    /// 3. Auto mode
    /// 4. Disarmed fallback
    ///
    /// Bu metot sadece "istenen komutu" üretir.
    /// SafetyLimiter ve ActuatorManager bu komuttan sonra çalışır.
    /// </summary>
    private static ControlSelectionResult SelectControlCommand(
        CommandServer cmdSrv,
        IDecisionModule decision,
        ITaskManager tasks,
        Insights insights,
        VehicleState state,
        double dtMeasured,
        bool invertRudder,
        bool estopTaskCleared,
        int heartbeatTimeoutMs,
        int manualCommandTimeoutMs,
        ManualControlLimits manualLimits)
    {
        var nowUtc = DateTime.UtcNow;
        var heartbeatAgeMs = (nowUtc - cmdSrv.LastHeartbeatUtc).TotalMilliseconds;
        var manualAgeMs = (nowUtc - cmdSrv.LastManualCommandUtc).TotalMilliseconds;

        bool heartbeatFresh = heartbeatAgeMs <= heartbeatTimeoutMs;
        bool manualFresh = manualAgeMs <= manualCommandTimeoutMs;

        DecisionCommand cmdDesired;
        string controlMode;
        AdvancedDecisionReport decisionReport = AdvancedDecisionReport.Empty;

        if (cmdSrv.IsEmergencyStop)
        {
            if (!estopTaskCleared)
            {
                tasks.ClearTask();
                estopTaskCleared = true;
            }

            cmdDesired = DecisionCommand.Zero;
            controlMode = "ESTOP";
            decisionReport = AdvancedDecisionReport.Empty with { Reason = "ESTOP" };
        }
        else if (cmdSrv.IsManualMode)
        {
            estopTaskCleared = false;

            if (cmdSrv.IsArmed && heartbeatFresh && manualFresh)
            {
                var md = cmdSrv.CurrentManualDrive;

                cmdDesired = new DecisionCommand(
                    fx: md.Surge * manualLimits.MaxFxN,
                    fy: md.Sway * manualLimits.MaxFyN,
                    fz: md.Heave * manualLimits.MaxFzN,
                    tx: md.Roll * manualLimits.MaxTxNm,
                    ty: md.Pitch * manualLimits.MaxTyNm,
                    tz: md.Yaw * manualLimits.MaxTzNm
                );

                controlMode = "MANUAL";
                decisionReport = AdvancedDecisionReport.Empty with { Reason = "MANUAL_DIRECT" };
            }
            else
            {
                cmdDesired = DecisionCommand.Zero;
                controlMode = cmdSrv.IsArmed ? "MANUAL-HOLD" : "DISARMED";
                decisionReport = AdvancedDecisionReport.Empty with { Reason = controlMode };
            }
        }
        else
        {
            estopTaskCleared = false;

            tasks.Update(insights, state);

            if (cmdSrv.IsArmed)
            {
                cmdDesired = decision.Decide(insights, tasks.CurrentTask, state, dtMeasured);
                controlMode = "AUTO";

                if (decision is AdvancedDecision advancedDecision)
                    decisionReport = advancedDecision.LastDecisionReport;
            }
            else
            {
                cmdDesired = DecisionCommand.Zero;
                controlMode = "DISARMED";
                decisionReport = AdvancedDecisionReport.Empty with { Reason = "DISARMED" };
            }
        }

        if (invertRudder)
            cmdDesired = cmdDesired with { Tz = -cmdDesired.Tz };

        return new ControlSelectionResult(
            DesiredCommand: cmdDesired,
            ControlMode: controlMode,
            DecisionReport: decisionReport,
            EstopTaskCleared: estopTaskCleared
        );
    }

    /// <summary>
    /// Manual control eksen limitleri.
    /// </summary>
    private readonly record struct ManualControlLimits(
        double MaxFxN,
        double MaxFyN,
        double MaxFzN,
        double MaxTxNm,
        double MaxTyNm,
        double MaxTzNm
    );

    /// <summary>
    /// Manual control limitlerini config'ten okur.
    /// </summary>
    private static ManualControlLimits ReadManualControlLimits(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        return new ManualControlLimits(
            MaxFxN: ReadDouble(config, "Control:Manual:MaxFxN", 24.0),
            MaxFyN: ReadDouble(config, "Control:Manual:MaxFyN", 12.0),
            MaxFzN: ReadDouble(config, "Control:Manual:MaxFzN", 35.0),
            MaxTxNm: ReadDouble(config, "Control:Manual:MaxTxNm", 5.0),
            MaxTyNm: ReadDouble(config, "Control:Manual:MaxTyNm", 7.0),
            MaxTzNm: ReadDouble(config, "Control:Manual:MaxTzNm", 8.0)
        );
    }
}