use crate::framing::byte_reader::{ByteReadError, ByteReader};
use crate::framing::crc32::crc32;
use crate::protocol::frame_type::FrameType;
use crate::protocol::payload_kind::PayloadKind;
use crate::protocol::sensor_frame::{
    SensorFrameHeader,
    HYDRONOM_SENSOR_FRAME_CRC_LEN,
    HYDRONOM_SENSOR_FRAME_HEADER_LEN,
    HYDRONOM_SENSOR_FRAME_VERSION,
    HYDRONOM_SENSOR_MAGIC_0,
    HYDRONOM_SENSOR_MAGIC_1,
};
use crate::protocol::sensor_kind::SensorKind;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameDecodeError {
    TooShort,
    InvalidMagic,
    UnsupportedVersion,
    PayloadLengthMismatch,
    CrcMismatch,
    UnexpectedEnd,
}

impl From<ByteReadError> for FrameDecodeError {
    fn from(_: ByteReadError) -> Self {
        Self::UnexpectedEnd
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct DecodedSensorFrame<'a> {
    pub header: SensorFrameHeader,
    pub payload: &'a [u8],
    pub received_crc32: u32,
    pub computed_crc32: u32,
}

pub fn decode_sensor_frame(
    input: &[u8],
) -> Result<DecodedSensorFrame<'_>, FrameDecodeError> {
    if input.len() < HYDRONOM_SENSOR_FRAME_HEADER_LEN + HYDRONOM_SENSOR_FRAME_CRC_LEN {
        return Err(FrameDecodeError::TooShort);
    }

    let received_crc_offset = input.len() - HYDRONOM_SENSOR_FRAME_CRC_LEN;
    let frame_without_crc = &input[..received_crc_offset];
    let received_crc_bytes = &input[received_crc_offset..];

    let received_crc32 = u32::from_le_bytes([
        received_crc_bytes[0],
        received_crc_bytes[1],
        received_crc_bytes[2],
        received_crc_bytes[3],
    ]);

    let computed_crc32 = crc32(frame_without_crc);

    if received_crc32 != computed_crc32 {
        return Err(FrameDecodeError::CrcMismatch);
    }

    let mut reader = ByteReader::new(frame_without_crc);

    let magic0 = reader.read_u8()?;
    let magic1 = reader.read_u8()?;

    if magic0 != HYDRONOM_SENSOR_MAGIC_0 || magic1 != HYDRONOM_SENSOR_MAGIC_1 {
        return Err(FrameDecodeError::InvalidMagic);
    }

    let version = reader.read_u8()?;

    if version != HYDRONOM_SENSOR_FRAME_VERSION {
        return Err(FrameDecodeError::UnsupportedVersion);
    }

    let frame_type = FrameType::from_u8(reader.read_u8()?);
    let sensor_kind = SensorKind::from_u8(reader.read_u8()?);
    let payload_kind = PayloadKind::from_u8(reader.read_u8()?);

    let node_id = reader.read_u16_le()?;
    let sequence = reader.read_u32_le()?;
    let timestamp_us = reader.read_u64_le()?;
    let payload_len = reader.read_u16_le()?;

    if reader.remaining() != payload_len as usize {
        return Err(FrameDecodeError::PayloadLengthMismatch);
    }

    let payload = reader.read_bytes(payload_len as usize)?;

    let header = SensorFrameHeader::new(
        frame_type,
        sensor_kind,
        payload_kind,
        node_id,
        sequence,
        timestamp_us,
        payload_len,
    );

    Ok(DecodedSensorFrame {
        header,
        payload,
        received_crc32,
        computed_crc32,
    })
}