use crate::protocol::frame_type::FrameType;
use crate::protocol::payload_kind::PayloadKind;
use crate::protocol::sensor_kind::SensorKind;

// Hydronom Pico Sensor Frame V1
//
// Binary paket formatı:
//
// Magic          2 byte   'H' 'S'
// Version        1 byte   0x01
// FrameType      1 byte
// SensorKind     1 byte
// PayloadKind    1 byte
// NodeId         2 byte   little-endian
// Sequence       4 byte   little-endian
// TimestampUs    8 byte   little-endian
// PayloadLength  2 byte   little-endian
// Payload        N byte
// Crc32          4 byte   little-endian
//
// CRC32, Crc32 alanı hariç bütün frame byte'ları üzerinden hesaplanır.

pub const HYDRONOM_SENSOR_MAGIC_0: u8 = b'H';
pub const HYDRONOM_SENSOR_MAGIC_1: u8 = b'S';
pub const HYDRONOM_SENSOR_FRAME_VERSION: u8 = 1;

pub const HYDRONOM_SENSOR_FRAME_HEADER_LEN: usize = 22;
pub const HYDRONOM_SENSOR_FRAME_CRC_LEN: usize = 4;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SensorFrameHeader {
    pub frame_type: FrameType,
    pub sensor_kind: SensorKind,
    pub payload_kind: PayloadKind,
    pub node_id: u16,
    pub sequence: u32,
    pub timestamp_us: u64,
    pub payload_len: u16,
}

impl SensorFrameHeader {
    pub const fn new(
        frame_type: FrameType,
        sensor_kind: SensorKind,
        payload_kind: PayloadKind,
        node_id: u16,
        sequence: u32,
        timestamp_us: u64,
        payload_len: u16,
    ) -> Self {
        Self {
            frame_type,
            sensor_kind,
            payload_kind,
            node_id,
            sequence,
            timestamp_us,
            payload_len,
        }
    }
}