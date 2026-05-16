// Hydronom MCU - Arming State Machine
//
// GÜVENLİK:
// MCU açıldığında her zaman DISARMED başlar.
// ARM komutu gelmeden hiçbir actuator aktif hareket üretemez.
//
// Emergency stop kilidi geldiyse, sistem manuel clear olmadan tekrar ARM olmaz.

#[derive(Clone, Copy, Debug, defmt::Format, PartialEq, Eq)]
pub enum ArmState {
    Disarmed,
    Armed,
    EmergencyStopped,
}

#[derive(Clone, Copy, Debug, defmt::Format, PartialEq, Eq)]
pub enum ArmError {
    EmergencyStopLatched,
}

#[derive(Clone, Copy, Debug)]
pub struct ArmingController {
    state: ArmState,
}

impl ArmingController {
    pub const fn new() -> Self {
        Self {
            state: ArmState::Disarmed,
        }
    }

    pub fn state(&self) -> ArmState {
        self.state
    }

    pub fn is_armed(&self) -> bool {
        self.state == ArmState::Armed
    }

    pub fn arm(&mut self) -> Result<(), ArmError> {
        if self.state == ArmState::EmergencyStopped {
            return Err(ArmError::EmergencyStopLatched);
        }

        self.state = ArmState::Armed;
        Ok(())
    }

    pub fn disarm(&mut self) {
        self.state = ArmState::Disarmed;
    }

    pub fn emergency_stop(&mut self) {
        self.state = ArmState::EmergencyStopped;
    }

    pub fn clear_emergency_stop(&mut self) {
        self.state = ArmState::Disarmed;
    }
}

impl Default for ArmingController {
    fn default() -> Self {
        Self::new()
    }
}