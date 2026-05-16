// Hydronom MCU - Application State
//
// MİMARİ NOT:
// Bu state Pico'nun yerel gerçekliğidir.
// Burada mission/world/route yoktur.
// Sadece MCU güvenlik durumu, actuator son komutları ve diagnostics için gerekli durum vardır.
//
// Pico şu sorulara cevap verir:
// - ARM mı, DISARM mı?
// - Failsafe aktif mi?
// - Son komut kaçıncı sequence idi?
// - Actuator çıkışları güvenli sınırlar içinde mi?
// - Komut tek yönlü ESC için ters yönde mi geldi?
//
// Pico şu işleri yapmaz:
// - Rota planlama
// - Görev yönetimi
// - World model işleme
// - 6DoF karar üretimi

use crate::config::actuator_profile::MAX_ACTUATORS;
use crate::config::defaults::{COMMAND_TIMEOUT_MS, DEFAULT_VEHICLE_PROFILE, HEARTBEAT_TIMEOUT_MS};
use crate::config::vehicle_profile::VehicleProfile;
use crate::protocol::commands::{ActuatorSetpoint, HydronomCommand};
use crate::protocol::telemetry::{
    ActuatorTelemetry, McuTelemetry, EMPTY_ACTUATOR_TELEMETRY,
};
use crate::safety::arming::{ArmError, ArmState, ArmingController};
use crate::safety::failsafe::{FailsafeController, FailsafeReason};
use crate::safety::limits::{limit_normalized_command, normalized_to_pwm_us};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CommandApplyResult {
    Ok,
    Pong,
    Armed,
    Disarmed,
    EmergencyStopped,
    EmergencyCleared,
    RejectedNotArmed,
    RejectedEmergencyStop,
    RejectedInvalidActuator,
}

#[derive(Clone, Copy, Debug)]
pub struct ActuatorRuntimeState {
    pub actuator_id: u8,
    pub requested: i16,
    pub limited: i16,
    pub pwm_us: u16,
    pub reverse_clamped: bool,
    pub range_clamped: bool,
}

impl ActuatorRuntimeState {
    pub const fn safe(id: u8, pwm_us: u16) -> Self {
        Self {
            actuator_id: id,
            requested: 0,
            limited: 0,
            pwm_us,
            reverse_clamped: false,
            range_clamped: false,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct AppState {
    pub vehicle_profile: VehicleProfile,
    pub arming: ArmingController,
    pub failsafe: FailsafeController,
    pub last_seq: u32,
    pub actuators: [ActuatorRuntimeState; MAX_ACTUATORS],
}

impl AppState {
    pub const fn new() -> Self {
        Self {
            vehicle_profile: DEFAULT_VEHICLE_PROFILE,
            arming: ArmingController::new(),
            failsafe: FailsafeController::new(COMMAND_TIMEOUT_MS, HEARTBEAT_TIMEOUT_MS),
            last_seq: 0,
            actuators: [
                ActuatorRuntimeState::safe(0, 1000),
                ActuatorRuntimeState::safe(1, 1000),
                ActuatorRuntimeState::safe(2, 1000),
                ActuatorRuntimeState::safe(3, 1000),
                ActuatorRuntimeState::safe(4, 1000),
                ActuatorRuntimeState::safe(5, 1000),
                ActuatorRuntimeState::safe(6, 1000),
                ActuatorRuntimeState::safe(7, 1000),
            ],
        }
    }

    pub fn tick(&mut self, now_ms: u64) {
        self.failsafe.tick(now_ms);

        if self.failsafe.active() || !self.arming.is_armed() {
            self.force_safe_outputs();
        }
    }

    pub fn apply_command(&mut self, command: HydronomCommand, now_ms: u64) -> CommandApplyResult {
        if let Some(seq) = command.seq() {
            self.last_seq = seq;
        }

        match command {
            HydronomCommand::Ping => CommandApplyResult::Pong,

            HydronomCommand::Heartbeat { .. } => {
                self.failsafe.mark_heartbeat(now_ms);
                CommandApplyResult::Ok
            }

            HydronomCommand::Arm { .. } => match self.arming.arm() {
                Ok(()) => {
                    self.failsafe.mark_command(now_ms);
                    self.force_safe_outputs();
                    CommandApplyResult::Armed
                }
                Err(ArmError::EmergencyStopLatched) => CommandApplyResult::RejectedEmergencyStop,
            },

            HydronomCommand::Disarm { .. } => {
                self.arming.disarm();
                self.failsafe.clear();
                self.force_safe_outputs();
                CommandApplyResult::Disarmed
            }

            HydronomCommand::EmergencyStop { .. } => {
                self.arming.emergency_stop();
                self.failsafe.trigger(FailsafeReason::EmergencyStop);
                self.force_safe_outputs();
                CommandApplyResult::EmergencyStopped
            }

            HydronomCommand::ClearEmergencyStop { .. } => {
                self.arming.clear_emergency_stop();
                self.failsafe.clear();
                self.force_safe_outputs();
                CommandApplyResult::EmergencyCleared
            }

            HydronomCommand::ActuatorBatch {
                count, setpoints, ..
            } => {
                if !self.arming.is_armed() {
                    self.force_safe_outputs();
                    return CommandApplyResult::RejectedNotArmed;
                }

                if self.arming.state() == ArmState::EmergencyStopped {
                    self.force_safe_outputs();
                    return CommandApplyResult::RejectedEmergencyStop;
                }

                self.failsafe.mark_command(now_ms);

                for setpoint in setpoints.iter().take(count) {
                    if !self.apply_setpoint(*setpoint) {
                        self.failsafe.trigger(FailsafeReason::InvalidCommand);
                        self.force_safe_outputs();
                        return CommandApplyResult::RejectedInvalidActuator;
                    }
                }

                CommandApplyResult::Ok
            }
        }
    }

    pub fn force_safe_outputs_public(&mut self) {
        self.force_safe_outputs();
    }

    fn apply_setpoint(&mut self, setpoint: ActuatorSetpoint) -> bool {
        let Some(profile) = self.vehicle_profile.actuator_bank.get(setpoint.actuator_id) else {
            return false;
        };

        if !profile.enabled {
            return false;
        }

        let limited = limit_normalized_command(profile, setpoint.value);
        let pwm_us = normalized_to_pwm_us(profile, limited.limited);

        let index = setpoint.actuator_id as usize;
        if index >= MAX_ACTUATORS {
            return false;
        }

        self.actuators[index] = ActuatorRuntimeState {
            actuator_id: setpoint.actuator_id,
            requested: limited.requested,
            limited: limited.limited,
            pwm_us,
            reverse_clamped: limited.reverse_clamped,
            range_clamped: limited.range_clamped,
        };

        true
    }

    fn force_safe_outputs(&mut self) {
        for index in 0..MAX_ACTUATORS {
            if let Some(profile) = self.vehicle_profile.actuator_bank.get(index as u8) {
                self.actuators[index] = ActuatorRuntimeState::safe(index as u8, profile.pwm.safe_us);
            }
        }
    }

    pub fn telemetry(&self, now_ms: u64) -> McuTelemetry {
        let mut actuator_telemetry = [EMPTY_ACTUATOR_TELEMETRY; MAX_ACTUATORS];

        for index in 0..MAX_ACTUATORS {
            let state = self.actuators[index];

            actuator_telemetry[index] = ActuatorTelemetry {
                actuator_id: state.actuator_id,
                requested: state.requested,
                limited: state.limited,
                pwm_us: state.pwm_us,
                reverse_clamped: state.reverse_clamped,
                range_clamped: state.range_clamped,
            };
        }

        McuTelemetry {
            firmware_uptime_ms: now_ms,
            last_seq: self.last_seq,
            arm_state: self.arming.state(),
            failsafe_active: self.failsafe.active(),
            failsafe_reason: self.failsafe.reason(),
            actuator_count: self.vehicle_profile.actuator_bank.enabled_count(),
            actuators: actuator_telemetry,
        }
    }
}

impl Default for AppState {
    fn default() -> Self {
        Self::new()
    }
}