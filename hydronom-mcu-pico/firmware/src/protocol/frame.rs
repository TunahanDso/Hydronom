// Hydronom MCU - Protocol Frame
//
// MİMARİ NOT:
// Bu dosya binary frame protokolünün ortak modelini taşır.
// USB CDC debug/text parser ayrı olabilir; fakat uzun vadede Runtime <-> MCU
// haberleşmesinde CRC'li frame yapısı esas alınacaktır.
//
// Frame felsefesi:
// - Magic byte ile senkronizasyon
// - Type ile komut/telemetry ayrımı
// - Sequence ile ACK/diagnostics takibi
// - Payload length ile güvenli parse
// - CRC ile veri bütünlüğü

use crate::protocol::crc::crc16_ccitt_false;

pub const HYDRONOM_FRAME_MAGIC_A: u8 = 0x48; // 'H'
pub const HYDRONOM_FRAME_MAGIC_B: u8 = 0x59; // 'Y'

pub const HYDRONOM_PROTOCOL_VERSION: u8 = 1;
pub const HYDRONOM_MAX_PAYLOAD_LEN: usize = 96;

pub const HYDRONOM_FRAME_HEADER_LEN: usize = 8;
pub const HYDRONOM_FRAME_CRC_LEN: usize = 2;
pub const HYDRONOM_MAX_FRAME_LEN: usize =
    HYDRONOM_FRAME_HEADER_LEN + HYDRONOM_MAX_PAYLOAD_LEN + HYDRONOM_FRAME_CRC_LEN;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameType {
    Ping = 1,
    Heartbeat = 2,
    Arm = 3,
    Disarm = 4,
    ClearEmergencyStop = 5,
    EmergencyStop = 6,
    ActuatorBatch = 7,
    Telemetry = 20,
    Ack = 21,
    Nack = 22,
}

impl FrameType {
    pub fn from_u8(value: u8) -> Option<Self> {
        match value {
            1 => Some(Self::Ping),
            2 => Some(Self::Heartbeat),
            3 => Some(Self::Arm),
            4 => Some(Self::Disarm),
            5 => Some(Self::ClearEmergencyStop),
            6 => Some(Self::EmergencyStop),
            7 => Some(Self::ActuatorBatch),
            20 => Some(Self::Telemetry),
            21 => Some(Self::Ack),
            22 => Some(Self::Nack),
            _ => None,
        }
    }

    pub fn as_u8(self) -> u8 {
        self as u8
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameError {
    TooShort,
    BadMagic,
    UnsupportedVersion,
    UnknownFrameType,
    PayloadTooLarge,
    LengthMismatch,
    CrcMismatch,
}

#[derive(Clone, Copy, Debug)]
pub struct HydronomFrame {
    pub frame_type: FrameType,
    pub seq: u32,
    pub payload_len: usize,
    pub payload: [u8; HYDRONOM_MAX_PAYLOAD_LEN],
}

impl HydronomFrame {
    pub const fn empty(frame_type: FrameType, seq: u32) -> Self {
        Self {
            frame_type,
            seq,
            payload_len: 0,
            payload: [0; HYDRONOM_MAX_PAYLOAD_LEN],
        }
    }

    pub fn with_payload(
        frame_type: FrameType,
        seq: u32,
        payload_bytes: &[u8],
    ) -> Result<Self, FrameError> {
        if payload_bytes.len() > HYDRONOM_MAX_PAYLOAD_LEN {
            return Err(FrameError::PayloadTooLarge);
        }

        let mut payload = [0u8; HYDRONOM_MAX_PAYLOAD_LEN];
        payload[..payload_bytes.len()].copy_from_slice(payload_bytes);

        Ok(Self {
            frame_type,
            seq,
            payload_len: payload_bytes.len(),
            payload,
        })
    }

    pub fn payload(&self) -> &[u8] {
        &self.payload[..self.payload_len]
    }

    pub fn encoded_len(&self) -> usize {
        HYDRONOM_FRAME_HEADER_LEN + self.payload_len + HYDRONOM_FRAME_CRC_LEN
    }

    pub fn encode(&self, out: &mut [u8]) -> Result<usize, FrameError> {
        let total_len = self.encoded_len();

        if out.len() < total_len {
            return Err(FrameError::PayloadTooLarge);
        }

        out[0] = HYDRONOM_FRAME_MAGIC_A;
        out[1] = HYDRONOM_FRAME_MAGIC_B;
        out[2] = HYDRONOM_PROTOCOL_VERSION;
        out[3] = self.frame_type.as_u8();

        out[4] = (self.seq & 0xFF) as u8;
        out[5] = ((self.seq >> 8) & 0xFF) as u8;
        out[6] = ((self.seq >> 16) & 0xFF) as u8;
        out[7] = self.payload_len as u8;

        let payload_start = HYDRONOM_FRAME_HEADER_LEN;
        let payload_end = payload_start + self.payload_len;

        out[payload_start..payload_end].copy_from_slice(self.payload());

        let crc = crc16_ccitt_false(&out[..payload_end]);

        out[payload_end] = (crc & 0xFF) as u8;
        out[payload_end + 1] = ((crc >> 8) & 0xFF) as u8;

        Ok(total_len)
    }

    pub fn decode(input: &[u8]) -> Result<Self, FrameError> {
        if input.len() < HYDRONOM_FRAME_HEADER_LEN + HYDRONOM_FRAME_CRC_LEN {
            return Err(FrameError::TooShort);
        }

        if input[0] != HYDRONOM_FRAME_MAGIC_A || input[1] != HYDRONOM_FRAME_MAGIC_B {
            return Err(FrameError::BadMagic);
        }

        if input[2] != HYDRONOM_PROTOCOL_VERSION {
            return Err(FrameError::UnsupportedVersion);
        }

        let Some(frame_type) = FrameType::from_u8(input[3]) else {
            return Err(FrameError::UnknownFrameType);
        };

        let seq = input[4] as u32 | ((input[5] as u32) << 8) | ((input[6] as u32) << 16);
        let payload_len = input[7] as usize;

        if payload_len > HYDRONOM_MAX_PAYLOAD_LEN {
            return Err(FrameError::PayloadTooLarge);
        }

        let expected_len = HYDRONOM_FRAME_HEADER_LEN + payload_len + HYDRONOM_FRAME_CRC_LEN;

        if input.len() != expected_len {
            return Err(FrameError::LengthMismatch);
        }

        let crc_index = HYDRONOM_FRAME_HEADER_LEN + payload_len;
        let expected_crc = input[crc_index] as u16 | ((input[crc_index + 1] as u16) << 8);
        let actual_crc = crc16_ccitt_false(&input[..crc_index]);

        if actual_crc != expected_crc {
            return Err(FrameError::CrcMismatch);
        }

        let mut payload = [0u8; HYDRONOM_MAX_PAYLOAD_LEN];
        let payload_start = HYDRONOM_FRAME_HEADER_LEN;
        let payload_end = payload_start + payload_len;

        payload[..payload_len].copy_from_slice(&input[payload_start..payload_end]);

        Ok(Self {
            frame_type,
            seq,
            payload_len,
            payload,
        })
    }
}