// Hydronom MCU - Runtime
//
// Bu dosya ana runtime kabuğunu taşır.
// AppState, protocol parser, diagnostics counters ve health değerlendirmesi burada birleşir.
//
// MİMARİ NOT:
// Pico tarafında bile ham gelen byte doğrudan actuator katmanına gitmez.
// Akış şu şekildedir:
//
// raw frame bytes
//   -> protocol frame decode
//   -> HydronomCommand
//   -> safety/state validation
//   -> actuator runtime state
//   -> telemetry/health/diagnostics
//
// Böylece C# Runtime çökerse, USB hattı bozulursa veya frame hatalı gelirse
// motor çıkışı güvenli tarafta kalır.

use crate::app::state::{AppState, CommandApplyResult};
use crate::diagnostics::counters::DiagnosticsCounters;
use crate::diagnostics::health::McuHealth;
use crate::protocol::frame::FrameError;
use crate::protocol::parser::{parse_frame, ParseError};
use crate::safety::failsafe::FailsafeReason;

pub struct HydronomMcuRuntime {
    pub state: AppState,
    pub counters: DiagnosticsCounters,
}

impl HydronomMcuRuntime {
    pub const fn new() -> Self {
        Self {
            state: AppState::new(),
            counters: DiagnosticsCounters::new(),
        }
    }

    pub fn tick(&mut self, now_ms: u64) {
        let was_failsafe_active = self.state.failsafe.active();

        self.state.tick(now_ms);

        if !was_failsafe_active && self.state.failsafe.active() {
            self.counters.mark_failsafe_event();
        }
    }

    pub fn apply_command_result(&mut self, result: CommandApplyResult) {
        match result {
            CommandApplyResult::Ok
            | CommandApplyResult::Pong
            | CommandApplyResult::Armed
            | CommandApplyResult::Disarmed
            | CommandApplyResult::EmergencyCleared => {
                self.counters.mark_applied_command();
            }

            CommandApplyResult::EmergencyStopped => {
                self.counters.mark_applied_command();
                self.counters.mark_emergency_stop_event();
            }

            CommandApplyResult::RejectedNotArmed
            | CommandApplyResult::RejectedEmergencyStop
            | CommandApplyResult::RejectedInvalidActuator => {
                self.counters.mark_rejected_command();
            }
        }
    }

    pub fn process_frame_bytes(&mut self, bytes: &[u8], now_ms: u64) -> CommandApplyResult {
        self.counters.mark_received_frame();

        match parse_frame(bytes) {
            Ok(command) => {
                self.counters.mark_parsed_command();

                let result = self.state.apply_command(command, now_ms);
                self.apply_command_result(result);
                self.collect_actuator_clamp_counters();

                result
            }

            Err(error) => {
                self.mark_parse_error(error);
                self.state.failsafe.trigger(FailsafeReason::ProtocolError);
                self.state.force_safe_outputs_public();

                CommandApplyResult::RejectedInvalidActuator
            }
        }
    }

    pub fn health(&self) -> McuHealth {
        McuHealth::evaluate(
            self.state.arming.state(),
            self.state.failsafe.active(),
            self.state.failsafe.reason(),
            self.counters,
        )
    }

    fn mark_parse_error(&mut self, error: ParseError) {
        match error {
            ParseError::Frame(FrameError::CrcMismatch) => {
                self.counters.mark_crc_error();
            }

            ParseError::Frame(_) => {
                self.counters.mark_protocol_error();
            }

            ParseError::BadPayloadLength
            | ParseError::TooManyActuators
            | ParseError::UnknownFrameType => {
                self.counters.mark_protocol_error();
            }
        }
    }

    fn collect_actuator_clamp_counters(&mut self) {
        for actuator in self.state.actuators {
            if actuator.reverse_clamped {
                self.counters.mark_reverse_clamp_event();
            }

            if actuator.range_clamped {
                self.counters.mark_range_clamp_event();
            }
        }
    }
}