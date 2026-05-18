use crate::framing::byte_reader::{ByteReadError, ByteReader};
use crate::framing::byte_writer::{ByteWriteError, ByteWriter};
use crate::protocol::capability::SensorCapability;

// CAPABILITY payload formatı:
//
// capability_bits       u32
// recommended_rate_hz   u16
// max_rate_hz           u16
//
// Toplam: 8 byte

pub fn encode_capability_payload(
    writer: &mut ByteWriter<'_>,
    capability: &SensorCapability,
) -> Result<(), ByteWriteError> {
    writer.write_u32_le(capability.capability_bits)?;
    writer.write_u16_le(capability.recommended_rate_hz)?;
    writer.write_u16_le(capability.max_rate_hz)?;

    Ok(())
}

pub fn decode_capability_payload(
    payload: &[u8],
) -> Result<SensorCapability, ByteReadError> {
    let mut reader = ByteReader::new(payload);

    let capability_bits = reader.read_u32_le()?;
    let recommended_rate_hz = reader.read_u16_le()?;
    let max_rate_hz = reader.read_u16_le()?;

    Ok(SensorCapability {
        capability_bits,
        recommended_rate_hz,
        max_rate_hz,
    })
}