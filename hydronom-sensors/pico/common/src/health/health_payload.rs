use crate::framing::byte_reader::{ByteReadError, ByteReader};
use crate::framing::byte_writer::{ByteWriteError, ByteWriter};
use crate::health::node_health::{NodeHealth, NodeHealthState};

// HEALTH payload formatı:
//
// state              u8
// uptime_ms          u32
// sample_count       u32
// read_error_count   u32
// bus_error_count    u32
// last_error_code    u16
//
// Toplam: 19 byte

pub fn encode_health_payload(
    writer: &mut ByteWriter<'_>,
    health: &NodeHealth,
) -> Result<(), ByteWriteError> {
    writer.write_u8(health.state as u8)?;
    writer.write_u32_le(health.uptime_ms)?;
    writer.write_u32_le(health.sample_count)?;
    writer.write_u32_le(health.read_error_count)?;
    writer.write_u32_le(health.bus_error_count)?;
    writer.write_u16_le(health.last_error_code)?;

    Ok(())
}

pub fn decode_health_payload(
    payload: &[u8],
) -> Result<NodeHealth, ByteReadError> {
    let mut reader = ByteReader::new(payload);

    let state = decode_health_state(reader.read_u8()?);
    let uptime_ms = reader.read_u32_le()?;
    let sample_count = reader.read_u32_le()?;
    let read_error_count = reader.read_u32_le()?;
    let bus_error_count = reader.read_u32_le()?;
    let last_error_code = reader.read_u16_le()?;

    Ok(NodeHealth {
        state,
        uptime_ms,
        sample_count,
        read_error_count,
        bus_error_count,
        last_error_code,
    })
}

pub fn decode_health_state(value: u8) -> NodeHealthState {
    match value {
        1 => NodeHealthState::Booting,
        2 => NodeHealthState::Healthy,
        3 => NodeHealthState::Degraded,
        4 => NodeHealthState::SensorMissing,
        5 => NodeHealthState::BusError,
        6 => NodeHealthState::Fatal,
        _ => NodeHealthState::Unknown,
    }
}