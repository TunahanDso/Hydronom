use crate::framing::byte_reader::{ByteReadError, ByteReader};
use crate::framing::byte_writer::{ByteWriteError, ByteWriter};
use crate::node::node_identity::NodeRole;
use crate::protocol::sensor_kind::SensorKind;

// HELLO payload formatı:
//
// node_id             u16
// sensor_kind         u8
// role                u8
// node_name           fixed ascii 32
// firmware_name       fixed ascii 48
// firmware_version    fixed ascii 16
// sensor_model        fixed ascii 48
// sensor_vendor       fixed ascii 48
//
// Toplam: 2 + 1 + 1 + 32 + 48 + 16 + 48 + 48 = 196 byte

use crate::node::node_identity::NodeIdentity;

pub const NODE_NAME_LEN: usize = 32;
pub const FIRMWARE_NAME_LEN: usize = 48;
pub const FIRMWARE_VERSION_LEN: usize = 16;
pub const SENSOR_MODEL_LEN: usize = 48;
pub const SENSOR_VENDOR_LEN: usize = 48;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct DecodedNodeHello<'a> {
    pub node_id: u16,
    pub sensor_kind: SensorKind,
    pub role: NodeRole,

    pub node_name: &'a [u8],
    pub firmware_name: &'a [u8],
    pub firmware_version: &'a [u8],
    pub sensor_model: &'a [u8],
    pub sensor_vendor: &'a [u8],
}

pub fn encode_node_hello_payload(
    writer: &mut ByteWriter<'_>,
    identity: &NodeIdentity,
) -> Result<(), ByteWriteError> {
    writer.write_u16_le(identity.node_id)?;
    writer.write_u8(identity.sensor_kind.as_u8())?;
    writer.write_u8(identity.role as u8)?;

    writer.write_fixed_ascii::<NODE_NAME_LEN>(identity.node_name)?;
    writer.write_fixed_ascii::<FIRMWARE_NAME_LEN>(identity.firmware_name)?;
    writer.write_fixed_ascii::<FIRMWARE_VERSION_LEN>(identity.firmware_version)?;
    writer.write_fixed_ascii::<SENSOR_MODEL_LEN>(identity.sensor_model)?;
    writer.write_fixed_ascii::<SENSOR_VENDOR_LEN>(identity.sensor_vendor)?;

    Ok(())
}

pub fn decode_node_hello_payload<'a>(
    payload: &'a [u8],
) -> Result<DecodedNodeHello<'a>, ByteReadError> {
    let mut reader = ByteReader::new(payload);

    let node_id = reader.read_u16_le()?;
    let sensor_kind = SensorKind::from_u8(reader.read_u8()?);
    let role = decode_node_role(reader.read_u8()?);

    let node_name = trim_fixed_ascii(reader.read_bytes(NODE_NAME_LEN)?);
    let firmware_name = trim_fixed_ascii(reader.read_bytes(FIRMWARE_NAME_LEN)?);
    let firmware_version = trim_fixed_ascii(reader.read_bytes(FIRMWARE_VERSION_LEN)?);
    let sensor_model = trim_fixed_ascii(reader.read_bytes(SENSOR_MODEL_LEN)?);
    let sensor_vendor = trim_fixed_ascii(reader.read_bytes(SENSOR_VENDOR_LEN)?);

    Ok(DecodedNodeHello {
        node_id,
        sensor_kind,
        role,
        node_name,
        firmware_name,
        firmware_version,
        sensor_model,
        sensor_vendor,
    })
}

pub fn decode_node_role(value: u8) -> NodeRole {
    match value {
        1 => NodeRole::PrimaryFusedOrientation,
        2 => NodeRole::RawImuReference,
        3 => NodeRole::BackupSensor,
        4 => NodeRole::DiagnosticsOnly,
        _ => NodeRole::Unknown,
    }
}

pub fn trim_fixed_ascii(bytes: &[u8]) -> &[u8] {
    let mut end = bytes.len();

    while end > 0 && bytes[end - 1] == 0 {
        end -= 1;
    }

    &bytes[..end]
}