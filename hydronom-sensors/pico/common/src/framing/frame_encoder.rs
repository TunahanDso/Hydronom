use crate::framing::byte_writer::{ByteWriteError, ByteWriter};
use crate::framing::crc32::crc32;
use crate::protocol::sensor_frame::{
    SensorFrameHeader,
    HYDRONOM_SENSOR_FRAME_VERSION,
    HYDRONOM_SENSOR_MAGIC_0,
    HYDRONOM_SENSOR_MAGIC_1,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameEncodeError {
    BufferTooSmall,
    PayloadTooLarge,
}

impl From<ByteWriteError> for FrameEncodeError {
    fn from(_: ByteWriteError) -> Self {
        Self::BufferTooSmall
    }
}

pub fn encode_sensor_frame(
    output: &mut [u8],
    header: SensorFrameHeader,
    payload: &[u8],
) -> Result<usize, FrameEncodeError> {
    if payload.len() > u16::MAX as usize {
        return Err(FrameEncodeError::PayloadTooLarge);
    }

    let mut writer = ByteWriter::new(output);

    writer.write_u8(HYDRONOM_SENSOR_MAGIC_0)?;
    writer.write_u8(HYDRONOM_SENSOR_MAGIC_1)?;
    writer.write_u8(HYDRONOM_SENSOR_FRAME_VERSION)?;
    writer.write_u8(header.frame_type.as_u8())?;
    writer.write_u8(header.sensor_kind.as_u8())?;
    writer.write_u8(header.payload_kind.as_u8())?;

    writer.write_u16_le(header.node_id)?;
    writer.write_u32_le(header.sequence)?;
    writer.write_u64_le(header.timestamp_us)?;
    writer.write_u16_le(payload.len() as u16)?;

    writer.write_bytes(payload)?;

    let crc = crc32(writer.as_written());
    writer.write_u32_le(crc)?;

    Ok(writer.position())
}