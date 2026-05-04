namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        // ---------------------------------------------------------------------
        // GLOBAL EFFORT SCALE
        // ---------------------------------------------------------------------
        private const double GlobalEffortScale = 0.75;

        // ---------------------------------------------------------------------
        // EKSEN KAPASİTELERİ
        // ---------------------------------------------------------------------
        private const double MaxFxN = 24.0;
        private const double MaxFyN = 12.0;
        private const double MaxFzN = 35.0;

        private const double MaxTxNm = 5.0;
        private const double MaxTyNm = 7.0;
        private const double MaxTzNm = 8.0;

        // ---------------------------------------------------------------------
        // MESAFE / HIZ PROFİLİ
        // ---------------------------------------------------------------------
        private const double SlowRadiusM = 4.0;
        private const double StopRadiusM = 0.7;

        private const double CloseEnoughRadiusM = 0.50;
        private const double CloseLinearVelThresh = 0.25;
        private const double CloseAngVelThreshDeg = 5.0;

        private const double CruiseThrottleNorm = 0.62;
        private const double MinApproachThrottleNorm = 0.07;

        private const double BrakeRadiusM = 1.60;
        private const double BrakeSpeedStartMps = 0.45;
        private const double BrakeSpeedFullMps = 1.20;
        private const double MaxReverseThrottleNorm = 0.32;

        private const double NearTurnInPlaceDeg = 95.0;

        // ---------------------------------------------------------------------
        // SCENARIO ARRIVAL PROFILE
        // ---------------------------------------------------------------------
        private const double ScenarioSlowRadiusM = 12.0;
        private const double ScenarioCaptureRadiusM = 1.50;
        private const double ScenarioCoastRadiusM = 2.75;

        private const double ScenarioMaxCaptureSpeedMps = 0.70;
        private const double ScenarioDesiredSpeedFloorMps = 0.18;

        private const double ScenarioEstimatedCoastDecelMps2 = 0.16;

        private const double ScenarioMinThrottleNorm = 0.00;
        private const double ScenarioCreepThrottleNorm = 0.025;
        private const double ScenarioMaxApproachThrottleNorm = 0.42;

        // ---------------------------------------------------------------------
        // HEADING / YAW KONTROL
        // ---------------------------------------------------------------------
        private const double RudderFullAtDeg = 45.0;
        private const double RudderDeadbandDeg = 3.0;
        private const double YawRateDeadbandDeg = 2.0;

        private const double NavYawKp = 1.0 / RudderFullAtDeg;
        private const double NavYawKd = 0.055;

        private const double NearYawBrakeKp = 0.030;
        private const double NearYawBrakeKd = 0.020;

        // ---------------------------------------------------------------------
        // Roll / pitch PD
        // ---------------------------------------------------------------------
        private const double AttKp = 0.035;
        private const double AttKd = 0.020;

        // ---------------------------------------------------------------------
        // Sway damping
        // ---------------------------------------------------------------------
        private const double SwayVelGain = 0.15;
        private const double MaxSwayNorm = 0.9;

        // ---------------------------------------------------------------------
        // Station keeping
        // ---------------------------------------------------------------------
        private const double HoldKp = 0.45;
        private const double HoldKd = 0.30;
        private const double YawKp = 0.018;
        private const double YawKd = 0.012;

        // ---------------------------------------------------------------------
        // Heave PID
        // ---------------------------------------------------------------------
        private const double HeaveKp = 0.25;
        private const double HeaveKd = 0.50;
        private const double HeaveKi = 0.02;
        private const double HeaveImax = 0.2;
        private const double MaxHeaveNorm = 1.0;
    }
}