// Hydronom MCU - Parser
//
// MİMARİ NOT:
// Bu parser iki katmana hazırlanır:
// 1. Binary frame parser: Runtime ile güvenilir, CRC'li haberleşme için.
// 2. Text/debug parser: İlk testlerde seri terminalden komut basmak için.
//
// Bu dosyada şu an binary frame -> HydronomCommand dönüşümü kurulur.
// Text parser daha sonra USB CDC bağlanırken eklenecek.

use crate::config::actuator_profile::MAX_ACTUATORS;
use crate::protocol::commands::{ActuatorSetpoint, HydronomCommand, EMPTY_SETPOINT};
use crate::protocol::frame::{FrameError, FrameType, HydronomFrame};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ParseError {
    Frame(FrameError),
    BadPayloadLength,
    TooManyActuators,
    UnknownFrameType,
}

impl From<FrameError> for ParseError {
    fn from(value: FrameError) -> Self {
        Self::Frame(value)
    }
}

pub fn parse_frame(input: &[u8]) -> Result<HydronomCommand, ParseError> {
    let frame = HydronomFrame::decode(input)?;
    frame_to_command(&frame)
}

pub fn frame_to_command(frame: &HydronomFrame) -> Result<HydronomCommand, ParseError> {
    match frame.frame_type {
        FrameType::Ping => Ok(HydronomCommand::Ping),

        FrameType::Heartbeat => {
            require_empty_payload(frame)?;
            Ok(HydronomCommand::Heartbeat { seq: frame.seq })
        }

        FrameType::Arm => {
            require_empty_payload(frame)?;
            Ok(HydronomCommand::Arm { seq: frame.seq })
        }

        FrameType::Disarm => {
            require_empty_payload(frame)?;
            Ok(HydronomCommand::Disarm { seq: frame.seq })
        }

        FrameType::ClearEmergencyStop => {
            require_empty_payload(frame)?;
            Ok(HydronomCommand::ClearEmergencyStop { seq: frame.seq })
        }

        FrameType::EmergencyStop => {
            require_empty_payload(frame)?;
            Ok(HydronomCommand::EmergencyStop { seq: frame.seq })
        }

        FrameType::ActuatorBatch => parse_actuator_batch(frame),

        FrameType::Telemetry | FrameType::Ack | FrameType::Nack => Err(ParseError::UnknownFrameType),
    }
}

fn require_empty_payload(frame: &HydronomFrame) -> Result<(), ParseError> {
    if frame.payload_len != 0 {
        return Err(ParseError::BadPayloadLength);
    }

    Ok(())
}

fn parse_actuator_batch(frame: &HydronomFrame) -> Result<HydronomCommand, ParseError> {
    let payload = frame.payload();

    if payload.is_empty() {
        return Err(ParseError::BadPayloadLength);
    }

    let count = payload[0] as usize;

    if count > MAX_ACTUATORS {
        return Err(ParseError::TooManyActuators);
    }

    let expected_len = 1 + count * 3;

    if payload.len() != expected_len {
        return Err(ParseError::BadPayloadLength);
    }

    let mut setpoints = [EMPTY_SETPOINT; MAX_ACTUATORS];

    for index in 0..count {
        let offset = 1 + index * 3;

        let actuator_id = payload[offset];
        let raw_value = payload[offset + 1] as u16 | ((payload[offset + 2] as u16) << 8);
        let value = raw_value as i16;

        setpoints[index] = ActuatorSetpoint { actuator_id, value };
    }

    Ok(HydronomCommand::ActuatorBatch {
        seq: frame.seq,
        count,
        setpoints,
    })
}