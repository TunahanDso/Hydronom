// BNO085 / BNO08X UART-RVC packet parser.
//
// UART-RVC paket formatı:
// byte 0      0xAA
// byte 1      0xAA
// byte 2      index
// byte 3..4   yaw    i16 little-endian, 0.01 degree
// byte 5..6   pitch  i16 little-endian, 0.01 degree
// byte 7..8   roll   i16 little-endian, 0.01 degree
// byte 9..10  accel x i16 little-endian, mg
// byte 11..12 accel y i16 little-endian, mg
// byte 13..14 accel z i16 little-endian, mg
// byte 15     MI / reserved
// byte 16     MR / reserved
// byte 17     reserved
// byte 18     checksum
//
// Checksum: byte 2..17 arası unsigned wrapping toplam.
// Header checksum'a dahil değildir.

pub const BNO085_RVC_PACKET_LEN: usize = 19;
pub const BNO085_RVC_HEADER_0: u8 = 0xAA;
pub const BNO085_RVC_HEADER_1: u8 = 0xAA;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Bno085RvcParseError {
    TooShort,
    InvalidHeader,
    ChecksumMismatch {
        expected: u8,
        actual: u8,
    },
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Bno085RvcPacket {
    pub index: u8,

    // Derece cinsinden yaw/pitch/roll.
    pub yaw_deg: f32,
    pub pitch_deg: f32,
    pub roll_deg: f32,

    // m/s^2 cinsinden ivme.
    pub accel_x_mps2: f32,
    pub accel_y_mps2: f32,
    pub accel_z_mps2: f32,

    // BNO086 için motion intent/request; BNO085 tarafında genelde reserved kabul edilir.
    pub motion_intent: u8,
    pub motion_request: u8,
    pub reserved: u8,

    pub checksum: u8,
}

impl Bno085RvcPacket {
    pub fn parse(bytes: &[u8]) -> Result<Self, Bno085RvcParseError> {
        if bytes.len() < BNO085_RVC_PACKET_LEN {
            return Err(Bno085RvcParseError::TooShort);
        }

        let packet = &bytes[..BNO085_RVC_PACKET_LEN];

        if packet[0] != BNO085_RVC_HEADER_0 || packet[1] != BNO085_RVC_HEADER_1 {
            return Err(Bno085RvcParseError::InvalidHeader);
        }

        let expected = compute_checksum(packet);
        let actual = packet[18];

        if expected != actual {
            return Err(Bno085RvcParseError::ChecksumMismatch {
                expected,
                actual,
            });
        }

        let index = packet[2];

        let yaw_raw = read_i16_le(packet, 3);
        let pitch_raw = read_i16_le(packet, 5);
        let roll_raw = read_i16_le(packet, 7);

        let accel_x_raw = read_i16_le(packet, 9);
        let accel_y_raw = read_i16_le(packet, 11);
        let accel_z_raw = read_i16_le(packet, 13);

        Ok(Self {
            index,

            yaw_deg: centi_degree_to_degree(yaw_raw),
            pitch_deg: centi_degree_to_degree(pitch_raw),
            roll_deg: centi_degree_to_degree(roll_raw),

            accel_x_mps2: milli_g_to_mps2(accel_x_raw),
            accel_y_mps2: milli_g_to_mps2(accel_y_raw),
            accel_z_mps2: milli_g_to_mps2(accel_z_raw),

            motion_intent: packet[15],
            motion_request: packet[16],
            reserved: packet[17],

            checksum: actual,
        })
    }
}

pub fn compute_checksum(packet: &[u8]) -> u8 {
    let mut sum = 0u8;

    // byte 2..17 dahil.
    let mut i = 2usize;
    while i <= 17 && i < packet.len() {
        sum = sum.wrapping_add(packet[i]);
        i += 1;
    }

    sum
}

pub fn find_rvc_header(bytes: &[u8], start: usize) -> Option<usize> {
    if bytes.len() < 2 || start >= bytes.len() {
        return None;
    }

    let mut i = start;

    while i + 1 < bytes.len() {
        if bytes[i] == BNO085_RVC_HEADER_0 && bytes[i + 1] == BNO085_RVC_HEADER_1 {
            return Some(i);
        }

        i += 1;
    }

    None
}

fn read_i16_le(bytes: &[u8], offset: usize) -> i16 {
    i16::from_le_bytes([bytes[offset], bytes[offset + 1]])
}

fn centi_degree_to_degree(value: i16) -> f32 {
    value as f32 * 0.01
}

fn milli_g_to_mps2(value: i16) -> f32 {
    // 1 g = 9.80665 m/s^2
    // 1 mg = 0.001 g
    value as f32 * 0.009_806_65
}