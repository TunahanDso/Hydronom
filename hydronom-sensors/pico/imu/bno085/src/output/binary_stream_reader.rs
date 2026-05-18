use std::fs;
use std::io;
use std::path::Path;

use hydronom_sensor_pico_common::framing::frame_decoder::{
    decode_sensor_frame,
    DecodedSensorFrame,
    FrameDecodeError,
};
use hydronom_sensor_pico_common::protocol::frame_type::FrameType;
use hydronom_sensor_pico_common::protocol::payload_kind::PayloadKind;
use hydronom_sensor_pico_common::protocol::sensor_frame::{
    HYDRONOM_SENSOR_FRAME_CRC_LEN,
    HYDRONOM_SENSOR_FRAME_HEADER_LEN,
    HYDRONOM_SENSOR_MAGIC_0,
    HYDRONOM_SENSOR_MAGIC_1,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct BinaryStreamReadStats {
    pub bytes_read: usize,
    pub frames_read: u32,

    pub hello_frames: u32,
    pub capability_frames: u32,
    pub health_frames: u32,
    pub sample_frames: u32,
    pub error_frames: u32,
    pub unknown_frames: u32,

    pub skipped_bytes: u32,
    pub crc_errors: u32,
    pub length_errors: u32,

    pub first_sequence: u32,
    pub last_sequence: u32,
}

impl BinaryStreamReadStats {
    pub const fn new(bytes_read: usize) -> Self {
        Self {
            bytes_read,
            frames_read: 0,

            hello_frames: 0,
            capability_frames: 0,
            health_frames: 0,
            sample_frames: 0,
            error_frames: 0,
            unknown_frames: 0,

            skipped_bytes: 0,
            crc_errors: 0,
            length_errors: 0,

            first_sequence: 0,
            last_sequence: 0,
        }
    }

    pub fn record_frame(&mut self, decoded: &DecodedSensorFrame<'_>) {
        self.frames_read = self.frames_read.wrapping_add(1);

        if self.first_sequence == 0 {
            self.first_sequence = decoded.header.sequence;
        }

        self.last_sequence = decoded.header.sequence;

        match decoded.header.frame_type {
            FrameType::Hello => self.hello_frames = self.hello_frames.wrapping_add(1),
            FrameType::Capability => {
                self.capability_frames = self.capability_frames.wrapping_add(1)
            }
            FrameType::Health => self.health_frames = self.health_frames.wrapping_add(1),
            FrameType::Sample => self.sample_frames = self.sample_frames.wrapping_add(1),
            FrameType::Error => self.error_frames = self.error_frames.wrapping_add(1),
            FrameType::Unknown => self.unknown_frames = self.unknown_frames.wrapping_add(1),
        }
    }
}

pub fn read_and_validate_stream_file<P: AsRef<Path>>(
    path: P,
) -> io::Result<BinaryStreamReadStats> {
    let bytes = fs::read(path)?;
    Ok(read_and_validate_stream_bytes(&bytes))
}

pub fn read_and_validate_stream_bytes(bytes: &[u8]) -> BinaryStreamReadStats {
    let mut stats = BinaryStreamReadStats::new(bytes.len());
    let mut offset = 0usize;

    while offset < bytes.len() {
        let Some(magic_offset) = find_next_magic(bytes, offset) else {
            let remaining = bytes.len().saturating_sub(offset);
            stats.skipped_bytes = stats.skipped_bytes.saturating_add(remaining as u32);
            break;
        };

        if magic_offset > offset {
            let skipped = magic_offset - offset;
            stats.skipped_bytes = stats.skipped_bytes.saturating_add(skipped as u32);
        }

        offset = magic_offset;

        let remaining = bytes.len().saturating_sub(offset);

        if remaining < HYDRONOM_SENSOR_FRAME_HEADER_LEN + HYDRONOM_SENSOR_FRAME_CRC_LEN {
            stats.length_errors = stats.length_errors.wrapping_add(1);
            break;
        }

        let Some(payload_len) = read_payload_len_from_header(&bytes[offset..]) else {
            stats.length_errors = stats.length_errors.wrapping_add(1);
            offset = offset.saturating_add(1);
            continue;
        };

        let frame_len = HYDRONOM_SENSOR_FRAME_HEADER_LEN
            .saturating_add(payload_len as usize)
            .saturating_add(HYDRONOM_SENSOR_FRAME_CRC_LEN);

        if remaining < frame_len {
            stats.length_errors = stats.length_errors.wrapping_add(1);
            break;
        }

        let frame_bytes = &bytes[offset..offset + frame_len];

        match decode_sensor_frame(frame_bytes) {
            Ok(decoded) => {
                stats.record_frame(&decoded);
                offset = offset.saturating_add(frame_len);
            }
            Err(FrameDecodeError::CrcMismatch) => {
                stats.crc_errors = stats.crc_errors.wrapping_add(1);
                offset = offset.saturating_add(1);
            }
            Err(_) => {
                stats.length_errors = stats.length_errors.wrapping_add(1);
                offset = offset.saturating_add(1);
            }
        }
    }

    stats
}

fn find_next_magic(bytes: &[u8], start: usize) -> Option<usize> {
    if bytes.len() < 2 || start >= bytes.len() {
        return None;
    }

    let mut i = start;

    while i + 1 < bytes.len() {
        if bytes[i] == HYDRONOM_SENSOR_MAGIC_0 && bytes[i + 1] == HYDRONOM_SENSOR_MAGIC_1 {
            return Some(i);
        }

        i += 1;
    }

    None
}

fn read_payload_len_from_header(frame_start: &[u8]) -> Option<u16> {
    if frame_start.len() < HYDRONOM_SENSOR_FRAME_HEADER_LEN {
        return None;
    }

    // Header layout:
    // 0..2   magic
    // 2      version
    // 3      frame_type
    // 4      sensor_kind
    // 5      payload_kind
    // 6..8   node_id
    // 8..12  sequence
    // 12..20 timestamp_us
    // 20..22 payload_len
    Some(u16::from_le_bytes([frame_start[20], frame_start[21]]))
}

pub fn print_stream_read_stats(stats: &BinaryStreamReadStats) {
    println!();
    println!("BINARY STREAM READBACK:");
    println!("bytes_read: {}", stats.bytes_read);
    println!("frames_read: {}", stats.frames_read);

    println!("hello_frames: {}", stats.hello_frames);
    println!("capability_frames: {}", stats.capability_frames);
    println!("health_frames: {}", stats.health_frames);
    println!("sample_frames: {}", stats.sample_frames);
    println!("error_frames: {}", stats.error_frames);
    println!("unknown_frames: {}", stats.unknown_frames);

    println!("skipped_bytes: {}", stats.skipped_bytes);
    println!("crc_errors: {}", stats.crc_errors);
    println!("length_errors: {}", stats.length_errors);

    println!("first_sequence: {}", stats.first_sequence);
    println!("last_sequence: {}", stats.last_sequence);

    print_expected_check(stats);
}

fn print_expected_check(stats: &BinaryStreamReadStats) {
    let ok =
        stats.bytes_read == 6526
            && stats.frames_read == 64
            && stats.hello_frames == 1
            && stats.capability_frames == 1
            && stats.health_frames == 2
            && stats.sample_frames == 60
            && stats.error_frames == 0
            && stats.unknown_frames == 0
            && stats.skipped_bytes == 0
            && stats.crc_errors == 0
            && stats.length_errors == 0
            && stats.first_sequence == 1
            && stats.last_sequence == 64;

    if ok {
        println!("[OK] binary stream readback matches expected BNO085 mock stream.");
    } else {
        println!("[WARN] binary stream readback differs from expected BNO085 mock stream.");
    }
}

// Küçük yardımcı: stream içindeki payload kind dağılımını ileride genişletmek istersek
// burada kullanacağız. Şimdilik aktif ana akışta sayım FrameType üzerinden yapılıyor.
#[allow(dead_code)]
fn _payload_kind_name(kind: PayloadKind) -> &'static str {
    match kind {
        PayloadKind::NodeHello => "NodeHello",
        PayloadKind::NodeCapability => "NodeCapability",
        PayloadKind::NodeHealth => "NodeHealth",
        PayloadKind::NodeError => "NodeError",
        PayloadKind::ImuFusionQuaternion => "ImuFusionQuaternion",
        PayloadKind::ImuRaw9Dof => "ImuRaw9Dof",
        PayloadKind::ImuCombined10Dof => "ImuCombined10Dof",
        PayloadKind::ImuBarometer => "ImuBarometer",
        PayloadKind::Unknown => "Unknown",
    }
}