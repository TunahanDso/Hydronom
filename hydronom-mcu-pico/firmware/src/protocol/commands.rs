// Hydronom MCU - Commands
//
// MİMARİ NOT:
// Protokol motor sayısına sabitlenmez.
// Bunun yerine actuator bank mantığı kullanılır.
//
// ACT_BATCH:
// Aynı frame içinde birden fazla actuator komutu taşır.
// Böylece tekne 4 ESC, su altı 8 thruster, roket servo/pyro gibi farklı
// çıkışları aynı modelle kontrol edebilir.

use crate::config::actuator_profile::MAX_ACTUATORS;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ActuatorSetpoint {
    pub actuator_id: u8,
    pub value: i16,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum HydronomCommand {
    Ping,
    Heartbeat {
        seq: u32,
    },
    Arm {
        seq: u32,
    },
    Disarm {
        seq: u32,
    },
    ClearEmergencyStop {
        seq: u32,
    },
    EmergencyStop {
        seq: u32,
    },
    ActuatorBatch {
        seq: u32,
        count: usize,
        setpoints: [ActuatorSetpoint; MAX_ACTUATORS],
    },
}

impl HydronomCommand {
    pub fn seq(&self) -> Option<u32> {
        match *self {
            HydronomCommand::Ping => None,
            HydronomCommand::Heartbeat { seq } => Some(seq),
            HydronomCommand::Arm { seq } => Some(seq),
            HydronomCommand::Disarm { seq } => Some(seq),
            HydronomCommand::ClearEmergencyStop { seq } => Some(seq),
            HydronomCommand::EmergencyStop { seq } => Some(seq),
            HydronomCommand::ActuatorBatch { seq, .. } => Some(seq),
        }
    }
}

pub const EMPTY_SETPOINT: ActuatorSetpoint = ActuatorSetpoint {
    actuator_id: 0,
    value: 0,
};