// Hydronom MCU - Health
//
// MİMARİ NOT:
// Health modeli C# Runtime / Gateway / Ops tarafına taşınabilir olmalı.
// Ama burada heap/string kullanmıyoruz; no_std uyumlu sade enum ve sayılarla ilerliyoruz.

use crate::diagnostics::counters::DiagnosticsCounters;
use crate::safety::arming::ArmState;
use crate::safety::failsafe::FailsafeReason;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum McuHealthLevel {
    Nominal,
    Warning,
    Critical,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum McuHealthCode {
    Ok,
    Disarmed,
    FailsafeActive,
    EmergencyStopped,
    ProtocolErrors,
    CrcErrors,
}

#[derive(Clone, Copy, Debug)]
pub struct McuHealth {
    pub level: McuHealthLevel,
    pub code: McuHealthCode,
    pub crc_errors: u32,
    pub protocol_errors: u32,
    pub failsafe_reason: FailsafeReason,
}

impl McuHealth {
    pub fn evaluate(
        arm_state: ArmState,
        failsafe_active: bool,
        failsafe_reason: FailsafeReason,
        counters: DiagnosticsCounters,
    ) -> Self {
        if arm_state == ArmState::EmergencyStopped {
            return Self {
                level: McuHealthLevel::Critical,
                code: McuHealthCode::EmergencyStopped,
                crc_errors: counters.crc_errors,
                protocol_errors: counters.protocol_errors,
                failsafe_reason,
            };
        }

        if failsafe_active {
            return Self {
                level: McuHealthLevel::Critical,
                code: McuHealthCode::FailsafeActive,
                crc_errors: counters.crc_errors,
                protocol_errors: counters.protocol_errors,
                failsafe_reason,
            };
        }

        if counters.crc_errors > 0 {
            return Self {
                level: McuHealthLevel::Warning,
                code: McuHealthCode::CrcErrors,
                crc_errors: counters.crc_errors,
                protocol_errors: counters.protocol_errors,
                failsafe_reason,
            };
        }

        if counters.protocol_errors > 0 {
            return Self {
                level: McuHealthLevel::Warning,
                code: McuHealthCode::ProtocolErrors,
                crc_errors: counters.crc_errors,
                protocol_errors: counters.protocol_errors,
                failsafe_reason,
            };
        }

        if arm_state == ArmState::Disarmed {
            return Self {
                level: McuHealthLevel::Warning,
                code: McuHealthCode::Disarmed,
                crc_errors: counters.crc_errors,
                protocol_errors: counters.protocol_errors,
                failsafe_reason,
            };
        }

        Self {
            level: McuHealthLevel::Nominal,
            code: McuHealthCode::Ok,
            crc_errors: counters.crc_errors,
            protocol_errors: counters.protocol_errors,
            failsafe_reason,
        }
    }
}