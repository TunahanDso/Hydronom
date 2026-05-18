use hydronom_sensor_pico_common::framing::byte_writer::ByteWriter;
use hydronom_sensor_pico_common::framing::frame_encoder::encode_sensor_frame;
use hydronom_sensor_pico_common::health::health_payload::encode_health_payload;
use hydronom_sensor_pico_common::health::node_health::NodeHealth;
use hydronom_sensor_pico_common::imu::imu_sample::ImuFusionSample;
use hydronom_sensor_pico_common::node::node_identity::NodeIdentity;
use hydronom_sensor_pico_common::node::node_payload::encode_node_hello_payload;
use hydronom_sensor_pico_common::protocol::capability::SensorCapability;
use hydronom_sensor_pico_common::protocol::capability_payload::encode_capability_payload;
use hydronom_sensor_pico_common::protocol::frame_type::FrameType;
use hydronom_sensor_pico_common::protocol::payload_kind::PayloadKind;
use hydronom_sensor_pico_common::protocol::sensor_frame::SensorFrameHeader;

pub type SensorFrameBytes = heapless::Vec<u8, 256>;

pub fn build_hello_frame(
    identity: &NodeIdentity,
    sequence: u32,
    timestamp_us: u64,
) -> SensorFrameBytes {
    let mut payload_buffer = [0u8; 224];
    let mut payload_writer = ByteWriter::new(&mut payload_buffer);

    encode_node_hello_payload(&mut payload_writer, identity)
        .expect("hello payload encode failed");

    build_frame(
        identity,
        FrameType::Hello,
        PayloadKind::NodeHello,
        sequence,
        timestamp_us,
        payload_writer.as_written(),
    )
}

pub fn build_capability_frame(
    identity: &NodeIdentity,
    capability: &SensorCapability,
    sequence: u32,
    timestamp_us: u64,
) -> SensorFrameBytes {
    let mut payload_buffer = [0u8; 32];
    let mut payload_writer = ByteWriter::new(&mut payload_buffer);

    encode_capability_payload(&mut payload_writer, capability)
        .expect("capability payload encode failed");

    build_frame(
        identity,
        FrameType::Capability,
        PayloadKind::NodeCapability,
        sequence,
        timestamp_us,
        payload_writer.as_written(),
    )
}

pub fn build_health_frame(
    identity: &NodeIdentity,
    health: &NodeHealth,
    sequence: u32,
    timestamp_us: u64,
) -> SensorFrameBytes {
    let mut payload_buffer = [0u8; 32];
    let mut payload_writer = ByteWriter::new(&mut payload_buffer);

    encode_health_payload(&mut payload_writer, health)
        .expect("health payload encode failed");

    build_frame(
        identity,
        FrameType::Health,
        PayloadKind::NodeHealth,
        sequence,
        timestamp_us,
        payload_writer.as_written(),
    )
}

pub fn build_sample_frame(
    identity: &NodeIdentity,
    sample: &ImuFusionSample,
    sequence: u32,
) -> SensorFrameBytes {
    let mut payload_buffer = [0u8; 128];
    let mut payload_writer = ByteWriter::new(&mut payload_buffer);

    sample
        .encode_into(&mut payload_writer)
        .expect("sample payload encode failed");

    build_frame(
        identity,
        FrameType::Sample,
        PayloadKind::ImuFusionQuaternion,
        sequence,
        sample.timestamp_us,
        payload_writer.as_written(),
    )
}

pub fn build_frame(
    identity: &NodeIdentity,
    frame_type: FrameType,
    payload_kind: PayloadKind,
    sequence: u32,
    timestamp_us: u64,
    payload: &[u8],
) -> SensorFrameBytes {
    let header = SensorFrameHeader::new(
        frame_type,
        identity.sensor_kind,
        payload_kind,
        identity.node_id,
        sequence,
        timestamp_us,
        payload.len() as u16,
    );

    let mut frame_buffer = [0u8; 256];

    let frame_len = encode_sensor_frame(
        &mut frame_buffer,
        header,
        payload,
    )
    .expect("sensor frame encode failed");

    let mut frame = heapless::Vec::<u8, 256>::new();

    for byte in &frame_buffer[..frame_len] {
        frame.push(*byte).expect("frame vec capacity exceeded");
    }

    frame
}